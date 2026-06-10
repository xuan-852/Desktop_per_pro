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

    // 拖拽状态
    private bool _isDragging = false;
    private bool _isClickCandidate = false;
    private Vector2 _dragStartMouse;
    private Vector2 _lastMousePos;
    private Vector2 _velocityBuffer;
    private int _velocityFrames;

    // 鼠标在宠物范围内的状态（每帧更新）
    private bool _mouseOverPet = false;

    private void Start()
    {
        _pet = GetComponent<DesktopPet>();
        _window = GetComponent<WindowOverlay>();
        if (_window == null)
            _window = FindObjectOfType<WindowOverlay>();
        _renderer = GetComponent<IPetRenderer>();
    }

    private void Update()
    {
        // ========== 1. 每帧更新点击穿透状态 ==========
        UpdateClickThrough();

        if (_pet.isPaused)
            return;

        // ========== 2. 鼠标左键按下 ==========
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

        // ========== 3. 鼠标移动（按住左键） ==========
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

        // ========== 4. 鼠标左键释放 ==========
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
            }
            else if (_isClickCandidate)
            {
                if (_renderer != null) _renderer.ShowClickPose();
                _pet.Pause(clickPauseDuration);
                Debug.Log("[DragHandler] 轻击宠物");
            }

            _isDragging = false;
            _isClickCandidate = false;
        }
    }

    /// <summary>
    /// 每帧根据鼠标位置动态设置点击穿透
    /// </summary>
    private void UpdateClickThrough()
    {
        if (_window == null) return;

        Vector2 mousePos = GetMousePos();
        bool overPet = IsPointInPet(mousePos);

        if (overPet != _mouseOverPet)
        {
            _mouseOverPet = overPet;
            // 在宠物范围内 → 关闭穿透（接收事件）
            // 在宠物范围外 → 开启穿透（透到桌面）
            _window.SetClickThrough(!overPet);
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
}
