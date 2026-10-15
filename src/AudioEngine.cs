using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicPlayer
{
    public class AudioException : Exception
    {
        public AudioException(string msg) : base(msg) { }
    }

    public enum AudioMode { Stopped, Playing, Paused }

    /// <summary>
    /// 播放引擎。首选 Windows MCI（winmm）—— 对 MP3/WAV 支持最稳、延迟最低；
    /// 若某格式 MCI 打不开，自动回退到 WPF MediaPlayer（走 Media Foundation）。
    /// 两条链路都是系统自带，无需任何第三方库。
    /// </summary>
    public class AudioEngine : IDisposable
    {
        private const string Alias = "dshmp_track";

        private bool mciOpen;
        private WpfBackend wpf;
        private double volume = 0.8;
        private bool volumeFallback;

        // ---------------- MCI (winmm) ----------------

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int mciSendString(string cmd, StringBuilder ret, int len, IntPtr hwnd);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciGetErrorStringW")]
        private static extern bool mciGetErrorString(int err, StringBuilder buf, int len);

        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        private static string Send(string cmd)
        {
            StringBuilder sb = new StringBuilder(512);
            int rc = mciSendString(cmd, sb, sb.Capacity, IntPtr.Zero);
            if (rc != 0) throw new AudioException(ErrorText(rc));
            return sb.ToString();
        }

        private static string TrySend(string cmd)
        {
            try { return Send(cmd); }
            catch { return ""; }
        }

        private static string ErrorText(int rc)
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                if (mciGetErrorString(rc, sb, sb.Capacity)) return sb.ToString();
            }
            catch { }
            return "MCI 错误码 " + rc;
        }

        // ---------------- 状态 ----------------

        public bool IsOpen
        {
            get { return mciOpen || (wpf != null && wpf.IsOpen); }
        }

        public string Backend
        {
            get { return mciOpen ? "MCI" : (wpf != null ? "MediaPlayer" : "-"); }
        }

        public double Volume
        {
            get { return volume; }
            set { SetVolume(value); }
        }

        public int DurationMs
        {
            get
            {
                if (mciOpen)
                {
                    int v;
                    if (int.TryParse(TrySend("status " + Alias + " length"), out v) && v > 0) return v;
                    return 0;
                }
                if (wpf != null) return wpf.DurationMs;
                return 0;
            }
        }

        public int PositionMs
        {
            get
            {
                if (mciOpen)
                {
                    int v;
                    if (int.TryParse(TrySend("status " + Alias + " position"), out v) && v >= 0) return v;
                    return 0;
                }
                if (wpf != null) return wpf.PositionMs;
                return 0;
            }
        }

        public AudioMode Mode
        {
            get
            {
                if (mciOpen)
                {
                    string m = TrySend("status " + Alias + " mode").Trim().ToLowerInvariant();
                    if (m == "playing") return AudioMode.Playing;
                    if (m == "paused") return AudioMode.Paused;
                    return AudioMode.Stopped;
                }
                if (wpf != null) return wpf.Mode;
                return AudioMode.Stopped;
            }
        }

        // ---------------- 打开 / 控制 ----------------

        public void Open(string path)
        {
            Close();

            string mciError = null;
            try
            {
                OpenMci(path);
                mciOpen = true;
                ApplyVolume();
                return;
            }
            catch (Exception ex)
            {
                mciError = ex.Message;
                TrySend("close " + Alias);
                mciOpen = false;
            }

            try
            {
                wpf = new WpfBackend(path, volume);
            }
            catch (Exception ex)
            {
                wpf = null;
                throw new AudioException("无法播放这个文件：\n" + ex.Message + "\n\n(MCI: " + mciError + ")");
            }
        }

        private void OpenMci(string path)
        {
            string p = path.Replace("\"", "");
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            try
            {
                Send("open \"" + p + "\" alias " + Alias);
            }
            catch (AudioException)
            {
                string type = "mpegvideo";
                if (ext == ".wav") type = "waveaudio";
                else if (ext == ".mp3" || ext == ".m4a" || ext == ".aac" || ext == ".mp4" || ext == ".wma") type = "mpegvideo";
                Send("open \"" + p + "\" alias " + Alias + " type " + type);
            }
        }

        public void Play()
        {
            if (mciOpen) Send("play " + Alias);
            else if (wpf != null) wpf.Play();
        }

        public void Pause()
        {
            if (mciOpen)
            {
                if (Mode == AudioMode.Playing) Send("pause " + Alias);
            }
            else if (wpf != null) wpf.Pause();
        }

        public void Resume()
        {
            if (mciOpen)
            {
                string m = TrySend("status " + Alias + " mode").Trim().ToLowerInvariant();
                if (m == "paused") Send("resume " + Alias);
                else Send("play " + Alias);
            }
            else if (wpf != null) wpf.Play();
        }

        public void Stop()
        {
            if (mciOpen)
            {
                TrySend("stop " + Alias);
                TrySend("seek " + Alias + " to 0");
            }
            else if (wpf != null) wpf.Stop();
        }

        public void Seek(int ms)
        {
            if (ms < 0) ms = 0;
            if (mciOpen)
            {
                bool wasPlaying = Mode == AudioMode.Playing;
                Send("seek " + Alias + " to " + ms);
                if (wasPlaying) Send("play " + Alias);
            }
            else if (wpf != null) wpf.Seek(ms);
        }

        public void SetVolume(double v)
        {
            if (v < 0) v = 0;
            if (v > 1) v = 1;
            volume = v;
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (mciOpen)
            {
                if (!volumeFallback)
                {
                    try
                    {
                        Send("setaudio " + Alias + " volume to " + (int)Math.Round(volume * 1000));
                        return;
                    }
                    catch
                    {
                        volumeFallback = true;   // 该设备不支持 setaudio，改用系统波形音量
                    }
                }
                uint v = (uint)Math.Round(volume * 0xFFFF);
                waveOutSetVolume(IntPtr.Zero, v | (v << 16));
            }
            else if (wpf != null)
            {
                wpf.SetVolume(volume);
            }
        }

        public void Close()
        {
            if (mciOpen)
            {
                TrySend("stop " + Alias);
                TrySend("close " + Alias);
                mciOpen = false;
            }
            if (wpf != null)
            {
                try { wpf.Dispose(); }
                catch { }
                wpf = null;
            }
        }

        public void Dispose()
        {
            Close();
        }

        // ---------------- WPF MediaPlayer 回退后端 ----------------

        private class WpfBackend : IDisposable
        {
            private MediaPlayer mp;
            private AudioMode mode = AudioMode.Stopped;

            public bool IsOpen;
            public bool Failed;
            public string Error = "";

            public WpfBackend(string path, double vol)
            {
                mp = new MediaPlayer();
                mp.MediaOpened += delegate { IsOpen = true; };
                mp.MediaFailed += delegate(object s, ExceptionEventArgs e)
                {
                    Failed = true;
                    Error = (e.ErrorException == null) ? "未知错误" : e.ErrorException.Message;
                };
                mp.MediaEnded += delegate { mode = AudioMode.Stopped; };
                mp.Volume = vol;
                mp.Open(new Uri(path));

                Stopwatch sw = Stopwatch.StartNew();
                while (!IsOpen && !Failed && sw.ElapsedMilliseconds < 3000) Pump(40);
                if (!IsOpen) throw new AudioException(Failed ? Error : "打开超时");
            }

            public AudioMode Mode { get { return mode; } }

            public int DurationMs
            {
                get
                {
                    if (mp.NaturalDuration.HasTimeSpan) return (int)mp.NaturalDuration.TimeSpan.TotalMilliseconds;
                    return 0;
                }
            }

            public int PositionMs
            {
                get
                {
                    double ms = mp.Position.TotalMilliseconds;
                    return ms < 0 ? 0 : (int)ms;
                }
            }

            public void Play() { mp.Play(); mode = AudioMode.Playing; }

            public void Pause() { mp.Pause(); mode = AudioMode.Paused; }

            public void Stop() { mp.Stop(); mode = AudioMode.Stopped; }

            public void Seek(int ms) { mp.Position = TimeSpan.FromMilliseconds(ms); }

            public void SetVolume(double v) { mp.Volume = v; }

            public void Dispose()
            {
                try { mp.Stop(); } catch { }
                try { mp.Close(); } catch { }
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
}
