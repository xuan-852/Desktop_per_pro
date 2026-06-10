using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// 桌面宠物 — 窗口透明化与置顶管理
///
/// 使用色键透明 (Color Key) 将 Unity 窗口中的亮绿色 (#00FF00) 抠除，
/// 实现透明窗口效果，支持置顶、无边框、点击穿透。
///
/// 用法：
/// 1. 挂到任意 GameObject
/// 2. Main Camera 的 Background 设为 #00FF00 (R=0,G=255,B=0)
/// 3. 构建运行
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

    private const uint LWA_COLORKEY = 0x00000001;

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

    private const uint WM_NCCALCSIZE = 0x0083;

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
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd,
        uint crKey, byte bAlpha, uint dwFlags);

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

    private const int SW_SHOWNA = 8;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

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

    [Tooltip("点击穿透：鼠标事件透过窗口传到桌面")]
    public bool clickThrough = false;

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
            cam.backgroundColor = new Color(0f, 1f, 0f, 1f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            Log("已强制相机背景为绿色+关HDR");
        }

        // 关掉任何可能改变颜色的后处理
        var fx = FindObjectOfType<MonoBehaviour>();
        // 不遍历，太暴力了，信任 ProjectSettings
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
    /// 获取全屏窗口尺寸（覆盖整个桌面）
    /// </summary>
    private void GetFullScreenSize(out int w, out int h)
    {
        // Unity Screen 在构建时不一定等于真实屏幕
        // 用 Win32 API 获取准确的屏幕尺寸
        w = GetSystemMetrics(SM_CXSCREEN);
        h = GetSystemMetrics(SM_CYSCREEN);
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

        int screenW, screenH;
        GetFullScreenSize(out screenW, out screenH);

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
        if (clickThrough)
            exStyle |= WS_EX_TRANSPARENT;

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
            0, 0, w, h,
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

        // ---- 步骤5: 色键透明（要额外调用两次确保生效） ----
        bool keyResult = SetLayeredWindowAttributes(_hwnd, 0x0000FF00, 0, LWA_COLORKEY);
        if (!keyResult)
        {
            int err = Marshal.GetLastWin32Error();
            LogError($"色键透明第一次失败, error={err}");
        }

        // 第二次：有些窗口需要重新 SetWindowPos 后再设一次
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, w, h, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        keyResult = SetLayeredWindowAttributes(_hwnd, 0x0000FF00, 0, LWA_COLORKEY);
        if (!keyResult)
        {
            int err = Marshal.GetLastWin32Error();
            LogError($"色键透明第二次失败, error={err}");
        }
        else
        {
            Log("已应用色键透明 (0x0000FF00)");
        }

        _applied = true;
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
        Log($"所有进程窗口:");

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
                    // 跳过已知的内部窗口
                    if (title.StartsWith("Unity_") || title.StartsWith("UMP_") ||
                        title.StartsWith("D3D") || title.Contains("GfxPlugin") ||
                        title == "UnityWindowClass" || title == "UnityChildWindow")
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

                    // 特征匹配
                    if (title.Contains("Unity") || title.Contains("Player") ||
                        title.IndexOf("desktop", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Log($"  → 特征匹配");
                        found = hWnd;
                        return false;
                    }

                    // 如果一个可见窗口有标题但没有被跳过，作为最后备选
                    if (found == IntPtr.Zero && len > 0)
                    {
                        found = hWnd;
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
