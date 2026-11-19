# GRings — Windows 任务栏 AI 额度三环

将 **Codex 双环**、**DeepSeek 余额环**、**Antigravity 双环** 整合为单个轻量化 Windows 程序 `GRings.exe`，使用 .NET Framework 4 + WinForms 原生编译，无需额外运行时依赖。

## 功能亮点
- **三环并排停靠**：自动吸附在任务栏通知区域左侧，从左到右依次为：
  1. **Antigravity 双环**：粉色（`#F988B3`）外环 5 小时额度 + 浅卡其色（`#E2D2AE`）内环本周额度，支持右键一键切换多套暖色配色与自定义调色板；通过官方只读命令 `agy -p "/usage" --output-format json` 获取实时额度（零 Token 消耗），卡片同时展示 Claude / GPT 与 Gemini 4 Argon 额度。
  2. **DeepSeek 余额环**：外环余额进度与预警色 + 内环峰谷时段切换倒计时，单击展开磨砂玻璃详情面板（支持深/浅色与透明度调节）。
  3. **Codex 双环**：青色外环 5 小时额度 + 紫色内环本周额度，通过本机 `codex app-server` 读取实时额度与**重置卡数量**，高 DPI 像素级排版无截断。
- **全屏自动隐藏**：前台存在全屏应用（游戏、视频等）时自动隐藏，退出全屏自动恢复。
- **统一交互**：单击展开详情卡片、双击切换中心黑白数字、右键打开各自功能菜单及「关闭此圆环 / 退出全部三环」。

## 环境要求
- Windows 10 / 11（自带 .NET Framework 4.x 与 `csc.exe`）
- **Codex**：已安装并登录 OpenAI Codex（桌面版或 CLI）
- **Antigravity**：已安装并登录官方 `agy` CLI（`irm https://antigravity.google/cli/install.ps1 | iex`）
- **DeepSeek**：已在 Win-CodexBar 中保存 DeepSeek API 密钥

## 编译与运行
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\build\GRings.exe
```

## License
[MIT](./LICENSE)
