using System;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    [Serializable]
    public sealed class CompanionSnapshot
    {
        public string npcId, gesture;
        public int style;
        public Color shirtColor, bagColor;
        public bool isOpponent;
        public Vector3 rootPosition;
        public Quaternion rootRotation, leftArmRotation, rightArmRotation, headRotation;
    }

    /// <summary>原创微缩儿童与围观动作；没有 collider，也不参与得分或物理。</summary>
    public sealed class CrowdActors : MonoBehaviour
    {
        sealed class Actor
        {
            public Transform Root, Hips, Torso, Head, LeftArm, RightArm, LeftForearm, RightForearm, LeftLeg, RightLeg;
            public Transform Mouth;
            public readonly Transform[] Eyes = new Transform[6];
            public readonly Vector3[] EyeScales = new Vector3[6];
            public Quaternion RootRotation, BaseHeadPose;
            public Vector3 RestPosition;
            public float Phase, Height;
            public bool Girl;
            public int Style;
            public float ReactionDelay;
            public CompanionPersonality.Profile Profile;
            public bool IsOpponent, ResultGesture;
            public string Gesture = "idle";
            public float GestureTime, GestureDuration;
            public MatchOutcome Outcome;
        }

        readonly List<Actor> actors = new List<Actor>();
        readonly List<Material> materials = new List<Material>();
        SceneWorld world;
        PogMatch pogs;
        MarbleMatch marbles;
        Transform crowdRoot, slapHand;
        Material skin, skinShadow, hair, dark, white, sole, cheek, brass;
        string currentLocation;
        string companionNpcId;
        bool companionIsOpponent;
        float motionClock;
        MatchPhase oldPogPhase, oldMarblePhase;
        int oldPogScore, oldNpcScore, oldMarbleScore;
        float cheerTime, shotTime, handTime;
        bool activeMatch;
        float cheerStartedAt, missTime, focusTime, nextFocusSearch;
        int scoreAtShot;
        Vector3 focusPoint = new Vector3(0f,.15f,.55f), forcedFocusPoint;
        DiscBody focusDisc;

        public void Configure(SceneWorld scene, PogMatch pogMatch, MarbleMatch marbleMatch)
        {
            world = scene; pogs = pogMatch; marbles = marbleMatch;
            Rebuild();
        }

        public void Configure(SceneWorld scene, PogMatch pogMatch) { Configure(scene, pogMatch, null); }

        public void Rebuild()
        {
            Clear();
            if (world == null) return;
            currentLocation = world.EnvironmentSignature;
            crowdRoot = new GameObject("街区伙伴 · 围观的小朋友").transform;
            crowdRoot.SetParent(transform, false);
            skin = Mat("温暖的肤色", new Color(.85f, .60f, .40f));
            skinShadow = Mat("肤色细节", new Color(.73f, .42f, .28f));
            hair = Mat("深棕色头发", new Color(.13f, .09f, .061f));
            dark = Mat("眼睛与发梢", new Color(.043f, .055f, .055f));
            white = Mat("棉布白与眼睛", new Color(.94f, .93f, .82f));
            sole = Mat("奶白鞋底", new Color(.77f, .79f, .72f));
            cheek = Mat("微红的脸颊", new Color(.87f, .42f, .34f));
            brass = Mat("黄铜扣子", new Color(.93f, .70f, .32f), .6f, .25f);
            Vector3[] positions =
            {
                new Vector3(-4.22f,0f,1.12f), new Vector3(4.22f,0f,1.75f),
                new Vector3(4.32f,0f,3.05f), new Vector3(-4.32f,0f,2.92f)
            };
            var roster = new List<string>();
            // 被约来的伙伴永远站在最近的首位；身份不再从围观数组的下标推导。
            if (CompanionPersonality.TryGet(companionNpcId, out _)) roster.Add(companionNpcId);
            var location = GameCatalog.GetLocation(world.LocationId);
            foreach (string id in location.npcIds)
                if (!roster.Contains(id)) roster.Add(id);
            int visit = world.Environment != null && world.Environment.geometryVersion >= 2 ? world.Environment.visitVariant : 0;
            int fillOffset = (world.LocationId.Length + visit * 2) % CompanionPersonality.NpcIds.Count;
            for (int n = 0; n < CompanionPersonality.NpcIds.Count; n++)
            {
                if (roster.Count >= 4) break;
                string id = CompanionPersonality.NpcIds[(n + fillOffset) % CompanionPersonality.NpcIds.Count];
                if (!roster.Contains(id)) roster.Add(id);
            }
            for (int i = 0; i < 4; i++)
            {
                if (i > 0 && world.Environment != null && world.Environment.geometryVersion >= 2)
                    positions[i] += new Vector3((i % 2 == 0 ? 1 : -1) * visit * .12f, 0f, (visit - 1) * .24f);
                var profile = CompanionPersonality.Get(roster[i]);
                var actor = BuildActor(profile, positions[i],
                    Mat(profile.DisplayName + "的衣服", profile.ShirtColor), Mat(profile.DisplayName + "的书包", profile.BagColor));
                actor.IsOpponent = companionIsOpponent && profile.NpcId == companionNpcId;
                actors.Add(actor);
            }
            BuildPlayerHand();
            oldPogPhase = pogs == null ? MatchPhase.Waiting : pogs.Phase;
            oldMarblePhase = marbles == null ? MatchPhase.Waiting : marbles.Phase;
            oldPogScore = pogs == null ? 0 : pogs.PlayerScore;
            oldNpcScore = pogs == null ? 0 : pogs.NpcScore;
            oldMarbleScore = marbles == null ? 0 : marbles.Score;
        }

        /// <summary>改变伙伴身份时重建，比赛/结果身份切换只更新标记，不打断同一个人的动作。</summary>
        public void SetCompanionContext(string npcId, bool isOpponent, bool greet = false)
        {
            string nextId = CompanionPersonality.TryGet(npcId, out _) ? npcId : null;
            bool identityChanged = companionNpcId != nextId;
            companionNpcId = nextId;
            companionIsOpponent = nextId != null && isOpponent;
            // 导航可在本帧先换场景；即使上下文同为 null，也不能保留上一个街区的人。
            if (identityChanged || crowdRoot == null || (world != null && currentLocation != world.EnvironmentSignature)) Rebuild();
            foreach (var actor in actors)
            {
                actor.IsOpponent = companionIsOpponent && actor.Profile.NpcId == companionNpcId;
                if (greet && actor.Profile.NpcId == companionNpcId)
                    BeginGesture(actor, actor.Profile.GestureName + "-greeting", 2.6f, false, MatchOutcome.Draw);
            }
        }

        /// <summary>只响应已结算结果，动作不写文本、存档或比分。</summary>
        public void RespondToResult(string npcId, MatchOutcome outcome)
        {
            if (!CompanionPersonality.TryGet(npcId, out _) || !Enum.IsDefined(typeof(MatchOutcome), outcome)) return;
            SetCompanionContext(npcId, false);
            cheerTime = shotTime = missTime = handTime = 0f;
            oldPogPhase = pogs == null ? MatchPhase.Waiting : pogs.Phase;
            oldPogScore = pogs == null ? 0 : pogs.PlayerScore;
            oldNpcScore = pogs == null ? 0 : pogs.NpcScore;
            if (slapHand != null) slapHand.gameObject.SetActive(false);
            foreach (var actor in actors)
                if (actor.Profile.NpcId == npcId)
                {
                    string suffix = outcome == MatchOutcome.PlayerWin ? "player-win" : outcome == MatchOutcome.NpcWin ? "npc-win" : "draw";
                    BeginGesture(actor, actor.Profile.GestureName + "-" + suffix, 3.2f, true, outcome);
                }
        }

        static void BeginGesture(Actor actor, string gesture, float duration, bool result, MatchOutcome outcome)
        {
            actor.Gesture = gesture; actor.GestureTime = 0f; actor.GestureDuration = duration;
            actor.ResultGesture = result; actor.Outcome = outcome;
        }

        /// <summary>原生验收读取真实模型姿态；每次返回独立快照，不向运行状态暴露引用。</summary>
        public List<CompanionSnapshot> CaptureCompanions()
        {
            var result = new List<CompanionSnapshot>(actors.Count);
            foreach (var actor in actors)
                result.Add(new CompanionSnapshot
                {
                    npcId = actor.Profile.NpcId, gesture = actor.Gesture, style = actor.Profile.Style,
                    shirtColor = actor.Profile.ShirtColor, bagColor = actor.Profile.BagColor, isOpponent = actor.IsOpponent,
                    rootPosition = actor.Root.position, rootRotation = actor.Root.rotation,
                    leftArmRotation = actor.LeftArm.localRotation, rightArmRotation = actor.RightArm.localRotation,
                    headRotation = actor.Head.localRotation
                });
            return result;
        }

        Material Mat(string name, Color color, float metallic = 0f, float smoothness = .15f)
        {
            var material = new Material(Shader.Find("Standard"));
            material.name = name; material.color = color;
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true; materials.Add(material); return material;
        }

        Transform Joint(string name, Transform parent, Vector3 position)
        {
            var obj = new GameObject(name).transform;
            obj.SetParent(parent, false); obj.localPosition = position; return obj;
        }

        GameObject Shape(PrimitiveType kind, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(kind);
            if (obj == null)
                throw new InvalidOperationException("人物模型创建失败：" + name + " / " + kind + "。请检查原始模型组件的构建保留配置。");
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var filter = obj.GetComponent<MeshFilter>();
            var renderer = obj.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null)
                throw new InvalidOperationException("人物模型缺少网格或渲染组件：" + name + " / " + kind + "。请检查 Assets/link.xml 的原始模型依赖。");
            renderer.sharedMaterial = material; return obj;
        }

        GameObject Ball(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        { return Shape(PrimitiveType.Sphere, name, parent, position, scale, material); }
        GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        { return Shape(PrimitiveType.Cube, name, parent, position, scale, material); }
        GameObject Limb(string name, Transform parent, Vector3 position, float width, float length, Material material)
        { return Shape(PrimitiveType.Capsule, name, parent, position, new Vector3(width, length * .5f, width), material); }

        Actor BuildActor(CompanionPersonality.Profile profile, Vector3 position, Material shirt, Material bag)
        {
            string name = profile.DisplayName;
            float size = profile.Height;
            bool girl = profile.Girl, cap = profile.Cap;
            int style = profile.Style;
            var actor = new Actor { Root = Joint(name + " · 微缩儿童", crowdRoot, position), RestPosition = position,
                Height = size, Girl = girl, Phase = profile.Phase, Style = style, ReactionDelay = profile.ReactionDelay, Profile = profile };
            float yaw = position.x < 0f ? -28f : 28f;
            actor.RootRotation = Quaternion.Euler(0f, yaw, 0f);
            actor.Root.localRotation = actor.RootRotation; actor.Root.localScale = new Vector3(profile.BodyWidth * size, size, size);
            actor.Hips = Joint("重心", actor.Root, new Vector3(0f, .57f, 0f));
            var shorts = Mat(name + "的短裤", profile.ShortsColor);
            var shoes = Mat(name + "的运动鞋", profile.ShoeColor);
            Ball("圆润的短裤腰", actor.Hips, new Vector3(0f, .045f, 0f), new Vector3(.36f, .23f, .22f), shorts);
            actor.LeftLeg = Joint("左腿", actor.Hips, new Vector3(-.095f, .012f, 0f));
            actor.RightLeg = Joint("右腿", actor.Hips, new Vector3(.095f, .012f, 0f));
            BuildLeg(actor.LeftLeg, shorts, shoes, -1);
            BuildLeg(actor.RightLeg, shorts, shoes, 1);

            actor.Torso = Joint("躯干", actor.Hips, new Vector3(0f, .15f, 0f));
            Ball("柔软棉布上衣", actor.Torso, new Vector3(0f, .13f, 0f), new Vector3(.40f, .47f, .255f), shirt);
            Box("下摆", actor.Torso, new Vector3(0f, -.052f, 0f), new Vector3(.33f, .054f, .20f), shirt);
            Limb("脖子", actor.Torso, new Vector3(0f, .345f, 0f), .112f, .15f, skin);
            Ball("领口", actor.Torso, new Vector3(0f, .292f, -.02f), new Vector3(.20f, .046f, .17f), white);
            for (int side = -1; side <= 1; side += 2)
            {
                var collar = Box("翻领", actor.Torso, new Vector3(side * .055f, .277f, -.128f), new Vector3(.074f, .070f, .016f), white);
                collar.transform.localRotation = Quaternion.Euler(0f, 0f, side * 30f);
                Box("背带", actor.Torso, new Vector3(side * .124f, .14f, -.113f), new Vector3(.026f, .35f, .030f), bag);
            }
            Box("上衣口袋", actor.Torso, new Vector3(-.049f, .118f, -.135f), new Vector3(.080f, .095f, .012f), shirt);
            Box("口袋沿", actor.Torso, new Vector3(-.049f, .16f, -.147f), new Vector3(.083f, .013f, .011f), white);
            for (int i = 0; i < 3; i++) Ball("衬衣纽扣", actor.Torso, new Vector3(.016f, .218f - i * .064f, -.140f), Vector3.one * .014f, brass);
            var scarf = Box("红领巾的一角", actor.Torso, new Vector3(.019f, .240f, -.158f), new Vector3(.052f, .14f, .014f), Mat(name + "的红领巾", new Color(.73f,.22f,.16f)));
            scarf.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);

            actor.LeftArm = Joint("左肩", actor.Torso, new Vector3(-.225f, .25f, 0f));
            actor.RightArm = Joint("右肩", actor.Torso, new Vector3(.225f, .25f, 0f));
            actor.LeftForearm = BuildArm(actor.LeftArm, shirt, -1);
            actor.RightForearm = BuildArm(actor.RightArm, shirt, 1);
            BuildBackpack(actor.Torso, bag);
            actor.Head = Joint("头部", actor.Torso, new Vector3(0f, .435f, -.005f));
            Ball("孩子的圆脸", actor.Head, new Vector3(0f, .085f, 0f), new Vector3(.365f, .405f, .34f), skin);
            Ball("后脑勺的头发", actor.Head, new Vector3(0f, .17f, .055f), new Vector3(.38f, .31f, .305f), hair);
            for (int side = -1; side <= 1; side += 2)
            {
                Ball("耳朵", actor.Head, new Vector3(side * .183f, .079f, .011f), new Vector3(.078f, .105f, .064f), skin);
                Ball("耳廓", actor.Head, new Vector3(side * .197f, .084f, -.016f), new Vector3(.027f, .050f, .019f), skinShadow);
                int eye = side < 0 ? 0 : 3;
                actor.Eyes[eye] = Ball("眼白", actor.Head, new Vector3(side * .076f, .117f, -.151f), new Vector3(.061f, .077f, .022f), white).transform;
                actor.Eyes[eye + 1] = Ball("黑眼睛", actor.Head, new Vector3(side * .071f, .116f, -.167f), new Vector3(.031f, .045f, .013f), dark).transform;
                actor.Eyes[eye + 2] = Ball("瞳孔里的光", actor.Head, new Vector3(side * .071f - .005f, .13f, -.175f), Vector3.one * .008f, white).transform;
                for (int e = eye; e < eye + 3; e++) actor.EyeScales[e] = actor.Eyes[e].localScale;
                var brow = Ball("眉毛", actor.Head, new Vector3(side * .075f, .173f, -.151f), new Vector3(.073f, .014f, .02f), hair);
                brow.transform.localRotation = Quaternion.Euler(0f, 0f, side * -6f);
                Ball("脸颊", actor.Head, new Vector3(side * .109f, .036f, -.142f), new Vector3(.043f, .021f, .014f), cheek);
                if (girl)
                {
                    Ball("小辫子", actor.Head, new Vector3(side * .207f, .085f, .084f), new Vector3(.108f, .24f, .104f), hair);
                    Ball("辫梢", actor.Head, new Vector3(side * .22f, -.06f, .065f), new Vector3(.096f, .11f, .083f), hair);
                    Ball("蝴蝶结左翼", actor.Head, new Vector3(side * .20f - .035f, .17f, .029f), new Vector3(.074f, .049f, .04f), shirt);
                    Ball("蝴蝶结右翼", actor.Head, new Vector3(side * .20f + .035f, .17f, .029f), new Vector3(.074f, .049f, .04f), shirt);
                    Ball("蝴蝶结中间", actor.Head, new Vector3(side * .20f, .17f, .005f), Vector3.one * .039f, brass);
                }
            }
            Ball("小鼻子", actor.Head, new Vector3(0f, .068f, -.176f), new Vector3(.040f, .049f, .035f), skin);
            actor.Mouth = Ball("微笑嘴巴", actor.Head, new Vector3(0f, -.004f, -.152f), new Vector3(.057f, .014f, .018f), skinShadow).transform;
            // 单独的小刘海让发型有层次，不是一颗涂黑的球。
            for (int i = 0; i < 5; i++)
            {
                float x = -.124f + i * .062f;
                var fringe = Ball("刘海发束", actor.Head, new Vector3(x, .236f - Mathf.Abs(x) * .13f, -.105f), new Vector3(.093f, .090f, .071f), hair);
                fringe.transform.localRotation = Quaternion.Euler(0f, 0f, -18f + i * 8f);
            }
            if (cap)
            {
                Ball("棒球帽顶", actor.Head, new Vector3(0f, .286f, .013f), new Vector3(.411f, .15f, .36f), shirt);
                var brim = Ball("棒球帽檐", actor.Head, new Vector3(0f, .248f, -.182f), new Vector3(.36f, .042f, .22f), shirt);
                brim.transform.localRotation = Quaternion.Euler(-7f, 0f, 0f);
                Ball("帽顶扣", actor.Head, new Vector3(0f, .365f, .015f), Vector3.one * .027f, brass);
                Box("帽子小标", actor.Head, new Vector3(0f, .293f, -.157f), new Vector3(.063f, .047f, .012f), white);
            }
            return actor;
        }

        void BuildLeg(Transform leg, Material shorts, Material shoes, int side)
        {
            Limb("短裤裤管", leg, new Vector3(0f, -.085f, 0f), .153f, .23f, shorts);
            Box("裤脚缝", leg, new Vector3(0f, -.174f, -.02f), new Vector3(.138f, .018f, .13f), sole);
            Limb("小腿", leg, new Vector3(0f, -.285f, 0f), .098f, .27f, skin);
            Limb("白袜子", leg, new Vector3(0f, -.393f, 0f), .109f, .135f, white);
            Box("袜口的蓝线", leg, new Vector3(0f, -.347f, -.043f), new Vector3(.10f, .012f, .015f), shorts);
            Ball("运动鞋", leg, new Vector3(0f, -.472f, -.059f), new Vector3(.155f, .117f, .267f), shoes);
            Ball("鞋底", leg, new Vector3(0f, -.512f, -.067f), new Vector3(.164f, .046f, .28f), sole);
            Ball("运动鞋白鞋头", leg, new Vector3(0f, -.465f, -.158f), new Vector3(.141f, .071f, .074f), white);
            for (int i = 0; i < 3; i++) Box("鞋带", leg, new Vector3(0f, -.42f, -.101f + i * .027f), new Vector3(.071f, .012f, .009f), white);
            var stripe = Box("运动鞋侧条纹", leg, new Vector3(side * .075f, -.464f, -.05f), new Vector3(.012f, .025f, .10f), white);
            stripe.transform.localRotation = Quaternion.Euler(0f, 0f, side * 13f);
        }

        Transform BuildArm(Transform shoulder, Material shirt, int side)
        {
            Limb("短袖", shoulder, new Vector3(side * .018f, -.055f, 0f), .144f, .19f, shirt);
            Limb("上臂", shoulder, new Vector3(side * .025f, -.163f, 0f), .094f, .19f, skin);
            var forearm = Joint("肘关节", shoulder, new Vector3(side * .026f, -.244f, 0f));
            Limb("小臂", forearm, new Vector3(0f, -.085f, 0f), .084f, .20f, skin);
            Ball("手掌", forearm, new Vector3(0f, -.192f, -.005f), new Vector3(.100f, .120f, .070f), skin);
            Ball("拇指", forearm, new Vector3(side * -.048f, -.176f, -.020f), new Vector3(.039f, .068f, .047f), skin);
            for (int i = 0; i < 3; i++)
                Ball("手指细节", forearm, new Vector3(-.027f + i * .027f, -.227f, -.016f), new Vector3(.022f, .051f, .037f), skin);
            return forearm;
        }

        void BuildBackpack(Transform torso, Material bag)
        {
            Ball("鼓鼓的布书包", torso, new Vector3(0f, .15f, .195f), new Vector3(.295f, .38f, .20f), bag);
            Box("书包翻盖", torso, new Vector3(0f, .292f, .227f), new Vector3(.26f, .094f, .13f), bag);
            Ball("书包侧袋", torso, new Vector3(.15f, .096f, .212f), new Vector3(.084f, .16f, .12f), bag);
            Box("包盖纽扣", torso, new Vector3(0f, .239f, .31f), new Vector3(.032f, .045f, .015f), brass);
            Box("书包姓名签", torso, new Vector3(-.063f, .126f, .305f), new Vector3(.083f, .062f, .011f), white);
            // 两个拱起的提手段，比实心方块更像布书包。
            var handle = Joint("提手", torso, new Vector3(0f, .365f, .192f));
            Limb("提手左", handle, new Vector3(-.047f, -.021f, 0f), .015f, .085f, bag);
            Limb("提手右", handle, new Vector3(.047f, -.021f, 0f), .015f, .085f, bag);
            Box("提手顶", handle, new Vector3(0f, .019f, 0f), new Vector3(.109f, .019f, .016f), bag);
        }

        void BuildPlayerHand()
        {
            slapHand = Joint("短暂可见的出手手掌", crowdRoot, new Vector3(0f, .12f, -2.5f));
            Ball("手掌", slapHand, new Vector3(0f, 0f, 0f), new Vector3(.38f, .11f, .36f), skin);
            Limb("手腕", slapHand, new Vector3(0f, .01f, -.23f), .20f, .33f, skin).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float x = -.132f + i * .088f;
                float length = i == 0 || i == 3 ? .25f : .31f;
                var finger = Limb("手指", slapHand, new Vector3(x, -.003f, .22f), .071f, length, skin);
                finger.transform.localRotation = Quaternion.Euler(90f, 0f, (i - 1.5f) * -3f);
                Ball("指关节", slapHand, new Vector3(x, .02f, .22f), new Vector3(.058f, .018f, .04f), skinShadow);
            }
            var thumb = Limb("拇指", slapHand, new Vector3(-.213f, 0f, .003f), .085f, .22f, skin);
            thumb.transform.localRotation = Quaternion.Euler(90f, -38f, 0f);
            slapHand.gameObject.SetActive(false);
        }

        void Update()
        {
            if (world == null || !world.IsBuilt) return;
            if (currentLocation != world.EnvironmentSignature) Rebuild();
            if (Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            motionClock += dt;
            var pogPhase = pogs == null ? MatchPhase.Waiting : pogs.Phase;
            var marblePhase = marbles == null ? MatchPhase.Waiting : marbles.Phase;
            bool newPogShot = pogPhase == MatchPhase.Flying && oldPogPhase != MatchPhase.Flying;
            bool newMarbleShot = marblePhase == MatchPhase.Flying && oldMarblePhase != MatchPhase.Flying;
            activeMatch = pogPhase == MatchPhase.PlayerAim || pogPhase == MatchPhase.Flying || pogPhase == MatchPhase.NpcThinking
                || marblePhase == MatchPhase.PlayerAim || marblePhase == MatchPhase.Flying;
            int pogScore = pogs == null ? 0 : pogs.PlayerScore;
            int npcScore = pogs == null ? 0 : pogs.NpcScore;
            int marbleScore = marbles == null ? 0 : marbles.Score;
            if (newPogShot || newMarbleShot)
            {
                shotTime = 1.1f; nextFocusSearch = 0f; missTime = 0f;
                scoreAtShot = pogScore + npcScore + marbleScore;
                if (newPogShot && pogs.Shot == ShotKind.Slap) handTime = .32f;
            }
            bool settled = (oldPogPhase == MatchPhase.Flying && pogPhase != MatchPhase.Flying)
                || (oldMarblePhase == MatchPhase.Flying && marblePhase != MatchPhase.Flying);
            if (settled && pogScore + npcScore + marbleScore == scoreAtShot) missTime = .9f;
            if (pogScore > oldPogScore || npcScore > oldNpcScore || marbleScore > oldMarbleScore) Cheer();
            oldPogScore = pogScore; oldNpcScore = npcScore; oldMarbleScore = marbleScore;
            oldPogPhase = pogPhase; oldMarblePhase = marblePhase;
            shotTime = Mathf.Max(0f, shotTime - dt); cheerTime = Mathf.Max(0f, cheerTime - dt);
            missTime = Mathf.Max(0f,missTime-dt); focusTime = Mathf.Max(0f,focusTime-dt);
            UpdateFocus(pogPhase,dt);
            bool flying = pogPhase == MatchPhase.Flying || marblePhase == MatchPhase.Flying;

            foreach (var actor in actors)
            {
                float t = motionClock + actor.Phase;
                float reactionAge = motionClock - cheerStartedAt - actor.ReactionDelay;
                float cheer = Mathf.Clamp01(cheerTime * 2f) * Mathf.SmoothStep(0f,1f,Mathf.Clamp01(reactionAge * 5f));
                float attention = Mathf.Clamp01(Mathf.Max(shotTime * 2f,flying ? .32f : 0f));
                float miss = Mathf.Clamp01(missTime * 2f);
                float breath = Mathf.Sin(t * 1.9f) * .007f;
                float jump = cheer > 0f && actor.Style != 3 ? Mathf.Abs(Mathf.Sin(Mathf.Max(0,reactionAge) * (actor.Style == 1 ? 8f : 7f))) * .044f * cheer : 0f;
                actor.Root.localPosition = actor.RestPosition + Vector3.up * jump;
                actor.Hips.localPosition = new Vector3(Mathf.Sin(t * .9f) * .009f, .57f + breath, 0f);
                Vector3 gaze = focusPoint;
                if (!activeMatch && focusTime <= 0f && Mathf.Sin(t * .32f) > .82f)
                    gaze = new Vector3(-actor.RestPosition.x,1.1f,2.3f);
                Vector3 direction = gaze - actor.Head.position;
                float facing = Mathf.Atan2(-direction.x,-direction.z) * Mathf.Rad2Deg;
                float rootTurn = Mathf.Clamp(Mathf.DeltaAngle(actor.RootRotation.eulerAngles.y,facing) * (activeMatch ? .23f : .14f),-11f,11f);
                actor.Root.localRotation = Quaternion.Slerp(actor.Root.localRotation,actor.RootRotation * Quaternion.Euler(0,rootTurn,0),1f-Mathf.Exp(-2.5f*dt));
                actor.Torso.localRotation = Quaternion.Euler(actor.Profile.IdleLean - attention * (actor.Style == 2 ? 9f : 6f), Mathf.Sin(t * .6f) * 1.3f, Mathf.Sin(t) * .85f + (actor.Style % 2 == 0 ? 1f : -1f) * miss * 1.5f);
                Vector3 localLook = actor.Torso.InverseTransformDirection(direction);
                float yaw = Mathf.Clamp(Mathf.Atan2(-localLook.x,-localLook.z)*Mathf.Rad2Deg,-35f,35f);
                float pitch = Mathf.Clamp(Mathf.Atan2(localLook.y,new Vector2(localLook.x,localLook.z).magnitude)*Mathf.Rad2Deg,-24f,10f);
                var headPose = Quaternion.Euler(pitch*.77f-attention*3f+cheer*4f,yaw,Mathf.Sin(t*.72f)*1.5f+miss*(actor.Style%2==0?5f:-5f));
                actor.BaseHeadPose = headPose;
                actor.Head.localRotation = Quaternion.Slerp(actor.Head.localRotation,headPose,1f-Mathf.Exp(-5f*dt));
                float clap = Mathf.Sin(Mathf.Max(0,reactionAge) * 15f) * 9f * cheer;
                float leftLift = cheer * (actor.Style == 1 ? 17f : 35f);
                float rightLift = cheer * (actor.Style == 1 ? 72f : actor.Style == 3 ? 22f : 35f);
                actor.LeftArm.localRotation = Quaternion.Euler(attention * 12f + leftLift, 0f, -5f + cheer * 27f + clap);
                actor.RightArm.localRotation = Quaternion.Euler(attention * 11f + rightLift, actor.Style == 1 ? cheer * -12f : 0f, 5f - cheer * 27f - clap);
                actor.LeftForearm.localRotation = Quaternion.Euler(cheer * 47f + attention * 12f, 0f, cheer * 15f);
                actor.RightForearm.localRotation = Quaternion.Euler(cheer * (actor.Style == 1 ? 68f : 47f) + attention * 10f, 0f, -cheer * 15f);
                actor.LeftLeg.localRotation = Quaternion.Euler(0f, 0f, -2f + Mathf.Sin(t * .8f) * 1f);
                actor.RightLeg.localRotation = Quaternion.Euler(0f, 0f, 2f);
                actor.Mouth.localScale = new Vector3(.057f, .014f + cheer * .027f, .018f);
                if (actor.GestureDuration > 0f)
                {
                    actor.GestureTime += dt;
                    float age = actor.GestureTime;
                    float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .25f)) *
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((actor.GestureDuration - age) / .45f));
                    ApplySignature(actor, age, weight, actor.ResultGesture);
                    if (age >= actor.GestureDuration) { actor.GestureDuration = 0f; actor.ResultGesture = false; }
                }
                else
                {
                    actor.Gesture = cheer > .01f ? "score-" + actor.Profile.GestureName : activeMatch ? "observe-" + actor.Profile.GestureName : "idle";
                    if (cheer > .01f) ApplySignature(actor, Mathf.Max(0f, reactionAge), cheer * .72f, false);
                }
                float blinkPhase = Mathf.Repeat(t * .235f,1f);
                float blink = blinkPhase > .962f ? Mathf.Sin((blinkPhase-.962f)/.038f*Mathf.PI) : 0f;
                for (int e=0;e<actor.Eyes.Length;e++)
                {
                    Vector3 eyeScale = actor.EyeScales[e]; eyeScale.y *= 1f-blink*.94f;
                    actor.Eyes[e].localScale = eyeScale;
                }
            }
            if (handTime > 0f && slapHand != null)
            {
                handTime -= dt;
                float progress = Mathf.Clamp01(1f - handTime / .32f);
                slapHand.gameObject.SetActive(true);
                float height = progress < .46f ? Mathf.Lerp(.85f, .12f, Mathf.Pow(progress / .46f, .7f))
                    : Mathf.Lerp(.12f, .65f, Mathf.Pow((progress - .46f) / .54f, 1.7f));
                slapHand.localPosition = new Vector3(-.06f, height, -2.57f + progress * .11f);
                slapHand.localRotation = Quaternion.Euler(-15f + progress * 26f, -6f, 4f);
                if (handTime <= 0f) slapHand.gameObject.SetActive(false);
            }
        }

        // 身份手势只改变人物关节，任何结果都仍由正式碰撞和停稳流程产生。
        static void ApplySignature(Actor actor, float age, float weight, bool result)
        {
            if (weight <= 0f) return;
            var signature = actor.Profile.Signature;
            Vector3 left = new Vector3(20f, 0f, -8f), right = new Vector3(70f, 0f, 8f);
            Vector3 leftFore = new Vector3(35f, 0f, 0f), rightFore = new Vector3(65f, 0f, 0f);
            switch (signature)
            {
                case CompanionPersonality.Motion.Wave:
                    right = new Vector3(135f, -18f, -15f + Mathf.Sin(age * 7f) * 18f);
                    if (actor.Profile.NpcId == "doudou") left = new Vector3(105f, 15f, 15f - Mathf.Sin(age * 7f) * 12f);
                    break;
                case CompanionPersonality.Motion.Fist:
                    right = new Vector3(104f, -10f, -10f); rightFore = new Vector3(120f, 0f, -8f);
                    actor.Root.localPosition += Vector3.up * Mathf.Abs(Mathf.Sin(age * 4f)) * .035f * weight;
                    break;
                case CompanionPersonality.Motion.Arc:
                    right = new Vector3(72f, Mathf.Lerp(-24f, 24f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 1.6f))), -12f);
                    rightFore = new Vector3(28f, 0f, -15f);
                    break;
                case CompanionPersonality.Motion.Nod:
                    left = new Vector3(35f, 0f, 24f); right = new Vector3(35f, 0f, -24f);
                    leftFore = new Vector3(64f, 0f, 12f); rightFore = new Vector3(64f, 0f, -12f);
                    actor.Head.localRotation = Quaternion.Slerp(actor.Head.localRotation,
                        actor.BaseHeadPose * Quaternion.Euler(Mathf.Sin(age * 3.6f) * 9f, 0f, 0f), weight);
                    break;
                case CompanionPersonality.Motion.Salute:
                    right = new Vector3(114f, -24f, -18f); rightFore = new Vector3(92f, 0f, -28f);
                    break;
            }
            if (result && actor.Outcome == MatchOutcome.PlayerWin)
            {
                // 输赢后先认可对方的一手，各自的幅度和拍手节奏仍保留。
                float clap = Mathf.Sin(age * (actor.Style == 3 ? 7f : 10f)) * 7f;
                left = new Vector3(46f, 0f, 24f + clap); right = new Vector3(46f, 0f, -24f - clap);
                leftFore = new Vector3(66f, 0f, 10f); rightFore = new Vector3(66f, 0f, -10f);
            }
            else if (result && actor.Outcome == MatchOutcome.Draw)
            {
                left = new Vector3(52f, -16f, -12f); right = new Vector3(52f, 16f, 12f);
                leftFore = rightFore = new Vector3(45f, 0f, 0f);
            }
            actor.LeftArm.localRotation = Quaternion.Slerp(actor.LeftArm.localRotation, Quaternion.Euler(left), weight);
            actor.RightArm.localRotation = Quaternion.Slerp(actor.RightArm.localRotation, Quaternion.Euler(right), weight);
            actor.LeftForearm.localRotation = Quaternion.Slerp(actor.LeftForearm.localRotation, Quaternion.Euler(leftFore), weight);
            actor.RightForearm.localRotation = Quaternion.Slerp(actor.RightForearm.localRotation, Quaternion.Euler(rightFore), weight);
        }

        void UpdateFocus(MatchPhase pogPhase,float dt)
        {
            if (focusTime > 0f)
            {
                focusPoint = Vector3.Lerp(focusPoint,forcedFocusPoint,1f-Mathf.Exp(-7f*dt)); return;
            }
            if (Time.time >= nextFocusSearch)
            {
                nextFocusSearch = Time.time+.22f;
                focusDisc = null;
                float best = float.NegativeInfinity;
                if (pogs != null && pogPhase != MatchPhase.Waiting)
                    foreach (var disc in pogs.Targets)
                    {
                        if (disc == null || disc.Claimed || disc.Body == null) continue;
                        float relevance = disc.Body.linearVelocity.sqrMagnitude * 2f - disc.Body.position.z * .16f;
                        if (relevance > best) { best = relevance; focusDisc = disc; }
                    }
            }
            Vector3 target = focusDisc != null && !focusDisc.Claimed ? focusDisc.Body.position : new Vector3(0f,.15f,.55f);
            focusPoint = Vector3.Lerp(focusPoint,target,1f-Mathf.Exp(-6f*dt));
        }

        public void SetFocusPoint(Vector3 point,float seconds = 1f)
        {
            forcedFocusPoint = point; focusTime = Mathf.Max(.1f,seconds);
        }

        public void Cheer(float seconds = 1.6f)
        {
            cheerTime = Mathf.Max(cheerTime,seconds); cheerStartedAt = motionClock; missTime = 0f;
        }

        void Clear()
        {
            if (crowdRoot != null) { crowdRoot.gameObject.SetActive(false); Destroy(crowdRoot.gameObject); }
            actors.Clear(); slapHand = null;
            focusDisc = null; cheerTime = shotTime = handTime = missTime = focusTime = 0f;
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }
        void OnDestroy() { Clear(); }
    }
}
