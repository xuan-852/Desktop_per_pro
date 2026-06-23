using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 自动聊天 — 管理定时问候和互动事件
///
/// 职责：
/// 1. 定时场景问候（根据时间/星期自动显示气泡）
/// 2. 监听点击/拖拽事件 → 注入 AI 对话
/// 3. 监听 ChatManager 新回复 → 自动显示到气泡
/// </summary>
public class AutoChat : MonoBehaviour
{
    [Header("定时问候")]
    [Tooltip("首次问候延迟（秒）")]
    public float firstGreetingDelay = 3f;

    [Tooltip("问候冷却（秒）")]
    public float greetingCooldown = 180f; // 3 分钟

    [Tooltip("问候检查间隔（秒）")]
    public float greetingCheckInterval = 60f;

    [Header("互动事件")]
    [Tooltip("AI 互动事件冷却（秒）")]
    public float interactionCooldown = 20f; // 避免频繁触发

    [Header("气泡")]
    [Tooltip("AI 回复显示时长（秒）")]
    public float aiReplyDuration = 8f;

    [Tooltip("问候显示时长（秒）")]
    public float greetingDuration = 6f;

    private ChatManager _chat;
    private ChatBubble _bubble;
    private DragHandler _drag;
    private Live2DRenderer _renderer;
    private float _lastGreetingTime = -999f;
    private float _lastInteractionTime = -999f;

    // ===== 问候语库 =====
    private static readonly Dictionary<string, string[]> Greetings = new Dictionary<string, string[]>
    {
        ["清晨"] = new string[] {
            "早上好呀，今日祥瑞当头，宜出门走走~",
            "晨光正好，本座算了一卦，今日运势不错哦",
            "早啊，要不要本座帮你卜一卦？"
        },
        ["上午"] = new string[] {
            "上午好，工作可还顺利？",
            "看你这般忙碌，本座就不打扰了——不过有需要随时开口",
            "今日天象平和，是个做正事的好日子呢"
        },
        ["中午"] = new string[] {
            "中午了，该用膳啦~ 别饿着了",
            "日正当中，歇一歇再忙也不迟",
            "午时已到，本座也要去饮茶了"
        },
        ["下午"] = new string[] {
            "下午好~ 本座推算你该休息一下了",
            "日头西斜，事情做得如何了？",
            "下午容易犯困，本座陪你聊聊天可好？"
        },
        ["傍晚"] = new string[] {
            "天快黑了，今天辛苦啦",
            "傍晚时分，最适合散步放松了",
            "夕阳西下，本座观你气运渐旺，好事将近呢"
        },
        ["夜晚"] = new string[] {
            "晚上好，今天可有什么趣事？",
            "夜色正浓，本座帮你看看今晚的星象如何？",
            "忙了一天，该好好休息了"
        },
        ["深夜"] = new string[] {
            "夜深了，还不歇息吗？熬夜伤身哦",
            "子时已过，本座劝你早些休息",
            "这么晚了还在忙？真有你的风格……"
        },
        ["周末"] = new string[] {
            "周末啦，今天有什么安排？",
            "难得休息日，该出去走走了",
            "周末闲暇，要不要本座陪你说说话？"
        }
    };

    void Start()
    {
        _chat = GetComponent<ChatManager>();
        _bubble = GetComponent<ChatBubble>();
        if (_bubble == null) _bubble = gameObject.AddComponent<ChatBubble>();
        _renderer = GetComponent<Live2DRenderer>();

        // 监听拖拽事件
        _drag = GetComponent<DragHandler>();
        if (_drag != null)
        {
            _drag.OnPetClicked += HandleClick;
            _drag.OnDragEnded += HandleDrag;
        }

        // 监听 AI 新回复
        if (_chat != null)
        {
            _chat.OnNewReply += HandleNewReply;
            _chat.OnSentenceChanged += HandleSentenceChanged;
            _chat.OnRequestError += HandleRequestError;
        }

        // 首次问候
        Invoke("DoTimeGreeting", firstGreetingDelay);
        // 定时检查
        InvokeRepeating("CheckTimeGreeting", greetingCheckInterval, greetingCheckInterval);

        Debug.Log("[AutoChat] 已启动，首次问候延迟 " + firstGreetingDelay + "s");
    }

    void OnDestroy()
    {
        if (_drag != null)
        {
            _drag.OnPetClicked -= HandleClick;
            _drag.OnDragEnded -= HandleDrag;
        }
        if (_chat != null)
        {
            _chat.OnNewReply -= HandleNewReply;
            _chat.OnSentenceChanged -= HandleSentenceChanged;
            _chat.OnRequestError -= HandleRequestError;
        }
    }

    // ==================== 互动事件 ====================

    private void HandleClick()
    {
        if (_chat == null || _chat.IsWaiting) return;
        if (Time.time - _lastInteractionTime < interactionCooldown) return;
        // 高优消息显示时不打扰
        if (_bubble != null && _bubble.IsShowingHighPriority) return;

        _lastInteractionTime = Time.time;
        _bubble.ShowMessage("🌸 嗯？找本座何事呀~", 4f, ChatBubble.MsgPriority.Low);
        _chat.SendMessage("*你伸出手指，轻轻戳了戳符玄的额头*", null);
    }

    private void HandleDrag()
    {
        if (_chat == null || _chat.IsWaiting) return;
        if (Time.time - _lastInteractionTime < interactionCooldown) return;
        // 高优消息显示时不打扰
        if (_bubble != null && _bubble.IsShowingHighPriority) return;

        _lastInteractionTime = Time.time;
        _bubble.ShowMessage("🌸 哎呀，别摸头啦……", 4f, ChatBubble.MsgPriority.Low);
        _chat.SendMessage("*你温柔地抚摸了符玄的头发*", null);
    }

    // ==================== AI 回复监听 ====================

    private void HandleNewReply(string reply)
    {
        // 检测困惑 → 触发困惑动作
        if (_renderer != null && IsConfusedReply(reply))
        {
            _renderer.ForceAction("confuse");
        }

        // ⚠️ 不在这里显示气泡——有逐句切换时 OnSentenceChanged 会立刻接手
        // 如果 OnSentenceChanged 没有被触发（单句），由它自己处理
    }

    /// <summary>逐句切换时更新气泡内容，延长显示时间</summary>
    private void HandleSentenceChanged(string sentence, int idx, int total)
    {
        if (_bubble == null) return;
        if (string.IsNullOrEmpty(sentence)) return;

        // 🛡 SkipSentenceAnimation 触发的全文(idx >= total)跳过——
        // 气泡已显示最后一句，不需要用全文覆盖
        if (idx >= total) return;

        // 第一句：用高优先级启动气泡（不可被闲话覆盖）
        // 后续句子：仅更新文本，延长显示时间
        if (idx == 0)
        {
            _bubble.ShowMessage("🌸 " + sentence, _chat.sentenceInterval + 1f, ChatBubble.MsgPriority.High);
        }
        else
        {
            _bubble.UpdateText("🌸 " + sentence, _chat.sentenceInterval + 1f);
        }

        // 如果是最后一句，延长显示时间让用户读完
        if (idx == total - 1)
        {
            _bubble.ExtendDuration(aiReplyDuration);
        }
    }

    /// <summary>API 请求出错时显示错误信息到气泡</summary>
    private void HandleRequestError(string error)
    {
        if (_bubble == null) return;
        _bubble.ShowMessage("⚠️ " + error, 8f, ChatBubble.MsgPriority.High);
    }

    // ===== 困惑检测 =====

    /// <summary>AI 回复中出现这些词时，说明它没听懂，触发困惑动画</summary>
    private static readonly string[] ConfusionKeywords = new string[]
    {
        "不懂", "不明白", "没听懂", "没明白", "不理解",
        "听不懂", "搞不懂", "一头雾水", "摸不着头脑",
        "不知所云", "莫名其妙", "什么意思", "困惑",
        "没头没脑", "搞不清楚", "听不明白", "不知所谓"
    };

    private bool IsConfusedReply(string reply)
    {
        foreach (var kw in ConfusionKeywords)
        {
            if (reply.Contains(kw)) return true;
        }
        return false;
    }

    // ==================== 定时问候 ====================

    private void DoTimeGreeting()
    {
        if (_bubble == null) return;
        // 高优消息显示时不打扰
        if (_bubble.IsShowingHighPriority) return;
        string greeting = PickGreeting();
        _bubble.ShowMessage("🌸 " + greeting, greetingDuration, ChatBubble.MsgPriority.Low);
        _lastGreetingTime = Time.time;
    }

    private void CheckTimeGreeting()
    {
        // 冷却中或 AI 正在回复时不打扰
        if (_bubble == null) return;
        if (_chat != null && _chat.IsWaiting) return;
        if (_bubble.IsShowingHighPriority) return;
        if (Time.time - _lastGreetingTime < greetingCooldown) return;

        string greeting = PickGreeting();
        _bubble.ShowMessage("🌸 " + greeting, greetingDuration, ChatBubble.MsgPriority.Low);
        _lastGreetingTime = Time.time;
    }

    private string PickGreeting()
    {
        int hour = System.DateTime.Now.Hour;
        bool isWeekend = System.DateTime.Now.DayOfWeek == System.DayOfWeek.Saturday
                      || System.DateTime.Now.DayOfWeek == System.DayOfWeek.Sunday;

        string key;
        if (isWeekend && hour >= 8 && hour <= 22)
            key = "周末";
        else if (hour >= 5 && hour < 8)
            key = "清晨";
        else if (hour >= 8 && hour < 12)
            key = "上午";
        else if (hour >= 12 && hour < 14)
            key = "中午";
        else if (hour >= 14 && hour < 17)
            key = "下午";
        else if (hour >= 17 && hour < 19)
            key = "傍晚";
        else if (hour >= 19 && hour < 23)
            key = "夜晚";
        else
            key = "深夜";

        if (Greetings.TryGetValue(key, out string[] msgs) && msgs.Length > 0)
            return msgs[Random.Range(0, msgs.Length)];

        return "今天天气不错呢~";
    }
}
