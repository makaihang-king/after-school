using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    /// <summary>弹玻璃球：纯刚体碰撞，目标整颗越过圈外才算得分。</summary>
    public sealed class MarbleMatch : MonoBehaviour
    {
        public MatchPhase Phase { get; private set; } = MatchPhase.Waiting;
        public float Power { get; private set; }
        public bool IsDragging { get; private set; }
        public int Score { get; private set; }
        public int ShotsRemaining { get; private set; }
        public MarbleLayout Layout { get; private set; }
        public string LastFeedback { get; private set; }
        public int LastShotGain { get; private set; }
        public int BestScoringStreak { get; private set; }
        public string Message { get; private set; }
        public Func<Vector2, bool> BlocksInput;
        public event Action Changed;
        public event Action<int> Completed;
        internal event Action<string> PhysicsDiagnostic;
        int activeFingerId = -1;

        public const float RingRadius = 1.85f;
        public static readonly Vector3 RingCenter = new Vector3(0f, 0f, 0.55f);
        public static readonly Vector3 LaunchPosition = new Vector3(0f, 0.181f, -2.25f);

        sealed class BallData
        {
            public GameObject Object;
            public Rigidbody Body;
            public SphereCollider Collider;
            public bool Claimed;
            public float Radius;
        }

        readonly List<BallData> targets = new List<BallData>();
        readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        SceneWorld world;
        SummerAudio audioFx;
        Transform arena;
        BallData cue;
        LineRenderer aimLine, pullLine, cueHalo;
        Vector3 dragStart;
        Vector3 direction = Vector3.forward;
        Vector3 restCuePosition;
        float startDelay;
        float flightTime;
        float quietTime;
        float impactCooldown;
        int shotStartScore;
        bool awaitingResult;
        int roundSeed, scoringStreak, impactCount;
        float shotPower, maximumMovement;
        Vector3[] shotStartPositions;

        public IEnumerable<Rigidbody> TargetBodies
        {
            get { foreach (var target in targets) if (!target.Claimed) yield return target.Body; }
        }

        [Serializable] public sealed class BoardState
        {
            public int version, seed, score, shotsRemaining;
            public MarbleLayout layout;
            public string lastFeedback;
            public int lastShotGain, scoringStreak, bestScoringStreak;
            public PoseState[] balls;
        }
        [Serializable] public sealed class PoseState
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool claimed;
        }

        /// <summary>仅稳定回合可保存。协调器可按需接入；不更改当前存档 schema。</summary>
        public string CaptureBoard()
        {
            if (Phase == MatchPhase.Flying || IsDragging || targets.Count == 0) return null;
            var poses = new PoseState[targets.Count];
            for (int i = 0; i < targets.Count; i++) poses[i] = new PoseState { position = targets[i].Body.position, rotation = targets[i].Body.rotation, claimed = targets[i].Claimed };
            return JsonUtility.ToJson(new BoardState { version = 1, seed = roundSeed, score = Score, shotsRemaining = ShotsRemaining, layout = Layout,
                lastFeedback = LastFeedback, lastShotGain = LastShotGain, scoringStreak = scoringStreak, bestScoringStreak = BestScoringStreak, balls = poses });
        }

        public bool RestoreBoard(string json)
        {
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                var board = JsonUtility.FromJson<BoardState>(json);
                if (board == null || targets.Count != 8 || board.version != 1 || !Enum.IsDefined(typeof(MarbleLayout), board.layout) || board.balls == null || board.balls.Length != targets.Count ||
                    board.score < 0 || board.score > targets.Count || board.shotsRemaining < 0 || board.shotsRemaining > 6 || board.lastShotGain < 0 || board.lastShotGain > 8 ||
                    board.scoringStreak < 0 || board.scoringStreak > 6 || board.bestScoringStreak < 0 || board.bestScoringStreak > 6)
                    throw new InvalidOperationException("玻璃球阵型或回合快照无效。");
                int claimed = 0;
                foreach (var pose in board.balls)
                {
                    if (pose == null || !Finite(pose.position.x) || !Finite(pose.position.y) || !Finite(pose.position.z) || !Finite(pose.rotation.x) ||
                        !Finite(pose.rotation.y) || !Finite(pose.rotation.z) || !Finite(pose.rotation.w) ||
                        pose.rotation.x * pose.rotation.x + pose.rotation.y * pose.rotation.y + pose.rotation.z * pose.rotation.z + pose.rotation.w * pose.rotation.w < .1f)
                        throw new InvalidOperationException("玻璃球的位置或朝向无效。");
                    if (pose.claimed) claimed++;
                }
                if (claimed != board.score) throw new InvalidOperationException("得分与已收下的玻璃球数量不一致。");
                StopAllCoroutines(); ResetAim();
                Layout = board.layout; roundSeed = board.seed; Score = board.score; ShotsRemaining = board.shotsRemaining;
                LastFeedback = board.lastFeedback; LastShotGain = board.lastShotGain; scoringStreak = board.scoringStreak; BestScoringStreak = board.bestScoringStreak;
                var interpolation = new RigidbodyInterpolation[targets.Count];
                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i]; var pose = board.balls[i];
                    interpolation[i] = target.Body.interpolation; target.Body.interpolation = RigidbodyInterpolation.None;
                    target.Claimed = pose.claimed;
                    // Rigidbody.position 不会立即更新插值 Transform。必须先统一两套姿态，避免
                    // localScale/active 标脏后，SyncTransforms 用新开局的旧 Transform 覆盖已恢复的 Body。
                    // 已收球也先恢复姿态再隐藏，不能先移除其物理对象再写位置。
                    target.Collider.enabled = false; target.Object.SetActive(true);
                    target.Object.transform.localScale = Vector3.one;
                    target.Body.isKinematic = pose.claimed; target.Collider.enabled = !pose.claimed;
                    target.Object.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    target.Body.position = pose.position; target.Body.rotation = pose.rotation;
                    if (!pose.claimed) { target.Body.linearVelocity = Vector3.zero; target.Body.angularVelocity = Vector3.zero; target.Body.linearDamping = .34f; target.Body.angularDamping = .27f; target.Body.Sleep(); }
                    target.Object.SetActive(!pose.claimed);
                }
                awaitingResult = false;
                if (Score == targets.Count || ShotsRemaining == 0)
                {
                    // 已结束快照只展示结果，不再次触发 Completed 或重复写成绩。
                    Phase = MatchPhase.Finished; startDelay = 0; Message = "这一轮已结束，收下 " + Score + " / 8 颗。";
                    if (cue != null) cue.Object.SetActive(false);
                }
                else { SpawnCue(); Phase = MatchPhase.Waiting; startDelay = .25f; Message = GameplayLayouts.Name(Layout) + " · 继续这一轮。"; }
                Physics.SyncTransforms();
                for (int i = 0; i < targets.Count; i++) targets[i].Body.interpolation = interpolation[i];
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception) { Debug.LogWarning("玻璃球快照无法读取：" + exception.Message); return false; }
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public void Configure(SceneWorld scene, SummerAudio audio) { world = scene; audioFx = audio; }

        public void Begin(int seed = 97, MarbleLayout layout = MarbleLayout.Classic)
        {
            if (!Enum.IsDefined(typeof(MarbleLayout), layout)) throw new ArgumentOutOfRangeException("layout");
            StopMatch();
            if (world == null) throw new InvalidOperationException("MarbleMatch.Configure 必须在 Begin 前调用。");
            world.SetPogTerrainEnabled(false);
            Score = 0; ShotsRemaining = 6; Power = 0f; Layout = layout; roundSeed = seed;
            LastFeedback = ""; LastShotGain = BestScoringStreak = scoringStreak = 0;
            direction = Vector3.forward;
            arena = new GameObject("弹玻璃球 · 彩色玻璃擂台").transform;
            arena.SetParent(transform, false);
            CreateLines();
            var random = new System.Random(seed);
            Color[] colors =
            {
                new Color(0.15f,0.72f,0.81f), new Color(0.96f,0.34f,0.25f),
                new Color(0.98f,0.72f,0.2f), new Color(0.49f,0.33f,0.86f),
                new Color(0.24f,0.71f,0.46f), new Color(0.96f,0.48f,0.65f),
                new Color(0.31f,0.51f,0.95f), new Color(0.9f,0.53f,0.2f)
            };
            // 不重叠的两组错列球阵；第一次出手也能找到清晰的击球角度。
            Vector3[] positions = GameplayLayouts.MarblePositions(layout);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 jitter = new Vector3((float)(random.NextDouble() - 0.5) * .035f, 0f, (float)(random.NextDouble() - 0.5) * .035f);
                var ball = CreateBall("彩玻璃球 " + (i + 1), positions[i] + jitter, .155f, colors[i], false);
                ball.Object.transform.rotation = UnityEngine.Random.rotation;
                targets.Add(ball);
            }
            shotStartPositions = new Vector3[targets.Count];
            SpawnCue();
            Phase = MatchPhase.Waiting; startDelay = .5f;
            Message = GameplayLayouts.Name(Layout) + " · 6 次机会，把 8 颗玻璃球完整弹出粉笔圈。  " + GameplayLayouts.Hint(Layout);
            PhysicsDiagnostic?.Invoke("Marble layout=" + Layout + " seed=" + seed + " targets=" + targets.Count);
            world.SetCameraMode(WorldCameraMode.Match);
            audioFx?.Duck(true);
            Changed?.Invoke();
        }

        BallData CreateBall(string name, Vector3 position, float radius, Color color, bool striker)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(arena, false);
            obj.transform.position = position;
            var collider = obj.AddComponent<SphereCollider>(); collider.radius = radius;
            var physical = new PhysicsMaterial("玻璃球碰撞 · " + name);
            physical.dynamicFriction = .10f; physical.staticFriction = .12f;
            physical.bounciness = .62f;
            physical.frictionCombine = PhysicsMaterialCombine.Minimum;
            physical.bounceCombine = PhysicsMaterialCombine.Average;
            resources.Add(physical); collider.sharedMaterial = physical;
            var body = obj.AddComponent<Rigidbody>();
            body.mass = striker ? .058f : .041f;
            body.linearDamping = .34f; body.angularDamping = .27f;
            body.maxAngularVelocity = 100f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 20; body.solverVelocityIterations = 12;
            body.sleepThreshold = .02f;

            // 内芯先建立，透明球壳覆盖在外；底部另有玻璃的焦散色斑。
            var coreMaterial = NewMaterial(name + " · 彩色猫眼内芯", color * .83f, .02f, .54f);
            coreMaterial.EnableKeyword("_EMISSION"); coreMaterial.SetColor("_EmissionColor", color * .11f);
            var shellMaterial = GlassMaterial(name + " · 通透玻璃", Color.Lerp(color, Color.white, .12f), .10f);
            var shell = VisualSphere("透明玻璃球壳", obj.transform, radius * 2f, shellMaterial);
            shell.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var core = VisualSphere("彩色内芯", obj.transform, radius * .96f, coreMaterial);
            core.transform.localScale = new Vector3(radius * .75f, radius * 1.59f, radius * .71f);
            core.transform.localRotation = Quaternion.Euler(17f, 25f, 38f);
            // 三条微扭的叶片，使球有传统猫眼玻璃珠的彩条和纵深。
            for (int i = 0; i < 3; i++)
            {
                var blade = VisualSphere("螺旋彩条", obj.transform, radius, coreMaterial);
                blade.transform.localScale = new Vector3(radius * 1.44f, radius * .37f, radius * .27f);
                blade.transform.localRotation = Quaternion.Euler(i * 58f + 19f, i * 80f + 35f, i * 51f + 10f);
            }
            var highlightMaterial = NewMaterial(name + " · 玻璃反光", new Color(1f, 1f, .97f, .72f), 0f, 1f);
            SetTransparent(highlightMaterial);
            var highlight = VisualSphere("玻璃白色高光", obj.transform, radius * .26f, highlightMaterial);
            highlight.transform.localScale = new Vector3(radius * .24f, radius * .13f, radius * .055f);
            highlight.transform.localPosition = new Vector3(-radius * .37f, radius * .53f, -radius * .68f);
            highlight.transform.localRotation = Quaternion.Euler(-30f, -23f, -21f);
            highlight.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 用实心的下层玻璃与高光保住球的轮廓，远看仍易于瞄准。
            var baseMaterial = GlassMaterial(name + " · 内部折射色", Color.Lerp(color, Color.white, .12f), .055f);
            var baseGlass = VisualSphere("玻璃珠里的折射球", obj.transform, radius * 1.61f, baseMaterial);
            baseGlass.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var relay = obj.AddComponent<MarbleCollisionRelay>();
            relay.Callback = OnBallImpact;
            return new BallData { Object = obj, Body = body, Collider = collider, Radius = radius };
        }

        GameObject VisualSphere(string name, Transform parent, float diameter, Material material)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name; sphere.transform.SetParent(parent, false);
            sphere.transform.localScale = Vector3.one * diameter;
            var collider = sphere.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
            return sphere;
        }

        Material NewMaterial(string name, Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("Standard"));
            material.name = name; material.color = color;
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true; resources.Add(material); return material;
        }

        Material GlassMaterial(string name, Color color, float alpha)
        {
            var shader = Resources.Load<Shader>("Shaders/MarbleGlass");
            color.a = alpha;
            if (shader != null)
            {
                var material = new Material(shader) { name = name, color = color };
                material.SetFloat("_EdgeStrength", 1f); resources.Add(material); return material;
            }
            var fallback = NewMaterial(name, color, 0f, .65f);
            SetTransparent(fallback); fallback.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return fallback;
        }

        static void SetTransparent(Material material)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); material.renderQueue = 3000;
        }

        void CreateLines()
        {
            var ring = Line("玻璃球比赛粉笔圈", new Color(.98f, .97f, .81f), .045f);
            ring.loop = true; ring.positionCount = 128;
            for (int i = 0; i < 128; i++)
            {
                float a = i / 128f * Mathf.PI * 2f;
                float wobble = Mathf.Sin(a * 17f) * .009f;
                ring.SetPosition(i, RingCenter + new Vector3(Mathf.Cos(a) * (RingRadius + wobble), .022f, Mathf.Sin(a) * (RingRadius + wobble)));
            }
            aimLine = Line("弹球瞄准线", new Color(.99f, .86f, .43f, .96f), .03f);
            aimLine.positionCount = 2; aimLine.enabled = false;
            pullLine = Line("手指蓄力拉线", new Color(.45f, .82f, .9f, .65f), .014f);
            pullLine.positionCount = 2; pullLine.enabled = false;
            cueHalo = Line("你的玻璃球光圈", new Color(.99f, .88f, .50f, .8f), .018f);
            cueHalo.positionCount = 64; cueHalo.loop = true;
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2f;
                cueHalo.SetPosition(i, LaunchPosition + new Vector3(Mathf.Cos(a) * .28f, -.156f, Mathf.Sin(a) * .28f));
            }
        }

        LineRenderer Line(string name, Color color, float width)
        {
            var obj = new GameObject(name); obj.transform.SetParent(arena, false);
            var line = obj.AddComponent<LineRenderer>();
            var material = new Material(Shader.Find("Sprites/Default")); resources.Add(material);
            line.sharedMaterial = material; line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width; line.numCapVertices = 8;
            line.useWorldSpace = true; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        void SpawnCue()
        {
            if (cue != null && cue.Object != null)
            {
                cue.Object.SetActive(false); Destroy(cue.Object);
            }
            cue = CreateBall("你的大玻璃球", LaunchPosition, .18f, new Color(.14f, .58f, .79f), true);
            cue.Body.isKinematic = true;
            restCuePosition = LaunchPosition;
            if (cueHalo != null) cueHalo.enabled = true;
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (Phase == MatchPhase.Waiting && startDelay > 0f)
            {
                startDelay -= Time.deltaTime;
                if (startDelay <= 0f && !awaitingResult)
                {
                    Phase = MatchPhase.PlayerAim;
                    Message = (string.IsNullOrEmpty(LastFeedback) ? GameplayLayouts.Hint(Layout) : LastFeedback) + "  按住大球向后拖，松手弹出；右键取消。";
                    Changed?.Invoke();
                }
            }
            if (Phase != MatchPhase.PlayerAim || cue == null) return;
            ReadPointer(out Vector2 pointer, out bool pointerDown, out bool pointerHeld, out bool pointerUp, out bool pointerCancelled);
            if (pointerDown && !(BlocksInput?.Invoke(pointer) ?? false) && FloorPoint(pointer, out var point))
            {
                // 允许从球附近或目标附近发起拖拽，触控板用户同样容易瞄准。
                IsDragging = true; dragStart = point; Power = .03f;
                direction = point.z > -1.4f ? (point - LaunchPosition).normalized : Vector3.forward;
                direction.y = 0f;
                direction.Normalize(); Changed?.Invoke();
            }
            if (IsDragging && pointerHeld && FloorPoint(pointer, out var current))
            {
                Vector3 pull = dragStart - current; pull.y = 0f;
                if (pull.magnitude > .035f) direction = pull.normalized;
                // 场地后方没有目标；限制方向仍然允许足够大的侧击角度。
                direction.z = Mathf.Max(.08f, direction.z); direction.Normalize();
                Power = Mathf.Clamp01(pull.magnitude / 1.8f);
                DrawAim(pull); Changed?.Invoke();
            }
            if (IsDragging && pointerUp)
            {
                IsDragging = false;
                if (Power >= .07f) Launch(direction, Power);
                else { ResetAim(); Changed?.Invoke(); }
            }
            if (IsDragging && (pointerCancelled || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            { CancelAim(); }
        }

        void ReadPointer(out Vector2 pointer, out bool down, out bool held, out bool up, out bool cancelled)
        {
            pointer = Input.mousePosition;
            down = Input.GetMouseButtonDown(0); held = Input.GetMouseButton(0); up = Input.GetMouseButtonUp(0); cancelled = false;
            if (!Application.isMobilePlatform || Input.touchCount == 0) { activeFingerId = -1; return; }
            Touch touch = default;
            bool found = false;
            if (activeFingerId >= 0)
                for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).fingerId == activeFingerId) { touch = Input.GetTouch(i); found = true; break; }
            if (!found)
                for (int i = 0; i < Input.touchCount; i++)
                    if (Input.GetTouch(i).phase == TouchPhase.Began) { touch = Input.GetTouch(i); activeFingerId = touch.fingerId; found = true; break; }
            if (!found) { down = held = up = false; return; }
            pointer = touch.position;
            down = touch.phase == TouchPhase.Began;
            held = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            up = touch.phase == TouchPhase.Ended;
            cancelled = touch.phase == TouchPhase.Canceled;
            if (up || cancelled) activeFingerId = -1;
        }

        bool FloorPoint(Vector2 pointer, out Vector3 point)
        {
            var ray = world.MainCamera.ScreenPointToRay(pointer);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = Vector3.zero; return false;
        }

        void DrawAim(Vector3 pull)
        {
            audioFx?.PreviewCharge(Power);
            aimLine.enabled = true;
            Vector3 start = LaunchPosition + Vector3.up * .05f;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, start + direction * (.55f + Power * .85f));
            aimLine.startWidth = .025f + Power * .015f; aimLine.endWidth = .008f;
            pullLine.enabled = true;
            pullLine.SetPosition(0, restCuePosition + Vector3.up * .045f);
            pullLine.SetPosition(1, restCuePosition - Vector3.ClampMagnitude(pull, 1.8f) + Vector3.up * .045f);
            cue.Object.transform.position = restCuePosition - direction * (Power * .04f);
            cue.Object.transform.rotation = Quaternion.Euler(0f, Time.unscaledTime * 22f, Power * 15f);
        }

        void ResetAim()
        {
            audioFx?.PreviewCharge(0);
            Power = 0f;
            // 飞行中打开暂停或失焦，只关闭输入反馈，不能把动态大球搬回发射点。
            if (cue != null && cue.Body.isKinematic) cue.Object.transform.position = restCuePosition;
            if (aimLine != null) aimLine.enabled = false;
            if (pullLine != null) pullLine.enabled = false;
        }

        public void CancelAim()
        {
            IsDragging = false; ResetAim(); Changed?.Invoke();
        }

        void OnApplicationFocus(bool focused) { if (!focused) CancelAim(); }

        public void Launch(Vector3 shotDirection, float power)
        {
            if (Phase != MatchPhase.PlayerAim || cue == null || ShotsRemaining <= 0 || Time.timeScale <= 0) return;
            if (!Finite(shotDirection.x) || !Finite(shotDirection.y) || !Finite(shotDirection.z) || !Finite(power)) return;
            shotDirection.y = 0f;
            if (shotDirection.sqrMagnitude < .001f) shotDirection = Vector3.forward;
            shotDirection.Normalize(); power = Mathf.Clamp01(power);
            direction = shotDirection;
            ResetAim(); IsDragging = false; Power = power;
            cue.Body.isKinematic = false;
            cue.Body.position = LaunchPosition;
            cue.Body.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            float speed = 3.4f + power * 8.5f;
            // 只有初始速度；后续撞击、反弹与出圈均由 PhysX 计算。
            cue.Body.linearVelocity = shotDirection * speed;
            cue.Body.angularVelocity = Vector3.Cross(Vector3.up, shotDirection) * (speed / cue.Radius);
            cue.Body.WakeUp();
            if (cueHalo != null) cueHalo.enabled = false;
            ShotsRemaining--; shotStartScore = Score; flightTime = quietTime = impactCooldown = 0f;
            shotPower = power; maximumMovement = 0; impactCount = 0;
            for (int i = 0; i < targets.Count; i++) shotStartPositions[i] = targets[i].Body.position;
            Phase = MatchPhase.Flying; Message = "清脆一弹，看看能撞出几颗！";
            audioFx?.PlayShot(ShotKind.Skim, power);
            world.Shake(.018f + power * .023f, .10f); Changed?.Invoke();
        }

        void FixedUpdate()
        {
            if (Phase != MatchPhase.Flying) return;
            flightTime += Time.fixedDeltaTime;
            impactCooldown = Mathf.Max(0f, impactCooldown - Time.fixedDeltaTime);
            bool quiet = flightTime > .35f;
            for (int i = 0; i < targets.Count; i++)
            {
                var ball = targets[i];
                if (ball.Claimed) continue;
                maximumMovement = Mathf.Max(maximumMovement, Vector3.Distance(ball.Body.position, shotStartPositions[i]));
                Vector3 offset = ball.Object.transform.position - RingCenter; offset.y = 0f;
                if (offset.magnitude > RingRadius + ball.Radius && ball.Object.transform.position.y < .8f)
                {
                    Claim(ball); continue;
                }
                if (ball.Object.transform.position.y < -1f)
                {
                    // 只作为越界保护；正常场地覆盖整条街，不会掉出地面。
                    ball.Body.linearVelocity = Vector3.zero;
                    ball.Body.angularVelocity = Vector3.zero;
                    ball.Body.position = new Vector3(0f, ball.Radius + .01f, .55f);
                }
                if (ball.Body.linearVelocity.sqrMagnitude > .012f || ball.Body.angularVelocity.sqrMagnitude > .50f) quiet = false;
                ApplyRollingResistance(ball);
            }
            if (Score >= targets.Count) { Settle(); return; }
            if (cue != null)
            {
                ApplyRollingResistance(cue);
                if (cue.Body.linearVelocity.sqrMagnitude > .018f && cue.Object.transform.position.magnitude < 9f && flightTime < 7f) quiet = false;
            }
            quietTime = quiet ? quietTime + Time.fixedDeltaTime : 0f;
            if (quietTime > .3f || flightTime > 11.5f) Settle();
        }

        void ApplyRollingResistance(BallData ball)
        {
            if (ball.Claimed || ball.Body.isKinematic) return;
            // 模拟粗糙地面的滚动阻力；不朝目标加力，不改变得分条件。
            if (ball.Object.transform.position.y < ball.Radius + .08f)
            {
                Vector3 velocity = ball.Body.linearVelocity;
                Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
                if (planar.sqrMagnitude > .0001f)
                    ball.Body.AddForce(-planar.normalized * .50f, ForceMode.Acceleration);
                if (planar.magnitude < .10f && flightTime > .6f)
                { ball.Body.linearVelocity = new Vector3(0f, velocity.y, 0f); ball.Body.angularVelocity *= .86f; }
            }
            if (flightTime > 7f) { ball.Body.linearDamping = 1.5f; ball.Body.angularDamping = 1.7f; }
        }

        void Claim(BallData ball)
        {
            ball.Claimed = true; Score++;
            ball.Body.linearVelocity = Vector3.zero; ball.Body.angularVelocity = Vector3.zero;
            ball.Body.isKinematic = true; ball.Collider.enabled = false;
            world.Punch(ball.Object.transform.position, .38f);
            audioFx?.PlayCollect(Score - shotStartScore);
            Message = "出圈了！已经拿到 " + Score + " / 8 颗。";
            StartCoroutine(Collect(ball)); Changed?.Invoke();
        }

        IEnumerator Collect(BallData ball)
        {
            Vector3 start = ball.Object.transform.position;
            float t = 0f;
            while (t < 1f && ball.Object != null)
            {
                t += Time.deltaTime * 1.7f;
                float progress = Mathf.Clamp01(t);
                ball.Object.transform.position = start + Vector3.up * Mathf.Sin(progress * Mathf.PI) * .65f;
                ball.Object.transform.Rotate(Vector3.up, 145f * Time.deltaTime, Space.World);
                ball.Object.transform.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
            if (ball.Object != null) ball.Object.SetActive(false);
        }

        void Settle()
        {
            ResetAim();
            foreach (var ball in targets)
            {
                if (ball.Claimed) continue;
                ball.Body.linearVelocity = Vector3.zero; ball.Body.angularVelocity = Vector3.zero;
                ball.Body.linearDamping = .34f; ball.Body.angularDamping = .27f;
                ball.Body.Sleep();
            }
            int gained = Score - shotStartScore;
            LastShotGain = gained;
            scoringStreak = gained > 0 ? scoringStreak + 1 : 0;
            BestScoringStreak = Mathf.Max(BestScoringStreak, scoringStreak);
            LastFeedback = BuildFeedback(gained);
            if (gained >= 2 || scoringStreak >= 2) audioFx?.PlayCombo(Mathf.Max(gained, scoringStreak));
            PhysicsDiagnostic?.Invoke("Marble settle layout=" + Layout + " shot=" + (6 - ShotsRemaining) + " gained=" + gained + " score=" + Score + " contacts=" + impactCount + " move=" + maximumMovement.ToString("0.00"));
            if (Score >= targets.Count || ShotsRemaining == 0)
            {
                Phase = MatchPhase.Waiting; awaitingResult = true;
                Message = Score == 8 ? "全收！玻璃球高手就是你。" : "6 次出手结束，拿到 " + Score + " 颗彩色玻璃球。";
                Changed?.Invoke(); StartCoroutine(FinishAfterBeat()); return;
            }
            Message = LastFeedback;
            SpawnCue(); Phase = MatchPhase.Waiting; startDelay = .9f; Changed?.Invoke();
        }

        string BuildFeedback(int gained)
        {
            if (gained >= 3) return "一手连弹 " + gained + " 颗，漂亮！这条接力线路找对了。";
            if (gained == 2) return "两颗一起出圈。剩下的球，先找离边线最近的一颗。";
            if (gained == 1) return scoringStreak >= 2 ? "连续有收获，保持这份节奏，不必每手都拉满。" : "整颗越线，收下！下一手可以换侧边试试。";
            if (impactCount == 0) return "这手没碰上球。先瞄前排的内侧，拖拽方向再收准一点。";
            float gap = float.MaxValue;
            foreach (var ball in targets)
            {
                if (ball.Claimed) continue;
                var offset = ball.Body.position - RingCenter; offset.y = 0;
                gap = Mathf.Min(gap, RingRadius + ball.Radius - offset.magnitude);
            }
            if (gap < .24f) return "有球已经贴着圈边。轻轻从内侧擦过去，试着把最后一点送出圈。";
            if (shotPower < .28f) return "碰到了，不过这手偏轻。多拉一点，让碰撞传到下一颗。";
            if (maximumMovement > .65f) return "球阵已经散开，机会变多了。先找靠边的一颗，从内侧向外打。";
            return ShotsRemaining % 2 == 0 ? GameplayLayouts.Hint(Layout) : "侧击常比正撞更有用。试试把大球的方向偏向目标内侧。";
        }

        IEnumerator FinishAfterBeat()
        {
            yield return new WaitForSeconds(.8f);
            Phase = MatchPhase.Finished; awaitingResult = false; audioFx?.Duck(false);
            audioFx?.Play(Score >= 4 ? "win" : "lose", .65f);
            Changed?.Invoke(); Completed?.Invoke(Score);
        }

        void OnBallImpact(Vector3 point, float speed)
        {
            if (Phase != MatchPhase.Flying || impactCooldown > 0f || speed < .9f) return;
            impactCount++;
            impactCooldown = .055f;
            audioFx?.PlayImpact(ImpactMaterial.Glass, speed, .35f, point);
            if (speed > 3.5f) world.Punch(point, Mathf.Clamp(speed * .055f, .2f, .6f));
        }

        public void StopMatch()
        {
            audioFx?.PreviewCharge(0);
            StopAllCoroutines(); startDelay = 0f; awaitingResult = false; IsDragging = false; Power = 0f;
            Phase = MatchPhase.Waiting; targets.Clear(); cue = null;
            aimLine = pullLine = cueHalo = null;
            if (arena != null) { arena.gameObject.SetActive(false); Destroy(arena.gameObject); arena = null; }
            foreach (var resource in resources) if (resource != null) Destroy(resource);
            resources.Clear(); audioFx?.Duck(false);
            world?.SetPogTerrainEnabled(true);
        }

        void OnDestroy() { StopMatch(); }
    }

    internal sealed class MarbleCollisionRelay : MonoBehaviour
    {
        public Action<Vector3, float> Callback;
        void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount > 0 && collision.collider.GetComponent<MarbleCollisionRelay>() != null)
                Callback?.Invoke(collision.GetContact(0).point, collision.relativeVelocity.magnitude);
        }
    }
}
