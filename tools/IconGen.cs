using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

/// <summary>生成 exe 图标（多尺寸 ICO）和窗口图标 PNG。编译期工具，不属于播放器本体。</summary>
class IconGen
{
    static void Main(string[] args)
    {
        string icoPath = args[0];
        string pngPath = args[1];

        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> pngs = new List<byte[]>();

        foreach (int s in sizes)
        {
            using (Bitmap bmp = Draw(s))
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }
        }

        // 窗口图标用 128px
        using (Bitmap bmp = Draw(128))
            bmp.Save(pngPath, ImageFormat.Png);

        using (FileStream fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(pngs[i].Length);
                w.Write(offset);
                offset += pngs[i].Length;
            }
            for (int i = 0; i < pngs.Count; i++) w.Write(pngs[i]);
        }

        Console.WriteLine("icon written: " + icoPath + " (" + new FileInfo(icoPath).Length + " bytes)");
    }

    static Bitmap Draw(int s)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float r = s * 0.23f;
            using (GraphicsPath path = RoundRect(new RectangleF(0, 0, s, s), r))
            using (LinearGradientBrush bg = new LinearGradientBrush(
                new RectangleF(0, 0, s, s),
                Color.FromArgb(255, 0x5B, 0x8C, 0xFF),
                Color.FromArgb(255, 0x8E, 0x5B, 0xFF),
                45f))
            {
                g.FillPath(bg, path);
            }

            float k = s / 64f;
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillEllipse(white, 7 * k, 39 * k, 21 * k, 17 * k);
                g.FillEllipse(white, 36 * k, 31 * k, 21 * k, 17 * k);
                g.FillRectangle(white, 23 * k, 10 * k, 5 * k, 34 * k);
                g.FillRectangle(white, 52 * k, 2 * k, 5 * k, 34 * k);
                PointF[] beam =
                {
                    new PointF(23 * k, 10 * k),
                    new PointF(57 * k, 2 * k),
                    new PointF(57 * k, 15 * k),
                    new PointF(23 * k, 23 * k)
                };
                g.FillPolygon(white, beam);
            }
        }
        return bmp;
    }

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
