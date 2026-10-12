# 玩法深化：摆阵、决策与手感反馈

## 本轮内容

打圆片增加四种五片阵型：`Classic` 放学老阵、`Fan` 扇面开彩、`TwinIslands` 两岸夹击、`EdgeRace` 圈边五子。玻璃球增加四种八球阵型：`Classic` 经典清圈、`SplitLanes` 两路包抄、`Crescent` 月牙阵、`Pinwheel` 风车阵。名字、描述和练习提示由 `GameplayLayouts` 提供。

默认 `Classic` 开局坐标保持已校准版本；新阵型仅改变摆放。原有掌拍掀边冲量、接触延迟、斜擦触发与完整出圈/静止翻面判定不变。所有开局目标处于圈内并且互不重叠。

NPC 会评估可达距离、前方遮挡、邻片密度、离圈距离和向外碰撞的角度。翻面局优先寻找适合掌拍或邻片掀动的落点；出圈局使用斜擦，从内侧向边界推进。精度仍受到角色熟练度和已保存的随机序列影响，决策只控制出手刚体。

练习反馈依据实际碰撞、移动、最低法线和停稳结果：没碰上时提示找落点；曾翻过去却落回时提示收一点力道/旋转；贴圈时提示从内侧向外；连续成功与多张收片有不同反馈。超长出手临时增加的阻尼会在下一手恢复为创建时参数，避免后续回合手感无意改变。

蓄力、发射、不同材质碰撞、收片和连得已使用新的 `SummerAudio` 接口。取消蓄力、暂停和失焦关闭蓄力反馈；取消不会重置回合，也不会移动飞行中的刚体。出手片可显示库存磨损与珍藏标记，场上练习目标使用轻度外观磨损。

## 接入 API

```csharp
Pogs.PlayerCardWear = selected.wear;
Pogs.PlayerCardTreasured = selected.treasured;
Pogs.Begin(card, npc, rule, training, seed, PracticeLayout.Fan);
Marbles.Begin(seed, MarbleLayout.Crescent);
Pogs.CancelAim();
Marbles.CancelAim();
```

新增参数均有 `Classic` 默认值，已有调用保持兼容。`Layout` / `LastFeedback` 为公共只读属性；玻璃球另有 `LastShotGain` / `BestScoringStreak` / `TargetBodies` 供显示与验证。

打片 `BoardState.version=2` 保存阵型、反馈和连续回合信息；版本 0 / 1 视为 `Classic`，依照原有五个姿态恢复，未知未来版本会拒绝读取。协调器恢复后应同步 UI 选择到 `Pogs.Layout`。没有修改收藏存档 schema。

玻璃球提供可选 `CaptureBoard()` / `RestoreBoard()`，保存八颗球、阵型、剩余手数、得分和连得信息。飞行或拖拽中不能捕获；已结束快照不会再次触发 `Completed`。这些接口尚不代表协调器已经接入跨进程玻璃球续局。

## 验证

八种阵型的几何检查通过：目标数量、圈内余量、成对间距均合格，默认坐标与已校准位置严格一致。所有运行脚本已用 Unity 自带 Roslyn 和原生程序集直接编译通过，随后由 Unity 导入与构建原生 Windows Player。

原生验收已通过默认掌拍三手基线、真实出圈赌片结算、每阵一次真实出手与停稳、版本 0 / 1 / 2 与未来版本拒绝、八份稳定快照无损恢复、八次飞行中取消/失焦/暂停，以及风车阵完整六手结算和成绩重载。四个 NPC 的完整难度分布与人工鼠标手感仍需后续试玩调校。

`RuntimeProbe` 已加入八布局独立真实出手、稳定快照恢复、旧版本/未来版本、飞行取消与失焦暂停检查，以及界面控件/音景观察。`--smoke-test --polish-smoke` 可单独运行新增验收，详见 [原生验收说明](QA_POLISH.md)。修复后的报告 `Logs/QA/Polish/runtime-report.json` 为 `completed=true`、`errors=[]`；非默认整局见 `Logs/QA/Pinwheel`。
