# Antigravity 任务栏双环 (`AntigravityQuotaBar`)

仿照 Codex (`DualQuotaBar`) 设计的 Windows 任务栏反重力双环额度监控工具。

## 外观与交互
- **位置**：自动停靠在任务栏通知区域左侧，若检测到 `DeepSeek 余额任务栏` 或 `Codex 双额度任务栏` 正在运行，会自动紧贴其左侧排成一排。
- **双环配色**：
  - **外环（Google 蓝 `#4285F4`）**：Gemini 5 小时剩余额度（中心数字同步显示）
  - **内环（Google 绿 `#34A853`）**：Gemini 本周剩余额度
- **详情卡片**：显示 Gemini 5 小时、Gemini 本周（带进度条与重置倒计时），以及 Claude / GPT（5h + 本周）和 Gemini 4 Argon（本周）剩余额度。
- **交互**：
  - **单击**：在圆环正上方弹出深色用量卡片（高 DPI 像素字体，不截断）
  - **双击**：切换中心数字为白色 / 黑色
  - **右键**：刷新额度、安装官方 `agy` CLI、打开 `agy` 登录终端、退出
  - **全屏自动隐藏**：前台有全屏应用时自动隐藏

## 数据源（官方 `agy` CLI）
程序默认调用官方无头只读命令 `agy -p "/usage" --output-format json`（不消耗模型额度）。

首次使用前请确保本机已安装并登录 `agy`：
```powershell
irm https://antigravity.google/cli/install.ps1 | iex
agy
```
（也可以直接右键圆环点击「安装官方 agy CLI」或「打开 agy 登录终端」。）

## 编译与运行
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\build\AntigravityQuotaBar.exe
```
