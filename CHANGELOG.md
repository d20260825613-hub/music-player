# 更新日志

本文件记录「音乐播放器」的显著变更。
格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

> 说明：本仓库没有版本清单文件（无 package.json、无 AssemblyInfo.cs），
> 因此版本号不做机器校验，仅按日期在此登记。

## [0.2.0] - 2026-10-15

### 修复

- **一个坏文件就能让整个播放器凭空消失。** `AudioEngine.Send` 把 MCI 的错误码
  一律转成 `AudioException` 抛出，设计上假定调用方一定接住 —— 但键盘快捷键、
  拖动进度条、切换曲目这些 UI 事件处理器里都没有 `try`，`Program.Main` 也没挂
  `Application.DispatcherUnhandledException`。异常冒到消息循环就直接终止进程，
  而这个程序的卖点恰恰是"单文件、绿色、随手就能放"。现在 UI 线程上任何没人
  接住的异常都会被兜住：弹一句人话，程序继续运行。
- **拖动/快进在时长未知时会发出无意义的 seek。** `OnSeek` 本来就有
  `dur <= 0` 的防呆，但 `SeekRelative`（键盘左右方向键走的路径）没有：对
  MCI 读不出长度的文件按一下方向键，就会因为上面那条路径崩掉。现在两处口径
  一致，长度未知就不发 seek。
- **`build.ps1` 把仓库路径写死成 `D:\dsh\music-player`。** 这个脚本只在作者那台
  机器的那个目录下能跑，别人克隆下来执行 `build.ps1` 会找不到源码。改为从
  `$PSScriptRoot` 自动定位，任何目录下都能编译。

### 验证

- 用 in-box 的 `csc.exe`（.NET Framework 4.8）重新编译通过：`build.ps1`
  退出码 0，产出 `MusicPlayer.exe`（77.0 KB）。
- 用程序自带的 `--selftest` 跑了一遍真实播放链路（程序化合成的 1 秒 440Hz
  WAV）：时长识别 1000ms、播放推进、暂停、拖动到 300ms、恢复、停止，全部正常，
  `RESULT: PASS`，退出码 0。

## [0.1.0] - 2026-09-30

首个版本：76 KB 单文件绿色音乐播放器，WPF / .NET Framework 4.8，零第三方依赖，
手写 ID3 与 MP3 时长解析。
