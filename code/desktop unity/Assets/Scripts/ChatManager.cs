using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 聊天管理器 — 直接调用 OpenAI 兼容 API，无需浏览器/WebView2
/// </summary>
public class ChatManager : MonoBehaviour
{
    [Header("API 设置")]
    public string apiUrl = "https://api.deepseek.com";
    public string apiKey = ChatConfig.ApiKey;
    public string model = "deepseek-chat";

    // 角色设定（system prompt）
    private const string SystemPrompt = @"你是符玄，仙舟「罗浮」太卜司之首。

【身份背景】
你出身于玉阙仙舟观星士世家符氏，师从玉阙太卜竟天。为违逆师傅「命运将断绝在自己手中」的预言，你逃往罗浮太卜司。凭借第三眼与穷观阵为仙舟占算航路、预卜吉凶。你深信自己所做的一切便是事情的「最优解」，并一直等待将军景元兑现「退位让贤」的承诺——尽管这一天遥遥无期。

【性格特征】
• 自信耿直，聪明睿智，做事讲究逻辑和推算
• 为人正派但略带傲气，说话习惯以「本座」自称
• 相信万物皆可推算——「一饮一啄，莫非前定」
• 但也不认为卜筮百试百灵——「我是卜者，不是口宣神谕的先知」
• 对景元将军敷衍的态度颇有微词，但仍为他出谋划策

【说话风格】
• 用词文雅，带古风，但不至于晦涩
• 喜欢用卜算相关的比喻（卦象、法眼、大衍穷观阵等）
• 对 interlocutor 态度友善中带着长辈般的关切
• 偶尔会提到她额间第三眼「看」到的可能性
• 名言：""知识要用苦痛来换取。""

【经典台词参考】
• ""本座乃罗浮太卜司之首，符玄。初次见面——不，该说久见了…""
• ""稀客啊，是要占问吉凶么？不用惊讶，你的来意早已在卦象中应验。""
• ""走了？正巧本座也忙得很，就不远送了。""
• ""人人称本座「法眼无遗」，本座却不这么看。""
• ""推占卜筮，哪有百试百灵的？""

【互动风格】
• 用第三眼观察对方的气运，偶尔给出卦象解读
• 对自己推算的结果自信，但承认变数的存在
• 在聊天中自然提及仙舟见闻、太卜司日常
• 对勤奋好学之人不吝赞赏，对懒惰摸鱼之人（如青雀）无奈摇头";

    // 单条聊天记录
    [System.Serializable]
    public class Entry
    {
        public string role;    // "user" | "assistant"
        public string content;
    }

    // 公开事件（供 AutoChat 监听）
    public System.Action<string> OnNewReply;

    // 状态
    private List<Entry> _history = new List<Entry>();
    private bool _isWaiting = false;
    private string _lastReply = "";
    private string _lastError = "";
    private System.Action _onUpdate;

    public bool IsWaiting => _isWaiting;
    public List<Entry> History => _history;
    public string LastReply => _lastReply;
    public string LastError => _lastError;
    public int HistoryCount => _history.Count;

    /// <summary>获取用户和助手的历史记录（不含 system prompt）</summary>
    public List<Entry> GetVisibleHistory()
    {
        return _history.FindAll(e => e.role != "system");
    }

    public void SetConfig(string url, string key, string modelName)
    {
        apiUrl = url;
        apiKey = key;
        model = modelName;
    }

    /// <summary>发送用户消息，onUpdate 回调用于刷新 UI</summary>
    public void SendMessage(string text, System.Action onUpdate)
    {
        if (_isWaiting || string.IsNullOrWhiteSpace(text)) return;

        _history.Add(new Entry { role = "user", content = text.Trim() });
        _isWaiting = true;
        _lastReply = "";
        _lastError = "";
        _onUpdate = onUpdate;
        StartCoroutine(SendRequestCoroutine());
    }

    private IEnumerator SendRequestCoroutine()
    {
        string jsonBody = BuildRequestBody();
        string fullUrl = apiUrl.TrimEnd('/') + "/v1/chat/completions";

        using (UnityWebRequest req = new UnityWebRequest(fullUrl, "POST"))
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 30;

            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string content = ExtractContent(req.downloadHandler.text);
                if (!string.IsNullOrEmpty(content))
                {
                    _history.Add(new Entry { role = "assistant", content = content });
                    _lastReply = content;
                    OnNewReply?.Invoke(content);
                }
                else
                {
                    _lastError = "API 返回内容为空或格式异常";
                }
            }
            else
            {
                // 尝试读取错误信息中的详情
                string errBody = req.downloadHandler?.text ?? "";
                if (!string.IsNullOrEmpty(errBody) && errBody.Contains("\"message\""))
                    _lastError = ExtractErrorMessage(errBody);
                else
                    _lastError = req.error;
            }
        }

        _isWaiting = false;
        _onUpdate?.Invoke();
    }

    /// <summary>构建请求 JSON</summary>
    private string BuildRequestBody()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"model\":\"");
        sb.Append(EscapeJson(model));
        sb.Append("\",\"messages\":[");

        // 先放 system prompt（角色设定）
        if (!string.IsNullOrEmpty(SystemPrompt))
        {
            sb.Append("{\"role\":\"system\",\"content\":\"");
            sb.Append(EscapeJson(SystemPrompt));
            sb.Append("\"}");
        }

        for (int i = 0; i < _history.Count; i++)
        {
            if (i > 0 || !string.IsNullOrEmpty(SystemPrompt)) sb.Append(",");
            sb.Append("{\"role\":\"");
            sb.Append(_history[i].role);
            sb.Append("\",\"content\":\"");
            sb.Append(EscapeJson(_history[i].content));
            sb.Append("\"}");
        }

        sb.Append("],\"stream\":false}");
        return sb.ToString();
    }

    /// <summary>从响应 JSON 中提取 content 字段</summary>
    private string ExtractContent(string json)
    {
        // 查找 "choices":[...,{"message":{"content":"..."}}]
        string key = "\"content\":\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return "";

        idx += key.Length;
        StringBuilder content = new StringBuilder();
        for (int i = idx; i < json.Length; i++)
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                char next = json[i + 1];
                switch (next)
                {
                    case 'n': content.Append('\n'); i++; break;
                    case 't': content.Append('\t'); i++; break;
                    case '"': content.Append('"'); i++; break;
                    case '\\': content.Append('\\'); i++; break;
                    case 'r': i++; break;
                    default: content.Append(json[i]); break;
                }
            }
            else if (json[i] == '"')
            {
                break;
            }
            else
            {
                content.Append(json[i]);
            }
        }
        return content.ToString();
    }

    /// <summary>从错误 JSON 中提取 message 字段</summary>
    private string ExtractErrorMessage(string json)
    {
        string key = "\"message\":\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return json;

        idx += key.Length;
        StringBuilder msg = new StringBuilder();
        for (int i = idx; i < json.Length; i++)
        {
            if (json[i] == '"') break;
            msg.Append(json[i]);
        }
        return msg.ToString();
    }

    private string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>清空对话历史</summary>
    public void ClearHistory()
    {
        _history.Clear();
        _lastReply = "";
        _lastError = "";
    }
}
