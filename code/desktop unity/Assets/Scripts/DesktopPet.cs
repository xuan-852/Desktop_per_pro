using System.Threading;
using UnityEngine;

/// <summary>
/// 桌面宠物 — 主控脚本
///
/// 职责：
/// 1. 管理宠物的物理状态 (位置、速度、尺寸)
/// 2. 驱动物理步进（重力、碰撞、落地检测）
/// 3. 管理地面状态机（行走、停止等任务）
/// 4. 协调 DragHandler、TouchController 等交互模块
///
/// v2 架构设计：
/// - 分离交互逻辑到独立的 Handler 脚本
/// - 用 IPetRenderer 接口抽象渲染层
/// - 状态机可扩展，方便添加新行为
/// </summary>
public class DesktopPet : MonoBehaviour
{
    // 单例互斥锁：防止多个实例同时运行
    private static Mutex _instanceMutex = null;
    private const string MutexName = "DesktopPet_Unity_SingleInstance";

    // ================ 可调参数（改这里）================
    const int GROUND_Y_MARGIN     = -300;    // 地面距屏幕底部距离（像素），负数=往下调，正数=往上调
    const float WALK_SPEED_FACTOR = 0.5f;    // 移动速度系数（1=正常，0.5=一半）
    // ==================================================

    #region 物理状态

    [Header("物理属性")]
    [Tooltip("宠物初始X位置")]
    public int startX = 50;

    [Tooltip("宠物初始Y位置，-1=屏幕底部")]
    public int startY = -1;

    [Tooltip("重力加速度（像素/帧²）")]
    public int gravity = 1;

    [Tooltip("最大下落速度（像素/帧）")]
    public int maxFallSpeed = 8;

    [Tooltip("水平速度范围")]
    public int maxHorizontalSpeed = 12;

    // 宠物物理状态（仿 v1 PetState）
    [System.NonSerialized]
    public int petX;
    [System.NonSerialized]
    public int petY;
    [System.NonSerialized]
    public int petVx;
    [System.NonSerialized]
    public int petVy;
    [System.NonSerialized]
    public int petWidth;
    [System.NonSerialized]
    public int petHeight;

    [System.NonSerialized]
    public bool onGround = false;

    [System.NonSerialized]
    public bool isPaused = false;

    [System.NonSerialized]
    public bool isDragging = false;

    // 屏幕尺寸（动态获取，不缓存）
    private int _screenWidth => Screen.width;
    private int _screenHeight => Screen.height;

    #endregion

    #region 地面任务状态机

    /// <summary>
    /// 地面行为枚举（与 v1 GroundTask 对应）
    /// </summary>
    public enum GroundTask
    {
        None,
        MoveLeftEdge,      // 向左走到边缘
        MoveRightEdge,     // 向右走到边缘
        MoveLeftTime,      // 向左走固定时长
        MoveRightTime,     // 向右走固定时长
        StopTime           // 停止固定时长
    }

    [Header("地面任务配置")]
    [Tooltip("向左走到边缘权重")]
    public int taskWeightMoveLeftEdge = 1;
    [Tooltip("向右走到边缘权重")]
    public int taskWeightMoveRightEdge = 1;
    [Tooltip("向左走定时权重")]
    public int taskWeightMoveLeftTime = 1;
    [Tooltip("向右走定时权重")]
    public int taskWeightMoveRightTime = 1;
    [Tooltip("停止定时权重")]
    public int taskWeightStopTime = 6;

    [Tooltip("地面任务移动最短时间（毫秒）")]
    public int taskMoveTimeMinMs = 2000;

    [Tooltip("地面任务移动最长时间（毫秒）")]
    public int taskMoveTimeMaxMs = 4000;

    [Tooltip("停止最短时间（毫秒）")]
    public int taskStopTimeMinMs = 6000;

    [Tooltip("停止最长时间（毫秒）")]
    public int taskStopTimeMaxMs = 15000;

    [System.NonSerialized]
    public GroundTask currentTask = GroundTask.None;

    [System.NonSerialized]
    public GroundTask lastTask = GroundTask.None;

    private float _taskEndTime = 0f;

    #endregion

    #region 组件引用

    private WindowOverlay _windowOverlay;
    private IPetRenderer _renderer;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        // ---- 单例互斥锁：防止 Build and Run 产生多个实例 ----
        try
        {
            bool createdNew;
            _instanceMutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                Debug.LogWarning("[DesktopPet] 检测到已有实例在运行，立即退出当前实例");

                // 在构建版中，立即终止进程（不等帧结束）
                if (!Application.isEditor)
                {
                    System.Environment.Exit(0);
                }
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                return; // 安全保底
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DesktopPet] 互斥锁创建失败（通常无害）: {ex.Message}");
        }

        // 防重复：如果已经有一个 DesktopPet 了，这个自毁
        DesktopPet[] all = FindObjectsOfType<DesktopPet>();
        if (all.Length > 1)
        {
            Debug.LogWarning("[DesktopPet] 检测到多个实例，自毁中");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // ---- 限制帧率：降低 CPU/GPU 占用 ----
#if !UNITY_EDITOR
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Debug.Log($"[DesktopPet] 帧率限制: {Application.targetFrameRate}fps");
#endif

        // 初始化物理状态
        petX = startX;
        int groundFloor = _screenHeight + GROUND_Y_MARGIN;
        petY = startY >= 0 ? startY : (groundFloor - petHeight);
        petVx = 0;
        petVy = 0;
        petWidth = 100;
        petHeight = 170;

        // 强制落地检测
        if (petY + petHeight >= groundFloor)
        {
            petY = groundFloor - petHeight;
            onGround = true;
        }

        // 自动确保 WindowOverlay 存在
        _windowOverlay = GetComponent<WindowOverlay>();
        if (_windowOverlay == null)
        {
            _windowOverlay = gameObject.AddComponent<WindowOverlay>();
            Debug.Log("[DesktopPet] 自动添加了 WindowOverlay 组件");
        }

        // 自动确保 DragHandler 存在
        if (GetComponent<DragHandler>() == null)
        {
            gameObject.AddComponent<DragHandler>();
            Debug.Log("[DesktopPet] 自动添加了 DragHandler 组件");
        }

        // 自动确保 HybridRenderer 存在
        if (GetComponent<HybridRenderer>() == null)
        {
            gameObject.AddComponent<HybridRenderer>();
            Debug.Log("[DesktopPet] 自动添加了 HybridRenderer 组件");
        }

        // 自动确保底部输入栏存在
        if (GetComponent<BottomInputBar>() == null)
        {
            gameObject.AddComponent<BottomInputBar>();
            Debug.Log("[DesktopPet] 自动添加了 BottomInputBar 组件");
        }

        // 自动确保 TimeWeatherController 存在
        if (GetComponent<TimeWeatherController>() == null)
        {
            gameObject.AddComponent<TimeWeatherController>();
            Debug.Log("[DesktopPet] 自动添加了 TimeWeatherController 组件");
        }

        // 获取渲染器引用：优先使用 HybridRenderer
        var hybrid = GetComponent<HybridRenderer>();
        if (hybrid != null)
        {
            _renderer = hybrid;
            Debug.Log("[DesktopPet] 使用 HybridRenderer（Live2D + 3D 混合）");
        }
        else
        {
            // 降级：单独使用 Live2DRenderer
            var live2d = GetComponent<Live2DRenderer>();
            if (live2d != null)
            {
                _renderer = live2d;
                Debug.Log("[DesktopPet] 使用 Live2DRenderer（降级模式）");
            }
            else
            {
                Debug.LogError("[DesktopPet] 未找到任何渲染器组件 (HybridRenderer/Live2DRenderer)");
                enabled = false;
            }
        }

        Debug.Log($"[DesktopPet] 初始化完成 @ ({petX},{petY}), 屏幕: {Screen.width}x{Screen.height}");
    }

    private void Update()
    {
        // 暂停时不更新物理
        if (isPaused)
            return;

        // ========== v1 行为：拖拽时完全冻结物理 ==========
        if (isDragging)
        {
            // ★ 拖拽时仍要通知渲染器切换挣扎动画（物理不更新）
            if (_renderer != null)
                _renderer.OnPetUpdate(petX, petY, petWidth, petHeight,
                    petVx, petVy, onGround, isDragging, isPaused);
            return;
        }

        // 通知渲染器更新状态
        if (_renderer != null)
        {
            _renderer.OnPetUpdate(petX, petY, petWidth, petHeight,
                petVx, petVy, onGround, isDragging, isPaused);
        }

        // 物理步进
        StepPet();

        // 地面状态机更新
        if (onGround && !isPaused)
        {
            UpdateGroundTask();
        }
    }

    #endregion

    #region 物理步进

    private float _walkSpeedAccum = 0f; // 速度系数累加器（小数部分）

    /// <summary>
    /// 物理步进：位置更新、重力、边界碰撞、落地检测
    /// </summary>
    private void StepPet()
    {
        // 1. 应用速度（支持 WALK_SPEED_FACTOR）
        _walkSpeedAccum += petVx * WALK_SPEED_FACTOR;
        int deltaX = Mathf.RoundToInt(_walkSpeedAccum);
        _walkSpeedAccum -= deltaX;
        petX += deltaX;
        petY += petVy;

        // 2. 重力（空中时）
        if (!onGround)
        {
            petVy += gravity;
            if (petVy > maxFallSpeed)
                petVy = maxFallSpeed;
        }

        // 3. 左右边界碰撞
        if (petX <= 0)
        {
            petX = 0;
            if (petVx < 0)
            {
                if (onGround && currentTask == GroundTask.MoveLeftEdge)
                {
                    petVx = 0;
                }
                else
                {
                    petVx = -petVx;
                    // 碰撞反弹动画
                    if (_renderer != null) _renderer.ShowWallHitPose(-1);
                }
            }
        }
        else if (petX + petWidth >= _screenWidth)
        {
            petX = _screenWidth - petWidth;
            if (petVx > 0)
            {
                if (onGround && currentTask == GroundTask.MoveRightEdge)
                {
                    petVx = 0;
                }
                else
                {
                    petVx = -petVx;
                    // 碰撞反弹动画
                    if (_renderer != null) _renderer.ShowWallHitPose(1);
                }
            }
        }

        // 4. 顶部边界
        if (petY <= 0)
        {
            petY = 0;
            if (petVy < 0)
                petVy = -petVy;
        }

        // 5. 底部落地检测（地面位置 = 屏幕底部 + GROUND_Y_MARGIN）
        int groundFloor = _screenHeight + GROUND_Y_MARGIN;
        if (petY + petHeight >= groundFloor)
        {
            petY = groundFloor - petHeight;
            if (petVy > 0)
            {
                petVy = 0;
                onGround = true;
                OnLand();
            }
        }
        else
        {
            onGround = false;
        }
    }

    /// <summary>
    /// 落地回调
    /// </summary>
    private void OnLand()
    {
        Debug.Log("[DesktopPet] 落地");

        // 通知渲染器显示落地姿势
        if (_renderer != null)
            _renderer.ShowLandPose();

        // 落地后开始地面任务
        StartNextGroundTask();
    }

    #endregion

    #region 地面状态机

    /// <summary>
    /// 选择下一个地面任务 — 用权重随机选取
    /// </summary>
    private GroundTask PickNextGroundTask()
    {
        int wLeftEdge = taskWeightMoveLeftEdge;
        int wRightEdge = taskWeightMoveRightEdge;
        int wLeftTime = taskWeightMoveLeftTime;
        int wRightTime = taskWeightMoveRightTime;
        int wStop = taskWeightStopTime;

        int total = wLeftEdge + wRightEdge + wLeftTime + wRightTime + wStop;
        if (total <= 0) return GroundTask.StopTime; // 安全保底

        int roll = Random.Range(0, total);

        if (roll < wLeftEdge) return GroundTask.MoveLeftEdge;
        roll -= wLeftEdge;

        if (roll < wRightEdge) return GroundTask.MoveRightEdge;
        roll -= wRightEdge;

        if (roll < wLeftTime) return GroundTask.MoveLeftTime;
        roll -= wLeftTime;

        if (roll < wRightTime) return GroundTask.MoveRightTime;

        return GroundTask.StopTime;
    }

    private GroundTask PickNextFromLeftEdge()
    {
        int wEdge = taskWeightMoveRightEdge;
        int wTime = taskWeightMoveRightTime;
        int wStop = taskWeightStopTime;
        int total = wEdge + wTime + wStop;
        if (total <= 0) return GroundTask.StopTime;
        int roll = Random.Range(0, total);
        if (roll < wEdge) return GroundTask.MoveRightEdge;
        roll -= wEdge;
        if (roll < wTime) return GroundTask.MoveRightTime;
        return GroundTask.StopTime;
    }

    private GroundTask PickNextFromRightEdge()
    {
        int wEdge = taskWeightMoveLeftEdge;
        int wTime = taskWeightMoveLeftTime;
        int wStop = taskWeightStopTime;
        int total = wEdge + wTime + wStop;
        if (total <= 0) return GroundTask.StopTime;
        int roll = Random.Range(0, total);
        if (roll < wEdge) return GroundTask.MoveLeftEdge;
        roll -= wEdge;
        if (roll < wTime) return GroundTask.MoveLeftTime;
        return GroundTask.StopTime;
    }

    /// <summary>
    /// 启动一个地面任务
    /// </summary>
    public void StartGroundTask(GroundTask task)
    {
        currentTask = task;
        lastTask = task;
        _taskEndTime = 0f;

        switch (task)
        {
            case GroundTask.MoveLeftEdge:
            case GroundTask.MoveLeftTime:
                petVx = -1;
                break;
            case GroundTask.MoveRightEdge:
            case GroundTask.MoveRightTime:
                petVx = 1;
                break;
            case GroundTask.StopTime:
                petVx = 0;
                break;
        }

        switch (task)
        {
            case GroundTask.MoveLeftEdge:
            case GroundTask.MoveRightEdge:
            case GroundTask.MoveLeftTime:
            case GroundTask.MoveRightTime:
                if (_renderer != null) _renderer.ShowWalkPose();
                float moveDuration = Random.Range(taskMoveTimeMinMs, taskMoveTimeMaxMs + 1);
                _taskEndTime = Time.time + moveDuration / 1000f;
                break;
            case GroundTask.StopTime:
                if (_renderer != null) _renderer.ShowStopPose(0f); // 不锁定姿势，让空闲动作播放
                float stopDuration = Random.Range(taskStopTimeMinMs, taskStopTimeMaxMs + 1);
                _taskEndTime = Time.time + stopDuration / 1000f;
                break;
        }
    }

    public void StartNextGroundTask()
    {
        StartGroundTask(PickNextGroundTask());
    }

    private void StartNextFromLeftEdge()
    {
        StartGroundTask(PickNextFromLeftEdge());
    }

    private void StartNextFromRightEdge()
    {
        StartGroundTask(PickNextFromRightEdge());
    }

    /// <summary>
    /// 每帧检查当前地面任务是否需要切换
    /// </summary>
    private void UpdateGroundTask()
    {
        if (currentTask == GroundTask.None)
        {
            StartNextGroundTask();
            return;
        }

        switch (currentTask)
        {
            case GroundTask.MoveLeftEdge:
                if (petX <= 0)
                    StartNextFromLeftEdge();
                break;

            case GroundTask.MoveRightEdge:
                if (petX + petWidth >= _screenWidth)
                    StartNextFromRightEdge();
                break;

            case GroundTask.MoveLeftTime:
            case GroundTask.MoveRightTime:
            case GroundTask.StopTime:
                if (_taskEndTime > 0f && Time.time >= _taskEndTime)
                    StartNextGroundTask();
                break;
        }
    }

    #endregion

    // Live2DRenderer 负责所有渲染，无需 OnGUI

    private void OnDestroy()
    {
        // 释放互斥锁
        if (_instanceMutex != null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { }
            try { _instanceMutex.Dispose(); } catch { }
            _instanceMutex = null;
        }
    }

    #region 交互接口

    /// <summary>
    /// 从拖拽释放设置初速度（v1 行为：保证向下速度）
    /// </summary>
    public void ApplyDragVelocity(int vx, int vy)
    {
        // v1 行为：释放后如果 vy <= 0，强制设 vy = 2 保证下落
        if (vy <= 0) vy = 2;
        petVx = Mathf.Clamp(vx, -maxHorizontalSpeed, maxHorizontalSpeed);
        petVy = Mathf.Clamp(vy, -maxFallSpeed, maxFallSpeed);
        onGround = false;
        currentTask = GroundTask.None;
        Debug.Log($"[DesktopPet] 拖拽释放: vx={petVx}, vy={petVy}");
    }

    /// <summary>
    /// 暂停宠物运动
    /// </summary>
    public void Pause(float durationSeconds)
    {
        isPaused = true;
        if (durationSeconds > 0)
        {
            Invoke(nameof(Resume), durationSeconds);
        }
    }

    /// <summary>
    /// 恢复宠物运动
    /// </summary>
    public void Resume()
    {
        isPaused = false;
        if (onGround)
        {
            if (currentTask == GroundTask.None || currentTask == GroundTask.StopTime)
            {
                StartNextGroundTask();
            }
        }
    }

    /// <summary>
    /// 重置宠物位置
    /// </summary>
    public void TeleportTo(int x, int y)
    {
        petX = x;
        petY = y;
    }

    /// <summary>
    /// 强制停止走路（被右键菜单调用）
    /// </summary>
    public void ForceStop()
    {
        // 如果当前是边缘任务，强制转为自由走路
        if (currentTask == GroundTask.MoveLeftEdge)
            currentTask = GroundTask.MoveLeftTime;
        else if (currentTask == GroundTask.MoveRightEdge)
            currentTask = GroundTask.MoveRightTime;

        // 立即结束当前任务
        if (currentTask != GroundTask.None && currentTask != GroundTask.StopTime)
        {
            petVx = 0;
            if (_renderer != null)
            {
                _renderer.ShowStopPose(0f);
                _renderer.OnPetUpdate(petX, petY, petWidth, petHeight,
                    petVx, petVy, onGround, isDragging, isPaused);
            }
            _taskEndTime = 0f;
            StartGroundTask(GroundTask.StopTime);
        }
    }

    #endregion
}
