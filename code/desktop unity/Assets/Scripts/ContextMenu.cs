using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 右键上下文菜单 — 分类标签布局
/// 标签：设置 | 动作 | 聊天 | 调试
///
/// 用 OnGUI 绘制，无需 Canvas/UIPrefab
/// </summary>
public class ContextMenu : MonoBehaviour
{
    private DesktopPet _pet;
    private Live2DRenderer _renderer;
    private WindowOverlay _window;
    private ChatManager _chat;

    // ===== 标签系统 =====
    private enum Tab { 设置, 动作, 聊天, 调试 }
    private Tab _currentTab = Tab.设置;
    private string[] _tabNames = { "⚙ 设置", "▶ 动作", "💬 聊天", "🔧 调试" };

    // ===== 菜单状态 =====
    private bool _isOpen = false;
    private Rect _menuRect;
    private float _menuWidth = 300f;
    private float _menuHeight = 420f;
    private Vector2 _scrollPos = Vector2.zero;

    // ===== 权重编辑器副本 =====
    private int _wLeftEdge, _wRightEdge, _wLeftTime, _wRightTime, _wStop;

    // ===== 聊天 =====
    private string _chatInputText = "";
    private string _chatStatusMsg = "";
    private Color _chatStatusColor = Color.gray;
    private Vector2 _chatScrollPos = Vector2.zero;
    private bool _chatShowConfig = false;

    // ===== 句子队列本地计时器（逐句重播） =====
    private int _localSentenceIdx = 0;
    private float _localSentenceTimer = 0f;
    private int _lastSentenceVersion = -1;
    private bool _isLocalAnimating = false;
    public float localSentenceInterval = 1.8f;

    // ===== 调试信息 =====
    private string _debugInfo = "";

    // ===== 样式 =====
    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _smallButtonStyle;
    private GUIStyle _closeButtonStyle;
    private GUIStyle _tabButtonStyle;
    private GUIStyle _tabButtonActiveStyle;
    private GUIStyle _textFieldStyle;
    private GUIStyle _debugTextStyle;
    private Texture2D _bgTexture;
    private Texture2D _sectionBg;
    private Texture2D _btnBg;
    private Texture2D _btnSmallBg;
    private Texture2D _tabBg;
    private Texture2D _tabActiveBg;
    private Texture2D _inputBg;
    private bool _stylesInitialized = false;

    void Start()
    {
        _pet = GetComponent<DesktopPet>();
        _renderer = GetComponent<Live2DRenderer>();
        _window = GetComponent<WindowOverlay>();
        if (_window == null) _window = FindObjectOfType<WindowOverlay>();

        // 聊天管理器
        _chat = GetComponent<ChatManager>();
        if (_chat == null) _chat = gameObject.AddComponent<ChatManager>();

        // 工具调用（符玄法阵）
        var toolInvoker = GetComponent<ToolCallInvoker>();
        if (toolInvoker == null) toolInvoker = gameObject.AddComponent<ToolCallInvoker>();
        _chat.toolInvoker = toolInvoker;

        // 自动聊天（定时问候 + 互动事件 + 气泡）
        var autoChat = GetComponent<AutoChat>();
        if (autoChat == null) gameObject.AddComponent<AutoChat>();
    }

    void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _bgTexture = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.17f, 0.95f));
        _sectionBg = MakeTex(1, 1, new Color(0.12f, 0.12f, 0.14f, 0.9f));
        _btnBg = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.28f, 1f));
        _btnSmallBg = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.33f, 1f));
        _tabBg = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.22f, 1f));
        _tabActiveBg = MakeTex(1, 1, new Color(0.35f, 0.25f, 0.4f, 1f));
        _inputBg = MakeTex(1, 1, new Color(0.08f, 0.08f, 0.1f, 0.9f));

        _titleStyle = new GUIStyle
        {
            normal = { textColor = new Color(0.9f, 0.6f, 0.8f), background = _sectionBg },
            fontStyle = FontStyle.Bold,
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(0, 0, 6, 6)
        };

        _sectionStyle = new GUIStyle
        {
            normal = { textColor = new Color(0.7f, 0.7f, 0.8f), background = _sectionBg },
            fontStyle = FontStyle.Bold,
            fontSize = 12,
            padding = new RectOffset(6, 0, 4, 4)
        };

        _labelStyle = new GUIStyle
        {
            normal = { textColor = Color.white },
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 0, 2, 2)
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { textColor = Color.white, background = _btnBg },
            hover = { background = MakeTex(1, 1, new Color(0.35f, 0.35f, 0.4f)) },
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 4, 4)
        };

        _smallButtonStyle = new GUIStyle(_buttonStyle)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(6, 6, 0, 0),
            fixedWidth = 24,
            fixedHeight = 22
        };

        _closeButtonStyle = new GUIStyle(_buttonStyle)
        {
            normal = { textColor = new Color(1f, 0.4f, 0.4f), background = _btnSmallBg },
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };

        _tabButtonStyle = new GUIStyle(_buttonStyle)
        {
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f), background = _tabBg },
            fontSize = 11,
            padding = new RectOffset(4, 4, 4, 4),
            margin = new RectOffset(0, 0, 0, 0)
        };

        _tabButtonActiveStyle = new GUIStyle(_tabButtonStyle)
        {
            normal = { textColor = Color.white, background = _tabActiveBg },
            fontStyle = FontStyle.Bold
        };

        _textFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            normal = { textColor = Color.white, background = _inputBg },
            fontSize = 11,
            padding = new RectOffset(4, 4, 3, 3)
        };

        _debugTextStyle = new GUIStyle
        {
            normal = { textColor = new Color(0.5f, 0.8f, 0.5f) },
            fontSize = 10,
            fontStyle = FontStyle.Normal,
            padding = new RectOffset(6, 0, 2, 2),
            wordWrap = true
        };
    }

    #region 公开接口

    public void Open(Vector2 screenPos)
    {
        _isOpen = true;
        _currentTab = Tab.设置;   // 默认打开设置
        _scrollPos = Vector2.zero;

        // ★ 重置聊天状态同步
        _lastSentenceVersion = -1;

        // ★ 暂停宠物运动
        if (_pet != null)
        {
            _pet.ForceStop();
            _pet.isPaused = true;
        }

        // 复制当前权重
        _wLeftEdge = _pet.taskWeightMoveLeftEdge;
        _wRightEdge = _pet.taskWeightMoveRightEdge;
        _wLeftTime = _pet.taskWeightMoveLeftTime;
        _wRightTime = _pet.taskWeightMoveRightTime;
        _wStop = _pet.taskWeightStopTime;

        // 定位菜单
        float x = Mathf.Clamp(screenPos.x, 10, Screen.width - _menuWidth - 10);
        float y = Mathf.Clamp(screenPos.y, 10, Screen.height - _menuHeight - 10);
        _menuRect = new Rect(x, y, _menuWidth, _menuHeight);
    }

    public void Close()
    {
        _isOpen = false;

        if (_renderer != null)
            _renderer.OnForcedActionFinished -= OnForcedActionComplete;

        if (_pet != null)
        {
            _pet.isPaused = false;
            _pet.Resume();
        }
    }

    public bool IsOpen => _isOpen;
    public bool IsMouseOverMenu(Vector2 mousePos) => _isOpen && _menuRect.Contains(mousePos);

    #endregion

    #region 本地句子队列计时器

    void Update()
    {
        if (!_isLocalAnimating || _chat == null || !_chat.HasMultiSentenceReply || !_isOpen) return;

        _localSentenceTimer += Time.deltaTime;
        if (_localSentenceTimer >= localSentenceInterval)
        {
            _localSentenceTimer = 0f;
            _localSentenceIdx++;

            if (_localSentenceIdx >= _chat.SentenceList.Count)
            {
                _isLocalAnimating = false;
                _localSentenceIdx = _chat.SentenceList.Count - 1;
            }
        }
    }

    /// <summary>每次绘制聊天标签时调用：检测新回复，重置本地索引</summary>
    private void CheckLocalSentenceState()
    {
        if (_chat == null) return;

        // 有新回复 → 重置本地计时器
        if (_chat.SentenceVersionId != _lastSentenceVersion)
        {
            _lastSentenceVersion = _chat.SentenceVersionId;
            _localSentenceIdx = 0;
            _localSentenceTimer = 0f;
            _isLocalAnimating = _chat.HasMultiSentenceReply;
        }
    }

    #endregion

    #region 主绘制循环

    void OnGUI()
    {
        if (!_isOpen) return;
        InitStyles();

        // 背景
        GUI.Box(_menuRect, GUIContent.none, new GUIStyle { normal = { background = _bgTexture } });

        GUILayout.BeginArea(_menuRect);

        // ===== 标题 =====
        GUILayout.Label("✦ 符玄 · 控制面板", _titleStyle);
        GUILayout.Space(2);

        // ===== 标签栏 =====
        DrawTabBar();
        GUILayout.Space(4);

        // ===== 标签内容 =====
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true,
            GUILayout.Width(_menuWidth), GUILayout.Height(_menuHeight - 90));

        switch (_currentTab)
        {
            case Tab.设置: DrawSettingsTab(); break;
            case Tab.动作: DrawActionsTab(); break;
            case Tab.聊天: DrawChatTab(); break;
            case Tab.调试: DrawDebugTab(); break;
        }

        GUILayout.EndScrollView();

        // ===== 底部关闭按钮 =====
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕ 关闭", _closeButtonStyle, GUILayout.Width(80), GUILayout.Height(24)))
            Close();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    /// <summary>绘制标签栏</summary>
    private void DrawTabBar()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(4);

        for (int i = 0; i < _tabNames.Length; i++)
        {
            bool isActive = ((int)_currentTab == i);
            GUIStyle style = isActive ? _tabButtonActiveStyle : _tabButtonStyle;

            if (GUILayout.Button(_tabNames[i], style, GUILayout.Height(26)))
            {
                if ((Tab)i != _currentTab)
                {
                    _currentTab = (Tab)i;
                    _scrollPos = Vector2.zero;
                }
            }
        }

        GUILayout.Space(4);
        GUILayout.EndHorizontal();
    }

    #endregion

    #region 标签页: 设置

    private void DrawSettingsTab()
    {
        GUILayout.Label("⚙ 任务权重", _sectionStyle);
        GUILayout.Space(2);

        DrawWeightRow("向左走到边缘", ref _wLeftEdge, 0, 10);
        DrawWeightRow("向右走到边缘", ref _wRightEdge, 0, 10);
        DrawWeightRow("向左走定时", ref _wLeftTime, 0, 10);
        DrawWeightRow("向右走定时", ref _wRightTime, 0, 10);
        DrawWeightRow("停止", ref _wStop, 0, 10);

        GUILayout.Space(4);

        if (GUILayout.Button("✓ 应用权重", _buttonStyle, GUILayout.Height(26)))
            ApplyWeights();

        GUILayout.Space(6);

        // 快捷预设
        GUILayout.Label("📦 预设", _sectionStyle);
        GUILayout.Space(2);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("好动", _buttonStyle, GUILayout.Height(24)))
        {
            _wLeftEdge = 3; _wRightEdge = 3;
            _wLeftTime = 3; _wRightTime = 3; _wStop = 1;
            ApplyWeights();
        }
        if (GUILayout.Button("均衡", _buttonStyle, GUILayout.Height(24)))
        {
            _wLeftEdge = 2; _wRightEdge = 2;
            _wLeftTime = 2; _wRightTime = 2; _wStop = 2;
            ApplyWeights();
        }
        if (GUILayout.Button("安静", _buttonStyle, GUILayout.Height(24)))
        {
            _wLeftEdge = 1; _wRightEdge = 1;
            _wLeftTime = 1; _wRightTime = 1; _wStop = 6;
            ApplyWeights();
        }
        GUILayout.EndHorizontal();
    }

    #endregion

    #region 标签页: 动作

    private void DrawActionsTab()
    {
        GUILayout.Label("▶ 强制播放动作", _sectionStyle);
        GUILayout.Space(2);

        DrawActionButtonRow(1, "歪头", 2, "微笑", 3, "挑眉");
        DrawActionButtonRow(4, "星辉", 5, "伸懒腰", 6, "爱心");
        DrawActionButtonRow(7, "数钱", 8, "委屈", 9, "法阵");
        DrawActionButtonRow(10, "害羞", 0, null, 0, null);
    }

    #endregion

    #region 标签页: 聊天

    // 聊天标签专用的聊天消息样式
    private GUIStyle _chatMsgStyle;
    private GUIStyle _chatUserStyle;

    private void InitChatStyles()
    {
        if (_chatMsgStyle != null) return;
        _chatMsgStyle = new GUIStyle
        {
            normal = { textColor = new Color(0.7f, 1f, 0.8f) },
            fontSize = 11,
            wordWrap = true,
            padding = new RectOffset(4, 4, 2, 2)
        };
        _chatUserStyle = new GUIStyle
        {
            normal = { textColor = new Color(0.6f, 0.8f, 1f) },
            fontSize = 11,
            wordWrap = true,
            padding = new RectOffset(4, 4, 2, 2)
        };
    }

    private void DrawChatTab()
    {
        InitChatStyles();

        if (_chat == null)
        {
            GUILayout.Label("⚠ ChatManager 未初始化", _labelStyle);
            return;
        }

        // ——— 本地句子队列：逐句重播 ——
        CheckLocalSentenceState();

        // ——— 消息显示区域 ———
        float areaHeight = _menuHeight - 280f;
        float areaWidth = _menuWidth - 20f;

        GUILayout.BeginVertical(new GUIStyle { normal = { background = _inputBg },
            padding = new RectOffset(4, 4, 4, 4) });

        _chatScrollPos = GUILayout.BeginScrollView(_chatScrollPos, false, true,
            GUILayout.Width(areaWidth), GUILayout.Height(areaHeight));

        var visibleHistory = _chat.GetVisibleHistory();
        if (visibleHistory.Count == 0)
        {
            GUILayout.Label("💡 开始和符玄聊天吧", _labelStyle);
        }
        else
        {
            // 找到最后一个 assistant 条目的索引（有句子队列时才需要逐句显示）
            int lastAssistantIdx = -1;
            if (_chat.HasMultiSentenceReply)
            {
                for (int j = visibleHistory.Count - 1; j >= 0; j--)
                {
                    if (visibleHistory[j].role == "assistant")
                    {
                        lastAssistantIdx = j;
                        break;
                    }
                }
            }

            for (int i = 0; i < visibleHistory.Count; i++)
            {
                var entry = visibleHistory[i];
                string prefix = entry.role == "user" ? "🧑 " : "🌸 ";
                GUIStyle style = entry.role == "user" ? _chatUserStyle : _chatMsgStyle;

                // 有句子列表且是最后一个助手条目 → 用本地索引逐句显示
                bool hasSentences = _chat.HasMultiSentenceReply && i == lastAssistantIdx && entry.role == "assistant";
                if (hasSentences)
                {
                    string content, progress;
                    if (_isLocalAnimating)
                    {
                        var list = _chat.SentenceList;
                        int idx = Mathf.Clamp(_localSentenceIdx, 0, list.Count - 1);
                        content = idx < list.Count ? list[idx] : entry.content;
                        progress = $" ({idx + 1}/{list.Count})";
                    }
                    else
                    {
                        content = _chat.FullReplyText;
                        progress = "";
                    }
                    GUILayout.Label(prefix + content + progress, style);
                }
                else
                {
                    GUILayout.Label(prefix + entry.content, style);
                }
                GUILayout.Space(2);
            }
        }

        // 等待状态
        if (_chat.IsWaiting)
        {
            GUILayout.Label("🌸 符玄正在思考...", _chatMsgStyle);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // ——— 错误/状态提示 ———
        if (!string.IsNullOrEmpty(_chat.LastError))
        {
            GUIStyle errStyle = new GUIStyle(_labelStyle) { normal = { textColor = Color.red } };
            GUILayout.Label("⚠ " + _chat.LastError, errStyle);
        }
        else if (!string.IsNullOrEmpty(_chatStatusMsg))
        {
            GUIStyle statusStyle = new GUIStyle(_labelStyle) { normal = { textColor = _chatStatusColor } };
            GUILayout.Label(_chatStatusMsg, statusStyle);
        }

        GUILayout.Space(4);

        // ——— 输入区域 ———
        GUILayout.BeginHorizontal();

        // 按 Enter 发送
        bool enterPressed = Event.current.isKey
            && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && _chatInputText.Length > 0 && GUI.GetNameOfFocusedControl() == "chatInput";

        GUI.SetNextControlName("chatInput");
        _chatInputText = GUILayout.TextField(_chatInputText, _textFieldStyle,
            GUILayout.Height(24), GUILayout.MinWidth(160));

        bool canSend = !string.IsNullOrWhiteSpace(_chatInputText) && !_chat.IsWaiting;

        if (GUILayout.Button("发送", canSend ? _buttonStyle : _tabButtonStyle,
            GUILayout.Width(50), GUILayout.Height(24)))
        {
            if (canSend)
                enterPressed = true;
        }

        if (enterPressed && canSend)
        {
            Event.current.Use();
            string msg = _chatInputText;
            _chatInputText = "";
            _chatScrollPos = new Vector2(0, float.MaxValue);
            // 用户发新消息时跳过逐句动画
            if (_isLocalAnimating || _chat.IsSentenceAnimating) _chat.SkipSentenceAnimation();
            _isLocalAnimating = false;
            _localSentenceIdx = 0;
            _chat.SendMessage(msg, () => { _chatScrollPos = new Vector2(0, float.MaxValue); });
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ——— 底部工具栏 ———
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("📋 复制聊天", _buttonStyle, GUILayout.Height(22)))
        {
            CopyChatToClipboard();
        }

        if (GUILayout.Button("🗑 清空", _buttonStyle, GUILayout.Height(22)))
        {
            _chat.ClearHistory();
            _chatStatusMsg = "对话已清空";
            _chatStatusColor = Color.gray;
        }

        GUILayout.EndHorizontal();
    }

    #endregion

    #region 标签页: 调试

    private void DrawDebugTab()
    {
        GUILayout.Label("🔧 调试信息", _sectionStyle);
        GUILayout.Space(2);

        // 刷新调试信息
        RefreshDebugInfo();

        GUILayout.BeginVertical(new GUIStyle { normal = { background = _inputBg }, padding = new RectOffset(4, 4, 4, 4) });
        GUILayout.TextArea(_debugInfo, _debugTextStyle, GUILayout.Height(150));
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // 操作按钮
        GUILayout.Label("🛠 工具", _sectionStyle);
        GUILayout.Space(2);

        if (GUILayout.Button("🔄 刷新参数", _buttonStyle, GUILayout.Height(24)))
        {
            RefreshDebugInfo();
        }

        GUILayout.Space(2);

        if (GUILayout.Button("📋 复制调试信息", _buttonStyle, GUILayout.Height(24)))
        {
            GUIUtility.systemCopyBuffer = _debugInfo;
        }

        GUILayout.Space(2);

        // 参数快捷操作
        GUILayout.Label("参数重置", _sectionStyle);
        GUILayout.Space(2);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("重置手臂", _buttonStyle, GUILayout.Height(24)))
        {
            if (_renderer != null)
            {
                _renderer.SetParameterValue("Param33", 0);
                _renderer.SetParameterValue("Param31", 0);
                _renderer.SetParameterValue("Param32", 0);
                _renderer.SetParameterValue("Param94", 0);
                _renderer.SetParameterValue("Param97", 0);
            }
        }
        if (GUILayout.Button("法阵手势", _buttonStyle, GUILayout.Height(24)))
        {
            if (_renderer != null)
            {
                _renderer.SetParameterValue("Param33", 1);
                _renderer.SetParameterValue("Param31", 1);
                _renderer.SetParameterValue("Param32", 1);
                _renderer.SetParameterValue("Param94", -5);
                _renderer.SetParameterValue("Param97", 0);
            }
        }
        GUILayout.EndHorizontal();
    }

    private void RefreshDebugInfo()
    {
        try
        {
            string petState = _pet != null
                ? $"isPaused={_pet.isPaused}  onGround={_pet.onGround}  petVx={_pet.petVx}"
                : "N/A";

            string rendererState = _renderer != null
                ? $"currentAction={_renderer.CurrentActionId}  isLocked={_renderer.IsActionLocked}"
                : "N/A";

            string windowState = _window != null
                ? $"visible={_window.isActiveAndEnabled}"
                : "N/A";

            string chatState = _chat != null
                ? $"history={_chat.HistoryCount}  waiting={_chat.IsWaiting}"
                : "N/A";

            // 参数快照
            string paramsSnapshot = "";
            if (_renderer != null)
            {
                int[] paramIds = { 31, 32, 33, 94, 95, 97, 98, 100, 108, 116, 117, 119, 120 };
                foreach (int id in paramIds)
                {
                    float val = _renderer.GetParameterValue($"Param{id}");
                    paramsSnapshot += $"  Param{id}={val:F2}";
                }
            }

            _debugInfo = $"⏱ {System.DateTime.Now:HH:mm:ss}\n"
                       + $"🐾 宠物: {petState}\n"
                       + $"🎨 渲染: {rendererState}\n"
                       + $"🪟 窗口: {windowState}\n"
                       + $"💬 聊天: {chatState}\n"
                       + $"📐 参数:\n{paramsSnapshot}";
        }
        catch (System.Exception ex)
        {
            _debugInfo = $"获取调试信息失败: {ex.Message}";
        }
    }

    #endregion

    #region 聊天复制

    /// <summary>将聊天记录复制到剪贴板</summary>
    private void CopyChatToClipboard()
    {
        if (_chat == null) return;

        var visible = _chat.GetVisibleHistory();
        if (visible.Count == 0)
        {
            _chatStatusMsg = "没有聊天记录可复制";
            _chatStatusColor = Color.gray;
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var entry in visible)
        {
            string who = entry.role == "user" ? "我" : "符玄";
            sb.AppendLine($"[{who}] {entry.content}");
            sb.AppendLine();
        }

        GUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
        _chatStatusMsg = $"✅ 已复制 {_chat.HistoryCount} 条消息到剪贴板";
        _chatStatusColor = Color.green;
    }

    #endregion

    #region 绘制辅助

    private void DrawWeightRow(string label, ref int value, int min, int max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _labelStyle, GUILayout.Width(130));

        if (GUILayout.Button("-", _smallButtonStyle))
            value = Mathf.Max(min, value - 1);

        string valStr = value.ToString();
        GUIStyle valStyle = new GUIStyle(_labelStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fixedWidth = 30
        };
        GUILayout.Label(valStr, valStyle);

        if (GUILayout.Button("+", _smallButtonStyle))
            value = Mathf.Min(max, value + 1);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawActionButtonRow(int id1, string name1, int id2, string name2, int id3, string name3)
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (id1 > 0) DrawActionButton(id1, name1);
        if (id2 > 0) DrawActionButton(id2, name2);
        if (id3 > 0) DrawActionButton(id3, name3);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(3);
    }

    private void DrawActionButton(int id, string name)
    {
        if (GUILayout.Button($"[{id}] {name}", _buttonStyle, GUILayout.Width(75), GUILayout.Height(28)))
        {
            if (_renderer != null)
            {
                if (_pet != null)
                    _pet.ForceStop();
                _renderer.ForceIdleAction(id);

                // 关闭菜单
                _isOpen = false;

                _renderer.OnForcedActionFinished -= OnForcedActionComplete;
                _renderer.OnForcedActionFinished += OnForcedActionComplete;
            }
        }
    }

    private void OnForcedActionComplete()
    {
        if (_renderer != null)
            _renderer.OnForcedActionFinished -= OnForcedActionComplete;

        if (_pet != null)
        {
            _pet.isPaused = false;
            _pet.Resume();
        }
    }

    #endregion

    #region 操作

    private void ApplyWeights()
    {
        _pet.taskWeightMoveLeftEdge = _wLeftEdge;
        _pet.taskWeightMoveRightEdge = _wRightEdge;
        _pet.taskWeightMoveLeftTime = _wLeftTime;
        _pet.taskWeightMoveRightTime = _wRightTime;
        _pet.taskWeightStopTime = _wStop;

        Debug.Log($"[ContextMenu] 权重已应用 左边={_wLeftEdge} 右边={_wRightEdge} 左走={_wLeftTime} 右走={_wRightTime} 停止={_wStop}");
    }

    #endregion

    #region 工具

    private Texture2D MakeTex(int w, int h, Color c)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = c;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }

    #endregion
}
