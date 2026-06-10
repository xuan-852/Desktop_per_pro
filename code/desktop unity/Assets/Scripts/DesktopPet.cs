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
    public int taskWeightMoveLeftEdge = 0;
    [Tooltip("向右走到边缘权重")]
    public int taskWeightMoveRightEdge = 0;
    [Tooltip("向左走定时权重")]
    public int taskWeightMoveLeftTime = 0;
    [Tooltip("向右走定时权重")]
    public int taskWeightMoveRightTime = 0;
    [Tooltip("停止定时权重")]
    public int taskWeightStopTime = 1;

    [Tooltip("地面任务移动持续时间（毫秒）")]
    public int taskMoveTimeMs = 5000;

    [Tooltip("停止持续时间（毫秒）")]
    public int taskStopTimeMs = 30000;

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

        // 获取渲染器引用：使用 Live2DRenderer
        var live2d = GetComponent<Live2DRenderer>();
        if (live2d != null)
        {
            _renderer = live2d;
            Debug.Log("[DesktopPet] 使用 Live2DRenderer");
        }
        else
        {
            Debug.LogError("[DesktopPet] 场景中未找到 Live2DRenderer 组件，请添加");
            enabled = false;
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
            return;

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

    /// <summary>
    /// 物理步进：位置更新、重力、边界碰撞、落地检测
    /// </summary>
    private void StepPet()
    {
        // 1. 应用速度
        petX += petVx;
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
    /// 选择下一个地面任务 — 测试模式：先左后右循环（Ping-Pong）
    /// </summary>
    private GroundTask PickNextGroundTask()
    {
        if (lastTask == GroundTask.MoveLeftTime)
            return GroundTask.MoveRightTime;
        return GroundTask.MoveLeftTime;
    }

    private GroundTask PickNextFromLeftEdge()
    {
        int wEdge = taskWeightMoveRightEdge;
        int wTime = taskWeightMoveRightTime;
        int total = wEdge + wTime;
        if (total <= 0) return GroundTask.MoveRightEdge;
        return Random.Range(0, total) < wEdge ?
            GroundTask.MoveRightEdge : GroundTask.MoveRightTime;
    }

    private GroundTask PickNextFromRightEdge()
    {
        int wEdge = taskWeightMoveLeftEdge;
        int wTime = taskWeightMoveLeftTime;
        int total = wEdge + wTime;
        if (total <= 0) return GroundTask.MoveLeftEdge;
        return Random.Range(0, total) < wEdge ?
            GroundTask.MoveLeftEdge : GroundTask.MoveLeftTime;
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
                _taskEndTime = Time.time + taskMoveTimeMs / 1000f;
                break;
            case GroundTask.StopTime:
                if (_renderer != null) _renderer.ShowStopPose(taskStopTimeMs / 1000f);
                _taskEndTime = Time.time + taskStopTimeMs / 1000f;
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
        if (onGround && currentTask == GroundTask.None)
        {
            StartNextGroundTask();
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

    #endregion
}
