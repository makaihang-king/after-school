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
        public bool CanTrade { get { return !isFoundation && !treasured; } }
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
        public List<string> visitedLocationIds = new List<string>();
        public List<string> unlockedPlayModes = new List<string>();
        public List<NpcProgress> npcs = new List<NpcProgress>();
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
    }

    /// <summary>
    /// 所有写操作先构造副本，再校验、写入并提交；调用方读到的对象也是副本。
    /// 真实对局先持久化约定赌注，结算只移动这些实例，重复结算返回同一张收据。
    /// </summary>
    public sealed class SaveRepository
    {
        public const int SchemaVersion = 1;
        private readonly object sync = new object();
        private GameSave current;
        private readonly string directory;
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

        public SaveRepository(string saveDirectory = null)
        {
            directory = Path.GetFullPath(string.IsNullOrEmpty(saveDirectory) ? Path.Combine(Application.persistentDataPath, "SummerAfterSchool") : saveDirectory);
            SavePath = Path.Combine(directory, "collection.json");
            BackupPath = Path.Combine(directory, "collection.backup.json");
        }

        public GameSave Current { get { lock (sync) { EnsureLoaded(); return Copy(current); } } }
        public bool HasPendingMatch { get { lock (sync) { EnsureLoaded(); return current.pendingMatch != null; } } }
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
                    RepairCollectionIfNeeded();
                    return Copy(current);
                }
                string primaryProblem = problem;
                if (TryRead(BackupPath, out loaded, out problem))
                {
                    LastRecoveryMessage = "主存档不可读取，已从备份恢复。";
                    PreserveUnreadablePrimary();
                    current = loaded;
                    var recovered = Copy(current);
                    RepairCollections(recovered);
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
                    var recovered = Copy(current);
                    RepairCollections(recovered);
                    Commit(recovered);
                    return Copy(current);
                }
                if (File.Exists(SavePath) || File.Exists(BackupPath))
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
                        if (card.ownerId != GameCatalog.PlayerOwnerId && HasBeenOwnedByPlayer(card)) result.Add(Copy(card));
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
                return card.ownerId == GameCatalog.PlayerOwnerId && card.CanTrade && !IsLocked(current, instanceId);
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

        public PendingMatch CreatePendingMatch(string npcId, string locationId, RuleKind rule, IReadOnlyList<string> playerStakeIds, IReadOnlyList<string> npcStakeIds, bool isReal = true, int randomSeed = 0, bool soloPractice = false)
        {
            PendingMatch result = null;
            Mutate(save =>
            {
                GameCatalog.GetNpc(npcId);
                GameCatalog.GetLocation(locationId);
                ValidateRule(rule);
                if (isReal && soloPractice) throw new InvalidOperationException("自己练手不能押片约战。");
                if (save.pendingMatch != null) throw new InvalidOperationException("已有未完成约战，请继续对局或认输后再开始。");
                ValidateStakes(save, playerStakeIds, GameCatalog.PlayerOwnerId, isReal);
                ValidateStakes(save, npcStakeIds, npcId, isReal);
                if (isReal && playerStakeIds.Count != npcStakeIds.Count) throw new InvalidOperationException("双方必须先约定相同数量的赌注片。");
                var match = new PendingMatch
                {
                    matchId = NewId("match"), npcId = npcId, locationId = locationId, rule = rule, isReal = isReal, soloPractice = soloPractice,
                    randomSeed = randomSeed == 0 ? NewSeed() : randomSeed, startedUtc = UtcNow(),
                    playerStakeIds = new List<string>(playerStakeIds), npcStakeIds = new List<string>(npcStakeIds),
                    checkpoint = new MatchCheckpoint { savedUtc = UtcNow() }
                };
                save.pendingMatch = match;
                Visit(save.progress, locationId);
                result = Copy(match);
            });
            return result;
        }

        public PendingMatch CreatePracticeMatch(string npcId, string locationId, RuleKind rule, IReadOnlyList<string> playerCardIds, IReadOnlyList<string> npcCardIds, int randomSeed = 0, bool soloPractice = false)
        {
            return CreatePendingMatch(npcId, locationId, rule, playerCardIds, npcCardIds, false, randomSeed, soloPractice);
        }

        public PendingMatch CreatePracticeMatch(string npcId, string locationId, RuleKind rule, int randomSeed = 0, string playerCardId = null, bool soloPractice = false)
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
                return CreatePracticeMatch(npcId, locationId, rule, new[] { player.instanceId }, npcCards, randomSeed, soloPractice);
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
                    experienceGained = (match.isReal ? 14 : 6) + (outcome == MatchOutcome.PlayerWin ? 10 : 4)
                };
                UpdateCardStats(next, match.playerStakeIds, outcome, true, shotsUsed);
                UpdateCardStats(next, match.npcStakeIds, outcome, false, shotsUsed);
                if (match.isReal && outcome == MatchOutcome.PlayerWin)
                {
                    foreach (var id in match.npcStakeIds)
                    {
                        var card = FindCard(next, id);
                        if (HasBeenOwnedByPlayer(card)) receipt.recoveredCardIds.Add(id);
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
                next.settlements.Add(receipt);
                next.pendingMatch = null;
                RepairCollections(next);
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

        public void VisitLocation(string locationId)
        {
            GameCatalog.GetLocation(locationId);
            Mutate(save => Visit(save.progress, locationId));
        }

        public void AdvanceTime()
        {
            Mutate(save =>
            {
                if (save.progress.period == DayPeriod.Evening) { save.progress.period = DayPeriod.Morning; save.progress.day++; }
                else save.progress.period = (DayPeriod)((int)save.progress.period + 1);
            });
        }

        public void SetTutorialComplete(bool complete = true) { Mutate(save => save.progress.tutorialComplete = complete); }
        public void Flush() { lock (sync) { EnsureLoaded(); Commit(Copy(current)); } }
        public void EnsurePlayableCollection() { Mutate(RepairCollections); }

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
                if (current != null && File.Exists(SavePath) && TryRead(SavePath, out disk, out issue) && disk.revision != current.revision)
                    throw new IOException("存档已被另一个游戏实例更新，请重新加载后再保存。");
                next.revision = current == null ? 1 : current.revision + 1;
                next.updatedUtc = UtcNow();
                var payload = JsonUtility.ToJson(next);
                var envelope = new SaveEnvelope { payload = payload, checksum = Checksum(payload) };
                var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope, true));
                var temporary = SavePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    try { File.Replace(temporary, SavePath, BackupPath, true); }
                    catch (PlatformNotSupportedException) { ReplaceWithRecovery(temporary); }
                }
                else File.Move(temporary, SavePath);
                current = next;
            }
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
                if (envelope == null || envelope.envelopeVersion != 1 || string.IsNullOrEmpty(envelope.payload))
                    throw new InvalidDataException("存档封装无效。");
                if (!string.Equals(envelope.checksum, Checksum(envelope.payload), StringComparison.Ordinal))
                    throw new InvalidDataException("存档校验不一致。");
                save = JsonUtility.FromJson<GameSave>(envelope.payload);
                NormalizeNullableState(save);
                ValidateSave(save);
                problem = null;
                return true;
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException) throw;
                save = null;
                problem = exception.Message;
                return false;
            }
        }

        private static void ValidateSave(GameSave save)
        {
            if (save == null || save.schemaVersion != SchemaVersion || string.IsNullOrEmpty(save.profileId)) throw new InvalidDataException("不支持的存档版本。");
            if (save.cards == null || save.progress == null || save.settlements == null) throw new InvalidDataException("存档缺少收藏或进度。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in save.cards)
            {
                if (card == null || string.IsNullOrEmpty(card.instanceId) || !ids.Add(card.instanceId)) throw new InvalidDataException("圆片实例缺失或重复。");
                GameCatalog.GetCard(card.definitionId);
                ValidateOwner(card.ownerId);
                if (card.history == null || card.wins < 0 || card.losses < 0 || card.draws < 0 || float.IsNaN(card.wear) || card.wear < 0 || card.wear > 1)
                    throw new InvalidDataException("圆片履历或磨损无效。");
                if (card.isFoundation && card.ownerId != GameCatalog.PlayerOwnerId) throw new InvalidDataException("基础练习片不可转手。");
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
            var pending = save.pendingMatch;
            if (pending == null) return;
            if (string.IsNullOrEmpty(pending.matchId) || settlementIds.Contains(pending.matchId)) throw new InvalidDataException("待结算对局编号无效。");
            GameCatalog.GetNpc(pending.npcId);
            GameCatalog.GetLocation(pending.locationId);
            ValidateRule(pending.rule);
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
                if (isReal && !card.CanTrade) throw new InvalidOperationException(card.isFoundation ? "基础练习片不可作为赌注。" : "珍藏片不可作为赌注，请先取消珍藏。");
            }
        }

        private static void ValidateOwner(string owner)
        {
            if (owner == GameCatalog.PlayerOwnerId) return;
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
            save.cards.Add(CreateInstance(0, GameCatalog.PlayerOwnerId, "home", "外婆送给你的第一张练习片，永远不会输掉。", true));
            foreach (var index in new[] { 1, 2, 4, 5, 7 }) save.cards.Add(CreateInstance(index, GameCatalog.PlayerOwnerId, "home", "从童年的小铁盒里找到的圆片。", false));
            foreach (var npc in GameCatalog.Npcs)
            {
                foreach (var index in npc.startingCardIndexes) save.cards.Add(CreateInstance(index, npc.id, npc.locationId, npc.displayName + "的老收藏。", false));
                save.progress.npcs.Add(new NpcProgress { npcId = npc.id });
            }
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

        private void RepairCollectionIfNeeded()
        {
            bool hasFoundation = false;
            foreach (var card in current.cards) if (card.isFoundation && card.ownerId == GameCatalog.PlayerOwnerId) { hasFoundation = true; break; }
            if (!hasFoundation) { var next = Copy(current); RepairCollections(next); Commit(next); }
        }

        private static void RepairCollections(GameSave save)
        {
            foreach (var card in save.cards) if (card.isFoundation && card.ownerId == GameCatalog.PlayerOwnerId) return;
            save.cards.Add(CreateInstance(0, GameCatalog.PlayerOwnerId, "home", "外婆帮你补回的基础练习片。练习永远有片可用。", true));
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
            if (card.isFoundation) throw new InvalidOperationException("基础练习片不可转手。");
            var now = UtcNow();
            card.history.Add(new CardHistoryEntry { utc = now, matchId = match.matchId, locationId = match.locationId, fromOwnerId = card.ownerId, toOwnerId = newOwner, description = description });
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
            if (save != null && save.pendingMatch != null && string.IsNullOrEmpty(save.pendingMatch.matchId)) save.pendingMatch = null;
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
