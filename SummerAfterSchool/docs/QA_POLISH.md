# 原生玩法与界面深化验收

`RuntimeProbe` 只在 `--smoke-test` 下启动。默认完整模式保留经典打片、暂停续局、真实出圈赌片结算和玻璃球六手/成绩重载流程，最后运行本轮新增验收。

快速验收使用 `--smoke-test --polish-smoke`，输出到 `Logs/QA/Polish/`，跳过原有完整竞技流程。`--marble-smoke` 保留玻璃球快速回归，输出到 `Logs/QA/Marbles/`。请勿同时传入两个快速模式。

`--smoke-test --marble-smoke --pinwheel-smoke` 将完整六手挑战切换为风车阵，验证非默认阵型结算与成绩重载。`--smoke-test --visual-smoke` 只检查最新版四街区、约战/收藏/声音/选阵面板及比赛 HUD，并验证暂停面板内打开声音设置后逐层 Esc 返回与恢复。画面模式输出到 `Logs/QA/Visual`，不重复已经通过的竞技回归。

新增验收覆盖：

- 八种布局的目标数量、圈内位置、成对间距。
- 每种布局一次真实刚体出手，限时等待停稳，然后恢复稳定快照，核对目标姿态、收片标记、得分、手数、模式和反馈。
- 打片快照版本 0 / 1 / 2；未来打片版本 3 和玻璃球版本 2 必须拒绝，且拒绝不能改变现有盘面。
- 物体已经离开发射点后，调用 `CancelAim`、失焦回调并暂停，核对刚体位置、朝向、速度、动态属性和回合阶段没有改变；瞄准状态和蓄力必须清零。
- 四路音量控件、Esc 关闭及时间缩放恢复、四种玻璃球挑战选择。截图 `14-audio-settings.png` / `15-marble-choices.png` 单独检查渲染，模态窗口不使用竞技场中央应可点击的检查。
- 音频公开地点/情境属性，以及 `Audio/summer_collection` 资源。完整模式记录实际 Hub / Collection / Match / Result 转换；快速模式的独立挑战只验证 Match 和 Result 音景 API，不声称经过真实战绩结算。

布局对局创建独立玩法组件，使用同一原生地面和 PhysX，不订阅协调器结算事件；结束时对比整个存档对象，确保收藏、约战和进度没有被改变。新布局不要求第一手必得分，成功以真实出手、停稳和无损恢复为准。

`runtime-report.json` 预期 `errors=[]`，`polishControlsValid/layoutsGeometryValid/legacySnapshotsRestored/futureSnapshotRejected/layoutTestsPreservedSave=true`，`pogLayoutsVerified=4`、`marbleLayoutsVerified=4`、`layoutSnapshotRestores=8`、`flightCancelChecks=8`。只有 `completed=true` 且上述字段符合预期，才能报告本轮原生验收通过。

本轮已在 Unity 6000.3.25f1 / Direct3D 11 / RTX 4060 Laptop 的原生 Player 执行：八布局、八次无损恢复、八次飞行取消/失焦/暂停全部通过，`completed=true`、`errors=[]`，上述五个布尔字段均为 true。逐球位置差为零，朝向差为零，浮点四元数分量最多出现 1.2e-7 级差异；没有放宽原姿态断言。

首次运行曾发现玻璃球恢复时旧插值 Transform 覆盖刚体姿态，失败报告保留于 `Logs/QA/Polish-first-attempt`。修复将显示与物理姿态同时恢复后再同步，复验报告位于 `Logs/QA/Polish/runtime-report.json`。快照 API 尚未作为玻璃球跨进程存档功能对用户开放，此测试验证组件的稳定恢复能力。

风车阵另完成六次真实出手，得分 6/8，重载后局数增加一、最好成绩仍为此前 7/8。结果音景、成绩持久化及最终面板通过，见 `Logs/QA/Pinwheel/runtime-report.json`。四街区与全部新面板的最终视觉记录见 `Logs/QA/Visual`。各快速模式只填写本模式覆盖的字段；未执行的布尔/计数默认值不表示检查失败。
