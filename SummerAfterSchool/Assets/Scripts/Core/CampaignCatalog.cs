using System;
using System.Collections.Generic;

namespace Childhood
{
    public enum WeatherKind { Sunny, Cloudy, Rainy }
    public enum MatchRoute { FreePlay, Story, Tournament }
    public enum TradeKind { Buy, Sell, Swap }
    public enum CardTransferReason { Origin, Wager, Trade, Purchase, Reward }

    [Serializable]
    public sealed class EnvironmentContext
    {
        public string locationId = "home";
        public int visitVariant;
        public int day = 1;
        public DayPeriod period = DayPeriod.Afternoon;
        public WeatherKind weather = WeatherKind.Sunny;
        public int weatherSeed;
        public int geometryVersion = 2;
        public float frictionMultiplier = 1f;
    }

    [Serializable]
    public sealed class ChallengeDefinition
    {
        public string id;
        public string title;
        public string description;
        public string npcId;
        public string locationId;
        public RuleKind rule;
        public int layout;
        public int rewardMarbles;
        // Boss 招牌是本次对手实际押的实例，绝不另外复制一张奖励片。
        public int rewardCardIndex = -1;
        public int bossLevel;
        public float skill;
        public string[] requiredStageIds;
        public string introLine;
        public string hintLine;
        public string winLine;
        public string loseLine;
    }

    [Serializable]
    public sealed class ShopOfferDefinition
    {
        public string id;
        public string cardDefinitionId;
        public int buyPrice;
        public bool isFoundation;
    }

    [Serializable]
    public sealed class SwapOfferDefinition
    {
        public string id;
        public string npcId;
        public string wantedDefinitionId;
        public string offeredDefinitionId;
        public string line;
    }

    [Serializable]
    public sealed class ErrandDefinition
    {
        public string id;
        public string title;
        public string description;
        public string destinationLocationId;
        public int rewardMarbles = 6;
        public string acceptLine;
        public string completionLine;
    }

    /// <summary>独立故事与杯赛的静态内容。存档只记录稳定 ID、签表及完成收据。</summary>
    public static class CampaignCatalog
    {
        public const int ChampionReward = 24;
        public const string ShopOwnerId = "shopkeeper";
        public static readonly ChallengeDefinition[] Stages =
        {
            Stage("story_01", "葡萄架下的约定", "home", "xiaoyu", RuleKind.Flip, 0, 0, .34f, -1,
                "小雨把铁盒放在葡萄架下：‘外婆说，出门前先把手练稳。各押一张，输了也笑着握手。’",
                "先试中等力道，看清纸边翘起的方向。旧砖能借力，别只顾往下砸。",
                "‘你赢啦！这张陪你去广场。豆豆已经摆好新阵，去给他看看刚才那手！’",
                "‘这张我替你收着。没真片了就去接跑腿，六颗玻璃珠够买一张新的。’"),
            Stage("story_02", "一场误会", "square", "doudou", RuleKind.RingOut, 1, 0, .44f, -1,
                "豆豆挡在广场圈边：‘有人说你把我的片顺走了！’你打开铁盒：‘这张是小雨刚输给我的，背面还有她画的小雨点。’豆豆红着脸：‘是我听错了。咱们按规矩各押一张，比完一起把话讲清楚。’",
                "从片的内侧斜擦向圈外，经过石缝留点余量。整张越线才算。",
                "‘片归你，刚才那句我说错了，对不起。’豆豆拉着你给围观伙伴看背面的记号，误会终于散了。‘去校门口找阿岳吧，我替你加油！’",
                "‘输赢归输赢，误会是我弄错的，对不起。’豆豆把你的片放进盒子：‘下次来赢回去，我还会帮你把事情解释清楚。’"),
            Stage("story_03", "校门守擂·月牙队长", "school_gate", "ayue", RuleKind.Flip, 1, 1, .55f, 30,
                "阿岳从口袋拿出月牙招牌：‘这张陪我守了一夏天的校门。赢过我，就带它继续走；招牌只有这一张。’",
                "井盖比砖面滑，路沿会改变回弹。先找平稳起手点，别每次都用最大力道。",
                "‘说到做到，月牙归你。小雨在教学楼走廊等着你的好消息，去吧！’",
                "‘招牌还由我守，你那张也在这里。准备好再来，我不会躲。’"),
            Stage("story_04", "走廊里的回声", "corridor", "xiaoyu", RuleKind.Flip, 2, 0, .60f, -1,
                "窗影一格格落在走廊地上。小雨压低声音：‘别把片打进门缝！阿岳夸你厉害，我也要认真跟你比一场。’",
                "两组片分在左右，先翻稳一边，再找另一边的落点。门檐下不受雨水影响。",
                "‘这下我服了！胡同里的桃桃正在守擂，你已经准备好去挑战她了。’",
                "‘脚步声都把我吓一跳。我们等片停稳再看，下一回一定能找准。’"),
            Stage("story_05", "胡同守擂·红砖大将", "alley", "taotao", RuleKind.RingOut, 2, 1, .70f, 31,
                "桃桃把红砖大将压在掌下：‘胡同口的台阶我闭眼也认得。各押一张，别只盯着力气，看好这条弧线！’",
                "砖有高低，台阶会挡直线。看清目标脚下的高度，留心侧向绕出去的路径。",
                "‘你连台阶都用上了，红砖大将交给你。去小卖部找大志，他正等着比一场。’",
                "‘我靠的也不是光有力气。输片我先收着，准备好真片回来，再比个痛快。’"),
            Stage("story_06", "汽水箱边的较量", "shop", "dazhi", RuleKind.RingOut, 3, 0, .74f, -1,
                "大志把汽水箱搬到门边：‘我把台阶上的手劲也带来了！小卖部檐下地面干燥，各押一张，比谁先打出圈。’",
                "圈边目标适合从内向外推。门前地面平，落点比蛮力更可靠。",
                "‘好手劲！这张给你。林林在老槐树下，她可是最后一位地区高手！’",
                "‘我也不是一下就学会的。跑腿不押片，送到地方就有六颗珠，买片回来继续练。’"),
            Stage("story_07", "槐荫守擂·金蝉使者", "old_tree", "linlin", RuleKind.Flip, 3, 1, .84f, 32,
                "林林抹去金蝉片上的一粒尘：‘树根顶起的砖，反倒多了一条路。让片翻得漂亮，这张金蝉就陪你去操场。’",
                "花纹砖旁有缓坡，借坡掀边比硬撞更稳。圈边阵要看准每张片自己的落点。",
                "‘这手我会记得。金蝉归你，操场上阿砚的终场，现在轮到你了。’",
                "‘别急着给自己下结论。片还在这棵树下，等你准备好接它回家。’"),
            Stage("story_08", "操场终场·夏日大王", "playground", "ayan", RuleKind.Flip, 3, 2, .92f, 33,
                "阿砚把夏日大王摆到跑道旁：‘一路走过来的人，才懂每块砖的脾气。最后一场，还是老规矩：各押一张，停稳再算。’",
                "看好跑道边的台阶和排水槽，先翻容易找准落点的目标。最后一手也要把瞄准做完。",
                "‘你就是今年的街区高手！夏日大王归你，故事走完了，八人杯赛的大门还一直开着。’大家把铁盒敲得叮当响。",
                "‘输一场不会抹掉你走过的路。招牌我守着，你那张也守着，下一次照样等你。’")
        };

        public static readonly ChallengeDefinition[] TournamentStages =
        {
            Tournament("tournament_01", "杯赛·八强", "school_gate", "ayue", RuleKind.Flip, 0, .55f,
                "阿岳亮出签表：‘八个人已经到齐，各押一张。这回只看这一场，故事走到哪儿都能报名！’",
                "‘校门这一关你过了！桃桃已经从另一组晋级，下一轮见。’"),
            Tournament("tournament_02", "杯赛·半决赛", "alley", "taotao", RuleKind.RingOut, 2, .72f,
                "桃桃靠在胡同墙边：‘阿岳都给你赢了？这一轮我得认真找角度。押片先说好，出手再分高低！’",
                "‘这条弧线也被你看透了，去操场吧，阿砚已经等着决赛。’"),
            Tournament("tournament_03", "杯赛·决赛", "playground", "ayan", RuleKind.Flip, 3, .92f,
                "阿砚把决赛签表压在铁盒下：‘七位伙伴都来作证。各一张真片，冠军还有二十四颗珠！’",
                "‘冠军就是你！二十四颗珠数好了，赢来的片也交给你。这个暑假，咱们还会再见。’")
        };

        public static readonly ShopOfferDefinition[] ShopOffers = BuildShopOffers();
        public static readonly SwapOfferDefinition[] SwapOffers = BuildSwapOffers();
        public static readonly ErrandDefinition[] Errands =
        {
            new ErrandDefinition { id = "errand_basket", title = "替外婆送竹篮", description = "把外婆的小竹篮送到老槐树下，交给乘凉的街坊。", destinationLocationId = "old_tree", acceptLine = "‘篮子不重，路上慢些。送到就领六颗玻璃珠。’外婆替你理好提手。", completionLine = "街坊接过竹篮：‘替我谢谢外婆！这六颗珠给你，路上喝口水。’" },
            new ErrandDefinition { id = "errand_chalk", title = "给操场送粉笔", description = "把值日室的粉笔盒送到校园操场，供大家重新画线。", destinationLocationId = "playground", acceptLine = "‘这是画线用的，盖子扣好就行。送到操场，有六颗珠的跑腿钱。’", completionLine = "值日生收好粉笔：‘来得正好！六颗珠，数清楚，辛苦啦。’" },
            new ErrandDefinition { id = "errand_bottles", title = "送回汽水空瓶", description = "把洗干净的空汽水瓶送回小卖部门口。", destinationLocationId = "shop", acceptLine = "阿岳指指瓶筐：‘一趟六颗珠，不用押片。帮我送回门口就成。’", completionLine = "阿岳接过空瓶：‘一个也没磕坏，六颗珠给你。想买片就看看价签。’" },
            new ErrandDefinition { id = "errand_book", title = "把借书还到走廊", description = "把包好封面的故事书送到教学楼走廊的借书架。", destinationLocationId = "corridor", acceptLine = "‘书角别折着，送到借书架就有六颗珠。路上还能看最后一页。’", completionLine = "你把书摆回架子，值日伙伴递来六颗珠：‘下一位正等着读呢，谢谢你！’" }
        };

        public static ChallengeDefinition GetTournamentStage(int roundIndex) { if (roundIndex < 0 || roundIndex >= TournamentStages.Length) throw new ArgumentOutOfRangeException("roundIndex"); return TournamentStages[roundIndex]; }
        public static bool IsOfferUnlocked(GameSave save, ShopOfferDefinition offer)
        {
            if (save == null || offer == null) return false;
            foreach (var stage in Stages) if (stage.rewardCardIndex >= 0 && GameCatalog.Cards[stage.rewardCardIndex].id == offer.cardDefinitionId) return IsBossDefeated(save, stage.npcId);
            return true;
        }
        public static bool IsBossDefeated(GameSave save, string npcId)
        {
            if (save == null || save.story == null) return false;
            if (save.story.defeatedBossIds != null && save.story.defeatedBossIds.Contains(npcId)) return true;
            foreach (var stage in Stages) if (stage.bossLevel > 0 && stage.npcId == npcId && save.story.clearedStageIds != null && save.story.clearedStageIds.Contains(stage.id)) return true;
            return false;
        }
        public static ChallengeDefinition GetStage(string id) { foreach (var stage in Stages) if (stage.id == id) return stage; throw new ArgumentException("未知故事关卡：" + id); }
        public static ShopOfferDefinition GetShopOffer(string id) { foreach (var offer in ShopOffers) if (offer.id == id) return offer; throw new ArgumentException("未知小卖部清单：" + id); }
        public static SwapOfferDefinition GetSwapOffer(string id) { foreach (var offer in SwapOffers) if (offer.id == id) return offer; throw new ArgumentException("未知伙伴换片清单：" + id); }
        public static ErrandDefinition GetErrand(string id) { foreach (var errand in Errands) if (errand.id == id) return errand; throw new ArgumentException("未知跑腿任务：" + id); }
        public static int BuyPrice(CardDefinition card) { return card.rarity == CardRarity.Common ? 6 : card.rarity == CardRarity.Uncommon ? 12 : card.rarity == CardRarity.Rare ? 24 : 48; }
        public static int SellPrice(CardDefinition card) { return card.rarity == CardRarity.Common ? 3 : card.rarity == CardRarity.Uncommon ? 6 : card.rarity == CardRarity.Rare ? 12 : 24; }
        public static string WeatherName(WeatherKind weather) { return weather == WeatherKind.Rainy ? "小雨" : weather == WeatherKind.Cloudy ? "多云" : "晴天"; }

        private static ChallengeDefinition Tournament(string id, string title, string location, string npc, RuleKind rule, int layout, float skill, string intro, string win)
        {
            return new ChallengeDefinition { id = id, title = title, locationId = location, npcId = npc, rule = rule, layout = layout, skill = skill, rewardMarbles = id == "tournament_03" ? ChampionReward : 0, rewardCardIndex = -1, requiredStageIds = new string[0], introLine = intro, hintLine = rule == RuleKind.Flip ? "等片停稳再判断正反，找准落点再拍。" : "从圈内侧向外擦，整张越线才计分。", description = "八人签表固定晋级路线，三轮胜出即夺冠。", winLine = win, loseLine = "‘这回先到这里，赢来的片按老规矩交接。下一届从八强再来，签表会重新记录。’" };
        }

        private static ChallengeDefinition Stage(string id, string title, string location, string npc, RuleKind rule, int layout, int boss, float skill, int signature, string intro, string hint, string win, string lose)
        {
            var number = int.Parse(id.Substring(id.Length - 2));
            return new ChallengeDefinition { id = id, title = title, description = hint, npcId = npc, locationId = location, rule = rule, layout = layout, bossLevel = boss, skill = skill, rewardMarbles = boss == 2 ? 16 : boss == 1 ? 8 : 4, rewardCardIndex = signature, requiredStageIds = number == 1 ? new string[0] : new[] { "story_" + (number - 1).ToString("00") }, introLine = intro, hintLine = hint, winLine = win, loseLine = lose };
        }

        private static ShopOfferDefinition[] BuildShopOffers()
        {
            var result = new List<ShopOfferDefinition>();
            foreach (var card in GameCatalog.Cards)
                result.Add(new ShopOfferDefinition { id = "buy_" + card.id, cardDefinitionId = card.id, buyPrice = BuyPrice(card) });
            result.Add(new ShopOfferDefinition { id = "buy_practice", cardDefinitionId = "pog_00", buyPrice = 2, isFoundation = true });
            return result.ToArray();
        }

        private static SwapOfferDefinition[] BuildSwapOffers()
        {
            // 固定同稀有度清单；成交必须双方仍持有指定实例，每份清单每天只换一次。
            return new[]
            {
                Swap("xiaoyu", 1, 6), Swap("xiaoyu", 34, 8), Swap("dazhi", 2, 3), Swap("dazhi", 12, 13),
                Swap("taotao", 5, 7), Swap("taotao", 15, 16), Swap("linlin", 23, 24), Swap("linlin", 28, 29),
                Swap("doudou", 4, 35), Swap("doudou", 9, 36), Swap("ayue", 6, 34), Swap("ayue", 11, 37),
                Swap("ayan", 25, 39), Swap("ayan", 29, 41)
            };
        }

        private static SwapOfferDefinition Swap(string npc, int wanted, int offered)
        {
            var a = GameCatalog.Cards[wanted]; var b = GameCatalog.Cards[offered];
            if (a.rarity != b.rarity) throw new InvalidOperationException("换片清单必须同稀有度。");
            return new SwapOfferDefinition { id = "swap_" + npc + "_" + offered, npcId = npc, wantedDefinitionId = a.id, offeredDefinitionId = b.id, line = "‘你的" + a.displayName + "，换我的" + b.displayName + "，同档一换一，今天就这一回。’" };
        }
    }
}
