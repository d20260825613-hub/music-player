using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace MusicPlayer
{
    public static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int pid);

        [STAThread]
        public static void Main(string[] args)
        {
            // 隐藏的自检模式，用于验证播放链路（不显示界面）
            if (args.Length > 0 && args[0] == "--selftest")
            {
                AttachConsole(-1);
                int code = SelfTest.Run(args);
                Environment.Exit(code);
                return;
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow w = new MainWindow(args);
            app.Run(w);
        }
    }
}
