using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    public enum MatchPhase { Waiting, PlayerAim, Flying, NpcThinking, Finished }

    /// <summary>共享一块场地的回合竞技；得分只在物理静止后判定。</summary>
    public sealed class PogMatch : MonoBehaviour
    {
        public MatchPhase Phase { get; private set; }
        public RuleKind Rule { get; private set; }
        ShotKind selectedShot = ShotKind.Slap;
        float selectedSpin;
        public ShotKind Shot
        {
            get => selectedShot;
            set { if (!Enum.IsDefined(typeof(ShotKind), value)) return; selectedShot = value; if (Phase == MatchPhase.PlayerAim) UpdateReadyPose(); }
        }
        public float Spin
        {
            get => selectedSpin;
            set { if (!Finite(value)) return; selectedSpin = Mathf.Clamp(value, -1, 1); if (Phase == MatchPhase.PlayerAim) UpdateReadyPose(); }
        }
        public float PlayerAzimuth { get; private set; }
        public float TiltPitch { get; private set; }
        public float TiltRoll { get; private set; }
        public Vector3 LaunchPosition => launch;
        public DiscBody Striker => striker;
        public Quaternion ReadyRotation => ShotRotation(aim, Shot, Spin, playerTurn && !IsGuidedLayout);
        public float Power { get; private set; }
        public int PlayerScore { get; private set; }
        public int NpcScore { get; private set; }
        public int Turn { get; private set; }
        public int ShotsUsed { get; private set; }
        public bool Practice { get; private set; }
        public PracticeLayout Layout { get; private set; }
        public EnvironmentContext MatchEnvironment { get; private set; }
        public string LastFeedback { get; private set; }
        public float PlayerCardWear { get; set; }
        public bool PlayerCardTreasured { get; set; }
        public bool IsDragging { get; private set; }
        public bool GuidedResultHeld { get; set; }
        public bool IsGuidedLayout { get; private set; }
        GuidedPracticeStep guidedStep;
        public Vector3 GuidedAimPoint { get; private set; }
        public float GuidedSuggestedPower { get; private set; }
        public string Message { get; private set; }
        public event Action Changed;
        public event Action<MatchOutcome> Completed;
        /// <summary>真实 PhysX 出手停稳后发出；教学和 QA 可订阅，正式对局不改变原有计分流程。</summary>
        public event Action<ShotObservation> ShotSettled;
        /// <summary>玩家在蓄力中右键/Esc 取消时发出。</summary>
        public event Action AimCancelled;
        int activeFingerId = -1;
        public event Action<string> Highlight;
        internal event Action<string> PhysicsDiagnostic;
        public Func<Vector2, bool> BlocksInput;

        readonly List<DiscBody> targets = new List<DiscBody>();
        readonly List<Vector2> baseDamping = new List<Vector2>();
        readonly HashSet<DiscBody> touchedTargets = new HashSet<DiscBody>();
        Vector3[] shotStartPositions;
        Vector3 shotStartStrikerPosition;
        float largestMovement, lowestUpDot = 1f, lowestContactNormalY = 1f, lastImpactSoundTime = -1;
        int shotCollisionCount;
        int playerMissStreak, playerScoringStreak;
        SceneWorld world;
        SummerAudio audioFx;
        Transform arena;
        DiscBody striker;
        CardDefinition selectedCard;
        NpcDefinition npc;
        LineRenderer aimLine;
        LineRenderer landingRing;
        LineRenderer guidedTargetRing, guidedActionArrow, guidedLandingRing;
        Vector3 dragStart, aim = Vector3.forward;
        static readonly Vector3 defaultLaunch = new Vector3(0, 0.1f, -2.25f);
        Vector3 launch = defaultLaunch;
        bool orbitDragging;
        Vector2 orbitPointer;
        bool readyPoseValid;
        Vector3 readyLaunch, readyPosition;
        Quaternion readyRotation;
        readonly Vector3 ringCenter = new Vector3(0, 0, .55f);
        const float ringRadius = 1.85f;
        bool playerTurn;
        bool shockTriggered;
        float shotPower;
        float quietTime;
        float flightTime;
        float slowTime;
        float remainingStartDelay;
        ShotKind flyingShot;
        float flyingSpin;
        float airDelay = -1;
        Vector3 airImpact;
        System.Random random;
        int randomSeed;
        int randomDrawCount;
        readonly List<Material> lineMaterials = new List<Material>();
        public IEnumerable<DiscBody> Targets => targets;
        public ShotObservation LastShotObservation { get; private set; }

        [Serializable] public sealed class BoardState
        {
            public int version;
            public int geometryVersion;
            public float baseHeight;
            public EnvironmentContext environment;
            public int nextTurnBase, playerScore, npcScore, shotsUsed;
            public int randomDrawCount;
            public bool practice;
            public ShotKind shot;
            public PracticeLayout layout;
            public string lastFeedback;
            public int playerMissStreak, playerScoringStreak;
            // 可选字段沿用 snapshot3；老存档缺字段时为零，保持原站位与倾角。
            public float playerAzimuth, tiltPitch, tiltRoll;
            public PoseState[] discs;
        }
        [Serializable] public sealed class PoseState
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool claimed;
        }

        public string CaptureBoard()
        {
            var poses = new PoseState[targets.Count];
            for (int i = 0; i < targets.Count; i++) poses[i] = new PoseState { position = targets[i].SnapshotPosition, rotation = targets[i].SnapshotRotation, claimed = targets[i].Claimed };
            return JsonUtility.ToJson(new BoardState { version = 3, geometryVersion = MatchEnvironment?.geometryVersion ?? DiscBody.GeometryVersion,
                baseHeight = DiscBody.BaseHeight, environment = MatchEnvironment,
                nextTurnBase = Phase == MatchPhase.PlayerAim || Phase == MatchPhase.NpcThinking ? Mathf.Max(0, Turn - 1) : Turn,
                playerScore = PlayerScore, npcScore = NpcScore, shotsUsed = ShotsUsed, randomDrawCount = randomDrawCount, practice = Practice, shot = Shot,
                layout = Layout, lastFeedback = LastFeedback, playerMissStreak = playerMissStreak, playerScoringStreak = playerScoringStreak,
                playerAzimuth = PlayerAzimuth, tiltPitch = TiltPitch, tiltRoll = TiltRoll, discs = poses });
        }

        public bool RestoreBoard(string json)
        {
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                var board = JsonUtility.FromJson<BoardState>(json);
                if (board?.discs == null || board.version < 0 || board.version > 3 || board.discs.Length != targets.Count || board.nextTurnBase < 0 || board.nextTurnBase > 14 ||
                    board.playerScore < 0 || board.npcScore < 0 || board.playerScore + board.npcScore > targets.Count ||
                    board.shotsUsed < 0 || board.shotsUsed > 14 || board.randomDrawCount < 0 || board.randomDrawCount > 1024)
                    throw new InvalidOperationException("回合或圆片快照数量无效。");
                int claimed = 0;
                foreach (var pose in board.discs)
                {
                    if (pose == null || !Finite(pose.position.x) || !Finite(pose.position.y) || !Finite(pose.position.z) ||
                        !Finite(pose.rotation.x) || !Finite(pose.rotation.y) || !Finite(pose.rotation.z) || !Finite(pose.rotation.w) ||
                        pose.rotation.x * pose.rotation.x + pose.rotation.y * pose.rotation.y + pose.rotation.z * pose.rotation.z + pose.rotation.w * pose.rotation.w < .1f)
                        throw new InvalidOperationException("圆片的位置或朝向无效。");
                    if (pose.claimed) claimed++;
                }
                if (claimed != board.playerScore + board.npcScore) throw new InvalidOperationException("得分与已收下的圆片数量不一致。");
                if (board.version >= 1 && !Enum.IsDefined(typeof(ShotKind), board.shot)) throw new InvalidOperationException("出手方式无效。");
                if (board.version >= 2 && (!Enum.IsDefined(typeof(PracticeLayout), board.layout) || board.playerMissStreak < 0 || board.playerScoringStreak < 0))
                    throw new InvalidOperationException("练习阵型或连续回合无效。");
                if (board.version >= 3 && (!Finite(board.baseHeight) || board.baseHeight <= 0 || board.baseHeight > .1f || board.geometryVersion < 1 || board.geometryVersion > DiscBody.GeometryVersion))
                    throw new InvalidOperationException("圆片几何版本或厚度无效。");
                if (!Finite(board.playerAzimuth) || !Finite(board.tiltPitch) || !Finite(board.tiltRoll) ||
                    board.playerAzimuth < 0 || board.playerAzimuth >= 360 || Mathf.Abs(board.tiltPitch) > 35 || Mathf.Abs(board.tiltRoll) > 35)
                    throw new InvalidOperationException("出手站位或片角无效。");
                PlayerAzimuth = board.playerAzimuth; TiltPitch = board.tiltPitch; TiltRoll = board.tiltRoll;
                world.SetMatchOrbit(PlayerAzimuth, world.MatchElevation, world.MatchDistance, true);
                Turn = board.nextTurnBase; PlayerScore = board.playerScore; NpcScore = board.npcScore; ShotsUsed = board.shotsUsed;
                if (board.version >= 1) { Practice = board.practice; Shot = board.shot; }
                Layout = board.version >= 2 ? board.layout : PracticeLayout.Classic;
                LastFeedback = board.version >= 2 && board.playerMissStreak == 0 ? board.lastFeedback : "";
                playerMissStreak = board.version >= 2 ? board.playerMissStreak : 0;
                playerScoringStreak = board.version >= 2 ? board.playerScoringStreak : 0;
                // 用种子和已消耗的随机数恢复 NPC 决策序列，续局不会重新从第一个随机数开始。
                random = new System.Random(randomSeed); randomDrawCount = 0;
                for (int i = 0; i < board.randomDrawCount; i++) NextRandom();
                for (int i = 0; i < targets.Count; i++)
                {
                    var pose = board.discs[i];
                    Vector3 position = pose.position;
                    if (!pose.claimed && world.Terrain != null)
                    {
                        if (!world.Terrain.Contains(position)) position = world.Terrain.ClampInside(position, targets[i].Radius + .01f);
                        // 老快照没有保存厚度。旧场景先由协调器恢复，再把薄片落到同一接触面，避免睡眠刚体悬空。
                        bool legacyHeight = board.version < 3 || Mathf.Abs(board.baseHeight - DiscBody.BaseHeight) > .00001f;
                        if (legacyHeight || position.y < world.Terrain.transform.position.y - targets[i].Height || OverlapsTerrain(targets[i], position, pose.rotation))
                        {
                            float originalHeight = position.y;
                            position.y = RestHeight(targets[i], position, pose.rotation, targets[i].RestClearance);
                            if (legacyHeight) PhysicsDiagnostic?.Invoke("Legacy geometry migration disc=" + targets[i].ArtIndex + " oldY=" + originalHeight.ToString("F6") + " newY=" + position.y.ToString("F6") + " rotation/claimed/score preserved");
                        }
                    }
                    targets[i].RestorePose(position, pose.rotation, pose.claimed);
                }
                if (Phase == MatchPhase.PlayerAim)
                {
                    launch = PlayerLaunch(); aim = ringCenter - launch; aim.y = 0; aim.Normalize(); UpdateReadyPose();
                }
                return true;
            }
            catch (Exception e) { Debug.LogWarning("对局快照无法读取：" + e.Message); return false; }
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        double NextRandom() { randomDrawCount++; return random.NextDouble(); }

        public void Configure(SceneWorld scene, SummerAudio audio) { world = scene; audioFx = audio; }

        /// <summary>
        /// 为分步教学设置真实场景中的目标片。第一步保留完整阵型；翻面和出圈各只保留
        /// 一张未收下的目标片，其他片通过 DiscBody 的正常 claimed 路径隐藏，不改变碰撞
        /// 形状或正式对局的布局快照。
        /// </summary>
        public bool ConfigureGuidedStep(GuidedPracticeStep step)
        {
            if (!Enum.IsDefined(typeof(GuidedPracticeStep), step) || world == null || targets.Count == 0 || Phase == MatchPhase.Flying)
                return false;
            IsGuidedLayout = true; guidedStep = step;
            // Begin() 已经把所有圆片恢复为未收下状态。第一步使用完整阵型。
            if (step == GuidedPracticeStep.DragAndCancel) { CreateGuidedCues(launch); return true; }
            var positions = GameplayLayouts.PogPositions(Layout);
            Vector3 position = step == GuidedPracticeStep.SkimRingOut
                ? ringCenter + new Vector3(1.70f, 0, -1.10f)
                : new Vector3(0, 0, .45f);
            Quaternion rotation = Quaternion.identity;
            position.y = RestHeight(targets[0], position, rotation, targets[0].RestClearance);
            targets[0].RestorePose(position, rotation, false);
            for (int i = 1; i < targets.Count; i++)
            {
                Vector3 hidden = i < positions.Length ? positions[i] : positions[0] + Vector3.right * (i * .08f);
                Quaternion hiddenRotation = Quaternion.FromToRotation(Vector3.up, world.SurfaceNormal(hidden));
                hidden.y = RestHeight(targets[i], hidden, hiddenRotation, targets[i].RestClearance);
                targets[i].RestorePose(hidden, hiddenRotation, true);
            }
            CreateGuidedCues(position);
            Changed?.Invoke();
            return true;
        }

        void CreateGuidedCues(Vector3 target)
        {
            Vector3 direction = target - launch; direction.y = 0;
            direction = direction.sqrMagnitude > .01f ? direction.normalized : Vector3.forward;
            GuidedAimPoint = guidedStep == GuidedPracticeStep.SlapFlip ? target - direction * .18f : target;
            GuidedSuggestedPower = guidedStep == GuidedPracticeStep.SkimRingOut ? .8f :
                Mathf.Clamp01((Vector3.Distance(launch, GuidedAimPoint) - 1.25f) / 3.3f);
            guidedTargetRing = Line("教学 · 纸边位置", new Color(.45f, .88f, .68f), .025f);
            guidedTargetRing.loop = true; guidedTargetRing.positionCount = 48;
            DrawSurfaceCircle(guidedTargetRing, guidedStep == GuidedPracticeStep.DragAndCancel ? launch : target,
                guidedStep == GuidedPracticeStep.DragAndCancel ? .39f : targets[0].Radius + .055f);
            guidedActionArrow = Line("教学 · 动作方向", new Color(.98f, .79f, .38f), .04f);
            Vector3 start, end;
            if (guidedStep == GuidedPracticeStep.DragAndCancel) { start = launch - Vector3.forward * .38f; end = launch - Vector3.forward * 1.15f; }
            else if (guidedStep == GuidedPracticeStep.SlapFlip) { start = GuidedAimPoint - direction * .65f; end = GuidedAimPoint; }
            else { start = target - direction * .53f; end = target + direction * .83f; }
            DrawSurfaceArrow(guidedActionArrow, start, end);
            guidedLandingRing = Line("教学 · 大致落地区域", new Color(.99f, .86f, .52f), .023f);
            guidedLandingRing.loop = true; guidedLandingRing.positionCount = 48; guidedLandingRing.enabled = false;
        }

        void DrawSurfaceCircle(LineRenderer line, Vector3 center, float radius)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i / (float)line.positionCount * Mathf.PI * 2;
                line.SetPosition(i, world.SurfacePoint(center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius, .04f));
            }
        }

        void DrawSurfaceArrow(LineRenderer line, Vector3 start, Vector3 end)
        {
            Vector3 direction = (end - start).normalized, side = Vector3.Cross(direction, Vector3.up) * .14f;
            line.positionCount = 5;
            var points = new[] { start, end, end - direction * .23f + side, end, end - direction * .23f - side };
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, world.SurfacePoint(points[i], .055f));
        }

        public void Begin(CardDefinition card, NpcDefinition opponent, RuleKind rule, bool practice, int seed = 97, PracticeLayout layout = PracticeLayout.Classic, EnvironmentContext environment = null)
        {
            if (card == null) throw new ArgumentNullException("card");
            if (!Enum.IsDefined(typeof(PracticeLayout), layout)) throw new ArgumentOutOfRangeException("layout");
            StopMatch(); selectedCard = card; npc = opponent; Rule = rule; Practice = practice; Layout = layout;
            PlayerAzimuth = TiltPitch = TiltRoll = 0; launch = defaultLaunch; aim = Vector3.forward;
            world.SetMatchOrbit(0, world.MatchElevation, world.MatchDistance);
            IsGuidedLayout = false;
            var context = environment ?? world.Environment;
            MatchEnvironment = context == null ? null : JsonUtility.FromJson<EnvironmentContext>(JsonUtility.ToJson(context));
            world.SetPogTerrainEnabled(true);
            LastFeedback = ""; LastShotObservation = null; playerMissStreak = playerScoringStreak = 0; lastImpactSoundTime = -1;
            randomSeed = seed; randomDrawCount = 0; random = new System.Random(seed); PlayerScore = NpcScore = Turn = ShotsUsed = 0;
            arena = new GameObject("打片 · 对局物件").transform;
            arena.SetParent(transform, false);
            var positions = GameplayLayouts.PogPositions(layout);
            for (int i = 0; i < positions.Length; i++)
            {
                int art = opponent == null || opponent.startingCardIndexes == null || opponent.startingCardIndexes.Length == 0 ? 1 + i : opponent.startingCardIndexes[i % opponent.startingCardIndexes.Length];
                var definition = GameCatalog.Cards[art]; var p = definition.Physics;
                var disc = DiscBody.Spawn(arena, art, positions[i],
                    p.massGrams / 4.8f, p.thicknessMeters / .0027f, p.grip / .64f, Mathf.Clamp01(p.flipEase - .5f));
                disc.Arena = world.Terrain;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, world.SurfaceNormal(positions[i]));
                Vector3 restingPosition = positions[i]; restingPosition.y = RestHeight(disc, restingPosition, rotation, disc.RestClearance);
                disc.RestorePose(restingPosition, rotation, false); disc.Body.WakeUp();
                disc.ConfigureAppearance(.08f + (i % 3) * .035f);
                disc.IsTarget = true; disc.Hit += OnDiscHit; targets.Add(disc);
                baseDamping.Add(new Vector2(disc.Body.linearDamping, disc.Body.angularDamping));
            }
            shotStartPositions = new Vector3[targets.Count];
            CreateLines(); world.SetCameraMode(WorldCameraMode.Match);
            world.SetArenaBoundaryVisible(rule != RuleKind.RingOut);
            Phase = MatchPhase.Waiting; remainingStartDelay = .65f;
            Message = (rule == RuleKind.Flip ? "把圆片完整打翻，先拿到 3 张就赢。" : "把整张圆片撞出粉笔圈，先拿到 3 张就赢。") + "  " + GameplayLayouts.Name(layout) + " · " + GameCatalog.GetLocation(world.LocationId == "school" ? "school_gate" : world.LocationId).surfaceName;
            PhysicsDiagnostic?.Invoke("Pog layout=" + layout + " seed=" + seed + " targets=" + targets.Count);
            Changed?.Invoke(); audioFx.Duck(true);
        }

        void CreateLines()
        {
            aimLine = Line("出手方向", new Color(1f, .87f, .45f), .035f);
            aimLine.positionCount = 2; aimLine.enabled = false;
            landingRing = Line("力道范围", new Color(1f, .85f, .5f, .9f), .018f);
            landingRing.loop = true; landingRing.positionCount = 48; landingRing.enabled = false;
            if (Rule == RuleKind.RingOut)
            {
                var circle = Line("粉笔比赛圈", new Color(.97f, .95f, .81f), .047f);
                circle.positionCount = 96; circle.loop = true;
                for (int i = 0; i < 96; i++)
                {
                    float a = i / 96f * Mathf.PI * 2;
                    circle.SetPosition(i, world.SurfacePoint(ringCenter + new Vector3(Mathf.Cos(a) * ringRadius, 0, Mathf.Sin(a) * ringRadius), .018f));
                }
            }
        }

        LineRenderer Line(string name, Color color, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(arena, false);
            var line = go.AddComponent<LineRenderer>();
            var material = new Material(Shader.Find("Sprites/Default")); lineMaterials.Add(material);
            line.sharedMaterial = material; line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width; line.numCapVertices = 6;
            line.useWorldSpace = true; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        void NextTurn()
        {
            if (PlayerScore >= 3 || NpcScore >= 3 || Turn >= 14 || AllClaimed())
            {
                Finish(PlayerScore > NpcScore ? MatchOutcome.PlayerWin : PlayerScore < NpcScore ? MatchOutcome.NpcWin : MatchOutcome.Draw);
                return;
            }
            Turn++; playerTurn = Practice || npc == null || Turn % 2 == 1;
            // 超长出手的临时减速不应永久改变下一手的物理手感。
            for (int i = 0; i < targets.Count; i++)
                if (!targets[i].Claimed) { targets[i].Body.linearDamping = baseDamping[i].x; targets[i].Body.angularDamping = baseDamping[i].y; }
            launch = playerTurn ? PlayerLaunch() : defaultLaunch;
            aim = (ringCenter - launch).normalized; aim.y = 0; aim.Normalize();
            Spin = 0; Power = 0; SpawnStriker();
            if (playerTurn)
            {
                Phase = MatchPhase.PlayerAim;
                Message = string.IsNullOrEmpty(LastFeedback) ? "按住你的圆片，向后拖动蓄力，松手出击。  " + GameplayLayouts.Hint(Layout, Rule)
                    : LastFeedback + "  按住圆片向后拖，试试下一手。";
            }
            else
            {
                Phase = MatchPhase.NpcThinking; Message = npc.displayName + "在找角度……";
                StartCoroutine(NpcShot());
            }
            Changed?.Invoke();
        }

        bool AllClaimed() { foreach (var disc in targets) if (!disc.Claimed) return false; return true; }

        void SpawnStriker()
        {
            readyPoseValid = false;
            if (striker != null) Destroy(striker.gameObject);
            var definition = playerTurn || npc == null || npc.startingCardIndexes == null || npc.startingCardIndexes.Length == 0 ? selectedCard : GameCatalog.Cards[npc.startingCardIndexes[0]];
            var p = definition.Physics;
            striker = DiscBody.Spawn(arena, definition.artIndex, launch, p.massGrams / 4.8f,
                p.thicknessMeters / .0027f, p.grip / .64f, Mathf.Clamp01(p.flipEase - .5f));
            striker.Arena = world.Terrain;
            Vector3 readyPosition = launch; readyPosition.y = RestHeight(striker, launch, Quaternion.identity, striker.RestClearance * 1.3f);
            striker.RestorePose(readyPosition, Quaternion.identity, false);
            striker.ConfigureAppearance(playerTurn ? PlayerCardWear : .12f, playerTurn && PlayerCardTreasured);
            striker.Body.isKinematic = true; striker.Hit += OnDiscHit;
            // 换站位和调片角只是准备动作，不能把停稳的目标片推走；真实释放时才参加碰撞。
            foreach (var collider in striker.PhysicsColliders) collider.enabled = false;
            UpdateReadyPose();
        }

        Vector3 PlayerLaunch()
        {
            Vector3 position = ringCenter + Quaternion.Euler(0, PlayerAzimuth, 0) * (defaultLaunch - ringCenter);
            // 全方位站位也要让片缘越过近侧高路沿的视线，最低32°俯角仍保留完整纸边。
            // .31m半径 + .36m路沿 / tan(32°) + 姿态/取景余量。
            return world.Terrain == null ? position : world.Terrain.ClampInside(position, 1.02f);
        }

        public bool SetPlayerAzimuth(float degrees)
        {
            if (!Finite(degrees) || Phase != MatchPhase.PlayerAim || IsDragging || Time.timeScale <= 0 || IsGuidedLayout || GuidedResultHeld) return false;
            PlayerAzimuth = Mathf.Repeat(degrees, 360); launch = PlayerLaunch();
            aim = ringCenter - launch; aim.y = 0; aim.Normalize();
            world.SetMatchOrbit(PlayerAzimuth, world.MatchElevation, world.MatchDistance);
            UpdateReadyPose(); Changed?.Invoke(); return true;
        }

        public bool SetDiscAngles(float pitch, float roll)
        {
            if (!Finite(pitch) || !Finite(roll) || Phase != MatchPhase.PlayerAim || Time.timeScale <= 0 || IsGuidedLayout || GuidedResultHeld) return false;
            TiltPitch = Mathf.Clamp(pitch, -35, 35); TiltRoll = Mathf.Clamp(roll, -35, 35);
            UpdateReadyPose(); Changed?.Invoke(); return true;
        }

        public bool ResetShotSetup()
        {
            if (!SetPlayerAzimuth(0)) return false;
            Spin = 0; return SetDiscAngles(0, 0);
        }

        Quaternion ShotRotation(Vector3 direction, ShotKind kind, float spin, bool usePlayerAngles)
        {
            Quaternion basis = usePlayerAngles ? Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward, Vector3.up) : Quaternion.identity;
            return basis * Quaternion.Euler((kind == ShotKind.Skim ? -3 : -15) + (usePlayerAngles ? TiltPitch : 0), 0,
                spin * 12 + (usePlayerAngles ? TiltRoll : 0));
        }

        void UpdateReadyPose()
        {
            if (striker == null || !striker.Body.isKinematic || striker.Claimed) return;
            Quaternion rotation = ReadyRotation;
            if (readyPoseValid && launch == readyLaunch && Quaternion.Angle(readyRotation, rotation) < .001f &&
                (striker.Body.position - readyPosition).sqrMagnitude < .00000001f && Quaternion.Angle(striker.Body.rotation, rotation) < .001f) return;
            Vector3 position = launch; position.y = RestHeight(striker, launch, rotation, striker.RestClearance * 1.3f);
            readyLaunch = launch; readyPosition = position; readyRotation = rotation; readyPoseValid = true;
            if ((striker.Body.position - position).sqrMagnitude < .00000001f && Quaternion.Angle(striker.Body.rotation, rotation) < .001f) return;
            striker.transform.SetPositionAndRotation(position, rotation);
            striker.Body.position = position; striker.Body.rotation = rotation; Physics.SyncTransforms();
        }

        internal float RestHeight(DiscBody disc, Vector3 position, Quaternion rotation, float clearance)
        {
            Vector3 vertical = rotation * Vector3.up * disc.HalfHeight;
            float height = world.SurfaceHeight(position) + Mathf.Abs(vertical.y);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                Vector3 rim = rotation * new Vector3(Mathf.Cos(angle) * disc.Radius, 0, Mathf.Sin(angle) * disc.Radius);
                height = Mathf.Max(height, world.SurfaceHeight(position + rim) - rim.y + Mathf.Abs(vertical.y));
            }
            return height + clearance;
        }

        bool OverlapsTerrain(DiscBody disc, Vector3 position, Quaternion rotation)
        {
            // 实际碰撞形状不重叠时，合法靠台阶/靠墙快照必须逐位恢复，不能被理想圆面抬高。
            foreach (var surface in world.Terrain.SurfaceColliders)
            {
                if (surface == null || surface == world.FloorCollider || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                foreach (var shape in disc.PhysicsColliders)
                    if (Physics.ComputePenetration(shape, position + rotation * shape.transform.localPosition,
                        rotation * shape.transform.localRotation, surface, surface.transform.position, surface.transform.rotation,
                        out Vector3 direction, out float distance) && distance > disc.DeepPenetrationTolerance) return true;
            }
            return false;
        }

        void UpdateOrbitControls()
        {
            if (IsDragging || IsGuidedLayout || GuidedResultHeld || Application.isMobilePlatform) { orbitDragging = false; return; }
            Vector2 pointer = Input.mousePosition;
            bool blocked = BlocksInput?.Invoke(pointer) ?? false;
            if (Input.GetMouseButtonDown(2) && !blocked) { orbitDragging = true; orbitPointer = pointer; }
            float azimuthDelta = 0, elevationDelta = 0;
            if (orbitDragging && Input.GetMouseButton(2))
            {
                Vector2 delta = pointer - orbitPointer; orbitPointer = pointer;
                azimuthDelta = -delta.x * .25f; elevationDelta = -delta.y * .15f;
            }
            if (Input.GetMouseButtonUp(2)) orbitDragging = false;
            if (Input.GetKey(KeyCode.A)) azimuthDelta -= 70 * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.D)) azimuthDelta += 70 * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.W)) elevationDelta += 30 * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.S)) elevationDelta -= 30 * Time.unscaledDeltaTime;
            if (azimuthDelta != 0) SetPlayerAzimuth(PlayerAzimuth + azimuthDelta);
            float scroll = blocked ? 0 : Input.mouseScrollDelta.y;
            if (elevationDelta != 0 || scroll != 0)
            {
                world.SetMatchOrbit(PlayerAzimuth, world.MatchElevation + elevationDelta, world.MatchDistance - scroll * .65f);
                Changed?.Invoke();
            }
        }

        void Update()
        {
            // 暂停时既不接受出手，也不让慢镜头计时自行恢复 timeScale。
            if (Time.timeScale <= 0) return;
            if (Phase == MatchPhase.Waiting && remainingStartDelay > 0 && !GuidedResultHeld)
            { remainingStartDelay -= Time.deltaTime; if (remainingStartDelay <= 0) NextTurn(); }
            if (slowTime > 0) { slowTime -= Time.unscaledDeltaTime; if (slowTime <= 0) Time.timeScale = 1; }
            if (Phase != MatchPhase.PlayerAim) return;
            bool changed = false;
            if (Input.GetKeyDown(KeyCode.Alpha1)) { Shot = ShotKind.Slap; changed = true; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { Shot = ShotKind.Smash; changed = true; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { Shot = ShotKind.Skim; changed = true; }
            UpdateOrbitControls();
            float previousSpin = Spin;
            if (Input.GetKey(KeyCode.Q)) Spin = Mathf.Max(-1, Spin - Time.deltaTime);
            if (Input.GetKey(KeyCode.E)) Spin = Mathf.Min(1, Spin + Time.deltaTime);
            if (changed || !Mathf.Approximately(previousSpin, Spin)) Changed?.Invoke();
            if (!GuidedResultHeld) UpdateReadyPose();
            ReadPointer(out Vector2 pointer, out bool pointerDown, out bool pointerHeld, out bool pointerUp, out bool pointerCancelled);
            ProcessPointer(pointer, pointerDown, pointerHeld, pointerUp, pointerCancelled);
        }

        // 鼠标、触屏与原生验收使用同一屏幕输入路径；验收不跳过拖拽直接 Launch。
        internal void ProcessPointer(Vector2 pointer, bool pointerDown, bool pointerHeld, bool pointerUp, bool pointerCancelled)
        {
            if (Phase != MatchPhase.PlayerAim || Time.timeScale <= 0 || GuidedResultHeld) return;
            if (pointerCancelled || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            { if (IsDragging) CancelAim(); return; }
            if (orbitDragging) return;
            if (pointerDown && !(BlocksInput?.Invoke(pointer) ?? false) && FloorPoint(pointer, out var point))
            {
                IsDragging = true; dragStart = point; Power = 0;
                world.SetMatchInteractionLocked(true);
                // 从目标附近开始拉时先提供大致朝向；从出手片开始拉时采用传统弹弓方向。
                aim = point - launch; aim.y = 0;
                if (aim.sqrMagnitude < .16f) aim = ringCenter - launch;
                aim.y = 0; aim.Normalize();
                Changed?.Invoke();
            }
            if (IsDragging && (pointerHeld || pointerUp) && FloorPoint(pointer, out var current))
            {
                Vector3 pull = dragStart - current; pull.y = 0;
                if (pull.magnitude > .12f) aim = pull.normalized;
                aim.Normalize();
                Power = Mathf.Clamp01(pull.magnitude / 2.25f);
                DrawAim(); Changed?.Invoke();
            }
            if (IsDragging && pointerUp)
            {
                IsDragging = false;
                world.SetMatchInteractionLocked(false);
                if (Power > .06f) Launch(aim, Power, Spin, Shot); else HideAim();
                Changed?.Invoke();
            }
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
            // UI Toolkit and the arena both use screen coordinates with an origin at the lower-left.
            // Lock the shot to its first finger so a second finger cannot take over the drag.
            pointer = touch.position;
            down = touch.phase == TouchPhase.Began;
            held = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            up = touch.phase == TouchPhase.Ended;
            cancelled = touch.phase == TouchPhase.Canceled;
            if (up || cancelled) activeFingerId = -1;
        }

        bool FloorPoint(Vector2 screenPoint, out Vector3 point)
        {
            var ray = world.MainCamera.ScreenPointToRay(screenPoint);
            // 平面保证拖拽移到画面边缘时仍能控制，避免地面 collider 尺寸影响蓄力。
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = Vector3.zero; return false;
        }

        void DrawAim()
        {
            audioFx?.PreviewCharge(Power);
            aimLine.enabled = true; aimLine.SetPosition(0, world.SurfacePoint(launch, .08f));
            aimLine.SetPosition(1, world.SurfacePoint(launch + aim * (0.6f + Power * .65f), .08f));
            // 只给力道的宽泛反馈，不预测碰撞或精确轨迹。
            landingRing.enabled = true;
            float radius = .37f + Power * .12f;
            for (int i = 0; i < 48; i++)
            {
                float angle = i / 48f * Mathf.PI * 2;
                landingRing.SetPosition(i, world.SurfacePoint(launch + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), .05f));
            }
            // 教学只显示未发生碰撞前的大致落地范围；不预测翻面或得分。
            if (IsGuidedLayout && guidedLandingRing != null && Shot != ShotKind.Skim)
            {
                guidedLandingRing.enabled = Power > .06f;
                DrawSurfaceCircle(guidedLandingRing, launch + aim * (1.25f + Power * 3.3f), .34f);
                aimLine.SetPosition(1, world.SurfacePoint(launch + aim * (1.25f + Power * 3.3f), .08f));
            }
            else if (IsGuidedLayout && Shot == ShotKind.Skim)
                aimLine.SetPosition(1, world.SurfacePoint(launch + aim * 3.9f, .08f));
            UpdateReadyPose();
        }

        void HideAim() { if (aimLine != null) aimLine.enabled = false; if (landingRing != null) landingRing.enabled = false; if (guidedLandingRing != null) guidedLandingRing.enabled = false; audioFx?.PreviewCharge(0); }

        public void CancelAim()
        {
            bool wasDragging = IsDragging;
            IsDragging = false; orbitDragging = false; world?.SetMatchInteractionLocked(false); Power = 0; HideAim(); UpdateReadyPose(); Changed?.Invoke();
            if (wasDragging) AimCancelled?.Invoke();
        }

        void OnApplicationFocus(bool focused) { if (!focused) CancelAim(); }

        public void Launch(Vector3 direction, float power, float spin, ShotKind kind)
        {
            if ((Phase != MatchPhase.PlayerAim && Phase != MatchPhase.NpcThinking) || Time.timeScale <= 0 || striker == null) return;
            if (!Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z) || !Finite(power) || !Finite(spin) || !Enum.IsDefined(typeof(ShotKind), kind)) return;
            direction.y = 0;
            if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
            direction.Normalize(); power = Mathf.Clamp01(power); spin = Mathf.Clamp(spin, -1, 1);
            HideAim(); IsDragging = false; world.SetMatchInteractionLocked(false); Phase = MatchPhase.Flying; Power = power;
            flyingShot = kind; shotPower = power; shockTriggered = false; quietTime = flightTime = 0; ShotsUsed++;
            flyingSpin = spin; airDelay = -1; aim = direction.normalized;
            touchedTargets.Clear(); largestMovement = 0; lowestUpDot = 1; lowestContactNormalY = 1; shotCollisionCount = 0;
            for (int i = 0; i < targets.Count; i++) shotStartPositions[i] = targets[i].Body.position;
            float height = kind == ShotKind.Slap ? 1.15f : kind == ShotKind.Smash ? 1.5f : .12f;
            float vy = kind == ShotKind.Skim ? .12f : -2.0f;
            float timeToFloor = (vy + Mathf.Sqrt(vy * vy + 2 * 9.81f * height)) / 9.81f;
            float range = 1.25f + power * 3.3f;
            float horizontal = kind == ShotKind.Skim ? 3f + power * 8f : range / Mathf.Max(.1f, timeToFloor);
            Quaternion shotRotation = ShotRotation(direction, kind, spin, playerTurn && !IsGuidedLayout);
            Vector3 start = launch + Vector3.up * (height + world.SurfaceHeight(launch));
            if (kind == ShotKind.Skim) start.y = RestHeight(striker, launch, shotRotation, striker.RestClearance);
            striker.Body.isKinematic = false;
            // 插值刚体必须同步物理姿态，避免从旧的待出手位置提前触地触发拍击。
            striker.Body.position = start;
            striker.Body.rotation = shotRotation;
            foreach (var collider in striker.PhysicsColliders) collider.enabled = true;
            shotStartStrikerPosition = start;
            Physics.SyncTransforms();
            striker.Body.linearVelocity = direction.normalized * horizontal + Vector3.up * vy;
            Vector3 flipAxis = playerTurn && !IsGuidedLayout ? Vector3.Cross(Vector3.up, direction) : Vector3.right;
            striker.Body.angularVelocity = Vector3.up * spin * 19 + flipAxis * (kind == ShotKind.Skim ? -2 : 3);
            world.Shake(.022f + power * .025f, .12f); audioFx.PlayShot(kind, power);
            Message = playerTurn ? "看这一手！" : npc.displayName + "出手了！"; Changed?.Invoke();
        }

        void OnDiscHit(DiscBody disc, float speed)
        {
            if (Phase != MatchPhase.Flying) return;
            shotCollisionCount++;
            lowestContactNormalY = Mathf.Min(lowestContactNormalY, disc.LastHitNormal.y);
            var hitDisc = DiscBody.FromCollider(disc.LastHitCollider);
            if (disc.IsTarget && hitDisc != null) touchedTargets.Add(disc);
            if (Time.unscaledTime - lastImpactSoundTime > .035f)
            {
                lastImpactSoundTime = Time.unscaledTime;
                var material = GameCatalog.Cards[disc.ArtIndex].prototypeId;
                audioFx.PlayImpact(material == "metal" ? ImpactMaterial.Metal : material == "rubber" ? ImpactMaterial.Rubber : ImpactMaterial.Paper, speed, .45f, disc.Body.position);
            }
            if (disc == striker && !shockTriggered && disc.Body.position.y < world.SurfaceHeight(disc.Body.position) + .4f &&
                (hitDisc != null || disc.LastHitNormal.y > .35f))
            {
                // 斜擦一路贴地，真正碰到目标片才触发推力，擦地本身不消耗这次拍击。
                if (flyingShot == ShotKind.Skim && (hitDisc == null || !hitDisc.IsTarget)) return;
                shockTriggered = true;
                Vector3 impact = world.SurfacePoint(disc.Body.position, .02f);
                PhysicsDiagnostic?.Invoke("Air impact turn=" + Turn + " kind=" + flyingShot + " power=" + shotPower.ToString("0.00") + " body=" + disc.Body.position);
                world.Punch(impact, .6f + shotPower * .75f);
                // 接触解算时出手片仍压住目标。让气流传播片刻，待两片解离后掀边。
                airImpact = impact; airDelay = flyingShot == ShotKind.Skim ? .01f : .095f;
            }
        }

        void ApplyAirImpulse()
        {
            foreach (var target in targets)
            {
                    if (target.Claimed) continue;
                    Vector3 offset = target.Body.position - airImpact; offset.y = 0;
                    float centerDistance = offset.magnitude;
                    float distance = flyingShot == ShotKind.Skim ? Mathf.Max(0, centerDistance - target.Radius - striker.Radius) : centerDistance;
                    float reach = flyingShot == ShotKind.Slap ? 1.3f : flyingShot == ShotKind.Smash ? .82f : .7f;
                    if (distance > reach) continue;
                    // 台阶另一侧的片不会隔着立面受到同样的贴地气流。
                    if (Mathf.Abs(world.SurfaceHeight(target.Body.position) - airImpact.y) > .16f) continue;
                    float falloff = Mathf.Pow(1 - distance / reach, flyingShot == ShotKind.Slap ? .25f : .75f);
                    Vector3 away = centerDistance > .02f ? offset.normalized : aim;
                    float impulse = (.32f + shotPower * .68f) * falloff;
                    float lift = flyingShot == ShotKind.Slap ? target.Weight * (.125f + shotPower * .025f) * falloff :
                        flyingShot == ShotKind.Smash ? target.Weight * (.145f + shotPower * .035f) * falloff : target.Weight * .07f * impulse;
                    Vector3 force = Vector3.up * lift + away * (flyingShot == ShotKind.Skim ? .23f : .06f) * impulse;
                    // 气流从圆片边缘掀起，翻转由刚体扭矩产生，绝不直接设置翻面结果。
                    Vector3 edge = target.Body.position - away * (.23f + target.EdgeLift * .045f);
                    target.Body.AddForceAtPosition(force, edge, ForceMode.Impulse);
                    target.Body.AddTorque(away * (flyingSpin * .007f * falloff), ForceMode.Impulse);
            }
        }

        void FixedUpdate()
        {
            if (Phase != MatchPhase.Flying) return;
            flightTime += Time.fixedDeltaTime;
            if (airDelay >= 0)
            {
                airDelay -= Time.fixedDeltaTime;
                if (airDelay <= 0) { airDelay = -1; ApplyAirImpulse(); }
            }
            bool quiet = flightTime > .7f;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Claimed) continue;
                largestMovement = Mathf.Max(largestMovement, Vector3.Distance(target.Body.position, shotStartPositions[i]));
                lowestUpDot = Mathf.Min(lowestUpDot, Vector3.Dot(target.transform.up, Vector3.up));
                if (!target.Quiet) quiet = false;
                if (flightTime > 8) { target.Body.linearDamping = 2.5f; target.Body.angularDamping = 3; }
            }
            if (striker != null) largestMovement = Mathf.Max(largestMovement, Vector3.Distance(striker.Body.position, shotStartStrikerPosition));
            if (striker != null && !striker.Quiet && flightTime < 8) quiet = false;
            quietTime = quiet ? quietTime + Time.fixedDeltaTime : 0;
            if (quietTime > .35f) Settle();
        }

        void Settle()
        {
            int gained = 0;
            bool rare = false;
            var observation = new ShotObservation
            {
                turn = Turn,
                playerControlled = playerTurn,
                shot = flyingShot,
                shotKind = flyingShot,
                normalizedPower = Mathf.Clamp01(shotPower),
                normalizedSpin = Mathf.Clamp(flyingSpin, -1, 1),
                collisionCount = shotCollisionCount,
                collisions = shotCollisionCount,
                targetCollisionCount = touchedTargets.Count,
                targetCollisions = touchedTargets.Count,
                maxMovement = largestMovement,
                lowestContactNormalY = lowestContactNormalY,
                finalStrikerPosition = striker == null ? Vector3.zero : striker.Body.position,
                finalStrikerRotation = striker == null ? Quaternion.identity : striker.Body.rotation,
                finalStrikerUpDot = striker == null ? 1 : Vector3.Dot(striker.transform.up, Vector3.up)
            };
            float finalTargetUpDot = 1;
            bool capturedTargetPose = false;
            foreach (var target in targets)
            {
                if (target.Claimed) continue;
                var offset = target.Body.position - ringCenter; offset.y = 0;
                bool scored = Rule == RuleKind.Flip ? target.FaceDown : offset.magnitude > ringRadius + target.Radius;
                finalTargetUpDot = Mathf.Min(finalTargetUpDot, Vector3.Dot(target.transform.up, Vector3.up));
                if (!capturedTargetPose)
                {
                    observation.finalTargetPosition = target.Body.position;
                    observation.finalTargetRotation = target.Body.rotation;
                    capturedTargetPose = true;
                }
                observation.flipped |= target.FaceDown;
                observation.ringOut |= offset.magnitude > ringRadius + target.Radius;
                if (!scored) continue;
                target.MarkClaimed(); gained++;
                rare |= GameCatalog.Cards[target.ArtIndex].rarity >= CardRarity.Rare;
                StartCoroutine(CollectAnimation(target));
            }
            observation.finalTargetUpDot = finalTargetUpDot;
            observation.scoredTargetCount = gained;
            observation.scored = gained > 0;
            if (playerTurn) PlayerScore += gained; else NpcScore += gained;
            if (playerTurn)
            {
                playerMissStreak = gained == 0 ? playerMissStreak + 1 : 0;
                playerScoringStreak = gained > 0 ? playerScoringStreak + 1 : 0;
                LastFeedback = BuildPlayerFeedback(gained);
            }
            if (gained > 0)
            {
                audioFx.PlayCollect(gained, rare);
                if (playerTurn && (gained >= 2 || playerScoringStreak >= 2)) audioFx.PlayCombo(Mathf.Max(gained, playerScoringStreak));
                world.Shake(.06f, .15f);
                string word = gained >= 3 ? "一拍三连！" : gained == 2 ? "漂亮！双翻！" : Rule == RuleKind.Flip ? "翻了！" : "出圈！";
                Highlight?.Invoke(word); Message = word + "  " + (playerTurn ? "你" : npc.displayName) + "拿到 " + gained + " 张。";
                Time.timeScale = .55f; slowTime = .35f;
            }
            else
            {
                Message = "";
                Highlight?.Invoke("");
            }
            PhysicsDiagnostic?.Invoke("Pog settle layout=" + Layout + " turn=" + Turn + " gained=" + gained + " move=" + largestMovement.ToString("0.00") + " minUp=" + lowestUpDot.ToString("0.00") + " contacts=" + touchedTargets.Count);
            LastShotObservation = observation;
            Phase = MatchPhase.Waiting; remainingStartDelay = 1.2f; Changed?.Invoke();
            ShotSettled?.Invoke(observation);
        }

        string BuildPlayerFeedback(int gained)
        {
            if (gained <= 0) return "";
            if (gained >= 3) return "一手连收三张！落点和力道都拿捏住了。";
            if (gained == 2) return Rule == RuleKind.Flip ? "双翻漂亮！看看邻片之间还能不能借一次气流。" : "两张完整出圈，内侧这一擦很有效。";
            if (gained == 1) return playerScoringStreak >= 2 ? "连续两手有收获，照着这份稳劲儿继续。" : Rule == RuleKind.Flip ? "这一张翻稳了。下一手先找落点，再加旋转。" : "整张越线才收下，这一擦算得漂亮。";
            return "";
        }

        IEnumerator CollectAnimation(DiscBody disc)
        {
            yield return new WaitForSeconds(.5f);
            var start = disc.transform.position; var rotation = disc.transform.rotation; float t = 0;
            while (t < 1 && disc != null)
            {
                t += Time.deltaTime * 1.8f;
                disc.transform.position = start + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * .7f;
                disc.transform.rotation = rotation * Quaternion.Euler(0, t * 220, 0);
                disc.transform.localScale = Vector3.one * Mathf.Lerp(1, .01f, t);
                yield return null;
            }
            if (disc != null) disc.gameObject.SetActive(false);
        }

        IEnumerator NpcShot()
        {
            yield return new WaitForSeconds(1.2f);
            if (Phase != MatchPhase.NpcThinking) yield break;
            DiscBody chosen = null;
            float best = float.NegativeInfinity;
            foreach (var target in targets)
            {
                if (target.Claimed) continue;
                float score = EvaluateNpcTarget(target) + ((float)NextRandom() - .5f) * (1 - npc.skill) * .65f;
                if (score > best) { best = score; chosen = target; }
            }
            if (chosen == null) { Finish(MatchOutcome.Draw); yield break; }
            float error = Mathf.Lerp(.85f, .12f, npc.skill);
            Vector3 point = chosen.Body.position;
            ShotKind kind = Rule == RuleKind.RingOut ? ShotKind.Skim : npc.preferredShot;
            Vector3 outward = point - ringCenter; outward.y = 0;
            if (Rule == RuleKind.RingOut)
            {
                // 瞄向片的内侧，令碰撞方向尽量靠向最近边界。
                point -= outward.normalized * (.06f + npc.skill * .08f);
            }
            else
            {
                int neighbours = 0;
                Vector3 center = point;
                foreach (var target in targets)
                    if (target != chosen && !target.Claimed && Vector3.Distance(target.Body.position, point) < 1.12f) { center += target.Body.position; neighbours++; }
                if (kind == ShotKind.Skim || (npc.skill > .55f && neighbours >= 2)) kind = ShotKind.Slap;
                // 有经验的对手会瞄向小簇的间隙，但依然只发射自己的刚体。
                if (neighbours > 0) point = Vector3.Lerp(point, center / (neighbours + 1), npc.skill * .20f);
                var lateral = Vector3.Cross(Vector3.up, (point - launch).normalized);
                point += lateral * (((float)NextRandom() - .5f) * .18f * npc.skill);
            }
            point += new Vector3(((float)NextRandom() - .5f) * error, 0, ((float)NextRandom() - .5f) * error);
            Vector3 direction = point - launch; direction.y = 0;
            float power = Mathf.Clamp((direction.magnitude - 1.25f) / 3.3f + ((float)NextRandom() - .5f) * error * .25f, .25f, .96f);
            // 台阶和砖沿会阻住贴地斜擦，伙伴应改用真实的空中掌拍越过障碍。
            // 检查整张出手片的通道，不能只采样目标中心的地面高度。
            if (Rule == RuleKind.RingOut && SkimPathNeedsLift(chosen, point))
            {
                kind = ShotKind.Slap;
                const float height = 1.15f, gravity = 9.81f, vy = -2f;
                float startHeight = height + world.SurfaceHeight(launch);
                float drop = Mathf.Max(.05f, startHeight - chosen.Body.position.y);
                float targetTime = (vy + Mathf.Sqrt(vy * vy + 2 * gravity * drop)) / gravity;
                float floorTime = (vy + Mathf.Sqrt(vy * vy + 2 * gravity * height)) / gravity;
                power = Mathf.Clamp((direction.magnitude * floorTime / Mathf.Max(.1f, targetTime) - 1.25f) / 3.3f, .25f, .96f);
            }
            Spin = ((float)NextRandom() - .5f) * npc.skill * (Rule == RuleKind.RingOut ? .65f : 1f);
            power = Rule == RuleKind.RingOut && kind == ShotKind.Skim ? Mathf.Clamp(power + .32f, .65f, .96f) : power;
            PhysicsDiagnostic?.Invoke("NPC plan rule=" + Rule + " target=" + chosen.ArtIndex + " kind=" + kind + " score=" + best.ToString("0.00") + " power=" + power.ToString("0.00") + " aim=" + point);
            Launch(direction.normalized, power, Spin, kind);
        }

        float EvaluateNpcTarget(DiscBody target)
        {
            var toTarget = target.Body.position - launch; toTarget.y = 0;
            float distance = toTarget.magnitude;
            Vector3 direction = toTarget.normalized;
            float obstruction = 0;
            int neighbours = 0;
            foreach (var other in targets)
            {
                if (other == target || other.Claimed) continue;
                var delta = other.Body.position - launch; delta.y = 0;
                float along = Vector3.Dot(delta, direction);
                if (along > .15f && along < distance - .35f && (delta - direction * along).magnitude < other.Radius + .16f) obstruction += 1;
                if (Vector3.Distance(other.Body.position, target.Body.position) < 1.12f) neighbours++;
            }
            float reachPenalty = distance > 4.55f ? (distance - 4.55f) * 3 : 0;
            if (Rule == RuleKind.RingOut)
            {
                var radial = target.Body.position - ringCenter; radial.y = 0;
                float outwardAlignment = radial.sqrMagnitude > .01f ? Vector3.Dot(direction, radial.normalized) : 0;
                float terrainPenalty = SkimPathNeedsLift(target, target.Body.position) ? .8f : 0;
                return radial.magnitude * 1.45f + outwardAlignment * 1.8f - obstruction * 1.2f - terrainPenalty - distance * .08f - reachPenalty;
            }
            float tilt = 1 - Mathf.Clamp01(Vector3.Dot(target.transform.up, Vector3.up));
            return neighbours * (.40f + npc.skill * .55f) + target.EdgeLift * .5f + tilt * .45f - obstruction * .30f - distance * .15f - reachPenalty;
        }

        bool SkimPathNeedsLift(DiscBody target, Vector3 point)
        {
            if (striker == null || world == null) return false;
            Vector3 delta = point - launch; delta.y = 0;
            float distance = delta.magnitude;
            if (distance < .05f) return false;
            Vector3 direction = delta / distance;
            Vector3 lateral = Vector3.Cross(Vector3.up, direction) * striker.Radius;
            float baseHeight = RestHeight(striker, launch, Quaternion.identity, 0) - striker.HalfHeight;
            float last = Mathf.Max(0, distance - target.Radius - striker.Radius);
            for (float along = .12f; along <= last; along += .12f)
            {
                Vector3 sample = launch + direction * along;
                float height = Mathf.Max(world.SurfaceHeight(sample),
                    Mathf.Max(world.SurfaceHeight(sample + lateral), world.SurfaceHeight(sample - lateral)));
                if (height > baseHeight + .018f) return true;
            }
            // 目标的片边也可能搭在台面上，中心落在低地不能代表可擦到。
            return RestHeight(target, target.Body.position, Quaternion.identity, 0) - target.HalfHeight > baseHeight + .018f;
        }

        void Finish(MatchOutcome outcome)
        {
            Phase = MatchPhase.Finished; HideAim(); Time.timeScale = 1; slowTime = 0; audioFx.Duck(false);
            Message = outcome == MatchOutcome.PlayerWin ? "这一局，赢得漂亮。" : outcome == MatchOutcome.NpcWin ? "它还在朋友手里，随时可以赢回来。" : "旗鼓相当，再来一局？";
            audioFx.Play(outcome == MatchOutcome.NpcWin ? "lose" : "win", .7f); Changed?.Invoke(); Completed?.Invoke(outcome);
        }

        public void StopMatch()
        {
            GuidedResultHeld = false; IsGuidedLayout = false;
            GuidedAimPoint = Vector3.zero; GuidedSuggestedPower = 0;
            guidedTargetRing = guidedActionArrow = guidedLandingRing = null;
            StopAllCoroutines(); Time.timeScale = 1; slowTime = 0; remainingStartDelay = 0; Phase = MatchPhase.Waiting;
            airDelay = -1;
            IsDragging = false; orbitDragging = false; readyPoseValid = false; world?.SetMatchInteractionLocked(false); HideAim(); targets.Clear(); baseDamping.Clear(); touchedTargets.Clear(); striker = null;
            if (arena != null) { arena.gameObject.SetActive(false); Destroy(arena.gameObject); } arena = null;
            foreach (var material in lineMaterials) if (material != null) Destroy(material); lineMaterials.Clear();
        }
        void OnDisable() { Time.timeScale = 1; }
    }
}
