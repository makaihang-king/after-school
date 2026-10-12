using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Childhood
{
    // 保留旧页面编号，新增页面只追加。
    public enum AppScreen { Hub, Lobby, Collection, Pogs, Marbles, Result, Story, Tournament, Shop, Errands }

    public sealed class SummerGame : MonoBehaviour
    {
        public SaveRepository Repository { get; private set; }
        public SceneWorld World { get; private set; }
        public PogMatch Pogs { get; private set; }
        public SummerAudio Audio { get; private set; }
        public SummerUI UI { get; private set; }
        public CrowdActors Crowd { get; private set; }
        public DisplaySettings Display { get; private set; }
        public AppScreen Screen { get; private set; } = AppScreen.Hub;
        public string LocationId { get; private set; } = "home";
        public string NpcId { get; private set; } = "xiaoyu";
        public string SelectedInstanceId { get; private set; }
        public string InspectedCardId { get; set; } = "pog_00";
        public string PreferredRecoveryInstanceId { get; set; }
        public RuleKind SelectedRule { get; set; } = RuleKind.Flip;
        public bool RealMatch { get; set; }
        public bool Training { get; set; }
        public PracticeLayout SelectedPracticeLayout { get; set; } = PracticeLayout.Classic;
        public MatchRoute SelectedRoute { get; private set; } = MatchRoute.FreePlay;
        public string SelectedStageId { get; private set; }
        public string Error { get; private set; }
        public string ResultTitle { get; private set; }
        public string ResultText { get; private set; }
        public bool ShowDebug { get; private set; }
        public GuidedPractice GuidedPractice { get; private set; }
        public bool GuidedPracticeMode { get; private set; }
        public MatchSettlement LastSettlement { get; private set; }
        public PendingMatch ActiveMatch => currentMatch;
        PendingMatch currentMatch;
        MatchPhase lastSavedPhase;
        int lastSavedTurn = -1;
        float highlightTimer;
        bool exitConfirmed, exitConfirmSuppressed;
        bool exitOperationRunning;
        Coroutine exitWaitCoroutine;
        string startupStage = "启动";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Environment.GetCommandLineArgs().Contains("--arena-test") || Environment.GetCommandLineArgs().Contains("--world-environment-test")) return;
            if (FindFirstObjectByType<SummerGame>() == null)
                new GameObject("放学后 · 童年街区").AddComponent<SummerGame>();
        }
        void Awake()
        {
            bool mobile = Application.isMobilePlatform;
            Application.targetFrameRate = mobile ? 60 : 120;
            Time.fixedDeltaTime = mobile ? 1f / 100f : 1f / 200f;
            Physics.defaultSolverIterations = mobile ? 8 : 16;
            Physics.defaultSolverVelocityIterations = mobile ? 6 : 10;
            QualitySettings.antiAliasing = mobile ? 2 : 4;
            QualitySettings.vSyncCount = mobile ? 0 : 1;
            Display = gameObject.AddComponent<DisplaySettings>();
            var arguments = Environment.GetCommandLineArgs();
            string directory = arguments.Contains("--smoke-test")
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/save-v04")) : null;
            // 显式验收可指定一次性目录；正常启动始终由 Repository 选择版本隔离目录。
            if (arguments.Contains("--smoke-test"))
                for (int i = 0; i + 1 < arguments.Length; i++)
                    if (arguments[i] == "--qa-save") directory = Path.GetFullPath(arguments[i + 1]);
            // 自动化探针在测试结束时直接退出，不应被确认弹窗拦住。
            exitConfirmSuppressed = arguments.Contains("--smoke-test") || arguments.Contains("--arena-test") || arguments.Contains("--world-environment-test");
            try
            {
                startupStage = "读取存档";
                Repository = new SaveRepository(directory);
                var save = Repository.LoadOrCreate(); LocationId = save.progress.currentLocationId;
                SynchronizeSelectedCard(); Error = Repository.LastRecoveryMessage;
                startupStage = "初始化声音";
                Audio = gameObject.AddComponent<SummerAudio>(); World = gameObject.AddComponent<SceneWorld>();
                var environment = save.progress.environment;
                if (environment == null || environment.locationId != LocationId)
                    environment = Repository.VisitLocation(LocationId);
                startupStage = "生成街区";
                World.BuildWorld(environment);
                startupStage = "初始化对局";
                Pogs = gameObject.AddComponent<PogMatch>(); Pogs.Configure(World, Audio);
                Pogs.Changed += OnPogChanged; Pogs.Completed += OnPogCompleted; Pogs.Highlight += Highlight;
                startupStage = "初始化界面";
                UI = gameObject.AddComponent<SummerUI>(); UI.Initialize(this);
                Pogs.BlocksInput = UI.BlocksInput;
                startupStage = "初始化街区人物";
                Crowd = gameObject.AddComponent<CrowdActors>(); Crowd.Configure(World, Pogs);
                startupStage = "绘制首页";
                UI.Render(); ApplyAudioContext();
                startupStage = "完成";
            }
            catch (Exception exception)
            {
                Debug.LogError("游戏启动失败，版本：" + Application.version + "，阶段：" + startupStage + "\n" + exception);
                Error = "游戏启动失败（" + exception.GetType().Name + "，阶段：" + startupStage + "）：" + exception.Message + "\n已有存档文件已保留。";
                var failure = gameObject.AddComponent<StartupFailure>();
                failure.Message = Error; failure.Details = exception.StackTrace; enabled = false;
            }
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) { ShowDebug = !ShowDebug; UI.ToggleDebug(ShowDebug); }
            if (Input.GetKeyDown(KeyCode.M)) { Audio.ToggleMute(); UI.UpdateMute(); }
            if (Screen == AppScreen.Pogs && Input.GetKeyDown(KeyCode.V)) ToggleMatchCamera();
            if (Input.GetKeyDown(KeyCode.Escape) && !UI.HandleEscape() && !Pogs.IsDragging)
            {
                if (Screen == AppScreen.Pogs) UI.ShowPause();
                else if (Screen != AppScreen.Hub) GoHub();
            }
            if (highlightTimer > 0) { highlightTimer -= Time.unscaledDeltaTime; if (highlightTimer <= 0) UI.ClearHighlight(); }
            if (ShowDebug) UI.UpdateDebug();
            // 退出确认浮层可以由“继续玩”按钮关闭；取消仍需终止等待中的退出协程。
            if (exitWaitCoroutine != null && currentMatch != null && UI != null && !UI.ExitVisible) CancelQuit();
        }
        public void ToggleMatchCamera()
        {
            if (Screen != AppScreen.Pogs || World == null || Time.timeScale <= 0) return;
            if (Pogs.IsDragging) Pogs.CancelAim();
            World.ToggleTopDownMatchView();
            UI?.UpdateMatch();
        }
        public void SelectLocation(string id)
        {
            SuspendCurrentMatch();
            var location = GameCatalog.GetLocation(id);
            LocationId = id; SelectedRoute = MatchRoute.FreePlay; SelectedStageId = null;
            NpcId = location.npcIds.Length > 0 ? location.npcIds[0] : "xiaoyu";
            Training = id == "home"; RealMatch = false;
            World.Rebuild(Repository.VisitLocation(id)); Screen = AppScreen.Hub;
            World.SetCameraMode(WorldCameraMode.Hub); Error = null; ApplyAudioContext(); UI.Render(); Audio.Play("ui", .4f);
        }
        public void ShowLobby()
        {
            SuspendCurrentMatch(); SelectedRoute = MatchRoute.FreePlay; SelectedStageId = null;
            SynchronizeSelectedCard(); Screen = AppScreen.Lobby; Error = null; ApplyAudioContext(); UI.Render();
            RefreshCompanionContext(true);
        }
        public void ShowStory() { Navigate(AppScreen.Story); }
        public void ShowTournament() { Navigate(AppScreen.Tournament); }
        public void ShowErrands() { Navigate(AppScreen.Errands); }
        public void ShowCollection() { Navigate(AppScreen.Collection); }
        public void ShowShop()
        {
            SuspendCurrentMatch();
            if (LocationId != "shop") { LocationId = "shop"; World.Rebuild(Repository.VisitLocation("shop")); }
            SynchronizeSelectedCard(); Screen = AppScreen.Shop; Error = null;
            World.SetCameraMode(WorldCameraMode.Hub); ApplyAudioContext(); UI.Render();
        }
        void Navigate(AppScreen target)
        {
            SuspendCurrentMatch(); SynchronizeSelectedCard(); Screen = target; Error = null;
            World.SetCameraMode(target == AppScreen.Collection ? WorldCameraMode.Collection : WorldCameraMode.Hub);
            ApplyAudioContext(); UI.Render();
        }
        public void OpenStoryStage(string id)
        {
            TryAction(() =>
            {
                if (Repository.HasPendingMatch) throw new InvalidOperationException("先继续或结算未完成的约战。");
                var stage = CampaignCatalog.GetStage(id);
                var completed = Repository.Current.story.clearedStageIds;
                if (stage.requiredStageIds.Any(required => !completed.Contains(required)))
                    throw new InvalidOperationException("先完成前面的约定，再来这里挑战。");
                Repository.PrepareChallengeStake(stage.id);
                ConfigureChallenge(stage, MatchRoute.Story);
            });
        }
        public void EnterTournament()
        {
            TryAction(() => { Repository.EnterTournament(); Screen = AppScreen.Tournament; UI.Render(); });
        }
        public void OpenTournamentRound()
        {
            TryAction(() =>
            {
                if (Repository.HasPendingMatch) throw new InvalidOperationException("先继续或结算未完成的约战。");
                var run = Repository.Current.tournament;
                if (run == null || !run.active) throw new InvalidOperationException("先报名八人锦标赛。");
                Repository.PrepareChallengeStake(CampaignCatalog.GetTournamentStage(run.roundIndex).id);
                ConfigureChallenge(CampaignCatalog.GetTournamentStage(run.roundIndex), MatchRoute.Tournament);
            });
        }
        void ConfigureChallenge(ChallengeDefinition stage, MatchRoute route)
        {
            SelectedRoute = route; SelectedStageId = stage.id; NpcId = stage.npcId;
            LocationId = stage.locationId; Training = false; RealMatch = true; SelectedRule = stage.rule;
            SelectedPracticeLayout = (PracticeLayout)stage.layout;
            World.Rebuild(Repository.VisitLocation(LocationId)); World.SetCameraMode(WorldCameraMode.Hub);
            SynchronizeSelectedCard();
            if (SelectedCard == null || !Repository.CanStake(SelectedCard.instanceId))
                SelectedInstanceId = Repository.PlayerCards.FirstOrDefault(c => Repository.CanStake(c.instanceId))?.instanceId ?? SelectedCard?.instanceId;
            Screen = AppScreen.Lobby; Error = null; ApplyAudioContext(); UI.Render();
            RefreshCompanionContext(true);
        }
        public ChallengeDefinition SelectedChallenge => string.IsNullOrEmpty(SelectedStageId) ? null :
            SelectedRoute == MatchRoute.Story ? CampaignCatalog.GetStage(SelectedStageId) :
            CampaignCatalog.TournamentStages.FirstOrDefault(stage => stage.id == SelectedStageId);
        public void SelectNpc(string id)
        {
            if (SelectedRoute != MatchRoute.FreePlay) return;
            PreferredRecoveryInstanceId = null; NpcId = id; LocationId = GameCatalog.GetNpc(id).locationId; Training = false;
            World.Rebuild(Repository.VisitLocation(LocationId)); ApplyAudioContext(); UI.Render();
            RefreshCompanionContext(true);
        }
        public void SelectPlayMode(bool training, bool realMatch)
        {
            if (SelectedRoute != MatchRoute.FreePlay) return;
            Training = training; RealMatch = realMatch && !training;
            RefreshCompanionContext(!training); UI.Render();
        }
        public void SelectCard(string id)
        {
            SelectedInstanceId = id; Error = null; UI.Render();
            if (RealMatch && SelectedCard != null && SelectedCard.isFoundation) UI.ShowPracticeRestriction();
        }
        public CardInstance SelectedCard => Repository.PlayerCards.FirstOrDefault(c => c.instanceId == SelectedInstanceId) ?? Repository.PlayerCards.FirstOrDefault();
        void SynchronizeSelectedCard()
        {
            var cards = Repository.PlayerCards;
            if (cards.Any(card => card.instanceId == SelectedInstanceId)) return;
            SelectedInstanceId = (cards.FirstOrDefault(card => Repository.CanStake(card.instanceId)) ?? cards.FirstOrDefault())?.instanceId;
        }
        public CardInstance OpponentStake
        {
            get
            {
                if (SelectedChallenge != null) return Repository.GetOpponentStakeForChallenge(SelectedStageId);
                string[] bosses = { "ayue", "taotao", "linlin", "ayan" };
                var defeated = Repository.Current.story.defeatedBossIds;
                var cards = Repository.NpcCards(NpcId).Where(c => !c.isFoundation && !c.treasured &&
                    (c.Definition.artIndex < 30 || c.Definition.artIndex > 33 || defeated.Contains(bosses[c.Definition.artIndex - 30]))).ToList();
                var lost = Repository.LostPlayerCards.FirstOrDefault(c => c.ownerId == NpcId && (PreferredRecoveryInstanceId == null || c.instanceId == PreferredRecoveryInstanceId));
                if (lost != null && !cards.Any(card => card.instanceId == lost.instanceId)) lost = null;
                return lost ?? cards.LastOrDefault();
            }
        }
        public void StartPogs()
        {
            try
            {
                if (Repository.HasPendingMatch) throw new InvalidOperationException("还有一场未结束的约战，请先继续它或认输结算。");
                SynchronizeSelectedCard(); var card = SelectedCard;
                if (card == null) { UI.ShowNoCards(); return; }
                if (SelectedRoute != MatchRoute.FreePlay) { Training = false; RealMatch = true; }
                else if (Training) RealMatch = false;
                if (RealMatch && card.isFoundation) { UI.ShowPracticeRestriction(); return; }
                if (RealMatch && card.treasured) throw new InvalidOperationException("这张片标记了“舍不得”。请选择另一张真片，或先在铁盒中取消标记。");
                var environment = World.Environment;
                if (SelectedRoute == MatchRoute.Story) currentMatch = Repository.CreateStoryMatch(SelectedStageId, card.instanceId, environment);
                else if (SelectedRoute == MatchRoute.Tournament) currentMatch = Repository.CreateTournamentMatch(Repository.Current.tournament.runId, card.instanceId, environment);
                else if (RealMatch)
                {
                    if (OpponentStake == null) throw new InvalidOperationException("伙伴暂时没有能押的片。可以换位伙伴，或玩假的切磋。");
                    currentMatch = Repository.CreatePendingMatch(NpcId, LocationId, SelectedRule, new[] { card.instanceId }, new[] { OpponentStake.instanceId }, true,
                        Environment.TickCount, false, (int)SelectedPracticeLayout, environment);
                }
                else currentMatch = Repository.CreatePracticeMatch(NpcId, LocationId, SelectedRule, new[] { card.instanceId },
                    OpponentStake == null ? new string[0] : new[] { OpponentStake.instanceId }, Environment.TickCount, Training, (int)SelectedPracticeLayout, environment);
                Screen = AppScreen.Pogs; lastSavedTurn = -1;
                Pogs.PlayerCardWear = card.wear; Pogs.PlayerCardTreasured = card.treasured;
                ApplyAudioContext(); UI.Render();
                Pogs.Begin(card.Definition, OpponentFor(currentMatch), currentMatch.rule, currentMatch.soloPractice,
                    currentMatch.randomSeed, (PracticeLayout)currentMatch.layout, currentMatch.environment);
            }
            catch (Exception exception) { UI.ShowError(exception.Message); }
        }

        public void StartGuidedPractice()
        {
            try
            {
                if (Repository.HasPendingMatch) throw new InvalidOperationException("还有一场未结束的约战，请先继续它或认输结算。");
                SynchronizeSelectedCard();
                var card = SelectedCard;
                if (card == null) { UI.ShowNoCards(); return; }
                SuspendCurrentMatch();
                GuidedPracticeMode = true; Training = true; RealMatch = false; SelectedRule = RuleKind.Flip;
                SelectedRoute = MatchRoute.FreePlay; SelectedStageId = null; GuidedCard = card;
                World.ResetMatchOrbit();
                Screen = AppScreen.Pogs; World.SetCameraMode(WorldCameraMode.Match); ApplyAudioContext(); UI.Render();
                if (GuidedPractice == null)
                {
                    GuidedPractice = gameObject.AddComponent<GuidedPractice>();
                    GuidedPractice.StepCompleted += OnGuidedStepCompleted;
                    GuidedPractice.StepFailed += OnGuidedStepFailed;
                    GuidedPractice.ResetRequested += OnGuidedResetRequested;
                    GuidedPractice.ProgressChanged += OnGuidedProgressChanged;
                }
                GuidedPractice.AutoAdvance = false;
                GuidedPractice.Begin(Pogs, GuidedPracticeStep.DragAndCancel);
                BeginGuidedStep(GuidedPracticeStep.DragAndCancel);
            }
            catch (Exception exception) { GuidedPracticeMode = false; UI.ShowError(exception.Message); }
        }

        CardInstance GuidedCard;

        void BeginGuidedStep(GuidedPracticeStep step)
        {
            if (!GuidedPracticeMode || GuidedCard == null) return;
            SelectedRule = step == GuidedPracticeStep.SkimRingOut ? RuleKind.RingOut : RuleKind.Flip;
            var layout = step == GuidedPracticeStep.SkimRingOut ? PracticeLayout.EdgeRace : PracticeLayout.Classic;
            Pogs.PlayerCardWear = GuidedCard.wear; Pogs.PlayerCardTreasured = false;
            Pogs.GuidedResultHeld = false;
            Pogs.Begin(GuidedCard.Definition, null, SelectedRule, true, Environment.TickCount ^ ((int)step * 7919), layout, World.Environment);
            Pogs.ConfigureGuidedStep(step);
            Pogs.Shot = step == GuidedPracticeStep.SkimRingOut ? ShotKind.Skim : ShotKind.Slap;
            Pogs.Spin = 0;
            UI?.UpdateMatch();
        }

        public void ContinueGuidedPractice()
        {
            if (!GuidedPracticeMode || GuidedPractice == null || !GuidedPractice.HasResult || !GuidedPractice.StepComplete) return;
            GuidedPractice.AdvanceStep();
            UI?.UpdateMatch();
        }

        public void RetryGuidedPractice()
        {
            if (!GuidedPracticeMode || GuidedPractice == null || !GuidedPractice.Progress.awaitingRetry || GuidedPractice.IsComplete) return;
            GuidedPractice.RetryStep();
            UI?.UpdateMatch();
        }

        void OnGuidedResetRequested(GuidedPracticeStep step) { BeginGuidedStep(step); }
        void OnGuidedProgressChanged(GuidedPracticeProgress progress) { UI?.UpdateMatch(); }
        void OnGuidedStepFailed(GuidedPracticeStep step, string feedback)
        {
            if (!GuidedPracticeMode) return;
            Pogs.GuidedResultHeld = true;
            UI?.UpdateMatch();
        }
        void OnGuidedStepCompleted(GuidedPracticeStep step)
        {
            if (!GuidedPracticeMode) return;
            Pogs.GuidedResultHeld = true;
            if (step == GuidedPracticeStep.SkimRingOut)
            {
                try { Repository.SetGuidedPracticeCompleted(true); } catch (Exception exception) { UI?.ShowError("教学完成标记保存失败：" + exception.Message); }
                Highlight("三个动作都学会了");
            }
            else Highlight(step == GuidedPracticeStep.DragAndCancel ? "第一步完成" : "第二步完成");
            UI?.UpdateMatch();
        }
        NpcDefinition OpponentFor(PendingMatch match)
        {
            var npc = GameCatalog.GetNpc(match.npcId);
            ChallengeDefinition challenge = match.route == MatchRoute.Story ? CampaignCatalog.GetStage(match.stageId) :
                match.route == MatchRoute.Tournament ? CampaignCatalog.TournamentStages.FirstOrDefault(s => s.id == match.stageId) : null;
            if (challenge == null) return npc;
            return new NpcDefinition { id = npc.id, displayName = npc.displayName, locationId = match.locationId,
                introduction = challenge.introLine, challengeLine = challenge.hintLine, winLine = npc.winLine, loseLine = npc.loseLine,
                accentHex = npc.accentHex, skill = challenge.skill, preferredShot = npc.preferredShot,
                preferredRule = match.rule, startingCardIndexes = npc.startingCardIndexes };
        }
        public void ResumePogs()
        {
            try
            {
                var match = Repository.ResumePendingMatch(); if (match == null) return;
                var card = Repository.GetInstance(match.playerStakeIds[0]);
                NpcId = match.npcId; LocationId = match.locationId; SelectedRule = match.rule;
                SelectedRoute = match.route; SelectedStageId = match.stageId; SelectedInstanceId = card.instanceId;
                RealMatch = match.isReal; Training = match.soloPractice; SelectedPracticeLayout = (PracticeLayout)match.layout;
                World.Rebuild(match.environment); Screen = AppScreen.Pogs; currentMatch = null;
                Pogs.PlayerCardWear = card.wear; Pogs.PlayerCardTreasured = card.treasured;
                ApplyAudioContext(); UI.Render();
                Pogs.Begin(card.Definition, OpponentFor(match), SelectedRule, Training, match.randomSeed, SelectedPracticeLayout, match.environment);
                if (!Pogs.RestoreBoard(match.checkpoint?.boardStateJson))
                {
                    Pogs.StopMatch(); Repository.AbortPendingMatch();
                    throw new InvalidDataException("约战快照无法恢复，收藏和赌注仍然保留。可回首页认输结束此局。");
                }
                Training = Pogs.Practice; SelectedPracticeLayout = Pogs.Layout;
                currentMatch = match; lastSavedTurn = -1; OnPogChanged();
            }
            catch (Exception exception) { Screen = AppScreen.Hub; ApplyAudioContext(); UI.Render(); UI.ShowError(exception.Message); }
        }
        public void Forfeit()
        {
            TryAction(() =>
            {
                if (!Repository.HasPendingMatch) { GoHub(); return; }
                var pending = Repository.Current.pendingMatch;
                var settlement = Repository.ForfeitPendingMatch();
                if (pending.environment != null) World.Rebuild(pending.environment);
                ShowSettlement(settlement, true, pending.soloPractice);
            });
        }
        void OnPogChanged() { UI?.UpdateMatch(); SaveStablePogCheckpoint(); }
        void SaveStablePogCheckpoint(bool force = false, bool includeFinished = false)
        {
            if (currentMatch == null || Pogs.IsDragging || Pogs.Phase == MatchPhase.Flying || (Pogs.Phase == MatchPhase.Finished && !includeFinished)) return;
            if (!force && Pogs.Turn == lastSavedTurn && Pogs.Phase == lastSavedPhase) return;
            try
            {
                var checkpoint = new MatchCheckpoint { boardStateJson = Pogs.CaptureBoard(), currentTurn = Math.Max(Pogs.Turn, currentMatch.checkpoint.currentTurn),
                    playerScore = Pogs.PlayerScore, npcScore = Pogs.NpcScore, shotsUsed = Pogs.ShotsUsed, isPlayerTurn = Pogs.Phase == MatchPhase.PlayerAim };
                Repository.SaveMatchProgress(currentMatch.matchId, checkpoint); currentMatch.checkpoint = checkpoint;
                lastSavedTurn = Pogs.Turn; lastSavedPhase = Pogs.Phase;
            }
            catch (Exception exception) { Debug.LogError("对局存档失败：" + exception.Message); Error = "存档写入失败，请检查磁盘空间。"; }
        }
        void OnPogCompleted(MatchOutcome outcome)
        {
            if (GuidedPracticeMode)
            {
                Pogs.GuidedResultHeld = true;
                UI?.UpdateMatch();
                return;
            }
            if (currentMatch == null) return;
            try { SaveStablePogCheckpoint(true, true); ShowSettlement(Repository.ResolvePendingMatch(currentMatch.matchId, outcome, Pogs.ShotsUsed), false, currentMatch.soloPractice); }
            catch (Exception exception) { UI.ShowError("结算存档失败：" + exception.Message); }
        }
        void ShowSettlement(MatchSettlement settlement, bool forfeited, bool soloPractice)
        {
            LastSettlement = settlement;
            NpcId = settlement.npcId; LocationId = settlement.locationId;
            Training = soloPractice; RealMatch = settlement.isReal;
            SelectedRoute = settlement.route; SelectedStageId = settlement.stageId;
            bool won = settlement.outcome == MatchOutcome.PlayerWin, draw = settlement.outcome == MatchOutcome.Draw;
            ResultTitle = forfeited ? "约定的片，留在这里" : won ? "漂亮！这局归你" : draw ? "旗鼓相当" : "下一次，再赢回来";
            string line = draw ? "平局，双方的片各自带回。" : won ? GameCatalog.GetNpc(settlement.npcId).loseLine : GameCatalog.GetNpc(settlement.npcId).winLine;
            if (settlement.route != MatchRoute.FreePlay)
            {
                var stage = settlement.route == MatchRoute.Story ? CampaignCatalog.GetStage(settlement.stageId) : CampaignCatalog.TournamentStages.First(s => s.id == settlement.stageId);
                line = draw ? "这场约定还没分出胜负，下次接着比。" : won ? stage.winLine : stage.loseLine;
            }
            ResultText = line + "\n\n" + (settlement.gainedCardIds.Count > 0 ? "赢来的片已进铁盒，转手履历已经记好。" :
                settlement.lostCardIds.Count > 0 ? "你押的这一张留在伙伴手里，之后仍可约战夺回。" : "这局没有圆片转手。");
            if (!forfeited) ResultText += "\n" + Pogs.PlayerScore + " : " + Pogs.NpcScore;
            if (settlement.rewardMarbles > 0) ResultText += "\n奖励：+" + settlement.rewardMarbles + " 颗玻璃珠";
            if (settlement.firstClear && settlement.route == MatchRoute.Story) ResultText += "\n新的故事约定已解锁。";
            if (settlement.route == MatchRoute.Tournament)
            {
                var run = Repository.Current.tournament;
                ResultText += run.champion ? "\n你是这届盛夏锦标赛冠军！" : !run.active ? "\n本届比赛结束，可以补给后再次报名。" : draw ? "\n下一次重赛本轮。" : "\n已晋级下一轮。";
            }
            ResultText += "\n\n现在是第 " + Repository.Current.progress.day + " 天 · " + GameCatalog.PeriodName(Repository.Current.progress.period);
            SynchronizeSelectedCard(); currentMatch = null; Pogs.StopMatch(); Time.timeScale = 1;
            Screen = AppScreen.Result; ApplyAudioContext(); UI.Render();
            if (!Training) Crowd?.RespondToResult(settlement.npcId, settlement.outcome);
        }
        public void ExecuteTrade(TradeQuote quote, bool confirmTreasured)
        {
            TryAction(() =>
            {
                Repository.ExecuteTrade(quote, confirmTreasured); SynchronizeSelectedCard(); Error = null; UI.Render();
                UI.ShowNotice("交易完成", "圆片与玻璃珠已放好，转手记录也已保存。");
            });
        }
        public void AcceptErrand(string id) { TryAction(() => { Repository.AcceptErrand(id); Screen = AppScreen.Errands; UI.Render(); }); }
        public void CompleteErrand(string id)
        {
            TryAction(() => { Repository.CompleteErrand(id); RefreshEnvironment(); Screen = AppScreen.Errands; UI.Render();
                UI.ShowNotice("跑腿完成", "收到 6 颗玻璃珠，够换一张普通真片了。时间也走到了下一段。"); });
        }
        public void Rest()
        {
            TryAction(() => { if (Repository.HasPendingMatch) throw new InvalidOperationException("这场约战还在等你，先完成或认输再休息。");
                Repository.Rest(); RefreshEnvironment(); UI.Render(); });
        }
        void RefreshEnvironment()
        {
            var context = Repository.Current.progress.environment;
            if (context != null) { LocationId = context.locationId; World.Rebuild(context); ApplyAudioContext(); }
        }
        void TryAction(Action action) { try { action(); } catch (Exception exception) { UI.ShowError(exception.Message); } }
        void SuspendCurrentMatch()
        {
            GuidedPracticeMode = false;
            GuidedCard = null;
            GuidedPractice?.Attach(null);
            if (currentMatch != null)
            {
                SaveStablePogCheckpoint(true, Pogs.Phase == MatchPhase.Finished); Repository.AbortPendingMatch(); currentMatch = null;
            }
            Pogs?.StopMatch(); Time.timeScale = 1; Audio?.Duck(false);
        }
        public void GoHub()
        {
            try { SuspendCurrentMatch(); RefreshEnvironment(); } catch (Exception exception) { Error = exception.Message; }
            Screen = AppScreen.Hub; World.SetCameraMode(WorldCameraMode.Hub); ApplyAudioContext(); UI.Render(); World.SetArenaBoundaryVisible(true);
        }
        public void Retry()
        {
            if (LastSettlement?.route == MatchRoute.Story) ShowStory();
            else if (LastSettlement?.route == MatchRoute.Tournament) ShowTournament(); else ShowLobby();
        }
        public void RefreshUI() { UI.Render(); }
        public void Highlight(string text) { UI.SetHighlight(text); highlightTimer = 1.2f; }
        public void SetError(string text) { Error = text; }
        void ApplyAudioContext()
        {
            RefreshCompanionContext();
            Audio.SetContext(LocationId, Screen == AppScreen.Collection ? AudioMood.Collection : Screen == AppScreen.Result ? AudioMood.Result :
                Screen == AppScreen.Pogs ? AudioMood.Match : AudioMood.Explore); Audio.SetEnvironment(World.Environment);
        }
        void RefreshCompanionContext(bool greet = false)
        {
            string companionId = GuidedPracticeMode ? "xiaoyu" : !Training &&
                (Screen == AppScreen.Lobby || Screen == AppScreen.Pogs || Screen == AppScreen.Result) ? NpcId : null;
            Crowd?.SetCompanionContext(companionId, Screen == AppScreen.Pogs && !Training, greet);
        }
        void OnEnable() { DisplaySettings.ExitGate = RequestQuitGate; }
        void OnDisable() { if (DisplaySettings.ExitGate == RequestQuitGate) DisplaySettings.ExitGate = null; }
        public void RequestExit() { RequestQuit(); }
        public void RequestQuit()
        {
            if (exitConfirmed || exitOperationRunning) return;
            UI?.ShowExitConfirm();
        }
        // 窗口关闭和 Alt+F4 也走同一道确认；自动化验收与启动失败界面直接放行。
        bool RequestQuitGate()
        {
            if (exitConfirmed || exitConfirmSuppressed) return true;
            if (exitOperationRunning) return false;
            if (UI == null) return true;
            UI.ShowExitConfirm();
            return false;
        }
        public void ConfirmExit() { ConfirmQuit(); }
        public void ConfirmQuit()
        {
            if (exitConfirmed || exitOperationRunning) return;
            Error = null;
            // 退出确认会暂停对局；飞行中的圆片必须恢复时间流逝并自然停稳后再取快照。
            if (currentMatch != null && Pogs != null && Pogs.Phase == MatchPhase.Flying)
            {
                exitOperationRunning = true;
                Time.timeScale = 1;
                UI?.ShowExitWaiting();
                exitWaitCoroutine = StartCoroutine(WaitForFlightThenQuit());
                return;
            }
            FinishQuit();
        }
        IEnumerator WaitForFlightThenQuit()
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (currentMatch != null && Pogs != null && Pogs.Phase == MatchPhase.Flying && Time.realtimeSinceStartup < deadline)
                yield return null;
            exitWaitCoroutine = null;
            if (currentMatch != null && Pogs != null && Pogs.Phase == MatchPhase.Flying)
            {
                exitOperationRunning = false;
                UI?.ShowExitSaveFailed("圆片还没有停稳。请继续留在游戏里，等这一手结束后再退出。");
                yield break;
            }
            FinishQuit();
        }
        void FinishQuit()
        {
            if (exitConfirmed) return;
            exitOperationRunning = true;
            Error = null;
            if (currentMatch != null)
            {
                SaveStablePogCheckpoint(true, Pogs != null && Pogs.Phase == MatchPhase.Finished);
                if (Error != null) { exitOperationRunning = false; UI?.ShowExitSaveFailed(Error); return; }
                try { Repository.AbortPendingMatch(); }
                catch (Exception exception) { exitOperationRunning = false; UI?.ShowExitSaveFailed(exception.Message); return; }
            }
            else if (Repository != null && Repository.HasPendingMatch)
            {
                // 首页、设置页也能退出；已持久化的待续对局要保持暂停状态。
                try { Repository.AbortPendingMatch(); }
                catch (Exception exception) { exitOperationRunning = false; UI?.ShowExitSaveFailed(exception.Message); return; }
            }
            try { Repository.Flush(); }
            catch (Exception exception) { exitOperationRunning = false; UI?.ShowExitSaveFailed("存档写入失败：" + exception.Message); return; }
            exitConfirmed = true; exitOperationRunning = false;
            UI?.CloseExitOverlay(); Time.timeScale = 1; DisplaySettings.Quit();
        }
        public void CancelExit() { CancelQuit(); }
        public void CancelQuit()
        {
            if (exitConfirmed) return;
            if (exitWaitCoroutine != null) { StopCoroutine(exitWaitCoroutine); exitWaitCoroutine = null; }
            exitOperationRunning = false;
            Time.timeScale = Screen == AppScreen.Pogs && UI != null && (UI.PauseVisible || UI.SettingsVisible) ? 0 : 1;
        }
        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            Pogs?.CancelAim();
            if (currentMatch != null) SaveStablePogCheckpoint(true, Pogs.Phase == MatchPhase.Finished);
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused) Pogs?.CancelAim();
        }
        void OnApplicationQuit()
        {
            if (currentMatch != null) { SaveStablePogCheckpoint(true, Pogs.Phase == MatchPhase.Finished);
                try { Repository.AbortPendingMatch(); } catch (Exception exception) { Debug.LogError(exception.Message); } }
            Time.timeScale = 1;
        }
    }

    sealed class StartupFailure : MonoBehaviour
    {
        public string Message;
        public string Details;
        Font font;
        bool ownsFont;
        Vector2 scroll;
        void Awake()
        {
            font = Resources.Load<Font>("Fonts/Chinese");
            if (font == null) font = SummerUI.CreateRuntimeFont(22, out ownsFont);
        }
        void OnGUI()
        {
            if (font != null) GUI.skin.label.font = GUI.skin.button.font = font;
            GUI.skin.label.fontSize = 22;
            GUI.skin.label.wordWrap = true;
            Rect safe = UnityEngine.Screen.safeArea;
            Rect panel = new Rect(safe.x + 24, UnityEngine.Screen.height - safe.yMax + 24, Mathf.Max(1, safe.width - 48), Mathf.Max(1, safe.height - 48));
            Color previousColor = GUI.color;
            GUI.color = new Color(.06f, .08f, .1f, .96f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUILayout.BeginArea(new Rect(panel.x + 16, panel.y + 16, Mathf.Max(1, panel.width - 32), Mathf.Max(1, panel.height - 32)));
            GUILayout.Label("放学后 · 启动异常 · " + Application.version); GUILayout.Space(12);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(Message);
            if (!string.IsNullOrEmpty(Details)) { GUILayout.Space(12); GUILayout.Label(Details); }
            GUILayout.EndScrollView();
            if (GUILayout.Button("关闭游戏", GUILayout.Height(45))) DisplaySettings.Quit(); GUILayout.EndArea();
        }
        void OnDestroy() { if (font != null && ownsFont) Destroy(font); }
    }
}
