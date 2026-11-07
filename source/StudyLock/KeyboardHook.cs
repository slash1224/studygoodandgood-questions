using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace StudyLock
{
    /// <summary>
    /// 全局低级键盘钩子：锁屏期间拦截 Win 键、Alt+Tab、Alt+F4、Ctrl+Esc 等系统组合键。
    /// 注意：Ctrl+Alt+Del 是 Windows 安全底线，任何应用都无法拦截（由 SystemGuard 配合禁用任务管理器兜底）。
    /// </summary>
    public class KeyboardHook : IDisposable
    {
        private IntPtr _hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _proc;
        private bool _active;

        public KeyboardHook()
        {
            _proc = HookCallback;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_TAB = 0x09;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_F4 = 0x73;
        private const int VK_MENU = 0x12; // Alt

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            using var cur = Process.GetCurrentProcess();
            using var mod = cur.MainModule!;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
            _active = true;
        }

        public void Stop()
        {
            _active = false;
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _active)
            {
                int vk = Marshal.ReadInt32(lParam);
                bool alt = (GetKeyState(VK_MENU) & 0x8000) != 0;
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                int msg = wParam.ToInt32();

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    // Win 键
                    if (vk == VK_LWIN || vk == VK_RWIN) return (IntPtr)1;
                    // Alt+Tab / Alt+Esc / Alt+F4
                    if (alt && (vk == VK_TAB || vk == VK_ESCAPE || vk == VK_F4)) return (IntPtr)1;
                    // Ctrl+Esc（弹出开始菜单）
                    if (ctrl && vk == VK_ESCAPE) return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose() => Stop();
    }
}
