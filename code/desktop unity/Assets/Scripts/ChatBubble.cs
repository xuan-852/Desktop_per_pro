using UnityEngine;

/// <summary>
/// 聊天气泡 — 在模型上方显示浮动的 AI 对话气泡
/// OnGUI 绘制，自动淡出，跟随模型位置
/// </summary>
public class ChatBubble : MonoBehaviour
{
    private DesktopPet _pet;

    private string _currentText = "";
    private float _showStartTime = 0f;
    private float _displayDuration = 5f;
    private float _fadeDuration = 0.8f;
    private bool _hasMessage = false;

    // 气泡尺寸
    private const float BUBBLE_WIDTH = 280f;
    private const float BUBBLE_PADDING = 14f;
    private const float TOP_MARGIN = 25f; // 模型头顶到气泡底部距离

    // 样式
    private GUIStyle _textStyle;
    private Texture2D _bgTex;
    private Texture2D _borderTex;
    private Texture2D _accentTex;
    private bool _stylesInit = false;

    void Start()
    {
        _pet = GetComponent<DesktopPet>();
        if (_pet == null) _pet = FindObjectOfType<DesktopPet>();
    }

    /// <summary>显示一条消息（自动淡出）</summary>
    public void ShowMessage(string text, float duration = 5f)
    {
        _currentText = text;
        _displayDuration = duration;
        _showStartTime = Time.time;
        _hasMessage = true;
    }

    /// <summary>立即隐藏气泡</summary>
    public void Hide()
    {
        _hasMessage = false;
        _currentText = "";
    }

    void OnGUI()
    {
        if (!_hasMessage || string.IsNullOrEmpty(_currentText) || _pet == null) return;

        float elapsed = Time.time - _showStartTime;
        if (elapsed > _displayDuration + _fadeDuration)
        {
            _hasMessage = false;
            return;
        }

        // 透明度
        float alpha = 1f;
        if (elapsed > _displayDuration)
            alpha = 1f - (elapsed - _displayDuration) / _fadeDuration;

        if (!_stylesInit) InitStyles();

        // 气泡定位：模型头顶上方
        float centerX = _pet.petX + _pet.petWidth / 2f;
        float bubbleBottom = Mathf.Max(_pet.petY - TOP_MARGIN, 10f);

        // 文本尺寸
        float textWidth = BUBBLE_WIDTH - BUBBLE_PADDING * 2;
        float textHeight = _textStyle.CalcHeight(new GUIContent(_currentText), textWidth);
        float bubbleHeight = textHeight + BUBBLE_PADDING * 2 + 6;

        // 气泡 Rect
        float bx = Mathf.Clamp(centerX - BUBBLE_WIDTH / 2f, 5f, Screen.width - BUBBLE_WIDTH - 5f);
        float by = Mathf.Clamp(bubbleBottom - bubbleHeight, 5f, Screen.height - bubbleHeight - 5f);
        Rect bgRect = new Rect(bx, by, BUBBLE_WIDTH, bubbleHeight);

        Color origColor = GUI.color;

        // ——— 背景（深色） ———
        GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.95f * alpha);
        GUI.Box(bgRect, GUIContent.none, new GUIStyle { normal = { background = _borderTex } });

        // ——— 内层（稍亮） ———
        Rect innerRect = new Rect(bx + 1, by + 1, BUBBLE_WIDTH - 2, bubbleHeight - 2);
        GUI.color = new Color(0.14f, 0.14f, 0.17f, 0.92f * alpha);
        GUI.Box(innerRect, GUIContent.none, new GUIStyle { normal = { background = _bgTex } });

        // ——— 顶部紫色装饰线 ———
        GUI.color = new Color(0.6f, 0.35f, 0.7f, 0.8f * alpha);
        GUI.Box(new Rect(bx + 2, by + 1, BUBBLE_WIDTH - 4, 2f), GUIContent.none,
            new GUIStyle { normal = { background = _accentTex } });

        // ——— 文字 ———
        GUI.color = new Color(1f, 1f, 1f, alpha);
        Rect textRect = new Rect(bx + BUBBLE_PADDING, by + BUBBLE_PADDING + 3, textWidth, textHeight);
        GUI.Label(textRect, _currentText, _textStyle);

        GUI.color = origColor;
    }

    private void InitStyles()
    {
        _stylesInit = true;
        _bgTex = MakeTex(1, 1, new Color(0.14f, 0.14f, 0.17f));
        _borderTex = MakeTex(1, 1, new Color(0.08f, 0.08f, 0.1f));
        _accentTex = MakeTex(1, 1, new Color(0.6f, 0.35f, 0.7f));

        _textStyle = new GUIStyle
        {
            normal = { textColor = Color.white },
            fontSize = 12,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(0, 0, 0, 0),
            richText = true
        };
    }

    private static Texture2D MakeTex(int w, int h, Color c)
    {
        Texture2D tex = new Texture2D(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, c);
        tex.Apply();
        return tex;
    }
}
