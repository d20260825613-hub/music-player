using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

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
            app.DispatcherUnhandledException += OnUnhandledUiException;
            MainWindow w = new MainWindow(args);
            app.Run(w);
        }

        /// <summary>
        /// 兜住 UI 线程上任何没人接住的异常。
        ///
        /// AudioEngine.Send 把 MCI 的错误码一律转成 AudioException 抛出，设计上
        /// 假定调用方一定接住 —— 但键盘快捷键、拖动进度条、切换曲目这些事件
        /// 处理器里都没有 try。异常冒到消息循环就终止进程：一个损坏的、或者
        /// 只是格式不被 MCI 支持的音频文件，足以让整个播放器凭空消失。
        ///
        /// 这里说一句人话然后继续运行，而不是把窗口收掉。
        /// </summary>
        private static void OnUnhandledUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string reason = e.Exception == null ? "未知错误" : e.Exception.Message;
            try
            {
                MessageBox.Show(
                    "播放时出了点问题：\n\n" + reason + "\n\n程序会继续运行，换一首再试。",
                    "音乐播放器",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch
            {
                // 连提示都弹不出来时，也绝不能让异常再冒出去
            }
            e.Handled = true;
        }
    }
}
