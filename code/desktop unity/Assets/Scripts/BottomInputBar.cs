using UnityEngine;

/// <summary>
/// 底部输入栏 — Windows 搜索风格
///
/// 固定位置、简洁浅色、干净输入体验
/// </summary>
public class BottomInputBar : MonoBehaviour
{
    // ===== 固定坐标（玩家调整后写入） =====
    private const float BAR_LEFT = 265f;
    private const float BAR_RIGHT = 635f;
    private const float BAR_TOP = 1528f;
    private const float BAR_BOTTOM = 1602f;

    // 便捷访问
    public float BarLeft => BAR_LEFT;
    public float BarRight => BAR_RIGHT;
    public float BarTop => BAR_TOP;
    public float BarBottom => BAR_BOTTOM;
    private float BarW => BarRight - BarLeft;
    private float BarH => BarBottom - BarTop;

    // ===== 内部状态 =====
    private ChatManager _chat;
    private string _inputText = "";

    // ===== 样式 =====
    private GUIStyle _barBgStyle;
    private GUIStyle _inputStyle;
    private GUIStyle _sendBtnStyle;
    private Texture2D _bgTex;
    private Texture2D _inputBgTex;
    private Texture2D _btnTex;
    private Texture2D _btnHoverTex;
    private bool _stylesReady = false;

    // ===== 淡入动画 =====
    private float _alpha = 0f;
    private const float FADE_IN_DURATION = 0.5f;
    private float _fadeTimer = 0f;
    private bool _started = false;

    // ===== 公共属性 =====

    public bool IsMouseOverBar { get; private set; } = false;

    void Start()
    {
        RefreshChatRef();
        _started = true;
        Debug.Log($"[BottomInputBar] 搜索栏已就绪 | left={BAR_LEFT:F0} right={BAR_RIGHT:F0} top={BAR_TOP:F0} bottom={BAR_BOTTOM:F0}");
    }

    /// <summary>查找 ChatManager 引用</summary>
    private void RefreshChatRef()
    {
        if (_chat != null) return;
        _chat = GetComponent<ChatManager>();
        if (_chat == null) _chat = FindObjectOfType<ChatManager>();
    }

    /// <summary>跳过逐句动画，代理到 ChatManager</summary>
    public void SkipSentenceAnimation()
    {
        if (_chat != null) _chat.SkipSentenceAnimation();
    }

    void OnGUI()
    {
        InitStyles();
        RefreshChatRef();

        if (!_started)
        {
            _fadeTimer += Time.deltaTime;
            _alpha = Mathf.Clamp01(_fadeTimer / FADE_IN_DURATION);
        }
        else _alpha = 1f;

        // 鼠标悬停检测
        Vector2 mp = Event.current.mousePosition;
        IsMouseOverBar = mp.x >= BarLeft && mp.x <= BarRight && mp.y >= BarTop && mp.y <= BarBottom;

        DrawBar();
    }

    private void InitStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;

        // Windows 搜索风格 — 白色干净背景
        _bgTex = MakeTex(1, 1, new Color(1f, 1f, 1f, 0.92f));                // 白底
        _inputBgTex = MakeTex(1, 1, new Color(0.85f, 0.85f, 0.88f, 0.6f));  // 浅灰输入框底
        _btnTex = MakeTex(1, 1, new Color(0.20f, 0.50f, 0.95f, 1f));        // 蓝按钮
        _btnHoverTex = MakeTex(1, 1, new Color(0.15f, 0.40f, 0.85f, 1f));   // 深蓝悬停

        _barBgStyle = new GUIStyle
        {
            normal = { background = _bgTex },
            padding = new RectOffset(12, 12, 10, 10)
        };

        _inputStyle = new GUIStyle(GUI.skin.textField)
        {
            normal = { textColor = new Color(0.15f, 0.15f, 0.18f), background = _inputBgTex },
            fontSize = 14,
            padding = new RectOffset(10, 10, 6, 6),
            margin = new RectOffset(0, 4, 0, 0),
            alignment = TextAnchor.MiddleLeft
        };

        _sendBtnStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { textColor = Color.white, background = _btnTex },
            hover = { background = _btnHoverTex },
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 2, 2),
            margin = new RectOffset(0, 4, 0, 0)
        };
    }

    private void DrawBar()
    {
        float y = BarTop;
        float w = BarW;
        float h = BarH;

        // ——— 白底圆角区域 ———
        Rect barRect = new Rect(BarLeft, y, w, h);
        GUI.Box(barRect, GUIContent.none, _barBgStyle);

        // ——— 顶部细阴影线（浅灰） ———
        DrawLine((int)BarLeft, (int)y, (int)(BarLeft + w), (int)y, new Color(0.80f, 0.80f, 0.82f, _alpha * 0.8f));

        float padL = 14f;
        float padR = 14f;
        float padT = 8f;
        float padB = 8f;

        // ——— 放大输入框 + 发送按钮 ———
        float inputH = h - padT - padB;
        float inputW = w - padL - padR - 66f;
        float inputY = y + padT;

        // Enter 检测
        bool enterPressed = Event.current.isKey
            && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && _inputText.Length > 0
            && GUI.GetNameOfFocusedControl() == "bottomChatInput";

        GUI.SetNextControlName("bottomChatInput");
        _inputText = GUI.TextField(new Rect(BarLeft + padL, inputY, inputW, inputH), _inputText, _inputStyle);

        bool hasInput = !string.IsNullOrWhiteSpace(_inputText);
        bool chatReady = _chat != null;
        bool canSend = hasInput && chatReady;
        bool isWaiting = chatReady && _chat.IsWaiting;

        Color origColor = GUI.color;
        if (!canSend) GUI.color = new Color(1f, 1f, 1f, 0.4f);

        string btnLabel = isWaiting ? "📤" : (hasInput ? "发送" : "💬");
        if (GUI.Button(new Rect(BarLeft + padL + inputW + 4f, inputY, 56f, inputH),
            btnLabel, _sendBtnStyle))
        {
            if (canSend) enterPressed = true;
        }

        GUI.color = origColor;

        if (enterPressed && canSend)
        {
            Event.current.Use();
            string msg = _inputText.Trim();
            _inputText = "";
            _chat.SendMessage(msg, null);
            GUI.FocusControl(null);
        }
    }

    /// <summary>画一条水平线</summary>
    private void DrawLine(int x1, int y1, int x2, int y2, Color color)
    {
        Texture2D tex = MakeTex(1, 1, color);
        GUI.Box(new Rect(x1, y1, x2 - x1, 1), GUIContent.none,
            new GUIStyle { normal = { background = tex } });
    }

    private Texture2D MakeTex(int w, int h, Color c)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = c;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}
