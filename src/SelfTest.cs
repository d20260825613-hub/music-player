using System;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace MusicPlayer
{
    /// <summary>命令行自检：验证标签解析、时长探测、以及 MCI 播放链路是否真的能工作。</summary>
    public static class SelfTest
    {
        private static StringBuilder sb;

        private static void L(string s)
        {
            sb.AppendLine(s);
            try { Console.WriteLine(s); } catch { }
        }

        public static int Run(string[] args)
        {
            string file = args.Length > 1 ? args[1] : null;
            string logPath = args.Length > 2
                ? args[2]
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "musicplayer-selftest.log");

            sb = new StringBuilder();
            int code = 0;

            try
            {
                if (file == null || !File.Exists(file)) throw new Exception("找不到测试文件: " + file);

                L("== 文件 ==");
                L("path=" + file);
                L("size=" + new FileInfo(file).Length);

                Track t = new Track(file);
                TagReader.Read(t);
                int probe = DurationProbe.Get(file);
                L("== 标签/时长 ==");
                L("title=" + t.DisplayTitle);
                L("artist=" + t.DisplayArtist);
                L("album=" + t.Album);
                L("coverBytes=" + (t.Cover == null ? 0 : t.Cover.Length));
                L("probeDurationMs=" + probe);

                using (AudioEngine e = new AudioEngine())
                {
                    e.Open(file);
                    L("== 引擎 ==");
                    L("backend=" + e.Backend);
                    L("durationMs=" + e.DurationMs);
                    L("positionMs=" + e.PositionMs);
                    L("mode=" + e.Mode);

                    e.SetVolume(0.5);
                    e.Play();
                    Pump(600);
                    int p1 = e.PositionMs;
                    AudioMode m1 = e.Mode;
                    Pump(800);
                    int p2 = e.PositionMs;
                    L("== 播放 ==");
                    L("pos1=" + p1 + " pos2=" + p2 + " mode=" + m1);
                    L("advancing=" + (p2 > p1 + 100));

                    e.Pause();
                    Pump(120);
                    L("pausedMode=" + e.Mode);
                    int pausedPos = e.PositionMs;
                    Pump(400);
                    int pausedPos2 = e.PositionMs;
                    L("pausedStable=" + (Math.Abs(pausedPos2 - pausedPos) < 150) + " (" + pausedPos + " -> " + pausedPos2 + ")");

                    e.Seek(300);
                    Pump(60);
                    L("afterSeekPos=" + e.PositionMs);

                    e.Resume();
                    Pump(500);
                    L("resumedMode=" + e.Mode + " pos=" + e.PositionMs);

                    e.Stop();
                    Pump(60);
                    L("afterStopPos=" + e.PositionMs + " mode=" + e.Mode);
                    e.Close();
                    L("closed");
                }

                L("RESULT: PASS");
            }
            catch (Exception ex)
            {
                code = 1;
                L("RESULT: FAIL");
                L("error=" + ex.GetType().Name + ": " + ex.Message);
                L(ex.StackTrace);
            }

            try { File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false)); } catch { }
            try { Console.WriteLine("log=" + logPath); } catch { }
            return code;
        }

        private static void Pump(int ms)
        {
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer t = new DispatcherTimer(
                TimeSpan.FromMilliseconds(ms),
                DispatcherPriority.Background,
                delegate { frame.Continue = false; },
                Dispatcher.CurrentDispatcher);
            t.Start();
            Dispatcher.PushFrame(frame);
            t.Stop();
        }
    }
}
