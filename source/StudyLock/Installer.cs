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
            Directory.CreateDirectory(InstallDir);
            File.Copy(src, InstalledExe, true);
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
            // 计划任务（最高权限，避免UAC弹窗）+ 注册表双保险
            Run("schtasks", $"/delete /tn \"{AppName}\" /f");
            Run("schtasks", $"/create /tn \"{AppName}\" /tr \"\\\"\"{InstalledExe}\"\\\"\" /sc onlogon /rl highest /f");
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
