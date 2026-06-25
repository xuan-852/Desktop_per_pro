using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// 桌面宠物 — 窗口透明化与置顶管理
///
/// 使用 DWM 玻璃层扩展 (DwmExtendFrameIntoClientArea) 使黑色像素透明，
/// 实现透明窗口效果，支持置顶、无边框、点击穿透。
///
/// 相比色键抠图 (Color Key) 方案，此方案不会产生绿色残留问题，
/// 因为 Live2D 模型的半透明/抗锯齿像素是黑色系的，不会混入绿色。
///
/// 用法：
/// 1. 挂到任意 GameObject
/// 2. Main Camera 的 Background 设为纯黑 (R=0,G=0,B=0)
/// 3. 构建运行
/// 4. 需在 Project Settings 中：Graphics API = D3D11，取消勾选 DXGI flip model
/// </summary>
public class WindowOverlay : MonoBehaviour
{
    #region Win32 API

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_THICKFRAME = 0x00040000;
    private const uint WS_SYSMENU = 0x00080000;
    private const uint WS_MINIMIZEBOX = 0x00020000;
    private const uint WS_MAXIMIZEBOX = 0x00010000;

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_APPWINDOW = 0x00040000;

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    // 要移除的样式：标题栏、边框、系统菜单、最小/最大按钮
    private const uint STYLE_TO_REMOVE = WS_CAPTION | WS_THICKFRAME | WS_SYSMENU |
                                          WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOZORDER = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_SHOWNA = 8;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("Dwmapi.dll", SetLastError = true)]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint SPI_GETWORKAREA = 0x0030;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left; public int Top;
        public int Right; public int Bottom;
    }

    #endregion

    [Header("窗口设置")]
    [Tooltip("窗口宽度（像素），0=全屏宽度")]
    public int width = 0;  // 0=全屏

    [Tooltip("窗口高度（像素），0=全屏高度")]
    public int height = 0; // 0=全屏

    [Tooltip("点击穿透：鼠标事件透过窗口传到桌面（默认 true=安全启动，DragHandler 每帧动态管理）")]
    public bool clickThrough = true;

    [Tooltip("重试次数")]
    public int maxRetries = 5;

    [Header("调试")]
    public bool debugLog = true;

    private IntPtr _hwnd = IntPtr.Zero;
    public System.IntPtr WindowHandle => _hwnd;
    private bool _applied = false;
    private int _origW;
    private int _origH;

    private void Start()
    {
        // 强制相机设置
        ForceCameraSettings();

        if (!Application.isEditor)
        {
            StartCoroutine(RetryApply());
        }
    }

    private void ForceCameraSettings()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 纯黑透明背景
            cam.allowHDR = false;
            cam.allowMSAA = false;
            Log("已强制相机背景为纯黑+关HDR");
        }
    }

    private System.Collections.IEnumerator RetryApply()
    {
        for (int i = 0; i < startupDelayFrames; i++)
            yield return null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            Log($"第 {attempt}/{maxRetries} 次尝试...");

            // 每次重试前重新找窗口（窗口可能在延迟后才有正确标题）
            _hwnd = FindUnityWindow();

            if (_hwnd != IntPtr.Zero)
            {
                bool ok = ApplyNow();
                if (ok)
                {
                    Log($"✅ 第 {attempt} 次尝试成功");
                    yield break;
                }
            }

            // 等待一帧再重试
            yield return null;
        }

        LogError($"❌ 经过 {maxRetries} 次尝试仍无法完成透明窗口设置");
    }

    private int _startupDelayFrames = 5;
    private int startupDelayFrames => _startupDelayFrames;

    /// <summary>
    /// 获取全屏尺寸（覆盖整个屏幕，包括任务栏区域）
    /// </summary>
    private void GetFullScreenSize(out int w, out int h, out int originX, out int originY)
    {
        w = GetSystemMetrics(SM_CXSCREEN);
        h = GetSystemMetrics(SM_CYSCREEN);
        originX = 0;
        originY = 0;
        Log($"全屏尺寸: {w}x{h}");
    }

    /// <summary>
    /// 应用透明窗口设置
    /// </summary>
    public bool ApplyNow()
    {
        if (_hwnd == IntPtr.Zero)
        {
            LogError("ApplyNow: 窗口句柄为空");
            return false;
        }

        int screenW, screenH, screenX, screenY;
        GetFullScreenSize(out screenW, out screenH, out screenX, out screenY);

        // 读取窗口标题做诊断
        StringBuilder titleSb = new StringBuilder(256);
        int titleLen = (int)GetWindowTextW(_hwnd, titleSb, titleSb.Capacity);
        string title = titleLen > 0 ? titleSb.ToString().Trim() : "(空标题)";
        Log($"目标窗口: '{title}' ({_hwnd.ToInt64():X8})");

        if (GetWindowRect(_hwnd, out RECT rect))
        {
            _origW = rect.Right - rect.Left;
            _origH = rect.Bottom - rect.Top;
            Log($"原始窗口大小: {_origW}x{_origH}");
        }

        // ---- 步骤1: 设置扩展样式（必须先做！因为 WS_EX_LAYERED 必须存在） ----
        uint exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        Log($"旧扩展样式: 0x{exStyle:X8}");

        exStyle |= WS_EX_LAYERED | WS_EX_TOPMOST;
        exStyle &= ~WS_EX_APPWINDOW;
        exStyle |= WS_EX_TOOLWINDOW;
        // ★ 不在第一步加穿透，最后统一由 SetClickThrough(false) 配合 DragHandler 管理
        //    （否则中间时序可能导致 DWM 命中测试状态不一致）

        int setResult = SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
        if (setResult == 0)
        {
            int err = Marshal.GetLastWin32Error();
            LogError($"SetWindowLong(扩展样式) 失败, error={err}");
        }
        else
        {
            Log($"已设置扩展样式: 0x{exStyle:X8}");
        }

        // ---- 步骤2: 移除标题栏和边框 ----
        uint style = GetWindowLong(_hwnd, GWL_STYLE);
        Log($"旧窗口样式: 0x{style:X8}");

        style &= ~STYLE_TO_REMOVE;
        style |= WS_POPUP | WS_VISIBLE;

        setResult = SetWindowLong(_hwnd, GWL_STYLE, style);
        if (setResult == 0)
        {
            int err = Marshal.GetLastWin32Error();
            LogError($"SetWindowLong(样式) 失败, error={err}");
        }
        else
        {
            Log($"已移除标题栏和边框: 0x{style:X8}");
        }

        // ---- 步骤3: 用 SetWindowPos 刷新窗口 ----
        int w = width > 0 ? width : screenW;
        int h = height > 0 ? height : screenH;

        bool posResult = SetWindowPos(_hwnd, HWND_TOPMOST,
            screenX, screenY, w, h,
            SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        if (!posResult)
        {
            int err = Marshal.GetLastWin32Error();
            LogError($"SetWindowPos 失败, error={err}");
        }
        else
        {
            Log($"窗口已刷新: {w}x{h}");
        }

        // ---- 步骤4: 重新显示窗口，确保新样式生效 ----
        ShowWindow(_hwnd, SW_SHOWNA);

        // ---- 步骤5: DWM 玻璃层扩展（黑色=透明） ----
        // 关键：将整个客户区扩展为 DWM 玻璃区域，使纯黑 (0,0,0) 变透明
        MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
        uint dwmResult = DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        if (dwmResult != 0)
        {
            LogError($"DwmExtendFrameIntoClientArea 失败, error={dwmResult}");
        }
        else
        {
            Log("已应用 DWM 玻璃层透明（黑色=透明）");
        }

        // 刷新窗口使 DWM 生效
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, w, h, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        _applied = true;

        // ★ 启动时开启穿透，让桌面点击正常通过。
        //    DragHandler.UpdateClickThrough() 每帧接管，鼠标移到宠物身上时自动关穿透。
        //    如果启动时关穿透，窗口会拦截桌面鼠标事件，必须拖一次才能恢复。
        SetClickThrough(true);

        // ★ 通知 DragHandler 强制重设穿透缓存，下一帧根据实际鼠标位置正确评估
        DragHandler dragHandler = GetComponent<DragHandler>();
        if (dragHandler != null)
            dragHandler.ResetClickState();

        // ★★★ 确保窗口能接收输入事件
        //     WS_EX_TOOLWINDOW + SW_SHOWNA 导致窗口从未成为活动窗口，
        //     Unity Input.GetMouseButtonDown() 在窗口非活动时不返回 true。
        //     需要激活窗口一次让 Unity 输入系统初始化。
        ShowWindow(_hwnd, 5); // SW_SHOW=5，激活窗口
        SetForegroundWindow(_hwnd);
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, w, h,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        Log($"✅ 透明窗口已就绪: {w}x{h}, 句柄={_hwnd.ToInt64():X8}, 标题='{title}'");
        return true;
    }

    /// <summary>
    /// 查找 Unity 主窗口句柄
    /// </summary>
    private IntPtr FindUnityWindow()
    {
        uint currentPid = (uint)Process.GetCurrentProcess().Id;
        string productName = Application.productName;

        Log($"本进程 PID={currentPid}, productName='{productName}'");

        // ★ 方法1: 用 Process.MainWindowHandle — 最简单可靠
        try
        {
            IntPtr mainHwnd = Process.GetCurrentProcess().MainWindowHandle;
            if (mainHwnd != IntPtr.Zero)
            {
                StringBuilder sb = new StringBuilder(256);
                int len = (int)GetWindowTextW(mainHwnd, sb, sb.Capacity);
                string title = len > 0 ? sb.ToString().Trim() : "(空标题)";
                Log($"Process.MainWindowHandle → '{title}' ({mainHwnd.ToInt64():X8})");
                if (len > 0)
                {
                    Log($"匹配窗口: '{title}' ({mainHwnd.ToInt64():X8})");
                    return mainHwnd;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Process.MainWindowHandle 失败: {ex.Message}");
        }

        // ★ 方法2: 枚举窗口，但更严格地跳过非主窗口
        Log($"回退: 枚举进程窗口...");
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == currentPid)
            {
                StringBuilder sb = new StringBuilder(256);
                int len = (int)GetWindowTextW(hWnd, sb, sb.Capacity);
                string title = len > 0 ? sb.ToString().Trim() : "(空标题)";

                Log($"  窗口: {hWnd.ToInt64():X8} 标题='{title}'");

                if (len > 0 && !string.IsNullOrEmpty(title))
                {
                    // 跳过所有已知的内部窗口 — 尤其警惕单字符标题！
                    if (title.StartsWith("Unity_") || title.StartsWith("UMP_") ||
                        title.StartsWith("D3D") || title.Contains("GfxPlugin") ||
                        title == "UnityWindowClass" || title == "UnityChildWindow" ||
                        title.Length <= 2)  // ★ 单/双字符标题几乎肯定是内部窗口
                        return true;

                    // 优先精确匹配 productName
                    if (!string.IsNullOrEmpty(productName) &&
                        title.Equals(productName, StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"  → 精确匹配 productName");
                        found = hWnd;
                        return false;
                    }

                    // 包含 productName
                    if (!string.IsNullOrEmpty(productName) &&
                        title.IndexOf(productName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Log($"  → 包含 productName");
                        found = hWnd;
                        return false;
                    }

                    // 特征匹配 — 必须有明确的关键词
                    if (title.Contains("Unity") || title.Contains("Player") ||
                        title.IndexOf("desktop", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Log($"  → 特征匹配");
                        found = hWnd;
                        return false;
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        if (found != IntPtr.Zero)
        {
            StringBuilder sb = new StringBuilder(256);
            int len = (int)GetWindowTextW(found, sb, sb.Capacity);
            Log($"匹配窗口: '{sb.ToString().Trim()}' ({found.ToInt64():X8})");
        }

        return found;
    }

    public void SetClickThrough(bool enabled)
    {
        if (_hwnd == IntPtr.Zero || !_applied) return;
        uint ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
        if (enabled) ex |= WS_EX_TRANSPARENT;
        else ex &= ~WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, ex);
        clickThrough = enabled;

        // ★★★ 关键：SetWindowLong 改完 WS_EX_TRANSPARENT 后，DWM 可能不会立即重新命中测试。
        //     连续两次 SetWindowPos 强制 DWM 刷新窗口区域和命中测试状态。
        //     第一次刷帧，第二次确保生效（有用户反馈单次 SWP_FRAMECHANGED 在某些 Win11 版本不够）。
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    private void Log(string msg)
    {
        if (debugLog)
            UnityEngine.Debug.Log($"[WindowOverlay] {msg}");
    }

    private void LogError(string msg)
    {
        UnityEngine.Debug.LogError($"[WindowOverlay] {msg}");
    }
}
