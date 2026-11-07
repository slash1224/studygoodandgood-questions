using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TVInstaller
{
    public partial class MainWindow : Window
    {
        private const string PackageName = "com.studygoodandgood.tv";
        private const string ActivityName = "com.studygoodandgood.tv.MainActivity";
        private const int HttpPort = 8899;

        private string _adbDir;
        private string _apkPath;
        private string _adbExe;

        private sealed class DeviceItem
        {
            public string Ip { get; set; }
            public string Display => Ip + " : 5555";
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += (s, e) => { try { StopHttp(); } catch { } };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                PreparePayload();
                StartHttp();
                TxtUrl.Text = $"http://{LocalIp()}:{HttpPort}/";
                TxtPhone.Text = "手机也能打开这个地址（同一个 WiFi），页面里同样能下载安装包";
                Log($"安装包已就绪：{_apkPath}");
                Log($"局域网下载地址：{TxtUrl.Text}");
                Log("提示：电视浏览器打不开时，多半是电视没浏览器，请用「方式一」的 ADB 直连安装。");
            }
            catch (Exception ex)
            {
                Log("初始化失败：" + ex.Message);
            }
        }

        // ---------------- 内置文件释放 ----------------

        private void PreparePayload()
        {
            _adbDir = Path.Combine(Path.GetTempPath(), "StudyTVInstaller");
            Directory.CreateDirectory(_adbDir);
            Extract("adb.exe", Path.Combine(_adbDir, "adb.exe"));
            Extract("AdbWinApi.dll", Path.Combine(_adbDir, "AdbWinApi.dll"));
            Extract("AdbWinUsbApi.dll", Path.Combine(_adbDir, "AdbWinUsbApi.dll"));
            _adbExe = Path.Combine(_adbDir, "adb.exe");

            _apkPath = Path.Combine(_adbDir, "学习好电视.apk");
            Extract("StudyTV.apk", _apkPath);
        }

        private static void Extract(string name, string dest)
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (s == null) throw new Exception("内置文件缺失：" + name);
            using var f = File.Create(dest);
            s.CopyTo(f);
        }

        // ---------------- 局域网 HTTP ----------------

        private TcpListener _tcp;
        private Thread _httpThread;
        private bool _httpRunning;

        private void StartHttp()
        {
            try
            {
                _tcp = new TcpListener(IPAddress.Any, HttpPort);
                _tcp.Start();
                _httpRunning = true;
                _httpThread = new Thread(() =>
                {
                    while (_httpRunning)
                    {
                        try
                        {
                            TcpClient c = _tcp.AcceptTcpClient();
                            Task.Run(() => Serve(c));
                        }
                        catch { break; }
                    }
                }) { IsBackground = true };
                _httpThread.Start();
            }
            catch (Exception ex)
            {
                Log("本地下载服务启动失败（可忽略，方式一仍可用）：" + ex.Message);
            }
        }

        private void StopHttp()
        {
            _httpRunning = false;
            try { _tcp?.Stop(); } catch { }
            _tcp = null;
        }

        private void Serve(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 8000;
                    var ns = client.GetStream();
                    var head = new StringBuilder();
                    var buf = new byte[4096];
                    while (head.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                    {
                        int n = ns.Read(buf, 0, buf.Length);
                        if (n <= 0) return;
                        head.Append(Encoding.ASCII.GetString(buf, 0, n));
                        if (head.Length > 8192) return;
                    }
                    string first = head.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                    string[] parts = first.Split(' ');
                    string path = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "/";
                    int q = path.IndexOf('?');
                    if (q >= 0) path = path.Substring(0, q);

                    if (path == "/" || path == "/index.html")
                    {
                        Write(ns, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(IndexHtml()));
                    }
                    else if (path.EndsWith(".apk"))
                    {
                        byte[] data = File.ReadAllBytes(_apkPath);
                        Write(ns, 200, "application/vnd.android.package-archive", data);
                        Log("电视已开始下载安装包（" + client.Client.RemoteEndPoint + "）");
                    }
                    else
                    {
                        Write(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("not found"));
                    }
                }
            }
            catch { }
        }

        private static void Write(Stream ns, int code, string type, byte[] data)
        {
            var h = "HTTP/1.1 " + code + " " + (code == 200 ? "OK" : "Not Found") + "\r\n"
                    + "Content-Type: " + type + "\r\n"
                    + "Content-Length: " + data.Length + "\r\n"
                    + "Connection: close\r\n\r\n";
            var hb = Encoding.UTF8.GetBytes(h);
            ns.Write(hb, 0, hb.Length);
            ns.Write(data, 0, data.Length);
            ns.Flush();
        }

        private string IndexHtml()
        {
            var apkMB = Math.Round(new FileInfo(_apkPath).Length / 1024.0 / 1024.0, 1);
            return "<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">" +
                   "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                   "<title>学习好电视 · 安装</title><style>" +
                   "body{margin:0;padding:28px 20px;background:#0d1520;color:#e8f1f8;font-family:-apple-system,'PingFang SC','Microsoft YaHei',sans-serif}" +
                   "h1{font-size:22px;margin:0 0 8px}p{color:#93a6ba;font-size:14px;line-height:1.7}" +
                   "a.b{display:block;margin:26px 0;background:#2bb673;color:#fff;text-decoration:none;text-align:center;" +
                   "padding:18px;border-radius:14px;font-size:20px;font-weight:700}" +
                   ".card{background:#172433;border-radius:14px;padding:18px;margin-top:16px}" +
                   ".card h2{font-size:15px;color:#7fb3ff;margin:0 0 8px}" +
                   "</style></head><body>" +
                   "<h1>学习好电视 · 电视端安装包</h1><p>APK 大小 " + apkMB + " MB，安装后桌面上会出现「学习好电视」。</p>" +
                   "<a class=\"b\" href=\"/StudyTV.apk\">下载安装包（点击开始下载）</a>" +
                   "<div class=\"card\"><h2>下载后怎么装</h2><p>1. 下载完成后，电视一般会自动弹出安装界面；如果没有，到「文件管理/下载」里找到 StudyTV.apk 点击安装。<br/>" +
                   "2. 提示「未知来源」或「禁止安装」时，选择「设置 → 允许安装」。<br/>" +
                   "3. 小米/海信/TCL/创维等安卓电视都能装；三星 Tizen、LG webOS 不是安卓系统，装不了。</p></div>" +
                   "<div class=\"card\"><h2>想要更严格的模式</h2><p>装好后打开「学习好电视 → 家长管理 → 设为电视桌面」，" +
                   "然后在电视设置里选它作为默认桌面，孩子按 HOME 键就会回到学习页。</p></div>" +
                   "</body></html>";
        }

        private static string LocalIp()
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        var ip = ua.Address.ToString();
                        if (ip.StartsWith("192.") || ip.StartsWith("10.") || ip.StartsWith("172.")) return ip;
                    }
                }
            }
            catch { }
            return "本机IP";
        }

        // ---------------- ADB ----------------

        private async Task<string> RunAdb(string args, int timeoutMs = 30000)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo(_adbExe, args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = _adbDir
                    };
                    using var p = Process.Start(psi);
                    var sb = new StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return "命令超时"; }
                    return sb.ToString().Trim();
                }
                catch (Exception ex)
                {
                    return "调用 adb 失败：" + ex.Message;
                }
            });
        }

        private async void OnScan(object sender, RoutedEventArgs e)
        {
            BtnScan.IsEnabled = false;
            LstDevices.Items.Clear();
            Log("开始扫描局域网中的电视（约 10 秒）…");
            var found = await ScanTvs();
            foreach (var ip in found)
                LstDevices.Items.Add(new DeviceItem { Ip = ip });
            Log(found.Count == 0
                ? "没扫描到。请确认电视已开机并开启了「网络调试/ADB」，也可以在电视 IP 框里手动输入。"
                : "扫描到 " + found.Count + " 台设备。先点选一行，再点「连接并安装」。");
            if (found.Count > 0) LstDevices.SelectedIndex = 0;
            BtnScan.IsEnabled = true;
        }

        private static async Task<List<string>> ScanTvs()
        {
            var result = new List<string>();
            string self = LocalIp();
            if (!self.Contains('.')) return result;
            var parts = self.Split('.');
            var prefix = parts[0] + "." + parts[1] + "." + parts[2] + ".";
            var tasks = new List<Task<string>>();
            var tasksLock = new object();
            for (int i = 1; i <= 254; i++)
            {
                string ip = prefix + i;
                if (ip == self) continue;
                tasks.Add(Probe(ip));
            }
            var all = await Task.WhenAll(tasks);
            foreach (var ip in all.Where(x => x != null)) result.Add(ip);
            return result;
        }

        private static async Task<string> Probe(string ip)
        {
            try
            {
                using var c = new TcpClient();
                var t = c.ConnectAsync(ip, 5555);
                if (await Task.WhenAny(t, Task.Delay(600)) == t && c.Connected)
                {
                    try { c.Close(); } catch { }
                    return ip;
                }
                try { c.Close(); } catch { }
            }
            catch { }
            return null;
        }

        private async void OnInstall(object sender, RoutedEventArgs e)
        {
            string ip = TxtIp.Text.Trim();
            if (LstDevices.SelectedItem is DeviceItem di) ip = di.Ip;
            if (!ip.Contains('.')) { Log("请先填写电视 IP，或点「扫描局域网」。"); return; }

            BtnInstall.IsEnabled = false;
            try
            {
                Log($"连接 {ip}:5555 …");
                var outp = await RunAdb($"connect {ip}:5555", 20000);
                Log(string.IsNullOrEmpty(outp) ? "(无输出)" : outp);
                if (!outp.Contains("connected"))
                {
                    Log("连接失败。请检查：1) 电视与电脑是否同一 WiFi；2) 电视是否已打开「USB 调试 / 网络调试」；");
                    return;
                }
                Log("正在安装，电视上可能会弹出「允许安装」的确认框，请用遥控器点允许 …");
                // 首次 adb 连接需要在电视上确认授权，稍等用户操作
                await Task.Delay(1500);
                var install = await RunAdb($"install -r -d \"{_apkPath}\"", 180000);
                Log(install);
                if (install.Contains("Success"))
                {
                    Log("安装成功！正在电视上启动 …");
                    await RunAdb($"shell am start -n {PackageName}/{ActivityName}", 20000);
                    Log("完成。电视上应已出现「学习好电视」。");
                }
                else
                {
                    Log("安装没有成功，请看上面的电视返回信息。常见原因：电视空间不足、或需要在电视上点「允许安装」。");
                }
            }
            catch (Exception ex)
            {
                Log("出错：" + ex.Message);
            }
            finally
            {
                BtnInstall.IsEnabled = true;
            }
        }

        private async void OnSetHome(object sender, RoutedEventArgs e)
        {
            string ip = TxtIp.Text.Trim();
            if (LstDevices.SelectedItem is DeviceItem di) ip = di.Ip;
            if (!ip.Contains('.')) { Log("请先填写电视 IP 或扫描。"); return; }
            await RunAdb($"connect {ip}:5555", 15000);
            Log("正在把「学习好电视」设为电视默认桌面（严格模式）…");
            var r = await RunAdb($"shell cmd package set-home-activity {PackageName}/{ActivityName}", 20000);
            Log(r);
            if (r.Contains("Success") || r.Length == 0)
                Log("已设为默认桌面。孩子按 HOME 键会回到学习页；想恢复原来的桌面，在电视设置里改回来即可。");
            else
                Log("这台电视不允许远程改桌面。请在电视上打开本应用的「家长管理 → 设为电视桌面」手动选择。");
        }

        private async void OnUninstall(object sender, RoutedEventArgs e)
        {
            string ip = TxtIp.Text.Trim();
            if (LstDevices.SelectedItem is DeviceItem di) ip = di.Ip;
            if (!ip.Contains('.')) { Log("请先填写电视 IP 或扫描。"); return; }
            Log("如果当前默认桌面是本应用，请先在电视设置里换回原桌面，再卸载。");
            await RunAdb($"connect {ip}:5555", 15000);
            var r = await RunAdb($"uninstall {PackageName}", 30000);
            Log(r);
        }

        private void OnCopyUrl(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(TxtUrl.Text); Log("地址已复制到剪贴板。"); } catch { }
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
                TxtLog.ScrollToEnd();
            });
        }
    }
}
