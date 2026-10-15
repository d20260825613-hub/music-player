using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicPlayer
{
    /// <summary>把音量、播放模式、上次播放位置和整个播放列表存到 %APPDATA%，下次打开自动恢复。</summary>
    public static class PlaylistStore
    {
        private static string resolvedDir;

        private static string AppDataDir
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicPlayer");
            }
        }

        /// <summary>程序所在目录下的便携模式目录（%APPDATA% 不可写时使用）。</summary>
        private static string PortableDir
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MusicPlayerData"); }
        }

        private static string AppDataFile { get { return System.IO.Path.Combine(AppDataDir, "state.txt"); } }

        private static string PortableFile { get { return System.IO.Path.Combine(PortableDir, "state.txt"); } }

        /// <summary>优先用 %APPDATA%，不可写则退回到程序目录，避免在受限环境下丢配置。</summary>
        private static string Dir
        {
            get
            {
                if (resolvedDir == null)
                {
                    if (CanWrite(AppDataDir)) resolvedDir = AppDataDir;
                    else if (CanWrite(PortableDir)) resolvedDir = PortableDir;
                    else resolvedDir = AppDataDir;
                }
                return resolvedDir;
            }
        }

        private static string FilePath { get { return System.IO.Path.Combine(Dir, "state.txt"); } }

        private static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = System.IO.Path.Combine(dir, ".writetest");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public class State
        {
            public double Volume = 0.8;
            public string Mode = "LoopList";
            public int Index = -1;
            public List<string> Tracks = new List<string>();
        }

        public static void Save(double volume, string mode, int index, List<Track> tracks)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("version=1");
                sb.AppendLine("volume=" + volume.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("mode=" + mode);
                sb.AppendLine("index=" + index);
                sb.AppendLine("tracks:");
                for (int i = 0; i < tracks.Count; i++) sb.AppendLine(tracks[i].Path);
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        public static State Load()
        {
            State s = new State();
            try
            {
                string path = FilePath;
                if (!File.Exists(path) && File.Exists(PortableFile)) path = PortableFile;
                if (!File.Exists(path)) return s;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                bool inTracks = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (inTracks)
                    {
                        if (line.Trim().Length > 0) s.Tracks.Add(line);
                        continue;
                    }
                    if (line == "tracks:") { inTracks = true; continue; }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq);
                    string v = line.Substring(eq + 1);
                    if (k == "volume")
                    {
                        double d;
                        if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
                            s.Volume = Math.Max(0, Math.Min(1, d));
                    }
                    else if (k == "mode") s.Mode = v;
                    else if (k == "index")
                    {
                        int n;
                        if (int.TryParse(v, out n)) s.Index = n;
                    }
                }
            }
            catch { }
            return s;
        }
    }
}
