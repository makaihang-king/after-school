using System;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    /// <summary>原创微缩儿童与围观动作；没有 collider，也不参与得分或物理。</summary>
    public sealed class CrowdActors : MonoBehaviour
    {
        sealed class Actor
        {
            public Transform Root, Hips, Torso, Head, LeftArm, RightArm, LeftForearm, RightForearm, LeftLeg, RightLeg;
            public Transform Mouth;
            public Quaternion RootRotation;
            public Vector3 RestPosition;
            public float Phase, Height;
            public bool Girl;
        }

        readonly List<Actor> actors = new List<Actor>();
        readonly List<Material> materials = new List<Material>();
        SceneWorld world;
        PogMatch pogs;
        MarbleMatch marbles;
        Transform crowdRoot, slapHand;
        Material skin, skinShadow, hair, dark, white, sole, cheek, brass;
        string currentLocation;
        MatchPhase oldPogPhase, oldMarblePhase;
        int oldPogScore, oldNpcScore, oldMarbleScore;
        float cheerTime, shotTime, handTime;
        bool activeMatch;

        public void Configure(SceneWorld scene, PogMatch pogMatch, MarbleMatch marbleMatch)
        {
            world = scene; pogs = pogMatch; marbles = marbleMatch;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            if (world == null) return;
            currentLocation = world.LocationId;
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
            Color[] clothes = { new Color(.33f,.68f,.61f), new Color(.91f,.62f,.26f), new Color(.91f,.46f,.60f), new Color(.55f,.47f,.73f) };
            Color[] bags = { new Color(.27f,.43f,.58f), new Color(.58f,.38f,.23f), new Color(.44f,.55f,.37f), new Color(.37f,.47f,.53f) };
            string[] names = { "小雨", "大志", "桃桃", "林林" };
            Vector3[] positions =
            {
                new Vector3(-3.48f,0f,1.12f), new Vector3(3.52f,0f,1.75f),
                new Vector3(3.67f,0f,3.05f), new Vector3(-3.72f,0f,2.92f)
            };
            for (int i = 0; i < 4; i++)
            {
                var actor = BuildActor(names[i], positions[i], i < 2 ? 1.0f : .96f, i >= 2, i == 1,
                    Mat(names[i] + "的衣服", clothes[i]), Mat(names[i] + "的书包", bags[i]), i);
                actors.Add(actor);
            }
            BuildPlayerHand();
            oldPogPhase = pogs == null ? MatchPhase.Waiting : pogs.Phase;
            oldMarblePhase = marbles == null ? MatchPhase.Waiting : marbles.Phase;
            oldPogScore = pogs == null ? 0 : pogs.PlayerScore;
            oldNpcScore = pogs == null ? 0 : pogs.NpcScore;
            oldMarbleScore = marbles == null ? 0 : marbles.Score;
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
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            obj.GetComponent<MeshRenderer>().sharedMaterial = material; return obj;
        }

        GameObject Ball(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        { return Shape(PrimitiveType.Sphere, name, parent, position, scale, material); }
        GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        { return Shape(PrimitiveType.Cube, name, parent, position, scale, material); }
        GameObject Limb(string name, Transform parent, Vector3 position, float width, float length, Material material)
        { return Shape(PrimitiveType.Capsule, name, parent, position, new Vector3(width, length * .5f, width), material); }

        Actor BuildActor(string name, Vector3 position, float size, bool girl, bool cap, Material shirt, Material bag, int style)
        {
            var actor = new Actor { Root = Joint(name + " · 微缩儿童", crowdRoot, position), RestPosition = position,
                Height = size, Girl = girl, Phase = style * 1.84f };
            float yaw = position.x < 0f ? -28f : 28f;
            actor.RootRotation = Quaternion.Euler(0f, yaw, 0f);
            actor.Root.localRotation = actor.RootRotation; actor.Root.localScale = Vector3.one * size;
            actor.Hips = Joint("重心", actor.Root, new Vector3(0f, .57f, 0f));
            var shorts = Mat(name + "的短裤", new Color(.24f + style * .025f, .34f, .43f - style * .03f));
            var shoes = Mat(name + "的运动鞋", style % 2 == 0 ? new Color(.37f,.53f,.60f) : new Color(.65f,.28f,.19f));
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
                Ball("眼白", actor.Head, new Vector3(side * .076f, .117f, -.151f), new Vector3(.061f, .077f, .022f), white);
                Ball("黑眼睛", actor.Head, new Vector3(side * .071f, .116f, -.167f), new Vector3(.031f, .045f, .013f), dark);
                Ball("瞳孔里的光", actor.Head, new Vector3(side * .071f - .005f, .13f, -.175f), Vector3.one * .008f, white);
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
            if (currentLocation != world.LocationId) Rebuild();
            if (Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            var pogPhase = pogs == null ? MatchPhase.Waiting : pogs.Phase;
            var marblePhase = marbles == null ? MatchPhase.Waiting : marbles.Phase;
            bool newPogShot = pogPhase == MatchPhase.Flying && oldPogPhase != MatchPhase.Flying;
            bool newMarbleShot = marblePhase == MatchPhase.Flying && oldMarblePhase != MatchPhase.Flying;
            activeMatch = pogPhase == MatchPhase.PlayerAim || pogPhase == MatchPhase.Flying || pogPhase == MatchPhase.NpcThinking
                || marblePhase == MatchPhase.PlayerAim || marblePhase == MatchPhase.Flying;
            if (newPogShot || newMarbleShot) { shotTime = 1.1f; if (newPogShot) handTime = .32f; }
            int pogScore = pogs == null ? 0 : pogs.PlayerScore;
            int npcScore = pogs == null ? 0 : pogs.NpcScore;
            int marbleScore = marbles == null ? 0 : marbles.Score;
            if (pogScore > oldPogScore || npcScore > oldNpcScore || marbleScore > oldMarbleScore) cheerTime = 1.6f;
            oldPogScore = pogScore; oldNpcScore = npcScore; oldMarbleScore = marbleScore;
            oldPogPhase = pogPhase; oldMarblePhase = marblePhase;
            shotTime = Mathf.Max(0f, shotTime - dt); cheerTime = Mathf.Max(0f, cheerTime - dt);

            foreach (var actor in actors)
            {
                float t = Time.time + actor.Phase;
                float cheer = Mathf.Clamp01(cheerTime * 2f);
                float attention = Mathf.Clamp01(shotTime * 2f);
                float breath = Mathf.Sin(t * 1.9f) * .007f;
                float jump = cheer > 0f ? Mathf.Abs(Mathf.Sin((1.6f - cheerTime) * 10f + actor.Phase * .16f)) * .075f * cheer : 0f;
                actor.Root.localPosition = actor.RestPosition + Vector3.up * jump;
                actor.Hips.localPosition = new Vector3(Mathf.Sin(t * .9f) * .009f, .57f + breath, 0f);
                actor.Torso.localRotation = Quaternion.Euler(-attention * 9f, Mathf.Sin(t * .6f) * 2f, Mathf.Sin(t) * 1.2f);
                actor.Head.localRotation = Quaternion.Euler(-attention * 8f + cheer * 5f, Mathf.Sin(t * .77f) * 3f, Mathf.Sin(t * 1.3f) * 1.9f);
                float clap = Mathf.Sin(t * 17f) * 8f * cheer;
                actor.LeftArm.localRotation = Quaternion.Euler(attention * 18f + cheer * 32f, 0f, -5f + cheer * 30f + clap);
                actor.RightArm.localRotation = Quaternion.Euler(attention * 14f + cheer * 32f, 0f, 5f - cheer * 30f - clap);
                actor.LeftForearm.localRotation = Quaternion.Euler(cheer * 49f + attention * 15f, 0f, cheer * 15f);
                actor.RightForearm.localRotation = Quaternion.Euler(cheer * 49f + attention * 11f, 0f, -cheer * 15f);
                actor.LeftLeg.localRotation = Quaternion.Euler(0f, 0f, -2f + Mathf.Sin(t * .8f) * 1f);
                actor.RightLeg.localRotation = Quaternion.Euler(0f, 0f, 2f);
                actor.Mouth.localScale = new Vector3(.057f, .014f + cheer * .027f, .018f);
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

        public void Cheer(float seconds = 1.6f) { cheerTime = Mathf.Max(cheerTime, seconds); }

        void Clear()
        {
            if (crowdRoot != null) { crowdRoot.gameObject.SetActive(false); Destroy(crowdRoot.gameObject); }
            actors.Clear(); slapHand = null;
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }
        void OnDestroy() { Clear(); }
    }
}
