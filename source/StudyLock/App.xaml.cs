using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace StudyLock
{
    /// <summary>
    /// 程序入口：单实例；1秒主循环驱动 Guardian；根据锁屏状态显示/隐藏锁屏窗、启停键盘钩子。
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private Guardian? _guardian;
        private QuestionBank? _bank;
        private LockWindow? _lockWin;
        private KeyboardHook? _hook;
        private AdminServer? _admin;
        private DispatcherTimer? _secondTimer;
        private DispatcherTimer? _killTimer;
        private EventWaitHandle? _summon;
        private bool _locked = true;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 编辑器模式：不安装、不锁屏、不接管防护，只提供本地题库维护界面。
            // 进入方式：--editor 参数，或把 exe 重命名为「题库编辑器.exe」双击。
            if (IsEditorRequested(e.Args))
            {
                base.OnStartup(e);
                // 密码框是当时唯一的窗口，关闭它会触发"最后窗口关闭即退出"，
                // 因此先改成手动关闭，等编辑器窗口显示出来后再恢复默认行为。
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                if (!BankEditorWindow.AskPassword(null))
                {
                    Shutdown();
                    return;
                }
                var editor = new BankEditorWindow();
                MainWindow = editor;
                editor.Show();
                ShutdownMode = ShutdownMode.OnLastWindowClose;
                return;
            }

            // 未安装则先自安装（复制到 Program Files、注册自启、开端口），然后退出当前进程
            try
            {
                if (Installer.EnsureInstalled())
                {
                    Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("安装失败：" + ex.Message, "StudyGoodAndGood");
                Shutdown();
                return;
            }

            // 单实例：防止孩子开多个副本干扰
            _mutex = new Mutex(true, "StudyGoodAndGood_SingleInstance_9F3A", out bool created);
            if (!created)
            {
                // 已经有实例在跑：这次启动就当成「一键召唤锁屏」，通知它弹窗后自己退出
                Installer.RequestShowLock();
                Shutdown();
                return;
            }
            base.OnStartup(e);

            _guardian = new Guardian();
            _guardian.LoadAll();
            _guardian.DebugMode = Guardian.DetectDebugMode();
            if (_guardian.DebugMode)
                _guardian.Log("unlock", "调试模式已开启：防护已降级，仅供开发测试");
            _bank = new QuestionBank();
            _bank.UseLocalBank = _guardian.Rules.UseLocalBank;
            _bank.WeakSubjects = _guardian.Rules.WeakSubjects.ToHashSet();
            _bank.ForceReload();
            _admin = new AdminServer(_guardian, _bank);
            _admin.Start();

            _hook = new KeyboardHook();
            CreateLockWindow();
            _guardian.LockStateChanged += OnLockStateChanged;

            // 一键召唤锁屏：第二个实例、手机管理页「立即锁屏」都通过这个命名事件通知本进程
            _summon = new EventWaitHandle(false, EventResetMode.AutoReset, Installer.ShowLockEventName);
            var summonThread = new Thread(() =>
            {
                while (_summon != null)
                {
                    try { _summon.WaitOne(); } catch { break; }
                    try { Dispatcher.Invoke(() => _guardian.ForceLockNow()); } catch { break; }
                }
            }) { IsBackground = true };
            summonThread.Start();

            // 题库自动在线更新：开启且距离上次检查超过3天时，后台静默更新
            if (_guardian.Rules.AutoUpdateBank && !string.IsNullOrWhiteSpace(_guardian.Rules.QuestionBankUrl))
            {
                _ = Task.Run(async () =>
                {
                    var msg = await _bank.UpdateFromUrlIfStaleAsync(_guardian.Rules.QuestionBankUrl, TimeSpan.FromDays(3));
                    if (msg != null) _guardian.Log("unlock", "题库自动更新：" + msg);
                });
            }

            // 主循环：每秒
            _secondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _secondTimer.Tick += (_, _) => _guardian.OnSecond();
            _secondTimer.Start();

            // 锁屏期间每2秒杀任务管理器（调试模式下不杀）
            _killTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _killTimer.Tick += (_, _) => { if (!_guardian.DebugMode) SystemGuard.KillTaskManagers(); };
            _killTimer.Start();

            // 关机/注销前保存状态，防止额度被"重启大法"重置
            SessionEnding += (_, _) => _guardian.SaveState();
            Exit += (_, _) =>
            {
                _guardian.SaveState();
                SystemGuard.Release();
                _hook?.Dispose();
                _admin?.Dispose();
                _summon?.Dispose();
            };

            // 初始判定一次
            OnLockStateChanged(_guardian.ShouldLock(_guardian.TrustedNow));
        }

        /// <summary>
        /// 判断是否以「题库编辑器」模式启动：
        /// 带 --editor 参数，或程序文件被重命名为包含"编辑"/"Editor"的名字。
        /// </summary>
        private static bool IsEditorRequested(string[] args)
        {
            if (args.Any(a => a.Trim().Equals("--editor", StringComparison.OrdinalIgnoreCase)))
                return true;
            var name = System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
            return name.Contains("编辑") || name.Contains("Editor", StringComparison.OrdinalIgnoreCase);
        }

        private void OnLockStateChanged(bool locked)
        {
            if (locked == _locked && _lockWin != null && locked == _lockWin.IsVisible) return;
            _locked = locked;
            Dispatcher.Invoke(() =>
            {
                if (locked)
                {
                    // 调试模式下 Alt+F4 会真正关掉窗口并置空引用，这里按需重建
                    if (_lockWin == null) CreateLockWindow();
                    if (_guardian!.DebugMode)
                    {
                        // 调试模式：只弹锁屏窗口，不启用防护（方便 Alt+F4 / 任务管理器调试）
                        _lockWin?.ShowLocked();
                    }
                    else
                    {
                        SystemGuard.Engage();
                        _hook?.Start();
                        _lockWin?.ShowLocked();
                    }
                }
                else
                {
                    SystemGuard.Release();
                    _hook?.Stop();
                    _lockWin?.HideUnlocked();
                }
            });
        }

        private void CreateLockWindow()
        {
            _lockWin = new LockWindow(_guardian!, _bank!, _admin!);
            // 窗口被关闭（调试模式 Alt+F4）后清空引用，下次召唤锁屏时重新创建
            _lockWin.Closed += (_, _) => _lockWin = null;
        }
    }
}
