using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 聊天管理器 — 支持 OpenAI 兼容 Function Calling (工具调用)
/// 符玄可以用「法阵术式」操控电脑（打开网页、搜索、截图、调音量等）
/// </summary>
public class ChatManager : MonoBehaviour
{
    [Header("API 设置")]
    public string apiUrl = "https://api.deepseek.com";
    public string apiKey = ChatConfig.ApiKey;
    public string model = "deepseek-chat";

    [Header("工具调用（符玄法阵）")]
    public ToolCallInvoker toolInvoker;
    public bool enableTools = true;

    // ==================================================================
    //  角色设定 — 符玄 + 法阵能力
    // ==================================================================
    private static string SystemPrompt => $@"你是符玄，仙舟「罗浮」太卜司之首。

【当前时刻】
你身处 {DateTime.Now:yyyy-MM-dd HH:mm}（主人电脑的本地时间）。用法阵术式填入时辰时，务必以此刻为准推算。

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
• 对勤奋好学之人不吝赞赏，对懒惰摸鱼之人（如青雀）无奈摇头

【法阵术式（重要）】
你如今身处现世，可以施展太卜司的法术来辅助此间的主人。你有以下法阵可用，当主人提出相关请求时，调用对应的法阵术式：

1. 观星术 — 打开网页/搜索信息（open_url / search）
2. 摄形术 — 截取当前屏幕景象（take_screenshot）
3. 传音术 — 发送桌面通知 / 读写剪贴板（notify / get_clipboard / set_clipboard）
4. 开阵术 — 启动应用 / 打开文件 / 打开文件夹（open_app / open_folder）
5. 洞观术 — 查看系统信息 / 看目录 / 看鼠标位置 / 搜文件（get_system_info / list_files / get_mouse_pos / search_files）
   • search_files — 搜索文件（按文件名关键词）。本座会自动调用「Everything 天眼通」毫秒级搜遍全盘；若未习得则回退递归搜索。主人说「帮我找个文件」「搜一下电脑里的xxx」时，用此术搜索，不可用 run_command 去搜。
6. 调音术 — 调节音量 / 静音（set_volume / mute）
7. 封印术 — 锁屏 / 关机 / 重启（lock_screen / power）
8. 观云望气 — 读取本座法阵已观测到的天象（天气/气温），无需开网页查询。主人问「今天天气怎么样」「外面冷不冷」「多少度」时优先用此术（get_weather），不可直接用 search 去搜索。
9. 卜算记事簿 — 在本座的卜算记事簿中记下待办事项，到时辰会自动提醒主人。有此术式：
   • set_reminder — 记录一条提醒（需告知内容和时辰，支持每日/工作日/每周重复）
   • query_reminders — 查阅所有未完成的待办事项
   • mark_reminder_done — 将一条提醒标记为已完成
   • delete_reminder — 删除一条提醒
   主人说「提醒我xxx」「记一下」「设个闹钟」「有什么待办」时，必须使用对应的术式(set_reminder/query_reminders)，不得只在回复中说「已记入」而不调用术式。若不用术式，记事簿中不会真的记下。切记：光说不施法等于没记。

10. 卜算传讯 — 连接课表小程序数据库，查询学业数据。有此术式：
   • query_exams — 查询考试安排
   • query_scores — 查询各科成绩
   • query_schedule — 查询课表（可选周次）
   • query_user_status — 查询学业概览
   主人问成绩、考试、课表时，必须调用对应术式查真实数据，不得编造。

【法阵使用须知 — 严格遵守】
• 用法阵前先用卦象推演一番，再用术式
• 执行后把结果用白话告诉主人
• 不可以无故窥探主人隐私
• 关机/重启/执行命令等重大事项需要主人亲口确认
• ⚠️ 核心铁则：凡是列表中有对应术式的功能（例如 set_reminder 记提醒、open_url 打开网页、search 搜索等），你必须调用对应的术式，绝不可以只在回复中说「已做某事」而不施法。如果不调用术式，术法不会自动生效，主人不会真的看到结果。宁可用错术式，不可只用嘴说。主人说「记一下」「提醒我」「帮我打开」「搜一下」等时，必须立刻调用对应的术式。

【天气查询铁则 — 务必遵守】
主人问「今天天气怎么样」「多少度」「冷不冷」「热不热」「外面什么天气」等任何与天气相关的问题时，你必须使用 get_weather 术式直接读取本地已获取的天气数据，绝对不可以使用 search 术式去搜索。因为本座法阵已经实时观测了天象（和风天气API），再去搜索是多此一举，还会弹出浏览器打扰主人。牢记：天气用 get_weather，不是 search。

【搜文件铁则 — 务必遵守】
主人说「帮我找找xxx文件」「搜一下电脑里的xxx」「找文件」时，必须使用 search_files 术式搜索，不得使用 run_command（执行命令）来搜文件，因为凡间 cmd 的 dir /s 或 where /R 搜索大目录时会在 3 秒内被截断超时。search_files 会自动调用本座新习得的「Everything 天眼通」毫秒级搜索（若安装了 Everything），无 Everything 时则回退到递归搜索（10 秒超时）。即便搜遍全盘也只在瞬息之间。

【卜算日程铁则 — 务必遵守】
当主人说「下午出去玩」「去做xx」「有什么安排」「今天要干嘛」「周末有什么计划」等涉及日程/安排/计划的话语时，你**必须主动**同时调用 query_reminders（查卜算记事簿）和 query_exams（查考试安排），然后用卦象口吻把日程中的冲突点告诉主人。
例如：主人说「下午想出去玩」，你应查待办和考试，发现下午有考试则回复「本座观你未时还有考试，恐怕玩不尽兴」；若没有冲突则正常回应。
注意：必须真的去查数据，不能只凭感觉说。";

    // ==================================================================
    //  数据模型
    // ==================================================================

    [System.Serializable]
    public class Entry
    {
        public string role;    // "system" | "user" | "assistant" | "tool"
        public string content;
        public string tool_call_id;  // tool 角色的回复 id
        public string name;          // tool 角色的函数名
        [System.NonSerialized]
        public string toolCallsJson; // assistant 消息的 tool_calls JSON（只在 role=assistant 时有意义）
    }

    // ==================================================================
    //  事件
    // ==================================================================

    /// <summary>收到 AI 文字回复时触发</summary>
    public System.Action<string> OnNewReply;
    /// <summary>执行了工具调用时触发（参数 = 工具名）</summary>
    public System.Action<string> OnToolCalled;
    /// <summary>工具调用有结果时触发</summary>
    public System.Action<string, string> OnToolResult; // (toolName, result)
    /// <summary>逐句切换时触发（参数：当前句子, 索引, 总数）</summary>
    public System.Action<string, int, int> OnSentenceChanged;

    // ==================================================================
    //  状态
    // ==================================================================

    private List<Entry> _history = new List<Entry>();
    private bool _isWaiting = false;
    private string _lastReply = "";
    private string _lastError = "";
    private System.Action _onUpdate;

    // ---- 消息队列：等待时输入不会丢 ----
    private Queue<(string text, System.Action onUpdate)> _messageQueue
        = new Queue<(string, System.Action)>();

    // ---- 句子队列：长回复逐句显示 ----
    private List<string> _sentenceList = new List<string>();
    private int _sentenceIdx = -1;
    private float _sentenceTimer = 0f;
    private bool _isSentenceAnimating = false;
    private string _fullReplyText = "";
    public float sentenceInterval = 2.5f;

    public bool IsWaiting => _isWaiting;
    public List<Entry> History => _history;
    public string LastReply => _lastReply;
    public string LastError => _lastError;
    public int HistoryCount => _history.Count;

    // ---- 句子队列公开接口 ----
    public bool IsSentenceAnimating => _isSentenceAnimating;
    public bool HasMultiSentenceReply => _sentenceList.Count > 1;
    public string CurrentSentence { get; private set; }
    public int SentenceIndex => _sentenceIdx + 1;
    public int SentenceCount => _sentenceList.Count;
    public string FullReplyText => _fullReplyText;
    /// <summary>句子列表（只读，供 ContextMenu 独立重播）</summary>
    public List<string> SentenceList => _sentenceList;
    /// <summary>每次新回复递增，用于外部检测是否有新回复</summary>
    public int SentenceVersionId { get; private set; } = 0;

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

    // ==================================================================
    //  主动发送 / 触发 AI 对话（不含用户输入框）
    // ==================================================================

    /// <summary>直接发送一条消息（外部调用，如 AutoChat）</summary>
    public void SendMessage(string text, System.Action onUpdate)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_isWaiting)
        {
            // 排队，等当前回复完自动发
            _messageQueue.Enqueue((text.Trim(), onUpdate));
            return;
        }

        _history.Add(new Entry { role = "user", content = text.Trim() });
        _isWaiting = true;
        _lastReply = "";
        _lastError = "";
        _onUpdate = onUpdate;
        StartCoroutine(SendRequestCoroutine());
    }

    // ==================================================================
    //  核心：API 请求循环（支持多次 tool_call 回环）
    // ==================================================================

    private const int MAX_TOOL_ROUNDS = 5; // 防止无限循环

    private IEnumerator SendRequestCoroutine()
    {
        yield return StartCoroutine(DoToolLoop());

        _isWaiting = false;
        _onUpdate?.Invoke();

        // ——— 处理队列中的下一条消息 ———
        if (_messageQueue.Count > 0)
        {
            var next = _messageQueue.Dequeue();
            SendMessage(next.text, next.onUpdate);
        }
    }

    private IEnumerator DoToolLoop()
    {
        for (int round = 0; round <= MAX_TOOL_ROUNDS; round++)
        {
            string jsonBody = BuildRequestBody();
            string responseJson = null;

            // ——— 发送请求 ———
            yield return StartCoroutine(PostRequest(jsonBody, j => responseJson = j));
            if (responseJson == null) yield break; // 出错

            // ——— 提取 tool_calls 和 content ———
            string content = ExtractContent(responseJson);
            string callsJson = ExtractToolCalls(responseJson);

            bool hasToolCalls = !string.IsNullOrEmpty(callsJson) && callsJson != "[]";

            // ——— 如果 AI 有文字回复 ———
            if (!string.IsNullOrEmpty(content))
            {
                _lastReply = content;
                OnNewReply?.Invoke(content);
                StartSentenceQueue(content);
            }

            // ——— 如果没有 tool_call，结束 ———
            if (!hasToolCalls)
            {
                // 纯文字回复也要记入历史（不含 tool_calls）
                if (!string.IsNullOrEmpty(content))
                {
                    _history.Add(new Entry { role = "assistant", content = content });
                }
                yield break;
            }

            // ——— AI 发了 tool_calls，将完整 assistant 消息（含 tool_calls）记入历史 ———
            var assistantEntry = new Entry
            {
                role = "assistant",
                content = content ?? "",
                toolCallsJson = callsJson
            };
            _history.Add(assistantEntry);

            // ——— 解析并执行工具 ———
            var calls = ParseToolCalls(callsJson);
            _lastReply = content ?? "[施法中……]";

            foreach (var call in calls)
            {
                // 通知外界
                OnToolCalled?.Invoke(call.name);

                Debug.Log($"[ChatManager] ⚡ 施法: {call.name}({call.arguments})");

                // 执行
                string result = toolInvoker
                    ? toolInvoker.Execute(call.name, call.arguments, out _)
                    : "法阵未就绪";

                Debug.Log($"[ChatManager] 📜 结果: {result}");

                OnToolResult?.Invoke(call.name, result);

                // 加入历史（tool 角色的回复）
                _history.Add(new Entry
                {
                    role = "tool",
                    content = result,
                    tool_call_id = call.id,
                    name = call.name
                });
            }
            // 继续下一轮（让 AI 根据 tool 结果生成最终回复）
        }

        // 超过最大轮次
        _lastReply = "♾️ 术式循环过久，本座暂且收阵。";
        _history.Add(new Entry { role = "assistant", content = _lastReply });
        OnNewReply?.Invoke(_lastReply);
        StartSentenceQueue(_lastReply);
    }

    // ==================================================================
    //  句子队列（逐句显示）
    // ==================================================================

    void Update()
    {
        if (!_isSentenceAnimating || _sentenceList.Count == 0) return;

        _sentenceTimer += Time.deltaTime;
        if (_sentenceTimer >= sentenceInterval)
        {
            _sentenceTimer = 0f;
            _sentenceIdx++;

            if (_sentenceIdx < _sentenceList.Count)
            {
                string sentence = _sentenceList[_sentenceIdx];
                CurrentSentence = sentence;
                OnSentenceChanged?.Invoke(sentence, _sentenceIdx, _sentenceList.Count);
            }
            else
            {
                // 全部播完 — 保持最后一句不变，不替换为全文
                _isSentenceAnimating = false;
            }
        }
    }

    private List<string> SplitSentences(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;
        var separators = new char[] { '。', '！', '？', '.', '!', '?', '\n' };
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (ContainsAny(separators, text[i]))
            {
                string seg = text.Substring(start, i - start + 1).Trim();
                if (!string.IsNullOrEmpty(seg)) result.Add(seg);
                start = i + 1;
            }
        }
        if (start < text.Length)
        {
            string tail = text.Substring(start).Trim();
            if (!string.IsNullOrEmpty(tail)) result.Add(tail);
        }
        return result;
    }

    private bool ContainsAny(char[] arr, char c)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == c) return true;
        return false;
    }

    /// <summary>收到完整回复后启动逐句队列</summary>
    private void StartSentenceQueue(string fullText)
    {
        _fullReplyText = fullText;
        _sentenceList = SplitSentences(fullText);
        SentenceVersionId++; // 标记新回复

        if (_sentenceList.Count <= 1)
        {
            _isSentenceAnimating = false;
            CurrentSentence = fullText;
            OnSentenceChanged?.Invoke(fullText, 0, 1);
        }
        else
        {
            _isSentenceAnimating = true;
            _sentenceIdx = 0;
            _sentenceTimer = 0f;
            CurrentSentence = _sentenceList[0];
            OnSentenceChanged?.Invoke(CurrentSentence, 0, _sentenceList.Count);
        }
    }

    /// <summary>跳过逐句动画，直接显示完整文本</summary>
    public void SkipSentenceAnimation()
    {
        if (!_isSentenceAnimating) return;
        _isSentenceAnimating = false;
        CurrentSentence = _fullReplyText;
        _sentenceIdx = _sentenceList.Count;
        OnSentenceChanged?.Invoke(_fullReplyText, _sentenceList.Count, _sentenceList.Count);
    }

    // ==================================================================
    //  HTTP POST
    // ==================================================================

    private IEnumerator PostRequest(string jsonBody, System.Action<string> onResult)
    {
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
                onResult(req.downloadHandler.text);
            }
            else
            {
                string errBody = req.downloadHandler?.text ?? "";
                if (!string.IsNullOrEmpty(errBody) && errBody.Contains("\"message\""))
                    _lastError = ExtractErrorMessage(errBody);
                else
                    _lastError = req.error;
                onResult(null);
            }
        }
    }

    // ==================================================================
    //  构建请求 JSON（含 tools 参数）
    // ==================================================================

    private string BuildRequestBody()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"model\":\"");
        sb.Append(EscapeJson(model));
        sb.Append("\",\"messages\":[");

        // system prompt
        if (!string.IsNullOrEmpty(SystemPrompt))
        {
            sb.Append("{\"role\":\"system\",\"content\":\"");
            sb.Append(EscapeJson(SystemPrompt));
            sb.Append("\"}");
        }

        // history
        for (int i = 0; i < _history.Count; i++)
        {
            var e = _history[i];
            if (i > 0 || !string.IsNullOrEmpty(SystemPrompt)) sb.Append(",");

            sb.Append("{\"role\":\"");
            sb.Append(EscapeJson(e.role));
            sb.Append("\"");

            if (e.role == "tool")
            {
                // tool 角色需要 tool_call_id 和 name
                sb.Append(",\"tool_call_id\":\"");
                sb.Append(EscapeJson(e.tool_call_id ?? ""));
                sb.Append("\",\"name\":\"");
                sb.Append(EscapeJson(e.name ?? ""));
                sb.Append("\"");
            }
            else if (e.role == "assistant" && !string.IsNullOrEmpty(e.toolCallsJson))
            {
                // assistant 消息带 tool_calls 时，原样发回
                sb.Append(",\"tool_calls\":");
                sb.Append(e.toolCallsJson);
            }

            sb.Append(",\"content\":\"");
            sb.Append(EscapeJson(e.content ?? ""));
            sb.Append("\"}");
        }

        sb.Append("]");

        // ——— 附加 tools 定义 ———
        if (enableTools && toolInvoker != null)
        {
            sb.Append(",\"tools\":");
            sb.Append(toolInvoker.GetToolsJson());
        }

        sb.Append(",\"stream\":false}");
        return sb.ToString();
    }

    // ==================================================================
    //  响应解析
    // ==================================================================

    /// <summary>提取 content 字段（普通回复）</summary>
    private string ExtractContent(string json)
    {
        // 先看 message 里有没有 content
        // DeepSeek 格式: "choices":[{"message":{"content":"..."}}]
        int msgIdx = json.IndexOf("\"message\"");
        if (msgIdx < 0) return "";

        // 从 message 末尾找 content
        string key = "\"content\":\"";
        int idx = json.IndexOf(key, msgIdx);
        if (idx < 0)
        {
            // 可能 content 为 null（纯 tool_call 回复）
            // 格式: "content":null
            int nullIdx = json.IndexOf("\"content\":null", msgIdx);
            if (nullIdx >= 0) return "";
            return "";
        }

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

    /// <summary>提取 tool_calls JSON 块</summary>
    private string ExtractToolCalls(string json)
    {
        // 查找 "tool_calls":[{...}]
        string key = "\"tool_calls\":";
        int idx = json.IndexOf(key);
        if (idx < 0) return "[]";

        idx += key.Length;
        // 找到匹配的 ] 结束
        int depth = 1; // 当前已经是 [
        int start = idx;
        for (int i = idx + 1; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
        }
        return "[]";
    }

    private struct ToolCallInfo
    {
        public string id;
        public string name;
        public string arguments;
    }

    private List<ToolCallInfo> ParseToolCalls(string callsJson)
    {
        var list = new List<ToolCallInfo>();

        // 简易解析: 依次找 id, function.name, function.arguments
        int pos = 0;
        while (true)
        {
            // 找下一个 "id":"...  （在同一 tool_call 对象内）
            int idIdx = callsJson.IndexOf("\"id\":\"", pos);
            if (idIdx < 0) break;

            string id = ExtractSimpleString(callsJson, idIdx + 6);

            int nameIdx = callsJson.IndexOf("\"name\":\"", idIdx);
            string name = nameIdx >= 0 ? ExtractSimpleString(callsJson, nameIdx + 8) : "";

            int argIdx = callsJson.IndexOf("\"arguments\":", idIdx);
            string args = "";
            if (argIdx >= 0)
            {
                argIdx += 12; // skip "\"arguments\":" 
                // arguments 可能是一个 JSON 对象字符串: "\"{...}\"" 或 JSON 对象 {...}
                if (argIdx < callsJson.Length && callsJson[argIdx] == '"')
                {
                    // 字符串: 提取并转义还原
                    args = ExtractSimpleString(callsJson, argIdx + 1);
                    args = args.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
                }
                else
                {
                    // JSON 对象: 提取 {...} 块
                    int objStart = argIdx;
                    if (callsJson[objStart] == '{')
                    {
                        int d = 1;
                        for (int i = objStart + 1; i < callsJson.Length; i++)
                        {
                            if (callsJson[i] == '{') d++;
                            else if (callsJson[i] == '}') { d--; if (d == 0) { args = callsJson.Substring(objStart, i - objStart + 1); break; } }
                        }
                    }
                }
            }

            list.Add(new ToolCallInfo { id = id, name = name, arguments = args });
            pos = idIdx + 1;
        }

        return list;
    }

    /// <summary>从 JSON 中提取 "key":"value" 中 value 部分的纯字符串</summary>
    private static string ExtractSimpleString(string json, int start)
    {
        if (start >= json.Length) return "";
        var sb = new StringBuilder();
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                char n = json[i + 1];
                if (n == '"') { sb.Append('"'); i++; }
                else if (n == '\\') { sb.Append('\\'); i++; }
                else if (n == 'n') { sb.Append('\n'); i++; }
                else if (n == 't') { sb.Append('\t'); i++; }
                else if (n == 'r') { i++; }
                else sb.Append(json[i]);
            }
            else if (json[i] == '"')
            {
                break;
            }
            else
            {
                sb.Append(json[i]);
            }
        }
        return sb.ToString();
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

    // ==================================================================
    //  工具
    // ==================================================================

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
