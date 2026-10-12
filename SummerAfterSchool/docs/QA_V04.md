# v0.4 交付与验收

更新日期：2026-10-04。运行文件为 `Builds/Windows-v0.4/放学后.exe`，工程使用 Unity `6000.3.25f1`。

**0.4.0 验收通过：258 项数据回归、18 项原生存档、三模式六个进程重启、完整同档八故事及三轮杯赛、顶层提示与交易补给、24 套场景、96 组环境、388 项薄片压力全部通过。运行包与两个快捷方式已更新，最终发布证据见文末。**

## 批准内容与实际规则

### 八关故事

故事与锦标赛进度独立。故事每关首次胜利解锁下一节点，失败可以重试；所有正式对局均由双方各押一张真片，结算转移约定的原实例。

| 关卡 ID | 地点 | 对手 | 规则 | 类型 | 首通玻璃珠 | 首次 Boss 招牌 |
| --- | --- | --- | --- | --- | ---: | --- |
| story_01 | 外婆小院 / home | 小雨 / xiaoyu | 翻面 | 普通 | 4 | — |
| story_02 | 小区广场 / square | 豆豆 / doudou | 打出圈 | 普通·一场误会 | 4 | — |
| story_03 | 小学门口 / school_gate | 阿岳 / ayue | 翻面 | 地区 Boss | 8 | pog_30 月牙护校将 |
| story_04 | 教学楼走廊 / corridor | 小雨 / xiaoyu | 翻面 | 普通 | 4 | — |
| story_05 | 汽水胡同 / alley | 桃桃 / taotao | 打出圈 | 地区 Boss | 8 | pog_31 红砖大将军 |
| story_06 | 小卖部门口 / shop | 大志 / dazhi | 打出圈 | 普通 | 4 | — |
| story_07 | 老槐树下 / old_tree | 林林 / linlin | 翻面 | 地区 Boss | 8 | pog_32 槐荫金蝉使 |
| story_08 | 校园操场 / playground | 阿砚 / ayan | 翻面 | 大 Boss | 16 | pog_33 夏日大王 |

八关首通奖励合计 56 颗珠。重玩不重复发章节首通奖励。每关完整开场、提示、胜负对白保存在 `Assets/Scripts/Core/CampaignCatalog.cs`；第二关包含误会、记号说明、约战及胜败友好和解。

### 八人杯赛

参赛者为玩家、小雨、大志、桃桃、林林、豆豆、阿岳、阿砚，共八人，签表有七场。玩家实际需胜三轮：

| 轮次 | 关卡 ID | 地点 | 对手 | 规则 |
| --- | --- | --- | --- | --- |
| 八强 | tournament_01 | school_gate | 阿岳 / ayue | 翻面 |
| 半决赛 | tournament_02 | alley | 桃桃 / taotao | 打出圈 |
| 决赛 | tournament_03 | playground | 阿砚 / ayan | 翻面 |

落败结束本届；平局不转片、不淘汰、不晋级，保持原轮次、对手和种子重新开局。三连胜冠军奖励 24 颗珠。重放同一结算不重复给奖或推进时段，新一届使用新的 `runId`。

故事或杯赛先击败某 Boss，都记录同一 Boss 首胜：第一次赢得 NPC 押出的招牌原实例，并解锁该图案商店兑换。另一条路线再遇到该 Boss 时押普通库存片，不重新复制原始招牌；之后付玻璃珠购买的同图案新实例记为 `Purchase`。

### 圆卡、交易与破产恢复

保留 `pog_00`–`pog_29`，新增 `pog_30`–`pog_41`，共 42 个稳定定义。新增系列如下：

| 系列 | 图案 |
| --- | --- |
| 街坊小英雄 | 30 月牙护校将、31 红砖大将军、32 槐荫金蝉使、33 夏日大王 |
| 花纹游戏标 | 34 竹蜻蜓小兵、35 葵扇守卫、36 水桶小侠、37 石狮巡街 |
| 校园冒险队 | 38 风筝双子、39 荷塘莲将、40 铜鼓使者、41 晚霞麒麟 |

| 稀有度 / 用途 | 买价 | 卖价 |
| --- | ---: | ---: |
| 常见真片 | 6 | 3 |
| 特别真片 | 12 | 6 |
| 稀有真片 | 24 | 12 |
| 传说真片 | 48 | 24 |
| 基础练习片 | 2 | 1 |

商店有 42 条真片报价及单独的基础练习片报价；同为 `pog_00` 的普通真片和基础练习实例通过 `isFoundation` 区分。所有片可交易，基础练习片及珍藏片禁止真局下注；珍藏交易必须确认本次报价，成交仍保留珍藏标记。对局中约定的实例锁定。伙伴按固定清单同稀有度一换一，每份清单每天可完成一次。

新档或首次旧档升级有 12 颗珠。空收藏与零余额均合法，读档或结算不会给玩家自动补片。跑腿顺序是接取 → 到指定目的地 → 交付，每次 6 颗珠，不要求持有圆片，足够买一张常见真片。NPC 普通真片不足可以在开局前补购具体实例供展示，不能复制唯一原始 Boss 招牌，也不向玩家免费补片。

每场结算（含投降、平局）与每次跑腿完成推进一个时段；访问地图、接跑腿和交易不耗时。休息推进一个时段。时段为上午、正午、下午、傍晚，傍晚后进入下一天上午。

## 存档和环境兼容

- schema2 默认目录：`Application.persistentDataPath/SummerAfterSchool-v2`。首次只读导入旧 `SummerAfterSchool` 的有效主档或备份，旧文件不改写、不移动；已有新版存档不重复导入或给予初始珠。
- 旧卡片 ID、实例 GUID、归属、历史、磨损、战绩、日期、pending、随机种子及停用的弹珠统计保留。保留 NPC ID，不因大志常驻地点变为 shop 而改动旧对局地点或旧归属。
- 校验通过的未来 schema 或封装版本明确拒读，不回退旧备份、不覆盖原文件。损坏档案的备份恢复继续保留损坏原档。
- snapshot3 保存环境、`visitVariant` 和 `geometryVersion`。新场景使用 geometry2；旧 snapshot0/1/2 和导入 pending 使用可实际重建的四处 geometry1，先建环境再恢复位置、朝向、分数和回合。
- 访问变体按 0/1/2 循环并持久化，改变摊位、摆设和路人，比赛几何与 Boss 锚点不变。每局冻结位置、日期时段、天气、天气种子和摩擦，暂停、换地图或续局都不重抽。
- 时段推进时抽取下一环境天气：晴天 50%、多云 30%、小雨 20%。户外普通地面雨摩擦系数乘 .90，井盖乘 .85；shop/corridor 保持干燥。重复应用天气不得累乘摩擦。
- 卡片转移、章节奖励、杯赛进度、钱包、时段与下一天气在一次原子结算中提交；交易和跑腿分别有幂等收据。主动卖出/换出不显示为赌注丢失。

## 可复现验收

以下命令在项目根目录执行。示例输出目录使用 `Final` 名称，最终证据栏应填写实际本轮路径，不能以示例命令或报告存在本身作为通过依据。

### 数据层

需要 .NET 10 SDK，链接生产 Core 文件，以 JSON 字段序列化替身模拟 Unity 内联 null；真实创建临时存档并测试文件锁、备份及失败回滚。

```powershell
dotnet run --project Tests/DataRepositoryHarness/DataRepositoryHarness.csproj --configuration Release
```

本轮实际输出：`PASS: 258 data-layer invariants checked.`。测试覆盖批准目录与价格、章节与冠军奖励、连续平局重赛、跨路线 Boss 首胜、原实例转移、珍藏确认、每份清单每日换片、破产恢复、旧真实/练习 pending 迁移、未来版本拒读、1–12 张赌注上界、钱包和日期整数边界、冻结雨天与室内干燥、NPC 普通片补购。全部临时文件位于唯一测试子目录，默认导入测试也重定向 persistentDataPath，不接触用户收藏。

### Unity 原生 JsonUtility

Unity 菜单「放学后 → 验证原生收藏存档」，或关闭同工程 Editor 后执行：

```powershell
& 'D:\GameDev\Unity\6000.3.25f1\Editor\Unity.exe' -batchmode -quit -projectPath (Get-Location).Path -executeMethod Childhood.Editor.SaveRepositoryValidation.Run -logFile 'Logs/native-save-v04.log'
```

报告为 `Logs/QA/Data/native-save-report.json`。使用独立存档，检查原生内联 class 序列化、开局、暂停续局、结算清除和幂等；其结果与数据替身测试分别记录。

### Windows 真正运行态

```powershell
& '.\Builds\Windows-v0.4\放学后.exe' --smoke-test --v04-smoke --qa-output 'Logs/QA/V04Final' --qa-save 'Logs/QA/V04Final/save'
& '.\Builds\Windows-v0.4\放学后.exe' --smoke-test --campaign-ui-test --qa-output 'Logs/QA/CampaignV04Final' --qa-save 'Logs/QA/CampaignV04Final/save'
& '.\Builds\Windows-v0.4\放学后.exe' --world-environment-test --qa-output 'Logs/QA/WorldV04Final'
& '.\Builds\Windows-v0.4\放学后.exe' --arena-test --qa-output 'Logs/QA/ArenaV04Final'
```

- `--smoke-test --v04-smoke` 使用独立测试收藏与显示偏好，通过实际游戏流程验收练习片限制浮层、故事真局及续局、买卖收据、跑腿和杯赛淘汰；验证三种显示模式及真实时间 15 秒超时恢复。报告 `runtime-report.json`，附原生截图。
- `--smoke-test --campaign-ui-test` 从全新独立收藏通过真实按钮逐关开局、实际 `Launch` 与 PhysX 停稳计分；不调整目标姿态、得分、NPC 技能或结算结果。报告逐手瞄准、出手方式、目标足迹高度、通道阶跃及失败重试。玩家会根据台阶、目标高度和连续失误改用掌拍或重拍，不能把挡在砖前的斜擦当成圆片穿模。
- `--world-environment-test` 不创建 SaveRepository 或 SummerGame，验收八地点 × 三变体的真实渲染差别、相同 Context 重建一致、固定比赛几何和资源释放；八地点 × 四时段 × 三天气共 96 个环境组合，以及四处旧几何。报告 `world-report.json`，附截图与真实材质摩擦观察。
- `--arena-test` 创建独立 World/PogMatch 和测试圆片，逐 PhysX 步记录高度、边界、速度、旋转、碰撞和恢复次数。报告 `arena-report.json`、`arena-frames.csv` 和场地截图；穿入判据按片厚收紧为 `min(.006, Height*.4)`，低于表面的阈值 .006 m，边界余量阈值 .012 m。高能叠片保留 4 秒逐片检查，仍在自然倒伏的片继续观测到最多 8 秒，同一速度阈值下连续稳定 .35 秒才停表，不能调阻尼、冻结姿态或强制休眠。操场晴天叠片另重复三次。异常恢复必须单列，使用兜底不能写成纯 CCD 成功。

独立验收核对真实存档未变。物理压力与实际按钮、完整对局、玩家可读界面分别验证；完整八故事与三轮杯赛的实际通关证据见下表。

自动串行验收入口为 `powershell -ExecutionPolicy Bypass -File Tools/verify-v04.ps1`，每次创建独立输出和测试存档；可用 `-SkipCampaign` / `-SkipArena` 按需缩小范围。测试只终止自己创建的超时进程。三种显示模式分别由写入和无窗口覆盖参数的新进程回读，整组结束恢复测试前 Unity 原生窗口键，玩家的显示偏好保持不变。

## 本版最终证据栏

| 检查 | 当前状态 | 最终证据 |
| --- | --- | --- |
| 数据回归 | 已通过 258 项 | 本次 `dotnet run` 输出；测试说明 `Tests/DataRepositoryHarness/README.md` |
| Unity 最终构建 | 已成功，0 错误、0 警告 | `Logs/build-summary.json`；运行文件 `Builds/Windows-v0.4/放学后.exe`，0.4.0 |
| Unity 原生存档 | 已通过 18 项，0 错误 | `Logs/QA/Data/native-save-report.json`；含本机真实旧档的全部 41 个原实例与旧成绩保留，原文件逐字节未变 |
| 三模式真实进程重启 | 已通过 6/6，0 错误 | `Logs/QA/DisplayV04Pass9/<模式>-write/readback/display-startup-report.json`：正式配对全部没有窗口覆盖参数；两个全屏 2560×1600、窗口 1280×720，记忆窗口尺寸一致，正常玩家偏好未变。生产退出前释放独占显示，下次按自有已确认偏好恢复 |
| 显示与顶层确认、买卖、跑腿、续局 | 已通过，0 错误 | `Logs/QA/ProductV04Pass9/runtime-report.json`：三模式、三次保存后组件销毁重建、暂停 15 秒恢复；真实 UI 买/卖/换按钮、跑腿、冻结环境续局、杯赛淘汰 |
| 完整八故事、三轮杯赛真实通关 | 已通过，同一新档，0 错误 | `Logs/QA/CampaignV04Pass7/campaign-report.json`：84 次真实出手，17 场实际结算，8/8 故事、3/3 杯赛、四 Boss 原招牌、56/24 奖励及钱包均通过 |
| 24 访问变体、96 环境组合、旧几何 | 已通过，0 错误 | `Logs/QA/WorldV04Pass7/world-report.json`：24/96、旧四场、哈希复现、幂等摩擦、资源释放；包含小卖部两块中央砖接地缓坡修复。24 张场景图已复核，修复后小卖部三变体另作目视检查 |
| 八场真实物理压力及异常恢复 | 已通过 388 项，0 错误 | `Logs/QA/ArenaV04Pass4/arena-report.json`：8 地点×晴雨，34 次真实出手、116075 物理步。操场晴天三次叠片各在 4 秒内自然停稳。毫米峰值、恢复次数见下方汇总；使用兜底单列，生产物理源未因验收调整 |
| 正常入口与桌面快捷方式 | 已更新并核对，0 错误 | `Logs/shortcuts-v04.json`：桌面及工作区两个快捷方式均指向 v0.4，Arguments 为空；`Tools/launch-game.ps1 -DiagnosticsOnly` 完整运行库检查通过。正常启动脚本只带日志参数，显示重启证据见六进程测试 |

最终压力峰值：地面穿入 **4.76mm**，片间穿入 **2.96mm**，边界超出 **3.98mm**，均低于各片厚度对应的严格判据。各案例收尾合计 **310 次边界兜底、70 次穿入恢复**，明确保留在报告中；这不表示完全依靠 CCD、每一步都零交叠。没有为了验收改变生产阻尼、旋转、得分或休眠状态。

圆片视觉、碰撞、落地高度与惯性统一使用目录厚度的十倍：薄纸 `.014`、纸板 `.027`、闪膜 `.021`、软胶 `.031`、铁皮 `.019` 游戏单位，相比旧统一基准减薄约 58%。

同档双路线报告为 `Logs/QA/CampaignV04Pass7/campaign-report.json`：全新收藏、84 次真实出手、17 场实际结算、八故事和杯赛三轮全部通过，未使用得分姿态初始化。四张 Boss 招牌均为对方原始赌注实例，结算未额外创建卡片。章节奖励 56、冠军奖励 24；四次 6 珠购片后钱包 68，逐项核对为 `12 + 56 + 24 - 24`。实际失败、平局、淘汰和重试原样保留，全部合同、归属、时段及原生存档重载检查通过。

发布压缩包：`Builds/放学后-0.4.0-Windows.zip`，61,657,215 字节（约 58.8 MiB），包含完整 Windows 运行库和操作说明，排除 QA 存档、截图及日志。SHA256 为 `6DE3317EC222AB610AC30F6E8BA43CFF23A2F56CC338B90C0872D67B412E0F29`，封包清单见 `Logs/release-v04.json`；143 个标准 ZIP 条目与核心运行文件均核验通过。

日常入口为工作区 `开始玩.cmd` 或「放学后」快捷方式，目标 `Builds/Windows-v0.4/放学后.exe`。`Tools/launch-game.ps1` 仅指定日志参数，由游戏记忆显示模式；诊断日志为 `Logs/launcher.log` 和 `Logs/play.log`。旧启动编码/CRLF 修复保留，旧版本的 1280×720 强制窗口说明仅属历史记录。

旧 0.2/0.3 原生验收保留在 `QA_POLISH.md`、`QA_ARENA.md` 和 `docs/CHANGELOG.md`，不作为本版验收结果。
