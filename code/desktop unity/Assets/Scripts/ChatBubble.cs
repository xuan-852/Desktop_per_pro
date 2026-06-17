using UnityEngine;

/// <summary>
/// 聊天气泡 — 在模型上方显示浮动的 AI 对话气泡
/// OnGUI 绘制，圆角 + 小尾巴 + 淡入淡出动画
/// 参考 Live2DPet (Electron/HTML) 设计风格
/// </summary>
public class ChatBubble : MonoBehaviour
{
    [Header("显示设置")]
    public float displayDuration = 5f;
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.5f;

    [Header("尺寸")]
    public float maxWidth = 320f;
    public float minWidth = 160f;
    public float cornerRadius = 12f;
    public float tailHeight = 14f;
    public float tailWidth = 20f;
    public float topOffset = 20f;     // 模型头顶到气泡的距离
    public float shadowOffset = 3f;

    [Header("配色 — 符玄紫灰主题")]
    public Color bgColor = new Color(0.16f, 0.13f, 0.20f);         // 内背景
    public Color borderColor = new Color(0.10f, 0.08f, 0.14f);     // 外框
    public Color accentColor = new Color(0.72f, 0.48f, 0.84f);     // 紫色装饰线
    public Color textColor = new Color(0.95f, 0.92f, 0.97f);       // 浅紫白文字
    public Color shadowColor = new Color(0f, 0f, 0f, 0.25f);

    private DesktopPet _pet;
    private Live2DRenderer _renderer;
    private string _currentText = "";
    private float _messageStartTime = -1f;
    private bool _hasMessage = false;

    // === 动画状态机 ===
    private enum BubbleState { Hidden, FadingIn, Showing, FadingOut }
    private BubbleState _state = BubbleState.Hidden;
    private float _animProgress = 0f;

    // === 缓存的气泡尺寸 ===
    private float _bubbleWidth;
    private float _bubbleHeight;
    private float _textWidth;
    private float _textHeight;

    // === 纹理 / 样式 ===
    private Texture2D _bgTex;         // 圆角矩形（内背景）
    private Texture2D _borderTex;     // 圆角矩形（外框）
    private Texture2D _shadowTex;     // 圆角矩形（阴影）
    private Texture2D _tailTex;       // 三角形小尾巴
    private Texture2D _accentTex;     // 1x1 紫色装饰条
    private GUIStyle _textStyle;
    private bool _needsRebuild = true;

    void Start()
    {
        _pet = GetComponent<DesktopPet>();
        if (_pet == null) _pet = FindObjectOfType<DesktopPet>();
        _renderer = GetComponent<Live2DRenderer>();
        if (_renderer == null) _renderer = FindObjectOfType<Live2DRenderer>();
    }

    // ============================================================
    //  公开接口
    // ============================================================

    /// <summary>显示一条消息（自动淡出）</summary>
    public void ShowMessage(string text, float duration = 5f)
    {
        _currentText = text;
        displayDuration = duration;
        _messageStartTime = Time.time;
        _hasMessage = true;
        _state = BubbleState.FadingIn;
        _animProgress = 0f;
        _needsRebuild = true;
    }

    /// <summary>立即隐藏气泡</summary>
    public void Hide()
    {
        if (_state == BubbleState.Hidden) return;
        _state = BubbleState.FadingOut;
        _animProgress = 0f;
    }

    /// <summary>气泡是否正在显示（含淡入/淡出动画过程中）</summary>
    public bool IsShowing
    {
        get { return _hasMessage && _state != BubbleState.Hidden; }
    }

    // ============================================================
    //  Update — 驱动动画状态机
    // ============================================================

    void Update()
    {
        if (!_hasMessage) return;

        switch (_state)
        {
            case BubbleState.FadingIn:
                _animProgress += Time.deltaTime / fadeInDuration;
                if (_animProgress >= 1f) { _animProgress = 1f; _state = BubbleState.Showing; }
                break;

            case BubbleState.Showing:
                if (Time.time - _messageStartTime > displayDuration) Hide();
                break;

            case BubbleState.FadingOut:
                _animProgress += Time.deltaTime / fadeOutDuration;
                if (_animProgress >= 1f)
                {
                    _state = BubbleState.Hidden;
                    _hasMessage = false;
                    _currentText = "";
                }
                break;
        }
    }

    // ============================================================
    //  OnGUI — 绘制气泡
    // ============================================================

    void OnGUI()
    {
        if (!_hasMessage || _pet == null || _state == BubbleState.Hidden) return;

        // ---- 计算动画值 ----
        float alpha = 1f;
        float scale = 1f;
        switch (_state)
        {
            case BubbleState.FadingIn:
                alpha = _animProgress;
                scale = Mathf.Lerp(0.92f, 1f, _animProgress);
                break;
            case BubbleState.FadingOut:
                alpha = 1f - _animProgress;
                scale = Mathf.Lerp(1f, 0.92f, _animProgress);
                break;
        }

        if (_needsRebuild) RebuildTextures();
        if (_bgTex == null) return;

        // ---- 定位 ----
        float centerX = _pet.petX + _pet.petWidth / 2f;
        // 气泡跟随模型视觉位置（考虑垂直偏移）
        float visualOffset = (_renderer != null) ? _renderer.verticalOffset : 0f;
        float bubbleBottom = Mathf.Max(_pet.petY + visualOffset - topOffset, 10f);

        float bx = centerX - _bubbleWidth / 2f;
        float by = bubbleBottom - _bubbleHeight - tailHeight;
        bx = Mathf.Clamp(bx, 5f, Screen.width - _bubbleWidth - 5f);
        by = Mathf.Clamp(by, 5f, Screen.height - _bubbleHeight - tailHeight - 5f);

        // 气泡中心（用于缩放）
        float cx = bx + _bubbleWidth / 2f;
        float cy = by + _bubbleHeight / 2f;

        float sw = _bubbleWidth * scale;
        float sh = _bubbleHeight * scale;
        Rect bgRect = new Rect(cx - sw / 2f, cy - sh / 2f, sw, sh);

        Color orig = GUI.color;

        // ——— 阴影 ———
        GUI.color = new Color(0f, 0f, 0f, 0.22f * alpha);
        GUI.DrawTexture(new Rect(bgRect.x + shadowOffset, bgRect.y + shadowOffset, bgRect.width, bgRect.height), _shadowTex);

        // ——— 外框 ———
        GUI.color = new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * alpha);
        GUI.DrawTexture(bgRect, _borderTex);

        // ——— 内背景 ———
        Rect innerRect = new Rect(bgRect.x + 1, bgRect.y + 1, bgRect.width - 2, bgRect.height - 2);
        GUI.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * alpha);
        GUI.DrawTexture(innerRect, _bgTex);

        // ——— 紫色装饰线 ———
        GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.75f * alpha);
        GUI.DrawTexture(new Rect(bgRect.x + 5, bgRect.y + 2, bgRect.width - 10, 3f), _accentTex);

        // ——— 小尾巴（气泡底部中央朝下） ———
        float tailX = (bx + _bubbleWidth / 2f) - tailWidth / 2f;
        float tailY = bgRect.y + bgRect.height;
        GUI.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * alpha);
        GUI.DrawTexture(new Rect(tailX, tailY, tailWidth, tailHeight), _tailTex);

        // ——— 文字（居中） ———
        GUI.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        Rect textRect = new Rect(bgRect.x + 14, bgRect.y + 14, _textWidth, _textHeight);
        GUI.Label(textRect, _currentText, _textStyle);

        GUI.color = orig;
    }

    // ============================================================
    //  纹理生成
    // ============================================================

    private void RebuildTextures()
    {
        _needsRebuild = false;

        // 1) 初始化样式 & 计算尺寸
        _textStyle = new GUIStyle
        {
            normal = { textColor = textColor },
            fontSize = 13,
            wordWrap = true,
            alignment = TextAnchor.UpperCenter,
            richText = true
        };

        _textWidth = maxWidth - 28;
        _textHeight = _textStyle.CalcHeight(new GUIContent(_currentText), _textWidth);
        _bubbleWidth = Mathf.Clamp(_textWidth + 28, minWidth, maxWidth);
        _textWidth = _bubbleWidth - 28;  // 重新计算（因为宽度变了）
        _textHeight = _textStyle.CalcHeight(new GUIContent(_currentText), _textWidth);
        _bubbleHeight = _textHeight + 28 + 4;

        // 2) 生成纹理
        int w = Mathf.RoundToInt(_bubbleWidth);
        int h = Mathf.RoundToInt(_bubbleHeight);
        int r = Mathf.RoundToInt(cornerRadius);

        _bgTex = GenRoundedRect(w, h, r, bgColor);
        _borderTex = GenRoundedRect(w, h, r, borderColor);
        _shadowTex = GenRoundedRect(w, h, r, new Color(0f, 0f, 0f, 0.22f));
        _accentTex = MakeTex(1, 1, accentColor);
        _tailTex = GenTriangle(Mathf.RoundToInt(tailWidth), Mathf.RoundToInt(tailHeight), bgColor);
    }

    // ---------- 圆角矩形 ----------

    private static Texture2D GenRoundedRect(int w, int h, float r, Color c)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        Color t = new Color(0, 0, 0, 0);
        float r2 = r * r;
        float rw = w - r - 1;
        float rh = h - r - 1;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool draw;
                if (x < r && y < r)
                    draw = (x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f) <= r2;
                else if (x > rw && y < r)
                    draw = (x - rw - 0.5f) * (x - rw - 0.5f) + (y - r + 0.5f) * (y - r + 0.5f) <= r2;
                else if (x < r && y > rh)
                    draw = (x - r + 0.5f) * (x - r + 0.5f) + (y - rh - 0.5f) * (y - rh - 0.5f) <= r2;
                else if (x > rw && y > rh)
                    draw = (x - rw - 0.5f) * (x - rw - 0.5f) + (y - rh - 0.5f) * (y - rh - 0.5f) <= r2;
                else
                    draw = true;

                tex.SetPixel(x, y, draw ? c : t);
            }
        }
        tex.Apply();
        return tex;
    }

    // ---------- 三角形（朝下的小尾巴） ----------

    private static Texture2D GenTriangle(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        Color t = new Color(0, 0, 0, 0);
        float cx = (w - 1f) / 2f;

        for (int y = 0; y < h; y++)
        {
            float progress = (float)y / h; // 0 = 顶, 1 = 底
            float halfW = (1f - progress) * (w - 1f) / 2f;
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, y, (x >= cx - halfW && x <= cx + halfW) ? c : t);
            }
        }
        tex.Apply();
        return tex;
    }

    // ---------- 纯色 1×1 ----------

    private static Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, c);
        tex.Apply();
        return tex;
    }
}
