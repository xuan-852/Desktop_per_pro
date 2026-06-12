using UnityEngine;

/// <summary>
/// 右键上下文菜单 — 调整任务权重 + 强制播放动作
///
/// 用 OnGUI 绘制，无需 Canvas/UIPrefab
/// </summary>
public class ContextMenu : MonoBehaviour
{
    private DesktopPet _pet;
    private Live2DRenderer _renderer;
    private WindowOverlay _window;

    // 状态
    private bool _isOpen = false;
    private Rect _menuRect;
    private float _menuWidth = 280f;
    private float _menuHeight = 370f;
    private int _actionButtonCount = 5; // 每行按钮数
    private Vector2 _scrollPos = Vector2.zero;

    // 权重编辑器副本（确认后写回）
    private int _wLeftEdge, _wRightEdge, _wLeftTime, _wRightTime, _wStop;

    // ===== 样式 =====
    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _smallButtonStyle;
    private GUIStyle _closeButtonStyle;
    private Texture2D _bgTexture;
    private Texture2D _sectionBg;
    private Texture2D _btnBg;
    private Texture2D _btnSmallBg;
    private bool _stylesInitialized = false;

    void Start()
    {
        _pet = GetComponent<DesktopPet>();
        _renderer = GetComponent<Live2DRenderer>();
        _window = GetComponent<WindowOverlay>();
        if (_window == null) _window = FindObjectOfType<WindowOverlay>();
    }

    void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // 背景
        _bgTexture = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.17f, 0.95f));
        _sectionBg = MakeTex(1, 1, new Color(0.12f, 0.12f, 0.14f, 0.9f));
        _btnBg = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.28f, 1f));
        _btnSmallBg = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.33f, 1f));

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
    }

    #region 公开接口

    public void Open(Vector2 screenPos)
    {
        _isOpen = true;

        // ★ 暂停宠物运动（右键菜单打开时不走路）
        if (_pet != null)
        {
            _pet.ForceStop();    // 先清零 petVx，让走路动画停止
            _pet.isPaused = true; // 再暂停物理状态机
        }

        // 复制当前权重
        _wLeftEdge = _pet.taskWeightMoveLeftEdge;
        _wRightEdge = _pet.taskWeightMoveRightEdge;
        _wLeftTime = _pet.taskWeightMoveLeftTime;
        _wRightTime = _pet.taskWeightMoveRightTime;
        _wStop = _pet.taskWeightStopTime;

        // 定位菜单（确保不超出屏幕）
        float x = Mathf.Clamp(screenPos.x, 10, Screen.width - _menuWidth - 10);
        float y = Mathf.Clamp(screenPos.y, 10, Screen.height - _menuHeight - 10);
        _menuRect = new Rect(x, y, _menuWidth, _menuHeight);

        Debug.Log($"[ContextMenu] 打开 位置=({x},{y})");
    }

    public void Close()
    {
        _isOpen = false;

        // 清除可能残留的回调（安全保底）
        if (_renderer != null)
            _renderer.OnForcedActionFinished -= OnForcedActionComplete;

        // ★ 恢复宠物运动
        if (_pet != null)
        {
            _pet.isPaused = false;
            _pet.Resume();
        }
    }

    public bool IsOpen => _isOpen;

    /// <summary>鼠标是否在菜单区域内</summary>
    public bool IsMouseOverMenu(Vector2 mousePos)
    {
        return _isOpen && _menuRect.Contains(mousePos);
    }

    #endregion

    void OnGUI()
    {
        if (!_isOpen) return;
        InitStyles();

        // ===== 背景 =====
        GUI.Box(_menuRect, GUIContent.none, new GUIStyle { normal = { background = _bgTexture } });

        GUILayout.BeginArea(_menuRect);

        // ===== 标题 =====
        GUILayout.Label("✦ 符玄 · 行为设置", _titleStyle);
        GUILayout.Space(2);

        _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true,
            GUILayout.Width(_menuWidth), GUILayout.Height(_menuHeight - 55));

        // ===== 权重设置 =====
        GUILayout.Label("⚙ 权重设置", _sectionStyle);
        GUILayout.Space(2);

        DrawWeightRow("向左走到边缘", ref _wLeftEdge, 0, 10);
        DrawWeightRow("向右走到边缘", ref _wRightEdge, 0, 10);
        DrawWeightRow("向左走定时", ref _wLeftTime, 0, 10);
        DrawWeightRow("向右走定时", ref _wRightTime, 0, 10);
        DrawWeightRow("停止", ref _wStop, 0, 10);

        GUILayout.Space(4);

        // 应用权重按钮
        if (GUILayout.Button("✓ 应用权重", _buttonStyle, GUILayout.Height(26)))
        {
            ApplyWeights();
        }

        GUILayout.Space(8);

        // ===== 强制动作 =====
        GUILayout.Label("▶ 强制动作", _sectionStyle);
        GUILayout.Space(2);

        DrawActionButtonRow(1, "歪头", 2, "微笑", 3, "挑眉");
        DrawActionButtonRow(4, "星辉", 5, "伸懒腰", 6, "爱心");
        DrawActionButtonRow(7, "数钱", 8, "委屈", 9, "法阵");
        DrawActionButtonRow(10, "害羞", 0, null, 0, null);

        GUILayout.Space(4);

        GUILayout.EndScrollView();

        // ===== 关闭按钮 =====
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕ 关闭", _closeButtonStyle, GUILayout.Width(80), GUILayout.Height(24)))
        {
            Close();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    #region 绘制辅助

    private void DrawWeightRow(string label, ref int value, int min, int max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _labelStyle, GUILayout.Width(130));

        // 减
        if (GUILayout.Button("-", _smallButtonStyle))
            value = Mathf.Max(min, value - 1);

        // 数值
        string valStr = value.ToString();
        GUIStyle valStyle = new GUIStyle(_labelStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fixedWidth = 30
        };
        GUILayout.Label(valStr, valStyle);

        // 加
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
                // 先停止走路
                if (_pet != null)
                    _pet.ForceStop();
                // 强制播放动作（锁定，不被走路覆盖）
                _renderer.ForceIdleAction(id);

                // 关闭菜单 UI（宠物保持暂停，等动作播完再恢复）
                _isOpen = false;

                // 清除旧回调 + 注册新回调
                _renderer.OnForcedActionFinished -= OnForcedActionComplete;
                _renderer.OnForcedActionFinished += OnForcedActionComplete;
            }
        }
    }

    /// <summary>强制动作播完后的回调 — 恢复宠物运动</summary>
    private void OnForcedActionComplete()
    {
        if (_renderer != null)
            _renderer.OnForcedActionFinished -= OnForcedActionComplete;

        if (_pet != null)
        {
            _pet.isPaused = false;
            _pet.Resume();
        }

        Debug.Log("[ContextMenu] 强制动作完成，宠物已恢复运动");
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

        // 如果当前是停止任务，无需重启；如果是走路任务也无需中断，下次切换生效
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
