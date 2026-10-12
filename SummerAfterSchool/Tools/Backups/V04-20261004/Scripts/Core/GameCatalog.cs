using System;
using System.Collections.Generic;

namespace Childhood
{
    public enum RuleKind { Flip, RingOut }
    public enum ShotKind { Slap, Smash, Skim }
    public enum CardRarity { Common, Uncommon, Rare, Legendary }
    public enum DayPeriod { Morning, Noon, Afternoon, Evening }
    public enum MatchOutcome { PlayerWin, NpcWin, Draw }

    [Serializable]
    public sealed class PhysicsPrototype
    {
        public string id;
        public string displayName;
        public string description;
        public float massGrams;
        public float radiusMeters;
        public float thicknessMeters;
        public float grip;
        public float bounce;
        public float flipEase;
        public float spinRetention;
        public float airDrag;
        public string weightFeel;
        public string gripFeel;
        public string springFeel;
        public string spinFeel;
        public float mass { get { return massGrams * .001f; } }
        public float thickness { get { return thicknessMeters; } }
        public float friction { get { return grip; } }
        public float edgeLift { get { return flipEase; } }
    }

    [Serializable]
    public sealed class CardDefinition
    {
        public string id;
        public int artIndex;
        public string displayName;
        public string series;
        public string story;
        public CardRarity rarity;
        public string prototypeId;
        public string artResourcePath;
        public string accentHex;
        public string weightFeel;
        public string gripFeel;
        public string springFeel;
        public string spinFeel;
        public string title { get { return displayName; } }
        public PhysicsPrototype Physics { get { return GameCatalog.GetPrototype(prototypeId); } }
    }

    [Serializable]
    public sealed class LocationDefinition
    {
        public string id;
        public string displayName;
        public string description;
        public string surfaceName;
        public float groundGrip;
        public float ringRadius;
        public string[] npcIds;
    }

    [Serializable]
    public sealed class NpcDefinition
    {
        public string id;
        public string displayName;
        public string locationId;
        public string introduction;
        public string challengeLine;
        public string winLine;
        public string loseLine;
        public string accentHex;
        public float skill;
        public ShotKind preferredShot;
        public RuleKind preferredRule;
        public int[] startingCardIndexes;
    }

    /// <summary>原创内容与物理参数的单一入口。天然语言手感可直接用于卡册。</summary>
    public static class GameCatalog
    {
        public const string PlayerOwnerId = "player";

        public static readonly PhysicsPrototype[] Prototypes =
        {
            Prototype("paper", "薄纸片", "轻薄的老式纸片，借风借旋，轻拍也能漂亮翻身。", 2.6f, .031f, .0014f, .56f, .20f, 1.16f, .78f, .42f,
                "轻盈，指尖一碰就走", "纸边咬地，斜擦很听话", "软软弹起，翻面很灵", "旋转轻快，落地收得住"),
            Prototype("cardboard", "厚纸片", "层层压实的纸板，手感均衡，是街角高手的可靠老伙计。", 4.8f, .031f, .0027f, .64f, .24f, 1.0f, .72f, .32f,
                "分量刚好，掌心踏实", "纹理抓地，落点稳定", "弹起温和，容错大", "旋转稳，方向容易拿捏"),
            Prototype("foil", "闪膜片", "覆着闪亮薄膜，转起来像一束小太阳；打出圈时尤其滑溜。", 4.1f, .031f, .0021f, .34f, .36f, 1.05f, .94f, .26f,
                "轻巧利落", "表面滑，擦出去很远", "弹得脆，容易蹦起来", "转得久，弧线很好看"),
            Prototype("rubber", "软胶片", "柔韧的胶片会吃住力道，再把那股劲儿弹回来。", 6.2f, .031f, .0031f, .88f, .60f, .89f, .54f, .27f,
                "沉稳，按下去有质感", "很抓地，站位守得住", "弹性足，重拍会跳高", "旋转收得快，近身很强"),
            Prototype("metal", "铁皮片", "薄铁压成的收藏片，声音清脆，靠沉重的撞击打开阵地。", 10.4f, .031f, .0019f, .40f, .30f, .74f, .88f, .16f,
                "压手，撞起来有劲", "硬边滑地，挤位很强", "低弹快落，压场稳", "惯性足，转起来不易停")
        };

        public static readonly CardDefinition[] Cards = BuildCards();

        public static readonly LocationDefinition[] Locations =
        {
            new LocationDefinition { id = "home", displayName = "外婆家的小院", description = "蝉声钻过葡萄架，旧砖有些翘边，借着坡面试试翻片。这里可以安心练手。", surfaceName = "起伏旧砖", groundGrip = .62f, ringRadius = .48f, npcIds = new string[0] },
            new LocationDefinition { id = "school_gate", displayName = "小学门口", description = "放学铃一响，路沿围住擂台，井盖和砖缝让落片多了几分变化。", surfaceName = "井盖 · 路沿", groundGrip = .66f, ringRadius = .55f, npcIds = new[] { "xiaoyu" } },
            new LocationDefinition { id = "alley", displayName = "汽水胡同", description = "红砖墙晒得发暖，高低错落的铺砖接着两级马路牙子，看准台阶再斜擦。", surfaceName = "高低砖 · 两级台阶", groundGrip = .54f, ringRadius = .53f, npcIds = new[] { "dazhi", "taotao" } },
            new LocationDefinition { id = "old_tree", displayName = "老槐树下", description = "树荫落在菱形花纹砖上，树根顶起的缓坡，让掀边和借坡都有门道。", surfaceName = "花纹砖 · 树根缓坡", groundGrip = .48f, ringRadius = .58f, npcIds = new[] { "linlin" } }
        };

        public static readonly NpcDefinition[] Npcs =
        {
            new NpcDefinition { id = "xiaoyu", displayName = "小雨", locationId = "school_gate", introduction = "同桌兼圆片搭子，嘴上让你三分，出手可一点也不含糊。", challengeLine = "就一张，咱们放学后的老规矩！", winLine = "这回我先保管啦，明天再来赢回去！", loseLine = "哇，这一拍真漂亮。归你了！", accentHex = "#7DCBC5", skill = .34f, preferredShot = ShotKind.Slap, preferredRule = RuleKind.Flip, startingCardIndexes = new[] { 5, 6, 7, 8, 9, 11, 12, 16 } },
            new NpcDefinition { id = "dazhi", displayName = "大志", locationId = "alley", introduction = "胡同里的重拍高手，手掌一落，连汽水瓶都要震一下。", challengeLine = "手要稳，胆要大！来吧！", winLine = "嘿嘿，这张我可惦记好久了。", loseLine = "好手劲！这张给你，我再练练。", accentHex = "#ED9564", skill = .57f, preferredShot = ShotKind.Smash, preferredRule = RuleKind.RingOut, startingCardIndexes = new[] { 3, 10, 13, 14, 15, 18, 20, 24, 26 } },
            new NpcDefinition { id = "taotao", displayName = "桃桃", locationId = "alley", introduction = "总能找到最刁钻的擦边角度，听说她闭着眼也认得每张片的手感。", challengeLine = "别只盯着力气，看好这条弧线。", winLine = "这张先来我的小盒子做客。", loseLine = "你连这条边都算到了？服气！", accentHex = "#E98DAB", skill = .68f, preferredShot = ShotKind.Skim, preferredRule = RuleKind.RingOut, startingCardIndexes = new[] { 2, 7, 12, 16, 17, 19, 21, 22, 27 } },
            new NpcDefinition { id = "linlin", displayName = "林林", locationId = "old_tree", introduction = "老槐树下的传说，珍藏着整个街区最漂亮的一套圆片。她只夸真正漂亮的出手。", challengeLine = "输赢是一张片，漂亮的出手是一整个夏天。", winLine = "别灰心，它还在这里，等你来接它回家。", loseLine = "这张配得上你。好好照顾它。", accentHex = "#A598D5", skill = .86f, preferredShot = ShotKind.Slap, preferredRule = RuleKind.Flip, startingCardIndexes = new[] { 4, 8, 18, 19, 23, 24, 25, 28, 29 } }
        };

        public static CardDefinition GetCard(string id)
        {
            foreach (var card in Cards) if (card.id == id) return card;
            throw new ArgumentException("未知圆片定义：" + id, "id");
        }

        public static PhysicsPrototype GetPrototype(string id)
        {
            foreach (var prototype in Prototypes) if (prototype.id == id) return prototype;
            throw new ArgumentException("未知物理原型：" + id, "id");
        }

        public static NpcDefinition GetNpc(string id)
        {
            foreach (var npc in Npcs) if (npc.id == id) return npc;
            throw new ArgumentException("未知街区伙伴：" + id, "id");
        }

        public static LocationDefinition GetLocation(string id)
        {
            foreach (var location in Locations) if (location.id == id) return location;
            throw new ArgumentException("未知地点：" + id, "id");
        }

        public static string RuleName(RuleKind rule) { return rule == RuleKind.Flip ? "翻面" : "打出圈"; }
        public static string ShotName(ShotKind shot) { return shot == ShotKind.Slap ? "掌拍" : shot == ShotKind.Smash ? "重拍" : "斜擦"; }
        public static string PeriodName(DayPeriod period) { return period == DayPeriod.Morning ? "清晨" : period == DayPeriod.Noon ? "午间" : period == DayPeriod.Afternoon ? "放学后" : "黄昏"; }
        public static string RarityName(CardRarity rarity) { return rarity == CardRarity.Common ? "常见" : rarity == CardRarity.Uncommon ? "特别" : rarity == CardRarity.Rare ? "稀有" : "传说"; }

        private static PhysicsPrototype Prototype(string id, string name, string description, float mass, float radius, float thickness, float grip, float bounce, float flip, float spin, float drag, string weightFeel, string gripFeel, string springFeel, string spinFeel)
        {
            return new PhysicsPrototype { id = id, displayName = name, description = description, massGrams = mass, radiusMeters = radius, thicknessMeters = thickness, grip = grip, bounce = bounce, flipEase = flip, spinRetention = spin, airDrag = drag, weightFeel = weightFeel, gripFeel = gripFeel, springFeel = springFeel, spinFeel = spinFeel };
        }

        private static CardDefinition[] BuildCards()
        {
            var names = new[] { "汽水小火箭", "月牙小队长", "西瓜冲浪手", "纸飞机一号", "橘猫午睡", "云端邮差", "星星捕手", "糖果风车", "小熊机车", "雨靴探险家", "蒸汽小火车", "蓝鲸电台", "萤火夏令营", "红砖守卫", "爆米花骑士", "雪糕宇航员", "彩虹水母", "霓虹溜溜球", "豆豆大力王", "弹簧青蛙", "潜水鸭船长", "葡萄泡泡龙", "胶鞋跳跳侠", "山丘巨人", "铜铃巡游者", "铁皮月亮", "齿轮小狮子", "太阳徽章", "银河独角兽", "夏天的金蝉" };
            var stories = new[]
            {
                "瓶盖一拧，橘子汽水的泡泡就把小火箭送上天。", "月牙队长负责照亮每一个忘带手电的夜晚。", "踩着西瓜皮冲浪，终点是冰镇的夏天。", "把想说的话折进纸飞机，风会替你送到。", "橘猫在作业本上睡着了，尾巴盖住了最后一道题。", "云端邮差背着一袋晚霞，挨家挨户送好梦。", "星星捕手的网里，永远给最小的星星留个位置。", "风吹过糖果风车，整个街区都变甜了。", "小熊机车没有油箱，只要朋友的一声加油。", "一双黄雨靴，也能走出世界上最远的路。", "小火车穿过山洞，烟囱喷出棉花糖。", "蓝鲸电台在海底播放放学路上的那首歌。", "十只萤火虫组成营地的小灯塔，夜晚就不怕黑了。", "红砖守卫守着胡同口，也守着你的秘密基地。", "骑士的盾牌是一颗爆米花，勇气倒有满满一桶。", "融化之前，雪糕宇航员要先看看地球有多蓝。", "水母披着雨后彩虹，轻轻游过玻璃弹珠里的海。", "溜溜球上的光绕了三圈，照亮一整个舞台。", "豆豆个子不高，却能把大家的书包一起扛回家。", "弹簧青蛙一跳，连荷叶上的露水也飞起来。", "鸭船长把浴盆开成大海，每一朵泡泡都是岛屿。", "泡泡龙吹出的不是火，是一串葡萄味的小宇宙。", "胶鞋跳跳侠可以跳过水坑，但不会错过朋友。", "山丘巨人弯下腰，听蚂蚁讲今天的冒险。", "铜铃叮当响，巡游队伍绕着老槐树走了三圈。", "铁皮月亮挂在秘密基地门口，白天也能看见月光。", "小狮子的齿轮心脏，装着一个不会停的好奇心。", "戴上太阳徽章的人，要负责让身边的人笑起来。", "独角兽踏过银河，把碎星撒在回家的路上。", "金蝉蜕下旧壳，把最响亮的歌留给这个夏天。"
            };
            var prototypes = new[] { "paper", "paper", "paper", "paper", "cardboard", "paper", "cardboard", "foil", "cardboard", "rubber", "metal", "foil", "foil", "cardboard", "metal", "paper", "foil", "foil", "rubber", "rubber", "rubber", "foil", "rubber", "cardboard", "metal", "metal", "metal", "metal", "foil", "metal" };
            var colors = new[] { "#EF9F50", "#8F9FD8", "#E97876", "#75B4D8", "#DCAC77", "#8AC5DF", "#D6B36A", "#E5A1CB", "#A2BCB0", "#E5BD58", "#CB7358", "#669DBD", "#B8D470", "#C88167", "#DCAE6E", "#A7CEE4", "#A4B8E4", "#DB84CA", "#89BBA9", "#82B379", "#EBC379", "#B09BDD", "#8AAE97", "#C59D6C", "#CEAD68", "#9BAABD", "#B09D71", "#E4BB64", "#C5A9E3", "#D7BC65" };
            var cards = new CardDefinition[30];
            for (int i = 0; i < cards.Length; i++)
            {
                var physics = GetPrototype(prototypes[i]);
                cards[i] = new CardDefinition
                {
                    id = "pog_" + i.ToString("00"), artIndex = i, displayName = names[i], series = i < 10 ? "放学路上的梦" : i < 20 ? "秘密基地的冒险" : "老槐树的宝藏", story = stories[i],
                    rarity = i >= 28 ? CardRarity.Legendary : i >= 23 ? CardRarity.Rare : i >= 11 ? CardRarity.Uncommon : CardRarity.Common,
                    prototypeId = prototypes[i], artResourcePath = "Art/Pogs/pog_" + i.ToString("00"), accentHex = colors[i],
                    weightFeel = physics.weightFeel, gripFeel = physics.gripFeel, springFeel = physics.springFeel, spinFeel = physics.spinFeel
                };
            }
            return cards;
        }
    }
}
