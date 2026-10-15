# 音乐播放器 · MusicPlayer

一个 **76 KB 单文件**的绿色音乐播放器：**.NET Framework 4.8 + WPF**，用系统自带 `csc.exe` 编译，
**零第三方依赖**，双击即用、无需安装。手写了 ID3 标签解析、专辑封面提取和 MP3 时长探测。

A single-file (76 KB) portable music player for Windows: WPF on .NET Framework 4.8, compiled with the
built-in `csc.exe`, **no third-party dependencies**. ID3 tag parsing, embedded cover extraction and
MP3 duration probing are all hand-written (no libraries).

---

## 怎么导入 MP3

三种方式，随便挑一种：

1. **点按钮**：右上角「＋ 添加音乐」→ 可按住 Ctrl 多选，一次选一堆。
2. **拖进来**：把 MP3 文件（或整个文件夹）直接拖到窗口里，文件夹会自动扫描子目录。
3. **用「添加文件夹」**：适合一次导入整个音乐库。

导入后**双击某一首**开始播放。

## 支持的格式

主力支持 **MP3**、WAV（系统原生解码，最稳）。
另外也能尝试 m4a / aac / wma / flac / ogg / opus / mp4 —— 能否播放取决于系统是否装了对应解码器。

## 快捷键

| 按键 | 作用 |
| --- | --- |
| `空格` | 播放 / 暂停 |
| `←` / `→` | 快退 / 快进 5 秒 |
| `↑` / `↓` | 音量 +5% / -5% |
| `M` | 静音开关 |
| `R` | 切换播放模式（顺序 → 列表循环 → 单曲循环 → 随机） |
| `Delete` | 把选中的歌从列表移除 |
| `Ctrl + O` | 添加音乐 |
| `Ctrl + N` / `Ctrl + P` | 下一首 / 上一首 |
| `Esc` | 清空搜索框 |

鼠标右键单曲 → 播放 / 从列表移除 / **在资源管理器中显示**。

## 其它功能

- **自动读标签**：歌名、歌手、专辑、**内嵌专辑封面**自动显示（ID3v2.2/2.3/2.4 与 ID3v1；中文标签 UTF-16 / UTF-8 / GBK 均正确识别）
- **没有标签也不怕**：从文件名猜，`歌手 - 歌名.mp3` 能自动拆开
- **时长显示**：导入时只读文件头把时长算好，上千首也不卡
- **播放模式**：顺序 / 列表循环 / 单曲循环 / 随机（左下四个按钮，单击直达）
- **记忆功能**：关闭时记住音量、播放模式、播放列表，下次自动恢复
- **搜索**：按歌名 / 歌手 / 专辑 / 文件名实时过滤

## 技术实现

- 纯 **.NET Framework 4.8 + WPF**，系统自带 `csc.exe` 编译，**零第三方依赖**
- 播放引擎双后端：首选 **Windows MCI (winmm)**（MP3/WAV 最稳、延迟最低）；打不开时**自动回退 WPF MediaPlayer**（Media Foundation）
- MP3 时长探测是手写的 MPEG 帧头解析（识别 Xing/Info/VBRI 帧数，否则按 CBR 码率估算），**不整文件扫描**
- ID3 解析、专辑封面提取全部手写，无外部库
- 配置：`%APPDATA%\MusicPlayer\state.txt`；不可写时退回程序目录下 `MusicPlayerData\`

### 源码结构

```
build.ps1            一键编译脚本
src\Program.cs       入口（含 --selftest 自检模式）
src\MainWindow.cs    界面与交互
src\AudioEngine.cs   MCI + MediaPlayer 双后端播放引擎
src\TagReader.cs     ID3v1 / ID3v2 标签与封面解析
src\DurationProbe.cs MP3 / WAV 时长解析
src\PlaylistStore.cs 配置与播放列表持久化
src\Models.cs        数据模型
src\SelfTest.cs      播放链路自检
tools\IconGen.cs     图标生成器
```

### 重新编译

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1 -DesktopCopy "$([Environment]::GetFolderPath('Desktop'))\音乐播放器.exe"
```

### 自检（不开界面，直接验证解码播放链路）

```powershell
& .\build\MusicPlayer.exe --selftest "D:\某首歌.mp3" "$env:TEMP\st.log"
```

## 常见问题

- **双击没反应？** 需要 Windows 10/11（自带 .NET Framework 4.8）。系统精简掉了 .NET 4.8 的话装一下即可。
- **某首歌放不出来？** 状态栏会提示原因；文件损坏或编码不支持时会出现。MP3/WAV 基本不会。
- **音量滑块没反应？** 少数声卡驱动不支持 MCI 音量命令，程序会自动改用系统波形音量。

## 许可

MIT。编译产物（`build\*.exe`）不随仓库分发，用上面的 `build.ps1` 自己编。
