using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 符玄「法阵」— 工具调用执行器
/// 将 DeepSeek Function Calling 映射到 Windows 系统操作
/// </summary>
public class ToolCallInvoker : MonoBehaviour
{
    [Header("安全设置")]
    [Tooltip("危险操作（关机/重启/执行命令）需要人确认")]
    public bool dangerousOpsNeedConfirm = true;

    [Header("当前执行状态")]
    public string lastToolResult = "";
    public string lastToolError = "";

    // ========== 工具注册表 ==========
    private Dictionary<string, Func<string, string>> _executors;

    void Awake()
    {
        RegisterAllTools();
        RegisterCoroutineTools();
    }

    // ================================================================
    //  工具注册
    // ================================================================

    private void RegisterAllTools()
    {
        _executors = new Dictionary<string, Func<string, string>>();

        // ——— 1. 观星窥天：打开网页 ———
        _executors["open_url"] = args =>
        {
            string url = JsonRead(args, "url");
            if (string.IsNullOrEmpty(url)) return "❌ 未提供网址，本座如何观星？";
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return $"✅ 已开天目，窥视「{url}」";
        };

        // ——— 2. 太卜敕令：启动应用/文件 ———
        _executors["open_app"] = args =>
        {
            string name = JsonRead(args, "name");
            if (string.IsNullOrEmpty(name)) return "❌ 未指明要启动什么";
            try
            {
                // 先直接试试
                Process.Start(new ProcessStartInfo(name) { UseShellExecute = true });
                return $"✅ 已遵法旨，召来「{name}」";
            }
            catch
            {
                // 直接启动失败 → 快速模糊搜索（禁用递归搜索以免卡死主线程！）
                try
                {
                    // 1) where 不加 /R → 只搜 PATH（超快，毫秒级）
                    string found = FastWhich(name);
                    if (found != null)
                    {
                        Process.Start(new ProcessStartInfo(found) { UseShellExecute = true });
                        return $"✅ 在 PATH 中寻得「{Path.GetFileName(found)}」，已召来";
                    }

                    // 2) 搜开始菜单快捷方式（本地文件夹，快）
                    string startMenu = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                    var lnk = FastFindLink(startMenu, name) ??
                              FastFindLink(
                                  Path.Combine(Environment.GetFolderPath(
                                      Environment.SpecialFolder.CommonStartMenu), "Programs"),
                                  name);
                    if (lnk != null)
                    {
                        Process.Start(new ProcessStartInfo(lnk) { UseShellExecute = true });
                        return $"✅ 在开始菜单寻得「{Path.GetFileNameWithoutExtension(lnk)}」，已召来";
                    }

                    return $"❌ 本座寻遍四海也未找到「{name}」……你要不要试试说全名？";
                }
                catch (Exception e2)
                {
                    return $"❌ 召不来…{e2.Message}";
                }
            }
        };

        // ——— 3. 穷观推演：浏览器搜索 ———
        _executors["search"] = args =>
        {
            string query = JsonRead(args, "query");
            if (string.IsNullOrEmpty(query)) return "❌ 未说要推演什么";
            string url = "https://www.bing.com/search?q=" + Uri.EscapeDataString(query);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return $"✅ 穷观阵已推演「{query}」，结果自现";
        };

        // ——— 4. 法眼摄形：截图保存到桌面 ———
        _executors["take_screenshot"] = args =>
        {
            string path = SaveScreenshot();
            if (path != null)
                return $"✅ 法眼已摄形！存于「{path}」";
            else
                return "❌ 摄形失败…";
        };

        // ——— 5. 气运探查：系统信息 ———
        _executors["get_system_info"] = args =>
        {
            return GetSystemInfo();
        };

        // ——— 6. 封印结界：锁屏 ———
        _executors["lock_screen"] = args =>
        {
            LockWorkStation();
            return "🔒 已布下封印结界，旁人不得窥探";
        };

        // ——— 7. 听诊天机：音量控制 ———
        _executors["set_volume"] = args =>
        {
            string levelStr = JsonRead(args, "level");
            if (int.TryParse(levelStr, out int level))
            {
                level = Mathf.Clamp(level, 0, 100);
                SetSystemVolume(level);
                return level == 0
                    ? "🔇 已禁声，天地俱寂"
                    : $"🔊 已将音量调至 {level}%";
            }
            return "❌ 音量须在 0~100 之间";
        };

        // ——— 8. 调音结界：静音切换 ———
        _executors["mute"] = args =>
        {
            string muteStr = JsonRead(args, "muted");
            bool mute = muteStr == "true";
            SetSystemVolume(mute ? 0 : 50);
            return mute ? "🔇 已禁声" : "🔊 已解禁";
        };

        // ——— 9. 摄魂取念：读剪贴板 ———
        _executors["get_clipboard"] = args =>
        {
            string text = GetClipboardText();
            return string.IsNullOrEmpty(text)
                ? "📋 剪贴板空空如也"
                : $"📋 剪贴板内容：\n{text}";
        };

        // ——— 10. 传音入密：写剪贴板 ———
        _executors["set_clipboard"] = args =>
        {
            string text = JsonRead(args, "text");
            if (string.IsNullOrEmpty(text)) return "❌ 未说要传什么";
            SetClipboardText(text);
            return $"✅ 已写入剪贴板：{text}";
        };

        // ——— 11. 洞天开门：打开文件夹 ———
        _executors["open_folder"] = args =>
        {
            string path = JsonRead(args, "path");
            if (string.IsNullOrEmpty(path))
                path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!Directory.Exists(path) && !File.Exists(path))
                return $"❌ 路径不存在：「{path}」";
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return $"✅ 洞天已开：「{path}」";
            }
            catch (Exception e)
            {
                return $"❌ 开门失败…{e.Message}";
            }
        };

        // ——— 12. 传讯符：桌面通知 ———
        _executors["notify"] = args =>
        {
            string title = JsonRead(args, "title");
            string message = JsonRead(args, "message");
            if (string.IsNullOrEmpty(title)) title = "符玄传讯";
            if (string.IsNullOrEmpty(message)) message = "本座有话要说。";
            ShowNotification(title, message);
            return "📨 传讯符已发出";
        };

        // ——— 13. 归寂令：关机/重启/睡眠（需要确认） ———
        _executors["power"] = args =>
        {
            string action = JsonRead(args, "action"); // shutdown / restart / sleep
            if (dangerousOpsNeedConfirm)
                return "⚠️ 此等大事须卜者确认，请让用户在聊天中明确答复「确认关机/重启/睡眠」";
            switch (action)
            {
                case "shutdown":
                    Process.Start("shutdown", "/s /t 5");
                    return "🕯️ 归寂令已下，5 秒后仙舟停转";
                case "restart":
                    Process.Start("shutdown", "/r /t 5");
                    return "🔄 重启法阵已启，5 秒后重开天地";
                case "sleep":
                    Application.Quit();
                    return "💤 本座要歇息了……";
                default:
                    return "❌ 不识此令（可用: shutdown/restart/sleep）";
            }
        };

        // ——— 14. 法旨行令：执行命令 ———
        _executors["run_command"] = args =>
        {
            string cmd = JsonRead(args, "command");
            if (string.IsNullOrEmpty(cmd)) return "❌ 未降法旨";
            if (dangerousOpsNeedConfirm)
                return "⚠️ 行令须谨慎，请在聊天中明确说出要执行的命令";
            try
            {
                var psi = new ProcessStartInfo("cmd", "/c " + cmd)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                var p = Process.Start(psi);
                // 非阻塞等 3 秒，超时就 Kill（防止卡死 Unity 主线程）
                if (p != null && !p.WaitForExit(3000))
                {
                    try { p.Kill(); } catch { }
                    return "⏱️ 行令超时（>3s），法旨已被截断";
                }
                string output = p?.StandardOutput?.ReadToEnd() ?? "";
                string err = p?.StandardError?.ReadToEnd() ?? "";
                string result = output;
                if (!string.IsNullOrEmpty(err)) result += "\n[error]\n" + err;
                return string.IsNullOrEmpty(result)
                    ? "✅ 令行禁止，无回响"
                    : $"📜 回响：\n{result.Truncate(500)}";
            }
            catch (Exception e)
            {
                return $"❌ 行令有违…{e.Message}";
            }
        };

        // ——— 15. 本座方位：鼠标位置 ———
        _executors["get_mouse_pos"] = args =>
        {
            GetCursorPos(out POINT p);
            return $"🖱️ 鼠标位于 ({p.X}, {p.Y})";
        };

        // ——— 16. 探查目录：列文件 ———
        _executors["list_files"] = args =>
        {
            string dir = JsonRead(args, "path");
            if (string.IsNullOrEmpty(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!Directory.Exists(dir))
                return $"❌ 「{dir}」不存在";
            var files = Directory.GetFiles(dir);
            var dirs = Directory.GetDirectories(dir);
            var sb = new StringBuilder();
            sb.AppendLine($"📁 {dir} 中共 {dirs.Length} 目录 / {files.Length} 文件");
            foreach (var d in dirs) sb.AppendLine($"  📂 {Path.GetFileName(d)}/");
            foreach (var f in files) sb.AppendLine($"  📄 {Path.GetFileName(f)}");
            return sb.ToString().Truncate(800);
        };

        // ——— 17. 搜天寻地：搜索文件 (优先用 Everything，毫秒级) ———
        _executors["search_files"] = args =>
        {
            string query = JsonRead(args, "query");
            string rootDir = JsonRead(args, "root");
            if (string.IsNullOrEmpty(query)) return "❌ 未说要搜什么";

            string esExe = FindEverythingCli();
            bool useEverything = esExe != null;

            try
            {
                var task = Task.Run(() =>
                {
                    var results = new List<string>();

                    if (useEverything)
                    {
                        // ——— 用 Everything (es.exe) 搜索，毫秒级 ———
                        try
                        {
                            string esArgs = $"-n 200 \"{query.Replace("\"", "\\\"")}\"";
                            if (!string.IsNullOrEmpty(rootDir))
                                esArgs = $"-n 200 -path \"{rootDir.Replace("\"", "\\\"")}\" \"{query.Replace("\"", "\\\"")}\"";

                            var psi = new ProcessStartInfo(esExe, esArgs)
                            {
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true,
                                StandardOutputEncoding = Encoding.UTF8,
                                StandardErrorEncoding = Encoding.UTF8
                            };
                            var p = Process.Start(psi);
                            if (p != null)
                            {
                                string output = p.StandardOutput.ReadToEnd();
                                p.WaitForExit(3000);
                                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var line in lines)
                                {
                                    if (!string.IsNullOrWhiteSpace(line))
                                        results.Add(line);
                                }
                            }
                        }
                        catch { useEverything = false; } // 失败了回退到递归
                    }

                    if (!useEverything)
                    {
                        // ——— 回退：递归搜索 ———
                        try
                        {
                            if (string.IsNullOrEmpty(rootDir))
                                rootDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                            if (Directory.Exists(rootDir))
                                SearchRecursive(rootDir, query, results, 200);
                        }
                        catch (Exception ex)
                        {
                            results.Add($"⚠️ 搜索中断：{ex.Message}");
                        }
                    }

                    return results;
                });

                // 等最多 10 秒
                if (task.Wait(TimeSpan.FromSeconds(10)))
                {
                    var list = task.Result;
                    if (list.Count == 0)
                    {
                        string scope = useEverything
                            ? (string.IsNullOrEmpty(rootDir) ? "本座的天眼所及之处" : $"「{rootDir}」")
                            : (string.IsNullOrEmpty(rootDir) ? "桌面" : $"「{rootDir}」");
                        return $"🔍 在{scope}中未找到与「{query}」匹配的文件";
                    }
                    string method = useEverything ? "⚡本座以 Everything 天眼通搜" : "🔍本座以递归之法搜";
                    string scope2 = string.IsNullOrEmpty(rootDir) ? "全境" : $"「{rootDir}」";
                    var sb = new StringBuilder();
                    sb.AppendLine($"{method}{scope2}，得 {list.Count} 件与「{query}」相关之物：");
                    foreach (var f in list)
                        sb.AppendLine($"  📄 {f}");
                    return sb.ToString().Truncate(2000);
                }
                else
                {
                    return $"⏱️ 搜索「{query}」耗时过长（>10s），请缩小搜索范围或指定更具体的目录";
                }
            }
            catch (Exception e)
            {
                return $"❌ 搜索时出了岔子：{e.Message}";
            }
        };

        // ——— 18. 卜算记事：设置提醒 ———
        _executors["set_reminder"] = args =>
        {
            string text = JsonRead(args, "text");
            string timeStr = JsonRead(args, "remind_at");
            if (string.IsNullOrEmpty(text)) return "❌ 未说要提醒何事";

            DateTime remindAt;
            if (!string.IsNullOrEmpty(timeStr))
            {
                if (!DateTime.TryParse(timeStr, out remindAt))
                    return "❌ 时辰格式不对，本座只识 yyyy-MM-dd HH:mm 之流";
            }
            else
            {
                remindAt = DateTime.Now.AddHours(1); // 默认 1 小时后
            }

            if (remindAt <= DateTime.Now)
            {
                // 可能是年份算错了，自动逐年修正到未来
                DateTime fixedTime = remindAt;
                for (int i = 0; i < 5 && fixedTime <= DateTime.Now; i++)
                    fixedTime = fixedTime.AddYears(1);
                if (fixedTime > DateTime.Now)
                {
                    remindAt = fixedTime;
                }
                else
                {
                    // 实在修不好，才拒绝
                    return "❌ 定在过去可不行，本座不会时光倒流之术";
                }
            }

            string recurring = JsonRead(args, "recurring");
            string priority = JsonRead(args, "priority");
            if (string.IsNullOrEmpty(priority)) priority = "normal";

            var mgr = ReminderManager.Instance;
            if (mgr == null) return "❌ 卜算记事簿未就绪";

            var r = mgr.AddReminder(text, remindAt,
                string.IsNullOrEmpty(recurring) ? null : recurring,
                priority, "ai");
            return $"✅ 已记入卜算记事簿！提醒「{text}」定于 {remindAt:yyyy-MM-dd HH:mm}" +
                   $"\n📌 ID: {r.id.Substring(0, 8)}… 可对我说「查提醒」查阅";
        };

        // ——— 18. 查阅记事：查询提醒 ———
        _executors["query_reminders"] = args =>
        {
            var mgr = ReminderManager.Instance;
            if (mgr == null) return "❌ 卜算记事簿未就绪";
            return mgr.GetPendingText();
        };

        // ——— 19. 勾销事宜：标记完成 ———
        _executors["mark_reminder_done"] = args =>
        {
            string id = JsonRead(args, "id");
            if (string.IsNullOrEmpty(id)) return "❌ 未指定要勾销哪条提醒的 ID";

            var mgr = ReminderManager.Instance;
            if (mgr == null) return "❌ 卜算记事簿未就绪";

            // 支持用前几位匹配（用户不一定记得完整 ID）
            var all = mgr.GetAllReminders();
            var match = all.Find(r => r.id.StartsWith(id) && !r.done);
            if (match == null) return $"❌ 未找到 ID 以「{id}」开头的待办事项";

            mgr.MarkDone(match.id);
            return $"✅ 已勾销「{match.text}」";
        };

        // ——— 20. 销毁记事：删除提醒 ———
        _executors["delete_reminder"] = args =>
        {
            string id = JsonRead(args, "id");
            if (string.IsNullOrEmpty(id)) return "❌ 未指定要删除哪条提醒的 ID";

            var mgr = ReminderManager.Instance;
            if (mgr == null) return "❌ 卜算记事簿未就绪";

            var all = mgr.GetAllReminders();
            var match = all.Find(r => r.id.StartsWith(id));
            if (match == null) return $"❌ 未找到 ID 以「{id}」开头的事项";

            mgr.DeleteReminder(match.id);
            return $"✅ 已销毁记事「{match.text}」";
        };

        // ——— 21. 查询考试：从课表服务获取考试安排 ———
        _executors["query_exams"] = args =>
        {
            var poll = FindObjectOfType<ServerPollService>();
            if (poll == null) return "❌ 课表传讯服务未就绪";
            try
            {
                var task = Task.Run(() => poll.QueryUpcomingExamsAsync());
                return task.GetAwaiter().GetResult();
            }
            catch (System.Exception e)
            {
                return $"❌ 查询考试时出错: {e.Message}";
            }
        };

        // ——— 22. 查看成绩：查询学业成绩 ———
        _executors["query_scores"] = args =>
        {
            var poll = FindObjectOfType<ServerPollService>();
            if (poll == null) return "❌ 课表传讯服务未就绪";
            try
            {
                var task = Task.Run(() => poll.QueryScoresAsync());
                return task.GetAwaiter().GetResult();
            }
            catch (System.Exception e)
            {
                return $"❌ 查询成绩时出错: {e.Message}";
            }
        };

        // ——— 23. 查看课表：查询课程安排 ———
        _executors["query_schedule"] = args =>
        {
            var poll = FindObjectOfType<ServerPollService>();
            if (poll == null) return "❌ 课表传讯服务未就绪";
            int week = 0;
            int.TryParse(JsonRead(args, "week"), out week);
            try
            {
                var task = Task.Run(() => poll.QueryScheduleAsync(week));
                return task.GetAwaiter().GetResult();
            }
            catch (System.Exception e)
            {
                return $"❌ 查询课表时出错: {e.Message}";
            }
        };

        // ——— 24. 卜算学业：查看用户绑定状态和学业概览 ———
        _executors["query_user_status"] = args =>
        {
            var poll = FindObjectOfType<ServerPollService>();
            if (poll == null) return "❌ 课表传讯服务未就绪";
            try
            {
                var task = Task.Run(() => poll.QueryUserStatusAsync());
                return task.GetAwaiter().GetResult();
            }
            catch (System.Exception e)
            {
                return $"❌ 查询学业信息时出错: {e.Message}";
            }
        };

        // ——— 25. 变脸术：切换桌面宠物的面部表情 ———
        _executors["set_expression"] = args =>
        {
            string exp = JsonRead(args, "expression");
            if (string.IsNullOrEmpty(exp)) return "❌ 未指定表情";
            var renderer = FindObjectOfType<Live2DRenderer>();
            if (renderer == null) return "❌ 本座法身未现";
            renderer.PlayExpression(exp);
            return $"✅ 已切换表情为「{exp}」";
        };

        // ——— 26. 演武：播放一段复合动作 ———
        _executors["play_action"] = args =>
        {
            string action = JsonRead(args, "action");
            if (string.IsNullOrEmpty(action)) return "❌ 未指定动作";
            var renderer = FindObjectOfType<Live2DRenderer>();
            if (renderer == null) return "❌ 本座法身未现";
            renderer.PlayAction(action);
            return $"✅ 正在演武「{action}」";
        };

        // ——— 27. 归元：停止所有动作/表情 ———
        _executors["stop_action"] = args =>
        {
            var renderer = FindObjectOfType<Live2DRenderer>();
            if (renderer == null) return "❌ 本座法身未现";
            renderer.ActionController?.StopAllWithFade();
            return "✅ 已归元，恢复常态";
        };

        // ——— 28. 观云望气：直接读取本地的天气数据（不开网页） ———
        _executors["get_weather"] = args =>
        {
            var tc = FindObjectOfType<TimeWeatherController>();
            if (tc == null) return "❌ 观星法阵未启";
            if (!tc.weatherFetched) return "⏳ 尚未观测天象，再稍等片刻";

            string wtName = tc.weather switch
            {
                TimeWeatherController.WeatherType.Clear    => "☀️ 晴",
                TimeWeatherController.WeatherType.Cloudy   => "☁️ 多云",
                TimeWeatherController.WeatherType.Overcast => "🌥 阴",
                TimeWeatherController.WeatherType.Rain     => "🌧 雨",
                TimeWeatherController.WeatherType.Drizzle  => "🌦 小雨",
                TimeWeatherController.WeatherType.Thunder  => "⛈ 雷雨",
                TimeWeatherController.WeatherType.Snow     => "❄️ 雪",
                TimeWeatherController.WeatherType.Fog      => "🌫 雾",
                _ => "未知"
            };
            string source = tc.weatherSource == TimeWeatherController.WeatherSource.QWeather ? "和风天机" : "wttr.in天眼";
            return $"🌤️ 本座以{source}观天之象：\n• 天气：{wtName}\n• 气温：{tc.temperatureC:F0}°C";
        };
    }

    // ================================================================
    //  对外接口
    // ================================================================

    /// <summary>获取工具的 JSON 定义</summary>
    public string GetToolsJson()
    {
        // language=json
        return @"[
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""open_url"",
      ""description"": ""在默认浏览器中打开指定网址。比如用户说「打开B站」就调用此术。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""url"": { ""type"": ""string"", ""description"": ""要打开的完整网址"" }
        },
        ""required"": [""url""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""open_app"",
      ""description"": ""启动一个应用程序或打开文件。比如「打开计算器」「打开记事本」「帮我打开D盘下的某某文件」"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""name"": { ""type"": ""string"", ""description"": ""应用名称或文件路径，如 calc、notepad、D:\path\file.txt"" }
        },
        ""required"": [""name""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""search"",
      ""description"": ""在 Bing 搜索引擎上查询信息并打开浏览器显示结果。用户说「帮我搜一下xxx」时调用。禁止用于查询天气（天气请用 get_weather）"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""query"": { ""type"": ""string"", ""description"": ""搜索关键词"" }
        },
        ""required"": [""query""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""take_screenshot"",
      ""description"": ""截取当前屏幕并保存到桌面。用户说「截图」「看一下我屏幕」时调用。调用后告诉用户截图已保存"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""get_system_info"",
      ""description"": ""获取电脑的系统信息，包括操作系统、CPU、内存、磁盘、运行时间等。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""lock_screen"",
      ""description"": ""锁定电脑屏幕。用户说「锁屏」「离开一下」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""set_volume"",
      ""description"": ""调节系统音量。用户说「声音大一点」「音量调到50」「静音」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""level"": { ""type"": ""integer"", ""description"": ""音量值 0～100"" }
        },
        ""required"": [""level""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""mute"",
      ""description"": ""切换静音状态。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""muted"": { ""type"": ""boolean"", ""description"": ""true=静音 false=取消静音"" }
        },
        ""required"": [""muted""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""get_clipboard"",
      ""description"": ""读取剪贴板文本内容。用户说「看剪贴板」「复制了什么」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""set_clipboard"",
      ""description"": ""将文本写入剪贴板。用户说「帮我复制这段」「记下来」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""text"": { ""type"": ""string"", ""description"": ""要写入的文本"" }
        },
        ""required"": [""text""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""open_folder"",
      ""description"": ""在文件资源管理器中打开指定文件夹。用户说「打开D盘」「打开桌面」「打开下载文件夹」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""path"": { ""type"": ""string"", ""description"": ""文件夹路径，如果为空则打开桌面"" }
        }
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""notify"",
      ""description"": ""发送 Windows 桌面通知。当你有什么事情想主动告诉用户但用户可能在忙时，用此术弹窗。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""title"": { ""type"": ""string"", ""description"": ""通知标题"" },
          ""message"": { ""type"": ""string"", ""description"": ""通知正文"" }
        },
        ""required"": [""message""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""power"",
      ""description"": ""关机 / 重启 / 睡眠。用户明确说「关机」「重启电脑」时调用。需要用户确认后才能执行。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""action"": { ""type"": ""string"", ""enum"": [""shutdown"", ""restart"", ""sleep""], ""description"": ""shutdown=关机 restart=重启 sleep=睡眠"" }
        },
        ""required"": [""action""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""run_command"",
      ""description"": ""执行一条 CMD 命令。高级用法，只有用户明确要求执行特定命令时才使用。需要用户确认。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""command"": { ""type"": ""string"", ""description"": ""要执行的命令"" }
        },
        ""required"": [""command""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""list_files"",
      ""description"": ""列出指定目录下的文件和子目录。用户说「看看桌面上有什么」「D盘有什么」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""path"": { ""type"": ""string"", ""description"": ""目录路径，为空则桌面"" }
        }
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""search_files"",
      ""description"": ""【搜索文件】按关键词搜索文件，若电脑有 Everything 则毫秒级搜索全盘，否则递归搜索指定目录。用户说「帮我找找xxx」「搜一下电脑里的xxx」「找文件」时调用。未指定 root 时默认搜索全盘（Everything模式）或桌面（递归回退模式）。最多返回200个结果。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""query"": { ""type"": ""string"", ""description"": ""要搜索的文件名关键词（不区分大小写）"" },
          ""root"": { ""type"": ""string"", ""description"": ""搜索根目录（有 Everything 时自动限定在此目录下搜索），为空则全盘搜索"" }
        },
        ""required"": [""query""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""set_reminder"",
      ""description"": ""设置一个定时提醒/便签。在本座的卜算记事簿中记下一件事，到时辰会提醒你。支持每日/工作日/每周重复。用户说「提醒我xxx」「记一下xxx」「设个闹钟」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""text"": { ""type"": ""string"", ""description"": ""提醒内容，如「下午3点开会」「买牛奶」"" },
          ""remind_at"": { ""type"": ""string"", ""description"": ""提醒时间，ISO 格式如 2025-01-15 14:30，不提供则默认1小时后"" },
          ""recurring"": { ""type"": ""string"", ""enum"": [""daily"", ""weekday"", ""weekly""], ""description"": ""重复类型：daily=每天 weekday=工作日 weekly=每周"" },
          ""priority"": { ""type"": ""string"", ""enum"": [""low"", ""normal"", ""high""], ""description"": ""优先级"" }
        },
        ""required"": [""text""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""query_reminders"",
      ""description"": ""查询所有待办提醒/便签。用户说「看看我的提醒」「有什么待办」「查便签」时调用，也可在用户提到出去玩/做安排等时主动调用来检查是否有冲突的待办事项。返回未完成提醒列表。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""mark_reminder_done"",
      ""description"": ""将一条提醒标记为已完成。用户说「完成了」「搞定了」「勾掉」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""id"": { ""type"": ""string"", ""description"": ""提醒的 ID（支持前几位模糊匹配）"" }
        },
        ""required"": [""id""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""delete_reminder"",
      ""description"": ""删除一条提醒。用户说「删除提醒」「不要这个了」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""id"": { ""type"": ""string"", ""description"": ""提醒的 ID（支持前几位模糊匹配）"" }
        },
        ""required"": [""id""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""query_exams"",
      ""description"": ""查询教务系统中的考试安排。用户问「什么时候考试」「考试时间」「考试安排」时调用，也可在用户提到出去玩/约了人/做安排等时主动调用来检查是否和考试冲突。返回每门考试的日期、时间、地点。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""query_scores"",
      ""description"": ""查询学业成绩。用户问「我考了多少分」「成绩怎么样」「出分了吗」时调用。返回所有学期的课程成绩和学分。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""query_schedule"",
      ""description"": ""查询课表。用户问「今天有什么课」「课表」「下周上什么课」时调用。可以指定周次。不指定周次时返回全部课表。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""week"": { ""type"": ""integer"", ""description"": ""要查询的周次，如 1, 2, 3… 不传则查看全部课表"" }
        }
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""query_user_status"",
      ""description"": ""查询用户的教务账号绑定状态和学业概览。用户问「你都知道我什么」「我的学业数据」「我的账号信息」时调用。返回学号、学期、成绩/考试/课表数量等概览数据。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""set_expression"",
      ""description"": ""切换桌面宠物的面部表情。支持：happy（开心）、sad（悲伤）、blush（羞涩）、confused（困惑）、love（爱意）、angry（生气）、sleepy（困倦）、surprise（惊讶）、tear（泪目）。使用时传入中文或英文均可。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""expression"": { ""type"": ""string"", ""description"": ""表情名称，支持 happy/sad/blush/confused/love/angry/sleepy/surprise/tear 或其中文译名"" }
        },
        ""required"": [""expression""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""play_action"",
      ""description"": ""播放一段复合动作动画。支持：stretch（伸懒腰）、cry（捂脸哭）、confuse（歪头困惑）、heart_eyes（比心）、money（数钱）、blush（捧脸羞）、magic_circle（绘制法阵）。使用时传入中文或英文均可。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {
          ""action"": { ""type"": ""string"", ""description"": ""动作名称，支持 stretch/cry/confuse/heart_eyes/money/blush/magic_circle 或其中文名"" }
        },
        ""required"": [""action""]
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""stop_action"",
      ""description"": ""停止当前正在播放的所有动作和表情，使桌面宠物恢复默认待机姿态。用户说「停下」「别动了」「恢复」时调用。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  },
  {
    ""type"": ""function"",
    ""function"": {
      ""name"": ""get_weather"",
      ""description"": ""【天气专用】直接读取桌面本地已经获取到的天气数据（使用和风天气或 wttr.in），无需打开浏览器或搜索。用户问任何关于天气/温度/冷热的问题时，必须优先使用此术式，绝对不能用 search 去搜索天气。"",
      ""parameters"": {
        ""type"": ""object"",
        ""properties"": {}
      }
    }
  }
]";
    }

    // ================================================================
    //  协程工具执行器（非阻塞，用于网络/IO 类工具）
    // ================================================================

    /// <summary>协程工具注册表</summary>
    private Dictionary<string, Func<string, IEnumerator>> _coroutineExecutors;

    /// <summary>当前协程工具的执行结果</summary>
    private string _coroutineResult;

    private void RegisterCoroutineTools()
    {
        _coroutineExecutors = new Dictionary<string, Func<string, IEnumerator>>
        {
            ["query_exams"]       = args => RunAsyncTool(() => FindObjectOfType<ServerPollService>()?.QueryUpcomingExamsAsync() ?? Task.FromResult("❌ 课表传讯服务未就绪")),
            ["query_scores"]      = args => RunAsyncTool(() => FindObjectOfType<ServerPollService>()?.QueryScoresAsync() ?? Task.FromResult("❌ 课表传讯服务未就绪")),
            ["query_schedule"]    = args => RunAsyncTool(() =>
            {
                int week = 0;
                int.TryParse(JsonRead(args, "week"), out week);
                return FindObjectOfType<ServerPollService>()?.QueryScheduleAsync(week) ?? Task.FromResult("❌ 课表传讯服务未就绪");
            }),
            ["query_user_status"] = args => RunAsyncTool(() => FindObjectOfType<ServerPollService>()?.QueryUserStatusAsync() ?? Task.FromResult("❌ 课表传讯服务未就绪")),
            ["search_files"]      = args => RunAsyncTool(() => SearchFilesTask(args)),
        };
    }

    /// <summary>判断工具是否应走协程执行</summary>
    public bool IsCoroutineTool(string name) => _coroutineExecutors?.ContainsKey(name) ?? false;

    /// <summary>协程方式执行工具，不阻塞主线程</summary>
    public IEnumerator ExecuteCoroutine(string name, string argsJson)
    {
        _coroutineResult = null;
        if (_coroutineExecutors.TryGetValue(name, out var executor))
        {
            yield return executor(argsJson);
        }
        else
        {
            _coroutineResult = Execute(name, argsJson, out _);
        }
    }

    /// <summary>获取最近一次协程工具的执行结果</summary>
    public string GetCoroutineResult() => _coroutineResult;

    /// <summary>将 async Task 包装为非阻塞协程</summary>
    private IEnumerator RunAsyncTool(Func<Task<string>> taskFactory)
    {
        var task = taskFactory();
        // 每帧检查是否完成，不阻塞主线程
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            _coroutineResult = $"❌ 执行出错: {task.Exception?.InnerException?.Message ?? task.Exception?.Message}";
        }
        else
        {
            _coroutineResult = task.Result;
        }
    }

    /// <summary>search_files 的 async 版本（在后台线程运行）</summary>
    private Task<string> SearchFilesTask(string args)
    {
        return Task.Run(() =>
        {
            // 直接将原来同步版的逻辑搬过来
            string query = JsonRead(args, "query");
            string rootDir = JsonRead(args, "root");
            if (string.IsNullOrEmpty(query)) return "❌ 未说要搜什么";

            string esExe = FindEverythingCli();
            bool useEverything = esExe != null;

            try
            {
                var results = new List<string>();

                if (useEverything)
                {
                    try
                    {
                        string esArgs = $"-n 200 \"{query.Replace("\"", "\\\"")}\"";
                        if (!string.IsNullOrEmpty(rootDir))
                            esArgs = $"-n 200 -path \"{rootDir.Replace("\"", "\\\"")}\" \"{query.Replace("\"", "\\\"")}\"";

                        var psi = new ProcessStartInfo(esExe, esArgs)
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };
                        var p = Process.Start(psi);
                        if (p != null)
                        {
                            string output = p.StandardOutput.ReadToEnd();
                            p.WaitForExit(3000);
                            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                                if (!string.IsNullOrWhiteSpace(line)) results.Add(line);
                        }
                    }
                    catch { useEverything = false; }
                }

                if (!useEverything)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(rootDir))
                            rootDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        if (Directory.Exists(rootDir))
                            SearchRecursive(rootDir, query, results, 200);
                    }
                    catch (Exception ex)
                    {
                        results.Add($"⚠️ 搜索中断：{ex.Message}");
                    }
                }

                if (results.Count == 0)
                {
                    string scope = useEverything
                        ? (string.IsNullOrEmpty(rootDir) ? "本座的天眼所及之处" : $"「{rootDir}」")
                        : (string.IsNullOrEmpty(rootDir) ? "桌面" : $"「{rootDir}」");
                    return $"🔍 在{scope}中未找到与「{query}」匹配的文件";
                }

                string method = useEverything ? "⚡本座以 Everything 天眼通搜" : "🔍本座以递归之法搜";
                string scope2 = string.IsNullOrEmpty(rootDir) ? "全境" : $"「{rootDir}」";
                var sb = new StringBuilder();
                sb.AppendLine($"{method}{scope2}，得 {results.Count} 件与「{query}」相关之物：");
                foreach (var f in results)
                    sb.AppendLine($"  📄 {f}");
                return sb.ToString().Truncate(2000);
            }
            catch (Exception e)
            {
                return $"❌ 搜索时出了岔子：{e.Message}";
            }
        });
    }

    /// <summary>执行工具调用</summary>
    public string Execute(string name, string argsJson, out string error)
    {
        lastToolResult = "";
        lastToolError = "";

        if (_executors == null)
        {
            error = "法阵未就绪";
            return error;
        }

        if (!_executors.ContainsKey(name))
        {
            error = $"不识此术：「{name}」";
            return error;
        }

        try
        {
            string result = _executors[name](argsJson ?? "{}");
            lastToolResult = result;
            error = null;
            return result;
        }
        catch (Exception e)
        {
            lastToolError = e.Message;
            error = $"施法失败：{e.Message}";
            return error;
        }
    }

    // ================================================================
    //  内部实现
    // ================================================================

    // ---- JSON 简易解析（不用依赖库） ----

    private static string JsonRead(string json, string key)
    {
        string search = $"\"{key}\":\"";
        int idx = json.IndexOf(search);
        if (idx >= 0)
        {
            idx += search.Length;
            var sb = new StringBuilder();
            for (int i = idx; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    if (n == '"') { sb.Append('"'); i++; }
                    else if (n == '\\') { sb.Append('\\'); i++; }
                    else if (n == 'n') { sb.Append('\n'); i++; }
                    else sb.Append(json[i]);
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString().Trim();
        }

        // 尝试数字/布尔值（不带引号）
        search = $"\"{key}\":";
        idx = json.IndexOf(search);
        if (idx >= 0)
        {
            idx += search.Length;
            var sb = new StringBuilder();
            for (int i = idx; i < json.Length; i++)
            {
                char c = json[i];
                if (c == ',' || c == '}' || c == ']') break;
                sb.Append(c);
            }
            return sb.ToString().Trim().Trim('"');
        }

        return "";
    }

    // ---- 截图 ----

    private static string SaveScreenshot()
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filename = $"符玄法眼_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = Path.Combine(desktop, filename);

#if !UNITY_EDITOR
            // Windows 截图: 使用 SendKeys 模拟 PrtScn + 从剪贴板保存
            // 方式1：使用 .NET 的 Graphics.CopyFromScreen
            int w = Screen.width;
            int h = Screen.height;

            // 需要 System.Drawing.Common
            // 但我们在这里用另一种方法：启动外部截图工具或 PowerShell
            string psScript = $@"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($screen.Left, $screen.Top, 0, 0, $bmp.Size)
$bmp.Save('{path.Replace("'", "''")}', [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
";
            var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{psScript.Replace("\"", "\\\"")}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            var p = Process.Start(psi);
            // 非阻塞：等一小会儿就行，Unity 不会卡死
            if (p != null && !p.WaitForExit(2000))
            {
                try { p.Kill(); } catch { }
            }
            if (File.Exists(path)) return path;
#endif
            // Unity Editor 下或失败时创建占位
            return path;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[ToolCallInvoker] 截图失败: {e.Message}");
            return null;
        }
    }

    // ---- 系统信息 ----

    private static string GetSystemInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🖥️ 本座以第三眼洞观此间气运：");
        try { sb.AppendLine($"OS: {RuntimeInformation.OSDescription}"); } catch { }
        try { sb.AppendLine($"架构: {RuntimeInformation.ProcessArchitecture}"); } catch { }
        try
        {
            var mi = Environment.WorkingSet;
            float gb = mi / 1024f / 1024f / 1024f;
            if (gb > 1)
                sb.AppendLine($"本进程占灵 {gb:F1} GB ({mi:#,0} bytes)");
            else
                sb.AppendLine($"本进程占灵 {mi / 1024 / 1024} MB");
        }
        catch { }
        try { sb.AppendLine($"CPU 核心: {Environment.ProcessorCount}"); } catch { }
        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var d in drives)
            {
                if (d.IsReady)
                    sb.AppendLine($"磁盘 {d.Name}: {d.TotalSize / 1024 / 1024 / 1024} GB 总 / {(d.TotalSize - d.AvailableFreeSpace) / 1024 / 1024 / 1024} GB 用 / {d.AvailableFreeSpace / 1024 / 1024 / 1024} GB 余");
            }
        }
        catch { }
        try { long ms = Environment.TickCount; sb.AppendLine($"开机: {TimeSpan.FromMilliseconds(ms):d}天{TimeSpan.FromMilliseconds(ms):h}时"); } catch { }
        try { sb.AppendLine($"计算机名: {Environment.MachineName}"); } catch { }
        try { sb.AppendLine($"用户: {Environment.UserName}"); } catch { }
        try { sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}"); } catch { }
        return sb.ToString();
    }

    // ---- 剪贴板 ----

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll")]
    private static extern bool SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVABLE = 0x0002;

    private static string GetClipboardText()
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;
            IntPtr hMem = GetClipboardData(CF_UNICODETEXT);
            if (hMem == IntPtr.Zero) { CloseClipboard(); return null; }
            IntPtr ptr = GlobalLock(hMem);
            string text = Marshal.PtrToStringUni(ptr);
            GlobalUnlock(hMem);
            CloseClipboard();
            return text;
        }
        catch { return null; }
    }

    private static void SetClipboardText(string text)
    {
        try
        {
            OpenClipboard(IntPtr.Zero);
            EmptyClipboard();
            IntPtr hMem = GlobalAlloc(GMEM_MOVABLE, (UIntPtr)((text.Length + 1) * 2));
            IntPtr ptr = GlobalLock(hMem);
            for (int i = 0; i <= text.Length; i++)
                Marshal.WriteInt16(ptr, i * 2, (short)(i < text.Length ? text[i] : 0));
            GlobalUnlock(hMem);
            SetClipboardData(CF_UNICODETEXT, hMem);
            CloseClipboard();
        }
        catch { }
    }

    // ---- 音量 ----

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_APPCOMMAND = 0x0319;
    private const int APPCOMMAND_VOLUME_UP = 0x0a;
    private const int APPCOMMAND_VOLUME_DOWN = 0x0b;
    private const int APPCOMMAND_VOLUME_MUTE = 0x08;

    private static void SetSystemVolume(int level)
    {
        // 用 PowerShell 调节音量（最可靠）
        string psCmd = $"(New-Object -ComObject WScript.Shell).SendKeys([char]0); " +
                       $"while($true){{ $v=[Math]::Round((([Audio]::Volume)*100)); if($v -eq {level}){{break}} " +
                       $"if($v -gt {level}){{ (New-Object -ComObject WScript.Shell).SendKeys([char]174) }} " +
                       $"else{{ (New-Object -ComObject WScript.Shell).SendKeys([char]175) }} Start-Sleep -Milliseconds 50 }}";
        try
        {
            Process.Start(new ProcessStartInfo("powershell",
                $"-NoProfile -Command \"& {{$obj = New-Object -ComObject WScript.Shell; for($i=0;$i<100;$i++){{if([Math]::Round($obj.Volume*100) -le {level}){{break}};$obj.SendKeys([char]174);Start-Sleep -Milliseconds 20}};$obj = New-Object -ComObject WScript.Shell; for($i=0;$i<100;$i++){{if([Math]::Round($obj.Volume*100) -ge {level}){{break}};$obj.SendKeys([char]175);Start-Sleep -Milliseconds 20}}}}\"")
            { UseShellExecute = false, CreateNoWindow = true });
        }
        catch { }
    }

    // ---- 锁屏 ----

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    // ---- 鼠标位置 ----

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    // ---- 通知 ----

    private static void ShowNotification(string title, string message)
    {
        try
        {
            // 使用 PowerShell 的 Windows 通知
            string ps = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$textNodes = $template.GetElementsByTagName('text')
$textNodes.Item(0).AppendChild($template.CreateTextNode('{title.Replace("'", "''")}')) > $null
$textNodes.Item(1).AppendChild($template.CreateTextNode('{message.Replace("'", "''")}')) > $null
$toast = [Windows.UI.Notifications.ToastNotification]::new($template)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('符玄').Show($toast)
";
            Process.Start(new ProcessStartInfo("powershell",
                $"-NoProfile -Command \"{ps.Replace("\"", "\\\"")}\"")
            { UseShellExecute = false, CreateNoWindow = true });
        }
        catch { }
    }

    // ================================================================
    //  快速搜索辅助（禁止递归搜索！只搜 PATH 和开始菜单顶层）
    // ================================================================

    /// <summary>只在 PATH 环境变量中精确找 exe（毫秒级返回）</summary>
    private static string FastWhich(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        try
        {
            // where 不加 /R → 只搜 PATH，毫秒级
            var psi = new ProcessStartInfo("where", $"{name}.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            var p = Process.Start(psi);
            string line = p?.StandardOutput?.ReadLine();
            p?.WaitForExit(500);
            return string.IsNullOrEmpty(line) ? null : line;
        }
        catch { return null; }
    }

    /// <summary>寻找 Everything 命令行工具 es.exe</summary>
    private static string FindEverythingCli()
    {
        string[] candidates =
        {
            @"C:\Program Files\Everything\es.exe",
            @"C:\Program Files (x86)\Everything\es.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Everything\es.exe"),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        // 试 PATH
        try
        {
            var psi = new ProcessStartInfo("where", "es.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var p = Process.Start(psi);
            string firstLine = p?.StandardOutput?.ReadLine();
            p?.WaitForExit(500);
            if (!string.IsNullOrEmpty(firstLine) && File.Exists(firstLine))
                return firstLine;
        }
        catch { }
        return null;
    }

    /// <summary>递归搜索文件（名称匹配，不限层级）</summary>
    private static void SearchRecursive(string dir, string query, List<string> results, int maxResults)
    {
        if (results.Count >= maxResults) return;
        try
        {
            // 搜索当前目录的文件名
            foreach (var f in Directory.GetFiles(dir))
            {
                if (results.Count >= maxResults) return;
                try
                {
                    string name = Path.GetFileName(f);
                    if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        results.Add(f);
                }
                catch { }
            }

            // 递归子目录
            foreach (var d in Directory.GetDirectories(dir))
            {
                if (results.Count >= maxResults) return;
                // 跳过系统/隐藏目录和 junction 点
                try
                {
                    var attr = File.GetAttributes(d);
                    if ((attr & FileAttributes.ReparsePoint) != 0) continue; // 跳过符号链接/junction 防止死循环
                    // 也把匹配的目录名加入结果
                    string dirName = Path.GetFileName(d);
                    if (dirName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(d + "\\");
                        if (results.Count >= maxResults) return;
                    }
                    SearchRecursive(d, query, results, maxResults);
                }
                catch { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (PathTooLongException) { }
        catch { }
    }

    /// <summary>只在开始菜单 Programs 顶层找快捷方式（不递归）</summary>
    private static string FastFindLink(string rootDir, string keyword)
    {
        if (!Directory.Exists(rootDir)) return null;
        string kw = keyword.ToLowerInvariant();
        try
        {
            foreach (var f in Directory.GetFiles(rootDir, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (name.Contains(kw)) return f;
            }
        }
        catch { }
        return null;
    }
}

// ================================================================
//  扩展方法
// ================================================================

public static class StringExtensions
{
    public static string Truncate(this string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s;
        return s.Substring(0, maxLen) + "\n…(余略)";
    }
}
