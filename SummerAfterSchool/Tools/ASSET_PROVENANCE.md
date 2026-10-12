# 原型资源来源与复现

本工程的资源全部为本次原创制作。圆片、场景材质和音效为程序化原创素材，三首正式 BGM 由用户已经部署的本地 ACE-Step 模型生成，标题背景由本地 Qwen Image 2.1 生成；不把程序素材标成 AI 成果。没有下载模型，没有引用他人品牌人物或商业音乐。

### 0.5.6 自由站位与片角（2026-10-09）

- 本轮没有新增图像、音频、字体或模型资源；没有启动 Qwen Image、ACE-Step 或其他重模型，也没有自启模型进程需要释放。
- 改动全部是现有 C# 场景相机、出手几何、UI 控件和原生验收代码。实际对局验收使用种子 `2225750`，完整运行参数与真实结果见 `Logs/QA/FreeAngle-056-native-r11/free-angle-experience-report.json` 与同目录 `native-run.json`。
- 桌面控制通道未返回候选游戏窗口，单独记录在 `Logs/QA/FreeAngle-056/manual-inspection-status.json`；没有把人工操作编造成模型或资源来源。

## 圆片与场景材质

### 0.5.5 伙伴身份与动作（2026-10-09）

- 来源：本工程 C# 原创程序化模型与关节动画，`Assets/Scripts/World/CrowdActors.cs`、`CompanionPersonality.cs`；没有新增图片、录音、配音或生成动画素材。本轮未启动 Qwen、ACE-Step 或其他重模型，不存在本轮模型进程需要释放。
- 每个 NPC 的颜色、身高、体宽、动作种类、反应延迟和问候语固定在 `CompanionPersonality.Profile`。场边序号与访问变体只改变站位/围观成员，不改变身份资料。
- 动作参数：招呼 2.6 秒，结果回应 3.2 秒，0.25 秒进入、0.45 秒离开；小雨挥手、大志抬拳、桃桃画弧、林林点头。点头目标偏移最多 9 度，使用目标姿态插值；模拟时钟使用 `Time.deltaTime`，暂停冻结。
- 随机种子：此人物表现无随机生成，`Phase = Style * 1.84` 为固定相位。对局仍沿用真实保存的 `randomSeed`，本轮原生验收会将实际种子写入报告。
- 既有圆片、音乐和音效文件保留，来源与种子仍为下面的原始记录；本轮没有把旧资源标为新生成成果。

- `Assets/Resources/Art/Pogs/pog_00.png` 至 `pog_41.png`：768 × 768、RGBA、42 个独立角色图案。旧 30 个角色和 ID 保留，0.4 新增“街坊小英雄”“花纹游戏标”“校园冒险队”三个原创系列，共 12 张，其中四张为 Boss 招牌片。名称与 `Assets/Scripts/Core/GameCatalog.cs` 的 `artIndex` 一致。米色纸边、环形游戏标、系列编号、复古网点、中文楷体标签。
- `Tools/pog_contact_sheet.jpg`：全部圆片的检查图。
- `Assets/Resources/Art/Pogs/pog_back.png`：同尺寸的米色纸背，中文“放学后·属于我们的夏天”与双色太阳印记，方便观察实际翻面。
- `Assets/Resources/Art/Surfaces/brick.png`、`wood.png`、`plaster.png`、`stone.png`、`cloth.png`：512 × 512 循环材质。
- 复现：用带有 `Pillow`、`numpy` 的 Python 执行 `Tools/generate_prototype_assets.py --only art` 和 `Tools/generate_surface_textures.py`。
- Windows 系统字体用于原型中文标签：`C:/Windows/Fonts/msyh.ttc`、`simkai.ttf`。字体文件没有拷贝进贴图库。

## 游戏反馈音效

`slap`、`smash`、`skim`、`flip`、`win`、`lose`、`ui`、`ambience` 均为原创合成，44.1 kHz 双声道 PCM WAV。`ambience` 包含低音量风声、蝉声、鸟鸣，不含他人录音。

`generate_prototype_assets.py --only audio` 会重新生成这些音效，也会将主 BGM 重新生成为程序作曲版本。若已有 AI BGM，重新生成音效后需再次运行 AI 配乐脚本或保留主 BGM。

细化音效使用 `Tools/generate_audio_polish.py`。该脚本只新增 64 个原创程序合成 WAV，并校验主 ACE 配乐 SHA-256 不变：

- 纸、金属、玻璃、木、软胶细碰各 4 个样本；掌拍、重拍、斜擦、翻片、UI、收片、胜负提示各 4 个样本。
- 连击 2 至 6 级提示、稀有收片短和声、2 秒柔和蓄力循环。
- 28 秒院子远鸟、老槐树蝉声与落叶、胡同步行与远处自行车铃、校门远脚步与轻球声循环，以及独立微风底层；不合成或使用他人人声。
- 文件为 44.1 kHz 双声道 PCM16，混音刻意减弱刺耳的高频泛音。循环接缝用短连续桥接，保持背景连续。
- 来源、seed、长度、幅度、循环标记和文件 SHA-256 记录在 `Tools/audio_polish_manifest.json`。所有这些资源均为程序音效，不是 AI 生成。

## 本地 AI 配乐

- 主 BGM：`Assets/Resources/Audio/summer_loop.wav`、`summer_match.wav`、`summer_collection.wav`，分别用于街区探索、圆片比赛和铁盒收藏。
- 模型：`D:/AI/ACE-Step-Models/acestep-v15-turbo` + `acestep-5Hz-lm-0.6B`。
- 环境：`D:/AI/ACE-Step-1.5/.venv/Scripts/python.exe`。
- 复现：按曲目执行 `D:/AI/ACE-Step-1.5/.venv/Scripts/python.exe Tools/generate_local_bgm.py --track summer_loop`（其余曲目替换 `--track` 参数）。
- 参数：每首原始 48 秒、48 kHz、8 步、int8、CPU offload、纯器乐；街区 / 比赛 / 收藏分别为 80 / 88 / 74 BPM，seed 为 `2026100401` / `2026100402` / `2026100403`。提示词与负面提示完整保存在 `Tools/LocalAI/BGM/generation_manifest.json` 及各曲目的 `prompt_spec.json`。
- 输出处理：48 kHz 双声道 16-bit、1.6 秒等功率循环叠合，游戏主文件 46.4 秒。原文件、提示词和完整参数保存在 `Tools/LocalAI/BGM`。
- 后备：`summer_loop_procedural.wav`、`summer_collection_procedural.wav`，本次原创程序作曲；没有冒充本地 AI 配乐。旧版回退文件在新资源验收通过前保留。

## 声音控制与验证

`SummerAudio` 按地点与 `AudioMood` 切换背景，音乐 2.2 秒、地点环境 1.9 秒双声部交叉淡化。声音变体不连续重复；碰撞短暂汇总最大力度，收片与连击合并同帧的最高级别。10 个效果声部使用优先级和每类/每秒限流，蓄力取消、发射或停止刷新后自动退出。

总音量、音乐、音效、环境及静音分别保存在本机 `PlayerPrefs` 的 `Summer.Audio.` 前缀下，拖动音量时平滑改变，停止拖动约 450 毫秒后再保存。`--smoke-test` 验收进程独立使用 `Summer.Audio.Smoke.` 前缀，不修改用户平常的音量键。

`Tools/verify_audio_polish.py` 验证所有 WAV 可读、无削波/异常直流、13 类样本各自不同、循环边界在正常采样步幅范围内，且旧版 BGM 回退文件保留。本次 77 个 WAV、9 个正式循环均通过，报告为 `Logs/QA/Audio/audio-waveform-report.json`。`SummerAudio` 已用 Unity 自带 C# 编译器及真实 Core/AudioModule 程序集验证编译；音画与实际玩法由统一运行验收负责。

## 本地 AI 标题图

`Tools/generate_local_keyart.py --start-service` 使用 `D:/AI/ComfyUI_windows_portable/python_embeded/python.exe`、既有的 Qwen Image 2.1 int4 + `qwen3vl_8b_w4a8` + Qwen VAE。

脚本在独立的 `http://127.0.0.1:8191` 启动只属于本次生成的隐藏 ComfyUI 进程；成功或失败后均关闭自己启动的进程。已有同端口服务会复用，不主动关闭。完全离线，不下载。生成目标为 `Assets/Resources/Art/summer_keyart.png`；以 `Tools/LocalAI/KeyArt/generation_status.json` 的 `success` 确认是否成功。

本次标题图已成功生成并视觉检查，1152 × 640、25 步、seed `2026100301`，左侧浅墙给标题与菜单留白，右侧为葡萄架下三个孩子与圆片。来源原图与 API 工作流均保留；自己创建的模型服务已关闭。

## 检查与接口

Unity：`D:/GameDev/Unity/6000.3.25f1/Editor/Unity.exe`。本地模型服务最初没有运行；现有用户启动方式：ComfyUI `8188`，ACE-Step Gradio `7860`，ACE-Step REST `8001`（健康检查 `/health`、提交 `/release_task`、查询 `/query_result`）。本原型并不依赖运行中的模型服务，生成后的资源均能离线播放。
