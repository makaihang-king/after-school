# 协作看板（所有 AI 代理开工前先读这里）

项目：《放学后》Unity 打圆片游戏，当前 Windows 0.5.6、Android 0.5.3。
参与方：Codex Desktop（用户桌面端会话）、ZCode（CLI 侧，共享会话 `01a10734-7891-8e7d-674237df01bc`）。用户通过两者对话。

## 分工（2026-10-04 双方商定）

- 美术视觉：42 张圆片卡面重绘、纸板材质、`Summer.uss` 的 `toy-*` 温暖微缩玩具风 —— Codex Desktop。
- 音乐：口琴慢节奏（街区约 80 BPM、比赛约 88 BPM），曲目就绪后 `SummerAudio` 接线归 ZCode。
- C# 逻辑：按下面的文件报备制流转。

## 文件所有权与进行中

- **追加已完成（Codex Desktop，2026-10-05，用户明确要求手机/电脑版取消无进展出手文字）**：`Assets/Scripts/Gameplay/PogMatch.cs`（仅无得分回合反馈/高亮与旧快照提示，保留物理和计分）、`Assets/Scripts/QA/RuntimeProbe.cs`（针对无得分出手的运行验收）、`Assets/Scripts/QA/MobileStartupProbe.cs`（卡片内部按钮边界验收）、`Builds/Windows-v0.4/`（重新构建电脑版）。手机/桌面真实出手及带旧失败文字快照恢复均通过，教学独立指引、得分庆祝、存档和退出门控保留。

- **已完成（Codex Desktop，0.5.3 手机 UI 重做，2026-10-05，本地验证完成，真机待验）**：`Assets/Scripts/UI/SummerUIMobile.cs`（缩放、通用导航、比赛与设置）、`Assets/Scripts/UI/SummerUIMobileHome.cs`（新增：首页/铁盒分页）、`Assets/Scripts/UI/SummerUIMobilePages.cs`（新增：约战/故事/杯赛/商店/跑腿）、`Assets/Scripts/UI/SummerUI.cs`、`Assets/Scripts/UI/SummerUIV04.cs`（仅手机分支入口与暂停/结果）、`Assets/Resources/UI/Summer.uss`（手机视觉）、`Assets/Scripts/QA/MobileStartupProbe.cs`（手机布局与交互验收）、`Assets/Editor/SummerAndroidBuild.cs`（版本）、`docs/ANDROID.md`、`README.md`。存档、物理、赌注和退出门控保持；所有新版手机页面独立于桌面布局，详细验收见下面 0.5.3 记录。

- **进行中（Codex Desktop，0.5.2 Android 原始模型裁剪修复，2026-10-05）**：`Assets/link.xml`、`Assets/link.xml.meta`（保留 CreatePrimitive 所需组件）、`Assets/Scripts/World/CrowdActors.cs`（仅 Shape 创建与组件检查）、`Assets/Editor/SummerAndroidBuild.cs`（版本）、`Assets/Scripts/QA/MobileStartupProbe.cs`（人物模型验收）、`docs/ANDROID.md`、`README.md`。截图已定位 Shape 材质赋值失败；原 0.5.1 Android 裁剪产物确实缺少 CapsuleCollider，后续构建必须检查 native 注册与裁剪后程序集。

- **补充报备（Codex Desktop，Android 启动续接，2026-10-05）**：`Assets/Scripts/UI/SummerUIV04.cs`（统一手机设置分支与显示实际应用版本）、`Assets/Resources/UI/Summer.uss`（仅手机设置面板宽度）。

- **进行中（Codex Desktop，Android 启动修复续接，2026-10-05）**：`Assets/Scripts/SummerGame.cs`（仅启动诊断与失败页字体，不修改退出门控）、`Assets/Scripts/UI/SummerUIMobile.cs`、`Assets/Scripts/UI/SummerUI.cs`（手机初始化与字体所有权）、`Assets/Scripts/QA/MobileStartupProbe.cs`（新增）、`Assets/Scripts/QA/RuntimeProbe.cs`（仅探针启动互斥）、`Assets/Editor/SummerMobileQABuild.cs`（新增）、`Assets/Editor/SummerAndroidBuild.cs`（修复包版本）、`Tools/build-android.ps1`、`docs/ANDROID.md`、`README.md`。

- **进行中（Codex Desktop，Android APK，2026-10-05）**：`Assets/Editor/SummerAndroidBuild.cs`、`Tools/build-android.ps1`、`ProjectSettings/ProjectSettings.asset`、`docs/ANDROID.md`、`README.md`（打包）；`Assets/Scripts/Gameplay/PogMatch.cs`（仅输入区域，不改教学判定）、`Assets/Scripts/Gameplay/DisplaySettings.cs`（仅移动平台分支）、`Assets/Scripts/Mobile/MobileRuntime.cs`、`Assets/Scripts/UI/SummerUIMobile.cs`、`Assets/Scripts/UI/SummerUI.cs`、`Assets/Scripts/UI/SummerUIV04.cs`、`Assets/Resources/UI/Summer.uss`（仅手机样式与触屏操作）。移动分支修改已在此报备，保留退出门控、教学和旧类名约定。

- **已完成（ZCode，退出+存档流程）**：`SummerGame.cs`、`DisplaySettings.cs`、`SummerUI.cs`、`SummerUIV04.cs`、`SummerUIV05.cs`（新建）、`QA/RuntimeProbe.cs`（退出弹窗断言）。退出相关代码区域修改前先打招呼；`toy-*` 类名约定见 `SummerUIV05.cs`，USS 落地后自动生效。
- **进行中（Codex Desktop，分步教学）**：`Gameplay/GuidedPractice.cs`、`Gameplay/PogMatch.cs`、`Core/SaveRepository.cs`（`guidedPracticeCompleted`）。ZCode 不动这些文件的教学区域；完成后 ZCode 负责代码 review 与冒烟验证。

## 规则

1. 动手改文件前，把路径登记到上面"进行中"一节（写明谁、什么时候）。
2. `Tools/build.ps1` 构建后可用 `--smoke-test --v04-smoke` 跑产品冒烟；退出弹窗断言（`VerifyExitConfirm`）已内置，教学断言由教学作者补。
3. 旧类名（`overlay`/`result`/`body` 等）与 `toy-*` 类名并存：旧类保证当前样式，`toy-*` 是新版式 hook，两边都不许删对方的。
4. 存档相关改动注意 schema2 兼容：未来版本拒读、不回退覆盖。

## 0.5.3 手机 UI 与无进展提示收尾（Codex Desktop，2026-10-05，本地验证完成，真机待验）

- 首页六个大入口、铁盒六张分页、约战三步、任务/商店短卡分页、比赛左右操作与中间拖拽区、完整设置/暂停/结果/退出确认均已完成。两种横屏比例 `1600×720`、`1280×720` 各 `41/41` 个界面状态通过，卡片按钮不越界，中央输入采样各 `45/45` 通过。证据 `Logs/QA/Mobile-UI-053-1600x720-verified/` 与 `Logs/QA/Mobile-UI-053-1280x720-verified/`。
- 手机和电脑版普通未得分出手清空 `Message`、`LastFeedback` 与中央高亮，旧失败提示在续局时清理。最新正式电脑版分别运行两套 UI 的真实 PhysX 出手与当前格式快照恢复，均 `passed=true`、`errors=[]`，恢复后盘面/几何/比分/计数保持。证据 `Logs/QA/Desktop-Quiet-Shot-053-final/` 与 `Logs/QA/Mobile-Quiet-Shot-053-final/`。探针保持 v3 快照，不再强降 v2 或重新 Begin 来测试提示清理；存档防倒退保护和旧几何迁移未改。
- Windows 正式目录已更新，构建零错误零警告，原快捷方式可用；构建前完整旧目录保存在 `Builds/Windows-v0.4-before-quiet-shot-20261005/`。
- 最终 APK `Builds/Android-v0.5/after-school-0.5.3.apk`，`0.5.3/versionCode 4`，`75,695,528` 字节，构建时间 `2026-10-05T14:04:29.4380178Z`，零错误零警告；与 canonical APK 完全相同，SHA256 `36408add969b361db7f8324f46d2b2cc0db86df0bb1dd1f3ba5dd6cc33046316`，同名 `.sha256` 已更新。
- 正式静态验收 `33/33` 通过：同证书 v2 签名、API36/ARM64、ZIP 与全部六个 ELF 16 KB 对齐、CapsuleCollider 管理/native/APK 元数据关联通过。报告 `Logs/QA/Android-UI-0.5.3/android-package-validation.json`，独立证据 `Logs/QA/Android-UI-0.5.3/final-20261005T140815126Z/`。真机运行标记为 false，当前未连接红米 K70 / Android 16，不能将 Windows 手机分支验收视为 Android 真机通过。
- 本轮以上文件已释放编辑所有权；后续仍遵守旧 UI 类与 `toy-*` hook、教学独立指引、押片规则、schema2 存档与退出门控约定。

## 0.5.2 人物模型修复记录（Codex Desktop，2026-10-05，本地验证完成，真机待验）

- 用户 `0.5.1` 真机截图明确定位 `CrowdActors.Shape` 的胶囊模型渲染组件缺失；旧裁剪后 PhysicsModule 和 native 注册均缺少 CapsuleCollider。`Assets/link.xml` 保留六类原始模型组件，Shape 保持必需模型完整性检查。
- `Builds/Android-v0.5/after-school-0.5.2.apk` 为 `0.5.2/versionCode 3`，75,509,072 字节，构建零错误零警告、同证书 v2 签名与 16 KB 对齐通过。新管理程序集、native 注册及实际 APK 元数据都确认保留 CapsuleCollider；新包原生库与含注册符号产物 Build ID 一致。
- 人物原生验收确认 4 名人物、66 个胶囊网格及渲染组件完整，两次检查均通过，完整手机 UI 验收无错误。证据 `Logs/QA/Android-Primitive-0.5.2/`，运行报告 `Logs/QA/Mobile-Startup-20261005-v052/`。
- 本地修复和打包已完成；红米 K70 上的启动结果等待用户覆盖安装 0.5.2 确认。保留旧 0.5.1 裁剪证据于 `Logs/QA/Android-Primitive-0.5.1/`。

## Android 续接记录（Codex Desktop，2026-10-05，本地验证完成，真机待验）

- 新包 `Builds/Android-v0.5/after-school-0.5.1.apk`，`0.5.1/versionCode 2`；正式构建 `Succeeded`、0 errors、0 warnings，75,486,136 字节，签名与旧包一致，ZIP 和两个主要原生库 16 KB 对齐通过。
- 字体回退与资源所有权已收尾；首页明确使用 `hierarchy.parent` 检查物理滚动内容层级（Unity 6 的 `parent` 是逻辑父级）。手机设置面板加宽，界面显示实际版本，启动异常显示阶段、版本和完整调用栈，IL2CPP 保留文件行号。
- 未再引入“损坏存档自动重建”；存档回归 258 项通过。手机界面独立 Windows 原生验收全部通过，报告 `Logs/QA/Mobile-Startup-20261005-v051-verified/mobile-startup-report.json`；静态 APK 验收 `Logs/android-package-validation.json`。
- 上述续接文件本地工作已完成。用户设备为红米 K70 / Android 16，ADB 当前无设备；原始 Android `NullReferenceException` 的真机结果仍需新版安装确认或手机堆栈，不能将桌面手机 UI 验收当作真机启动通过。
- `Tools/build-android.ps1` 使用 UTF-8 BOM 兼容 Windows PowerShell 5.1，镜像三份 Unity 输入目录，核验新 APK 与摘要的生成时间和大小；未修改正式 Windows-v0.4 产物。

## 修复记录（ZCode，23:15，构建恢复）

- `SummerGame.cs` 的 `WaitForFlightThenQuit`：迭代器内普通 `return;` 是 CS1622 编译错误，已改为 `yield break;`。写协程后请用批处理编译自查：`Unity.exe -batchmode -quit -projectPath . -logFile Logs/compile-check.log`。
- `Summer.uss` 原 190 行 `filter: contrast() saturate()`：USS 滤镜着色器不进构建包，玩家端应用样式时空引用。已移除；卡面对比度请烘进贴图或用背景色近似。修复后 `--v04-smoke` 全绿。

## 进行中：0.5.4 可操作教学（Codex Desktop，2026-10-05）

- 本轮登记：Assets/Scripts/Gameplay/PogMatch.cs（共享屏幕拖拽入口与仅教学地面示意）、Assets/Scripts/Gameplay/GuidedPractice.cs（目标碰撞判定与结果停留）、Assets/Scripts/SummerGame.cs（仅教学继续/重摆）、Assets/Scripts/UI/SummerUI.cs、Assets/Scripts/UI/SummerUIV04.cs、Assets/Scripts/UI/SummerUIMobile.cs、Assets/Resources/UI/Summer.uss（仅教学按钮/力度反馈）、Assets/Scripts/QA/GuidedExperienceProbe.cs（新增）与 RuntimeProbe.cs（启动互斥）、Assets/Editor/SummerBuild.cs、Tools/build.ps1（独立候选构建）、README.md、docs/IMPLEMENTATION.md、docs/OPTIMIZATION_BACKLOG.md、CHANGELOG.md。
- 已确认当前无 Unity/游戏进程；原正式 Windows-v0.4 完整目录在候选版通过前保持原位，源码备份 Logs/Backups/20261005-teaching-054。不修改退出门控、生产 PhysX 参数、收藏和旧存档 schema。

- **已完成（Codex Desktop，0.5.4 可操作教学，2026-10-06，Windows 原生验收通过）**：`Assets/Scripts/Gameplay/PogMatch.cs`（共享屏幕拖拽入口、教学目标/动作示意与结果停留）、`Assets/Scripts/Gameplay/GuidedPractice.cs`（真实目标碰撞、失败重试与结果状态）、`Assets/Scripts/SummerGame.cs`（教学继续/重试）、`Assets/Scripts/UI/SummerUI.cs`、`Assets/Scripts/UI/SummerUIV04.cs`、`Assets/Scripts/UI/SummerUIMobile.cs`、`Assets/Resources/UI/Summer.uss`（教学 HUD、力道区和按钮）、`Assets/Scripts/QA/GuidedExperienceProbe.cs`（新增原生验收）与 `RuntimeProbe.cs`（互斥）、`Assets/Editor/SummerBuild.cs`、`Tools/build.ps1`（候选输出与占用保护）、README/docs/CHANGELOG。证据 `Logs/QA/Teaching-054-native-r3/`：`passed=true`、`errors=[]`、341 条断言、15 次真实停稳出手、9 张截图；数据回归 258 项通过。上一稳定目录保留于 `Builds/Windows-v0.4-before-teaching-054-stable-20261006/`，候选版已晋级 `Builds/Windows-v0.4/`。
- **已知未通过（环境限制）**：候选版 `--smoke-test --v04-smoke` 仅独占全屏在本机 DXGI 4 秒超时后回退到全屏窗口化，证据 `Logs/QA/Teaching-054-v04-smoke/runtime-report.json`；窗口化、全屏窗口化及其余 96 个环境/正式流程完成，不能把该次报告写成全量通过。

## 已完成：0.5.5 伙伴来到场边（Codex Desktop 自动化，2026-10-07 开始，2026-10-09 原生验收完成）

- 本轮登记：`Assets/Scripts/World/CrowdActors.cs`（按 NPC 身份固定衣着/体态、真实对手到场、招呼与专属动作）、`Assets/Scripts/World/CompanionPersonality.cs`（新增角色表现资料）、`Assets/Scripts/SummerGame.cs`（仅伙伴上下文与结算动作接线，不动退出门控）、`Assets/Scripts/UI/SummerUIV04.cs`、`Assets/Scripts/UI/SummerUIMobilePages.cs`（约战准备的一句伙伴招呼）、`Assets/Scripts/QA/CompanionExperienceProbe.cs`（新增完整原生验收）、`Assets/Scripts/QA/RuntimeProbe.cs`（仅探针互斥）、`Assets/Editor/SummerBuild.cs`、`ProjectSettings/ProjectSettings.asset`（Windows 版本）、README/docs/CHANGELOG 和 `Tools/ASSET_PROVENANCE.md`（真实来源记录）。
- 已确认当前无 Unity/游戏进程，源码原样备份 `Logs/Backups/20261007-companions-055/`。候选输出 `Builds/Windows-v0.5.5-candidate/`，通过前不覆盖完整正式版。保留无得分出手安静、PhysX/计分、收藏实例、旧存档、教学和退出门控。
- 补充登记（2026-10-08 续接）：`Assets/Scripts/QA/MobileStartupProbe.cs` 仅增加伙伴验收/手动隔离验收的启动互斥，避免两探针同时导航；不扩展 Android 构建。

- 完成证据 `Logs/QA/Companions-055-native-r3/`：`passed=true`、`errors=[]`，166 条检查、14 次真实 PhysX 停稳出手、自然 2:2 平局与读盘、暂停/续局、挂起局认输身份、七位伙伴与走廊小雨教学通过。手机 UI 准备页两比例各125条检查通过，不等于Android真机；失败r1/r2保留。
- Windows 0.5.5 已晋级 `Builds/Windows-v0.4/`，完整 0.5.4 保留 `Builds/Windows-v0.4-before-companions-055-stable-20261009/`。原入口/快捷方式/原生窗口已检查，四份真实/旧存档SHA256保持，玩家显示/音量键和原生窗口偏好已恢复；本轮自启检查进程已释放，没有启动重模型。
- 本轮文件所有权已释放。新增原生验收说明 `docs/QA_COMPANIONS_055.md`、来源参数 `Tools/ASSET_PROVENANCE.md`，不修改存档 schema、正式 PhysX/计分、教学判据和退出门控。后续不重复本轮通过检查，除非有新修改、失败或疑点。

## 0.5.5 并发避让（Codex Desktop 自动化，2026-10-09）
- 本 chat `01a11ec2-7ab2-7a31-88af-5f82d5a163bb` 初次列表显示旧 chat `notLoaded`，但后续 `wait_threads` 确认 `01a115eb-be56-7261-915d-721216e92bce` 已恢复为 active，正在完成同一组伙伴代码。已停止本 chat 及 subagent 的源码编辑与构建，释放上述续接收尾所有权；本 chat 对 CrowdActors 的 patch 验证失败，未落盘，不恢复或覆盖旧 chat 已写的新动作。前期备份保留 `Logs/Backups/20261009-companions-055-completion/`。
- 四份真实与旧存档 SHA256 保持上一轮基线。本 chat 未启动 Unity、游戏或模型，未覆盖构建。审查建议：伙伴验收须比较真实关节变化/暂停冻结/动作结束；手机准备页需检查新增招呼是否把对方赌注挤到滚动区域外，目前仅代码风险、尚无新版原生复现。


## 已完成：0.5.6 自由站位与片角（Codex Desktop，2026-10-09，用户直接要求）
- 本轮登记：Assets/Scripts/Gameplay/PogMatch.cs（360°出手站位、任意方向拖拽、片角真实物理姿态）；Assets/Scripts/World/SceneWorld.cs（绕场镜头、俯仰/缩放）；Assets/Scripts/SummerGame.cs（镜头切换取消蓄力）；Assets/Scripts/UI/SummerUIV04.cs、Assets/Scripts/UI/SummerUIMobile.cs、Assets/Scripts/UI/SummerUI.cs、Assets/Scripts/UI/SummerUIShotAngles.cs（新增）、Assets/Resources/UI/Summer.uss（站位/片角操作）；Assets/Scripts/QA/FreeAngleExperienceProbe.cs（新增）、RuntimeProbe.cs、MobileStartupProbe.cs（启动互斥）；Tools/free-angle-native.ps1（隔离原生验收）；Assets/Editor/SummerBuild.cs、ProjectSettings/ProjectSettings.asset（Windows 0.5.6）；README/docs/CHANGELOG。
- 源码原样备份 Logs/Backups/20261009-free-angle-056/。本轮开始确认无用户 Unity/游戏进程。独立候选 Builds/Windows-v0.5.6-candidate/，通过前保留正式0.5.5完整目录。保留普通未得分安静、收藏/旧存档兼容、计分与退出门控；只扩展玩家实际出手角度，不增加胜负预设。
- 补充登记（Codex Desktop，2026-10-09，浮层恢复接线）：Assets/Scripts/UI/SummerUIV05.cs 仅在退出确认打开/关闭时调用本轮 UpdateShotSetupUI，确保站位/片角按钮按现有时间与阶段刷新；不修改退出确认、等待、保存门控及原时间恢复逻辑。
- 完成登记：原生主报告 `Logs/QA/FreeAngle-056-native-r11/` 为 passed=true、errors=[]，385 项检查、10 次实际停稳出手、自然比分 1:3、30 张截图；两种手机 UI 各 63 项通过，教学回归 252 项通过。构建 0 errors / 0 warnings，候选已完整晋级 `Builds/Windows-v0.4/`，上一稳定版保留在 `Builds/Windows-v0.4-before-free-angle-056-stable-20261009/`。候选与正式目录 142 个文件路径及 SHA256 全部一致；正式入口、开始玩.cmd 和两个桌面快捷方式沿用原目标。
- 可见窗口会话正常退出、玩家偏好恢复、真实收藏与旧存档 SHA256 不变；本机桌面自动化没有返回候选窗口，因此人工按键/拖拽清单明确记录为未完成。该限制见 `Logs/QA/FreeAngle-056/manual-inspection-status.json`，不能解读为原生自动验收失败或人工试玩通过。未启动重模型、未构建 Android APK、未发送外部消息、未部署发布；0.5.6 文件所有权已释放。
