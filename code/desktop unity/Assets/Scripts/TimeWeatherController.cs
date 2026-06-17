using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 时间与天气控制器 — 驱动宠物的昼夜/天气反应
///
/// 职责：
/// - 每帧检测系统时间
/// - 定期从 wttr.in 获取天气（可配置间隔）
/// - 公开状态供 Live2DRenderer/DesktopPet 查询
/// </summary>
public class TimeWeatherController : MonoBehaviour
{
    [Header("天气更新")]
    [Tooltip("天气轮询间隔（秒），0=不查询天气")]
    public float weatherUpdateInterval = 300f; // 5分钟

    [Tooltip("城市代码（用于 wttr.in），空=自动IP定位")]
    public string cityCode = "";

    [Header("调试")]
    [Tooltip("强制指定小时（-1=跟随系统）")]
    public int debugHourOverride = -1;

    // ===== 公开状态 =====
    [System.NonSerialized] public int hour;           // 当前小时 0~23
    [System.NonSerialized] public bool isNight;        // 18~6 夜间
    [System.NonSerialized] public float dayPhase;      // 0~1 (6点日出→18点日落)
    [System.NonSerialized] public bool isSleepyTime;   // 22~5 犯困时段

    public enum WeatherType
    {
        Unknown,
        Clear,      // ☀️ 晴
        Cloudy,     // ☁️ 多云
        Overcast,   // 🌥 阴
        Rain,       // 🌧 雨
        Drizzle,    // 🌦 小雨
        Thunder,    // ⛈ 雷雨
        Snow,       // ❄️ 雪
        Fog,        // 🌫 雾
    }

    [System.NonSerialized] public WeatherType weather = WeatherType.Unknown;
    [System.NonSerialized] public float temperatureC = 20f;   // 默认室温
    [System.NonSerialized] public bool weatherFetched = false; // 是否成功获取过

    private float _weatherTimer = 0f;
    private bool _isFetching = false;

    private void Start()
    {
        UpdateTime();
        // 首次启动立即获取一次天气
        if (weatherUpdateInterval > 0f)
        {
            _weatherTimer = weatherUpdateInterval; // 让第一帧立即触发
        }
    }

    private void Update()
    {
        // 每帧更新时间
        UpdateTime();

        // 定期获取天气
        if (weatherUpdateInterval > 0f)
        {
            _weatherTimer += Time.deltaTime;
            if (_weatherTimer >= weatherUpdateInterval && !_isFetching)
            {
                _weatherTimer = 0f;
                StartCoroutine(FetchWeather());
            }
        }
    }

    private void UpdateTime()
    {
        int rawHour;
        if (debugHourOverride >= 0 && debugHourOverride <= 23)
            rawHour = debugHourOverride;
        else
            rawHour = DateTime.Now.Hour;

        hour = rawHour;
        isNight = (hour < 6 || hour >= 18);
        isSleepyTime = (hour >= 22 || hour < 5);

        // dayPhase: 6点=0, 12点=0.5, 18点=1.0
        if (hour >= 6 && hour < 18)
        {
            dayPhase = (hour - 6) / 12f;
        }
        else if (hour < 6)
        {
            dayPhase = 0f; // 凌晨统一为0
        }
        else
        {
            dayPhase = 1f; // 夜晚统一为1
        }
    }

    private IEnumerator FetchWeather()
    {
        _isFetching = true;

        // 使用 wttr.in 的简洁格式：只返回天气代码和温度
        string url = string.IsNullOrEmpty(cityCode)
            ? "https://wttr.in/?format=%C+%t"
            : $"https://wttr.in/{cityCode}?format=%C+%t";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string raw = req.downloadHandler.text.Trim();
                Debug.Log($"[TimeWeather] 天气原始响应: {raw}");
                ParseWeather(raw);
                weatherFetched = true;
            }
            else
            {
                Debug.LogWarning($"[TimeWeather] 获取天气失败: {req.error}");
            }
        }

        _isFetching = false;
    }

    /// <summary>
    /// 解析 wttr.in 返回的天气字符串
    /// 格式如: "Clear +20°C" 或 "Light rain +15°C"
    /// </summary>
    private void ParseWeather(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return;

        // 解析温度
        try
        {
            int plusIdx = raw.IndexOf('+');
            int minusIdx = raw.IndexOf('-', 1); // 跳过开头的负号检测
            int degIdx = raw.IndexOf('°');
            if (degIdx > 0)
            {
                // 找温度起始位置
                int tempStart = -1;
                for (int i = degIdx - 1; i >= 0; i--)
                {
                    if (char.IsDigit(raw[i]) || raw[i] == '+' || raw[i] == '-')
                    {
                        if (raw[i] == '+' || raw[i] == '-')
                        {
                            tempStart = i;
                            break;
                        }
                    }
                    else
                    {
                        tempStart = i + 1;
                        break;
                    }
                }
                if (tempStart >= 0 && int.TryParse(raw.Substring(tempStart, degIdx - tempStart), out int temp))
                {
                    temperatureC = temp;
                }
            }
        }
        catch { }

        // 解析天气类型
        string lower = raw.ToLowerInvariant();
        if (lower.Contains("thunder") || lower.Contains(" storm"))
            weather = WeatherType.Thunder;
        else if (lower.Contains("snow") || lower.Contains("sleet") || lower.Contains("blizzard"))
            weather = WeatherType.Snow;
        else if (lower.Contains("rain") || lower.Contains("shower") || lower.Contains("drizzle"))
            weather = lower.Contains("light") || lower.Contains("drizzle") ? WeatherType.Drizzle : WeatherType.Rain;
        else if (lower.Contains("fog") || lower.Contains("mist") || lower.Contains("haze"))
            weather = WeatherType.Fog;
        else if (lower.Contains("overcast"))
            weather = WeatherType.Overcast;
        else if (lower.Contains("cloud") || lower.Contains("partly"))
            weather = WeatherType.Cloudy;
        else if (lower.Contains("clear") || lower.Contains("sunny"))
            weather = WeatherType.Clear;
        else
            weather = WeatherType.Unknown;
    }

    /// <summary>
    /// 获取天气的简短中文描述
    /// </summary>
    public string GetWeatherLabel()
    {
        switch (weather)
        {
            case WeatherType.Clear:    return "☀️ 晴";
            case WeatherType.Cloudy:   return "⛅ 多云";
            case WeatherType.Overcast: return "☁️ 阴";
            case WeatherType.Rain:     return "🌧 雨";
            case WeatherType.Drizzle:  return "🌦 小雨";
            case WeatherType.Thunder:  return "⛈ 雷雨";
            case WeatherType.Snow:     return "❄️ 雪";
            case WeatherType.Fog:      return "🌫 雾";
            default:                   return "🌡 " + temperatureC.ToString("F0") + "°C";
        }
    }
}
