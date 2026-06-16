using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 底部输入栏 — 像任务栏一样常驻屏幕底部
///
/// 显示：
/// • 最近一条消息（自动更新）
/// • 输入框 + 发送按钮（随时打字，无需右键菜单）
/// • 快捷操作（清空 / 复制）
///
/// 风格：深色半透明 VS Code 风格，与 ContextMenu 统一
/// </summary>
public class BottomInputBar : MonoBehaviour
{
    [Header("布局")]
    [Tooltip("输入栏高度（像素）")]
    public float barHeight = 64f;

    [Tooltip("底部留白（任务栏上方间距）")]
    public float bottomMargin = 4f;

    [Header("聊天")]
    [Tooltip("最近消息可见行数")]
    public int maxRecentLines = 2;

    // ===== 内部状态 =====
    private ChatManager _chat;
    private string _inputText = "";
    private string _lastMessage = "";
    private float _lastMsgChangeTime = 0f;
    private bool _chatSearched = false; // 防重复查找

    // ===== 句子队列 — 代理到 ChatManager =====
    private bool IsSentenceAnimating => _chat != null && _chat.IsSentenceAnimating;
    private string CurrentSentence => _chat != null ? _chat.CurrentSentence : null;
    private int SentenceIndex => _chat != null ? _chat.SentenceIndex : 0;
    private int SentenceCount => _chat != null ? _chat.SentenceCount : 0;

    // ===== 样式 =====
    private GUIStyle _barBgStyle;
    private GUIStyle _inputStyle;
    private GUIStyle _sendBtnStyle;
    private GUIStyle _smallBtnStyle;
    private GUIStyle _msgStyle;
    private GUIStyle _msgLabelStyle;
    private Texture2D _bgTex;
    private Texture2D _inputBgTex;
    private Texture2D _btnTex;
    private Texture2D _btnHoverTex;
    private Texture2D _smallBtnTex;
    private bool _stylesReady = false;

    // ===== 淡入动画 =====
    private float _alpha = 0f;
    private const float FADE_IN_DURATION = 0.5f;
    private float _fadeTimer = 0f;
    private bool _started = false;

    // ===== 公共属性 =====

    /// <summary>输入栏在 OnGUI 坐标中的位置（Y 从屏幕顶部算起）</summary>
    public float BarTopY => Screen.height - barHeight - bottomMargin;
    public float BarBottomY => Screen.height - bottomMargin;
    public bool IsMouseOverBar { get; private set; } = false;

    void Start()
    {
        // 查找 ChatManager
        RefreshChatRef();

        if (_chat != null)
        {
            _chat.OnNewReply += OnNewReply;
        }

        _started = true;
        Debug.Log("[BottomInputBar] 底部输入栏已就绪");
    }

    /// <summary>查找 ChatManager 引用</summary>
    private void RefreshChatRef()
    {
        if (_chat != null) return;
        _chat = GetComponent<ChatManager>();
        if (_chat == null) _chat = FindObjectOfType<ChatManager>();
        if (_chat != null && !_chatSearched)
        {
            _chat.OnNewReply += OnNewReply;
            _chatSearched = true;
        }
    }

    void OnDestroy()
    {
        if (_chat != null)
            _chat.OnNewReply -= OnNewReply;
    }

    private void OnNewReply(string reply)
    {
        _lastMessage = "🌸 " + reply;
        _lastMsgChangeTime = Time.time;
        TruncateLastMessage();
    }

    /// <summary>跳过逐句动画，代理到 ChatManager</summary>
    public void SkipSentenceAnimation()
    {
        if (_chat != null) _chat.SkipSentenceAnimation();
    }

    void Update()
    {
        // 监听从 ChatManager 发出的句子切换 → 更新底部栏显示
        if (_chat != null && _chat.IsSentenceAnimating && _chat.CurrentSentence != null)
        {
            _lastMessage = "🌸 " + _chat.CurrentSentence;
            _lastMsgChangeTime = Time.time;
        }
        else if (!IsSentenceAnimating && _chat != null && !string.IsNullOrEmpty(_chat.FullReplyText))
        {
            // 动画结束但 _lastMessage 还没更新到完整文本
            string full = "🌸 " + _chat.FullReplyText;
            if (_lastMessage != full)
            {
                _lastMessage = full;
                _lastMsgChangeTime = Time.time;
                TruncateLastMessage();
            }
        }
    }

    private void TruncateLastMessage()
    {
        // 按行数截断（仅非逐句动画时）
        if (IsSentenceAnimating) return;
        int lines = 0;
        for (int i = 0; i < _lastMessage.Length; i++)
        {
            if (_lastMessage[i] == '\n') lines++;
            if (lines >= maxRecentLines)
            {
                _lastMessage = _lastMessage.Substring(0, i) + "…";
                break;
            }
        }
    }

    void OnGUI()
    {
        InitStyles();

        // ★ 每帧刷新 ChatManager 引用（Start 可能因顺序错过）
        RefreshChatRef();

        // 淡入动画
        if (!_started)
        {
            _fadeTimer += Time.deltaTime;
            _alpha = Mathf.Clamp01(_fadeTimer / FADE_IN_DURATION);
        }
        else
        {
            _alpha = 1f; // Start 之后立刻全显
        }

        // 更新鼠标悬停状态（OnGUI Y=0 顶部，Y=Screen.height 底部）
        IsMouseOverBar = Event.current.mousePosition.y >= BarTopY
                      && Event.current.mousePosition.y <= BarBottomY;

        DrawBar();
    }

    private void InitStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;

        // 深色半透明底
        _bgTex = MakeTex(1, 1, new Color(0.12f, 0.12f, 0.14f, 0.92f));
        _inputBgTex = MakeTex(1, 1, new Color(0.08f, 0.08f, 0.10f, 0.95f));
        _btnTex = MakeTex(1, 1, new Color(0.30f, 0.25f, 0.35f, 1f));
        _btnHoverTex = MakeTex(1, 1, new Color(0.40f, 0.35f, 0.45f, 1f));
        _smallBtnTex = MakeTex(1, 1, new Color(0.22f, 0.22f, 0.25f, 1f));

        _barBgStyle = new GUIStyle
        {
            normal = { background = _bgTex },
            padding = new RectOffset(10, 10, 4, 4)
        };

        _msgLabelStyle = new GUIStyle
        {
            normal = { textColor = new Color(0.7f, 0.7f, 0.8f, _alpha) },
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            padding = new RectOffset(0, 0, 0, 0)
        };

        _inputStyle = new GUIStyle(GUI.skin.textField)
        {
            normal = { textColor = Color.white, background = _inputBgTex },
            fontSize = 12,
            padding = new RectOffset(6, 6, 4, 4),
            margin = new RectOffset(0, 4, 0, 0),
            alignment = TextAnchor.MiddleLeft
        };

        _sendBtnStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { textColor = Color.white, background = _btnTex },
            hover = { background = _btnHoverTex },
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 2, 2),
            margin = new RectOffset(0, 4, 0, 0)
        };

        _smallBtnStyle = new GUIStyle(_sendBtnStyle)
        {
            normal = { textColor = new Color(0.6f, 0.6f, 0.7f), background = _smallBtnTex },
            fontSize = 10,
            padding = new RectOffset(4, 4, 1, 1)
        };

        _msgStyle = new GUIStyle
        {
            normal = { textColor = new Color(0.5f, 0.8f, 0.6f, _alpha) },
            fontSize = 11,
            wordWrap = true,
            padding = new RectOffset(2, 2, 0, 0)
        };
    }

    private void DrawBar()
    {
        // 背景区域
        float y = BarTopY;
        float w = Screen.width;
        float h = barHeight;

        Rect barRect = new Rect(0, y, w, h);
        GUI.Box(barRect, GUIContent.none, _barBgStyle);

        // 内边距
        float padL = 12f;
        float padR = 12f;
        float padT = 3f;

        // ——— 第一行：最近消息 ———
        float msgAreaHeight = 20f;
        float msgY = y + padT;

        string displayMsg = string.IsNullOrEmpty(_lastMessage)
            ? "💡 在底部输入框和符玄聊天吧"
            : _lastMessage;

        // 如果消息太新加个闪烁指示
        if (Time.time - _lastMsgChangeTime < 3f && !string.IsNullOrEmpty(_lastMessage))
        {
            float pulse = Mathf.Sin(Time.time * 2f) * 0.15f + 0.85f;
            _msgStyle.normal.textColor = new Color(0.6f, 1f, 0.7f, _alpha * pulse);
        }
        else
        {
            _msgStyle.normal.textColor = new Color(0.5f, 0.8f, 0.6f, _alpha);
        }

        // 句子逐句动画时：可点击跳过 + 显示进度指示
        Rect msgLabelRect = new Rect(padL, msgY, w - padL - padR - 80f - 120f, msgAreaHeight);
        string progressHint = "";
        if (IsSentenceAnimating)
        {
            progressHint = $" ({SentenceIndex}/{SentenceCount})";
            // 点击消息区域跳过动画
            if (Event.current.type == EventType.MouseDown && msgLabelRect.Contains(Event.current.mousePosition))
            {
                SkipSentenceAnimation();
                Event.current.Use();
            }
        }

        GUI.Label(msgLabelRect, displayMsg + progressHint, _msgStyle);

        // ——— 快捷按钮（第一行右侧） ———
        float btnY = msgY;
        float btnH = msgAreaHeight;
        float btnX = w - padR - 120f;

        string clearLabel = _chat != null && _chat.HistoryCount > 0 ? "🗑" : "";
        if (!string.IsNullOrEmpty(clearLabel) &&
            GUI.Button(new Rect(btnX, btnY, 24f, btnH), clearLabel, _smallBtnStyle))
        {
            _chat.ClearHistory();
            _lastMessage = "💭 对话已清空";
        }

        if (GUI.Button(new Rect(btnX + 28f, btnY, 24f, btnH), "📋", _smallBtnStyle))
        {
            if (_chat != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var e in _chat.GetVisibleHistory())
                {
                    string who = e.role == "user" ? "我" : "符玄";
                    sb.AppendLine($"[{who}] {e.content}");
                }
                if (sb.Length > 0)
                    GUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
                _lastMessage = "✅ 已复制到剪贴板";
                _lastMsgChangeTime = Time.time;
            }
        }

        // ——— 第二行：输入框 + 发送 ———
        float inputY = y + padT + msgAreaHeight + 2f;
        float inputH = 28f;
        float inputW = w - padL - padR - 70f; // 留出发送按钮宽度

        // Enter 检测
        bool enterPressed = Event.current.isKey
            && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && _inputText.Length > 0
            && GUI.GetNameOfFocusedControl() == "bottomChatInput";

        GUI.SetNextControlName("bottomChatInput");
        _inputText = GUI.TextField(new Rect(padL, inputY, inputW, inputH), _inputText, _inputStyle);

        // ★ 只要有文字就能发（消息队列处理等待，不阻塞）
        bool hasInput = !string.IsNullOrWhiteSpace(_inputText);
        bool chatReady = _chat != null;
        bool canSend = hasInput && chatReady;

        bool isWaiting = chatReady && _chat.IsWaiting;

        // 发送按钮
        Color origColor = GUI.color;
        if (!canSend) GUI.color = new Color(1f, 1f, 1f, 0.4f);

        string btnLabel = isWaiting ? "📤" : (hasInput ? "发送" : "💬");
        if (GUI.Button(new Rect(padL + inputW + 4f, inputY, 60f, inputH),
            btnLabel, _sendBtnStyle))
        {
            if (canSend) enterPressed = true;
        }

        GUI.color = origColor;

        // Enter 发消息（有文字即可，排队发送）
        if (enterPressed && canSend)
        {
            Event.current.Use();
            string msg = _inputText.Trim();
            _inputText = "";
            // 用户发消息时停止逐句动画
            if (IsSentenceAnimating) SkipSentenceAnimation();
            if (isWaiting)
                _lastMessage = "📤 消息已排队，等符玄回完就发~";
            else
                _lastMessage = $"🧑 {msg}";
            _lastMsgChangeTime = Time.time;
            _chat.SendMessage(msg, null);
            GUI.FocusControl(null);
        }

        // ——— 分隔线（顶部分割桌面和输入栏） ———
        Color lineColor = new Color(0.35f, 0.25f, 0.40f, _alpha * 0.8f);
        DrawLine(0, (int)y, Screen.width, (int)y, lineColor);
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

    /// <summary>更新样式透明度（跟随淡入动画）</summary>
    private void UpdateStyleAlpha()
    {
        if (_msgStyle != null)
        {
            Color c = _msgStyle.normal.textColor;
            _msgStyle.normal.textColor = new Color(c.r, c.g, c.b, _alpha);
        }
        if (_barBgStyle != null)
        {
            var tex = _barBgStyle.normal.background;
            if (tex != null)
            {
                Color c = tex.GetPixel(0, 0);
                _bgTex.SetPixel(0, 0, new Color(c.r, c.g, c.b, 0.92f * _alpha));
                _bgTex.Apply();
            }
        }
    }
}
