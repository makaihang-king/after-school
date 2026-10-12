using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Childhood
{
    public enum AppScreen { Hub, Lobby, Collection, Pogs, Marbles, Result }

    /// <summary>合集入口。场景、角色、音频与收藏共享；具体童年玩法各自独立。</summary>
    public sealed class SummerGame : MonoBehaviour
    {
        public SaveRepository Repository { get; private set; }
        public SceneWorld World { get; private set; }
        public PogMatch Pogs { get; private set; }
        public MarbleMatch Marbles { get; private set; }
        public SummerAudio Audio { get; private set; }
        public SummerUI UI { get; private set; }
        public AppScreen Screen { get; private set; } = AppScreen.Hub;
        public string LocationId { get; private set; } = "school_gate";
        public string NpcId { get; private set; } = "xiaoyu";
        public string SelectedInstanceId { get; private set; }
        public string InspectedCardId { get; set; } = "pog_00";
        public string PreferredRecoveryInstanceId { get; set; }
        public RuleKind SelectedRule { get; set; } = RuleKind.Flip;
        public bool RealMatch { get; set; }
        public bool Training { get; set; }
        public PracticeLayout SelectedPracticeLayout { get; set; } = PracticeLayout.Classic;
        public MarbleLayout SelectedMarbleLayout { get; set; } = MarbleLayout.Classic;
        public string Error { get; private set; }
        public string ResultTitle { get; private set; }
        public string ResultText { get; private set; }
        public bool LastWasMarbles { get; private set; }
        public bool ShowDebug { get; private set; }
        PendingMatch currentMatch;
        MatchPhase lastSavedPhase;
        int lastSavedTurn = -1;
        float highlightTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // 地形专项验证创建独立对局，避免自动游戏入口接触玩家存档。
            if (Environment.GetCommandLineArgs().Contains("--arena-test")) return;
            if (FindFirstObjectByType<SummerGame>() == null) new GameObject("放学后 · 游戏合集").AddComponent<SummerGame>();
        }

        void Awake()
        {
            Application.targetFrameRate = 120;
            Time.fixedDeltaTime = 1f / 200f;
            Physics.defaultSolverIterations = 16; Physics.defaultSolverVelocityIterations = 10;
            QualitySettings.antiAliasing = 4; QualitySettings.vSyncCount = 1;
            string saveDirectory = Environment.GetCommandLineArgs().Contains("--smoke-test")
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/save")) : null;
            Repository = new SaveRepository(saveDirectory); Repository.LoadOrCreate();
            Error = Repository.LastRecoveryMessage;
            SelectedInstanceId = Repository.PlayerCards[0].instanceId;
            Audio = gameObject.AddComponent<SummerAudio>();
            World = gameObject.AddComponent<SceneWorld>(); World.BuildWorld(LocationId);
            Pogs = gameObject.AddComponent<PogMatch>(); Pogs.Configure(World, Audio);
            Pogs.Changed += OnPogChanged; Pogs.Completed += OnPogCompleted; Pogs.Highlight += Highlight;
            Marbles = gameObject.AddComponent<MarbleMatch>(); Marbles.Configure(World, Audio);
            Marbles.Changed += () => UI?.UpdateMatch(); Marbles.Completed += OnMarblesCompleted;
            UI = gameObject.AddComponent<SummerUI>(); UI.Initialize(this);
            Pogs.BlocksInput = UI.BlocksInput; Marbles.BlocksInput = UI.BlocksInput;
            var crowd = gameObject.AddComponent<CrowdActors>(); crowd.Configure(World, Pogs, Marbles);
            UI.Render();
            ApplyAudioContext();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) { ShowDebug = !ShowDebug; UI.ToggleDebug(ShowDebug); }
            if (Input.GetKeyDown(KeyCode.M)) { Audio.ToggleMute(); UI.UpdateMute(); }
            if (Input.GetKeyDown(KeyCode.Escape) && !UI.HandleEscape() && !Pogs.IsDragging && !Marbles.IsDragging)
            {
                if (Screen == AppScreen.Pogs || Screen == AppScreen.Marbles) UI.ShowPause();
                else if (Screen != AppScreen.Hub) GoHub();
            }
            if (highlightTimer > 0) { highlightTimer -= Time.unscaledDeltaTime; if (highlightTimer <= 0) UI.ClearHighlight(); }
            if (ShowDebug) UI.UpdateDebug();
        }

        public void SelectLocation(string id)
        {
            LocationId = id; var location = GameCatalog.GetLocation(id);
            if (location.npcIds.Length > 0) NpcId = location.npcIds[0];
            Training = id == "home"; World.Rebuild(id); World.SetCameraMode(WorldCameraMode.Hub);
            Error = null; ApplyAudioContext(); Audio.Play("ui", .4f); UI.Render();
        }

        public void ShowLobby()
        {
            SynchronizeSelectedCard();
            Screen = AppScreen.Lobby; Error = null; ApplyAudioContext(); Audio.Play("ui", .5f); UI.Render();
        }

        public void SelectNpc(string id)
        {
            PreferredRecoveryInstanceId = null;
            NpcId = id; var npc = GameCatalog.GetNpc(id); LocationId = npc.locationId; Training = false;
            World.Rebuild(LocationId); World.SetCameraMode(WorldCameraMode.Hub); ApplyAudioContext(); UI.Render(); Audio.Play("ui", .3f);
        }

        public void SelectCard(string instanceId) { SelectedInstanceId = instanceId; Error = null; UI.Render(); Audio.Play("ui", .3f); }

        public CardInstance SelectedCard
        {
            get
            {
                var cards = Repository.PlayerCards;
                foreach (var card in cards) if (card.instanceId == SelectedInstanceId) return card;
                return cards.Count > 0 ? cards[0] : null;
            }
        }

        void SynchronizeSelectedCard()
        {
            var cards = Repository.PlayerCards;
            if (cards.Any(card => card.instanceId == SelectedInstanceId)) return;
            var selected = cards.FirstOrDefault(card => card.CanTrade) ?? cards.FirstOrDefault();
            SelectedInstanceId = selected?.instanceId;
        }

        public CardInstance OpponentStake
        {
            get
            {
                var cards = Repository.NpcCards(NpcId);
                var preferred = Repository.LostPlayerCards.FirstOrDefault(c => c.instanceId == PreferredRecoveryInstanceId && c.ownerId == NpcId);
                if (preferred != null) return preferred;
                foreach (var lost in Repository.LostPlayerCards) if (lost.ownerId == NpcId) return lost;
                return cards.Count > 0 ? cards[cards.Count - 1] : null;
            }
        }

        public void StartPogs()
        {
            try
            {
                var pending = Repository.GetPendingMatch();
                if (pending != null) { Error = "还有一场未结束的约战。先继续它，或在首页选择认输。"; UI.Render(); return; }
                SynchronizeSelectedCard();
                var card = SelectedCard;
                if (card == null || card.ownerId != GameCatalog.PlayerOwnerId) card = Repository.PlayerCards[0];
                SelectedInstanceId = card.instanceId;
                if (Training) RealMatch = false;
                if (RealMatch)
                {
                    if (card.isFoundation || card.treasured) { Error = "练习片和标记“舍不得”的片不参与玩真的。换一张吧。"; UI.Render(); return; }
                    if (OpponentStake == null) { Error = "朋友的铁盒已经被你赢空了，先玩假的切磋。"; UI.Render(); return; }
                    currentMatch = Repository.CreatePendingMatch(NpcId, LocationId, SelectedRule,
                        new[] { card.instanceId }, new[] { OpponentStake.instanceId }, true, Environment.TickCount);
                }
                else currentMatch = Repository.CreatePracticeMatch(NpcId, LocationId, SelectedRule, new[] { card.instanceId },
                    OpponentStake == null ? new string[0] : new[] { OpponentStake.instanceId }, Environment.TickCount, Training);
                Screen = AppScreen.Pogs; LastWasMarbles = false; lastSavedTurn = -1;
                Pogs.PlayerCardWear = card.wear; Pogs.PlayerCardTreasured = card.treasured;
                ApplyAudioContext(); UI.Render(); Pogs.Begin(card.Definition, GameCatalog.GetNpc(NpcId), SelectedRule, Training, currentMatch.randomSeed, SelectedPracticeLayout);
            }
            catch (Exception e) { Error = e.Message; UI.Render(); }
        }

        public void ResumePogs()
        {
            try
            {
                currentMatch = Repository.ResumePendingMatch(); if (currentMatch == null) return;
                NpcId = currentMatch.npcId; LocationId = currentMatch.locationId; SelectedRule = currentMatch.rule;
                RealMatch = currentMatch.isReal; Training = currentMatch.soloPractice;
                var card = currentMatch.playerStakeIds.Count > 0 ? Repository.GetInstance(currentMatch.playerStakeIds[0]) : SelectedCard;
                if (card == null) card = Repository.PlayerCards[0];
                SelectedInstanceId = card.instanceId; World.Rebuild(LocationId); Screen = AppScreen.Pogs; LastWasMarbles = false;
                string board = currentMatch.checkpoint?.boardStateJson;
                // 防止 Begin 触发的 UI 更新覆盖原有快照。
                var match = currentMatch; currentMatch = null;
                Pogs.PlayerCardWear = card.wear; Pogs.PlayerCardTreasured = card.treasured;
                ApplyAudioContext(); UI.Render(); Pogs.Begin(card.Definition, GameCatalog.GetNpc(NpcId), SelectedRule, Training, match.randomSeed);
                if (!Pogs.RestoreBoard(board))
                {
                    Pogs.StopMatch(); Repository.AbortPendingMatch();
                    throw new InvalidDataException("这场约战的快照无法恢复，收藏和赌注仍然保留。可以认输结束此局。");
                }
                Training = Pogs.Practice; SelectedPracticeLayout = Pogs.Layout; currentMatch = match; lastSavedTurn = -1;
                OnPogChanged();
            }
            catch (Exception e) { Error = e.Message; Screen = AppScreen.Hub; UI.Render(); }
        }

        public void Forfeit()
        {
            try
            {
                if (!Repository.HasPendingMatch) { GoHub(); return; }
                var settlement = Repository.ForfeitPendingMatch();
                ResultTitle = settlement.isReal ? "明天，再赢回来" : "练到这里，喝口汽水";
                ResultText = settlement.isReal ? "认输后，约定的赌片留在朋友铁盒里，下一次挑战能把它赢回来。" : "这场切磋记为落败，圆片没有转手。随时可以再练一局。";
                SynchronizeSelectedCard(); Pogs.StopMatch(); currentMatch = null; Screen = AppScreen.Result; ApplyAudioContext(); UI.Render();
            }
            catch (Exception e) { Error = "认输存档失败：" + e.Message; UI.ShowError(Error); }
        }

        void OnPogChanged()
        {
            UI?.UpdateMatch();
            SaveStablePogCheckpoint();
        }

        void SaveStablePogCheckpoint(bool force = false, bool includeFinished = false)
        {
            if (currentMatch == null || Pogs.IsDragging || Pogs.Phase == MatchPhase.Flying || (Pogs.Phase == MatchPhase.Finished && !includeFinished)) return;
            if (!force && Pogs.Turn == lastSavedTurn && Pogs.Phase == lastSavedPhase) return;
            // 恢复时 Waiting 会暂时表示上一回合基数；不要把已保存回合数向后写。
            int checkpointTurn = Math.Max(Pogs.Turn, currentMatch.checkpoint.currentTurn);
            try
            {
                var checkpoint = new MatchCheckpoint { boardStateJson = Pogs.CaptureBoard(), currentTurn = checkpointTurn,
                    playerScore = Pogs.PlayerScore, npcScore = Pogs.NpcScore, shotsUsed = Pogs.ShotsUsed, isPlayerTurn = Pogs.Phase == MatchPhase.PlayerAim };
                Repository.SaveMatchProgress(currentMatch.matchId, checkpoint);
                currentMatch.checkpoint = checkpoint;
                // 只有实际写入成功后才跳过重复事件，写失败时下一次事件可重试。
                lastSavedTurn = Pogs.Turn; lastSavedPhase = Pogs.Phase;
            }
            catch (Exception e) { Debug.LogError("对局存档失败：" + e.Message); Error = "存档写入失败，请检查磁盘空间。"; }
        }

        void OnPogCompleted(MatchOutcome outcome)
        {
            try
            {
                if (currentMatch == null) return;
                SaveStablePogCheckpoint(true, true);
                var settlement = Repository.ResolvePendingMatch(currentMatch.matchId, outcome, Pogs.ShotsUsed);
                ResultTitle = outcome == MatchOutcome.PlayerWin ? "漂亮！这局归你" : outcome == MatchOutcome.NpcWin ? "明天，再赢回来" : "旗鼓相当";
                string line = outcome == MatchOutcome.Draw ? "旗鼓相当，下次放学再见！" : outcome == MatchOutcome.NpcWin ? GameCatalog.GetNpc(NpcId).winLine : GameCatalog.GetNpc(NpcId).loseLine;
                ResultText = line + "\n\n" + (settlement.gainedCardIds.Count > 0 ? "赢来的圆片已经放进铁盒，来源和这场比赛都记下来了。" : settlement.lostCardIds.Count > 0 ? "它留在" + GameCatalog.GetNpc(NpcId).displayName + "手里。下次约战优先把它赢回来。" : settlement.isReal ? "平局，约定的片各自带回，战绩已经记下。" : "这是切磋局，圆片各自带回，战绩已经记下。") + "\n\n" + Pogs.PlayerScore + " : " + Pogs.NpcScore;
                SynchronizeSelectedCard();
                currentMatch = null; Screen = AppScreen.Result; ApplyAudioContext(); UI.Render();
            }
            catch (Exception e) { Error = "结算存档失败：" + e.Message; UI.ShowError(Error); }
        }

        public void StartMarbles()
        {
            if (Repository.GetPendingMatch() != null) { Error = "先完成铁盒旁那场未结束的打片约战。"; UI.Render(); return; }
            Pogs.StopMatch(); Screen = AppScreen.Marbles; LastWasMarbles = true; World.SetCameraMode(WorldCameraMode.Match);
            ApplyAudioContext(); UI.Render(); Marbles.Begin(97, SelectedMarbleLayout); Audio.Play("ui", .5f);
            World.SetArenaBoundaryVisible(false);
        }

        void OnMarblesCompleted(int score)
        {
            int best;
            try { best = Repository.RecordMarbleRound(score); }
            catch (Exception e) { Error = "玻璃球成绩存档失败：" + e.Message; UI.ShowError(Error); return; }
            ResultTitle = score >= 8 ? "一颗不留，全收下！" : score >= 5 ? "玻璃球小高手" : "再找找那条线路";
            ResultText = GameplayLayouts.Name(Marbles.Layout) + " · 这一轮收下 " + score + " / 8 颗。\n最好成绩 " + Math.Max(best, score) + " / 8 · 最多连续得分 " + Marbles.BestScoringStreak + " 手。\n\n" + GameplayLayouts.Hint(Marbles.Layout);
            Screen = AppScreen.Result; ApplyAudioContext(); Audio.Play(score >= 5 ? "win" : "lose", .6f); UI.Render();
        }

        public void ShowCollection()
        {
            Pogs.StopMatch(); Marbles.StopMatch(); Screen = AppScreen.Collection; World.SetCameraMode(WorldCameraMode.Collection); ApplyAudioContext(); UI.Render(); Audio.Play("ui", .5f);
        }

        public void GoHub()
        {
            if (currentMatch != null)
            {
                SaveStablePogCheckpoint(true, Pogs.Phase == MatchPhase.Finished);
                try { Repository.AbortPendingMatch(); }
                catch (Exception e) { Debug.LogError("暂停存档失败：" + e.Message); }
                currentMatch = null;
            }
            Pogs.StopMatch(); Marbles.StopMatch(); Time.timeScale = 1; Audio.Duck(false);
            Screen = AppScreen.Hub; Error = null; World.SetCameraMode(WorldCameraMode.Hub); ApplyAudioContext(); UI.Render();
            World.SetArenaBoundaryVisible(true);
        }

        public void Retry() { if (LastWasMarbles) StartMarbles(); else { Pogs.StopMatch(); ShowLobby(); } }
        public void RefreshUI() { UI.Render(); }
        public void Highlight(string message) { UI.SetHighlight(message); highlightTimer = 1.2f; }
        public void SetError(string message) { Error = message; }
        void ApplyAudioContext()
        {
            Audio.SetContext(LocationId, Screen == AppScreen.Collection ? AudioMood.Collection :
                Screen == AppScreen.Result ? AudioMood.Result :
                Screen == AppScreen.Pogs || Screen == AppScreen.Marbles ? AudioMood.Match : AudioMood.Explore);
        }
        void OnApplicationPause(bool paused) { if (paused && currentMatch != null) SaveStablePogCheckpoint(true, Pogs.Phase == MatchPhase.Finished); }
        void OnApplicationQuit()
        {
            if (currentMatch != null)
            {
                SaveStablePogCheckpoint(true, Pogs.Phase == MatchPhase.Finished);
                try { Repository.AbortPendingMatch(); }
                catch (Exception e) { Debug.LogError("退出存档失败：" + e.Message); }
            }
            Time.timeScale = 1;
        }
    }
}
