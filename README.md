# GRings — Windows 任务栏 AI 额度三环

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows x64](https://img.shields.io/badge/Platform-Windows%20x64-0078D6.svg)](https://github.com/gwanting/GRings/releases)

> **圆环设计与制作：Wanting。** 作者：[Wanting / @gwanting](https://github.com/gwanting)

Windows 桌面轻量化用量监控工具：单文件整合程序 **`GRings.exe`**，在任务栏通知区域左侧并排展示 **Antigravity**、**DeepSeek**、**Codex** 三个透明双层圆环，无需安装额外运行时，不依赖 Win-CodexBar。

当前发布版本：**[v0.4.0](https://github.com/gwanting/GRings/releases/tag/v0.4.0)**。从 [Releases](https://github.com/gwanting/GRings/releases) 下载 `GRings.exe`。

## 功能

| 视图 | 中心数字 | 外环 | 内环 |
| --- | --- | --- | --- |
| Codex | 5h 剩余百分比 | 5h 剩余比例 | 每周剩余比例（详情卡片含重置卡数量） |
| DeepSeek | ≥1 元显示整数，不足 1 元显示两位小数 | 余额 / 满圈基准；颜色表示余额预警 | 距下次峰谷切换的剩余时间比例；峰期红、谷期绿 |
| Antigravity | Gemini 5h 剩余百分比 | Gemini 5h 剩余比例 | Gemini 本周剩余比例（支持右键切换配色） |

### 交互与细节
- **单文件三环整合**：双击 `GRings.exe` 即可一次性启动三个 60×60 像素透明圆环，自动在任务栏通知区左侧并排停靠，前台存在全屏应用时自动隐藏。
- **统一操控**：单击任一圆环展开对应详情卡片，再次点击圆环或点击外部收起；双击圆环切换中心黑白数字；右键任一圆环可选择「关闭此圆环」或「退出全部三环」，选择「关于」可查看作者及版权声明。
- **Codex 详情卡片**：高 DPI 像素级排版无截断，显示 5 小时与每周剩余额度、重置倒计时、按节奏预算以及**可用重置卡数量**。
- **DeepSeek 磨砂面板**：支持深色／浅色磨砂与 0%–100% 连续透明度调节，支持高帧率垂直同步展开／收起动画；外环默认 **20 元为满圈**（可右键修改），颜色按余额分级预警（<5 元红、5–10 元黄、10–20 元蓝、>20 元绿）；内环按北京时间峰谷时段（内置 2026 年法定节假日表）实时倒计时；内置「去充值」与「登录用量」查看今日消费与今日 Token。
- **Antigravity 详情卡片**：展示 Gemini（5h / 本周）、Claude / GPT（5h / 本周）与 Gemini 4 Argon（本周）剩余额度；右键菜单支持一键切换多套配色或使用调色板自定义内外环颜色。

## 快速使用（推荐）

1. 从 [Releases](https://github.com/gwanting/GRings/releases) 下载 `GRings.exe`（可用同页 `SHA256SUMS.txt` 校验），直接双击运行即可，无需安装 .NET 10 或 Win-CodexBar。
2. **Codex**：本机已安装并登录 OpenAI Codex（桌面版或 CLI）即可自动读取（支持自动定位微软商店版与透传 Windows 系统代理）。
3. **Antigravity**：本机已安装并登录官方 `agy` CLI（`irm https://antigravity.google/cli/install.ps1 | iex`）即可自动读取。
4. **DeepSeek**：右键 DeepSeek 圆环 → 点击 **「设置 DeepSeek API 密钥…」** 输入 API Key（使用 Windows DPAPI 本机加密保存，不上传任何服务器）；如需查看「今日消费 / 今日 Token」，可点击详情面板中的「登录用量」。

## 从源码构建

仅需 Windows 10 / 11 自带的 .NET Framework 4.x C# 编译器（`Framework64\v4.0.30319\csc.exe`）：

```powershell
# 构建单文件三环整合版 GRings.exe（推荐）
powershell -NoProfile -ExecutionPolicy Bypass -File .\GRings\build.ps1
.\GRings\build\GRings.exe
```

如需单独构建各模块或 DeepSeek 平台用量辅助程序（`DeepSeekPlatformUsage.exe`，需要 WebView2 Runtime）：

```powershell
.\DeepSeekBalance\build-platform.ps1
.\scripts\Test.ps1
```

## 数据来源与隐私说明

- **Codex**：直接调用本机 `codex app-server`（`account/rateLimits/read`）并结合 `CODEX_HOME/sessions` 元数据实时更新，自动透传 Windows 系统代理（不会为你切换代理节点）。数据过期或已重置时显示 `--`，不推算虚构额度。
- **DeepSeek**：查询官方 `https://api.deepseek.com/user/balance`（约每 60 秒更新，失败每 15 秒重试）。API 密钥通过 Windows DPAPI 加密保存在本机 `%LOCALAPPDATA%\GRings\deepseek-key.dat`；满圈基准保存于同目录 `balance-reference.json`。
- **DeepSeek 今日用量**：独立 WebView2 窗口在官方平台页面内只读查询，按北京时间汇总；登录态保存在 `%LOCALAPPDATA%\DeepSeekQuotaUsage\WebProfile`，汇总缓存位于 `today.json`，外观设置位于 `appearance.json`。
- **Antigravity**：通过官方无头只读命令 `agy -p "/usage" --output-format json` 读取剩余额度，零 Token 消耗；配色配置保存于 `%LOCALAPPDATA%\GRings\theme.txt`。
- 所有密钥、登录态、配置与日志均仅保存在本机用户目录，不随仓库或发布包分发。

## 目录结构

- `GRings/`：三环合一单文件整合版源码、多分辨率圆形双环图标与构建脚本
- `DualQuotaBar/`：Codex 独立双环模块
- `DeepSeekBalance/`：DeepSeek 独立余额与平台用量模块
- `AntigravityQuotaBar/`：Antigravity 独立双环模块
- `Shared/`：共享的全屏自动隐藏、圆环弧线绘制与单击／双击识别模块
- `third_party/`：第三方依赖清单与许可证声明

## 问题反馈

使用问题和功能建议请提交至 [Issues](https://github.com/gwanting/GRings/issues)，附上 Windows 版本、程序版本、复现步骤及脱敏截图。请勿上传 API 密钥、登录令牌、验证码或未经脱敏的账户文件。

## 版权与许可

圆环设计与制作：**Wanting**。

Copyright © 2026 Wanting ([gwanting](https://github.com/gwanting))。

许可证：[MIT License](LICENSE)。第三方组件许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)，更新记录见 [CHANGELOG.md](CHANGELOG.md)。

## 免责声明

本软件按“现状”提供，不附带 MIT 许可证所述的明示或暗示担保；责任限制详见 [LICENSE](LICENSE)。额度、余额和消费统计可能因网络、缓存、官方统计延迟或接口变化而与官方页面存在差异，请以相应服务提供方的账户记录为准。

本项目为独立第三方工具，与 OpenAI、DeepSeek、Google、Microsoft 及相关上游项目不存在官方隶属或背书关系。文中产品名称及商标属于各自权利人。
