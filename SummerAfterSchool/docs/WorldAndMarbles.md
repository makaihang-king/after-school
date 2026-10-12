# 街区布景、围观伙伴与弹玻璃球

## 场景坐标和接口

`SceneWorld` 的比赛地面固定为 `y = 0`，打片操作区域为 `x ∈ [-3, 3]`、`z ∈ [-2.7, 2.7]`。主比赛镜头位于 `(0, 8, -9)`，看向 `(0, 0.2, 1)`。所有装饰的 collider 在创建时禁用并移除，只有独立的 `FloorCollider` 负责真实地面碰撞，装饰不会遮挡瞄准射线。

- `BuildWorld(string locationId)` / `Rebuild(string locationId)`：重建街区，保持比赛原点不变。
- `SetCameraMode(WorldCameraMode mode, bool immediate = false)`：平滑切换 `Intro`、`Hub`、`Match`、`Collection`。`Intro` 会慢慢掠过街区。
- `Shake(float strength = .12f, float duration = .2f)`：有衰减的震屏。
- `Punch(Vector3 worldPoint, float strength = 1f)`：扬尘、纸屑与轻微震屏。
- `SetArenaBoundaryVisible(bool visible)`：显示或隐藏街区装饰粉笔圈。需要使用独立判分边界的玩法应隐藏它，回到街区可重新显示，避免两圈同时出现。
- `MainCamera`、`FloorCollider`、`SurfaceFriction`、`LocationId`、`IsBuilt`：主控制器可读取的状态。

四个正式地点为 `school_gate`（向阳小学校门、教学楼和宣传栏）、`alley`（汽水雪糕广告墙与小卖部）、`old_tree`（老槐树、砖花坛与长椅）、`home`（外婆家平房、春联和小院）。共同的街区细节包括红白遮阳棚、玻璃雪糕冰柜、汽水箱、自行车、花盆、路灯、晾衣、电线、雨水篦子、井盖、落叶、纸飞机和路边跳房子粉笔画。

材质、纹理与几何均由程序构建，不需要外部包。中文标牌优先读取 `Resources/Fonts/Chinese`，缺失时使用本机的 `Microsoft YaHei` 等动态字体。暖阳使用内置渲染管线的软阴影，天空采用 `Skybox/Procedural`。生成资源由 `SceneWorld` 在重建或销毁时清理。

## 细腻美术与纸片磨损

2026-10-03 的第二轮美术细化保留比赛原点、相机位置、视场角和全部 PhysX 参数。暖阳从偏橙调整为浅金色，阴影环境略带蓝灰，使奶油墙、肤色、翠绿槐叶和彩色玻璃使用更统一的色调。

槐叶簇使用一份共享的 480 三角面网格，轮廓有轻微自然起伏；`Resources/Shaders/FoliageWind` 在顶点阶段产生厘米级微风，同样作用于阴影投射网格，树影会随树冠轻微变化。只有树冠和花坛使用风动材质，木窗、黑板和汽水瓶保持静止。红旗的星星随旗面一起轻摆，巷口衣服沿用关节摇摆。两只小蝴蝶活动在圈外花盆旁，三片慢落的槐叶仅在左侧树池周围滑落，均无碰撞体。

`Resources/Shaders/WorldSignText` 使用 `ZTest LEqual`，宣传栏、门牌和招牌文字被树木及建筑正常遮挡，消除被遮住的牌子仍在前景浮出白字的现象。`WorldSignFontAtlas` 监听字体图集更新，将同街区文字共享材质指向最新中文图集，停用或销毁时解除事件。后方路灯的重复微型街牌已移除，临街街牌保留。

`SummerImageFinish` 自动挂到主相机，使用一个全屏 pass 完成克制的色调：极弱的冷色暗部、少量饱和度补偿、最亮处的柔和压缩与边缘轻暗。没有泛光、景深或模糊；画面中心和玻璃内芯保持清晰，UI 由原生 Panel 单独绘制。`Amount` 默认 `.7`，可设为 `0` 比较原始画面。材质在组件停用时清理，不分配额外长期 RenderTexture。落片扬尘颗粒与数量减小，纸屑改用不规则纤维贴图和独立旋转，震屏的垂直幅度收敛，避免冲击遮住片面。

`DiscBody.ConfigureAppearance(float wear, bool treasured = false)` 接受 `0…1` 的库存磨损，公开 `Wear`、`Treasured` 只读状态。`PaperDisc` 保留原印花贴图，通过边缘褪色、浅折痕、粗糙度变化与侧边层压纸纹表现纸片年代感，珍藏片增加细暖金包边。只调整原渲染材质，不改 48 边网格、凸碰撞体、质量、质心、摩擦、阻尼、发射位置或运动。主玩法在 `Spawn` 后传入库存值，即可在实机显示同一张片的磨损。

本轮已通过 Unity 所带 Roslyn 对全运行时 C# 的独立编译；未启动 Editor。新 Shader 的导入、最终色调和动画效果仍由统一 Windows 构建与原生截图验证。

第一轮完整 Windows 原生验证 `completed=true`、`errors=[]`，实际截图确认 UI 字体不受色调影响、猫眼内芯保持鲜明颜色、纸边和微风树冠正常渲染。根据截图另作三处局部修正：老槐树墙绘上移并略缩小，避开长椅靠背；校门宣传栏移到树干右侧的围墙前，保持边缘在 `x < -3.9`；“今日擂台”地面文字仅在 `Hub` / `Intro` 显示，比赛与收藏镜头隐藏，避免透过 HUD 重复出现。最终构建截图复验已通过，见 `Logs/QA/Visual`；没有改变交互中心或物理参数。

## 围观伙伴

`CrowdActors.Configure(SceneWorld, PogMatch, MarbleMatch)` 创建 4 位原创微缩伙伴，位置在圈外的 `x = ±3.48…3.72`、`z = 1.12…3.05`，不会挡住原点比赛区，也没有 collider。

儿童有独立的躯干、头部、肩肘、腿关节，细节包括眼白和瞳孔高光、眉毛、鼻耳、刘海、帽子或辫子、棉衣翻领与口袋、红领巾、书包背带、运动鞋鞋带与袜口。动作由少量关节旋转产生：日常呼吸与重心摇摆、间隔不同的眨眼、出手时专注前倾。目光会追随未收走且正在运动的圆片，闲时偶尔看看对面的伙伴；得分后四位伙伴按不同延迟拍手、轻跳或举拳，空手回合只作短暂轻歪头，不同时机械跳跃。掌拍出手时有短暂的手掌视觉动画，它不改变游戏碰撞或得分。`Cheer(float seconds = 1.6f)` 可以手动触发反馈；可选 `SetFocusPoint(Vector3 point, float seconds = 1f)` 强调特定落点，时间结束后恢复自动观察。

伙伴自动检测场景地点变化；重建时旧根物体先停用，再销毁，动态材质随后清理。所有基础网格共享 Unity primitive 网格，无每帧网格生成。暂停时动画停止。

## 打片的真实翻面与校准

打片以正式 `DiscBody` 的凸圆柱碰撞体和刚体角速度决定正反面，停稳时法线与世界向上的点积小于 `-.84` 才算完整翻面。气流在圆片边缘施加 `AddForceAtPosition(..., ForceMode.Impulse)`，力与作用点的偏移自然产生力矩，没有直接设定成功朝向。

2026-10-03 使用 Unity `6000.3.25f1` 和 `PhysicsScene.Simulate(.01f)` 做了 132 组引擎校准，包括薄纸片、普通厚纸片、铁片的冲量/作用点扫描、完整出手轨迹和连续回合。接触当帧的向上气流会被正压在目标上方的出手片吞掉，过大的冲量则可能转过两圈又落回正面。当前掌拍保留首次碰撞的位置，延后 `.095 s` 再按目标的实际位置施加气流；垂直冲量为 `target.Weight * (.125 + shotPower * .025) * falloff`，衰减指数 `.25`，作用点仍在圆片边缘。

启用插值的出手片使用 `Rigidbody.position` / `Rigidbody.rotation` 设置原生物理姿态，发射前调用 `Physics.SyncTransforms()`，确保从低位的 kinematic 准备状态切到空中出手状态时，首次碰撞不会误判在发射点。Windows 第 6 轮实际玩家验证已获得 `3 : 0` 的完整打片胜局，结果面板和围观伙伴拍手反馈均在原生截图中确认。完整方法、碰撞记录和参数表见 `Tools/PhysicsCalibration/findings.md`、`edge-impulse-report.json` 与 `calibration.log`。

## 第二种完整玩法：弹玻璃球

`MarbleMatch.Configure(SceneWorld, SummerAudio)` 后调用 `Begin(int seed = 97, MarbleLayout layout = MarbleLayout.Classic)` 开始。`StopMatch()` 停止并释放球体、材质和事件中间状态。公开状态包含 `Phase`（共享 `MatchPhase`）、`Power`、`IsDragging`、`Score`、`ShotsRemaining`、`Message`；有 `Changed` 与 `Completed(int score)` 事件，并通过 `BlocksInput` 避免鼠标在 UI 上误出手。

圈心为 `(0, 0, .55)`，半径 `1.85`。8 颗目标玻璃球半径 `.155`，大出手球半径 `.18`，初始位置 `(0, .181, -2.25)`。目标采用不重叠的错列阵形，彩色猫眼内芯、螺旋彩条和透明高光球壳独立渲染。外壳使用 `Resources/Shaders/MarbleGlass` 的 Fresnel 玻璃着色器：中心更透明，彩色边缘和很小的太阳反光保留球体轮廓，减少多层白色高光覆盖彩色内芯。此美术调整不改变球体碰撞与刚体参数。

向后拖拽蓄力，松手出击，右键或 `Escape` 取消。每局 6 次机会。出手仅赋初始线速度和滚动角速度，之后由 PhysX 处理碰撞、反弹与滚动阻力。当目标中心的平面距离大于 `圈半径 + 球半径`，且球在地面附近时，整颗球才算出圈。没有给目标施加辅助出圈力，也没有直接设置成功结果。已得分球退出物理并播放收集动画；所有球拿齐会立即进入结果等待，其他回合在球停止后开始下一次出手。

2026-10-03 的首轮 Unity `6000.3.25f1` Windows 玩家验证中，6 次自动出手拿到 7 / 8 颗，前 3 次累计得分分别为 2、5、7，后 3 次维持 7。该数据验证真实物理、得分与回合推进，不代表固定角度能够自动全收。场景与角色视觉以最终玩家截图为准。

最终补查等待 `.8 s` 的结算节拍后确实进入 `AppScreen.Result`，新的存档实例从磁盘读到 `marbleRounds=1`、`marbleBestScore=7`；原生报告 `marbleScorePersisted=true`、`errors=[]`。最终出圈打片局 `2:3`，赌片与约战状态正常结算。相应报告保存在 `Logs/QA/Runtime`。
