# 0.5.6 自由站位与片角验收

日期：2026-10-09  
版本：Windows 0.5.6  
引擎：Unity 6000.3.25f1  
平台：Windows Player

## 交付结论

0.5.6 已完成实现、隔离存档原生验收和 Windows 可玩构建晋级。正式入口为 `Builds/Windows-v0.4/放学后.exe`；完整上一稳定版为 `Builds/Windows-v0.4-before-free-angle-056-stable-20261009/`。候选与正式目录均为 142 个文件，候选、正式和备份的 `放学后.exe` SHA-256 均为 `3070E580E019576B9AFCDEB4FEC18D03CEBC119F6AE84D597412905C749AD653`。

## 本轮体验

- 站位从固定近侧扩展为 0°–360°。A/D 或按住鼠标中键左右拖动绕场；W/S 调镜头俯仰，鼠标滚轮只调远近，Q/E 调出手旋转；面板前后倾和左右倾滑块分别调整圆片真实倾角，各限制为 ±35°；复位按钮恢复默认站位、倾角与旋转。
- 站位、前后俯仰、左右片角在桌面和手机准备区可读，暂停、浮层、教学和退出确认沿用输入门控。
- 待出手片在预览阶段关闭 Collider，换位和片角调整不会推开目标；真正松手时才恢复 Collider，最终碰撞、翻面、出圈、停稳和得分仍由 PhysX 决定。
- 场景相机支持绕场、斜俯视、正上方俯视和距离调整；镜头与起手姿态切换会取消蓄力，避免把旧方向带入新站位。

## 原生证据

主报告：`Logs/QA/FreeAngle-056-native-r11/free-angle-experience-report.json`  
运行保护：`Logs/QA/FreeAngle-056-native-r11/native-run.json`

主报告 `passed=true`、`errors=[]`，385 项检查、10 次真实停稳出手、30 张原生截图；真实对局自然以玩家 1:3 阿岳结束；学校和教学楼走廊各验证 12 个遮挡机位。已覆盖：

- 0°、90°、180°、270° 绕场，45°高俯角和正上方俯视；场地边界、中央目标片、待出手片和底部控制区保持可读。
- 任意方向预览与真实释放、片角变化、待出手片 Collider 隔离、真实 PhysX 对局、自然结算、暂停冻结和继续后盘面一致。
- 旧 v3 快照缺少新站位/片角字段时使用默认值；未来快照明确拒绝；磁盘读回后元数据、位置和旋转差异为 0。
- 隔离存档、PlayerPrefs、真实收藏与旧存档保持不变；候选退出码 0、未超时、清理错误为空。

关键截图：

- `school_gate-orbit-180.png`、`school_gate-orbit-270.png`
- `school_gate-diagonal-az45-e72-d10.png`、`school_gate-topdown-free-angle.png`
- `corridor-orbit-180.png`、`corridor-orbit-270.png`
- `corridor-diagonal-az45-e72-d10.png`、`corridor-topdown-free-angle.png`

## 回归范围

- `Logs/QA/FreeAngle-056-mobile-1280-r3/free-angle-experience-report.json`：Windows 手机 UI 分支 63 项通过。
- `Logs/QA/FreeAngle-056-mobile-1600-r2/free-angle-experience-report.json`：Windows 手机 UI 分支 63 项通过。
- `Logs/QA/Teaching-056-native-r2/guided-experience-report.json`：教学 252 项断言通过，俯视/斜俯视、暂停恢复、取消和三步真实出手保持。
- `Logs/QA/FreeAngle-056-manual-visible-r1/`、`r2/`：人工模式包装脚本曾错误要求不存在的 JSON，失败证据保留；r3 修正后可见会话正常退出，`native-run.json` 记录退出码 0、偏好恢复、真实存档未变。由于桌面自动化通道未返回候选游戏窗口，未把人工按键、拖拽或真实释放写成通过。

本轮没有启动 Qwen Image、ACE-Step 或其他重模型；没有新增生成资源。历史 DXGI 独占全屏超时记录继续保留，本轮没有把窗口化回退写成独占全屏通过。Android APK 仍为 0.5.3，Windows 手机 UI 分支不代表 Android 真机验证。

低风险操作细节：正上方模式固定为 90° 俯视；W/S 调整保存的斜俯视仰角，因此在正上方模式按键时画面保持俯视，切回斜俯视后显示新角度。

## 晋级与入口

晋级前确认没有本项目 Unity/Player 进程。先把完整 0.5.5 正式目录移动到上一稳定备份，再整体复制候选目录，避免只替换 exe 遗漏 `Assembly-CSharp.dll`、资源或运行库。`开始玩.cmd`、`D:\桌面\放学后.lnk` 和工作区快捷方式均继续指向 `Builds/Windows-v0.4/放学后.exe`，无需改入口。

候选构建汇总 `Logs/QA/FreeAngle-056/build-summary.json` 为 `Succeeded`、0 errors、0 warnings、186,711,164 bytes。原始 `build.log` 曾记录 LicensingClient access-token refresh 不可用；后续日志成功解析 entitlement 并更新 Unity Personal license，构建结果仍为成功。

最终汇总：`Logs/QA/FreeAngle-056/release-validation.json`。
