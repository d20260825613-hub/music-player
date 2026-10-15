using System;
using System.IO;
using System.Text;

namespace MusicPlayer
{
    /// <summary>
    /// 纯手工解析 ID3 标签：优先 ID3v2（含内嵌专辑封面 APIC），
    /// 标签缺失时回退到文件末尾的 ID3v1。不依赖任何第三方库。
    /// </summary>
    public static class TagReader
    {
        private static Encoding gbk;
        private static UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);

        private static Encoding Gbk
        {
            get
            {
                if (gbk == null)
                {
                    try { gbk = Encoding.GetEncoding(936); }
                    catch { gbk = Encoding.GetEncoding(28591); }
                }
                return gbk;
            }
        }

        public static void Read(Track t)
        {
            try { ReadV2(t); }
            catch { }
            try
            {
                if (t.Title.Length == 0 || t.Artist.Length == 0) ReadV1(t);
            }
            catch { }
        }

        // ---------------- ID3v2 ----------------

        private static void ReadV2(Track t)
        {
            byte[] buf = ReadHead(t.Path, 1024 * 1024 * 4); // 标签最多读 4MB（够放封面）
            if (buf == null || buf.Length < 10) return;
            if (buf[0] != 'I' || buf[1] != 'D' || buf[2] != '3') return;

            int ver = buf[3];
            int flags = buf[5];
            int size = SyncSafe(buf, 6);
            if (size <= 0) return;

            int available = Math.Min(size, buf.Length - 10);
            if (available <= 0) return;

            if (ver == 2) ParseFrames22(t, buf, 10, available);
            else ParseFrames(t, buf, 10, available, ver);
        }

        private static void ParseFrames(Track t, byte[] b, int start, int len, int ver)
        {
            int p = start;
            int end = start + len;
            while (p + 10 <= end)
            {
                if (b[p] == 0) break; // 进入 padding
                string id = Ascii(b, p, 4);
                int fsz = (ver >= 4) ? SyncSafe(b, p + 4) : BEInt(b, p + 4);
                if (fsz <= 0 || p + 10 + fsz > end) break;
                Apply(t, id, b, p + 10, fsz);
                p += 10 + fsz;
            }
        }

        private static void ParseFrames22(Track t, byte[] b, int start, int len)
        {
            int p = start;
            int end = start + len;
            while (p + 6 <= end)
            {
                if (b[p] == 0) break;
                string id = Ascii(b, p, 3);
                int fsz = (b[p + 3] << 16) | (b[p + 4] << 8) | b[p + 5];
                if (fsz <= 0 || p + 6 + fsz > end) break;
                Apply(t, id, b, p + 6, fsz);
                p += 6 + fsz;
            }
        }

        private static void Apply(Track t, string id, byte[] b, int o, int len)
        {
            switch (id)
            {
                case "TIT2": case "TT2":
                    if (t.Title.Length == 0) t.Title = Clean(DecodeText(b, o, len));
                    break;
                case "TPE1": case "TP1":
                    if (t.Artist.Length == 0) t.Artist = Clean(DecodeText(b, o, len));
                    break;
                case "TALB": case "TAL":
                    if (t.Album.Length == 0) t.Album = Clean(DecodeText(b, o, len));
                    break;
                case "APIC": case "PIC":
                    if (t.Cover == null) t.Cover = ParsePicture(id == "PIC", b, o, len);
                    break;
            }
        }

        private static string DecodeText(byte[] b, int o, int len)
        {
            if (len <= 1) return "";
            byte enc = b[o];
            byte[] s = new byte[len - 1];
            Array.Copy(b, o + 1, s, 0, len - 1);
            return DecodeBytes(s, enc);
        }

        private static string DecodeBytes(byte[] s, byte enc)
        {
            if (enc == 0) return SmartAnsi(s);
            if (enc == 1)
            {
                // UTF-16LE，可能带 BOM
                if (s.Length >= 2 && s[0] == 0xFF && s[1] == 0xFE)
                {
                    byte[] x = new byte[s.Length - 2];
                    Array.Copy(s, 2, x, 0, x.Length);
                    return Encoding.Unicode.GetString(x);
                }
                if (s.Length >= 2 && s[0] == 0xFE && s[1] == 0xFF)
                {
                    byte[] x = new byte[s.Length - 2];
                    Array.Copy(s, 2, x, 0, x.Length);
                    return Encoding.BigEndianUnicode.GetString(x);
                }
                return Encoding.Unicode.GetString(s);
            }
            if (enc == 2) return Encoding.BigEndianUnicode.GetString(s);
            try { return strictUtf8.GetString(s); }
            catch { return Gbk.GetString(s); }
        }

        /// <summary>ID3v2 里标称 ISO-8859-1 的中文标签实际多为 GBK，这里智能判断。</summary>
        private static string SmartAnsi(byte[] s)
        {
            bool ascii = true;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= 0x80) { ascii = false; break; }
            }
            if (ascii) return Encoding.ASCII.GetString(s);
            try { return strictUtf8.GetString(s); }
            catch { }
            try { return Gbk.GetString(s); }
            catch { return Encoding.GetEncoding(28591).GetString(s); }
        }

        /// <summary>APIC/PIC: 编码 + MIME + 图片类型 + 描述 + 图片数据。</summary>
        private static byte[] ParsePicture(bool v22, byte[] b, int o, int len)
        {
            int end = o + len;
            if (o >= end) return null;
            byte enc = b[o];
            int p = o + 1;

            if (v22) p += 3;            // 3 字节图片格式
            else { while (p < end && b[p] != 0) p++; p++; }  // MIME 字符串，0 结尾
            if (p >= end) return null;

            p++;                        // 图片类型 1 字节
            if (p >= end) return null;

            if (enc == 1 || enc == 2)
            {
                while (p + 1 < end && !(b[p] == 0 && b[p + 1] == 0)) p += 2;
                p += 2;
            }
            else
            {
                while (p < end && b[p] != 0) p++;
                p++;
            }
            if (p >= end) return null;

            int imgLen = end - p;
            if (imgLen < 32 || imgLen > 12 * 1024 * 1024) return null;
            byte[] img = new byte[imgLen];
            Array.Copy(b, p, img, 0, imgLen);
            return img;
        }

        // ---------------- ID3v1 ----------------

        private static void ReadV1(Track t)
        {
            using (FileStream fs = new FileStream(t.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 128) return;

                // 有 ID3v2 时，v1 仍在文件尾，直接读最后 128 字节
                fs.Seek(-128, SeekOrigin.End);
                byte[] b = new byte[128];
                if (fs.Read(b, 0, 128) != 128) return;
                if (b[0] != 'T' || b[1] != 'A' || b[2] != 'G') return;

                string title = SmartAnsi(Slice(b, 3, 30)).Trim();
                string artist = SmartAnsi(Slice(b, 33, 30)).Trim();
                string album = SmartAnsi(Slice(b, 63, 30)).Trim();

                if (t.Title.Length == 0) t.Title = Clean(title);
                if (t.Artist.Length == 0) t.Artist = Clean(artist);
                if (t.Album.Length == 0) t.Album = Clean(album);
            }
        }

        // ---------------- 工具 ----------------

        private static byte[] ReadHead(string path, int max)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long n = Math.Min(fs.Length, max);
                byte[] b = new byte[n];
                int read = 0;
                while (read < b.Length)
                {
                    int r = fs.Read(b, read, b.Length - read);
                    if (r <= 0) break;
                    read += r;
                }
                if (read == b.Length) return b;
                byte[] c = new byte[read];
                Array.Copy(b, c, read);
                return c;
            }
        }

        private static byte[] Slice(byte[] b, int o, int len)
        {
            byte[] r = new byte[len];
            Array.Copy(b, o, r, 0, len);
            return r;
        }

        private static string Ascii(byte[] b, int o, int len)
        {
            char[] c = new char[len];
            for (int i = 0; i < len; i++) c[i] = (char)b[o + i];
            return new string(c);
        }

        private static int SyncSafe(byte[] b, int o)
        {
            return ((b[o] & 0x7F) << 21) | ((b[o + 1] & 0x7F) << 14) | ((b[o + 2] & 0x7F) << 7) | (b[o + 3] & 0x7F);
        }

        private static int BEInt(byte[] b, int o)
        {
            uint v = ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | (uint)b[o + 3];
            if (v > int.MaxValue) return 0;
            return (int)v;
        }

        private static string Clean(string s)
        {
            if (s == null) return "";
            int z = s.IndexOf('\0');
            if (z >= 0) s = s.Substring(0, z);
            return s.Trim();
        }
    }
}
