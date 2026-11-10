// Omni-Edu 桌面启动器（4B / 9B 合并版）
// 双击 EXE -> 自动检测 NVIDIA 显卡与显存
//          -> 显存 < 8GB：弹窗提示运行失败的原因和解决办法，然后退出
//          -> 8GB <= 显存 < 16GB：运行 4B 模型
//          -> 显存 >= 16GB：运行 9B 模型
//          -> 检查所选模型的两个文件（缺失自动从魔搭下载，显示进度/速度/剩余时间）
//          -> 后台启动 llama-server -> 欢迎窗口等待 -> 应用模式浏览器窗口 -> 关窗自动停止服务
// 编译: csc /nologo /target:winexe /codepage:65001 /out:OmniEdu.exe Launcher.cs /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal sealed class ModelFile
{
    public string Name;
    public string Repo;
    public long Size;
    public string Sha256;
    public string Url { get { return "https://modelscope.cn/models/prithivMLmods/" + Repo + "/resolve/master/" + Name; } }
    public ModelFile(string name, string repo, long size, string sha256)
    {
        Name = name; Repo = repo; Size = size; Sha256 = sha256;
    }
}

internal sealed class ModelProfile
{
    public string Key;
    public string Title;
    public string MainName;
    public string MmprojName;
    public ModelFile[] Files;
}

// 两个模型档位的定义（魔搭 prithivMLmods 仓库，Q6_K + mmproj）
internal static class Profiles
{
    public static readonly ModelProfile B4;
    public static readonly ModelProfile B9;

    static Profiles()
    {
        B4 = new ModelProfile();
        B4.Key = "4B";
        B4.Title = "Omni-Edu 教育助手（4B）";
        B4.MainName = "Omni-Edu-4B.Q6_K.gguf";
        B4.MmprojName = "Omni-Edu-4B.mmproj-bf16.gguf";
        B4.Files = new ModelFile[]
        {
            new ModelFile("Omni-Edu-4B.Q6_K.gguf",        "Omni-Edu-4B-GGUF", 3464055328L, "7464d8b7a8a52a746918c44978eef1a2e67d8e7019d66149d6964bb25dfc2760"),
            new ModelFile("Omni-Edu-4B.mmproj-bf16.gguf", "Omni-Edu-4B-GGUF", 675568896L,  "f945d130d459ac6d202a838b4b4965ddc5500b52068389122dda8840982b810e"),
        };

        B9 = new ModelProfile();
        B9.Key = "9B";
        B9.Title = "Omni-Edu 教育助手（9B）";
        B9.MainName = "Omni-Edu-9B.Q6_K.gguf";
        B9.MmprojName = "Omni-Edu-9B.mmproj-bf16.gguf";
        B9.Files = new ModelFile[]
        {
            new ModelFile("Omni-Edu-9B.Q6_K.gguf",        "Omni-Edu-9B-GGUF", 7359259456L, "63a8c0375d6bf2387995c4aeb6dbb44b9d828937c0819a3adcf2ee5f6096a3e4"),
            new ModelFile("Omni-Edu-9B.mmproj-bf16.gguf", "Omni-Edu-9B-GGUF", 921704864L,  "06278828402e368b3a2008f50be3034a1a9b47612ade5208aa15fe139a204313"),
        };
    }
}

internal sealed class GpuInfo
{
    public bool Nvidia;
    public string Name = "未检测到";
    public long VramMb;
    public string Driver = "";
}

internal sealed class HardwareDecision
{
    public ModelProfile Profile;   // 为 null 表示硬件不满足，启动失败
    public string Reason;
    public string Solution;
}

internal static class Hardware
{
    // 8GB / 16GB 显卡在 nvidia-smi 中的报告值会略小于标称值，留一点余量
    private const long Min4BMb = 7600;
    private const long Min9BMb = 15360;

    public static GpuInfo DetectGpu()
    {
        GpuInfo smi = DetectByNvidiaSmi();
        if (smi != null) return smi;

        // nvidia-smi 不可用时，用 WMI 判断是否存在 NVIDIA 显卡
        GpuInfo info = new GpuInfo();
        List<string> names = new List<string>();
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string n = obj["Name"] as string;
                    if (string.IsNullOrEmpty(n)) continue;
                    names.Add(n);
                    if (n.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        info.Nvidia = true;
                        info.Name = n;
                    }
                }
            }
        }
        catch { }

        if (!info.Nvidia && names.Count > 0)
            info.Name = string.Join(" / ", names.ToArray());
        return info;
    }

    private static GpuInfo DetectByNvidiaSmi()
    {
        List<string> candidates = new List<string>();
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation\\NVSMI\\nvidia-smi.exe"));
        candidates.Add("nvidia-smi.exe");

        foreach (string exe in candidates)
        {
            if (exe.IndexOf('\\') >= 0 && !File.Exists(exe)) continue;
            string output = RunCapture(exe, "--query-gpu=name,memory.total,driver_version --format=csv,noheader,nounits", 12000);
            if (output == null) continue;
            GpuInfo gpu = ParseNvidiaSmi(output);
            if (gpu != null) return gpu;
        }
        return null;
    }

    private static string RunCapture(string exe, string args, int timeoutMs)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            using (Process p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    return null;
                }
                if (p.ExitCode != 0) return null;
                return stdout;
            }
        }
        catch { return null; }
    }

    // nvidia-smi 输出示例：NVIDIA GeForce RTX 4090, 49140, 581.57
    private static GpuInfo ParseNvidiaSmi(string output)
    {
        GpuInfo best = null;
        string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Split(',');
            if (parts.Length < 3) continue;

            string memText = parts[parts.Length - 2].Trim();
            string drv = parts[parts.Length - 1].Trim();
            string name = string.Join(",", parts, 0, parts.Length - 2).Trim();

            long mb;
            if (!long.TryParse(memText, NumberStyles.Integer, CultureInfo.InvariantCulture, out mb)) continue;
            if (mb <= 0) continue;

            if (best == null || mb > best.VramMb)
            {
                GpuInfo g = new GpuInfo();
                g.Nvidia = true;
                g.Name = name;
                g.VramMb = mb;
                g.Driver = drv;
                best = g;
            }
        }
        return best;
    }

    public static HardwareDecision Evaluate(GpuInfo gpu)
    {
        HardwareDecision d = new HardwareDecision();

        if (gpu == null || !gpu.Nvidia)
        {
            d.Reason = "未检测到 NVIDIA 独立显卡。本程序使用 NVIDIA CUDA 推理引擎，AMD、Intel 显卡和纯核显无法运行。";
            d.Solution = "1. 改用配备 NVIDIA 显卡（显存 8 GB 及以上）的电脑运行。\r\n"
                       + "2. 如果本机确实有 NVIDIA 显卡，请到 https://www.nvidia.cn/drivers/ 下载安装最新驱动，重启后再试。\r\n"
                       + "3. 笔记本请插电使用，并在 NVIDIA 控制面板中把本程序设置为“高性能 NVIDIA 处理器”。";
            return d;
        }

        if (gpu.VramMb <= 0)
        {
            d.Reason = "检测到 NVIDIA 显卡，但无法读取显存容量，显卡驱动可能过旧或安装异常。";
            d.Solution = "1. 到 https://www.nvidia.cn/drivers/ 下载并安装最新显卡驱动，重启电脑。\r\n"
                       + "2. 按 Win+R 输入 cmd，运行 nvidia-smi，确认能正常显示显卡型号和显存。\r\n"
                       + "3. 如果仍无法读取，请更换一台 NVIDIA 显卡正常的电脑。";
            return d;
        }

        int driverMajor = DriverMajor(gpu.Driver);
        if (driverMajor > 0 && driverMajor < 525)
        {
            d.Reason = "显卡驱动版本 " + gpu.Driver + " 过旧，无法支持本程序使用的 CUDA 12 推理引擎（要求 525 及以上）。";
            d.Solution = "1. 到 https://www.nvidia.cn/drivers/ 下载并安装最新显卡驱动。\r\n"
                       + "2. 安装完成后重启电脑，再双击 OmniEdu.exe。";
            return d;
        }

        if (gpu.VramMb < Min4BMb)
        {
            d.Reason = "显卡显存为 " + FmtVram(gpu.VramMb) + "，低于 4B 模型最低要求的 8 GB，无法加载模型。";
            d.Solution = "1. 改用 NVIDIA 显卡显存 8 GB 及以上的电脑。\r\n"
                       + "2. 关闭游戏、视频剪辑、浏览器等占用显存的程序后重试。\r\n"
                       + "3. 笔记本请确认已启用独立显卡（NVIDIA 控制面板 → 管理 3D 设置 → 高性能 NVIDIA 处理器）。";
            return d;
        }

        d.Profile = gpu.VramMb >= Min9BMb ? Profiles.B9 : Profiles.B4;
        return d;
    }

    // 低显存机器自动缩小上下文，避免 32K 缓存把显存撑爆
    public static int ChooseContextSize(ModelProfile profile, long vramMb)
    {
        if (profile.Key == "4B")
        {
            if (vramMb < 10000) return 8192;
            if (vramMb < 12000) return 16384;
            return 32768;
        }
        if (vramMb < 20000) return 16384;
        return 32768;
    }

    public static string BuildFailMessage(GpuInfo gpu, HardwareDecision d)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Omni-Edu 运行失败：当前电脑不满足最低硬件要求。");
        sb.AppendLine();
        sb.AppendLine("【检测结果】");
        if (gpu == null)
        {
            sb.AppendLine("显卡：未检测到");
        }
        else
        {
            sb.AppendLine("显卡：" + (string.IsNullOrEmpty(gpu.Name) ? "未检测到" : gpu.Name));
            sb.AppendLine("显存：" + (gpu.VramMb > 0 ? FmtVram(gpu.VramMb) : "无法读取"));
            if (!string.IsNullOrEmpty(gpu.Driver)) sb.AppendLine("驱动：" + gpu.Driver);
        }
        double ram = TotalRamGb();
        if (ram > 0) sb.AppendLine("系统内存：" + ram.ToString("0.0") + " GB");
        sb.AppendLine();
        sb.AppendLine("【失败原因】");
        sb.AppendLine(d.Reason);
        sb.AppendLine();
        sb.AppendLine("【解决办法】");
        sb.AppendLine(d.Solution);
        return sb.ToString().TrimEnd();
    }

    public static string FmtVram(long mb)
    {
        return (mb / 1024.0).ToString("0.#") + " GB";
    }

    private static int DriverMajor(string v)
    {
        if (string.IsNullOrEmpty(v)) return 0;
        int i = 0;
        while (i < v.Length && char.IsDigit(v[i])) i++;
        if (i == 0) return 0;
        int n;
        return int.TryParse(v.Substring(0, i), out n) ? n : 0;
    }

    private static double TotalRamGb()
    {
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    object v = obj["TotalPhysicalMemory"];
                    if (v != null) return Convert.ToDouble(v) / 1073741824.0;
                }
            }
        }
        catch { }
        return 0;
    }
}

internal static class Program
{
    private const string ServerUrl = "http://127.0.0.1:8080";
    private const int MaxWaitSeconds = 600;        // 模型加载最长等待
    private const int AttachWaitSeconds = 10800;   // 附着模式（等另一个实例下载+加载）最长 3 小时

    internal static string RootDir;
    private static string LogPath;
    private static Process ServerProc;
    private static bool WeStartedServer;
    private static readonly object LogLock = new object();
    private static StreamWriter LogWriter;
    private static Mutex _mutex;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // 魔搭要求 TLS1.2；老系统默认协议可能连不上
        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
        try { ServicePointManager.Expect100Continue = false; } catch { }

        // 以 EXE 所在目录为根目录，整个文件夹可随意迁移（不写绝对路径）
        RootDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        string serverExe = Path.Combine(RootDir, "runtime\\llama.cpp\\llama-server.exe");
        if (!File.Exists(serverExe))
        {
            ShowError("找不到推理引擎：\r\n" + serverExe + "\r\n\r\n请确认本程序放在 Omni-Edu 文件夹的根目录。", "Omni-Edu 运行失败");
            return;
        }

        // 自动检测硬件，决定运行 4B 还是 9B
        GpuInfo gpu = Hardware.DetectGpu();
        HardwareDecision decision = Hardware.Evaluate(gpu);
        if (decision.Profile == null)
        {
            MessageBox.Show(Hardware.BuildFailMessage(gpu, decision),
                "Omni-Edu 运行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ModelProfile profile = decision.Profile;
        int ctxSize = Hardware.ChooseContextSize(profile, gpu.VramMb);
        string modelPath = Path.Combine(RootDir, "models\\" + profile.MainName);
        string mmprojPath = Path.Combine(RootDir, "models\\" + profile.MmprojName);

        // 单实例锁：连点双击只会产生一个"管事"的启动器，
        // 其余实例一律附着（不下载、不启动服务、不杀服务），杜绝互相抢端口误停服务
        bool firstInstance;
        _mutex = new Mutex(true, "OmniEdu-Launcher", out firstInstance);
        if (!firstInstance)
        {
            if (WaitReadyWithPrep(true, profile, gpu, ctxSize)) OpenChatAndWait(profile);
            return;
        }

        try
        {
            // 只检查所选模型的两个文件，缺失时自动下载（全部就位时秒过，不弹窗）
            if (!EnsureModels(profile)) return;

            if (IsReady())
            {
                // 服务已在运行（例如备用 bat 起的），直接打开问答窗口，退出时不杀服务
                OpenChatAndWait(profile);
                return;
            }

            // 服务没在跑，残留的专用浏览器进程一定是僵尸（窗口页面已失效），
            // 必须清掉，否则新浏览器进程会"交接"给旧进程后立刻退出，导致误停服务
            KillStaleAppBrowsers();

            if (!StartServer(serverExe, modelPath, mmprojPath, profile, ctxSize)) return;

            if (!WaitReadyWithPrep(false, profile, gpu, ctxSize))
            {
                StopServer();
                return;
            }

            OpenChatAndWait(profile);
            StopServer();
        }
        finally
        {
            try { _mutex.ReleaseMutex(); } catch { }
            try { _mutex.Dispose(); } catch { }
        }
    }

    // ---------------- 模型文件检查与下载 ----------------

    private static bool EnsureModels(ModelProfile profile)
    {
        string dir = Path.Combine(RootDir, "models");
        Directory.CreateDirectory(dir);

        List<ModelFile> missing = new List<ModelFile>();
        foreach (ModelFile f in profile.Files)
        {
            string p = Path.Combine(dir, f.Name);
            if (!File.Exists(p) || new FileInfo(p).Length != f.Size) missing.Add(f);
        }
        if (missing.Count == 0) return true;

        // 有缺失：弹欢迎窗口，后台线程顺序下载
        bool succeeded = false;
        string failMsg = null;
        using (PrepForm form = new PrepForm(profile.Title))
        {
            form.SetState("首次使用，正在下载 " + profile.Key + " 模型文件……", "共 " + missing.Count + " 个文件，下载完成后会自动校验", -1);

            Thread worker = new Thread(delegate()
            {
                string err = RunDownloads(missing, form);
                try
                {
                    form.BeginInvoke((MethodInvoker)delegate
                    {
                        if (err == null) { succeeded = true; }
                        else if (err != "CANCELLED") { failMsg = err; }
                        form.Close();
                    });
                }
                catch { }
            });
            worker.IsBackground = true;
            form.Shown += delegate { worker.Start(); };
            Application.Run(form);
        }

        if (failMsg != null)
        {
            ShowError(failMsg + "\r\n\r\n请检查网络后重新双击启动，已下载的部分会自动续传。", profile.Title);
        }
        return succeeded;
    }

    // 返回 null=全部成功；"CANCELLED"=用户关窗取消；其余=错误信息
    private static string RunDownloads(List<ModelFile> missing, PrepForm form)
    {
        string dir = Path.Combine(RootDir, "models");
        for (int i = 0; i < missing.Count; i++)
        {
            ModelFile f = missing[i];
            if (form.Cancelled) return "CANCELLED";

            string err = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                form.SetState("正在下载 " + (i + 1) + "/" + missing.Count + "：" + f.Name, "正在连接魔搭社区……", 0);
                err = DownloadOne(f, i, missing.Count, form);
                if (err == null) break;
                if (form.Cancelled) return "CANCELLED";
                form.SetState("下载出错，2 秒后重试（第 " + attempt + "/3 次）", err, -1);
                Thread.Sleep(2000);
            }
            if (err != null) return "下载失败：" + f.Name + "\r\n" + err;

            // 下载完立即校验，损坏则删除重来
            form.SetState("校验文件 " + (i + 1) + "/" + missing.Count + "：" + f.Name, "SHA256 校验中，大文件约需半分钟……", -1);
            string finalPath = Path.Combine(dir, f.Name);
            if (!VerifySha256(finalPath, f.Sha256))
            {
                try { File.Delete(finalPath); } catch { }
                return "文件校验未通过（已删除损坏文件）：" + f.Name;
            }
        }
        return null;
    }

    // 下载单个文件到 models\xxx.part，完成后改名；支持断点续传。返回 null=成功
    private static string DownloadOne(ModelFile f, int index, int total, PrepForm form)
    {
        string finalPath = Path.Combine(RootDir, "models", f.Name);
        string partPath = finalPath + ".part";
        try
        {
            long start = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
            if (start >= f.Size) { try { File.Delete(partPath); } catch { } start = 0; }

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(f.Url);
            req.AllowAutoRedirect = true;
            req.Timeout = 20000;
            req.ReadWriteTimeout = 30000;
            req.UserAgent = "Mozilla/5.0 OmniEdu-Launcher";
            if (start > 0) req.AddRange(start);

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                bool append = start > 0 && resp.StatusCode == HttpStatusCode.PartialContent;
                if (!append) start = 0; // 服务端没续传，从头下
                long totalBytes = start + resp.ContentLength;

                using (Stream rs = resp.GetResponseStream())
                using (FileStream fs = new FileStream(partPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buf = new byte[256 * 1024];
                    long done = start;
                    long lastMark = done;
                    double speed = 0;
                    Stopwatch sw = Stopwatch.StartNew();
                    int n;
                    while ((n = rs.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (form.Cancelled) return "CANCELLED";
                        fs.Write(buf, 0, n);
                        done += n;

                        if (sw.ElapsedMilliseconds >= 500)
                        {
                            double inst = (done - lastMark) / sw.Elapsed.TotalSeconds;
                            speed = speed <= 0 ? inst : speed * 0.6 + inst * 0.4;
                            lastMark = done;
                            sw.Restart();

                            double frac = totalBytes > 0 ? (double)done / totalBytes : 0;
                            string eta = speed > 1024 ? FmtTime((totalBytes - done) / speed) : "估算中……";
                            form.SetState(
                                "正在下载 " + (index + 1) + "/" + total + "：" + f.Name,
                                FmtSize(done) + " / " + FmtSize(totalBytes) + "　·　" + FmtSize((long)speed) + "/s　·　剩余约 " + eta,
                                frac);
                        }
                    }
                }
            }

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(partPath, finalPath);
            long len = new FileInfo(finalPath).Length;
            if (len != f.Size)
            {
                try { File.Delete(finalPath); } catch { }
                return "文件大小不符（" + len + " / " + f.Size + "）";
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static bool VerifySha256(string path, string expectedHex)
    {
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                System.Text.StringBuilder sb = new System.Text.StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return string.Equals(sb.ToString(), expectedHex, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { return false; }
    }

    private static string FmtSize(long bytes)
    {
        double mb = bytes / 1048576.0;
        if (mb >= 1024.0) return (mb / 1024.0).ToString("0.00") + " GB";
        return mb.ToString("0.0") + " MB";
    }

    private static string FmtTime(double seconds)
    {
        if (seconds < 60) return ((int)seconds) + " 秒";
        if (seconds < 3600) return ((int)(seconds / 60)) + " 分钟";
        return ((int)(seconds / 3600)) + " 小时 " + ((int)((seconds % 3600) / 60)) + " 分钟";
    }

    // ---------------- 服务启动与等待 ----------------

    private static bool StartServer(string serverExe, string modelPath, string mmprojPath, ModelProfile profile, int ctxSize)
    {
        try
        {
            string logDir = Path.Combine(RootDir, "logs");
            Directory.CreateDirectory(logDir);
            LogPath = Path.Combine(logDir, "server-" + profile.Key + ".log");
            LogWriter = new StreamWriter(LogPath, false);
            LogWriter.AutoFlush = true;

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = serverExe;
            psi.Arguments = "--model \"" + modelPath + "\""
                + " --mmproj \"" + mmprojPath + "\""
                + " --n-gpu-layers 99 --ctx-size " + ctxSize + " --image-min-tokens 1024"
                + " --port 8080 --host 127.0.0.1";
            psi.WorkingDirectory = Path.GetDirectoryName(serverExe);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            ServerProc = new Process();
            ServerProc.StartInfo = psi;
            ServerProc.OutputDataReceived += OnLog;
            ServerProc.ErrorDataReceived += OnLog;
            ServerProc.Start();
            ServerProc.BeginOutputReadLine();
            ServerProc.BeginErrorReadLine();
            WeStartedServer = true;
            return true;
        }
        catch (Exception ex)
        {
            ShowError("模型服务启动失败：\r\n" + ex.Message, profile.Title);
            return false;
        }
    }

    private static void OnLog(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;
        lock (LogLock)
        {
            try { LogWriter.WriteLine(e.Data); } catch { }
        }
    }

    // 欢迎窗口等待模型就绪（attachOnly=true 时只等不杀服务，且等待时间更长）
    private static bool WaitReadyWithPrep(bool attachOnly, ModelProfile profile, GpuInfo gpu, int ctxSize)
    {
        using (PrepForm form = new PrepForm(profile.Title))
        {
            string detail = attachOnly
                ? "就绪后会自动打开问答窗口"
                : "已自动选择 " + profile.Key + " 模型（" + gpu.Name + "，" + Hardware.FmtVram(gpu.VramMb)
                  + " 显存，" + (ctxSize / 1024) + "K 上下文），首次加载约需 1-3 分钟";
            form.SetState(attachOnly ? "另一个启动窗口正在准备，请稍候……" : "正在加载模型，请稍候……", detail, -1);

            bool ready = false;
            string failMsg = null;
            int waited = 0;
            int maxWait = attachOnly ? AttachWaitSeconds : MaxWaitSeconds;

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += delegate
            {
                waited++;
                if (IsReady())
                {
                    ready = true;
                    timer.Stop();
                    form.Close();
                    return;
                }
                if (!attachOnly && ServerProc != null && ServerProc.HasExited)
                {
                    if (IsReady())
                    {
                        // 重复双击时端口被另一个实例接管，转为附着模式（退出时不杀服务）
                        WeStartedServer = false;
                        ready = true;
                    }
                    else
                    {
                        failMsg = "模型服务异常退出。\r\n请把日志文件发给技术人员排查：\r\n" + LogPath;
                    }
                    timer.Stop();
                    form.Close();
                    return;
                }
                if (waited >= maxWait)
                {
                    failMsg = "等待超时。\r\n请把日志文件发给技术人员排查：\r\n" + LogPath;
                    timer.Stop();
                    form.Close();
                    return;
                }
            };

            form.Shown += delegate { timer.Start(); };
            Application.Run(form);

            if (ready) return true;
            if (failMsg != null) ShowError(failMsg, profile.Title);
            return false; // 用户手动关闭欢迎窗口 = 取消启动
        }
    }

    // ---------------- 问答窗口 ----------------

    private static void OpenChatAndWait(ModelProfile profile)
    {
        string browser = FindBrowser();
        if (browser != null)
        {
            try
            {
                // 独立 user-data-dir：应用窗口与普通浏览器互不影响
                string userData = Path.Combine(RootDir, "appdata\\browser");
                EnsureFreshBrowserProfile(userData, browser);
                Directory.CreateDirectory(userData);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = browser;
                psi.Arguments = "--user-data-dir=\"" + userData + "\""
                    + " --no-first-run --no-default-browser-check"
                    + " --disable-background-mode"
                    + " --window-size=1120,820"
                    + " --app=" + ServerUrl;
                psi.UseShellExecute = false;

                Process p = Process.Start(psi);

                // 不能等 p 退出：新进程可能把窗口"交接"给已存在的浏览器进程后自己立刻退出，
                // 会误判成"用户关了窗口"。改为轮询专用浏览器进程（命令行带本目录
                // appdata\browser 标记）是否还存在，归零才说明问答窗口真的关了
                for (int grace = 0; grace < 30; grace++)
                {
                    if (CountAppBrowsers(userData) != 0) break;
                    Thread.Sleep(1000);
                }
                int goneRounds = 0;
                while (goneRounds < 3)
                {
                    Thread.Sleep(1500);
                    if (WeStartedServer && ServerProc != null && ServerProc.HasExited) break; // 服务意外退出
                    int c = CountAppBrowsers(userData);
                    if (c < 0)
                    {
                        // WMI 不可用：退回等启动的浏览器进程退出
                        try { if (p != null && p.HasExited) break; } catch { }
                        continue;
                    }
                    if (c == 0) goneRounds++; else goneRounds = 0;
                }
                return;
            }
            catch { }
        }

        // 找不到 Edge/Chrome：退回默认浏览器
        Process.Start(ServerUrl);
        MessageBox.Show("问答页面已在默认浏览器中打开。\r\n用完后点击“确定”停止模型服务并退出。",
            profile.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // 统计命令行里带 appdata\browser 标记的浏览器进程数；WMI 故障返回 -1
    private static int CountAppBrowsers(string marker)
    {
        int n = 0;
        try
        {
            using (System.Management.ManagementObjectSearcher searcher =
                new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='msedge.exe' OR Name='chrome.exe'"))
            {
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    string cmd = obj["CommandLine"] as string;
                    if (cmd != null && cmd.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        n++;
                    }
                }
            }
        }
        catch { return -1; }
        return n;
    }

    // 清理残留的专用浏览器进程（只匹配本文件夹 appdata\browser 配置，
    // 绝不碰用户正常使用的浏览器），并等待其完全退出
    private static void KillStaleAppBrowsers()
    {
        string marker = Path.Combine(RootDir, "appdata\\browser");
        for (int round = 0; round < 16; round++)
        {
            List<int> stale = new List<int>();
            try
            {
                using (System.Management.ManagementObjectSearcher searcher =
                    new System.Management.ManagementObjectSearcher(
                        "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='msedge.exe' OR Name='chrome.exe'"))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        string cmd = obj["CommandLine"] as string;
                        if (cmd != null && cmd.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            stale.Add(Convert.ToInt32(obj["ProcessId"]));
                        }
                    }
                }
            }
            catch { return; } // WMI 不可用时放弃清理，继续走原逻辑

            if (stale.Count == 0) return;
            foreach (int pid in stale)
            {
                try { Process.GetProcessById(pid).Kill(); } catch { }
            }
            Thread.Sleep(500);
        }
    }

    // 浏览器 user-data-dir 跨机器拷贝、浏览器升级或崩溃后，Edge 会在启动时触发
    // 内部断点（SubCode=0x80000003，msedge.dll），表现为问答窗口一闪即退。
    // 这里在每次打开问答窗口前做一次轻量自检：本机 Edge 版本与上次记录的
    // Last Version 不一致，或 Crashpad 里已有崩溃转储，都判定为损坏，删除
    // 整个配置目录让 Edge 自动重建。自愈失败吞掉异常，保证主流程还能退回
    // 默认浏览器（OpenChatAndWait 末尾的兜底分支）。
    private static void EnsureFreshBrowserProfile(string userData, string browserExe)
    {
        try
        {
            if (!Directory.Exists(userData)) return;

            bool corrupted = false;

            // 信号 1：浏览器版本变更（跨机器拷贝、Edge/Chrome 升级都触发）
            string expectedVersion = null;
            try
            {
                System.Diagnostics.FileVersionInfo vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(browserExe);
                if (vi != null) expectedVersion = vi.ProductVersion;
            }
            catch { }

            if (!string.IsNullOrEmpty(expectedVersion))
            {
                string lastVersionFile = Path.Combine(userData, "Last Version");
                if (File.Exists(lastVersionFile))
                {
                    string recordedVersion;
                    try { recordedVersion = File.ReadAllText(lastVersionFile).Trim(); } catch { recordedVersion = ""; }
                    if (!string.IsNullOrEmpty(recordedVersion) && recordedVersion != expectedVersion)
                        corrupted = true;
                }
            }

            // 信号 2：已有崩溃转储（上次用这个 profile 启动 Edge 崩过）
            if (!corrupted)
            {
                string crashpadDir = Path.Combine(userData, "Crashpad");
                if (Directory.Exists(crashpadDir))
                {
                    try
                    {
                        foreach (string dmp in Directory.EnumerateFiles(crashpadDir, "*.dmp", SearchOption.AllDirectories))
                        { corrupted = true; break; }
                    }
                    catch { }
                }
            }

            if (corrupted)
            {
                KillStaleAppBrowsers();
                try { Directory.Delete(userData, true); } catch { }
            }
        }
        catch { }
    }

    private static string FindBrowser()
    {
        List<string> candidates = new List<string>();
        string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        string pf = Environment.GetEnvironmentVariable("ProgramFiles");
        string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (pf86 != null) candidates.Add(Path.Combine(pf86, "Microsoft\\Edge\\Application\\msedge.exe"));
        if (pf != null) candidates.Add(Path.Combine(pf, "Microsoft\\Edge\\Application\\msedge.exe"));
        if (pf != null) candidates.Add(Path.Combine(pf, "Google\\Chrome\\Application\\chrome.exe"));
        if (pf86 != null) candidates.Add(Path.Combine(pf86, "Google\\Chrome\\Application\\chrome.exe"));
        if (lad != null) candidates.Add(Path.Combine(lad, "Google\\Chrome\\Application\\chrome.exe"));
        foreach (string c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        return null;
    }

    private static bool IsReady()
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(ServerUrl + "/health");
            req.Method = "GET";
            req.Timeout = 2000;
            req.ReadWriteTimeout = 2000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                return (int)resp.StatusCode == 200;
            }
        }
        catch { return false; }
    }

    private static void StopServer()
    {
        try
        {
            if (WeStartedServer && ServerProc != null && !ServerProc.HasExited)
            {
                ServerProc.Kill();
                ServerProc.WaitForExit(5000);
            }
        }
        catch { }
        try
        {
            lock (LogLock)
            {
                if (LogWriter != null)
                {
                    LogWriter.Close();
                    LogWriter = null;
                }
            }
        }
        catch { }
    }

    private static void ShowError(string msg, string title)
    {
        MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

// 欢迎/准备窗口：C2510 背景图 + 底部居中自绘状态区（状态文字、进度条、速度/剩余时间）
internal sealed class PrepForm : Form
{
    private readonly object _sync = new object();
    private string _status = "正在准备……";
    private string _detail = "";
    private double _fraction = -1.0; // 负数 = 滚动动画（不确定进度）
    private int _tick;
    private readonly System.Windows.Forms.Timer _paintTimer;
    private volatile bool _cancelled;

    public bool Cancelled { get { return _cancelled; } }

    public PrepForm(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(840, 300); // 与背景图 3840x1371 同比例
        TopMost = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        BackColor = Color.Black;
        DoubleBuffered = true;

        string img = Path.Combine(Program.RootDir, "assets\\welcome.png");
        if (File.Exists(img))
        {
            try
            {
                BackgroundImage = Image.FromFile(img);
                BackgroundImageLayout = ImageLayout.Zoom;
            }
            catch { }
        }

        _paintTimer = new System.Windows.Forms.Timer();
        _paintTimer.Interval = 120;
        _paintTimer.Tick += delegate { _tick++; Invalidate(); };
        _paintTimer.Start();
    }

    public void SetState(string status, string detail, double fraction)
    {
        lock (_sync)
        {
            _status = status ?? "";
            _detail = detail ?? "";
            _fraction = fraction;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cancelled = true;
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        string status, detail;
        double frac;
        lock (_sync) { status = _status; detail = _detail; frac = _fraction; }

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = ClientSize.Width;
        int h = ClientSize.Height;

        // 底部半透明圆角底衬
        Rectangle panel = new Rectangle((int)(w * 0.09), h - 98, (int)(w * 0.82), 86);
        using (GraphicsPath pp = RoundRect(panel, 12))
        using (SolidBrush pb = new SolidBrush(Color.FromArgb(118, 0, 0, 0)))
        {
            g.FillPath(pb, pp);
        }

        StringFormat center = new StringFormat();
        center.Alignment = StringAlignment.Center;
        center.LineAlignment = StringAlignment.Center;

        // 状态行（小字，白色加粗）
        using (Font fs = new Font("微软雅黑", 9f, FontStyle.Bold))
        using (SolidBrush wb = new SolidBrush(Color.White))
        {
            g.DrawString(status, fs, wb, new Rectangle(panel.X, panel.Y + 6, panel.Width, 20), center);
        }

        // 进度条
        Rectangle bar = new Rectangle((int)(w * 0.17), panel.Y + 34, (int)(w * 0.66), 9);
        using (GraphicsPath bp = RoundRect(bar, 4))
        using (SolidBrush bb = new SolidBrush(Color.FromArgb(170, 70, 70, 70)))
        {
            g.FillPath(bb, bp);
        }
        if (frac >= 0)
        {
            int fw = Math.Max((int)(bar.Width * Math.Min(1.0, frac)), 10);
            using (GraphicsPath fp = RoundRect(new Rectangle(bar.X, bar.Y, fw, bar.Height), 4))
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(255, 90, 180, 255)))
            {
                g.FillPath(fb, fp);
            }
        }
        else
        {
            // 滚动亮块：来回滑动表示"工作中"
            int seg = bar.Width / 3;
            double phase = (_tick % 40) / 39.0;
            double k = phase < 0.5 ? phase * 2 : 2 - phase * 2;
            int x = bar.X + (int)((bar.Width - seg) * k);
            using (GraphicsPath fp = RoundRect(new Rectangle(x, bar.Y, seg, bar.Height), 4))
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            {
                g.FillPath(fb, fp);
            }
        }

        // 细节行（更小字，浅灰）：大小 / 速度 / 剩余时间
        using (Font fd = new Font("微软雅黑", 7.5f, FontStyle.Regular))
        using (SolidBrush gb = new SolidBrush(Color.FromArgb(215, 215, 215)))
        {
            g.DrawString(detail, fd, gb, new Rectangle(panel.X, panel.Y + 50, panel.Width, 28), center);
        }
    }

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
