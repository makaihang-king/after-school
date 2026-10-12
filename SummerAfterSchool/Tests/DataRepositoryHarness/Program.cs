using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Childhood;

internal static class Program
{
    private static int checks;

    private static void Main()
    {
        string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ChildhoodRepositoryTests"));
        string work = Path.GetFullPath(Path.Combine(testRoot, Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(work);
        try
        {
            Check(GameCatalog.Cards.Length == 42 && GameCatalog.Prototypes.Length == 5, "42 张原创圆片与 5 种物理原型");
            Check(GameCatalog.Cards.Select(c => c.id).Distinct().Count() == 42, "圆片定义 ID 唯一");
            Check(GameCatalog.Cards.All(c => c.artResourcePath == "Art/Pogs/pog_" + c.artIndex.ToString("00")), "资源路径一致");
            Check(GameCatalog.Cards.All(c => c.Physics.mass > 0 && !string.IsNullOrEmpty(c.spinFeel)), "物理与自然语言手感完整");

            var repo = new SaveRepository(Path.Combine(work, "main"));
            var initial = repo.LoadOrCreate();
            Check(initial.pendingMatch == null && repo.Current.pendingMatch == null && !repo.HasPendingMatch && repo.GetPendingMatch() == null, "Unity 空内联 class 仍表示没有约战");
            Check(new SaveRepository(Path.Combine(work, "main")).LoadOrCreate().pendingMatch == null, "模拟 Unity 空约战写盘再读仍为空");
            var player = repo.PlayerCards;
            Check(player.Count == 6 && player.Count(c => c.isFoundation) == 1, "1 张可交易基础练习片 + 5 张真片");
            Check(initial.cards.Select(c => c.instanceId).Distinct().Count() == initial.cards.Count, "每张实例 ID 唯一");
            var foundation = player.Single(c => c.isFoundation);
            var stake = player.First(c => !c.isFoundation);
            var npcStake = repo.NpcCards("xiaoyu").First();
            initial.cards.Clear();
            player[0].ownerId = "invalid";
            Check(repo.Current.cards.Count > 6 && repo.GetInstance(foundation.instanceId).ownerId == "player", "读出的副本不会改动真实收藏");

            long revision = repo.Current.revision;
            Reject(() => repo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new string[0], new[] { npcStake.instanceId }), "拒绝空赌注");
            Reject(() => repo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { foundation.instanceId }, new[] { npcStake.instanceId }), "拒绝基础片赌注");
            Reject(() => repo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { npcStake.instanceId }, new[] { stake.instanceId }), "拒绝赌别人的圆片");
            Reject(() => repo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { stake.instanceId, stake.instanceId }, new[] { npcStake.instanceId, npcStake.instanceId }), "拒绝重复下注");
            Check(repo.Current.revision == revision, "失败操作不写入存档");
            repo.SetTreasured(stake.instanceId, true);
            Reject(() => repo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { stake.instanceId }, new[] { npcStake.instanceId }), "珍藏片不能下注");
            repo.SetTreasured(stake.instanceId, false);

            var match = repo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { stake.instanceId }, new[] { npcStake.instanceId }, true, 12345);
            Check(repo.HasPendingMatch && repo.IsCardLocked(stake.instanceId), "开局立即保存并锁定赌注");
            Reject(() => repo.SetTreasured(stake.instanceId, true), "进行中的赌注不可改为珍藏");
            Reject(() => repo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { stake.instanceId }, new[] { npcStake.instanceId }), "禁止同时创建两局");
            repo.UpdatePendingProgress(match.matchId, "{\"message\":\"夏天\",\"x\":1.25}", 3, 1, 0, 3, false);
            repo.AbortPendingMatch();
            var resumed = new SaveRepository(Path.Combine(work, "main"));
            resumed.LoadOrCreate();
            Check(resumed.GetPendingMatch().matchId == match.matchId && resumed.GetPendingMatch().paused, "退出后真实对局仍可恢复");
            Check(resumed.GetPendingMatch().checkpoint.boardStateJson.Contains("夏天") && resumed.GetPendingMatch().randomSeed == 12345, "回合快照与随机种子保留");
            resumed.ResumePendingMatch();
            Check(!resumed.GetPendingMatch().paused, "继续对局取消暂停");
            Reject(() => resumed.UpdatePendingProgress(match.matchId, "{}", 1, 0, 0, 1, true), "拒绝进度倒退");
            var win = resumed.ResolvePendingMatch(match.matchId, MatchOutcome.PlayerWin, 4);
            Check(win.gainedCardIds.SequenceEqual(new[] { npcStake.instanceId }) && win.lostCardIds.Count == 0, "只赢约定赌注");
            Check(resumed.GetInstance(npcStake.instanceId).ownerId == "player" && resumed.GetInstance(stake.instanceId).ownerId == "player", "胜利只转移对方约定片");
            Check(!resumed.HasPendingMatch && resumed.Current.progress.wins == 1, "结算清除 pending 并记录战绩");
            Check(new SaveRepository(Path.Combine(work, "main")).LoadOrCreate().pendingMatch == null, "结算后重载不会出现幽灵约战");
            revision = resumed.Current.revision;
            var repeated = resumed.ResolvePendingMatch(match.matchId, MatchOutcome.NpcWin, 999);
            Check(repeated.outcome == MatchOutcome.PlayerWin && resumed.Current.revision == revision && resumed.Current.progress.totalMatches == 1, "重复或矛盾结算幂等");

            var secondNpc = resumed.NpcCards("xiaoyu").First();
            var lossMatch = resumed.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.RingOut, new[] { stake.instanceId }, new[] { secondNpc.instanceId });
            var loss = resumed.ResolvePendingMatch(lossMatch.matchId, MatchOutcome.NpcWin, 2);
            Check(loss.lostCardIds.Single() == stake.instanceId && resumed.GetInstance(stake.instanceId).ownerId == "xiaoyu", "输片留在该 NPC 收藏");
            Check(resumed.LostPlayerCards.Any(c => c.instanceId == stake.instanceId), "可追踪输掉的旧片");
            var recoveryStake = resumed.PlayerCards.First(c => c.CanStake);
            var recovery = resumed.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { recoveryStake.instanceId }, new[] { stake.instanceId });
            var recovered = resumed.ResolvePendingMatch(recovery.matchId, MatchOutcome.PlayerWin, 2);
            Check(recovered.recoveredCardIds.Single() == stake.instanceId && resumed.GetInstance(stake.instanceId).history.Count == 3, "夺回原实例并保留全部转手历史");
            Check(resumed.Current.progress.recoveredCards == 1, "累计夺回进度");

            var practice = resumed.CreatePracticeMatch("xiaoyu", "home", RuleKind.Flip, new[] { foundation.instanceId }, new[] { secondNpc.instanceId });
            var practiceResult = resumed.ResolvePendingMatch(practice.matchId, MatchOutcome.PlayerWin, 5);
            Check(!practiceResult.isReal && practiceResult.gainedCardIds.Count == 0 && resumed.GetInstance(secondNpc.instanceId).ownerId == "xiaoyu", "练习局保留战绩但不交易");
            Check(resumed.GetInstance(foundation.instanceId).wins == 1 && resumed.Current.progress.practiceMatches == 1, "基础片可以练习并积累履历");
            var beforeDrawOwners = resumed.Current.cards.ToDictionary(c => c.instanceId, c => c.ownerId);
            var draw = resumed.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { stake.instanceId }, new[] { secondNpc.instanceId });
            resumed.ResolvePendingMatch(draw.matchId, MatchOutcome.Draw, 6);
            Check(resumed.Current.cards.All(c => beforeDrawOwners[c.instanceId] == c.ownerId), "平局不交换赌注");

            var allPlayerStakes = resumed.PlayerCards.Where(c => c.CanStake).Select(c => c.instanceId).ToArray();
            var allNpcStakes = resumed.NpcCards("dazhi").Take(allPlayerStakes.Length).Select(c => c.instanceId).ToArray();
            var allLost = resumed.CreatePendingMatch("dazhi", "alley", RuleKind.RingOut, allPlayerStakes, allNpcStakes);
            resumed.ForfeitPendingMatch();
            Check(resumed.PlayerCards.Count == 1 && resumed.PlayerCards[0].isFoundation, "输光可交易片仍保留基础片可玩");
            Check(resumed.GetSettlement(allLost.matchId).outcome == MatchOutcome.NpcWin, "认输正常结算并保留收据");
            resumed.RecordPracticeMatch("xiaoyu", "home", RuleKind.Flip, MatchOutcome.PlayerWin, 2);
            Check(resumed.Current.progress.practiceMatches == 2, "无赌片仍可记录练习");

            var stale = new SaveRepository(Path.Combine(work, "main"));
            stale.LoadOrCreate();
            resumed.VisitLocation("old_tree");
            Reject(() => stale.AdvanceTime(), "拒绝其他实例覆盖较新的存档");
            stale.Reload();
            int dayBeforeRest = stale.Current.progress.day;
            var periodBeforeRest = stale.Current.progress.period;
            stale.AdvanceTime();
            Check(stale.Current.progress.period == (periodBeforeRest == DayPeriod.Evening ? DayPeriod.Morning : (DayPeriod)((int)periodBeforeRest + 1)), "重新加载后允许休息推进时段");
            while (stale.Current.progress.period != DayPeriod.Evening) stale.AdvanceTime();
            dayBeforeRest = stale.Current.progress.day;
            stale.AdvanceTime();
            Check(stale.Current.progress.day == dayBeforeRest + 1 && stale.Current.progress.period == DayPeriod.Morning, "黄昏跨日进入清晨");

            var backupRepo = new SaveRepository(Path.Combine(work, "backup"));
            backupRepo.LoadOrCreate();
            var backupMatch = backupRepo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { backupRepo.PlayerCards.First(c => c.CanStake).instanceId }, new[] { backupRepo.NpcCards("xiaoyu").First().instanceId });
            backupRepo.UpdatePendingProgress(backupMatch.matchId, "{\"ok\":true}", 1, 0, 0, 1, false);
            File.WriteAllText(backupRepo.SavePath, "partial interrupted JSON");
            var restored = new SaveRepository(Path.Combine(work, "backup"));
            restored.LoadOrCreate();
            Check(restored.GetPendingMatch().matchId == backupMatch.matchId && restored.LastRecoveryMessage != null, "主档损坏从备份恢复真实 pending");
            Check(Directory.GetFiles(Path.Combine(work, "backup"), "*.corrupt.*").Length == 1, "损坏原档留存可调查");
            Check(new SaveRepository(Path.Combine(work, "backup")).LoadOrCreate().pendingMatch.matchId == backupMatch.matchId, "恢复后的主档可再次加载");

            var emptyNpcRepo = new SaveRepository(Path.Combine(work, "empty-npc"));
            emptyNpcRepo.LoadOrCreate();
            while (emptyNpcRepo.NpcCards("xiaoyu").Count > 0)
            {
                var challenge = emptyNpcRepo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip,
                    new[] { emptyNpcRepo.PlayerCards.First(c => c.CanStake).instanceId }, new[] { emptyNpcRepo.NpcCards("xiaoyu")[0].instanceId });
                emptyNpcRepo.ResolvePendingMatch(challenge.matchId, MatchOutcome.PlayerWin, 1);
            }
            var chosenPracticeCard = emptyNpcRepo.PlayerCards.First(c => c.CanStake);
            var borrowedPractice = emptyNpcRepo.CreatePracticeMatch("xiaoyu", "school_gate", RuleKind.Flip, 987, chosenPracticeCard.instanceId, true);
            Check(!borrowedPractice.isReal && borrowedPractice.npcStakeIds.Count == 0 && borrowedPractice.playerStakeIds.Single() == chosenPracticeCard.instanceId, "伙伴收藏输空仍能借片练习并记录选中片");
            Check(new SaveRepository(Path.Combine(work, "empty-npc")).LoadOrCreate().pendingMatch.soloPractice, "街边自己练手模式跨进程保留");
            emptyNpcRepo.ResolvePendingMatch(borrowedPractice.matchId, MatchOutcome.PlayerWin, 1);
            Check(emptyNpcRepo.NpcCards("xiaoyu").Count == 0, "练习借片不生成可交易收藏");
            Reject(() => emptyNpcRepo.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip,
                new[] { chosenPracticeCard.instanceId }, new string[0]), "伙伴无片时不可空赌约战");
            Reject(() => emptyNpcRepo.CreatePendingMatch("dazhi", "alley", RuleKind.Flip,
                new[] { chosenPracticeCard.instanceId }, new[] { emptyNpcRepo.NpcCards("dazhi")[0].instanceId }, true, 1, true), "自己练手模式不能进行真实赌片");

            var corruptDirectory = Path.Combine(work, "corrupt");
            Directory.CreateDirectory(corruptDirectory);
            File.WriteAllText(Path.Combine(corruptDirectory, "collection.json"), "broken primary");
            File.WriteAllText(Path.Combine(corruptDirectory, "collection.backup.json"), "broken backup");
            Reject(() => new SaveRepository(corruptDirectory).LoadOrCreate(), "两份存档损坏时明确报错");
            Check(File.ReadAllText(Path.Combine(corruptDirectory, "collection.json")) == "broken primary", "双档损坏也不覆盖玩家旧收藏");

            var marbleRepository = new SaveRepository(Path.Combine(work, "marble"));
            marbleRepository.LoadOrCreate();
            Check(marbleRepository.Current.progress.marbleRounds == 0 && marbleRepository.Current.progress.marbleBestScore == 0, "玻璃球新档从零局零分开始");
            Check(marbleRepository.RecordMarbleRound(7) == 7 && marbleRepository.Current.progress.marbleRounds == 1, "首局7分记为最高分并累积一局");
            Check(marbleRepository.RecordMarbleRound(3) == 7 && marbleRepository.Current.progress.marbleBestScore == 7, "第二局3分不让7分最高纪录回退");
            Check(marbleRepository.Current.progress.marbleRounds == 2 && marbleRepository.Current.progress.totalMatches == 0, "两局玻璃球独立计数且不影响打片战绩");
            var marbleReloaded = new SaveRepository(Path.Combine(work, "marble"));
            var marbleSave = marbleReloaded.LoadOrCreate();
            Check(marbleSave.progress.marbleRounds == 2 && marbleSave.progress.marbleBestScore == 7, "玻璃球局数与最高分重载持久化");
            long marbleRevision = marbleReloaded.Current.revision;
            byte[] marbleBefore = File.ReadAllBytes(marbleReloaded.SavePath);
            Reject(() => marbleReloaded.RecordMarbleRound(-1), "拒绝负数玻璃球成绩");
            Reject(() => marbleReloaded.RecordMarbleRound(9), "拒绝超过8颗的玻璃球成绩");
            Check(marbleReloaded.Current.revision == marbleRevision && File.ReadAllBytes(marbleReloaded.SavePath).SequenceEqual(marbleBefore), "非法玻璃球成绩不写档且不改变修订号");
            Check(marbleReloaded.Current.progress.marbleRounds == 2 && marbleReloaded.Current.progress.marbleBestScore == 7, "非法成绩不改变局数和最高分");
            Check(marbleReloaded.RecordMarbleRound(0) == 7 && marbleReloaded.Current.progress.marbleRounds == 3, "0分是合法完成局且不回退纪录");
            Check(marbleReloaded.RecordMarbleRound(8) == 8 && marbleReloaded.Current.progress.marbleRounds == 4, "8分满分合法并刷新纪录");

            var legacyDirectory = Path.Combine(work, "legacy-marble");
            var legacyRepository = new SaveRepository(legacyDirectory);
            var legacyOriginal = legacyRepository.LoadOrCreate();
            RewriteSavePayload(legacyRepository.SavePath, payload =>
            {
                var progress = payload["progress"].AsObject();
                progress.Remove("marbleRounds");
                progress.Remove("marbleBestScore");
            });
            var legacyLoadedRepository = new SaveRepository(legacyDirectory);
            var legacyLoaded = legacyLoadedRepository.LoadOrCreate();
            Check(legacyLoaded.progress.marbleRounds == 0 && legacyLoaded.progress.marbleBestScore == 0, "缺少新增字段的旧档兼容零默认值");
            Check(legacyLoaded.profileId == legacyOriginal.profileId && legacyLoaded.cards.Select(card => card.instanceId).SequenceEqual(legacyOriginal.cards.Select(card => card.instanceId)), "旧档补默认值保留玩家身份与全部收藏实例");
            Check(legacyLoaded.schemaVersion == legacyOriginal.schemaVersion && legacyLoaded.revision == legacyOriginal.revision && !legacyLoadedRepository.HasPendingMatch, "旧档不强制升级版本或产生幽灵约战");
            legacyLoadedRepository.RecordMarbleRound(7);
            legacyLoadedRepository.RecordMarbleRound(3);
            var legacyAfter = new SaveRepository(legacyDirectory).LoadOrCreate();
            Check(legacyAfter.progress.marbleRounds == 2 && legacyAfter.progress.marbleBestScore == 7, "旧档首次记录玻璃球后新字段正常持久化");

            foreach (var invalidData in new[] { new[] { -1, 0 }, new[] { 0, -1 }, new[] { 0, 9 } })
            {
                var invalidRepository = new SaveRepository(Path.Combine(work, "invalid-marble-" + checks));
                invalidRepository.LoadOrCreate();
                RewriteSavePayload(invalidRepository.SavePath, payload =>
                {
                    payload["progress"]["marbleRounds"] = invalidData[0];
                    payload["progress"]["marbleBestScore"] = invalidData[1];
                });
                Reject(() => new SaveRepository(Path.GetDirectoryName(invalidRepository.SavePath)).LoadOrCreate(), "拒绝带有效校验和的非法玻璃球旧数据 " + invalidData[0] + "/" + invalidData[1]);
            }
            VerifyVersionTwo(work);
            Console.WriteLine("PASS: " + checks + " data-layer invariants checked.");
        }
        finally
        {
            // 仅清除本测试在明确临时根目录中创建的唯一子目录。
            if (work.StartsWith(testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(work, true);
        }
    }

    private static long Clock(GameSave save) { return ((long)save.progress.day - 1) * 4 + (int)save.progress.period; }
    private static SaveRepository Fresh(string work, string name)
    {
        var repo = new SaveRepository(Path.Combine(work, name)); repo.LoadOrCreate(); return repo;
    }

    private static void VerifyVersionTwo(string work)
    {
        Check(SaveRepository.SchemaVersion == 2 && GameCatalog.Locations.Length == 8 && GameCatalog.Npcs.Length == 7, "schema2、八地点、七伙伴稳定目录");
        Check(Enumerable.Range(0, 42).All(i => GameCatalog.Cards[i].id == "pog_" + i.ToString("00") && GameCatalog.Cards[i].artIndex == i), "原30 ID保留，新12张只追加");
        Check(CampaignCatalog.Stages.Length == 8 && CampaignCatalog.TournamentStages.Length == 3 && CampaignCatalog.ShopOffers.Length == 43, "八故事、三杯赛轮、42真片及独立练习片报价");
        Check(CampaignCatalog.SwapOffers.All(o => GameCatalog.GetCard(o.wantedDefinitionId).rarity == GameCatalog.GetCard(o.offeredDefinitionId).rarity), "所有伙伴清单同稀有度一换一");
        var approvedNpc = new[] { "xiaoyu", "doudou", "ayue", "xiaoyu", "taotao", "dazhi", "linlin", "ayan" };
        var approvedLocations = new[] { "home", "square", "school_gate", "corridor", "alley", "shop", "old_tree", "playground" };
        var approvedRules = new[] { RuleKind.Flip, RuleKind.RingOut, RuleKind.Flip, RuleKind.Flip, RuleKind.RingOut, RuleKind.RingOut, RuleKind.Flip, RuleKind.Flip };
        Check(CampaignCatalog.Stages.Select(s => s.npcId).SequenceEqual(approvedNpc) && CampaignCatalog.Stages.Select(s => s.locationId).SequenceEqual(approvedLocations) && CampaignCatalog.Stages.Select(s => s.rule).SequenceEqual(approvedRules), "剧情严格遵守批准NPC、场地与玩法顺序");
        Check(CampaignCatalog.Stages.All(s => !string.IsNullOrEmpty(s.introLine) && !string.IsNullOrEmpty(s.hintLine) && !string.IsNullOrEmpty(s.winLine) && !string.IsNullOrEmpty(s.loseLine)), "所有故事含完整原创对白和提示");
        Check(CampaignCatalog.Stages[1].title == "一场误会" && CampaignCatalog.Stages[1].introLine.Contains("听错") && CampaignCatalog.Stages[1].loseLine.Contains("对不起"), "广场剧情保留误会、说明约战与胜败和解");
        Check(GameCatalog.Cards.Skip(30).Take(4).All(c => c.series == "街坊小英雄") && GameCatalog.Cards.Skip(34).Take(4).All(c => c.series == "花纹游戏标") && GameCatalog.Cards.Skip(38).Take(4).All(c => c.series == "校园冒险队"), "新增三系列各四张符合批准分类");
        Check(Enum.GetValues(typeof(DayPeriod)).Cast<DayPeriod>().Select(GameCatalog.PeriodName).SequenceEqual(new[] { "上午", "正午", "下午", "傍晚" }), "时段名称按批准上午正午下午傍晚");
        Check(new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare, CardRarity.Legendary }.Select(r => CampaignCatalog.BuyPrice(GameCatalog.Cards.First(c => c.rarity == r))).SequenceEqual(new[] { 6, 12, 24, 48 }) && new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare, CardRarity.Legendary }.Select(r => CampaignCatalog.SellPrice(GameCatalog.Cards.First(c => c.rarity == r))).SequenceEqual(new[] { 3, 6, 12, 24 }), "真片批准买卖价6/12/24/48与3/6/12/24");
        Check(Enumerable.Range(0, 100).Count(i => SaveRepository.WeatherForSeed(i) == WeatherKind.Sunny) == 50 && Enumerable.Range(0, 100).Count(i => SaveRepository.WeatherForSeed(i) == WeatherKind.Cloudy) == 30 && Enumerable.Range(0, 100).Count(i => SaveRepository.WeatherForSeed(i) == WeatherKind.Rainy) == 20, "天气分段精确50/30/20");

        var trading = Fresh(work, "v2-trade");
        Check(trading.Current.glassMarbles == 12 && trading.Current.tournament == null && trading.Current.activeErrand == null, "新档12珠，无Unity内联幽灵杯赛或跑腿");
        var foundation = trading.PlayerCards.Single(c => c.isFoundation);
        Check(foundation.CanTrade && !foundation.CanStake && !trading.CanStake(foundation.instanceId), "基础练习片可交易但不能下注");
        var clock = Clock(trading.Current);
        var quote = trading.GetTradeQuote(TradeKind.Sell, "shop", foundation.instanceId);
        Check(quote.marbleDelta == 1 && quote.isFoundation, "练习片只卖1珠");
        var sold = trading.ExecuteTrade(quote);
        Check(sold.balanceAfter == 13 && trading.GetInstance(foundation.instanceId).ownerId == "shopkeeper" && Clock(trading.Current) == clock, "卖练习片原实例转给独立店主，不耗时");
        Check(trading.LostPlayerCards.All(c => c.instanceId != foundation.instanceId), "主动售片不进入输片夺回列表");
        long revision = trading.Current.revision;
        Check(trading.ExecuteTrade(quote).transactionId == sold.transactionId && trading.Current.revision == revision, "售片重放幂等不再给珠");
        var reloaded = new SaveRepository(Path.GetDirectoryName(trading.SavePath)); reloaded.LoadOrCreate();
        Check(!reloaded.PlayerCards.Any(c => c.isFoundation) && reloaded.Current.cards.Count == trading.Current.cards.Count, "基础片离手重载合法且不免费补片");
        reloaded.EnsurePlayableCollection();
        Check(reloaded.Current.revision == revision && !reloaded.PlayerCards.Any(c => c.isFoundation), "兼容EnsurePlayableCollection不补发也不写档");
        var boughtPractice = reloaded.ExecuteTrade(reloaded.GetTradeQuote(TradeKind.Buy, "buy_practice"));
        Check(boughtPractice.marbleDelta == -2 && reloaded.GetInstance(boughtPractice.gainedCardIds.Single()).isFoundation, "独立练习片买2珠并保留练习身份");
        var trueZero = reloaded.ExecuteTrade(reloaded.GetTradeQuote(TradeKind.Buy, "buy_pog_00"));
        Check(!reloaded.GetInstance(trueZero.gainedCardIds.Single()).isFoundation && reloaded.CanStake(trueZero.gainedCardIds.Single()), "同定义普通真片与基础练习实例资格分离");
        var treasured = reloaded.PlayerCards.First(c => !c.isFoundation);
        reloaded.SetTreasured(treasured.instanceId, true);
        var protectedQuote = reloaded.GetTradeQuote(TradeKind.Sell, "shop", treasured.instanceId);
        byte[] before = File.ReadAllBytes(reloaded.SavePath);
        Reject(() => reloaded.ExecuteTrade(protectedQuote), "珍藏每次转手必须本次明确确认");
        Check(File.ReadAllBytes(reloaded.SavePath).SequenceEqual(before), "未确认珍藏交易不写档");
        reloaded.ExecuteTrade(protectedQuote, true);
        Check(reloaded.GetInstance(treasured.instanceId).treasured && reloaded.GetInstance(treasured.instanceId).ownerId == "shopkeeper", "单次确认不永久解除珍藏标记");
        var staleQuote = reloaded.GetTradeQuote(TradeKind.Buy, "buy_practice");
        reloaded.VisitLocation("square");
        Reject(() => reloaded.ExecuteTrade(staleQuote), "旧revision报价不能覆盖变更后的收藏");
        var forged = reloaded.GetTradeQuote(TradeKind.Buy, "buy_practice"); forged.marbleDelta = 999;
        Reject(() => reloaded.ExecuteTrade(forged), "拒绝篡改报价金额");
        Reject(() => reloaded.GetTradeQuote(TradeKind.Buy, "buy_pog_30"), "Boss招牌首次战胜前未上架");
        Check(CampaignCatalog.ShopOffers.All(o => o.isFoundation || o.buyPrice > CampaignCatalog.SellPrice(GameCatalog.GetCard(o.cardDefinitionId))), "买卖价差阻止即时循环套利");

        var swaps = Fresh(work, "v2-swap");
        var give = swaps.PlayerCards.Single(c => c.definitionId == "pog_01");
        var swapOffer = CampaignCatalog.SwapOffers.First(o => o.npcId == "xiaoyu" && o.wantedDefinitionId == give.definitionId);
        var swapQuote = swaps.GetTradeQuote(TradeKind.Swap, swapOffer.id, give.instanceId);
        var incomingId = swapQuote.npcCardId;
        clock = Clock(swaps.Current);
        var swapReceipt = swaps.ExecuteTrade(swapQuote);
        Check(swaps.GetInstance(give.instanceId).ownerId == "xiaoyu" && swaps.GetInstance(incomingId).ownerId == "player" && swapReceipt.marbleDelta == 0 && Clock(swaps.Current) == clock, "伙伴同稀有度一换一使用原实例且不耗时");
        Check(!swaps.LostPlayerCards.Any(c => c.instanceId == give.instanceId), "主动换片不计失片");
        var offerTwo = CampaignCatalog.SwapOffers.First(o => o.npcId == "xiaoyu" && o.wantedDefinitionId == "pog_34");
        var buy34 = swaps.ExecuteTrade(swaps.GetTradeQuote(TradeKind.Buy, "buy_pog_34"));
        var secondSwapQuote = swaps.GetTradeQuote(TradeKind.Swap, offerTwo.id, buy34.gainedCardIds.Single());
        var secondSwapReceipt = swaps.ExecuteTrade(secondSwapQuote);
        Check(swaps.Current.transactions.Count(r => r.kind == TradeKind.Swap && r.npcId == "xiaoyu" && r.day == swaps.Current.progress.day) == 2 && secondSwapReceipt.offerId == offerTwo.id && Clock(swaps.Current) == clock, "同伙伴两份清单同日分别成交一次，不耗时");
        var another34 = swaps.ExecuteTrade(swaps.GetTradeQuote(TradeKind.Buy, "buy_pog_34"));
        Reject(() => swaps.GetTradeQuote(TradeKind.Swap, offerTwo.id, another34.gainedCardIds.Single()), "同一清单当天不能用另一个实物重复成交");
        RewriteSavePayload(swaps.SavePath, payload => payload["transactions"].AsArray().Single(r => r["quoteId"].GetValue<string>() == secondSwapQuote.quoteId).AsObject().Remove("offerId")); swaps.Reload();
        Check(swaps.Current.transactions.Single(r => r.quoteId == secondSwapQuote.quoteId).offerId == offerTwo.id, "schema2旧回执缺offerId由双方定义恢复具体清单");
        Reject(() => swaps.GetTradeQuote(TradeKind.Swap, offerTwo.id, another34.gainedCardIds.Single()), "旧回执清单归一化仍防同日重复");
        revision = swaps.Current.revision; swaps.ExecuteTrade(secondSwapQuote);
        Check(swaps.Current.revision == revision, "补清单旧回执仍能幂等重放原成交");
        var returnStock = swaps.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { secondSwapReceipt.gainedCardIds.Single() }, new[] { swaps.NpcCards("xiaoyu").First(c => c.CanStake).instanceId });
        swaps.ResolvePendingMatch(returnStock.matchId, MatchOutcome.NpcWin, 1);
        while (swaps.Current.progress.period != DayPeriod.Evening) swaps.Rest(); swaps.Rest();
        Check(swaps.GetTradeQuote(TradeKind.Swap, offerTwo.id, another34.gainedCardIds.Single()).marbleDelta == 0, "跨日后同份清单额度恢复，仍验证NPC实际持有库存");

        var bankrupt = Fresh(work, "v2-bankruptcy");
        foreach (var card in bankrupt.PlayerCards.ToArray()) bankrupt.ExecuteTrade(bankrupt.GetTradeQuote(TradeKind.Sell, "shop", card.instanceId));
        RewriteSavePayload(bankrupt.SavePath, p => p["glassMarbles"] = 0); bankrupt.Reload();
        Check(bankrupt.PlayerCards.Count == 0 && bankrupt.Current.glassMarbles == 0, "零库存零余额为合法状态");
        Reject(() => bankrupt.CreatePracticeMatch("xiaoyu", "home", RuleKind.Flip), "零库存不空引用也不免费练习补片");
        Reject(() => bankrupt.GetTradeQuote(TradeKind.Buy, "buy_pog_01"), "零余额不能赊账买片");
        var count = bankrupt.Current.cards.Count;
        clock = Clock(bankrupt.Current);
        var errand = bankrupt.AcceptErrand("errand_basket");
        Check(Clock(bankrupt.Current) == clock && !errand.destinationVisited, "接跑腿不耗时且必须接取后到达");
        Reject(() => bankrupt.CompleteErrand(errand.completionId), "不到目的地不得交差");
        bankrupt.VisitLocation(errand.destinationLocationId);
        var completed = bankrupt.CompleteErrand(errand.completionId);
        Check(completed.marbleDelta == 6 && bankrupt.Current.glassMarbles == 6 && Clock(bankrupt.Current) == clock + 1 && bankrupt.Current.cards.Count == count, "破产跑腿挣6珠、耗一时段、没有免费发片");
        revision = bankrupt.Current.revision;
        bankrupt.CompleteErrand(errand.completionId);
        Check(bankrupt.Current.glassMarbles == 6 && bankrupt.Current.revision == revision && Clock(bankrupt.Current) == clock + 1, "跑腿完成重放不重复收益或推进时间");
        var restoredCard = bankrupt.ExecuteTrade(bankrupt.GetTradeQuote(TradeKind.Buy, "buy_pog_01"));
        Check(bankrupt.Current.glassMarbles == 0 && bankrupt.PlayerCards.Count == 1 && bankrupt.CanStake(restoredCard.gainedCardIds.Single()), "一次跑腿足够买真片，破产可恢复");
        var nextErrand = bankrupt.AcceptErrand("errand_basket");
        Check(nextErrand.completionId != errand.completionId && !nextErrand.destinationVisited, "重复跑腿有新完成ID，不能沿用上次到达");

        var story = Fresh(work, "v2-story");
        count = story.Current.cards.Count;
        var storyFoundation = story.PlayerCards.Single(c => c.isFoundation);
        Reject(() => story.CreateStoryMatch("story_02", story.PlayerCards.First(c => c.CanStake).instanceId, null), "后续故事关须先首胜前置");
        Reject(() => story.CreateStoryMatch("story_01", storyFoundation.instanceId, null), "剧情真局禁止基础练习片");
        int earned = 0;
        foreach (var stage in CampaignCatalog.Stages)
        {
            var environment = story.VisitLocation(stage.locationId);
            var expectedOpponent = story.GetOpponentStakeForChallenge(stage.id);
            clock = Clock(story.Current);
            var pending = story.CreateStoryMatch(stage.id, story.PlayerCards.First(c => c.CanStake).instanceId, environment);
            Check(pending.route == MatchRoute.Story && pending.isReal && pending.playerStakeIds.Count == 1 && pending.npcStakeIds.SequenceEqual(new[] { expectedOpponent.instanceId }) && pending.layout == stage.layout, stage.id + "双方各押一张真片并冻结布局");
            environment.visitVariant = 2; environment.weather = WeatherKind.Rainy;
            story.VisitLocation("shop");
            var frozenBefore = UnityEngine.JsonUtility.ToJson(story.GetPendingMatch().environment);
            var loading = new SaveRepository(Path.GetDirectoryName(story.SavePath)); loading.LoadOrCreate();
            Check(UnityEngine.JsonUtility.ToJson(loading.GetPendingMatch().environment) == frozenBefore, stage.id + "变更UI场景及读档不改变冻结环境");
            Reject(() => story.GetTradeQuote(TradeKind.Sell, "shop", pending.playerStakeIds.Single()), stage.id + "待结算押片不能交易");
            var beforeResolve = story.Current.revision;
            var receipt = story.ResolvePendingMatch(pending.matchId, MatchOutcome.PlayerWin, 2);
            earned += stage.rewardMarbles;
            Check(receipt.firstClear && receipt.rewardMarbles == stage.rewardMarbles && story.Current.glassMarbles == 12 + earned && story.Current.revision == beforeResolve + 1 && Clock(story.Current) == clock + 1, stage.id + "首通奖励、卡归属、进度和时段一次提交");
            if (stage.rewardCardIndex >= 0)
            {
                Check(receipt.rewardCardIds.Single() == expectedOpponent.instanceId && story.GetInstance(expectedOpponent.instanceId).definitionId == GameCatalog.Cards[stage.rewardCardIndex].id && story.Current.cards.Count(c => c.definitionId == GameCatalog.Cards[stage.rewardCardIndex].id) == 1, stage.id + "赢唯一Boss原实例，未复制奖片");
                Check(CampaignCatalog.IsOfferUnlocked(story.Current, CampaignCatalog.GetShopOffer("buy_" + GameCatalog.Cards[stage.rewardCardIndex].id)), stage.id + "首勝同次解锁招牌商店");
            }
            revision = story.Current.revision;
            story.ResolvePendingMatch(pending.matchId, MatchOutcome.NpcWin, 100);
            Check(story.Current.revision == revision && story.Current.glassMarbles == 12 + earned, stage.id + "结算重放不会重复首通奖励");
        }
        Check(earned == 56 && story.Current.story.clearedStageIds.Count == 8 && story.Current.cards.Count == count, "完整剧情56珠、八关首胜，只有真实转片");
        var replay = story.CreateStoryMatch("story_01", story.PlayerCards.First(c => c.CanStake).instanceId, null);
        var replayReceipt = story.ResolvePendingMatch(replay.matchId, MatchOutcome.PlayerWin, 1);
        Check(!replayReceipt.firstClear && replayReceipt.rewardMarbles == 0 && story.Current.glassMarbles == 68, "故事重玩只转赌注，不重复章节奖励");
        var boughtBoss = story.ExecuteTrade(story.GetTradeQuote(TradeKind.Buy, "buy_pog_30"));
        Check(story.GetInstance(boughtBoss.gainedCardIds.Single()).history[0].reason == CardTransferReason.Purchase && story.Current.cards.Count(c => c.definitionId == "pog_30") == 2, "首勝后合法付珠兑换新实例，保留原Boss历史");

        var cup = Fresh(work, "v2-cup");
        var run = cup.EnterTournament();
        Check(run.participants.Count == 8 && run.participants.Distinct().Count() == 8 && run.rounds.Select(r => r.npcId).SequenceEqual(new[] { "ayue", "taotao", "ayan" }) && cup.Current.story.clearedStageIds.Count == 0, "独立八人杯赛冻结阿岳、桃桃、阿砚三轮，无剧情门槛");
        var bracketJson = UnityEngine.JsonUtility.ToJson(run);
        var cupReload = new SaveRepository(Path.GetDirectoryName(cup.SavePath)); cupReload.LoadOrCreate();
        Check(UnityEngine.JsonUtility.ToJson(cupReload.Current.tournament) == bracketJson, "杯赛签表、对手、种子重载一致");
        Reject(() => cup.EnterTournament(), "同届活动杯赛不重复报名");
        for (int retry = 0; retry < 2; retry++)
        {
            var pendingDraw = cup.CreateTournamentMatch(run.runId, cup.PlayerCards.First(c => c.CanStake).instanceId, null);
            clock = Clock(cup.Current); var balance = cup.Current.glassMarbles;
            var drawReceipt = cup.ResolvePendingMatch(pendingDraw.matchId, MatchOutcome.Draw, 3);
            Check(cup.Current.tournament.active && cup.Current.tournament.roundIndex == 0 && !cup.Current.tournament.rounds[0].completed && string.IsNullOrEmpty(cup.Current.tournament.rounds[0].matchId) && cup.Current.tournament.rounds[0].randomSeed == run.rounds[0].randomSeed && cup.Current.glassMarbles == balance && cup.GetInstance(pendingDraw.npcStakeIds.Single()).ownerId == "ayue" && Clock(cup.Current) == clock + 1, "杯赛连续平局" + retry + "不淘汰不转片，保持冻结轮次与种子");
            revision = cup.Current.revision; cup.ResolvePendingMatch(pendingDraw.matchId, MatchOutcome.Draw, 3);
            Check(cup.Current.revision == revision && Clock(cup.Current) == clock + 1, "同次平局结算重放不再推进时段");
        }
        for (int round = 0; round < 3; round++)
        {
            var stage = CampaignCatalog.GetTournamentStage(round);
            var environment = cup.VisitLocation(stage.locationId);
            var pending = cup.CreateTournamentMatch(run.runId, cup.PlayerCards.First(c => c.CanStake).instanceId, environment);
            clock = Clock(cup.Current);
            var receipt = cup.ResolvePendingMatch(pending.matchId, MatchOutcome.PlayerWin, 2);
            Check(pending.route == MatchRoute.Tournament && pending.rule == stage.rule && pending.randomSeed == run.rounds[round].randomSeed && cup.Current.tournament.roundIndex == round + 1 && receipt.rewardMarbles == (round == 2 ? 24 : 0) && Clock(cup.Current) == clock + 1, "杯赛第" + (round + 1) + "轮转真实赌注并一次推进，冠军只在末轮奖励");
            int bossIndex = round == 0 ? 30 : round == 1 ? 31 : 33;
            Check(receipt.defeatedBossId == stage.npcId && receipt.rewardCardIds.Single() == pending.npcStakeIds.Single() && cup.GetInstance(pending.npcStakeIds.Single()).definitionId == "pog_" + bossIndex && CampaignCatalog.IsOfferUnlocked(cup.Current, CampaignCatalog.GetShopOffer("buy_pog_" + bossIndex)), "杯赛首胜Boss转招牌原实例并解锁商店，不依赖剧情首通");
        }
        Check(cup.Current.tournament.champion && !cup.Current.tournament.active && cup.Current.glassMarbles == 36 && cup.Current.story.clearedStageIds.Count == 0, "冠军24珠，剧情路线保持独立");
        foreach (var stage in CampaignCatalog.Stages.Take(3))
        {
            var crossed = cup.CreateStoryMatch(stage.id, cup.PlayerCards.First(c => c.CanStake).instanceId, null);
            if (stage.id == "story_03") Check(cup.GetInstance(crossed.npcStakeIds.Single()).definitionId != "pog_30" && cup.Current.cards.Count(c => c.definitionId == "pog_30") == 1, "杯赛先击败阿岳，剧情再见只押普通片，不重新复制招牌");
            cup.ResolvePendingMatch(crossed.matchId, MatchOutcome.PlayerWin, 1);
        }
        var runTwo = cup.EnterTournament();
        Check(runTwo.runId != run.runId && runTwo.active, "再次参赛使用新runID");
        var lostCup = cup.CreateTournamentMatch(runTwo.runId, cup.PlayerCards.First(c => c.CanStake).instanceId, null);
        var beforeLoss = cup.Current.glassMarbles;
        cup.ForfeitPendingMatch();
        Check(!cup.Current.tournament.active && !cup.Current.tournament.champion && cup.Current.glassMarbles == beforeLoss && cup.GetInstance(lostCup.playerStakeIds.Single()).ownerId == "ayue", "杯赛投降淘汰、转真实片，没有冠军奖励");

        var environments = Fresh(work, "v2-environment");
        foreach (var location in GameCatalog.Locations)
        {
            clock = Clock(environments.Current);
            var visits = new List<int>();
            for (int i = 0; i < 4; i++) visits.Add(environments.VisitLocation(location.id).visitVariant);
            Check(visits.SequenceEqual(new[] { 0, 1, 2, 0 }) && Clock(environments.Current) == clock, location.id + "重访0/1/2循环持久化而不耗时");
        }
        for (int i = 0; i < 12; i++)
        {
            clock = Clock(environments.Current); var after = environments.Rest();
            Check(Clock(environments.Current) == clock + 1 && after.day == environments.Current.progress.day && after.period == environments.Current.progress.period && after.weather == SaveRepository.WeatherForSeed(after.weatherSeed), "休息一次推进时段与天气同次保存 " + i);
        }
        foreach (var location in GameCatalog.Locations)
        {
            environments.VisitLocation(location.id);
            RewriteSavePayload(environments.SavePath, p => { p["progress"]["environment"]["weather"] = 2; p["progress"]["environment"]["weatherSeed"] = 91; p["progress"]["environment"]["frictionMultiplier"] = location.id == "shop" || location.id == "corridor" ? 1 : .9; });
            environments.Reload();
            var wet = environments.VisitLocation(location.id);
            var freezing = environments.CreatePracticeMatch("xiaoyu", location.id, RuleKind.Flip, 98, environments.PlayerCards[0].instanceId, true, 3, wet);
            Check(freezing.layout == 3 && freezing.environment.weather == WeatherKind.Rainy && freezing.environment.frictionMultiplier == (location.id == "shop" || location.id == "corridor" ? 1f : .9f), location.id + "雨天冻结摩擦与有顶场地干燥，无练习上下文遗漏");
            environments.VisitLocation("home"); var froze = UnityEngine.JsonUtility.ToJson(freezing.environment);
            environments.Reload(); Check(UnityEngine.JsonUtility.ToJson(environments.GetPendingMatch().environment) == froze, location.id + "下雨练习局切地图/重载天气不变");
            environments.ResolvePendingMatch(freezing.matchId, MatchOutcome.Draw, 0);
        }
        VerifyMigrationAndBoundaries(work);
    }

    private static void VerifyMigrationAndBoundaries(string work)
    {
        var oldDirectory = Path.Combine(work, "v1-source");
        var old = Fresh(work, "v1-source");
        var player = old.PlayerCards.First(c => c.CanStake);
        var npc = old.NpcCards("xiaoyu").First();
        var oldMatch = old.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { player.instanceId }, new[] { npc.instanceId }, true, 13579);
        old.UpdatePendingProgress(oldMatch.matchId, "{\"version\":2,\"layout\":2,\"memo\":\"旧片的夏天\"}", 4, 2, 1, 4, false);
        RewriteSavePayload(old.SavePath, payload =>
        {
            payload["schemaVersion"] = 1; payload["revision"] = 10;
            foreach (var name in new[] { "glassMarbles", "story", "tournament", "activeErrand", "transactions", "activities" }) payload.Remove(name);
            var cards = payload["cards"].AsArray();
            for (int i = cards.Count - 1; i >= 0; i--)
                if (int.Parse(cards[i]["definitionId"].GetValue<string>().Substring(4)) >= 30 || new[] { "ayue", "doudou", "ayan" }.Contains(cards[i]["ownerId"].GetValue<string>())) cards.RemoveAt(i);
            foreach (var card in cards) foreach (var history in card["history"].AsArray()) { history.AsObject().Remove("reason"); history.AsObject().Remove("transactionId"); }
            var progress = payload["progress"].AsObject(); progress.Remove("environment"); progress.Remove("locationVisits"); progress["day"] = 6; progress["period"] = 1; progress["marbleRounds"] = 2; progress["marbleBestScore"] = 7;
            var npcs = progress["npcs"].AsArray(); for (int i = npcs.Count - 1; i >= 0; i--) if (new[] { "ayue", "doudou", "ayan" }.Contains(npcs[i]["npcId"].GetValue<string>())) npcs.RemoveAt(i);
            foreach (var name in new[] { "route", "stageId", "tournamentRunId", "layout", "environment" }) payload["pendingMatch"].AsObject().Remove(name);
        });
        File.Copy(old.SavePath, old.BackupPath, true);
        var originalFiles = Directory.GetFiles(oldDirectory).ToDictionary(path => path, File.ReadAllBytes);
        var originalPayload = JsonNode.Parse(JsonNode.Parse(File.ReadAllText(old.SavePath))["payload"].GetValue<string>());
        var oldIds = originalPayload["cards"].AsArray().Select(c => c["instanceId"].GetValue<string>()).ToArray();
        var oldOwners = originalPayload["cards"].AsArray().ToDictionary(c => c["instanceId"].GetValue<string>(), c => c["ownerId"].GetValue<string>());
        var importedDirectory = Path.Combine(work, "v2-import");
        var importer = new SaveRepository(importedDirectory, oldDirectory);
        var imported = importer.LoadOrCreate();
        Check(imported.schemaVersion == 2 && imported.revision == 11 && imported.glassMarbles == 12 && imported.profileId == originalPayload["profileId"].GetValue<string>(), "v1只读导入schema2一次升级并给予12珠，身份revision保留");
        Check(oldIds.All(id => imported.cards.Any(c => c.instanceId == id && c.ownerId == oldOwners[id])) && imported.progress.marbleRounds == 2 && imported.progress.marbleBestScore == 7, "导入保留旧GUID、归属和停用弹珠历史");
        Check(imported.pendingMatch.matchId == oldMatch.matchId && imported.pendingMatch.randomSeed == 13579 && imported.pendingMatch.layout == 2 && imported.pendingMatch.environment.geometryVersion == 1 && imported.pendingMatch.checkpoint.boardStateJson.Contains("旧片的夏天"), "旧真实pending恢复旧几何1、布局、种子及原棋盘");
        Check(originalFiles.All(file => File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) && Directory.GetFiles(oldDirectory).Length == originalFiles.Count, "导入绝不改旧主档、备份、锁文件或目录内容");
        var practiceLegacy = Path.Combine(work, "v1-practice"); Directory.CreateDirectory(practiceLegacy);
        File.Copy(old.SavePath, Path.Combine(practiceLegacy, "collection.json"));
        string oldFoundationId = originalPayload["cards"].AsArray().First(c => c["isFoundation"].GetValue<bool>())["instanceId"].GetValue<string>();
        RewriteSavePayload(Path.Combine(practiceLegacy, "collection.json"), p =>
        {
            p["pendingMatch"]["isReal"] = false; p["pendingMatch"]["soloPractice"] = true; p["pendingMatch"]["playerStakeIds"] = new JsonArray(oldFoundationId); p["pendingMatch"]["npcStakeIds"] = new JsonArray();
            p["pendingMatch"]["checkpoint"]["boardStateJson"] = "{\"version\":2,\"layout\":3}";
        });
        var practiceImport = new SaveRepository(Path.Combine(work, "v2-practice-import"), practiceLegacy); var practiceLoaded = practiceImport.LoadOrCreate();
        Check(!practiceLoaded.pendingMatch.isReal && practiceLoaded.pendingMatch.soloPractice && practiceLoaded.pendingMatch.playerStakeIds.Single() == oldFoundationId && practiceLoaded.pendingMatch.npcStakeIds.Count == 0 && practiceLoaded.pendingMatch.layout == 3 && practiceLoaded.pendingMatch.environment.geometryVersion == 1, "旧练习/借片pending保留基础实例与三号布局，不变真局");
        practiceImport.ResolvePendingMatch(oldMatch.matchId, MatchOutcome.PlayerWin, 5);
        Check(practiceImport.GetInstance(oldFoundationId).ownerId == "player" && practiceImport.Current.glassMarbles == 12, "旧练习完成没有押片转移或现金奖励");
        var repeated = new SaveRepository(importedDirectory, oldDirectory).LoadOrCreate();
        Check(repeated.revision == imported.revision && repeated.cards.Count == imported.cards.Count && repeated.glassMarbles == 12, "已有v2档不会再次导入、给钱或补NPC库存");
        foreach (int index in new[] { 30, 31, 32, 33 }) Check(imported.cards.Count(c => c.definitionId == "pog_" + index) == 1, "迁移Boss招牌" + index + "仅初始化一个实例");
        long clock = Clock(importer.Current);
        importer.ResolvePendingMatch(oldMatch.matchId, MatchOutcome.PlayerWin, 5);
        Check(importer.GetInstance(npc.instanceId).ownerId == "player" && Clock(importer.Current) == clock + 1 && importer.Current.progress.environment.geometryVersion == 2, "旧局正常原子结算，下一环境切换新几何2");
        var lostLegacy = Path.Combine(work, "v1-lost"); Directory.CreateDirectory(lostLegacy); File.Copy(old.SavePath, Path.Combine(lostLegacy, "collection.json"));
        RewriteSavePayload(Path.Combine(lostLegacy, "collection.json"), p =>
        {
            p["pendingMatch"] = null;
            var card = p["cards"].AsArray().Single(c => c["instanceId"].GetValue<string>() == player.instanceId); card["ownerId"] = "dazhi";
            card["history"].AsArray().Add(new JsonObject { ["matchId"] = "legacy_lost", ["locationId"] = "alley", ["fromOwnerId"] = "player", ["toOwnerId"] = "dazhi", ["description"] = "旧局输片" });
        });
        var lostImport = new SaveRepository(Path.Combine(work, "v2-lost-import"), lostLegacy); lostImport.LoadOrCreate();
        Check(lostImport.LostPlayerCards.Any(c => c.instanceId == player.instanceId) && lostImport.GetInstance(player.instanceId).history.Last().reason == CardTransferReason.Wager && lostImport.GetInstance(player.instanceId).ownerId == "dazhi", "旧无reason的赌注历史迁移为Wager，大志改常驻店门不改旧归属");

        var defaultRoot = Path.Combine(work, "default-root");
        var defaultLegacy = Path.Combine(defaultRoot, "SummerAfterSchool"); Directory.CreateDirectory(defaultLegacy);
        File.Copy(old.SavePath, Path.Combine(defaultLegacy, "collection.json"));
        UnityEngine.Application.DataPath = defaultRoot;
        try
        {
            var defaults = new SaveRepository(); defaults.LoadOrCreate();
            Check(defaults.SavePath == Path.Combine(defaultRoot, "SummerAfterSchool-v2", "collection.json") && File.ReadAllBytes(Path.Combine(defaultLegacy, "collection.json")).SequenceEqual(originalFiles[old.SavePath]), "默认版本目录隔离，旧0.3不能回写新版档");
        }
        finally { UnityEngine.Application.DataPath = null; }

        var brokenLegacy = Path.Combine(work, "broken-v1"); Directory.CreateDirectory(brokenLegacy);
        File.WriteAllText(Path.Combine(brokenLegacy, "collection.json"), "broken old primary"); File.Copy(old.SavePath, Path.Combine(brokenLegacy, "collection.backup.json"));
        new SaveRepository(Path.Combine(work, "v2-backup-import"), brokenLegacy).LoadOrCreate();
        Check(File.ReadAllText(Path.Combine(brokenLegacy, "collection.json")) == "broken old primary" && !Directory.GetFiles(brokenLegacy, "*.corrupt.*").Any(), "旧主档损坏只读选备份导入，不移动旧文件");

        var future = Fresh(work, "future"); future.Flush();
        RewriteSavePayload(future.SavePath, payload => payload["schemaVersion"] = 99);
        var futurePrimary = File.ReadAllBytes(future.SavePath); var futureBackup = File.ReadAllBytes(future.BackupPath);
        Reject(() => new SaveRepository(Path.GetDirectoryName(future.SavePath)).LoadOrCreate(), "未来schema明确拒读，不回退有效旧备份");
        Reject(() => future.VisitLocation("square"), "已运行实例也不能覆盖突然出现的未来档");
        Check(File.ReadAllBytes(future.SavePath).SequenceEqual(futurePrimary) && File.ReadAllBytes(future.BackupPath).SequenceEqual(futureBackup) && !Directory.GetFiles(Path.GetDirectoryName(future.SavePath), "*.corrupt.*").Any(), "未来主档和备份完全保留，无误判坏档改名");
        var futureEnvelope = Fresh(work, "future-envelope"); futureEnvelope.Flush();
        var envelopeNode = JsonNode.Parse(File.ReadAllText(futureEnvelope.SavePath)); envelopeNode["envelopeVersion"] = 2; File.WriteAllText(futureEnvelope.SavePath, envelopeNode.ToJsonString());
        Reject(() => new SaveRepository(Path.GetDirectoryName(futureEnvelope.SavePath)).LoadOrCreate(), "未来封装同样拒读，不退回旧备份");

        var failure = Fresh(work, "atomic-failure");
        var pending = failure.CreateStoryMatch("story_01", failure.PlayerCards.First(c => c.CanStake).instanceId, null);
        long revision = failure.Current.revision; byte[] before = File.ReadAllBytes(failure.SavePath); clock = Clock(failure.Current);
        using (var heldLock = new FileStream(failure.SavePath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Reject(() => failure.ResolvePendingMatch(pending.matchId, MatchOutcome.PlayerWin, 1), "写锁失败拒绝整个剧情结算");
        Check(failure.Current.revision == revision && failure.HasPendingMatch && failure.Current.glassMarbles == 12 && failure.Current.story.clearedStageIds.Count == 0 && Clock(failure.Current) == clock && File.ReadAllBytes(failure.SavePath).SequenceEqual(before), "失败不半转片、不半发奖、不半推进");
        failure.ResolvePendingMatch(pending.matchId, MatchOutcome.PlayerWin, 1);
        Check(failure.Current.glassMarbles == 16 && failure.Current.story.clearedStageIds.Count == 1, "重试成功仍只发一次首通");
        var stock = Fresh(work, "npc-stock");
        RewriteSavePayload(stock.SavePath, p =>
        {
            foreach (var card in p["cards"].AsArray()) if (card["ownerId"].GetValue<string>() == "xiaoyu") card["ownerId"] = "player";
        }); stock.Reload();
        int oldCount = stock.Current.cards.Count, oldPlayerCount = stock.PlayerCards.Count;
        Check(stock.GetOpponentStakeForChallenge("story_01") == null, "空NPC库存只读预览不虚构押片");
        var prepared = stock.PrepareChallengeStake("story_01");
        Check(prepared.CanStake && prepared.ownerId == "xiaoyu" && prepared.Definition.rarity == CardRarity.Common && prepared.history[0].reason == CardTransferReason.Purchase && stock.Current.cards.Count == oldCount + 1 && stock.PlayerCards.Count == oldPlayerCount, "开局前补购具体普通实例，只进NPC库存不补玩家");
        revision = stock.Current.revision;
        Check(stock.PrepareChallengeStake("story_01").instanceId == prepared.instanceId && stock.Current.revision == revision && stock.Current.cards.Count == oldCount + 1, "反复预备同关不复制库存或写档");
        Reject(() => stock.RestockOwner("xiaoyu"), "有可押普通片时不允许多次补购");
        var stockedMatch = stock.CreateStoryMatch("story_01", stock.PlayerCards.First(c => c.CanStake).instanceId, null);
        Check(stockedMatch.npcStakeIds.Single() == prepared.instanceId, "正式开局押展示过的具体补购实例");
        stock.ResolvePendingMatch(stockedMatch.matchId, MatchOutcome.PlayerWin, 1);
        var restocked = stock.RestockOwner("xiaoyu");
        Check(restocked.instanceId != prepared.instanceId && stock.GetInstance(prepared.instanceId).ownerId == "player" && stock.Current.cards.Count == oldCount + 2, "NPC后续显式补购新common，不覆盖赢走的旧实例");

        var bounds = Fresh(work, "real-boundaries");
        // 购买足够的真实实例，测试1..12赌注的合法上界及13张拒绝。
        RewriteSavePayload(bounds.SavePath, p => p["glassMarbles"] = 1000); bounds.Reload();
        while (bounds.PlayerCards.Count(c => c.CanStake) < 13) bounds.ExecuteTrade(bounds.GetTradeQuote(TradeKind.Buy, "buy_pog_01"));
        var player12 = bounds.PlayerCards.Where(c => c.CanStake).Take(12).Select(c => c.instanceId).ToArray();
        var npc12 = new List<string>();
        // 向校门小雨主动卖入片，再由固定清单/原NPC库存组成；只读fixture增加合法NPC普通实例。
        RewriteSavePayload(bounds.SavePath, p =>
        {
            var cards = p["cards"].AsArray(); var template = cards.First(c => c["ownerId"].GetValue<string>() == "xiaoyu").DeepClone();
            for (int i = 0; i < 6; i++) { var added = template.DeepClone(); added["instanceId"] = "boundary_npc_" + i; cards.Add(added); }
        }); bounds.Reload();
        npc12.AddRange(bounds.NpcCards("xiaoyu").Where(c => c.CanStake).Take(12).Select(c => c.instanceId));
        revision = bounds.Current.revision;
        Reject(() => bounds.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, bounds.PlayerCards.Where(c => c.CanStake).Take(13).Select(c => c.instanceId).ToArray(), bounds.NpcCards("xiaoyu").Take(13).Select(c => c.instanceId).ToArray()), "真实赌注13张超出上界拒绝");
        Check(bounds.Current.revision == revision, "超界赌注不写档");
        var twelve = bounds.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, player12, npc12);
        Check(twelve.playerStakeIds.Count == 12 && twelve.npcStakeIds.Count == 12, "真实赌注12张合法边界");
        bounds.ForfeitPendingMatch();
        var overflow = Fresh(work, "wallet-overflow");
        RewriteSavePayload(overflow.SavePath, p => p["glassMarbles"] = int.MaxValue); overflow.Reload();
        Reject(() => overflow.GetTradeQuote(TradeKind.Sell, "shop", overflow.PlayerCards[0].instanceId), "钱包int最大值不能溢出变负");
        var zeroErrand = overflow.AcceptErrand("errand_basket"); overflow.VisitLocation("old_tree"); revision = overflow.Current.revision;
        Reject(() => overflow.CompleteErrand(zeroErrand.completionId), "跑腿奖励超钱包边界整次拒绝");
        Check(overflow.Current.revision == revision && overflow.Current.activeErrand != null, "跑腿溢出保留待交付任务，未推进时段");
        var negative = Fresh(work, "negative-wallet"); RewriteSavePayload(negative.SavePath, p => p["glassMarbles"] = -1);
        Reject(() => new SaveRepository(Path.GetDirectoryName(negative.SavePath)).LoadOrCreate(), "合法checksum的负余额拒绝业务校验");
        var mismatch = Fresh(work, "weather-mismatch"); var wrong = mismatch.VisitLocation("home"); wrong.frictionMultiplier = .2f;
        Reject(() => mismatch.CreateStoryMatch("story_01", mismatch.PlayerCards.First(c => c.CanStake).instanceId, wrong), "冻结环境不能伪造摩擦系数");
        wrong = mismatch.Current.progress.environment; wrong.locationId = "square";
        Reject(() => mismatch.CreateStoryMatch("story_01", mismatch.PlayerCards.First(c => c.CanStake).instanceId, wrong), "剧情冻结环境与场地必须一致");
        var dateLimit = Fresh(work, "date-overflow");
        RewriteSavePayload(dateLimit.SavePath, p => { p["progress"]["day"] = int.MaxValue; p["progress"]["period"] = 3; p["progress"]["environment"]["day"] = int.MaxValue; p["progress"]["environment"]["period"] = 3; }); dateLimit.Reload(); revision = dateLimit.Current.revision;
        Reject(() => dateLimit.Rest(), "最大日期傍晚休息不能整数回绕");
        Check(dateLimit.Current.revision == revision && dateLimit.Current.progress.day == int.MaxValue && dateLimit.Current.progress.period == DayPeriod.Evening, "日期溢出整个操作未提交");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        checks++;
        Console.WriteLine("✓ " + description);
    }

    private static void Reject(Action action, string description)
    {
        bool rejected = false;
        try { action(); }
        catch (InvalidOperationException) { rejected = true; }
        catch (ArgumentException) { rejected = true; }
        catch (InvalidDataException) { rejected = true; }
        catch (IOException) { rejected = true; }
        Check(rejected, description);
    }

    private static void RewriteSavePayload(string savePath, Action<JsonObject> edit)
    {
        // 模拟 schema 1 的旧存档或有效封装中的非法业务字段；重新计算校验和，确保测到业务校验。
        var envelope = JsonNode.Parse(File.ReadAllText(savePath)).AsObject();
        var payload = JsonNode.Parse(envelope["payload"].GetValue<string>()).AsObject();
        edit(payload);
        string payloadText = payload.ToJsonString();
        envelope["payload"] = payloadText;
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payloadText));
            envelope["checksum"] = string.Concat(hash.Select(value => value.ToString("x2")));
        }
        File.WriteAllText(savePath, envelope.ToJsonString(), new UTF8Encoding(false));
    }
}
