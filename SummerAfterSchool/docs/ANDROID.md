# Android APK

本地安装包由 Unity 6000.3.25f1 的 IL2CPP Android 目标生成，固定横屏，最低 Android API 25，目标架构为 ARM64。包名为 `com.summerstreetstudio.afterschool`。

当前手机版版本为 `0.5.3`，`versionCode=4`，目标 API 36。已知反馈设备是红米 K70、Android 16。安装同包名、同签名的此版本可以覆盖之前的 `0.5.x`，保留应用数据。文件名仍为 `after-school.apk`，以应用版本号确认安装的是新包。

本次交付另提供明确版本名 `Builds/Android-v0.5/after-school-0.5.3.apk` 与同名 `.sha256` 校验文件，内容和 `after-school.apk` 完全一致；旧版安装包保留。

构建前需要在 Unity Hub 为同一个编辑器安装 Android Build Support、SDK & NDK Tools 和 OpenJDK。构建命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\build-android.ps1
```

输出文件是 `Builds/Android-v0.5/after-school.apk`，构建摘要位于 `Logs/android-build-summary.json`。这是 Unity 的调试签名 APK，适合 USB 安装和本机验收；发布到应用商店前应改用正式 keystore，并按商店要求提高 `minSdkVersion` 和配置签名。

中文工程路径会自动同步到 ASCII 临时工程后打包。同步仅镜像 `Assets`、`Packages`、`ProjectSettings`，移除临时工程里已经从源目录删除的旧脚本，并保留 `Library` 构建缓存。脚本同时核验 Unity 退出码、构建报告和 APK 更新时间，避免将旧包误报成新构建。构建摘要同时记录版本号、版本代码、实际 APK 大小与生成时间。

手机版保留拖拽出手、停稳判定、存档和暂停流程，额外使用明确的单指触摸输入。显示模式选项在 Android 上隐藏，系统全面屏和安全区由 Android 管理。

## 0.5.3 手机布局与安静出手

手机页面使用独立横屏布局，以 `960 × 480` 为逻辑参考、按高度缩放，主操作按钮高 `52–56`，主要文字 `20–24`，次要文字 `18`。首页用六个大入口直接进入玩法；铁盒每页六张、三列两行，七页可访问全部 42 张；约战分为玩法、对手与场地、选片三步，并将开局操作固定在底部。故事和跑腿每页两卡、商店每页三卡，交易、地点和详细签表采用浮层。比赛操作置于左右两侧，中间保留拖拽出手区域，四个音量滑杆可同时看到。

手机与电脑版共用的普通对局结算现在会在未得分时清空 `Message`、`LastFeedback` 与中央高亮，继续旧约战时也清理旧失败文字。得分庆祝、回合状态和教学指引保留；物理、得分、存档字段、押片规则与退出门控不变。

最终 Android 包于北京时间 `2026-10-05 22:04:29` 构建完成，`0.5.3/versionCode 4`，零错误、零警告，`75,695,528` 字节。明确版本名文件与 canonical 文件经 SHA256 确认完全相同：

```text
Builds/Android-v0.5/after-school-0.5.3.apk
36408add969b361db7f8324f46d2b2cc0db86df0bb1dd1f3ba5dd6cc33046316
```

正式包静态验收 `33/33` 通过，报告 `Logs/QA/Android-UI-0.5.3/android-package-validation.json`，原始证据 `Logs/QA/Android-UI-0.5.3/final-20261005T140815126Z/`。确认同包名、同证书 v2 签名、目标 API 36、ARM64、ZIP 与全部六个 ELF 原生库的 16 KB 对齐。人物启动修复仍保留：`CapsuleCollider` 的管理类型、生成注册、原生符号与 APK 元数据检查均通过。

同一手机 UI 分支在 Windows 原生播放器上以 `1600 × 720`、`1280 × 720` 各完成 `41/41` 个界面状态检查，全部无错误；包括分页、卡片内按钮边界、真实交易预览、跑腿领取、三步约战、教学、押片对局、暂停和退出确认。每轮 `45/45` 个中央输入采样均不被 UI 遮挡，四名人物和 66 个胶囊网格完整。报告和截图位于：

- `Logs/QA/Mobile-UI-053-1600x720-verified/`
- `Logs/QA/Mobile-UI-053-1280x720-verified/`

最新正式电脑版另分别运行桌面和手机分支的真实 PhysX 未得分出手验收，两份 `shot-feedback-report.json` 均 `passed=true`、`errors=[]`。停稳事件、比分、空提示、空高亮、屏幕文字，以及带旧失败文字的当前格式快照恢复均通过；恢复后几何、盘面、计数与比分保持。证据位于 `Logs/QA/Desktop-Quiet-Shot-053-final/` 和 `Logs/QA/Mobile-Quiet-Shot-053-final/`。该快照提示兼容测试不替代旧几何迁移测试。

正式电脑版 `Builds/Windows-v0.4/放学后.exe` 已同步更新，零错误、零警告，原快捷方式继续使用；完整旧版备份保存在 `Builds/Windows-v0.4-before-quiet-shot-20261005/`。Windows 版本设置仍为 `0.4.0`，因此电脑版运行手机分支的截图角标不表示 APK 版本。

本轮未连接 Android 设备，红米 K70 / Android 16 的实际启动、触控和显示仍需安装新版确认；包静态校验与 Windows 手机 UI 验收不等于真机运行通过。

## 0.5.2 人物模型启动修复

用户提供的 `0.5.1` 真机截图将错误定位到“初始化街区人物”阶段，`CrowdActors.Shape` 为胶囊模型赋材质时渲染组件缺失。检查旧 Android 裁剪产物确认 `CapsuleCollider` 同时缺失于裁剪后 `UnityEngine.PhysicsModule.dll` 和 `UnityClassRegistration.cpp`。`GameObject.CreatePrimitive(Capsule)` 自动添加原生组件，其完整依赖不能仅由 C# 泛型调用推导。

`Assets/link.xml` 现在明确保留 `MeshFilter`、`MeshRenderer`、`BoxCollider`、`CapsuleCollider`、`SphereCollider`、`MeshCollider`。人物创建对可选装饰 collider 做空检查，对必需网格和渲染组件仍要求完整并提供明确错误，保留全部角色模型。

新包于北京时间 2026-10-05 19:50:29 构建完成，`0.5.2/versionCode 3`，零错误、零警告，`75,509,072` 字节。Cecil 和 native 注册对照确认 `CapsuleCollider` 从旧包缺失变为新版保留；APK 管理元数据含该类型，APK 内 `libunity.so` 与含注册符号的本轮原生产物 Build ID 一致。旧、新裁剪证据分别位于 `Logs/QA/Android-Primitive-0.5.1/` 与 `Logs/QA/Android-Primitive-0.5.2/`。

本轮独立手机界面原生验收通过：4 名人物、全部 66 个胶囊模型均有有效网格、渲染组件、材质与 shader；进入练习再返回后仍通过，报告 `Logs/QA/Mobile-Startup-20261005-v052/mobile-startup-report.json`。新 APK v2 签名与旧包一致，ZIP 和两个主要原生库的 16 KB 装载对齐通过。真机修复结果仍需红米 K70 安装新版确认。

## 0.5.1 初始化诊断与通用验证范围

`0.5.1` 保留随包中文字体、系统字体与内置字体回退；仅销毁运行时创建的字体，保留共享资源。手机首页通过 `hierarchy.parent` 确认元素实际位于 `ScrollView.contentContainer`，避免混用 Unity 6 的逻辑父级与物理层级。手机设置面板提供更宽的音量滑杆。启动异常页面显示应用版本、失败阶段和调用栈，并使用深色底板保证可读性；IL2CPP 调用栈保留方法、文件和行号。

存档仍遵守原有保护契约：主档不可读时尝试备份和临时档；所有候选不可读时明确报错并保留文件。不会因界面启动异常自动重建存档。Android 写入继续保留 `Flush` 和 `File.Replace` 的兼容回退。

手机界面可在独立 Windows 验收包中执行同一 UI 分支：

```powershell
# 构建独立验收包，不覆盖正式 Windows 试玩版。
& 'D:\GameDev\Unity\6000.3.25f1\Editor\Unity.exe' -batchmode -quit -projectPath . -executeMethod Childhood.Editor.SummerMobileQABuild.BuildMobileQA -logFile Logs/mobile-qa-build.log
# --smoke-test 和 --qa-save 保证使用独立存档。
& '.\Builds\Mobile-QA\after-school-mobile-qa.exe' --smoke-test --mobile-ui-test --qa-save '.\Logs\QA\Mobile-Startup\save' --qa-output '.\Logs\QA\Mobile-Startup' -screen-fullscreen 0 -screen-width 1600 -screen-height 720 -logFile '.\Logs\mobile-ui-smoke.log'
```

这个探针验证打包字体、主界面与提示层、首页滚动容器、手机设置、铁盒、练习比赛和触屏旋转/取消按钮，并生成运行报告与截图。它不替代红米 K70 的 Android 真机安装、IL2CPP 运行和触摸验证。设备未连接时，真机启动是否解决仍需安装新版确认；如继续报错，应保留新版异常页面的阶段和调用栈，或通过 USB 调试读取 `adb logcat -v threadtime -s Unity AndroidRuntime`。

2026-10-05 本地验证：存档数据层 `258` 项通过；手机界面独立原生验收 `passed=true`、`errors=[]`，两个面板、两次首页滚动检查、设置、提示、铁盒、练习与四个按钮回调均通过。报告及六张截图位于 `Logs/QA/Mobile-Startup-20261005-v051-verified/`。Windows 验收程序使用工程的 Windows 版本设置，因此截图角标的 `0.4.0` 不表示 APK 的版本；Android 构建入口单独设置为 `0.5.1`。

Android APK 于北京时间 2026-10-05 19:12:40 构建完成，`Succeeded`、零错误、零警告，实际大小 `75,486,136` 字节。`aapt2` 确认 `0.5.1/versionCode 2`、目标 API 36 与 `arm64-v8a`；APK v2 签名通过，与旧版证书相同。`zipalign` 的 16 KB 对齐检查及 `libunity.so`、`libil2cpp.so` 的 ELF `PT_LOAD` 16 KB 装载对齐检查通过。静态验收汇总位于 `Logs/android-package-validation.json`，真机启动结果仍未验证。
