using System;
using System.Diagnostics;
using System.IO;
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
                // 安装目录里的旧版本正在后台运行，exe 被占用，无法覆盖
                if (File.Exists(InstalledExe))
                {
                    System.Windows.MessageBox.Show(
                        "程序已经在后台运行，不需要重复打开。\n\n" +
                        "如果确实要换成这个新版本：请先在手机家长管理页点「卸载」旧版，再双击本文件。",
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
