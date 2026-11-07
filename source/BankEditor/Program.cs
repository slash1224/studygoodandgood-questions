using System;
using System.Windows;

namespace StudyLock
{
    /// <summary>
    /// 「题库编辑器」独立程序的入口：双击 → 家长密码 → 编辑窗口。
    /// 不安装、不锁屏、不注册自启动，只维护 %ProgramData%\StudyGoodAndGood\local_questions.json。
    /// </summary>
    public static class EditorProgram
    {
        [STAThread]
        public static void Main()
        {
            // 密码框是唯一窗口，关闭它会触发"最后窗口关闭即退出"，
            // 所以先手动关闭模式，等编辑器窗口显示后再恢复。
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            if (!BankEditorWindow.AskPassword(null))
            {
                app.Shutdown();
                return;
            }

            var w = new BankEditorWindow();
            app.MainWindow = w;
            w.Show();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            app.Run();
        }
    }
}
