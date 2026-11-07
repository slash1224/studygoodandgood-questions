using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace StudyLock
{
    /// <summary>
    /// 系统防护：锁屏期间禁用任务管理器 + 循环结束 taskmgr 进程。
    /// 孩子即使按 Ctrl+Alt+Del 也打不开任务管理器，无法结束本程序。
    /// 解除锁定后恢复任务管理器。
    /// </summary>
    public static class SystemGuard
    {
        private const string PolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private static bool _taskMgrDisabled;

        /// <summary>锁屏开始时调用</summary>
        public static void Engage()
        {
            SetTaskMgr(disabled: true);
        }

        /// <summary>解锁/退出时调用，恢复任务管理器</summary>
        public static void Release()
        {
            SetTaskMgr(disabled: false);
        }

        private static void SetTaskMgr(bool disabled)
        {
            if (_taskMgrDisabled == disabled) return;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(PolicyKey);
                if (disabled) key?.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                else key?.DeleteValue("DisableTaskMgr", false);
                _taskMgrDisabled = disabled;
            }
            catch { }
        }

        /// <summary>每2秒调用一次：杀掉已打开的任务管理器/进程管理工具</summary>
        public static void KillTaskManagers()
        {
            if (!_taskMgrDisabled) return;
            foreach (var name in new[] { "Taskmgr", "ProcessExplorer", "ProcessHacker", "procexp", "procexp64" })
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
                catch { }
            }
        }
    }
}
