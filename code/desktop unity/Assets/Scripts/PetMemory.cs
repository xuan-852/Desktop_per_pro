using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 符玄「忆境」— 长期记忆系统
/// 记录关键对话摘要到 pet_memory.json，每次请求时注入 system prompt
/// 让符玄真的「记得你」
/// </summary>
public class PetMemory : MonoBehaviour
{
    [Header("记忆配置")]
    [Tooltip("最多保留多少条记忆")]
    public int maxMemories = 20;

    [Tooltip("每次对话后记忆冷却（秒），防止同一话题重复记录")]
    public float memoryCooldown = 120f;

    // ==================================================================

    [System.Serializable]
    public class MemoryEntry
    {
        /// <summary>记忆摘要文本</summary>
        public string summary;
        /// <summary>记录时间 (yyyy-MM-dd HH:mm)</summary>
        public string timestamp;
        /// <summary>话题标签，用于去重</summary>
        public string topic;
    }

    [System.Serializable]
    public class MemoryData
    {
        public List<MemoryEntry> entries = new List<MemoryEntry>();
    }

    private MemoryData _data = new MemoryData();
    private float _lastMemoryTime = -999f;

    public static PetMemory Instance { get; private set; }

    private string FilePath => Path.Combine(Application.persistentDataPath, "pet_memory.json");

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ==================================================================
    //  公开接口
    // ==================================================================

    /// <summary>
    /// 添加一条记忆
    /// </summary>
    /// <param name="summary">记忆摘要</param>
    /// <param name="topic">话题标签（用于去重，如"天气""成绩""文件搜索"）</param>
    public void AddMemory(string summary, string topic = "")
    {
        // 冷却检查：同一话题不重复记录
        if (!string.IsNullOrEmpty(topic) && Time.time - _lastMemoryTime < memoryCooldown)
        {
            var last = _data.entries.LastOrDefault(e => e.topic == topic);
            if (last != null) return;
        }

        _data.entries.Add(new MemoryEntry
        {
            summary = summary,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            topic = topic
        });

        // 裁剪
        while (_data.entries.Count > maxMemories)
            _data.entries.RemoveAt(0);

        _lastMemoryTime = Time.time;
        Save();
    }

    /// <summary>获取格式化的记忆文本（注入到 system prompt 用）</summary>
    public string GetFormattedMemories()
    {
        if (_data.entries.Count == 0) return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\n【本座的记忆（忆境残象）】");
        sb.AppendLine("以下是你记得的关于主人的一些事（时间从旧到新）：");

        // 按时间排序（旧→新）
        var sorted = _data.entries.OrderBy(e => e.timestamp).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var e = sorted[i];
            sb.AppendLine($"{{entry_{i}}}. ({e.timestamp}) {e.summary}");
        }

        sb.AppendLine("（这些是忆境中残留的印象，可能不完全准确。如果主人提到相关的事，可以参考这些记忆。）");
        return sb.ToString();
    }

    /// <summary>清空所有记忆</summary>
    public void ClearMemories()
    {
        _data.entries.Clear();
        Save();
        Debug.Log("[PetMemory] 🧹 忆境已清空");
    }

    /// <summary>获取记忆条数</summary>
    public int MemoryCount => _data.entries.Count;

    // ==================================================================
    //  持久化
    // ==================================================================

    public void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(_data, prettyPrint: true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PetMemory] ❌ 保存失败: {e.Message}");
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Debug.Log("[PetMemory] 无已有记忆，从零开始");
                return;
            }
            string json = File.ReadAllText(FilePath);
            var loaded = JsonUtility.FromJson<MemoryData>(json);
            if (loaded != null)
            {
                _data = loaded;
                Debug.Log($"[PetMemory] ✅ 忆境已载入，共 {_data.entries.Count} 条记忆 ({FilePath})");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PetMemory] ❌ 载入失败: {e.Message}");
        }
    }
}
