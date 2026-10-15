using System;
using System.IO;

namespace MusicPlayer
{
    /// <summary>
    /// 快速估算音频时长：只读文件头（最多 512KB），不整文件扫描，
    /// 这样导入上千首歌也不会卡。MP3 优先用 Xing/VBRI 帧数，其次按 CBR 码率估算。
    /// </summary>
    public static class DurationProbe
    {
        private static readonly int[] BitrateV1L3 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
        private static readonly int[] BitrateV2L3 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };
        private static readonly int[] SampleRates = { 44100, 48000, 32000, 0 };

        public static int Get(string path)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".mp3") return Mp3(path);
                if (ext == ".wav") return Wav(path);
            }
            catch { }
            return 0;
        }

        // ---------------- MP3 ----------------

        private static int Mp3(string path)
        {
            long fileLen;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                fileLen = fs.Length;

            byte[] b = ReadPart(path, 0, 512 * 1024);
            if (b == null || b.Length < 4) return 0;

            int tagSize = Id3v2Size(b);
            int p = FindSync(b, tagSize, b.Length);
            if (p < 0) return 0;

            int bitrate, sampleRate, spf, channelMode, version, layer;
            if (!ParseHeader(b, p, out bitrate, out sampleRate, out spf, out channelMode, out version, out layer))
                return 0;

            // Xing / Info / VBRI：精确帧数
            int frameLen = FrameLength(bitrate, sampleRate, version, layer, (b[p + 2] >> 1) & 0x01);
            int sideInfo = (version == 1) ? ((channelMode == 3) ? 17 : 32) : ((channelMode == 3) ? 9 : 17);
            int xingPos = p + 4 + sideInfo;
            if (xingPos + 16 < b.Length)
            {
                string mark = Ascii(b, xingPos, 4);
                if (mark == "Xing" || mark == "Info")
                {
                    int flags = BEInt(b, xingPos + 4);
                    if ((flags & 0x1) != 0)
                    {
                        int frames = BEInt(b, xingPos + 8);
                        if (frames > 0) return (int)((long)frames * spf * 1000L / sampleRate);
                    }
                }
                string vbri = Ascii(b, p + 4 + 32, 4);
                if (vbri == "VBRI")
                {
                    int frames = BEInt(b, p + 4 + 32 + 14);
                    if (frames > 0) return (int)((long)frames * spf * 1000L / sampleRate);
                }
            }

            // CBR：按首帧码率估算
            if (bitrate > 0 && fileLen > tagSize && frameLen > 0)
            {
                long audioBytes = fileLen - tagSize;
                return (int)(audioBytes * 8L / bitrate);
            }
            return 0;
        }

        private static int Id3v2Size(byte[] b)
        {
            if (b.Length < 10) return 0;
            if (b[0] != 'I' || b[1] != 'D' || b[2] != '3') return 0;
            int size = ((b[6] & 0x7F) << 21) | ((b[7] & 0x7F) << 14) | ((b[8] & 0x7F) << 7) | (b[9] & 0x7F);
            int footer = ((b[5] & 0x10) != 0) ? 10 : 0;
            return 10 + size + footer;
        }

        private static int FindSync(byte[] b, int from, int end)
        {
            for (int i = Math.Max(0, from); i + 4 <= end; i++)
            {
                if (b[i] == 0xFF && (b[i + 1] & 0xE0) == 0xE0)
                {
                    int dummy1, dummy2, dummy3, dummy4, dummy5, dummy6;
                    if (ParseHeader(b, i, out dummy1, out dummy2, out dummy3, out dummy4, out dummy5, out dummy6))
                        return i;
                }
            }
            return -1;
        }

        private static bool ParseHeader(byte[] b, int p, out int bitrate, out int sampleRate, out int spf, out int channelMode, out int version, out int layer)
        {
            bitrate = 0; sampleRate = 0; spf = 0; channelMode = 0; version = 0; layer = 0;
            if (p + 4 > b.Length) return false;
            if (b[p] != 0xFF || (b[p + 1] & 0xE0) != 0xE0) return false;

            version = (b[p + 1] >> 3) & 0x03;   // 0=MPEG2.5 2=MPEG2 3=MPEG1
            layer = (b[p + 1] >> 1) & 0x03;     // 1=Layer3 2=Layer2 3=Layer1
            int brIdx = (b[p + 2] >> 4) & 0x0F;
            int srIdx = (b[p + 2] >> 2) & 0x03;
            int pad = (b[p + 2] >> 1) & 0x01;
            channelMode = (b[p + 3] >> 6) & 0x03;

            if (version == 1 || layer == 0 || brIdx == 0 || brIdx == 15 || srIdx == 3) return false;

            if (layer == 1)  // Layer III
            {
                bitrate = (version == 3) ? BitrateV1L3[brIdx] : BitrateV2L3[brIdx];
                spf = (version == 3) ? 1152 : 576;
            }
            else if (layer == 2)
            {
                bitrate = (version == 3) ? BitrateV1L3[brIdx] : BitrateV2L3[brIdx];
                spf = 1152;
            }
            else
            {
                bitrate = 0;
                spf = (version == 3) ? 1152 : 576;
            }
            if (layer != 1) bitrate = 0; // 本播放器只关心 Layer III 的估算

            sampleRate = SampleRates[srIdx];
            if (version == 2) sampleRate = sampleRate / 2;
            else if (version == 0) sampleRate = sampleRate / 4;

            if (bitrate == 0 || sampleRate == 0) return false;
            return true;
        }

        private static int FrameLength(int bitrate, int sampleRate, int version, int layer, int padding)
        {
            if (sampleRate <= 0 || bitrate <= 0) return 0;
            if (layer == 1) return 144 * bitrate * 1000 / sampleRate + padding;
            if (version == 3) return 144 * bitrate * 1000 / sampleRate + padding;
            return 72 * bitrate * 1000 / sampleRate + padding;
        }

        // ---------------- WAV ----------------

        private static int Wav(string path)
        {
            byte[] b = ReadPart(path, 0, 256 * 1024);
            if (b == null || b.Length < 44) return 0;
            if (Ascii(b, 0, 4) != "RIFF" || Ascii(b, 8, 4) != "WAVE") return 0;

            int byteRate = 0;
            long dataSize = 0;
            int p = 12;
            while (p + 8 <= b.Length)
            {
                string id = Ascii(b, p, 4);
                long size = LEInt(b, p + 4);
                if (id == "fmt " && p + 8 + 16 <= b.Length)
                    byteRate = LEInt(b, p + 8 + 8);
                else if (id == "data")
                {
                    dataSize = size;
                    if (dataSize <= 0) dataSize = b.Length - (p + 8);
                    break;
                }
                if (size <= 0) break;
                p += 8 + (int)size + ((int)size % 2);
            }
            if (byteRate <= 0 || dataSize <= 0) return 0;
            return (int)(dataSize * 1000L / byteRate);
        }

        // ---------------- 工具 ----------------

        private static byte[] ReadPart(string path, int offset, int count)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                int n = (int)Math.Min(count, fs.Length - offset);
                if (n <= 0) return null;
                byte[] b = new byte[n];
                int read = 0;
                while (read < n)
                {
                    int r = fs.Read(b, read, n - read);
                    if (r <= 0) break;
                    read += r;
                }
                return b;
            }
        }

        private static string Ascii(byte[] b, int o, int len)
        {
            if (o + len > b.Length) return "";
            char[] c = new char[len];
            for (int i = 0; i < len; i++) c[i] = (char)b[o + i];
            return new string(c);
        }

        private static int BEInt(byte[] b, int o)
        {
            if (o + 4 > b.Length) return 0;
            return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        }

        private static int LEInt(byte[] b, int o)
        {
            if (o + 4 > b.Length) return 0;
            return b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
        }
    }
}
