using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace StudyLock
{
    /// <summary>发给本地模型的一条对话消息（OpenAI 兼容格式）</summary>
    public class AiChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        public AiChatMessage() { }

        public AiChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// 本地 AI 老师（Omni-Edu / llama.cpp）：
    /// 1. 需要时用 OmniEdu.exe --server 静默拉起本地推理服务（无界面、不弹窗）；
    /// 2. 用 OpenAI 兼容接口流式对话；
    /// 3. 用完后结束自己启动的进程树，释放显存。
    /// </summary>
    public static class AiTutor
    {
        public const string DefaultServerUrl = "http://127.0.0.1:8080";
        public const string DefaultLauncherPath = @"H:\Omni-Edu\OmniEdu.exe";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        // 健康检查单独用短超时、直连（不走系统代理）的客户端
        private static readonly HttpClient Health = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        private static readonly object Gate = new object();
        private static Process? _serverProc;
        private static bool _starting;

        /// <summary>服务是否已经就绪（/health 返回 200）</summary>
        public static bool ServerReady(string? baseUrl)
        {
            try
            {
                using var resp = Health.GetAsync(Normalize(baseUrl) + "/health").GetAwaiter().GetResult();
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// 确保本地服务可用。未就绪时启动 OmniEdu.exe --server 并等待 /health。
        /// 失败抛出带中文说明的异常（含 launcher 写入的错误日志内容）。
        /// </summary>
        public static async Task EnsureServerAsync(string? baseUrl, string? launcherPath,
            Action<string>? status, CancellationToken ct)
        {
            if (ServerReady(baseUrl)) return;

            string? exe = ResolveLauncherPath(launcherPath);
            if (exe == null)
                throw new InvalidOperationException(
                    "找不到本地 AI 程序：\n" + DefaultLauncherPath
                    + "\n\n请确认 Omni-Edu 文件夹的位置（可请家长修改 rules.json 里的 AiLauncherPath，"
                    + "或把 Omni-Edu 文件夹放在 H:\\Omni-Edu）。");

            status?.Invoke("正在启动本地 AI 老师……首次加载需要 1-3 分钟，请耐心等待");

            Process? proc;
            lock (Gate)
            {
                proc = _serverProc;
                if (proc != null && proc.HasExited) proc = _serverProc = null;
                if (proc == null)
                {
                    _starting = true;
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = exe,
                            Arguments = "--server",
                            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        proc = Process.Start(psi);
                        _serverProc = proc;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("无法启动本地 AI 程序：" + ex.Message);
                    }
                    finally { _starting = false; }
                }
            }

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromMinutes(6))
            {
                ct.ThrowIfCancellationRequested();
                if (ServerReady(baseUrl))
                {
                    status?.Invoke("AI 老师已就绪（本地模型，断网也能用）");
                    return;
                }

                lock (Gate) proc = _serverProc;
                if (!_starting && proc != null && proc.HasExited)
                {
                    string? detail = ReadLauncherError(exe);
                    throw new InvalidOperationException(
                        detail ?? "本地 AI 老师启动失败（推理进程已退出）。\n请把 logs 目录里的日志发给技术人员。");
                }

                await Task.Delay(1500, ct);
            }

            throw new TimeoutException(
                "本地 AI 老师启动超时（6 分钟）。\n可能是显存不足或模型加载太慢，请关闭游戏等占用显存的程序后重试。");
        }

        /// <summary>OpenAI 兼容的流式对话，逐段回调 delta 文本；模型进入"思考"阶段时回调 onThinking。</summary>
        public static async Task ChatStreamAsync(string? baseUrl, IList<AiChatMessage> messages,
            Action<string> onDelta, CancellationToken ct, Action? onThinking = null)
        {
            var body = new
            {
                model = "omni-edu",
                messages,
                stream = true,
                temperature = 0.6,
                top_p = 0.9,
                max_tokens = 2048
            };

            string json = JsonSerializer.Serialize(body);
            var uri = new Uri(Normalize(baseUrl) + "/v1/chat/completions");

            using var req = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                string text = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"AI 服务返回错误（{(int)resp.StatusCode}）：{Truncate(text, 200)}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            bool thinkingNotified = false;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                string? line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                line = line.Trim();
                if (line.Length == 0 || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;

                string data = line.Substring(5).Trim();
                if (data == "[DONE]") break;

                string? delta = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("choices", out var choices)
                        && choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("delta", out var d))
                        {
                            // 推理模型会先流式输出 reasoning_content（思考过程），不展示给孩子
                            if (!thinkingNotified
                                && d.TryGetProperty("reasoning_content", out var rc)
                                && rc.ValueKind == JsonValueKind.String
                                && (rc.GetString() ?? "").Length > 0)
                            {
                                thinkingNotified = true;
                                onThinking?.Invoke();
                            }
                            if (d.TryGetProperty("content", out var content)
                                && content.ValueKind == JsonValueKind.String)
                            {
                                delta = content.GetString();
                            }
                        }
                    }
                }
                catch { /* 忽略无法解析的心跳行 */ }

                if (!string.IsNullOrEmpty(delta)) onDelta(delta!);
            }
        }

        /// <summary>结束本程序启动的本地服务（只杀自己拉起的进程树，不影响用户手动开的 Omni-Edu）</summary>
        public static void StopServer()
        {
            Process? proc;
            lock (Gate)
            {
                proc = _serverProc;
                _serverProc = null;
            }
            if (proc == null) return;

            try
            {
                if (!proc.HasExited)
                {
                    var psi = new ProcessStartInfo("taskkill", "/PID " + proc.Id + " /T /F")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var kill = Process.Start(psi);
                    kill?.WaitForExit(8000);
                }
            }
            catch { }
        }

        /// <summary>
        /// 找可用的 Omni-Edu 启动器：优先用规则里的路径，其次 H:\Omni-Edu（默认位置），
        /// 最后找 EXE 旁边的 Omni-Edu 文件夹（把项目直接解压/克隆后使用的便携布局）。
        /// </summary>
        private static string? ResolveLauncherPath(string? configured)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured!.Trim());
            candidates.Add(DefaultLauncherPath);

            string baseDir = AppContext.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "Omni-Edu", "OmniEdu.exe"));
            try
            {
                candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "Omni-Edu", "OmniEdu.exe")));
                candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Omni-Edu", "OmniEdu.exe")));
            }
            catch { }

            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            return null;
        }

        private static string Normalize(string? baseUrl)
        {
            string u = string.IsNullOrWhiteSpace(baseUrl) ? DefaultServerUrl : baseUrl!.Trim();
            return u.TrimEnd('/');
        }

        private static string? ReadLauncherError(string launcherPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(launcherPath) ?? "";
                string path = Path.Combine(dir, "logs", "ai-server-error.txt");
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path, Encoding.UTF8).Trim();
                return text.Length > 0 ? text : null;
            }
            catch { return null; }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
