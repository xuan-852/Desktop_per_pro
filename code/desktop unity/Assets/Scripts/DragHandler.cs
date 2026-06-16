using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 拖拽处理 — 鼠标拖拽宠物 + 抛掷
///
/// 核心设计：
/// - 每帧根据鼠标位置动态设置点击穿透
///   → 鼠标在宠物范围内：关穿透，Unity 接收事件
///   → 鼠标在宠物范围外：开穿透，点击穿透到桌面
/// - 拖拽时用 SetWindowPos 移动整个窗口
/// - 松开时根据拖拽速度计算抛掷初速度
/// </summary>
[RequireComponent(typeof(DesktopPet))]
public class DragHandler : MonoBehaviour
{
    #region Win32 API

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(System.IntPtr hWnd, out RECT lpRect);

    private static readonly System.IntPtr HWND_TOPMOST = new System.IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    #endregion

    private DesktopPet _pet;
    private WindowOverlay _window;
    private IPetRenderer _renderer;

    [Header("拖拽设置")]
    [Tooltip("触发拖拽的最小移动像素")]
    public int dragThreshold = 5;

    [Tooltip("抛掷速度系数（像素/帧）")]
    public float throwScale = 0.5f;

    [Tooltip("最大抛掷速度")]
    public int maxThrowSpeed = 12;

    [Header("点击设置")]
    [Tooltip("点击后强制暂停时间（秒）")]
    public float clickPauseDuration = 1.0f;

    [Header("双击设置")]
    [Tooltip("双击判定时间窗口（秒）")]
    public float doubleClickThreshold = 0.35f;

    [Header("粒子特效")]
    [Tooltip("点击时的粒子数量（星星）")]
    public int starParticleCount = 5;
    [Tooltip("双击时的粒子数量（爱心）")]
    public int heartParticleCount = 6;

    private ParticleEffectManager _particleFx;

    // 拖拽状态
    private bool _isDragging = false;
    private bool _isClickCandidate = false;
    private Vector2 _dragStartMouse;
    private Vector2 _lastMousePos;
    private Vector2 _velocityBuffer;
    private int _velocityFrames;

    // 双击状态
    private float _lastClickTime = -1f;
    private Vector2 _lastClickPos;
    private bool _doubleClickPending = false; // 已确认是双击

    // 公开事件（供 AutoChat 监听）
    public System.Action OnPetClicked;
    public System.Action OnPetDoubleClicked;
    public System.Action OnDragEnded;

    // 右键菜单
    private ContextMenu _contextMenu;

    // 鼠标在宠物范围内的状态（每帧更新）
    private bool _mouseOverPet = false;

    // 底部输入栏引用
    private BottomInputBar _bottomBar;

    private void Start()
    {
        _pet = GetComponent<DesktopPet>();
        _window = GetComponent<WindowOverlay>();
        if (_window == null)
            _window = FindObjectOfType<WindowOverlay>();
        _renderer = GetComponent<IPetRenderer>();
        _contextMenu = GetComponent<ContextMenu>();
        if (_contextMenu == null)
        {
            _contextMenu = FindObjectOfType<ContextMenu>();
            if (_contextMenu == null)
            {
                // 自动挂载 ContextMenu（新脚本，防止用户忘记手动添加）
                _contextMenu = gameObject.AddComponent<ContextMenu>();
                Debug.Log("[DragHandler] 自动挂载 ContextMenu 组件");
            }
        }

        // BottomInputBar 可能稍后才添加，Start 中找一次
        RefreshBottomBar();

        // 粒子特效管理器
        _particleFx = GetComponent<ParticleEffectManager>();
        if (_particleFx == null)
            _particleFx = FindObjectOfType<ParticleEffectManager>();
        if (_particleFx == null)
        {
            _particleFx = gameObject.AddComponent<ParticleEffectManager>();
            Debug.Log("[DragHandler] 自动挂载 ParticleEffectManager 组件");
        }
    }

    private void Update()
    {
        // ========== 0. 菜单打开时：菜单自己的点击穿透管理 ==========
        // ★优先处理，不依赖 UpdateClickThrough（避免菜单打开后被 WS_EX_TRANSPARENT 吞掉输入）
        if (_contextMenu != null && _contextMenu.IsOpen)
        {
            Vector2 mousePos = GetMousePos();
            bool overMenu = _contextMenu.IsMouseOverMenu(mousePos);
            _window?.SetClickThrough(!overMenu);   // 菜单区域内可点击，区域外穿透
            _mouseOverPet = false; // 重置，确保关闭后重建穿透状态

            // ★ 右键点击时关闭菜单
            if (Input.GetMouseButtonDown(1))
            {
                _contextMenu.Close();
                // 关闭后不 return，让本帧正常处理
            }
            else
            {
                return; // 菜单打开且无右键时不处理拖拽
            }
        }

        // ========== 1. 每帧更新点击穿透（★必须在输入检测之前，否则右键被 WS_EX_TRANSPARENT 吞掉）==========
        UpdateClickThrough();

        // ========== 2. 右键菜单 ==========
        if (Input.GetMouseButtonDown(1))
        {
            Vector2 mousePos = GetMousePos();
            if (IsPointInPet(mousePos))
            {
                if (_contextMenu != null)
                {
                    _contextMenu.Open(mousePos);
                    // 打开后立即管理穿透，不等下一帧
                    bool overMenu = _contextMenu.IsMouseOverMenu(mousePos);
                    _window?.SetClickThrough(!overMenu);
                    _mouseOverPet = false;
                    return;
                }
            }
            else
            {
                if (_contextMenu != null) _contextMenu.Close();
            }
        }

        if (_pet.isPaused)
        {
            // ★ 暂停时：只检测点击（支持双击的第二下）
            // — 鼠标按下（注册候选）—
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = GetMousePos();
                if (IsPointInPet(mousePos))
                {
                    _isClickCandidate = true;
                }
            }
            // — 鼠标释放（触发双击判定）—
            if (Input.GetMouseButtonUp(0) && _isClickCandidate)
            {
                Vector2 clickPos = GetMousePos();
                float now = Time.time;
                if (now - _lastClickTime <= doubleClickThreshold)
                {
                    // === 暂停中的双击 ===
                    _doubleClickPending = true;
                    _particleFx?.BurstHearts(GetPetCenter(), heartParticleCount);
                    if (_renderer != null) _renderer.ShowClickPose(IPetRenderer.ClickZone.Head);
                    Debug.Log("[DragHandler] 暂停中双击宠物 ❤");
                    OnPetDoubleClicked?.Invoke();
                    OnPetClicked?.Invoke();
                    _lastClickTime = now;
                    _lastClickPos = clickPos;
                }
                _isClickCandidate = false;
                _isDragging = false;
            }
            return;
        }

        // ========== 3. 鼠标左键按下 ==========
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = GetMousePos();
            if (IsPointInPet(mousePos))
            {
                _isClickCandidate = true;
                _isDragging = false;
                _pet.isDragging = false;
                _dragStartMouse = mousePos;
                _lastMousePos = mousePos;
                _velocityBuffer = Vector2.zero;
                _velocityFrames = 0;
            }
        }

        // ========== 4. 鼠标移动（按住左键） ==========
        if (Input.GetMouseButton(0) && _isClickCandidate)
        {
            Vector2 mousePos = GetMousePos();
            Vector2 delta = mousePos - _dragStartMouse;

            if (delta.magnitude >= dragThreshold && !_isDragging)
            {
                _isDragging = true;
                _pet.isDragging = true;
                if (_renderer != null) _renderer.ShowDragPose();
            }

            if (_isDragging)
            {
                // 不移动窗口！窗口始终全屏固定在 (0,0)
                // 只更新宠物的渲染坐标 (petX/petY)
                Vector2 moveDelta = mousePos - _lastMousePos;
                _pet.petX += (int)moveDelta.x;
                _pet.petY += (int)moveDelta.y;

                // v1 行为：拖拽中更新 petVx 方向 → 渲染器切换左右的拖拽图
                if (moveDelta.x > 0)
                    _pet.petVx = 1;   // 朝右 → 显示 right 文件夹的 3.png
                else if (moveDelta.x < 0)
                    _pet.petVx = -1;  // 朝左 → 显示 left 文件夹的 3.png

                // 记录速度
                _velocityBuffer += (mousePos - _lastMousePos);
                _velocityFrames++;
                if (_velocityFrames > 5)
                {
                    _velocityBuffer -= _velocityBuffer * 0.5f;
                    _velocityFrames = 5;
                }

                _lastMousePos = mousePos;
            }
        }

        // ========== 5. 鼠标左键释放 ==========
        if (Input.GetMouseButtonUp(0))
        {
            if (_isDragging)
            {
                _pet.isDragging = false;
                Vector2 avgVelocity = _velocityFrames > 0
                    ? _velocityBuffer / _velocityFrames
                    : Vector2.zero;

                int vx = Mathf.RoundToInt(Mathf.Clamp(avgVelocity.x * throwScale,
                    -maxThrowSpeed, maxThrowSpeed));
                int vy = Mathf.RoundToInt(Mathf.Clamp(avgVelocity.y * throwScale,
                    -maxThrowSpeed, maxThrowSpeed));

                _pet.ApplyDragVelocity(vx, vy);
                Debug.Log($"[DragHandler] 抛掷: ({vx}, {vy})");
                OnDragEnded?.Invoke();
                _doubleClickPending = false;
            }
            else if (_isClickCandidate)
            {
                Vector2 clickPos = GetMousePos();
                float now = Time.time;

                // ★ 双击检测：两次点击间隔在阈值内
                if (now - _lastClickTime <= doubleClickThreshold)
                {
                    // === 双击 ===
                    _doubleClickPending = true;

                    // 粒子：爱心爆出
                    _particleFx?.BurstHearts(GetPetCenter(), heartParticleCount);

                    // 表情：摸头反应（点击位置在头上半部分）
                    IPetRenderer.ClickZone zone = CalcClickZone(clickPos);
                    if (zone == IPetRenderer.ClickZone.Head)
                    {
                        if (_renderer != null) _renderer.ShowClickPose(zone);
                    }
                    else
                    {
                        // 不在头部也显示爱心反应
                        if (_renderer != null) _renderer.ShowClickPose(IPetRenderer.ClickZone.Head);
                    }

                    _pet.Pause(clickPauseDuration);
                    Debug.Log("[DragHandler] 双击宠物 ❤");
                    OnPetDoubleClicked?.Invoke();
                    OnPetClicked?.Invoke();
                }
                else
                {
                    // === 单击 ===
                    _doubleClickPending = false;

                    // 粒子：星星爆出
                    Vector2 petCenter = GetPetCenter();
                    _particleFx?.BurstStars(petCenter, starParticleCount);

                    // 分区域点击反应
                    IPetRenderer.ClickZone zone = CalcClickZone(clickPos);
                    if (_renderer != null) _renderer.ShowClickPose(zone);

                    _pet.Pause(clickPauseDuration);
                    Debug.Log($"[DragHandler] 单击宠物 zone={zone}");
                    OnPetClicked?.Invoke();
                }

                _lastClickTime = now;
                _lastClickPos = clickPos;
            }

            _isDragging = false;
            _isClickCandidate = false;
        }
    }

    /// <summary>
    /// 查找底部输入栏（每次更新前刷新，应对动态添加）
    /// </summary>
    private void RefreshBottomBar()
    {
        if (_bottomBar != null) return;
        _bottomBar = GetComponent<BottomInputBar>();
        if (_bottomBar == null) _bottomBar = FindObjectOfType<BottomInputBar>();
    }

    /// <summary>
    /// 每帧根据鼠标位置动态设置点击穿透
    /// </summary>
    private void UpdateClickThrough()
    {
        if (_window == null) return;

        RefreshBottomBar(); // ★ 每帧重新确保引用

        Vector2 mousePos = GetMousePos();
        bool overPet = IsPointInPet(mousePos);
        // ★ 底部输入栏也接收点击（打字用）
        bool overBar = _bottomBar != null
            && mousePos.y >= _bottomBar.BarTopY
            && mousePos.y <= _bottomBar.BarBottomY;

        bool needInput = overPet || overBar;

        if (needInput != _mouseOverPet)
        {
            _mouseOverPet = needInput;
            _window.SetClickThrough(!needInput);
        }
    }

    private Vector2 GetMousePos()
    {
        Vector2 p = Input.mousePosition;
        p.y = Screen.height - p.y;
        return p;
    }

    private bool IsPointInPet(Vector2 mousePos)
    {
        return mousePos.x >= _pet.petX &&
               mousePos.x <= _pet.petX + _pet.petWidth &&
               mousePos.y >= _pet.petY &&
               mousePos.y <= _pet.petY + _pet.petHeight;
    }

    private System.IntPtr GetUnityWindowHandle()
    {
        return _window != null ? _window.WindowHandle : System.IntPtr.Zero;
    }

    // ================================================================
    //  点击区域判定
    // ================================================================

    /// <summary>
    /// 根据鼠标点击位置在宠物 bounding box 内的相对位置判断点击区域
    /// </summary>
    private IPetRenderer.ClickZone CalcClickZone(Vector2 mousePos)
    {
        // 转成宠物本地坐标 (0~1, 0~1)
        float lx = (mousePos.x - _pet.petX) / _pet.petWidth;
        float ly = (mousePos.y - _pet.petY) / _pet.petHeight;

        // 头部：上部 ~35%
        if (ly >= 0.65f) return IPetRenderer.ClickZone.Head;
        // 脚部：下部 ~20%
        if (ly <= 0.20f) return IPetRenderer.ClickZone.Feet;
        // 身体：中间
        return IPetRenderer.ClickZone.Body;
    }

    /// <summary>
    /// 获取宠物中心坐标（用于粒子爆发位置）
    /// </summary>
    private Vector2 GetPetCenter()
    {
        return new Vector2(
            _pet.petX + _pet.petWidth * 0.5f,
            _pet.petY + _pet.petHeight * 0.5f
        );
    }
}
