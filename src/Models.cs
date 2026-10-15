using System;
using System.IO;

namespace MusicPlayer
{
    public static class Fmt
    {
        public static string Time(int ms)
        {
            if (ms < 0) ms = 0;
            int total = ms / 1000;
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            if (h > 0) return string.Format("{0}:{1:00}:{2:00}", h, m, s);
            return string.Format("{0}:{1:00}", m, s);
        }
    }

    /// <summary>一首歌的信息（标签 + 时长 + 内嵌封面）。</summary>
    public class Track
    {
        public string Path = "";
        public string Title = "";    // 来自 ID3 标签
        public string Artist = "";
        public string Album = "";
        public int DurationMs;       // 0 表示未知
        public byte[] Cover;         // 内嵌封面原始字节，可为 null

        private string fallbackTitle = "";
        private string fallbackArtist = "";

        public Track(string path)
        {
            Path = path;
            GuessFromFileName();
        }

        /// <summary>没有标签时，从文件名猜测（常见 "艺术家 - 标题.mp3"）。</summary>
        private void GuessFromFileName()
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(Path);
            int dash = name.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0)
            {
                string a = name.Substring(0, dash).Trim();
                string b = name.Substring(dash + 3).Trim();
                if (a.Length > 0 && b.Length > 0)
                {
                    fallbackArtist = a;
                    fallbackTitle = b;
                }
            }
        }

        public string FileName { get { return System.IO.Path.GetFileName(Path); } }

        public string DisplayTitle
        {
            get
            {
                if (Title.Length > 0) return Title;
                if (fallbackTitle.Length > 0) return fallbackTitle;
                return System.IO.Path.GetFileNameWithoutExtension(Path);
            }
        }

        public string DisplayArtist
        {
            get
            {
                if (Artist.Length > 0) return Artist;
                if (fallbackArtist.Length > 0) return fallbackArtist;
                return "未知艺术家";
            }
        }

        public string DisplayAlbum { get { return Album; } }

        public string DurationText { get { return DurationMs > 0 ? Fmt.Time(DurationMs) : "--:--"; } }

        public bool Matches(string q)
        {
            if (q == null || q.Length == 0) return true;
            q = q.ToLowerInvariant();
            if (DisplayTitle.ToLowerInvariant().Contains(q)) return true;
            if (DisplayArtist.ToLowerInvariant().Contains(q)) return true;
            if (Album.ToLowerInvariant().Contains(q)) return true;
            if (FileName.ToLowerInvariant().Contains(q)) return true;
            return false;
        }
    }
}
