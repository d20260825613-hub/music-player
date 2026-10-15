using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using IOPath = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;

namespace MusicPlayer
{
    public class MainWindow : Window
    {
        // ---------------- 配色 ----------------
        static readonly SolidColorBrush CBg = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x15));
        static readonly SolidColorBrush CPanel = new SolidColorBrush(Color.FromRgb(0x16, 0x1A, 0x22));
        static readonly SolidColorBrush CPanel2 = new SolidColorBrush(Color.FromRgb(0x1D, 0x22, 0x2C));
        static readonly SolidColorBrush CHover = new SolidColorBrush(Color.FromRgb(0x23, 0x2A, 0x38));
        static readonly SolidColorBrush CLine = new SolidColorBrush(Color.FromRgb(0x2C, 0x34, 0x44));
        static readonly SolidColorBrush CText = new SolidColorBrush(Color.FromRgb(0xE9, 0xED, 0xF5));
        static readonly SolidColorBrush CDim = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0xA8));
        static readonly SolidColorBrush CDim2 = new SolidColorBrush(Color.FromRgb(0x5E, 0x67, 0x79));
        static readonly SolidColorBrush CAccent = new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF));
        static readonly SolidColorBrush CRowCur = new SolidColorBrush(Color.FromRgb(0x1A, 0x24, 0x38));
        static readonly LinearGradientBrush CGrad = MakeGrad();

        static LinearGradientBrush MakeGrad()
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(1, 1);
            g.GradientStops.Add(new GradientStop(Color.FromRgb(0x5B, 0x8C, 0xFF), 0));
            g.GradientStops.Add(new GradientStop(Color.FromRgb(0x8E, 0x5B, 0xFF), 1));
            return g;
        }

        static readonly string[] AudioExt = { ".mp3", ".wav", ".m4a", ".aac", ".wma", ".flac", ".ogg", ".opus", ".mp4", ".aif", ".aiff" };

        enum PlayMode { Sequential, LoopList, LoopOne, Shuffle }

        // ---------------- 状态 ----------------
        readonly AudioEngine engine = new AudioEngine();
        readonly List<Track> tracks = new List<Track>();
        readonly Random rnd = new Random();

        int currentIndex = -1;
        int selectedIndex = -1;
        PlayMode mode = PlayMode.LoopList;
        bool playing;
        double volume = 0.8;
        bool mute;

        DispatcherTimer timer;

        // ---------------- 控件引用 ----------------
        StackPanel listPanel;
        TextBox searchBox;
        TextBlock searchHint;
        TextBlock listCount;
        TextBlock statusText;
        Border coverBox;
        UIElement coverPlaceholder;
        TextBlock npTitle, npArtist, npAlbum;
        TextBlock curTime, totalTime, volLabel;
        Border[] modeSegments;
        ShapePath playIcon;
        Border playButton;
        Border currentRow;
        Rectangle[] eqBars;
        Bar seekBar, volBar;

        public MainWindow(string[] args)
        {
            Title = "音乐播放器";
            Width = 1060;
            Height = 700;
            MinWidth = 900;
            MinHeight = 580;
            Background = CBg;
            Foreground = CText;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI, Arial");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            AllowDrop = true;
            UseLayoutRounding = true;

            DragOver += OnDragOverEvent;
            Drop += OnDropEvent;
            PreviewKeyDown += OnKey;
            Closing += OnClosing;
            SourceInitialized += delegate { ApplyDarkTitleBar(); };

            try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/appicon.png")); }
            catch { }

            Content = BuildRoot();

            LoadState();

            if (args != null && args.Length > 0)
            {
                List<string> files = new List<string>();
                for (int i = 0; i < args.Length; i++)
                    if (File.Exists(args[i])) files.Add(args[i]);
                if (files.Count > 0) AddPaths(files, false);
            }

            timer = new DispatcherTimer(DispatcherPriority.Background);
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += OnTick;
            timer.Start();

            searchBox.Focus();
        }

        // ================= 界面搭建 =================

        UIElement BuildRoot()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            UIElement header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            UIElement body = BuildBody();
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            UIElement player = BuildPlayerBar();
            Grid.SetRow(player, 2);
            root.Children.Add(player);

            return root;
        }

        UIElement BuildHeader()
        {
            Border b = new Border { Height = 66, Padding = new Thickness(22, 0, 22, 0) };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Viewbox logo = new Viewbox { Width = 30, Height = 30, Child = NoteCanvas(Brushes.White) };
            brand.Children.Add(new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(10),
                Background = CGrad,
                Child = logo
            });
            brand.Children.Add(new TextBlock
            {
                Text = "音乐播放器",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = CText,
                Margin = new Thickness(11, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(brand, 0);
            g.Children.Add(brand);

            StackPanel btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btns.Children.Add(MakeButton("＋  添加音乐", true, delegate { ImportFiles(); }));
            btns.Children.Add(MakeButton("添加文件夹", false, delegate { ImportFolder(); }));
            btns.Children.Add(MakeButton("清空列表", false, delegate { ClearAll(); }));
            Grid.SetColumn(btns, 1);
            g.Children.Add(btns);

            b.Child = g;
            return b;
        }

        UIElement BuildBody()
        {
            Grid body = new Grid { Margin = new Thickness(18, 0, 18, 4) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(316) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            UIElement left = BuildLeftCard();
            Grid.SetColumn(left, 0);
            body.Children.Add(left);

            UIElement right = BuildListCard();
            Grid.SetColumn(right, 1);
            body.Children.Add(right);

            return body;
        }

        UIElement BuildLeftCard()
        {
            Border card = new Border
            {
                Background = CPanel,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20, 22, 20, 20)
            };

            StackPanel sp = new StackPanel();

            coverPlaceholder = new Grid
            {
                Width = 150,
                Height = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.28,
                Children = { new Viewbox { Child = NoteCanvas(CText), Width = 110, Height = 110 } }
            };

            coverBox = new Border
            {
                Width = 252,
                Height = 252,
                CornerRadius = new CornerRadius(14),
                Background = CPanel2,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = coverPlaceholder
            };
            sp.Children.Add(coverBox);

            npTitle = new TextBlock
            {
                Text = "未在播放",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = CText,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 254,
                Margin = new Thickness(0, 20, 0, 0)
            };
            sp.Children.Add(npTitle);

            npArtist = new TextBlock
            {
                Text = "导入歌曲后开始播放",
                FontSize = 13,
                Foreground = CDim,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 254,
                Margin = new Thickness(0, 8, 0, 0)
            };
            sp.Children.Add(npArtist);

            npAlbum = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = CDim2,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 254,
                Margin = new Thickness(0, 4, 0, 0)
            };
            sp.Children.Add(npAlbum);

            sp.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = CLine,
                Margin = new Thickness(0, 20, 0, 16)
            });

            sp.Children.Add(new TextBlock
            {
                Text = "把 MP3 文件或整个文件夹拖进窗口即可导入\n空格播放 / 暂停　←→ 快退快进　↑↓ 音量　R 切换模式",
                FontSize = 11.5,
                Foreground = CDim2,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                LineHeight = 20
            });

            card.Child = sp;
            return card;
        }

        UIElement BuildListCard()
        {
            Border card = new Border
            {
                Background = CPanel,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16, 14, 10, 10),
                Margin = new Thickness(14, 0, 0, 0)
            };

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 工具栏
            Grid bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid searchWrap = new Grid { Height = 34, VerticalAlignment = VerticalAlignment.Center };
            searchBox = new TextBox
            {
                Background = CPanel2,
                Foreground = CText,
                BorderThickness = new Thickness(0),
                CaretBrush = CAccent,
                FontSize = 13,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            searchBox.TextChanged += delegate { searchHint.Visibility = searchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; RebuildList(); };
            searchWrap.Children.Add(searchBox);
            searchHint = new TextBlock
            {
                Text = "搜索歌曲 / 歌手",
                Foreground = CDim2,
                FontSize = 13,
                Margin = new Thickness(13, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchWrap.Children.Add(searchHint);
            Grid.SetColumn(searchWrap, 0);
            bar.Children.Add(searchWrap);

            statusText = new TextBlock
            {
                Foreground = CDim,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(statusText, 1);
            bar.Children.Add(statusText);

            listCount = new TextBlock
            {
                Foreground = CDim,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(listCount, 2);
            bar.Children.Add(listCount);

            Grid.SetRow(bar, 0);
            g.Children.Add(bar);

            listPanel = new StackPanel { Margin = new Thickness(0, 8, 4, 0) };
            ScrollViewer sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = listPanel,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(sv, 1);
            g.Children.Add(sv);

            card.Child = g;
            return card;
        }

        UIElement BuildPlayerBar()
        {
            Border card = new Border
            {
                Background = CPanel,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20, 12, 20, 14),
                Margin = new Thickness(18, 8, 18, 16)
            };

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 进度条行
            Grid row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            curTime = new TextBlock { Text = "0:00", Foreground = CDim, FontSize = 12, Width = 46, TextAlignment = TextAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            totalTime = new TextBlock { Text = "--:--", Foreground = CDim, FontSize = 12, Width = 46, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

            seekBar = new Bar(6, CGrad);
            seekBar.Changed += delegate(double ratio, bool committed) { OnSeek(ratio, committed); };

            row1.Children.Add(curTime);
            Grid.SetColumn(seekBar.Root, 1);
            row1.Children.Add(seekBar.Root);
            Grid.SetColumn(totalTime, 2);
            row1.Children.Add(totalTime);
            Grid.SetRow(row1, 0);
            g.Children.Add(row1);

            // 控制行
            Grid row2 = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 左：播放模式 —— 四挡平铺，单击直达（循环 / 随机 一眼可见，不用轮流点）
            Border modeBox = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = CPanel2,
                Padding = new Thickness(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            StackPanel modeRow = new StackPanel { Orientation = Orientation.Horizontal };
            string[] modeLabels = { "顺序", "列表循环", "单曲循环", "随机" };
            string[] modeTips =
            {
                "顺序播放：播完最后一首就停止",
                "列表循环：播完最后一首回到第一首（快捷键 R）",
                "单曲循环：一直重复当前这一首（快捷键 R）",
                "随机播放：每首结束后随机挑下一首（快捷键 R）"
            };
            modeSegments = new Border[4];
            for (int i = 0; i < 4; i++)
            {
                int seg = i;
                Border cell = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = modeTips[i],
                    Child = new TextBlock
                    {
                        Text = modeLabels[i],
                        FontSize = 12.5,
                        Foreground = CDim,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                cell.MouseEnter += delegate { if (!IsActiveMode(seg)) cell.Background = CHover; };
                cell.MouseLeave += delegate { if (!IsActiveMode(seg)) cell.Background = Brushes.Transparent; };
                cell.MouseLeftButtonUp += delegate { SetMode(seg); };
                modeSegments[i] = cell;
                modeRow.Children.Add(cell);
            }
            modeBox.Child = modeRow;
            Grid.SetColumn(modeBox, 0);
            row2.Children.Add(modeBox);

            // 中：上一首 / 播放 / 下一首 / 停止
            StackPanel mid = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(MakeIconButton(Glyph("M 3,2 L 5,2 L 5,14 L 3,14 Z M 14,2 L 14,14 L 5,8 Z"), 36, 15, delegate { Prev(); }, "上一首"));
            mid.Children.Add(MakeIconButton(Glyph("M 4,4 L 12,4 L 12,12 L 4,12 Z"), 36, 13, delegate { StopPlayback(); }, "停止"));

            playIcon = Glyph("M 4,2 L 14,8 L 4,14 Z");
            playButton = MakeIconButton(playIcon, 54, 20, delegate { TogglePlay(); }, "播放 / 暂停 (空格)");
            playButton.Background = CGrad;
            playButton.Margin = new Thickness(12, 0, 12, 0);
            mid.Children.Add(playButton);

            mid.Children.Add(MakeIconButton(Glyph("M 11,2 L 13,2 L 13,14 L 11,14 Z M 2,2 L 11,8 L 2,14 Z"), 36, 15, delegate { Next(false); }, "下一首"));
            Grid.SetColumn(mid, 1);
            row2.Children.Add(mid);

            // 右：音量
            StackPanel rightBox = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            rightBox.Children.Add(new TextBlock { Text = "音量", Foreground = CDim2, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            volBar = new Bar(5, CAccent);
            volBar.Root.Width = 104;
            volBar.Changed += delegate(double ratio, bool committed)
            {
                volume = ratio;
                if (ratio > 0) mute = false;
                ApplyVolume();
                if (committed) SaveState();
            };
            rightBox.Children.Add(volBar.Root);
            volLabel = new TextBlock { Text = "80%", Foreground = CDim, FontSize = 12, Width = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            rightBox.Children.Add(volLabel);
            Grid.SetColumn(rightBox, 2);
            row2.Children.Add(rightBox);

            Grid.SetRow(row2, 1);
            g.Children.Add(row2);

            card.Child = g;
            return card;
        }

        // ================= 小组件工厂 =================

        static Canvas NoteCanvas(Brush fill)
        {
            Canvas c = new Canvas { Width = 64, Height = 64 };
            Ellipse h1 = new Ellipse { Width = 21, Height = 17, Fill = fill };
            Canvas.SetLeft(h1, 7); Canvas.SetTop(h1, 39);
            Ellipse h2 = new Ellipse { Width = 21, Height = 17, Fill = fill };
            Canvas.SetLeft(h2, 36); Canvas.SetTop(h2, 31);
            Rectangle s1 = new Rectangle { Width = 5, Height = 34, Fill = fill };
            Canvas.SetLeft(s1, 23); Canvas.SetTop(s1, 10);
            Rectangle s2 = new Rectangle { Width = 5, Height = 34, Fill = fill };
            Canvas.SetLeft(s2, 52); Canvas.SetTop(s2, 2);
            Polygon beam = new Polygon
            {
                Fill = fill,
                Points = new PointCollection { new Point(23, 10), new Point(57, 2), new Point(57, 15), new Point(23, 23) }
            };
            c.Children.Add(h1); c.Children.Add(h2); c.Children.Add(s1); c.Children.Add(s2); c.Children.Add(beam);
            return c;
        }

        static ShapePath Glyph(string data)
        {
            ShapePath p = new ShapePath();
            p.Data = Geometry.Parse(data);
            p.Fill = Brushes.White;
            p.Stretch = Stretch.Uniform;
            p.HorizontalAlignment = HorizontalAlignment.Center;
            p.VerticalAlignment = VerticalAlignment.Center;
            return p;
        }

        Border MakeButton(string text, bool primary, Action onClick)
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(8, 0, 0, 0),
                Background = primary ? (Brush)CGrad : CPanel2,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = primary ? Brushes.White : CText,
                    FontSize = 13,
                    FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal
                }
            };
            b.MouseEnter += delegate { b.Opacity = primary ? 0.88 : 1.0; if (!primary) b.Background = CHover; };
            b.MouseLeave += delegate { b.Opacity = 1.0; if (!primary) b.Background = CPanel2; };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        Border MakeIconButton(ShapePath icon, double size, double iconSize, Action onClick, string tip)
        {
            icon.Width = iconSize;
            icon.Height = iconSize;
            Border b = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = CPanel2,
                Cursor = Cursors.Hand,
                ToolTip = tip,
                Child = icon
            };
            b.MouseEnter += delegate { if (b.Background != CGrad) b.Background = CHover; };
            b.MouseLeave += delegate { if (b.Background != CGrad) b.Background = CPanel2; };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        UIElement EmptyHint(string text)
        {
            return new Border
            {
                Padding = new Thickness(20, 60, 20, 20),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = CDim2,
                    FontSize = 13.5,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    LineHeight = 24
                }
            };
        }

        // ================= 播放列表 =================

        void RebuildList()
        {
            if (listPanel == null) return;
            listPanel.Children.Clear();
            currentRow = null;
            eqBars = null;

            string q = searchBox == null ? "" : searchBox.Text.Trim();
            int shown = 0;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (!tracks[i].Matches(q)) continue;
                listPanel.Children.Add(MakeRow(i));
                shown++;
            }

            if (tracks.Count == 0)
            {
                listPanel.Children.Add(EmptyHint("还没有歌曲\n\n点击右上角「＋ 添加音乐」，\n或把 MP3 文件、文件夹直接拖进本窗口"));
            }
            else if (shown == 0)
            {
                listPanel.Children.Add(EmptyHint("没有找到匹配的歌曲"));
            }

            if (listCount != null)
            {
                listCount.Text = q.Length > 0
                    ? string.Format("匹配 {0} / {1} 首", shown, tracks.Count)
                    : string.Format("共 {0} 首", tracks.Count);
            }

            if (currentRow != null) currentRow.BringIntoView();
        }

        UIElement MakeRow(int i)
        {
            Track t = tracks[i];
            bool isCur = (i == currentIndex);
            bool isSel = (i == selectedIndex);

            Border row = new Border
            {
                Height = 48,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 2, 0, 2),
                Background = isCur ? (Brush)CRowCur : (isSel ? CHover : Brushes.Transparent),
                Cursor = Cursors.Hand
            };
            if (isCur) currentRow = row;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });

            if (isCur)
            {
                StackPanel eq = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Height = 16 };
                Rectangle[] bars = new Rectangle[3];
                int[] h = { 7, 13, 9 };
                for (int k = 0; k < 3; k++)
                {
                    bars[k] = new Rectangle
                    {
                        Width = 3,
                        Height = h[k],
                        RadiusX = 1.5,
                        RadiusY = 1.5,
                        Fill = CAccent,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 2, 0)
                    };
                    eq.Children.Add(bars[k]);
                }
                eqBars = bars;
                Grid.SetColumn(eq, 0);
                g.Children.Add(eq);
            }
            else
            {
                TextBlock num = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    Foreground = CDim2,
                    FontSize = 12.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(num, 0);
                g.Children.Add(num);
            }

            TextBlock title = new TextBlock
            {
                Text = t.DisplayTitle,
                Foreground = isCur ? CAccent : CText,
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 12, 0),
                ToolTip = t.Path
            };
            Grid.SetColumn(title, 1);
            g.Children.Add(title);

            TextBlock artist = new TextBlock
            {
                Text = t.DisplayArtist,
                Foreground = CDim,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(artist, 2);
            g.Children.Add(artist);

            TextBlock dur = new TextBlock
            {
                Text = t.DurationText,
                Foreground = CDim,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(dur, 3);
            g.Children.Add(dur);

            row.Child = g;

            int idx = i;
            row.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                selectedIndex = idx;
                if (e.ClickCount >= 2) PlayIndex(idx);
                else RebuildList();
            };
            row.MouseRightButtonUp += delegate
            {
                selectedIndex = idx;
                RebuildList();
            };
            row.ContextMenu = MakeRowMenu(idx);

            return row;
        }

        ContextMenu MakeRowMenu(int i)
        {
            ContextMenu m = new ContextMenu
            {
                Background = CPanel2,
                Foreground = CText,
                BorderBrush = CLine,
                BorderThickness = new Thickness(1)
            };
            m.Items.Add(MakeMenuItem("播放", delegate { PlayIndex(i); }));
            m.Items.Add(MakeMenuItem("从列表中移除", delegate { RemoveAt(i); }));
            m.Items.Add(MakeMenuItem("在资源管理器中显示", delegate { Reveal(pathOf(i)); }));
            return m;
        }

        static MenuItem MakeMenuItem(string text, Action act)
        {
            MenuItem mi = new MenuItem { Header = text, Foreground = CText, Background = CPanel2, Padding = new Thickness(12, 6, 18, 6) };
            mi.Click += delegate { act(); };
            return mi;
        }

        string pathOf(int i)
        {
            if (i < 0 || i >= tracks.Count) return "";
            return tracks[i].Path;
        }

        // ================= 播放控制 =================

        void TogglePlay()
        {
            if (tracks.Count == 0) { ImportFiles(); return; }
            if (!engine.IsOpen)
            {
                PlayIndex(currentIndex >= 0 && currentIndex < tracks.Count ? currentIndex : 0);
                return;
            }
            if (playing)
            {
                engine.Pause();
                playing = false;
            }
            else
            {
                engine.Resume();
                playing = true;
            }
            UpdatePlayIcon();
        }

        void PlayIndex(int i)
        {
            PlayIndex(i, false);
        }

        /// <summary>silent=true 用于自动切歌：失败时只在状态栏提示，不弹窗打断。</summary>
        void PlayIndex(int i, bool silent)
        {
            if (i < 0 || i >= tracks.Count) return;
            Track t = tracks[i];
            try
            {
                engine.Open(t.Path);
            }
            catch (Exception ex)
            {
                playing = false;
                UpdatePlayIcon();
                SetStatus("播放失败：" + t.DisplayTitle + "（" + FirstLine(ex.Message) + "）");
                if (!silent) MessageBox.Show(this, ex.Message, "无法播放", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            engine.SetVolume(mute ? 0 : volume);
            engine.Play();
            playing = true;
            currentIndex = i;
            selectedIndex = i;
            int dur = engine.DurationMs;
            if (dur > 0) t.DurationMs = dur;
            UpdatePlayIcon();
            UpdateNowPlaying();
            RebuildList();
            SaveState();
        }

        void StopPlayback()
        {
            if (!engine.IsOpen) return;
            engine.Stop();
            playing = false;
            UpdatePlayIcon();
            seekBar.SetValue(0);
            curTime.Text = "0:00";
        }

        void Next(bool auto)
        {
            if (tracks.Count == 0) return;
            if (mode == PlayMode.LoopOne && auto && currentIndex >= 0)
            {
                PlayIndex(currentIndex, true);
                return;
            }
            int n;
            if (mode == PlayMode.Shuffle) n = RandomIndex();
            else
            {
                n = currentIndex + 1;
                if (n >= tracks.Count)
                {
                    if (mode == PlayMode.Sequential && auto)
                    {
                        StopPlayback();
                        SetStatus("播放列表已结束");
                        return;
                    }
                    n = 0;
                }
            }
            PlayIndex(n, auto);
        }

        void Prev()
        {
            if (tracks.Count == 0) return;
            if (engine.IsOpen && engine.PositionMs > 3000)
            {
                engine.Seek(0);
                return;
            }
            int n = currentIndex - 1;
            if (n < 0) n = tracks.Count - 1;
            PlayIndex(n);
        }

        int RandomIndex()
        {
            if (tracks.Count <= 1) return 0;
            int n = currentIndex;
            for (int k = 0; k < 60 && n == currentIndex; k++) n = rnd.Next(tracks.Count);
            return n;
        }

        void SeekRelative(int deltaMs)
        {
            if (!engine.IsOpen) return;
            int dur = engine.DurationMs;
            int pos = engine.PositionMs + deltaMs;
            if (pos < 0) pos = 0;
            if (dur > 0 && pos > dur - 500) pos = dur - 500;
            if (pos < 0) pos = 0;
            engine.Seek(pos);
            seekBar.SetValue(dur > 0 ? (double)pos / dur : 0);
            curTime.Text = Fmt.Time(pos);
        }

        void OnSeek(double ratio, bool committed)
        {
            int dur = engine.IsOpen ? engine.DurationMs : 0;
            if (dur <= 0) return;
            int pos = (int)(ratio * dur);
            curTime.Text = Fmt.Time(pos);
            if (committed)
            {
                engine.Seek(pos);
                if (playing) engine.Resume();
            }
        }

        /// <summary>按序号切换模式：0=顺序 1=列表循环 2=单曲循环 3=随机。</summary>
        void SetMode(int index)
        {
            if (index < 0 || index > 3) return;
            mode = (PlayMode)index;
            UpdateModeUI();
            SetStatus("播放模式：" + ModeName(mode));
            SaveState();
        }

        void CycleMode()
        {
            SetMode(((int)mode + 1) % 4);
        }

        bool IsActiveMode(int index)
        {
            return (int)mode == index;
        }

        static string ModeName(PlayMode m)
        {
            switch (m)
            {
                case PlayMode.Sequential: return "顺序播放";
                case PlayMode.LoopList: return "列表循环";
                case PlayMode.LoopOne: return "单曲循环";
                default: return "随机播放";
            }
        }

        void UpdateModeUI()
        {
            if (modeSegments == null) return;
            for (int i = 0; i < modeSegments.Length; i++)
            {
                bool on = IsActiveMode(i);
                modeSegments[i].Background = on ? (Brush)CGrad : Brushes.Transparent;
                TextBlock t = modeSegments[i].Child as TextBlock;
                if (t != null) t.Foreground = on ? Brushes.White : CDim;
            }
        }

        void ApplyVolume()
        {
            engine.SetVolume(mute ? 0 : volume);
            if (volBar != null) volBar.SetValue(volume);
            if (volLabel != null) volLabel.Text = ((int)Math.Round((mute ? 0 : volume) * 100)) + "%";
        }

        void UpdatePlayIcon()
        {
            if (playIcon == null) return;
            playIcon.Data = Geometry.Parse(playing
                ? "M 4,2 L 7,2 L 7,14 L 4,14 Z M 9,2 L 12,2 L 12,14 L 9,14 Z"
                : "M 4,2 L 14,8 L 4,14 Z");
            playIcon.Width = playing ? 18 : 20;
            playIcon.Height = playing ? 18 : 20;
        }

        void UpdateNowPlaying()
        {
            if (currentIndex < 0 || currentIndex >= tracks.Count)
            {
                npTitle.Text = "未在播放";
                npArtist.Text = "导入歌曲后开始播放";
                npAlbum.Text = "";
                SetCover(null);
                return;
            }
            Track t = tracks[currentIndex];
            npTitle.Text = t.DisplayTitle;
            npArtist.Text = t.DisplayArtist;
            npAlbum.Text = t.Album.Length > 0 ? "《" + t.Album + "》" : "";
            SetCover(t.Cover);
            Title = t.DisplayTitle + " - 音乐播放器";
        }

        void SetCover(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                coverBox.Background = CPanel2;
                coverBox.Child = coverPlaceholder;
                return;
            }
            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 512;
                bi.StreamSource = new MemoryStream(data);
                bi.EndInit();
                bi.Freeze();
                ImageBrush ib = new ImageBrush(bi);
                ib.Stretch = Stretch.UniformToFill;
                ib.Freeze();
                coverBox.Child = null;
                coverBox.Background = ib;
            }
            catch
            {
                coverBox.Background = CPanel2;
                coverBox.Child = coverPlaceholder;
            }
        }

        void SetStatus(string s)
        {
            if (statusText != null) statusText.Text = s;
        }

        void OnTick(object sender, EventArgs e)
        {
            if (engine.IsOpen)
            {
                int dur = engine.DurationMs;
                int pos = engine.PositionMs;

                if (dur > 0 && currentIndex >= 0 && currentIndex < tracks.Count && tracks[currentIndex].DurationMs != dur)
                    tracks[currentIndex].DurationMs = dur;

                if (!seekBar.Dragging)
                {
                    seekBar.SetValue(dur > 0 ? Math.Min(1.0, (double)pos / dur) : 0);
                    curTime.Text = Fmt.Time(pos);
                }
                totalTime.Text = dur > 0 ? Fmt.Time(dur) : "--:--";

                if (playing)
                {
                    if (dur > 0 && pos >= dur - 300) Next(true);
                    else if (dur == 0 && engine.Mode == AudioMode.Stopped) Next(true);
                }
            }

            if (eqBars != null)
            {
                for (int i = 0; i < eqBars.Length; i++)
                    eqBars[i].Height = playing ? (4 + rnd.Next(12)) : 5;
            }
        }

        // ================= 导入 / 删除 =================

        void ImportFiles()
        {
            Microsoft.Win32.OpenFileDialog d = new Microsoft.Win32.OpenFileDialog();
            d.Title = "选择音乐文件";
            d.Multiselect = true;
            d.Filter = "音频文件|*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.ogg;*.opus;*.mp4|MP3 音乐 (*.mp3)|*.mp3|所有文件 (*.*)|*.*";
            if (d.ShowDialog(this) == true) AddPaths(new List<string>(d.FileNames), false);
        }

        void ImportFolder()
        {
            try
            {
                using (System.Windows.Forms.FolderBrowserDialog fb = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fb.Description = "选择存放音乐的文件夹（会自动扫描子文件夹）";
                    fb.ShowNewFolderButton = false;
                    if (fb.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        AddPaths(new List<string> { fb.SelectedPath }, false);
                }
            }
            catch (Exception ex)
            {
                SetStatus("打开文件夹选择器失败：" + ex.Message);
            }
        }

        static bool IsAudio(string path)
        {
            string ext = IOPath.GetExtension(path).ToLowerInvariant();
            for (int i = 0; i < AudioExt.Length; i++) if (AudioExt[i] == ext) return true;
            return false;
        }

        static List<string> ExpandPaths(List<string> input)
        {
            List<string> files = new List<string>();
            for (int i = 0; i < input.Count; i++)
            {
                string p = input[i];
                try
                {
                    if (File.Exists(p))
                    {
                        if (IsAudio(p)) files.Add(p);
                    }
                    else if (Directory.Exists(p))
                    {
                        Walk(p, files, 0);
                    }
                }
                catch { }
                if (files.Count > 5000) break;
            }
            return files;
        }

        static void Walk(string dir, List<string> files, int depth)
        {
            if (depth > 6 || files.Count > 5000) return;
            try
            {
                string[] fs = Directory.GetFiles(dir);
                for (int i = 0; i < fs.Length; i++) if (IsAudio(fs[i])) files.Add(fs[i]);
            }
            catch { }
            try
            {
                string[] ds = Directory.GetDirectories(dir);
                for (int i = 0; i < ds.Length; i++) Walk(ds[i], files, depth + 1);
            }
            catch { }
        }

        void AddPaths(List<string> paths, bool forcePlay)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            int added = 0;
            bool wasEmpty = tracks.Count == 0;
            try
            {
                List<string> files = ExpandPaths(paths);
                for (int i = 0; i < files.Count; i++)
                {
                    string f = files[i];
                    bool dup = false;
                    for (int j = 0; j < tracks.Count; j++)
                    {
                        if (string.Equals(tracks[j].Path, f, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                    }
                    if (dup) continue;

                    Track t = new Track(f);
                    try { TagReader.Read(t); } catch { }
                    try { t.DurationMs = DurationProbe.Get(f); } catch { }
                    tracks.Add(t);
                    added++;
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            if (added > 0)
            {
                RebuildList();
                SaveState();
                SetStatus(string.Format("已导入 {0} 首歌曲", added));
                if (selectedIndex < 0) selectedIndex = 0;
                if (forcePlay || wasEmpty) PlayIndex(0);
            }
            else
            {
                SetStatus(HasAny(paths) ? "这些歌曲已经在列表里了" : "没有找到可播放的音频文件（支持 mp3 / wav / m4a / flac 等）");
            }
        }

        static bool HasAny(List<string> paths)
        {
            return paths != null && paths.Count > 0;
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOfAny(new char[] { '\r', '\n' });
            return i < 0 ? s : s.Substring(0, i);
        }

        void RemoveAt(int i)
        {
            if (i < 0 || i >= tracks.Count) return;
            bool removingCurrent = (i == currentIndex);
            tracks.RemoveAt(i);
            if (removingCurrent)
            {
                engine.Close();
                playing = false;
                currentIndex = -1;
                UpdatePlayIcon();
                UpdateNowPlaying();
                curTime.Text = "0:00";
                totalTime.Text = "--:--";
                seekBar.SetValue(0);
            }
            else if (i < currentIndex) currentIndex--;
            selectedIndex = Math.Min(selectedIndex, tracks.Count - 1);
            RebuildList();
            SaveState();
            SetStatus("已从列表移除");
        }

        void ClearAll()
        {
            if (tracks.Count == 0) return;
            if (MessageBox.Show(this, "确定要清空整个播放列表吗？", "清空列表",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            engine.Close();
            playing = false;
            tracks.Clear();
            currentIndex = -1;
            selectedIndex = -1;
            UpdatePlayIcon();
            UpdateNowPlaying();
            curTime.Text = "0:00";
            totalTime.Text = "--:--";
            seekBar.SetValue(0);
            RebuildList();
            SaveState();
            SetStatus("列表已清空");
        }

        static void Reveal(string path)
        {
            try
            {
                if (path.Length == 0) return;
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch { }
        }

        // ================= 拖拽 / 键盘 / 状态 =================

        void OnDragOverEvent(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        void OnDropEvent(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (items != null && items.Length > 0) AddPaths(new List<string>(items), false);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (searchBox != null && searchBox.IsKeyboardFocusWithin && e.Key != Key.Escape) return;

            switch (e.Key)
            {
                case Key.Space: TogglePlay(); e.Handled = true; break;
                case Key.Left: SeekRelative(-5000); e.Handled = true; break;
                case Key.Right: SeekRelative(5000); e.Handled = true; break;
                case Key.Up:
                    volume = Math.Min(1.0, volume + 0.05);
                    mute = false;
                    ApplyVolume();
                    SaveState();
                    e.Handled = true;
                    break;
                case Key.Down:
                    volume = Math.Max(0.0, volume - 0.05);
                    mute = false;
                    ApplyVolume();
                    SaveState();
                    e.Handled = true;
                    break;
                case Key.M:
                    mute = !mute;
                    ApplyVolume();
                    e.Handled = true;
                    break;
                case Key.R:
                    CycleMode();
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (selectedIndex >= 0) RemoveAt(selectedIndex);
                    e.Handled = true;
                    break;
                case Key.O:
                    if (Keyboard.Modifiers == ModifierKeys.Control) { ImportFiles(); e.Handled = true; }
                    break;
                case Key.N:
                    if (Keyboard.Modifiers == ModifierKeys.Control) { Next(false); e.Handled = true; }
                    break;
                case Key.P:
                    if (Keyboard.Modifiers == ModifierKeys.Control) { Prev(); e.Handled = true; }
                    break;
                case Key.Escape:
                    if (searchBox != null && searchBox.Text.Length > 0) { searchBox.Text = ""; e.Handled = true; }
                    break;
            }
        }

        void ApplyDarkTitleBar()
        {
            try
            {
                IntPtr h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int on = 1;
                DwmSetWindowAttribute(h, 20, ref on, 4);
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // ================= 状态保存 =================

        void LoadState()
        {
            PlaylistStore.State s = PlaylistStore.Load();
            volume = s.Volume;
            mute = false;

            try { mode = (PlayMode)Enum.Parse(typeof(PlayMode), s.Mode); }
            catch { mode = PlayMode.LoopList; }
            UpdateModeUI();

            for (int i = 0; i < s.Tracks.Count && i < 2000; i++)
            {
                string p = s.Tracks[i];
                try
                {
                    if (!File.Exists(p)) continue;
                    Track t = new Track(p);
                    TagReader.Read(t);
                    t.DurationMs = DurationProbe.Get(p);
                    tracks.Add(t);
                }
                catch { }
            }

            if (tracks.Count > 0)
            {
                selectedIndex = (s.Index >= 0 && s.Index < tracks.Count) ? s.Index : 0;
                SetStatus(string.Format("已恢复上次的播放列表（{0} 首）", tracks.Count));
            }

            ApplyVolume();
            RebuildList();
        }

        void SaveState()
        {
            PlaylistStore.Save(volume, mode.ToString(), currentIndex, tracks);
        }

        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try { SaveState(); } catch { }
            try { engine.Dispose(); } catch { }
        }

        // ================= 自动生成的滑块 =================

        sealed class Bar
        {
            public readonly Grid Root = new Grid();
            readonly Border track = new Border();
            readonly Border fill = new Border();
            readonly Ellipse thumb = new Ellipse();
            readonly double thickness;
            double value;
            bool dragging;

            public event Action<double, bool> Changed;

            public Bar(double thickness, Brush accent)
            {
                this.thickness = thickness;
                Root.Height = thickness + 12;
                Root.Background = Brushes.Transparent;
                Root.VerticalAlignment = VerticalAlignment.Center;

                track.Height = thickness;
                track.CornerRadius = new CornerRadius(thickness / 2);
                track.Background = CLine;
                track.VerticalAlignment = VerticalAlignment.Center;

                fill.Height = thickness;
                fill.CornerRadius = new CornerRadius(thickness / 2);
                fill.Background = accent;
                fill.HorizontalAlignment = HorizontalAlignment.Left;
                fill.VerticalAlignment = VerticalAlignment.Center;

                thumb.Width = thickness + 6;
                thumb.Height = thickness + 6;
                thumb.Fill = Brushes.White;
                thumb.HorizontalAlignment = HorizontalAlignment.Left;
                thumb.VerticalAlignment = VerticalAlignment.Center;
                thumb.Visibility = Visibility.Collapsed;

                Root.Children.Add(track);
                Root.Children.Add(fill);
                Root.Children.Add(thumb);

                Root.SizeChanged += delegate { Layout(); };
                Root.MouseLeftButtonDown += OnDown;
                Root.MouseMove += OnMove;
                Root.MouseLeftButtonUp += OnUp;
                Root.MouseEnter += delegate { thumb.Visibility = Visibility.Visible; };
                Root.MouseLeave += delegate { if (!dragging) thumb.Visibility = Visibility.Collapsed; };
            }

            public bool Dragging { get { return dragging; } }

            public void SetValue(double v)
            {
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                value = v;
                if (!dragging) Layout();
            }

            void Layout()
            {
                double w = Root.ActualWidth;
                if (w <= 0) return;
                double x = value * w;
                fill.Width = x;
                thumb.Margin = new Thickness(Math.Max(0, x - thumb.Width / 2), 0, 0, 0);
            }

            void OnDown(object s, MouseButtonEventArgs e)
            {
                dragging = true;
                Root.CaptureMouse();
                Update(e.GetPosition(Root).X);
                Fire(false);
            }

            void OnMove(object s, MouseEventArgs e)
            {
                if (!dragging) return;
                Update(e.GetPosition(Root).X);
                Fire(false);
            }

            void OnUp(object s, MouseButtonEventArgs e)
            {
                if (!dragging) return;
                dragging = false;
                Root.ReleaseMouseCapture();
                Fire(true);
            }

            void Update(double x)
            {
                double w = Root.ActualWidth;
                if (w <= 0) return;
                value = Math.Max(0, Math.Min(1, x / w));
                Layout();
            }

            void Fire(bool committed)
            {
                if (Changed != null) Changed(value, committed);
            }
        }
    }
}
