using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace StudyLock
{
    /// <summary>
    /// 自安装 / 自卸载：
    /// - 家长双击 StudyGoodAndGood.exe 即自动复制到 Program Files、注册开机自启、开放管理页端口；
    /// - 卸载入口藏在家长手机管理页里（需访问码），孩子在本机找不到卸载方式。
    /// </summary>
    public static class Installer
    {
        public const string AppName = "StudyGoodAndGood";
        public static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);
        public static string InstalledExe => Path.Combine(InstallDir, AppName + ".exe");

        /// <summary>第二个实例 / 管理页用来通知正在运行的实例弹出锁屏</summary>
        public const string ShowLockEventName = "StudyGoodAndGood_ShowLock_9F3A";

        /// <summary>通知已在运行的实例立刻弹出锁屏（双击图标、手机管理页「立即锁屏」都走这里）</summary>
        public static bool RequestShowLock()
        {
            try
            {
                using var ev = System.Threading.EventWaitHandle.OpenExisting(ShowLockEventName);
                ev.Set();
                return true;
            }
            catch { return false; }
        }

        /// <summary>当前进程是否运行在安装目录中</summary>
        public static bool IsInstalled()
        {
            var exe = Environment.ProcessPath ?? "";
            try
            {
                return string.Equals(Path.GetFullPath(exe), Path.GetFullPath(InstalledExe),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// 启动时调用。未安装则自安装并启动安装后的副本。
        /// 返回 true 表示当前进程应退出（安装副本已启动）。
        /// </summary>
        public static bool EnsureInstalled()
        {
            // 清理改名前旧版（StudyLock）的残留：旧自启项会让新旧两版开机同时弹锁屏互抢前台
            RemoveLegacyInstall();

            // 先记录项目内 Omni-Edu 的实际路径；即使安装目录里的旧版正在运行导致复制失败，
            // 下次启动也能用上正确的 AI 路径
            AutoConfigureAiPath();

            if (IsInstalled())
            {
                // 已安装：每次启动都确认自启注册还在（防止被清理）
                RegisterAutostart();
                return false;
            }

            var src = Environment.ProcessPath!;
            try
            {
                Directory.CreateDirectory(InstallDir);
                File.Copy(src, InstalledExe, true);
            }
            catch (IOException)
            {
                // 安装目录里的版本正在后台运行，exe 被占用：
                // 这次双击就当作「一键召唤锁屏」，不再弹错误框
                if (File.Exists(InstalledExe))
                {
                    if (!RequestShowLock())
                        System.Windows.MessageBox.Show(
                            "程序已经在后台运行。\n\n" +
                            "马上锁屏：请在手机家长管理页点「立即锁屏」。",
                            "StudyGoodAndGood");
                    return true;
                }
                throw;
            }
            RegisterUrlAcl();
            RegisterAutostart();
            Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true });
            return true;
        }

        /// <summary>
        /// 首次安装/升级时，如果项目里自带 Omni-Edu 文件夹，就把探测到的路径写进 rules.json，
        /// 这样换一台电脑、换一个盘符也能直接用（探测基于程序自身位置，不写死绝对路径）。
        /// 如果配置里已经有可用的路径，则保持不变。
        /// </summary>
        private static void AutoConfigureAiPath()
        {
            try
            {
                var detected = DetectOmniEduNear(Environment.ProcessPath ?? "");
                if (detected == null) return;

                var rulesPath = Path.Combine(Guardian.DataDir, "rules.json");
                RuleConfig rules;
                if (File.Exists(rulesPath))
                {
                    var text = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(rulesPath))
                        .Trim().TrimStart('\uFEFF').Trim();
                    rules = JsonSerializer.Deserialize<RuleConfig>(text) ?? new RuleConfig();
                    if (!string.IsNullOrWhiteSpace(rules.AiLauncherPath)
                        && File.Exists(rules.AiLauncherPath)) return;
                }
                else
                {
                    rules = new RuleConfig();
                }

                rules.AiLauncherPath = detected;
                Directory.CreateDirectory(Guardian.DataDir);
                File.WriteAllBytes(rulesPath,
                    System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rules)));
            }
            catch { }
        }

        /// <summary>从 EXE 所在目录向上找 Omni-Edu\OmniEdu.exe（最多 6 层，覆盖仓库和构建目录布局）。</summary>
        private static string? DetectOmniEduNear(string exePath)
        {
            try
            {
                var dir = new DirectoryInfo(Path.GetDirectoryName(exePath) ?? "");
                for (int i = 0; i < 6 && dir != null; i++)
                {
                    var path = Path.Combine(dir.FullName, "Omni-Edu", "OmniEdu.exe");
                    if (File.Exists(path)) return path;
                    dir = dir.Parent;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 清理改名前旧版「StudyLock」的残留（进程、开机自启、安装目录、数据目录）。
        /// 旧版不清理的话，装过旧版的机器重启后会出现新旧两个锁屏窗口交替互抢。
        /// 全部静默容错，没装过旧版时什么都不做。
        /// </summary>
        private static void RemoveLegacyInstall()
        {
            const string legacy = "StudyLock";

            // 1. 结束正在运行的旧版进程
            try
            {
                foreach (var p in Process.GetProcessesByName(legacy))
                {
                    try { p.Kill(); } catch { }
                    p.Dispose();
                }
            }
            catch { }

            // 2. 删除旧版开机自启（计划任务 + 注册表 Run 键）
            Run("schtasks", $"/delete /tn \"{legacy}\" /f");
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue(legacy, false);
            }
            catch { }

            // 3. 删除旧版安装目录和数据目录
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), legacy);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { }
            try
            {
                var data = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), legacy);
                if (Directory.Exists(data)) Directory.Delete(data, true);
            }
            catch { }
        }

        private static void Run(string file, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args)
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p?.WaitForExit(10000);
            }
            catch { }
        }

        public static void RegisterUrlAcl()
        {
            Run("netsh", "http delete urlacl url=http://+:8099/");
            Run("netsh", "http add urlacl url=http://+:8099/ user=Everyone");
        }

        public static void RegisterAutostart()
        {
            // 计划任务（最高权限，避免 UAC 弹窗）。用 XML 定义创建，避开命令行里嵌套引号导致失败的问题
            try
            {
                var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? "";
                var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>StudyGoodAndGood 开机自启</Description></RegistrationInfo>
  <Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>
  <Principals><Principal id=""Author""><UserId>{sid}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
  <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><Enabled>true</Enabled></Settings>
  <Actions Context=""Author""><Exec><Command>{InstalledExe}</Command></Exec></Actions>
</Task>";
                var tmp = Path.Combine(Path.GetTempPath(), "sgg_autostart.xml");
                File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
                Run("schtasks", $"/delete /tn \"{AppName}\" /f");
                Run("schtasks", $"/create /tn \"{AppName}\" /xml \"{tmp}\" /f");
                try { File.Delete(tmp); } catch { }
            }
            catch { }

            // 注册表双保险
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue(AppName, $"\"{InstalledExe}\"");
            }
            catch { }
        }

        /// <summary>卸载：清除自启、端口、任务管理器禁用，并延迟删除安装目录。调用后进程应立即退出。</summary>
        public static void Uninstall()
        {
            Run("schtasks", $"/delete /tn \"{AppName}\" /f");
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue(AppName, false);
            }
            catch { }
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\System", true);
                key?.DeleteValue("DisableTaskMgr", false);
            }
            catch { }
            Run("netsh", "http delete urlacl url=http://+:8099/");
            // 程序正在运行无法删自己，启动延迟2秒的隐藏 cmd 来删
            Process.Start(new ProcessStartInfo("cmd.exe",
                $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{InstallDir}\"")
            { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = false });
        }
    }
}
