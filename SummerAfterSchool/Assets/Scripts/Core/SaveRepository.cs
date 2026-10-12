using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Childhood
{
    [Serializable]
    public sealed class CardHistoryEntry
    {
        public string utc;
        public string matchId;
        public string locationId;
        public string fromOwnerId;
        public string toOwnerId;
        public string description;
        public CardTransferReason reason;
        public string transactionId;
    }

    [Serializable]
    public sealed class CardInstance
    {
        public string instanceId;
        public string definitionId;
        public string ownerId;
        public string originalOwnerId;
        public string origin;
        public string sourceLocationId;
        public string sourceMatchId;
        public string acquiredUtc;
        public string lastPlayedUtc;
        public int wins;
        public int losses;
        public int draws;
        public float wear;
        public bool treasured;
        public bool isFoundation;
        public List<CardHistoryEntry> history = new List<CardHistoryEntry>();
        public CardDefinition Definition { get { return GameCatalog.GetCard(definitionId); } }
        public bool CanTrade { get { return true; } }
        public bool CanStake { get { return !isFoundation && !treasured; } }
        public string WearDescription { get { return wear < .12f ? "还很崭新" : wear < .36f ? "边缘有一点旧" : wear < .65f ? "陪你打过不少场" : "满是夏天的痕迹"; } }
    }

    [Serializable]
    public sealed class NpcProgress
    {
        public string npcId;
        public int matches;
        public int wins;
        public int losses;
        public int recoveredCards;
        public int friendship;
    }

    [Serializable]
    public sealed class GameProgress
    {
        public int day = 1;
        public DayPeriod period = DayPeriod.Afternoon;
        public string currentLocationId = "home";
        public int totalMatches;
        public int stakedMatches;
        public int practiceMatches;
        public int wins;
        public int losses;
        public int draws;
        public int experience;
        public int reputation;
        public int recoveredCards;
        public int marbleRounds;
        public int marbleBestScore;
        public bool tutorialComplete;
        // 新版分步教学的完成标记。旧版 tutorialComplete 保留用于存档兼容，不能互相推导。
        public bool guidedPracticeCompleted;
        public List<string> visitedLocationIds = new List<string>();
        public List<string> unlockedPlayModes = new List<string>();
        public List<NpcProgress> npcs = new List<NpcProgress>();
        public EnvironmentContext environment;
        public List<LocationVisit> locationVisits = new List<LocationVisit>();
    }

    [Serializable]
    public sealed class LocationVisit { public string locationId; public int count; }

    [Serializable]
    public sealed class StoryProgress
    {
        public List<string> clearedStageIds = new List<string>();
        public List<string> defeatedBossIds = new List<string>();
    }

    [Serializable]
    public sealed class TournamentRound
    {
        public int roundIndex;
        public string stageId;
        public string npcId;
        public string locationId;
        public RuleKind rule;
        public int layout;
        public int randomSeed;
        public string matchId;
        public bool completed;
        public MatchOutcome outcome;
    }

    [Serializable]
    public sealed class TournamentBracketMatch
    {
        public int roundIndex;
        public string leftOwnerId;
        public string rightOwnerId;
        public string winnerOwnerId;
    }

    [Serializable]
    public sealed class TournamentRun
    {
        public string runId;
        public int randomSeed;
        public int roundIndex;
        public bool active;
        public bool champion;
        public List<string> participants = new List<string>();
        public List<TournamentRound> rounds = new List<TournamentRound>();
        public List<TournamentBracketMatch> bracket = new List<TournamentBracketMatch>();
    }

    [Serializable]
    public sealed class PendingErrand
    {
        public string completionId;
        public string errandId;
        public string destinationLocationId;
        public bool destinationVisited;
        public int acceptedDay;
    }

    [Serializable]
    public sealed class TradeQuote
    {
        public string quoteId;
        public long expectedRevision;
        public TradeKind kind;
        public string offerId;
        public string playerCardId;
        public string npcCardId;
        public string npcId;
        public string cardDefinitionId;
        public int marbleDelta;
        public bool requiresTreasuredConfirmation;
        public bool isFoundation;
    }

    [Serializable]
    public sealed class TradeReceipt
    {
        public string transactionId;
        public string quoteId;
        public TradeKind kind;
        public string offerId;
        public string playerCardId;
        public string npcCardId;
        public string npcId;
        public int day;
        public int marbleDelta;
        public int balanceAfter;
        public string finishedUtc;
        public string quoteFingerprint;
        public List<string> gainedCardIds = new List<string>();
        public List<string> lostCardIds = new List<string>();
    }

    [Serializable]
    public sealed class ActivityReceipt
    {
        public string transactionId;
        public string errandId;
        public int marbleDelta;
        public int balanceAfter;
        public int dayAfter;
        public DayPeriod periodAfter;
        public string finishedUtc;
    }

    [Serializable]
    public sealed class MatchCheckpoint
    {
        // 玩法层可把圆片的位置、朝向、回合 RNG 状态存入这段 JSON。
        public string boardStateJson;
        public int currentTurn;
        public int playerScore;
        public int npcScore;
        public int shotsUsed;
        public bool isPlayerTurn = true;
        public string savedUtc;
    }

    [Serializable]
    public sealed class PendingMatch
    {
        public string matchId;
        public string npcId;
        public string locationId;
        public RuleKind rule;
        public bool isReal;
        public bool soloPractice;
        public bool paused;
        public int randomSeed;
        public string startedUtc;
        public List<string> playerStakeIds = new List<string>();
        public List<string> npcStakeIds = new List<string>();
        public MatchCheckpoint checkpoint = new MatchCheckpoint();
        public MatchRoute route;
        public string stageId;
        public string tournamentRunId;
        public int layout;
        public EnvironmentContext environment;
    }

    [Serializable]
    public sealed class MatchSettlement
    {
        public string matchId;
        public string npcId;
        public string locationId;
        public RuleKind rule;
        public bool isReal;
        public MatchOutcome outcome;
        public int shotsUsed;
        public string finishedUtc;
        public List<string> playerStakeIds = new List<string>();
        public List<string> npcStakeIds = new List<string>();
        public List<string> gainedCardIds = new List<string>();
        public List<string> lostCardIds = new List<string>();
        public List<string> recoveredCardIds = new List<string>();
        public int experienceGained;
        public MatchRoute route;
        public string stageId;
        public string tournamentRunId;
        public bool firstClear;
        public int rewardMarbles;
        public int balanceAfter;
        public int dayAfter;
        public DayPeriod periodAfter;
        public List<string> rewardCardIds = new List<string>();
        public string defeatedBossId;
    }

    [Serializable]
    public sealed class GameSave
    {
        public int schemaVersion = SaveRepository.SchemaVersion;
        public long revision;
        public string profileId;
        public string createdUtc;
        public string updatedUtc;
        public List<CardInstance> cards = new List<CardInstance>();
        public GameProgress progress = new GameProgress();
        public PendingMatch pendingMatch;
        public List<MatchSettlement> settlements = new List<MatchSettlement>();
        public int glassMarbles = 12;
        public StoryProgress story = new StoryProgress();
        public TournamentRun tournament;
        public PendingErrand activeErrand;
        public List<TradeReceipt> transactions = new List<TradeReceipt>();
        public List<ActivityReceipt> activities = new List<ActivityReceipt>();
    }

    /// <summary>
    /// 所有写操作先构造副本，再校验、写入并提交；调用方读到的对象也是副本。
    /// 真实对局先持久化约定赌注，结算只移动这些实例，重复结算返回同一张收据。
    /// </summary>
    public sealed class SaveRepository
    {
        public const int SchemaVersion = 2;
        private readonly object sync = new object();
        private GameSave current;
        private readonly string directory;
        private readonly string legacyDirectory;
        public string SavePath { get; private set; }
        public string BackupPath { get; private set; }
        public string LastRecoveryMessage { get; private set; }

        [Serializable]
        private sealed class SaveEnvelope
        {
            public int envelopeVersion = 1;
            public string checksum;
            public string payload;
        }

        public SaveRepository(string saveDirectory = null, string legacySaveDirectory = null)
        {
            directory = Path.GetFullPath(string.IsNullOrEmpty(saveDirectory) ? Path.Combine(Application.persistentDataPath, "SummerAfterSchool-v2") : saveDirectory);
            legacyDirectory = !string.IsNullOrEmpty(legacySaveDirectory) ? Path.GetFullPath(legacySaveDirectory) : string.IsNullOrEmpty(saveDirectory) ? Path.Combine(Application.persistentDataPath, "SummerAfterSchool") : null;
            if (legacyDirectory != null && string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar), legacyDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("新版与旧版存档目录必须独立。");
            SavePath = Path.Combine(directory, "collection.json");
            BackupPath = Path.Combine(directory, "collection.backup.json");
        }

        public GameSave Current { get { lock (sync) { EnsureLoaded(); return Copy(current); } } }
        public bool HasPendingMatch { get { lock (sync) { EnsureLoaded(); return current.pendingMatch != null; } } }
        public bool GuidedPracticeCompleted { get { lock (sync) { EnsureLoaded(); return current.progress != null && current.progress.guidedPracticeCompleted; } } }
        public IReadOnlyList<CardInstance> PlayerCards { get { return OwnedCards(GameCatalog.PlayerOwnerId); } }

        public GameSave LoadOrCreate()
        {
            lock (sync)
            {
                NormalizeNullableState(current);
                if (current != null) return Copy(current);
                Directory.CreateDirectory(directory);
                GameSave loaded;
                string problem;
                if (TryRead(SavePath, out loaded, out problem))
                {
                    current = loaded;
                    if (current.schemaVersion == 1) Commit(MigrateLegacy(current));
                    return Copy(current);
                }
                string primaryProblem = problem;
                if (TryRead(BackupPath, out loaded, out problem))
                {
                    LastRecoveryMessage = "主存档不可读取，已从备份恢复。";
                    PreserveUnreadablePrimary();
                    current = loaded;
                    var recovered = current.schemaVersion == 1 ? MigrateLegacy(current) : Copy(current);
                    Commit(recovered);
                    return Copy(current);
                }
                string backupProblem = problem;
                // 中断发生在首次原子写入之前时，临时文件仍可恢复。
                if (TryRead(SavePath + ".tmp", out loaded, out problem))
                {
                    LastRecoveryMessage = "已恢复上次中断的存档写入。";
                    PreserveUnreadablePrimary();
                    current = loaded;
                    var recovered = current.schemaVersion == 1 ? MigrateLegacy(current) : Copy(current);
                    Commit(recovered);
                    return Copy(current);
                }
                if (legacyDirectory != null && Directory.Exists(legacyDirectory))
                {
                    var legacyPrimary = Path.Combine(legacyDirectory, "collection.json");
                    var legacyBackup = Path.Combine(legacyDirectory, "collection.backup.json");
                    bool hasLegacy = File.Exists(legacyPrimary) || File.Exists(legacyBackup) || File.Exists(legacyPrimary + ".tmp");
                    if (hasLegacy)
                    {
                        if (!TryRead(legacyPrimary, out loaded, out problem) && !TryRead(legacyBackup, out loaded, out problem) && !TryRead(legacyPrimary + ".tmp", out loaded, out problem))
                            throw new InvalidDataException("旧版存档无法导入，原目录完全保留：" + problem);
                        current = loaded;
                        var imported = loaded.schemaVersion == 1 ? MigrateLegacy(loaded) : Copy(loaded);
                        Commit(imported);
                        LastRecoveryMessage = "已只读导入旧版收藏到独立新版目录，旧存档原样保留。";
                        return Copy(current);
                    }
                }
                if (File.Exists(SavePath) || File.Exists(BackupPath) || File.Exists(SavePath + ".tmp"))
                    throw new InvalidDataException("收藏存档和备份都无法读取，原文件已保留。主档：" + primaryProblem + "；备份：" + backupProblem);
                var created = CreateNewSave();
                Commit(created);
                return Copy(current);
            }
        }

        public GameSave Reload()
        {
            lock (sync) { current = null; LastRecoveryMessage = null; return LoadOrCreate(); }
        }

        public IReadOnlyList<CardInstance> OwnedCards(string ownerId)
        {
            lock (sync)
            {
                EnsureLoaded();
                var result = new List<CardInstance>();
                foreach (var card in current.cards) if (card.ownerId == ownerId) result.Add(Copy(card));
                return result;
            }
        }

        public IReadOnlyList<CardInstance> NpcCards(string npcId) { GameCatalog.GetNpc(npcId); return OwnedCards(npcId); }
        public IReadOnlyList<CardInstance> GetPlayerCards() { return PlayerCards; }

        public CardInstance GetInstance(string instanceId)
        {
            lock (sync) { EnsureLoaded(); return Copy(FindCard(current, instanceId)); }
        }

        public PendingMatch GetPendingMatch()
        {
            lock (sync) { EnsureLoaded(); return current.pendingMatch == null ? null : Copy(current.pendingMatch); }
        }

        public IReadOnlyList<CardInstance> LostPlayerCards
        {
            get
            {
                lock (sync)
                {
                    EnsureLoaded();
                    var result = new List<CardInstance>();
                    foreach (var card in current.cards)
                        if (card.ownerId != GameCatalog.PlayerOwnerId && WasLostByPlayer(card)) result.Add(Copy(card));
                    return result;
                }
            }
        }

        public bool IsCardLocked(string instanceId)
        {
            lock (sync) { EnsureLoaded(); return IsLocked(current, instanceId); }
        }

        public bool CanStake(string instanceId)
        {
            lock (sync)
            {
                EnsureLoaded();
                var card = FindCard(current, instanceId);
                return card.ownerId == GameCatalog.PlayerOwnerId && card.CanStake && !IsLocked(current, instanceId);
            }
        }

        public int RecordMarbleRound(int score)
        {
            if (score < 0 || score > 8) throw new ArgumentOutOfRangeException("score");
            int best = 0;
            Mutate(save => { save.progress.marbleRounds++; save.progress.marbleBestScore = Math.Max(save.progress.marbleBestScore, score); best = save.progress.marbleBestScore; });
            return best;
        }

        public void SetTreasured(string instanceId, bool treasured)
        {
            Mutate(save =>
            {
                var card = FindCard(save, instanceId);
                if (card.ownerId != GameCatalog.PlayerOwnerId) throw new InvalidOperationException("只能珍藏自己拥有的圆片。");
                if (IsLocked(save, instanceId)) throw new InvalidOperationException("这张片已进入约战，请先完成对局。");
                card.treasured = treasured;
            });
        }

        public PendingMatch CreatePendingMatch(string npcId, string locationId, RuleKind rule, IReadOnlyList<string> playerStakeIds, IReadOnlyList<string> npcStakeIds, bool isReal = true, int randomSeed = 0, bool soloPractice = false, int layout = 0, EnvironmentContext environment = null)
        {
            PendingMatch result = null;
            Mutate(save =>
            {
                GameCatalog.GetNpc(npcId);
                GameCatalog.GetLocation(locationId);
                ValidateRule(rule);
                ValidateLayout(layout);
                if (isReal && soloPractice) throw new InvalidOperationException("自己练手不能押片约战。");
                if (save.pendingMatch != null) throw new InvalidOperationException("已有未完成约战，请继续对局或认输后再开始。");
                ValidateStakes(save, playerStakeIds, GameCatalog.PlayerOwnerId, isReal);
                ValidateStakes(save, npcStakeIds, npcId, isReal);
                if (isReal)
                    foreach (var id in npcStakeIds)
                        foreach (var stage in CampaignCatalog.Stages)
                            if (stage.npcId == npcId && stage.rewardCardIndex >= 0 && FindCard(save, id).definitionId == GameCatalog.Cards[stage.rewardCardIndex].id && !CampaignCatalog.IsBossDefeated(save, npcId))
                                throw new InvalidOperationException("守擂人的招牌只在对应故事首次挑战时押出。");
                if (isReal && playerStakeIds.Count != npcStakeIds.Count) throw new InvalidOperationException("双方必须先约定相同数量的赌注片。");
                var match = new PendingMatch
                {
                    matchId = NewId("match"), npcId = npcId, locationId = locationId, rule = rule, isReal = isReal, soloPractice = soloPractice,
                    randomSeed = randomSeed == 0 ? NewSeed() : randomSeed, startedUtc = UtcNow(),
                    playerStakeIds = new List<string>(playerStakeIds), npcStakeIds = new List<string>(npcStakeIds),
                    checkpoint = new MatchCheckpoint { savedUtc = UtcNow() }, layout = layout,
                    environment = PrepareEnvironment(save, locationId, environment)
                };
                save.pendingMatch = match;
                Visit(save.progress, locationId);
                save.progress.environment = Copy(match.environment);
                result = Copy(match);
            });
            return result;
        }

        public PendingMatch CreatePracticeMatch(string npcId, string locationId, RuleKind rule, IReadOnlyList<string> playerCardIds, IReadOnlyList<string> npcCardIds, int randomSeed = 0, bool soloPractice = false, int layout = 0, EnvironmentContext environment = null)
        {
            return CreatePendingMatch(npcId, locationId, rule, playerCardIds, npcCardIds, false, randomSeed, soloPractice, layout, environment);
        }

        public PendingMatch CreatePracticeMatch(string npcId, string locationId, RuleKind rule, int randomSeed = 0, string playerCardId = null, bool soloPractice = false, int layout = 0, EnvironmentContext environment = null)
        {
            lock (sync)
            {
                EnsureLoaded();
                GameCatalog.GetNpc(npcId);
                CardInstance player = null;
                if (!string.IsNullOrEmpty(playerCardId)) player = FindCard(current, playerCardId);
                else
                {
                    foreach (var card in current.cards)
                        if (card.ownerId == GameCatalog.PlayerOwnerId && card.isFoundation) { player = card; break; }
                    if (player == null) player = FindFirstOwned(current, GameCatalog.PlayerOwnerId);
                }
                CardInstance npc = null;
                foreach (var card in current.cards) if (card.ownerId == npcId) { npc = card; break; }
                // 伙伴输空收藏也能借练习片切磋；借用片不进入收藏或交易记录。
                var npcCards = npc == null ? new string[0] : new[] { npc.instanceId };
                return CreatePracticeMatch(npcId, locationId, rule, new[] { player.instanceId }, npcCards, randomSeed, soloPractice, layout, environment);
            }
        }

        public void SaveMatchProgress(string matchId, MatchCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException("checkpoint");
            if (checkpoint.currentTurn < 0 || checkpoint.playerScore < 0 || checkpoint.npcScore < 0 || checkpoint.shotsUsed < 0)
                throw new ArgumentException("回合、分数和出手次数不可为负数。", "checkpoint");
            if (checkpoint.boardStateJson != null && checkpoint.boardStateJson.Length > 2 * 1024 * 1024)
                throw new ArgumentException("棋盘快照超过了 2 MB。", "checkpoint");
            Mutate(save =>
            {
                var match = RequirePending(save, matchId);
                if (checkpoint.shotsUsed < match.checkpoint.shotsUsed || checkpoint.currentTurn < match.checkpoint.currentTurn)
                    throw new InvalidOperationException("对局进度不能倒退。");
                match.checkpoint = Copy(checkpoint);
                match.checkpoint.savedUtc = UtcNow();
            });
        }

        public void UpdatePendingProgress(string matchId, string boardStateJson, int currentTurn, int playerScore, int npcScore, int shotsUsed, bool isPlayerTurn)
        {
            SaveMatchProgress(matchId, new MatchCheckpoint { boardStateJson = boardStateJson, currentTurn = currentTurn, playerScore = playerScore, npcScore = npcScore, shotsUsed = shotsUsed, isPlayerTurn = isPlayerTurn });
        }

        // 暂停保留双方片和全部进度；菜单仍会提示继续约战。
        public void AbortPendingMatch()
        {
            Mutate(save => { if (save.pendingMatch != null) save.pendingMatch.paused = true; });
        }

        public PendingMatch ResumePendingMatch()
        {
            PendingMatch result = null;
            Mutate(save =>
            {
                if (save.pendingMatch == null) throw new InvalidOperationException("没有待继续的约战。");
                save.pendingMatch.paused = false;
                result = Copy(save.pendingMatch);
            });
            return result;
        }

        public MatchSettlement ForfeitPendingMatch()
        {
            lock (sync)
            {
                EnsureLoaded();
                if (current.pendingMatch == null) throw new InvalidOperationException("没有待结算的约战。");
                return ResolvePendingMatch(current.pendingMatch.matchId, MatchOutcome.NpcWin, current.pendingMatch.checkpoint.shotsUsed);
            }
        }

        public MatchSettlement ResolvePendingMatch(string matchId, MatchOutcome outcome, int shotsUsed = 0)
        {
            lock (sync)
            {
                EnsureLoaded();
                foreach (var old in current.settlements) if (old.matchId == matchId) return Copy(old);
                if (!Enum.IsDefined(typeof(MatchOutcome), outcome)) throw new ArgumentException("未知对局结果。", "outcome");
                if (shotsUsed < 0) throw new ArgumentOutOfRangeException("shotsUsed");
                var next = Copy(current);
                var match = RequirePending(next, matchId);
                shotsUsed = Math.Max(shotsUsed, match.checkpoint.shotsUsed);
                ValidateStakes(next, match.playerStakeIds, GameCatalog.PlayerOwnerId, match.isReal);
                ValidateStakes(next, match.npcStakeIds, match.npcId, match.isReal);
                var receipt = new MatchSettlement
                {
                    matchId = matchId, npcId = match.npcId, locationId = match.locationId, rule = match.rule,
                    isReal = match.isReal, outcome = outcome, shotsUsed = shotsUsed, finishedUtc = UtcNow(),
                    playerStakeIds = new List<string>(match.playerStakeIds), npcStakeIds = new List<string>(match.npcStakeIds),
                    experienceGained = (match.isReal ? 14 : 6) + (outcome == MatchOutcome.PlayerWin ? 10 : 4),
                    route = match.route, stageId = match.stageId, tournamentRunId = match.tournamentRunId
                };
                UpdateCardStats(next, match.playerStakeIds, outcome, true, shotsUsed);
                UpdateCardStats(next, match.npcStakeIds, outcome, false, shotsUsed);
                if (match.isReal && outcome == MatchOutcome.PlayerWin)
                {
                    foreach (var id in match.npcStakeIds)
                    {
                        var card = FindCard(next, id);
                        if (WasLostByPlayer(card)) receipt.recoveredCardIds.Add(id);
                        Transfer(card, GameCatalog.PlayerOwnerId, match, "约战获胜，带回了这张圆片。");
                        receipt.gainedCardIds.Add(id);
                    }
                }
                else if (match.isReal && outcome == MatchOutcome.NpcWin)
                {
                    foreach (var id in match.playerStakeIds)
                    {
                        Transfer(FindCard(next, id), match.npcId, match, "约战落败，圆片暂时留在伙伴那里，随时可以约战夺回。");
                        receipt.lostCardIds.Add(id);
                    }
                }
                var progress = next.progress;
                progress.totalMatches++;
                if (match.isReal) progress.stakedMatches++; else progress.practiceMatches++;
                if (outcome == MatchOutcome.PlayerWin) { progress.wins++; progress.reputation += match.isReal ? 3 : 1; }
                else if (outcome == MatchOutcome.NpcWin) { progress.losses++; progress.reputation = Math.Max(0, progress.reputation - (match.isReal ? 1 : 0)); }
                else progress.draws++;
                progress.experience += receipt.experienceGained;
                progress.recoveredCards += receipt.recoveredCardIds.Count;
                progress.tutorialComplete = true;
                var npc = GetNpcProgress(progress, match.npcId);
                npc.matches++;
                npc.friendship += outcome == MatchOutcome.Draw ? 1 : 2;
                npc.recoveredCards += receipt.recoveredCardIds.Count;
                if (outcome == MatchOutcome.PlayerWin) npc.wins++;
                else if (outcome == MatchOutcome.NpcWin) npc.losses++;
                ApplyCampaignSettlement(next, match, receipt);
                AdvancePeriod(next);
                receipt.balanceAfter = next.glassMarbles;
                receipt.dayAfter = next.progress.day;
                receipt.periodAfter = next.progress.period;
                next.settlements.Add(receipt);
                next.pendingMatch = null;
                Commit(next);
                return Copy(receipt);
            }
        }

        public MatchSettlement RecordPracticeMatch(string npcId, string locationId, RuleKind rule, MatchOutcome outcome, int shotsUsed = 0)
        {
            if (!Enum.IsDefined(typeof(MatchOutcome), outcome)) throw new ArgumentException("未知对局结果。", "outcome");
            if (shotsUsed < 0) throw new ArgumentOutOfRangeException("shotsUsed");
            lock (sync)
            {
                EnsureLoaded();
                if (current.pendingMatch != null)
                {
                    var pending = current.pendingMatch;
                    if (pending.isReal || pending.npcId != npcId || pending.locationId != locationId || pending.rule != rule)
                        throw new InvalidOperationException("请先完成当前约战，再记录其他练习。");
                    return ResolvePendingMatch(pending.matchId, outcome, shotsUsed);
                }
                var match = CreatePracticeMatch(npcId, locationId, rule);
                return ResolvePendingMatch(match.matchId, outcome, shotsUsed);
            }
        }

        public MatchSettlement GetSettlement(string matchId)
        {
            lock (sync)
            {
                EnsureLoaded();
                foreach (var settlement in current.settlements) if (settlement.matchId == matchId) return Copy(settlement);
                return null;
            }
        }

        public EnvironmentContext VisitLocation(string locationId)
        {
            GameCatalog.GetLocation(locationId);
            EnvironmentContext result = null;
            Mutate(save =>
            {
                Visit(save.progress, locationId);
                LocationVisit visit = null;
                foreach (var item in save.progress.locationVisits) if (item.locationId == locationId) { visit = item; break; }
                if (visit == null) { visit = new LocationVisit { locationId = locationId }; save.progress.locationVisits.Add(visit); }
                var variant = visit.count % 3;
                if (visit.count == int.MaxValue) throw new InvalidOperationException("重访次数超出可保存范围。");
                visit.count++;
                var previous = save.progress.environment;
                var seed = previous == null ? NewSeed() : previous.weatherSeed;
                var weather = previous == null ? WeatherForSeed(seed) : previous.weather;
                save.progress.environment = BuildEnvironment(locationId, variant, save.progress.day, save.progress.period, weather, seed, 2);
                if (save.activeErrand != null && save.activeErrand.destinationLocationId == locationId) save.activeErrand.destinationVisited = true;
                result = Copy(save.progress.environment);
            });
            return result;
        }

        public void AdvanceTime() { Rest(); }
        public EnvironmentContext Rest()
        {
            EnvironmentContext result = null;
            Mutate(save => { RequireNoPending(save); AdvancePeriod(save); result = Copy(save.progress.environment); });
            return result;
        }
        public void SetTutorialComplete(bool complete = true) { Mutate(save => save.progress.tutorialComplete = complete); }
        public void SetGuidedPracticeCompleted(bool complete = true) { Mutate(save => save.progress.guidedPracticeCompleted = complete); }
        public void Flush() { lock (sync) { EnsureLoaded(); Commit(Copy(current)); } }
        // 保留旧 API，但缺片不再免费补发，空收藏是合法状态。
        public void EnsurePlayableCollection() { lock (sync) { EnsureLoaded(); } }

        public bool IsStoryStageUnlocked(string stageId)
        {
            lock (sync) { EnsureLoaded(); return StageUnlocked(current, CampaignCatalog.GetStage(stageId)); }
        }

        public CardInstance GetOpponentStakeForChallenge(string stageId)
        {
            lock (sync)
            {
                EnsureLoaded();
                ChallengeDefinition stage = null;
                foreach (var item in CampaignCatalog.TournamentStages) if (item.id == stageId) { stage = item; break; }
                if (stage == null) stage = CampaignCatalog.GetStage(stageId);
                try { return Copy(FindChallengeStake(current, stage)); }
                catch (InvalidOperationException) { return null; }
            }
        }

        public CardInstance PrepareChallengeStake(string stageId)
        {
            lock (sync)
            {
                EnsureLoaded();
                ChallengeDefinition stage = null;
                foreach (var item in CampaignCatalog.TournamentStages) if (item.id == stageId) { stage = item; break; }
                if (stage == null) stage = CampaignCatalog.GetStage(stageId);
                try { return Copy(FindChallengeStake(current, stage)); }
                catch (InvalidOperationException)
                {
                    var next = Copy(current);
                    EnsureNpcChallengeStock(next, stage);
                    var result = Copy(FindChallengeStake(next, stage));
                    Commit(next);
                    return result;
                }
            }
        }

        public PendingMatch CreateStoryMatch(string stageId, string playerCardId, EnvironmentContext environment)
        {
            PendingMatch result = null;
            Mutate(save =>
            {
                var stage = CampaignCatalog.GetStage(stageId);
                if (!StageUnlocked(save, stage)) throw new InvalidOperationException("请先完成上一关，再挑战这里的故事。");
                EnsureNpcChallengeStock(save, stage);
                var opponent = FindChallengeStake(save, stage);
                result = CreateChallengeMatch(save, stage, playerCardId, opponent.instanceId, environment, MatchRoute.Story, null, NewSeed());
            });
            return result;
        }

        public CardInstance RestockOwner(string npcId)
        {
            CardInstance result = null;
            Mutate(save =>
            {
                RequireNoPending(save);
                var npc = GameCatalog.GetNpc(npcId);
                foreach (var card in save.cards) if (card.ownerId == npcId && card.CanStake && (card.Definition.artIndex < 30 || card.Definition.artIndex > 33)) throw new InvalidOperationException("伙伴还有可押普通片，不需要补购。");
                result = Copy(PurchaseNpcCommon(save, npc));
            });
            return result;
        }

        public TournamentRun EnterTournament()
        {
            TournamentRun result = null;
            Mutate(save =>
            {
                RequireNoPending(save);
                if (save.tournament != null && save.tournament.active) throw new InvalidOperationException("本届杯赛还没有结束，请继续当前轮次。");
                bool hasStake = false;
                foreach (var card in save.cards) if (card.ownerId == GameCatalog.PlayerOwnerId && card.CanStake) { hasStake = true; break; }
                if (!hasStake) throw new InvalidOperationException("杯赛要用真片。先跑腿挣玻璃珠，再去小卖部买一张。");
                var run = new TournamentRun { runId = NewId("cup"), randomSeed = NewSeed(), active = true };
                // 固定八人签表：玩家路径为阿月、桃桃、阿砚，另外四位伙伴也有真实席位。
                run.participants.AddRange(new[] { "player", "ayue", "taotao", "doudou", "ayan", "xiaoyu", "dazhi", "linlin" });
                var rng = new System.Random(run.randomSeed);
                foreach (var stage in CampaignCatalog.TournamentStages)
                    run.rounds.Add(new TournamentRound { roundIndex = run.rounds.Count, stageId = stage.id, npcId = stage.npcId, locationId = stage.locationId, rule = stage.rule, layout = stage.layout, randomSeed = rng.Next(1, int.MaxValue) });
                run.bracket.Add(new TournamentBracketMatch { roundIndex = 0, leftOwnerId = "player", rightOwnerId = "ayue" });
                run.bracket.Add(new TournamentBracketMatch { roundIndex = 0, leftOwnerId = "taotao", rightOwnerId = "doudou", winnerOwnerId = "taotao" });
                run.bracket.Add(new TournamentBracketMatch { roundIndex = 0, leftOwnerId = "ayan", rightOwnerId = "xiaoyu", winnerOwnerId = "ayan" });
                var otherWinner = rng.Next(2) == 0 ? "dazhi" : "linlin";
                run.bracket.Add(new TournamentBracketMatch { roundIndex = 0, leftOwnerId = "dazhi", rightOwnerId = "linlin", winnerOwnerId = otherWinner });
                run.bracket.Add(new TournamentBracketMatch { roundIndex = 1, leftOwnerId = "player", rightOwnerId = "taotao" });
                run.bracket.Add(new TournamentBracketMatch { roundIndex = 1, leftOwnerId = "ayan", rightOwnerId = otherWinner, winnerOwnerId = "ayan" });
                run.bracket.Add(new TournamentBracketMatch { roundIndex = 2, leftOwnerId = "player", rightOwnerId = "ayan" });
                save.tournament = run;
                result = Copy(run);
            });
            return result;
        }

        public PendingMatch CreateTournamentMatch(string runId, string playerCardId, EnvironmentContext environment)
        {
            PendingMatch result = null;
            Mutate(save =>
            {
                var run = save.tournament;
                if (run == null || !run.active || run.runId != runId || run.roundIndex < 0 || run.roundIndex > 2) throw new InvalidOperationException("这届杯赛没有待开始的轮次。");
                var frozen = run.rounds[run.roundIndex];
                var stage = CampaignCatalog.GetTournamentStage(run.roundIndex);
                if (frozen.completed || !string.IsNullOrEmpty(frozen.matchId)) throw new InvalidOperationException("这一轮已经开局，请继续已有对局。");
                EnsureNpcChallengeStock(save, stage);
                var opponent = FindChallengeStake(save, stage);
                result = CreateChallengeMatch(save, stage, playerCardId, opponent.instanceId, environment, MatchRoute.Tournament, runId, frozen.randomSeed);
                frozen.matchId = result.matchId;
            });
            return result;
        }

        public TradeQuote GetTradeQuote(TradeKind kind, string offerId, string playerCardId = null)
        {
            lock (sync) { EnsureLoaded(); return BuildTradeQuote(current, kind, offerId, playerCardId, NewId("trade")); }
        }

        public TradeReceipt ExecuteTrade(TradeQuote quote, bool confirmTreasured = false)
        {
            if (quote == null || string.IsNullOrEmpty(quote.quoteId)) throw new ArgumentException("交易报价缺失。");
            lock (sync)
            {
                EnsureLoaded();
                var fingerprint = QuoteFingerprint(quote);
                foreach (var old in current.transactions)
                    if (old.quoteId == quote.quoteId)
                    {
                        if (old.quoteFingerprint != fingerprint) throw new InvalidOperationException("不能用同一个交易编号替换另一份报价。");
                        return Copy(old);
                    }
                if (quote.expectedRevision != current.revision) throw new InvalidOperationException("收藏或余额已有变化，请重新查看本次报价。");
                var expected = BuildTradeQuote(current, quote.kind, quote.offerId, quote.playerCardId, quote.quoteId);
                if (QuoteFingerprint(expected) != fingerprint) throw new InvalidOperationException("交易报价与当前固定清单不一致。");
                if (expected.requiresTreasuredConfirmation && !confirmTreasured) throw new InvalidOperationException("这次交易包含珍藏片，请明确确认本次转手。");
                var next = Copy(current);
                var receipt = new TradeReceipt { transactionId = quote.quoteId, quoteId = quote.quoteId, kind = quote.kind, offerId = quote.offerId, playerCardId = quote.playerCardId, npcCardId = quote.npcCardId, npcId = quote.npcId, day = next.progress.day, marbleDelta = quote.marbleDelta, finishedUtc = UtcNow(), quoteFingerprint = fingerprint };
                AddMarbles(next, quote.marbleDelta);
                if (quote.kind == TradeKind.Buy)
                {
                    var definition = GameCatalog.GetCard(quote.cardDefinitionId);
                    var bought = CreateInstance(definition.artIndex, GameCatalog.PlayerOwnerId, "shop", "在小卖部用玻璃珠买来的圆片。", quote.isFoundation);
                    bought.history[0].reason = CardTransferReason.Purchase;
                    bought.history[0].transactionId = quote.quoteId;
                    next.cards.Add(bought);
                    receipt.gainedCardIds.Add(bought.instanceId);
                }
                else
                {
                    var outgoing = FindCard(next, quote.playerCardId);
                    TransferForTrade(outgoing, quote.npcId, quote.quoteId, next.progress.currentLocationId, quote.kind == TradeKind.Sell ? "用这张片换来玻璃珠，主动售出。" : "和伙伴按固定清单一换一。", CardTransferReason.Trade);
                    receipt.lostCardIds.Add(outgoing.instanceId);
                    if (quote.kind == TradeKind.Swap)
                    {
                        var incoming = FindCard(next, quote.npcCardId);
                        TransferForTrade(incoming, GameCatalog.PlayerOwnerId, quote.quoteId, next.progress.currentLocationId, "伙伴固定清单换来的圆片。", CardTransferReason.Trade);
                        receipt.gainedCardIds.Add(incoming.instanceId);
                    }
                }
                receipt.balanceAfter = next.glassMarbles;
                next.transactions.Add(receipt);
                Commit(next);
                return Copy(receipt);
            }
        }

        public PendingErrand AcceptErrand(string errandId)
        {
            PendingErrand result = null;
            Mutate(save =>
            {
                RequireNoPending(save);
                var errand = CampaignCatalog.GetErrand(errandId);
                if (save.activeErrand != null) throw new InvalidOperationException("先送完手里的东西，再接下一次跑腿。");
                save.activeErrand = new PendingErrand { completionId = NewId("errand"), errandId = errandId, destinationLocationId = errand.destinationLocationId, acceptedDay = save.progress.day };
                result = Copy(save.activeErrand);
            });
            return result;
        }

        public ActivityReceipt CompleteErrand(string completionId)
        {
            lock (sync)
            {
                EnsureLoaded();
                foreach (var old in current.activities) if (old.transactionId == completionId) return Copy(old);
                var next = Copy(current);
                RequireNoPending(next);
                var pending = next.activeErrand;
                if (pending == null || pending.completionId != completionId) throw new InvalidOperationException("这次跑腿没有接取，或编号不匹配。");
                if (!pending.destinationVisited || next.progress.currentLocationId != pending.destinationLocationId) throw new InvalidOperationException("接取后先到目的地，再交付跑腿物品。");
                var errand = CampaignCatalog.GetErrand(pending.errandId);
                AddMarbles(next, errand.rewardMarbles);
                AdvancePeriod(next);
                var receipt = new ActivityReceipt { transactionId = completionId, errandId = pending.errandId, marbleDelta = errand.rewardMarbles, balanceAfter = next.glassMarbles, dayAfter = next.progress.day, periodAfter = next.progress.period, finishedUtc = UtcNow() };
                next.activeErrand = null;
                next.activities.Add(receipt);
                Commit(next);
                return Copy(receipt);
            }
        }

        private static PendingMatch CreateChallengeMatch(GameSave save, ChallengeDefinition stage, string playerCardId, string npcCardId, EnvironmentContext environment, MatchRoute route, string runId, int seed)
        {
            RequireNoPending(save);
            ValidateStakes(save, new[] { playerCardId }, GameCatalog.PlayerOwnerId, true);
            ValidateStakes(save, new[] { npcCardId }, stage.npcId, true);
            var match = new PendingMatch { matchId = NewId("match"), npcId = stage.npcId, locationId = stage.locationId, rule = stage.rule, isReal = true, randomSeed = seed, startedUtc = UtcNow(), playerStakeIds = new List<string> { playerCardId }, npcStakeIds = new List<string> { npcCardId }, route = route, stageId = stage.id, tournamentRunId = runId, layout = stage.layout, environment = PrepareEnvironment(save, stage.locationId, environment), checkpoint = new MatchCheckpoint { savedUtc = UtcNow() } };
            save.pendingMatch = match;
            Visit(save.progress, stage.locationId);
            save.progress.environment = Copy(match.environment);
            return Copy(match);
        }

        private static bool StageUnlocked(GameSave save, ChallengeDefinition stage)
        {
            foreach (var required in stage.requiredStageIds) if (!save.story.clearedStageIds.Contains(required)) return false;
            return true;
        }

        private static CardInstance FindChallengeStake(GameSave save, ChallengeDefinition stage)
        {
            var boss = BossForNpc(stage.npcId);
            if (boss != null && !CampaignCatalog.IsBossDefeated(save, boss.npcId))
            {
                var signature = GameCatalog.Cards[boss.rewardCardIndex].id;
                foreach (var card in save.cards) if (card.definitionId == signature && card.ownerId == stage.npcId && card.CanStake && !IsLocked(save, card.instanceId)) return card;
                throw new InvalidOperationException("这位守擂人的唯一招牌不在可押收藏里，请先归还或完成相关约战。");
            }
            // 杯赛及已首通的关卡先使用普通收藏，保留首次故事的唯一招牌。
            foreach (var card in save.cards) if (card.ownerId == stage.npcId && card.CanStake && !IsLocked(save, card.instanceId) && (card.Definition.artIndex < 30 || card.Definition.artIndex > 33)) return card;
            throw new InvalidOperationException("这位伙伴暂时没有可押的真片，可先与其他伙伴玩或通过交易归还收藏。");
        }

        private static void EnsureNpcChallengeStock(GameSave save, ChallengeDefinition stage)
        {
            RequireNoPending(save);
            if (BossForNpc(stage.npcId) != null && !CampaignCatalog.IsBossDefeated(save, stage.npcId)) return;
            foreach (var card in save.cards) if (card.ownerId == stage.npcId && card.CanStake && (card.Definition.artIndex < 30 || card.Definition.artIndex > 33)) return;
            PurchaseNpcCommon(save, GameCatalog.GetNpc(stage.npcId));
        }
        private static CardInstance PurchaseNpcCommon(GameSave save, NpcDefinition npc)
        {
            var card = CreateInstance(1, npc.id, "shop", npc.displayName + "从小卖部补购的普通真片。", false);
            card.history[0].reason = CardTransferReason.Purchase;
            card.history[0].transactionId = NewId("npc_purchase");
            save.cards.Add(card);
            return card;
        }

        private static void ApplyCampaignSettlement(GameSave save, PendingMatch match, MatchSettlement receipt)
        {
            if (match.route != MatchRoute.FreePlay && receipt.outcome == MatchOutcome.PlayerWin)
            {
                var boss = BossForNpc(match.npcId);
                if (boss != null && !CampaignCatalog.IsBossDefeated(save, match.npcId))
                {
                    save.story.defeatedBossIds.Add(match.npcId);
                    receipt.defeatedBossId = match.npcId;
                    foreach (var id in receipt.gainedCardIds) if (FindCard(save, id).definitionId == GameCatalog.Cards[boss.rewardCardIndex].id) receipt.rewardCardIds.Add(id);
                }
            }
            if (match.route == MatchRoute.Story && receipt.outcome == MatchOutcome.PlayerWin)
            {
                var stage = CampaignCatalog.GetStage(match.stageId);
                if (!save.story.clearedStageIds.Contains(stage.id))
                {
                    save.story.clearedStageIds.Add(stage.id);
                    receipt.firstClear = true;
                    receipt.rewardMarbles = stage.rewardMarbles;
                    AddMarbles(save, stage.rewardMarbles);
                }
            }
            else if (match.route == MatchRoute.Tournament)
            {
                var run = save.tournament;
                if (run == null || !run.active || run.runId != match.tournamentRunId) throw new InvalidOperationException("杯赛与当前对局不一致。");
                var round = run.rounds[run.roundIndex];
                if (round.matchId != match.matchId || round.completed) throw new InvalidOperationException("杯赛轮次已结算或不属于本场。");
                round.completed = true;
                round.outcome = receipt.outcome;
                foreach (var bracket in run.bracket) if (bracket.roundIndex == run.roundIndex && bracket.leftOwnerId == GameCatalog.PlayerOwnerId) bracket.winnerOwnerId = receipt.outcome == MatchOutcome.PlayerWin ? GameCatalog.PlayerOwnerId : receipt.outcome == MatchOutcome.NpcWin ? round.npcId : "draw";
                if (receipt.outcome == MatchOutcome.PlayerWin)
                {
                    run.roundIndex++;
                    if (run.roundIndex == 3)
                    {
                        run.active = false;
                        run.champion = true;
                        receipt.rewardMarbles = CampaignCatalog.ChampionReward;
                        AddMarbles(save, receipt.rewardMarbles);
                    }
                }
                else if (receipt.outcome == MatchOutcome.Draw)
                {
                    // 平局只结算本场，不淘汰；同一签表/对手/种子继续重赛。
                    round.completed = false;
                    round.matchId = "";
                }
                else run.active = false;
            }
        }

        private static TradeQuote BuildTradeQuote(GameSave save, TradeKind kind, string offerId, string playerCardId, string quoteId)
        {
            if (!Enum.IsDefined(typeof(TradeKind), kind)) throw new ArgumentException("未知交易方式。");
            var quote = new TradeQuote { quoteId = quoteId, expectedRevision = save.revision, kind = kind, offerId = offerId, playerCardId = playerCardId };
            if (kind == TradeKind.Buy)
            {
                if (!string.IsNullOrEmpty(playerCardId)) throw new InvalidOperationException("买片不需要给出自己的圆片。");
                var offer = CampaignCatalog.GetShopOffer(offerId);
                if (!CampaignCatalog.IsOfferUnlocked(save, offer)) throw new InvalidOperationException("这张片目前尚未上架。");
                quote.cardDefinitionId = offer.cardDefinitionId;
                quote.npcId = CampaignCatalog.ShopOwnerId;
                quote.marbleDelta = -offer.buyPrice;
                quote.isFoundation = offer.isFoundation;
                if (save.glassMarbles < offer.buyPrice) throw new InvalidOperationException("玻璃珠不够，先接一次跑腿再来看看。");
            }
            else
            {
                var outgoing = FindCard(save, playerCardId);
                if (outgoing.ownerId != GameCatalog.PlayerOwnerId || IsLocked(save, playerCardId)) throw new InvalidOperationException("只能交易自己持有且未进入对局的圆片。");
                quote.cardDefinitionId = outgoing.definitionId;
                quote.requiresTreasuredConfirmation = outgoing.treasured;
                if (kind == TradeKind.Sell)
                {
                    if (offerId != "shop") throw new ArgumentException("售片柜台是 shop。");
                    quote.npcId = CampaignCatalog.ShopOwnerId;
                    quote.marbleDelta = outgoing.isFoundation ? 1 : CampaignCatalog.SellPrice(outgoing.Definition);
                    quote.isFoundation = outgoing.isFoundation;
                }
                else
                {
                    var offer = CampaignCatalog.GetSwapOffer(offerId);
                    foreach (var boss in CampaignCatalog.Stages)
                        if (boss.rewardCardIndex >= 0 && (GameCatalog.Cards[boss.rewardCardIndex].id == offer.wantedDefinitionId || GameCatalog.Cards[boss.rewardCardIndex].id == offer.offeredDefinitionId) && !CampaignCatalog.IsBossDefeated(save, boss.npcId)) throw new InvalidOperationException("守擂招牌须先战胜对应高手，才能按清单交易。");
                    if (outgoing.definitionId != offer.wantedDefinitionId) throw new InvalidOperationException("这张片不在伙伴本次的固定换片清单里。");
                    foreach (var old in save.transactions) if (old.kind == TradeKind.Swap && old.offerId == offer.id && old.day == save.progress.day) throw new InvalidOperationException("这份清单今天已经换过一次，明天再来。");
                    CardInstance incoming = null;
                    foreach (var card in save.cards) if (card.ownerId == offer.npcId && card.definitionId == offer.offeredDefinitionId && !IsLocked(save, card.instanceId)) { incoming = card; break; }
                    if (incoming == null) throw new InvalidOperationException("伙伴目前没有这张清单片，不能临时复制补货。");
                    if (incoming.Definition.rarity != outgoing.Definition.rarity) throw new InvalidOperationException("伙伴换片必须同稀有度一换一。");
                    quote.npcId = offer.npcId;
                    quote.npcCardId = incoming.instanceId;
                    quote.requiresTreasuredConfirmation |= incoming.treasured;
                }
            }
            if ((long)save.glassMarbles + quote.marbleDelta > int.MaxValue) throw new InvalidOperationException("玻璃珠余额超出可保存范围。");
            return quote;
        }

        private static string QuoteFingerprint(TradeQuote quote) { return Checksum(JsonUtility.ToJson(quote)); }
        private static ChallengeDefinition BossForNpc(string npcId) { foreach (var stage in CampaignCatalog.Stages) if (stage.bossLevel > 0 && stage.npcId == npcId) return stage; return null; }
        private static void AddMarbles(GameSave save, int delta)
        {
            long balance = (long)save.glassMarbles + delta;
            if (balance < 0 || balance > int.MaxValue) throw new InvalidOperationException("玻璃珠余额不足或超出可保存范围。");
            save.glassMarbles = (int)balance;
        }
        private static void RequireNoPending(GameSave save) { if (save.pendingMatch != null) throw new InvalidOperationException("请先继续或结算当前对局。"); }
        public static WeatherKind WeatherForSeed(int seed) { var roll = (uint)seed % 100; return roll < 50 ? WeatherKind.Sunny : roll < 80 ? WeatherKind.Cloudy : WeatherKind.Rainy; }
        private static EnvironmentContext BuildEnvironment(string locationId, int variant, int day, DayPeriod period, WeatherKind weather, int seed, int geometry)
        {
            bool sheltered = locationId == "shop" || locationId == "corridor";
            return new EnvironmentContext { locationId = locationId, visitVariant = variant, day = day, period = period, weather = weather, weatherSeed = seed, geometryVersion = geometry, frictionMultiplier = weather == WeatherKind.Rainy && !sheltered ? .90f : 1f };
        }
        private static EnvironmentContext PrepareEnvironment(GameSave save, string locationId, EnvironmentContext supplied)
        {
            if (supplied != null)
            {
                ValidateEnvironment(supplied);
                if (supplied.locationId != locationId || supplied.day != save.progress.day || supplied.period != save.progress.period || supplied.geometryVersion != 2) throw new InvalidOperationException("开局环境与场地或当前时段不一致。");
                if (save.progress.environment != null && (supplied.weather != save.progress.environment.weather || supplied.weatherSeed != save.progress.environment.weatherSeed)) throw new InvalidOperationException("本时段天气已确定，请使用当前环境。");
                return Copy(supplied);
            }
            var source = save.progress.environment;
            var seed = source == null ? NewSeed() : source.weatherSeed;
            int variant = source != null && source.locationId == locationId ? source.visitVariant : 0;
            return BuildEnvironment(locationId, variant, save.progress.day, save.progress.period, source == null ? WeatherForSeed(seed) : source.weather, seed, 2);
        }
        private static void AdvancePeriod(GameSave save)
        {
            if (save.progress.period == DayPeriod.Evening)
            {
                if (save.progress.day == int.MaxValue) throw new InvalidOperationException("日期超出可保存范围。");
                save.progress.period = DayPeriod.Morning;
                save.progress.day++;
            }
            else save.progress.period = (DayPeriod)((int)save.progress.period + 1);
            var seed = NewSeed();
            var old = save.progress.environment;
            save.progress.environment = BuildEnvironment(save.progress.currentLocationId, old == null ? 0 : old.visitVariant, save.progress.day, save.progress.period, WeatherForSeed(seed), seed, 2);
        }
        private static void TransferForTrade(CardInstance card, string owner, string transactionId, string location, string description, CardTransferReason reason)
        {
            var now = UtcNow();
            card.history.Add(new CardHistoryEntry { utc = now, transactionId = transactionId, matchId = "", locationId = location, fromOwnerId = card.ownerId, toOwnerId = owner, description = description, reason = reason });
            card.ownerId = owner;
            card.sourceLocationId = location;
            card.sourceMatchId = "";
            card.acquiredUtc = now;
        }
        private static bool WasLostByPlayer(CardInstance card)
        {
            if (card.ownerId == GameCatalog.PlayerOwnerId) return false;
            for (int i = card.history.Count - 1; i >= 0; i--)
            {
                var entry = card.history[i];
                if (entry.fromOwnerId == GameCatalog.PlayerOwnerId) return entry.reason == CardTransferReason.Wager || (entry.reason == CardTransferReason.Origin && !string.IsNullOrEmpty(entry.matchId));
            }
            return false;
        }

        private void Mutate(Action<GameSave> mutation)
        {
            lock (sync)
            {
                EnsureLoaded();
                var next = Copy(current);
                mutation(next);
                Commit(next);
            }
        }

        private void EnsureLoaded()
        {
            if (current == null) LoadOrCreate();
            NormalizeNullableState(current);
        }

        private void Commit(GameSave next)
        {
            NormalizeNullableState(next);
            ValidateSave(next);
            Directory.CreateDirectory(directory);
            // 独占短期文件锁，避免两个客户端同时覆盖收藏。
            using (var writeLock = new FileStream(SavePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                GameSave disk;
                string issue;
                if (File.Exists(SavePath))
                {
                    if (!TryRead(SavePath, out disk, out issue)) throw new IOException("磁盘主档在本次会话中变得不可读取，请重新加载后再保存：" + issue);
                    if (current == null || disk.revision != current.revision || disk.profileId != current.profileId)
                        throw new IOException("存档已被另一个游戏实例更新，请重新加载后再保存。");
                }
                next.revision = current == null ? 1 : current.revision + 1;
                next.updatedUtc = UtcNow();
                var payload = JsonUtility.ToJson(next);
                var envelope = new SaveEnvelope { payload = payload, checksum = Checksum(payload) };
                var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope, true));
                var temporary = SavePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    FlushForPlatform(stream);
                }
                if (File.Exists(SavePath))
                {
                    try { File.Replace(temporary, SavePath, BackupPath, true); }
                    catch (Exception exception)
                    {
                        // Android/部分文件系统不支持 File.Replace；保留旧档后完成替换。
                        if (!(exception is PlatformNotSupportedException) && !(exception is NotSupportedException) && !(exception is IOException) && !(exception is UnauthorizedAccessException)) throw;
                        ReplaceWithRecovery(temporary);
                    }
                }
                else File.Move(temporary, SavePath);
                current = next;
            }
        }

        private static void FlushForPlatform(FileStream stream)
        {
            try { stream.Flush(true); }
            catch (NotSupportedException) { stream.Flush(); }
            catch (IOException) { stream.Flush(); }
        }

        private void ReplaceWithRecovery(string temporary)
        {
            // 没有 File.Replace 的平台仍保留一份完整备份；启动优先恢复主档/备份。
            File.Copy(SavePath, BackupPath, true);
            File.Delete(SavePath);
            File.Move(temporary, SavePath);
        }

        private static bool TryRead(string path, out GameSave save, out string problem)
        {
            save = null;
            problem = "文件不存在";
            if (!File.Exists(path)) return false;
            try
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                var envelope = JsonUtility.FromJson<SaveEnvelope>(text);
                if (envelope != null && envelope.envelopeVersion > 1) throw new FutureSaveVersionException("存档封装来自更新的游戏版本，原文件已保留。请使用对应新版游戏。");
                if (envelope == null || envelope.envelopeVersion != 1 || string.IsNullOrEmpty(envelope.payload))
                    throw new InvalidDataException("存档封装无效。");
                if (!string.Equals(envelope.checksum, Checksum(envelope.payload), StringComparison.Ordinal))
                    throw new InvalidDataException("存档校验不一致。");
                save = JsonUtility.FromJson<GameSave>(envelope.payload);
                if (save != null && save.schemaVersion > SchemaVersion) throw new FutureSaveVersionException("存档来自更新的游戏版本，不能回退到旧备份或覆盖原档。");
                NormalizeNullableState(save);
                ValidateSave(save);
                problem = null;
                return true;
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException || exception is FutureSaveVersionException) throw;
                save = null;
                problem = exception.Message;
                return false;
            }
        }

        private static void ValidateSave(GameSave save)
        {
            if (save == null || (save.schemaVersion != 1 && save.schemaVersion != SchemaVersion) || string.IsNullOrEmpty(save.profileId)) throw new InvalidDataException("不支持的存档版本。");
            if (save.revision < 0) throw new InvalidDataException("存档修订号无效。");
            if (save.cards == null || save.progress == null || save.settlements == null) throw new InvalidDataException("存档缺少收藏或进度。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in save.cards)
            {
                if (card == null || string.IsNullOrEmpty(card.instanceId) || !ids.Add(card.instanceId)) throw new InvalidDataException("圆片实例缺失或重复。");
                GameCatalog.GetCard(card.definitionId);
                ValidateOwner(card.ownerId);
                if (card.history == null || card.wins < 0 || card.losses < 0 || card.draws < 0 || float.IsNaN(card.wear) || card.wear < 0 || card.wear > 1)
                    throw new InvalidDataException("圆片履历或磨损无效。");
                foreach (var entry in card.history) if (entry == null || !Enum.IsDefined(typeof(CardTransferReason), entry.reason)) throw new InvalidDataException("圆片转移原因无效。");
            }
            var settlementIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var settlement in save.settlements)
            {
                if (settlement == null || string.IsNullOrEmpty(settlement.matchId) || !settlementIds.Add(settlement.matchId)) throw new InvalidDataException("结算收据重复或缺失。");
                if (!Enum.IsDefined(typeof(MatchOutcome), settlement.outcome) || settlement.shotsUsed < 0) throw new InvalidDataException("对局结果无效。");
                ValidateRule(settlement.rule);
                GameCatalog.GetNpc(settlement.npcId);
                GameCatalog.GetLocation(settlement.locationId);
            }
            if (save.progress.day < 1 || !Enum.IsDefined(typeof(DayPeriod), save.progress.period)) throw new InvalidDataException("日期或时段无效。");
            if (save.progress.marbleRounds < 0 || save.progress.marbleBestScore < 0 || save.progress.marbleBestScore > 8)
                throw new InvalidDataException("玻璃球局数或最高分无效。");
            GameCatalog.GetLocation(save.progress.currentLocationId);
            if (save.progress.visitedLocationIds == null || save.progress.unlockedPlayModes == null || save.progress.npcs == null) throw new InvalidDataException("街区进度缺失。");
            var npcIds = new HashSet<string>();
            foreach (var npcProgress in save.progress.npcs)
            {
                if (npcProgress == null || !npcIds.Add(npcProgress.npcId) || npcProgress.matches < 0 || npcProgress.wins < 0 || npcProgress.losses < 0 || npcProgress.friendship < 0 || npcProgress.recoveredCards < 0) throw new InvalidDataException("伙伴进度重复或无效。");
                GameCatalog.GetNpc(npcProgress.npcId);
            }
            ValidateCampaignState(save);
            var pending = save.pendingMatch;
            if (pending == null) return;
            if (string.IsNullOrEmpty(pending.matchId) || settlementIds.Contains(pending.matchId)) throw new InvalidDataException("待结算对局编号无效。");
            GameCatalog.GetNpc(pending.npcId);
            GameCatalog.GetLocation(pending.locationId);
            ValidateRule(pending.rule);
            if (save.schemaVersion == 2) ValidatePendingContext(save, pending);
            if (pending.isReal && pending.soloPractice) throw new InvalidDataException("真实约战不可标记为自己练手。");
            ValidateStakes(save, pending.playerStakeIds, GameCatalog.PlayerOwnerId, pending.isReal);
            ValidateStakes(save, pending.npcStakeIds, pending.npcId, pending.isReal);
            if (pending.isReal && pending.playerStakeIds.Count != pending.npcStakeIds.Count) throw new InvalidDataException("双方约定赌注不一致。");
            if (pending.checkpoint == null || pending.checkpoint.currentTurn < 0 || pending.checkpoint.shotsUsed < 0 || pending.checkpoint.playerScore < 0 || pending.checkpoint.npcScore < 0)
                throw new InvalidDataException("对局快照无效。");
        }

        private static void ValidateStakes(GameSave save, IReadOnlyList<string> ids, string ownerId, bool isReal)
        {
            if (ids == null || ids.Count > 12 || (ids.Count == 0 && (isReal || ownerId == GameCatalog.PlayerOwnerId)))
                throw new InvalidOperationException("玩家须有练习片；真实约战双方须选择 1 至 12 张赌注片。");
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || !distinct.Add(id)) throw new InvalidOperationException("同一张圆片不能重复下注。");
                var card = FindCard(save, id);
                if (card.ownerId != ownerId) throw new InvalidOperationException("赌注只能来自自己的收藏。");
                if (isReal && !card.CanStake) throw new InvalidOperationException(card.isFoundation ? "基础练习片不可作为赌注。" : "珍藏片不可作为赌注，请先取消珍藏。");
            }
        }

        private static GameSave MigrateLegacy(GameSave source)
        {
            if (source.schemaVersion != 1) throw new InvalidDataException("只能迁移 schema1 存档。");
            var save = Copy(source);
            save.schemaVersion = SchemaVersion;
            save.glassMarbles = 12;
            save.story = new StoryProgress();
            save.tournament = null;
            save.activeErrand = null;
            save.transactions = new List<TradeReceipt>();
            save.activities = new List<ActivityReceipt>();
            save.progress.locationVisits = new List<LocationVisit>();
            foreach (var id in save.progress.visitedLocationIds)
                save.progress.locationVisits.Add(new LocationVisit { locationId = id, count = 1 });
            var seed = save.pendingMatch != null ? save.pendingMatch.randomSeed : NewSeed();
            save.progress.environment = BuildEnvironment(save.progress.currentLocationId, 0, save.progress.day, save.progress.period, WeatherKind.Sunny, seed, 2);
            foreach (var card in save.cards)
                foreach (var entry in card.history)
                    if (!string.IsNullOrEmpty(entry.matchId)) { entry.reason = CardTransferReason.Wager; entry.transactionId = entry.matchId; }
            foreach (var receipt in save.settlements)
            {
                receipt.route = MatchRoute.FreePlay;
                if (receipt.rewardCardIds == null) receipt.rewardCardIds = new List<string>();
            }
            // 仅初始化首次引入的伙伴，绝不为旧伙伴补回输掉的收藏。
            foreach (var npc in GameCatalog.Npcs)
            {
                if (npc.id != "doudou" && npc.id != "ayue" && npc.id != "ayan") continue;
                bool introduced = false;
                foreach (var progress in save.progress.npcs) if (progress.npcId == npc.id) { introduced = true; break; }
                foreach (var card in save.cards) if (card.ownerId == npc.id) { introduced = true; break; }
                if (!introduced)
                {
                    foreach (var index in npc.startingCardIndexes) save.cards.Add(CreateInstance(index, npc.id, npc.locationId, npc.displayName + "带来的新版收藏。", false));
                    save.progress.npcs.Add(new NpcProgress { npcId = npc.id });
                }
            }
            foreach (var stage in CampaignCatalog.Stages)
            {
                if (stage.rewardCardIndex < 0) continue;
                bool exists = false;
                foreach (var card in save.cards) if (card.definitionId == GameCatalog.Cards[stage.rewardCardIndex].id) { exists = true; break; }
                if (!exists) save.cards.Add(CreateInstance(stage.rewardCardIndex, stage.npcId, stage.locationId, "新版街区守擂人的唯一招牌。", false));
            }
            if (save.pendingMatch != null)
            {
                var match = save.pendingMatch;
                match.route = MatchRoute.FreePlay;
                match.stageId = "";
                match.tournamentRunId = "";
                // 旧棋盘仍在真实 v0.3 几何中恢复，不能用新砖块重新解释旧坐标。
                match.environment = BuildEnvironment(match.locationId, 0, save.progress.day, save.progress.period, WeatherKind.Sunny, seed, 1);
                match.layout = LegacyLayout(match.checkpoint.boardStateJson);
            }
            return save;
        }

        [Serializable]
        private sealed class LegacyBoardLayout { public int layout = 0; }
        private static int LegacyLayout(string json)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            try
            {
                var board = JsonUtility.FromJson<LegacyBoardLayout>(json);
                return board != null && board.layout >= 0 && board.layout <= 3 ? board.layout : 0;
            }
            catch (Exception exception) { if (exception is OutOfMemoryException) throw; return 0; }
        }
        private static void ValidateLayout(int layout) { if (layout < 0 || layout > 3) throw new ArgumentOutOfRangeException("layout"); }
        private static void ValidateEnvironment(EnvironmentContext environment)
        {
            if (environment == null || string.IsNullOrEmpty(environment.locationId)) throw new InvalidDataException("环境上下文缺失。");
            GameCatalog.GetLocation(environment.locationId);
            if (environment.visitVariant < 0 || environment.visitVariant > 2 || environment.day < 1 || environment.weatherSeed < 0 || !Enum.IsDefined(typeof(DayPeriod), environment.period) || !Enum.IsDefined(typeof(WeatherKind), environment.weather) || environment.geometryVersion < 1 || environment.geometryVersion > 2)
                throw new InvalidDataException("环境的日期、变体、天气或几何版本无效。");
            bool dry = environment.locationId == "shop" || environment.locationId == "corridor";
            var expected = environment.weather == WeatherKind.Rainy && !dry ? .90f : 1f;
            if (float.IsNaN(environment.frictionMultiplier) || Math.Abs(environment.frictionMultiplier - expected) > .0001f) throw new InvalidDataException("环境摩擦必须与冻结天气一致。");
            if (environment.geometryVersion == 1 && environment.locationId != "home" && environment.locationId != "school_gate" && environment.locationId != "alley" && environment.locationId != "old_tree") throw new InvalidDataException("旧版几何仅支持四个原地点。");
        }

        private static void ValidateCampaignState(GameSave save)
        {
            if (save.schemaVersion == 1) return;
            if (save.glassMarbles < 0 || save.story == null || save.story.clearedStageIds == null || save.story.defeatedBossIds == null || save.transactions == null || save.activities == null || save.progress.locationVisits == null) throw new InvalidDataException("钱包、故事或事务记录缺失。");
            ValidateEnvironment(save.progress.environment);
            if (save.progress.environment.locationId != save.progress.currentLocationId || save.progress.environment.day != save.progress.day || save.progress.environment.period != save.progress.period) throw new InvalidDataException("当前环境与地图进度不一致。");
            var stages = new HashSet<string>();
            foreach (var id in save.story.clearedStageIds)
            {
                CampaignCatalog.GetStage(id);
                if (!stages.Add(id)) throw new InvalidDataException("故事首通记录重复。");
            }
            foreach (var id in stages) foreach (var prerequisite in CampaignCatalog.GetStage(id).requiredStageIds) if (!stages.Contains(prerequisite)) throw new InvalidDataException("故事首通缺少前置关卡。");
            var bosses = new HashSet<string>();
            foreach (var id in save.story.defeatedBossIds) if (BossForNpc(id) == null || !bosses.Add(id)) throw new InvalidDataException("Boss首胜记录无效或重复。");
            var locations = new HashSet<string>();
            foreach (var visit in save.progress.locationVisits)
            {
                if (visit == null || visit.count < 0 || !locations.Add(visit.locationId)) throw new InvalidDataException("重访计数无效或重复。");
                GameCatalog.GetLocation(visit.locationId);
            }
            var ids = new HashSet<string>();
            foreach (var receipt in save.transactions)
            {
                if (receipt == null || string.IsNullOrEmpty(receipt.transactionId) || receipt.quoteId != receipt.transactionId || !ids.Add(receipt.transactionId) || string.IsNullOrEmpty(receipt.quoteFingerprint) || receipt.balanceAfter < 0 || receipt.day < 1 || !Enum.IsDefined(typeof(TradeKind), receipt.kind) || receipt.gainedCardIds == null || receipt.lostCardIds == null) throw new InvalidDataException("交易收据无效或重复。");
                ValidateOwner(receipt.npcId);
                if (receipt.kind == TradeKind.Buy) CampaignCatalog.GetShopOffer(receipt.offerId);
                else if (receipt.kind == TradeKind.Swap) CampaignCatalog.GetSwapOffer(receipt.offerId);
                else if (receipt.offerId != "shop") throw new InvalidDataException("售片柜台无效。");
                foreach (var id in receipt.gainedCardIds) FindCard(save, id);
                foreach (var id in receipt.lostCardIds) FindCard(save, id);
            }
            foreach (var receipt in save.activities)
            {
                if (receipt == null || string.IsNullOrEmpty(receipt.transactionId) || !ids.Add(receipt.transactionId) || receipt.marbleDelta != 6 || receipt.balanceAfter < 0 || receipt.dayAfter < 1 || !Enum.IsDefined(typeof(DayPeriod), receipt.periodAfter)) throw new InvalidDataException("跑腿收据无效或重复。");
                CampaignCatalog.GetErrand(receipt.errandId);
            }
            if (save.activeErrand != null)
            {
                var errand = CampaignCatalog.GetErrand(save.activeErrand.errandId);
                if (save.activeErrand.destinationLocationId != errand.destinationLocationId || save.activeErrand.acceptedDay < 1 || save.activeErrand.acceptedDay > save.progress.day || ids.Contains(save.activeErrand.completionId)) throw new InvalidDataException("正在进行的跑腿无效。");
            }
            var run = save.tournament;
            if (run == null) return;
            if (run.randomSeed < 0 || run.roundIndex < 0 || run.roundIndex > 3 || run.participants == null || run.rounds == null || run.bracket == null || run.participants.Count != 8 || run.rounds.Count != 3 || run.bracket.Count != 7 || (run.active && (run.roundIndex == 3 || run.champion)) || (run.champion != (run.roundIndex == 3))) throw new InvalidDataException("杯赛状态或八人签表无效。");
            var participants = new HashSet<string>(run.participants);
            if (participants.Count != 8 || !participants.Contains(GameCatalog.PlayerOwnerId)) throw new InvalidDataException("杯赛参赛者重复或缺少玩家。");
            foreach (var npc in GameCatalog.Npcs) if (!participants.Contains(npc.id)) throw new InvalidDataException("杯赛缺少街区伙伴。");
            for (int i = 0; i < run.rounds.Count; i++)
            {
                var round = run.rounds[i]; var stage = CampaignCatalog.GetTournamentStage(i);
                if (round == null || round.roundIndex != i || round.stageId != stage.id || round.npcId != stage.npcId || round.locationId != stage.locationId || round.rule != stage.rule || round.layout != stage.layout || round.randomSeed < 1 || !Enum.IsDefined(typeof(MatchOutcome), round.outcome) || (i < run.roundIndex && (!round.completed || round.outcome != MatchOutcome.PlayerWin))) throw new InvalidDataException("杯赛冻结轮次无效。");
                if (round.completed && string.IsNullOrEmpty(round.matchId)) throw new InvalidDataException("杯赛已完成轮次缺少对局编号。");
            }
            foreach (var match in run.bracket)
            {
                if (match == null || match.roundIndex < 0 || match.roundIndex > 2 || !participants.Contains(match.leftOwnerId) || !participants.Contains(match.rightOwnerId) || match.leftOwnerId == match.rightOwnerId || (!string.IsNullOrEmpty(match.winnerOwnerId) && match.winnerOwnerId != "draw" && match.winnerOwnerId != match.leftOwnerId && match.winnerOwnerId != match.rightOwnerId)) throw new InvalidDataException("杯赛对阵或胜者无效。");
            }
        }

        private static void ValidatePendingContext(GameSave save, PendingMatch pending)
        {
            if (!Enum.IsDefined(typeof(MatchRoute), pending.route)) throw new InvalidDataException("未知比赛路线。");
            ValidateLayout(pending.layout);
            ValidateEnvironment(pending.environment);
            if (pending.environment.locationId != pending.locationId) throw new InvalidDataException("比赛环境与场地不一致。");
            if (pending.route == MatchRoute.FreePlay) return;
            if (!pending.isReal || pending.soloPractice || pending.playerStakeIds.Count != 1 || pending.npcStakeIds.Count != 1) throw new InvalidDataException("故事和杯赛必须双方各押一张真片。");
            ChallengeDefinition stage;
            if (pending.route == MatchRoute.Story)
            {
                stage = CampaignCatalog.GetStage(pending.stageId);
                if (!StageUnlocked(save, stage)) throw new InvalidDataException("故事对局尚未解锁。");
            }
            else
            {
                var run = save.tournament;
                if (run == null || !run.active || run.runId != pending.tournamentRunId || run.roundIndex > 2 || run.rounds[run.roundIndex].matchId != pending.matchId) throw new InvalidDataException("杯赛对局与冻结签表不一致。");
                stage = CampaignCatalog.GetTournamentStage(run.roundIndex);
            }
            if (pending.stageId != stage.id || pending.npcId != stage.npcId || pending.locationId != stage.locationId || pending.rule != stage.rule || pending.layout != stage.layout) throw new InvalidDataException("比赛内容与冻结关卡不一致。");
        }

        private static void ValidateOwner(string owner)
        {
            if (owner == GameCatalog.PlayerOwnerId || owner == CampaignCatalog.ShopOwnerId) return;
            GameCatalog.GetNpc(owner);
        }

        private static void ValidateRule(RuleKind rule)
        {
            if (!Enum.IsDefined(typeof(RuleKind), rule)) throw new ArgumentException("未知比赛规则。", "rule");
        }

        private static GameSave CreateNewSave()
        {
            var save = new GameSave { profileId = NewId("profile"), createdUtc = UtcNow() };
            save.progress.visitedLocationIds.Add("home");
            save.progress.unlockedPlayModes.Add("pogs");
            save.cards.Add(CreateInstance(0, GameCatalog.PlayerOwnerId, "home", "外婆送给你的第一张练习片，可以交易，但不能押进真局。", true));
            foreach (var index in new[] { 1, 2, 4, 5, 7 }) save.cards.Add(CreateInstance(index, GameCatalog.PlayerOwnerId, "home", "从童年的小铁盒里找到的圆片。", false));
            foreach (var npc in GameCatalog.Npcs)
            {
                foreach (var index in npc.startingCardIndexes) save.cards.Add(CreateInstance(index, npc.id, npc.locationId, npc.displayName + "的老收藏。", false));
                save.progress.npcs.Add(new NpcProgress { npcId = npc.id });
            }
            var seed = NewSeed();
            save.progress.environment = BuildEnvironment("home", 0, save.progress.day, save.progress.period, WeatherForSeed(seed), seed, 2);
            return save;
        }

        private static CardInstance CreateInstance(int definitionIndex, string owner, string location, string origin, bool foundation)
        {
            var now = UtcNow();
            var card = new CardInstance
            {
                instanceId = NewId("card"), definitionId = GameCatalog.Cards[definitionIndex].id, ownerId = owner,
                originalOwnerId = owner, origin = origin, sourceLocationId = location, sourceMatchId = "", acquiredUtc = now,
                isFoundation = foundation
            };
            card.history.Add(new CardHistoryEntry { utc = now, locationId = location, fromOwnerId = "", toOwnerId = owner, description = origin });
            return card;
        }

        private static CardInstance FindCard(GameSave save, string id)
        {
            foreach (var card in save.cards) if (card.instanceId == id) return card;
            throw new ArgumentException("找不到这张圆片实例：" + id, "instanceId");
        }

        private static CardInstance FindFirstOwned(GameSave save, string owner)
        {
            foreach (var card in save.cards) if (card.ownerId == owner) return card;
            throw new InvalidOperationException("这位伙伴暂时没有圆片：" + owner);
        }

        private static PendingMatch RequirePending(GameSave save, string id)
        {
            if (save.pendingMatch == null || save.pendingMatch.matchId != id) throw new InvalidOperationException("这场约战没有进行中，或已经结算。");
            return save.pendingMatch;
        }

        private static bool IsLocked(GameSave save, string id)
        {
            var match = save.pendingMatch;
            return match != null && (match.playerStakeIds.Contains(id) || match.npcStakeIds.Contains(id));
        }

        private static bool HasBeenOwnedByPlayer(CardInstance card)
        {
            if (card.originalOwnerId == GameCatalog.PlayerOwnerId) return true;
            foreach (var entry in card.history) if (entry.toOwnerId == GameCatalog.PlayerOwnerId) return true;
            return false;
        }

        private static void Transfer(CardInstance card, string newOwner, PendingMatch match, string description)
        {
            var now = UtcNow();
            card.history.Add(new CardHistoryEntry { utc = now, matchId = match.matchId, locationId = match.locationId, fromOwnerId = card.ownerId, toOwnerId = newOwner, description = description, reason = CardTransferReason.Wager, transactionId = match.matchId });
            card.ownerId = newOwner;
            card.sourceLocationId = match.locationId;
            card.sourceMatchId = match.matchId;
            card.acquiredUtc = now;
        }

        private static void UpdateCardStats(GameSave save, List<string> ids, MatchOutcome outcome, bool playerSide, int shots)
        {
            foreach (var id in ids)
            {
                var card = FindCard(save, id);
                if (outcome == MatchOutcome.Draw) card.draws++;
                else if ((outcome == MatchOutcome.PlayerWin) == playerSide) card.wins++;
                else card.losses++;
                card.wear = Math.Min(1f, card.wear + Math.Min(.05f, .009f + shots * .001f));
                card.lastPlayedUtc = UtcNow();
            }
        }

        private static NpcProgress GetNpcProgress(GameProgress progress, string npcId)
        {
            foreach (var npc in progress.npcs) if (npc.npcId == npcId) return npc;
            var created = new NpcProgress { npcId = npcId };
            progress.npcs.Add(created);
            return created;
        }

        private static void Visit(GameProgress progress, string locationId)
        {
            progress.currentLocationId = locationId;
            if (!progress.visitedLocationIds.Contains(locationId)) progress.visitedLocationIds.Add(locationId);
        }

        private void PreserveUnreadablePrimary()
        {
            if (File.Exists(SavePath)) File.Move(SavePath, SavePath + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        }

        private static string Checksum(string payload)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var value in bytes) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static void NormalizeNullableState(GameSave save)
        {
            // Unity 内联序列化普通 class：null 会 round-trip 成字段全空的对象。
            // 约战的存在以持久化 matchId 为准，不能单靠对象引用是否为 null 判断。
            if (save == null) return;
            if (save.pendingMatch != null && string.IsNullOrEmpty(save.pendingMatch.matchId)) save.pendingMatch = null;
            if (save.tournament != null && string.IsNullOrEmpty(save.tournament.runId)) save.tournament = null;
            if (save.activeErrand != null && string.IsNullOrEmpty(save.activeErrand.completionId)) save.activeErrand = null;
            // 早期 schema2 回执可能未写 offerId；由双方不变的定义 ID 恢复清单，不能绕过每日限制。
            if (save.transactions != null && save.cards != null)
                foreach (var receipt in save.transactions)
                {
                    if (receipt == null || receipt.kind != TradeKind.Swap || !string.IsNullOrEmpty(receipt.offerId)) continue;
                    CardInstance outgoing = null, incoming = null;
                    var outgoingId = !string.IsNullOrEmpty(receipt.playerCardId) ? receipt.playerCardId : receipt.lostCardIds != null && receipt.lostCardIds.Count == 1 ? receipt.lostCardIds[0] : null;
                    var incomingId = !string.IsNullOrEmpty(receipt.npcCardId) ? receipt.npcCardId : receipt.gainedCardIds != null && receipt.gainedCardIds.Count == 1 ? receipt.gainedCardIds[0] : null;
                    foreach (var card in save.cards)
                    {
                        if (card.instanceId == outgoingId) outgoing = card;
                        if (card.instanceId == incomingId) incoming = card;
                    }
                    if (outgoing == null || incoming == null) continue;
                    foreach (var offer in CampaignCatalog.SwapOffers)
                        if (offer.npcId == receipt.npcId && offer.wantedDefinitionId == outgoing.definitionId && offer.offeredDefinitionId == incoming.definitionId) { receipt.offerId = offer.id; break; }
                }
        }

        private sealed class FutureSaveVersionException : IOException
        {
            public FutureSaveVersionException(string message) : base(message) { }
        }

        private static T Copy<T>(T source) where T : class
        {
            if (source == null) return null;
            var copy = JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
            NormalizeNullableState(copy as GameSave);
            return copy;
        }
        private static string UtcNow() { return DateTime.UtcNow.ToString("o"); }
        private static string NewId(string prefix) { return prefix + "_" + Guid.NewGuid().ToString("N"); }
        private static int NewSeed() { var bytes = Guid.NewGuid().ToByteArray(); return BitConverter.ToInt32(bytes, 0) & int.MaxValue; }
    }
}
