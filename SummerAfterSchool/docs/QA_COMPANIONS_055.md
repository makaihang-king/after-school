# 0.5.5 伙伴来到场边 · 原生验收

完成日期：2026-10-09。Unity `6000.3.25f1`，Windows Player `0.5.5`。候选版通过后晋级 `Builds/Windows-v0.4/放学后.exe`；完整上一稳定 0.5.4 保存在 `Builds/Windows-v0.4-before-companions-055-stable-20261009/`。

本轮只改变伙伴身份、程序化姿态、约战准备对白与上下文接线。正式 PhysX 参数、计分、NPC 难度、收藏实例、存档 schema2/snapshot3、教学判据和退出门控没有修改。旧音频/图片资源保留，没有启动本地重模型。

## 桌面完整体验

证据：`Logs/QA/Companions-055-native-r3/companion-experience-report.json`，`completed=true`、`passed=true`、`errors=[]`，166 条检查、14 次实际 PhysX 停稳出手、14 张原生截图，73.09 秒。

- 七位伙伴经原生 UI 选择，两轮重访，身份、实际模型衣服材质和书包颜色保持一致；每个模型的 Collider 已移除。同一人的外观不再随场边序号或访问变体变化。
- 招呼触发前后读取真实关节姿态。林林、阿砚的点头额外连续采样，最大头部相对角度约 36.4–40.4 度，包含正常看向场地的转头；不存在逐帧叠加的失控点头。
- 校门阿岳实际占据首位并标为对手，UI 按钮阻挡玩法拖拽。第一手真实屏幕拖拽斜擦未得分，实际产生 8 次碰撞，`Message`/`LastFeedback` 为空，保留用户要求的安静未得分反馈。
- 飞行中从原生暂停 UI 进入，人物与刚体姿态、线速度、角速度均冻结。继续后同一次出手完成；回街区后由原生继续按钮恢复同一场约战，snapshot3 序列化盘面完全一致，阿岳身份保持。
- 正式双方各押一张的约战自然完成 14 手，7 次玩家出手、7 次 NPC 出手，最终 `Draw`，2:2；实际随机种子 `991031`。平局收据、无圆片转手、pending 清除与完整存档磁盘读回一致。没有预设终局分数或更改生产难度。
- 另开一场切磋，回街区并选择家中后认输，结果回应仍为实际挂起对手阿岳。此认输检查独立于上面的自然完整对局。
- 走廊中进入教学，小雨位于首位但不标为正式对手，无新增押片。原生教学画面可渲染；本轮未重跑 0.5.4 三步完整教学物理验收。

## 手机准备页分支

`Logs/QA/Companions-055-mobile-1280-r1/` 与 `Logs/QA/Companions-055-mobile-1600-r1/` 分别为 1280×720、1600×720 的 Windows 手机 UI 分支，各 `passed=true`、125 条检查、8 张原生截图，`errors=[]`。

七人选择、两轮身份、长招呼及大志真片模式均通过。左侧文字可滚动，固定详情按钮与底部开始按钮不重叠。这两份报告使用 `--companion-ui-only`，明确 `fullMatchValidated=false`，不重复完整桌面物理对局，也不代表 Android 真机运行或新版 APK 已交付；现有 APK 仍为 0.5.3。

## 真实入口、稳定版与用户数据

- 构建摘要 `Logs/QA/Companions-055/build-summary.json`：`Succeeded`，0 errors、0 warnings，186,610,907 字节。
- `promotion.json` 记录候选晋级、上一稳定目录及关键运行文件的前后 SHA256；移动前所有目标绝对路径均在本工程 Builds 内，并确认无用户游戏/Unity 进程。
- `normal-startup.json`：从原始 `开始玩.cmd` 实际启动，入口返回 0，唯一新进程响应正常；通过 computer-use 检查真实原生首页显示 0.5.5 与已有收藏/故事进度。只读启动未操作真实对局，检查后仅释放本轮启动的进程。
- `shortcut-check.json`：桌面和工作区两个快捷方式仍指向正式 exe，无强制显示参数。CMD 保持 ASCII/CRLF，日常启动不改用户选择的模式。
- `real-save-preservation.json`：真实 schema2 主档/备份与旧目录主档/备份四个文件的 SHA256 在本轮前后完全一致。`user-preferences-preservation.json` 核对玩家显示与音量键保持；原生 Screenmanager 键已恢复。
- 截图由 Unity 相机和原生 UI 面板实际渲染；另以 Windows computer-use 检查可见正式窗口。原生截图已检查关键准备、场边、结果和教学画面，没有使用概念图替代运行画面。

## 保留的失败和适用范围

- `Companions-055-native-r1`：探针从错误对象层级寻找 UIDocument，在玩法前失败。已改为和现有教学探针相同的 children 查询。
- `Companions-055-native-r2`：点头取样发生在动作已进入姿态后，两帧变化过小而误判。已把起点移至 UI 触发的同一帧，连续幅度检查仍保留。没有通过降低动作或得分要求来通过验收。
- 最终成功报告为 r3，失败目录没有删除。历史独占全屏组件重建的 DXGI 超时仍见 `Teaching-054-v04-smoke`，本轮实际正常启动可进入独占模式，但没有重跑旧的全量显示切换流程，因此不宣称该历史检查已修复。
- 258 项数据回归、96 个环境、完整故事/杯赛、音频循环等未改范围沿用各自历史证据，不标为本轮重新通过。

## 复现

无用户 Unity/游戏会话时，可对正式构建使用新的隔离目录名：

```powershell
powershell -ExecutionPolicy Bypass -File .\Logs\QA\Companions-055\run-native.ps1 -Name Companions-055-new-run
```

手机准备页另加 `-ExtraFlags '--mobile-ui-test --companion-ui-only' -Width 1280 -Height 720` 或 `-Width 1600 -Height 720`。该证据脚本拒绝复用已有 save 目录，并在存在玩家/Unity 会话时停止；测试会写 QA 显示键及 Unity 原生窗口键，批量验收完成后应按现有 `verify-v04.ps1` 的原生窗口备份/恢复流程收尾。
