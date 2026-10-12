using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>独立剧情验收：仅点真实界面、发射真实刚体；不写比分、场上姿态或伪造结算。</summary>
    public sealed class CampaignRuntimeProbe : MonoBehaviour
    {
        const int MaxStoryAttempts = 6;
        const int MaxTournamentRuns = 8;
        const int MaxDrawReplays = 6;
        const float SimulationScale = 4;
        const float MatchTimeoutSeconds = 110;

        [Serializable] sealed class Report
        {
            public bool completed, passed, freshSaveVerified, noScoringFixtures = true;
            public bool tournamentChampion, championRewardVerified, persistedResultsVerified = true;
            public string engine, savePath, startedUtc, finishedUtc;
            public float fixedDeltaTime, simulationTimeScale, elapsedSeconds;
            public int storyStagesVerified, tournamentRoundsVerified, tournamentRuns, directedShots;
            public int errandsCompleted, cardsPurchased, storyRewardMarbles, championRewardMarbles;
            public int maxStoryAttempts = MaxStoryAttempts, maxTournamentRuns = MaxTournamentRuns;
            public List<StageObservation> story = new List<StageObservation>();
            public List<StageObservation> tournament = new List<StageObservation>();
            public List<AttemptObservation> attempts = new List<AttemptObservation>();
            public List<SupplyObservation> supplies = new List<SupplyObservation>();
            public List<string> actions = new List<string>();
            public List<string> physics = new List<string>();
            public List<string> errors = new List<string>();
        }
        [Serializable] sealed class StageObservation
        {
            public string stageId, locationId, npcId, rule, winningMatchId, tournamentRunId;
            public int layout, attempts, retries, directedShots, rewardMarbles;
            public float skill;
            public bool passed, signatureTransferred;
            public string status = "NotAttempted";
        }
        [Serializable] sealed class AttemptObservation
        {
            public string matchId, stageId, route, tournamentRunId, outcome = "Pending";
            public string playerStakeId, npcStakeId, playerDefinitionId, npcDefinitionId;
            public string playerOwnerAfter, npcOwnerAfter;
            public int attemptNumber, seed, layout, playerScore, npcScore, directedShots, totalShots;
            public int dayBefore, periodBefore, dayAfter, periodAfter, balanceBefore, balanceAfter, rewardMarbles;
            public int cardsCreatedInSettlement, playerCardDelta;
            public List<string> signatureTransferIds = new List<string>();
            public float actualNpcSkill, elapsedSeconds;
            public bool contractVerified, ownershipVerified, timeAdvancedOnce, firstClear, persisted, timedOut;
            public EnvironmentContext environment;
            public List<ShotObservation> shots = new List<ShotObservation>();
            public List<TurnObservation> turns = new List<TurnObservation>();
        }
        [Serializable] sealed class ShotObservation
        {
            public int turn, targetArtIndex, playerScoreBefore, npcScoreBefore, playerScoreAfter, npcScoreAfter, contactedTargetCount = -1;
            public string targetPrototype, kind, strategy;
            public float power, spin, targetHeight, footprintHeight, pathStepHeight, targetMovement, radialProgress;
            public Vector3 targetPosition, targetPositionAfter, aimPoint, direction;
            public bool settled;
        }
        [Serializable] sealed class TurnObservation
        {
            public int turn, playerScore, npcScore, totalShots;
            public string phase, feedback;
        }
        [Serializable] sealed class SupplyObservation
        {
            public string kind, receiptId, offerId, cardId, destinationLocationId;
            public int balanceBefore, balanceAfter, dayBefore, periodBefore, dayAfter, periodAfter;
            public bool verified;
        }

        readonly Report report = new Report();
        SummerGame game;
        AttemptObservation activeAttempt;
        string output;
        bool finished;
        float startTime, watchdog, nextFlush;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootProbe()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--smoke-test") && arguments.Contains("--campaign-ui-test"))
                new GameObject("故事与锦标赛原生验收").AddComponent<CampaignRuntimeProbe>();
        }

        void Awake()
        {
            var arguments = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QA/Campaign"));
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == "--qa-output") output = Path.GetFullPath(arguments[i + 1]);
            Directory.CreateDirectory(output);
            report.engine = Application.unityVersion;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            report.simulationTimeScale = SimulationScale;
            startTime = Time.realtimeSinceStartup; watchdog = startTime + 1760; nextFlush = startTime + 20;
            Application.logMessageReceived += OnLog;
        }

        IEnumerator Start()
        {
            // 展平嵌套协程，任何异常都生成诚实的失败报告，而不是静默中断。
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object current = null; bool advanced = false, failed = false;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception exception) { report.errors.Add("Campaign probe: " + exception); CompleteReport(); failed = true; }
                if (failed) yield break;
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            if (!finished) CompleteReport();
        }

        IEnumerator Run()
        {
            yield return new WaitUntil(() => FindFirstObjectByType<SummerGame>() != null);
            game = FindFirstObjectByType<SummerGame>();
            yield return new WaitForSecondsRealtime(1.5f);
            Require(game.Repository != null && game.Pogs != null && game.UI != null, "SummerGame did not initialize.");
            report.savePath = game.Repository.SavePath;
            report.fixedDeltaTime = Time.fixedDeltaTime;
            string qaSave = Argument("--qa-save");
            Require(!string.IsNullOrEmpty(qaSave), "Campaign QA requires an explicit, independent --qa-save directory.");
            Require(string.Equals(Path.GetFullPath(Path.Combine(qaSave, "collection.json")), Path.GetFullPath(report.savePath), StringComparison.OrdinalIgnoreCase),
                "SummerGame did not use the requested QA save directory.");
            var initial = game.Repository.Current;
            report.freshSaveVerified = initial.settlements.Count == 0 && initial.story.clearedStageIds.Count == 0 &&
                initial.tournament == null && initial.pendingMatch == null && initial.glassMarbles == 12;
            Require(report.freshSaveVerified, "Campaign QA requires a fresh save; existing clears cannot count as native verification.");
            Require(Mathf.Abs(Time.fixedDeltaTime - .005f) < .000001f, "Unexpected fixed physics step.");
            game.Pogs.Changed += ObserveTurn;
            game.Pogs.PhysicsDiagnostic += ObservePhysics;
            foreach (var stage in CampaignCatalog.Stages) report.story.Add(StageRecord(stage));
            foreach (var stage in CampaignCatalog.TournamentStages) report.tournament.Add(StageRecord(stage));
            yield return Capture("00-hub");

            foreach (var stage in CampaignCatalog.Stages)
            {
                var record = report.story.First(item => item.stageId == stage.id);
                for (int attempt = 1; attempt <= MaxStoryAttempts && !record.passed; attempt++)
                {
                    yield return EnsureSupply(stage.rule);
                    yield return ReturnHub(); ClickNamed("open-story"); yield return UiTick();
                    Require(game.Screen == AppScreen.Story, "Story button did not open the story page.");
                    ClickNamed("stage-" + stage.id); yield return UiTick();
                    yield return PlayAttempt(stage, MatchRoute.Story, attempt);
                    var result = report.attempts.Last();
                    record.attempts++; record.retries = record.attempts - 1; record.directedShots += result.directedShots;
                    record.status = result.outcome;
                    if (result.outcome == MatchOutcome.PlayerWin.ToString())
                    {
                        var settlement = game.LastSettlement;
                        record.passed = result.contractVerified && result.ownershipVerified && result.timeAdvancedOnce && result.persisted &&
                            result.firstClear && settlement.rewardMarbles == stage.rewardMarbles && game.Repository.Current.story.clearedStageIds.Contains(stage.id);
                        record.winningMatchId = result.matchId; record.rewardMarbles = result.rewardMarbles;
                        record.signatureTransferred = stage.rewardCardIndex < 0 ||
                            result.npcDefinitionId == GameCatalog.Cards[stage.rewardCardIndex].id && result.npcOwnerAfter == GameCatalog.PlayerOwnerId;
                        record.passed &= record.signatureTransferred;
                        if (record.passed) { report.storyStagesVerified++; report.storyRewardMarbles += result.rewardMarbles; }
                        else report.errors.Add(stage.id + " won, but first-clear reward, signature transfer or atomic persistence failed.");
                    }
                    Flush();
                }
                if (!record.passed)
                {
                    report.errors.Add(stage.id + " did not clear through real shots after " + record.attempts + " attempts; later story stages remain locked.");
                    break;
                }
            }

            // 两条路线独立；剧情失利也仍测试合法的八人杯赛，不假解锁后续故事。
            for (int runAttempt = 1; runAttempt <= MaxTournamentRuns && !report.tournamentChampion; runAttempt++)
            {
                yield return EnsureSupply(CampaignCatalog.TournamentStages[0].rule);
                yield return ReturnHub(); ClickNamed("open-tournament"); yield return UiTick();
                ClickNamed(game.Repository.Current.tournament == null ? "enter-tournament" : "next-tournament-round");
                yield return UiTick();
                var run = game.Repository.Current.tournament;
                Require(run != null && run.active && run.roundIndex == 0 && run.participants.Count == 8 && run.bracket.Count == 7,
                    "Tournament entry did not freeze an eight-player bracket.");
                string runId = run.runId; string frozenRounds = FrozenRounds(run);
                report.tournamentRuns++;
                int replay = 0;
                while (game.Repository.Current.tournament.active)
                {
                    var beforeRun = game.Repository.Current.tournament;
                    var stage = CampaignCatalog.GetTournamentStage(beforeRun.roundIndex);
                    var record = report.tournament.First(item => item.stageId == stage.id);
                    yield return EnsureSupply(stage.rule);
                    yield return ReturnHub(); ClickNamed("open-tournament"); yield return UiTick();
                    ClickNamed("next-tournament-round"); yield return UiTick();
                    yield return PlayAttempt(stage, MatchRoute.Tournament, record.attempts + 1);
                    var result = report.attempts.Last();
                    record.attempts++; record.retries = record.attempts - 1; record.directedShots += result.directedShots; record.status = result.outcome;
                    var afterRun = game.Repository.Current.tournament;
                    Require(afterRun.runId == runId && FrozenRounds(afterRun) == frozenRounds, "Tournament opponents, layouts or seeds changed after settlement.");
                    if (result.outcome == MatchOutcome.Draw.ToString())
                    {
                        Require(afterRun.active && afterRun.roundIndex == beforeRun.roundIndex, "A drawn round eliminated or advanced the player.");
                        if (++replay > MaxDrawReplays) throw new InvalidOperationException("Tournament round exceeded legal draw-replay limit.");
                    }
                    else if (result.outcome == MatchOutcome.PlayerWin.ToString())
                    {
                        replay = 0;
                        Require(afterRun.roundIndex == beforeRun.roundIndex + 1, "A real cup victory did not advance one round.");
                        Require(result.rewardMarbles == (beforeRun.roundIndex == 2 ? 24 : 0), "Unexpected tournament reward.");
                        record.winningMatchId = result.matchId; record.tournamentRunId = runId; record.rewardMarbles = result.rewardMarbles;
                        record.passed = result.contractVerified && result.ownershipVerified && result.timeAdvancedOnce && result.persisted;
                    }
                    else Require(!afterRun.active && !afterRun.champion, "A real tournament defeat did not eliminate the player.");
                    Flush();
                }
                var completedRun = game.Repository.Current.tournament;
                if (completedRun.champion)
                {
                    report.tournamentChampion = completedRun.roundIndex == 3 && completedRun.rounds.All(round => round.completed && round.outcome == MatchOutcome.PlayerWin);
                    report.tournamentRoundsVerified = report.tournament.Count(record => record.passed && record.tournamentRunId == runId);
                    var receipts = game.Repository.Current.settlements.Where(receipt => receipt.tournamentRunId == runId).ToArray();
                    report.championRewardMarbles = receipts.Sum(receipt => receipt.rewardMarbles);
                    report.championRewardVerified = report.championRewardMarbles == 24 && receipts.Count(receipt => receipt.rewardMarbles == 24) == 1;
                    yield return ReturnHub(); ClickNamed("open-tournament"); yield return UiTick(); yield return Capture("cup-champion");
                }
            }
            if (!report.tournamentChampion) report.errors.Add("No real three-round championship after " + report.tournamentRuns + " tournament runs.");
            if (report.storyStagesVerified == 8 && report.storyRewardMarbles != 56) report.errors.Add("Story first-clear rewards did not total 56 marbles.");
        }

        IEnumerator PlayAttempt(ChallengeDefinition stage, MatchRoute route, int number)
        {
            Require(game.Screen == AppScreen.Lobby && game.SelectedStageId == stage.id && game.SelectedRoute == route, "Challenge UI opened the wrong contract.");
            var card = ChooseCard(stage.rule);
            Require(card != null, "No legal true card after UI resupply.");
            ClickNamed("card-" + card.instanceId); yield return UiTick();
            Require(game.SelectedInstanceId == card.instanceId && game.OpponentStake != null, "Lobby did not show/select both actual stake instances.");
            string shownOpponentId = game.OpponentStake.instanceId;
            var before = game.Repository.Current;
            ClickNamed("start-pogs"); yield return UiTick();
            Require(game.Screen == AppScreen.Pogs && game.Repository.HasPendingMatch, "Start button did not begin a real challenge: " + game.UI.AlertMessage);
            var pending = game.Repository.GetPendingMatch();
            var actualNpc = typeof(PogMatch).GetField("npc", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game.Pogs) as NpcDefinition;
            var observation = new AttemptObservation
            {
                matchId = pending.matchId, stageId = stage.id, route = route.ToString(), tournamentRunId = pending.tournamentRunId,
                attemptNumber = number, seed = pending.randomSeed, layout = pending.layout,
                playerStakeId = pending.playerStakeIds.Single(), npcStakeId = pending.npcStakeIds.Single(),
                playerDefinitionId = card.definitionId, npcDefinitionId = game.Repository.GetInstance(shownOpponentId).definitionId,
                environment = pending.environment, actualNpcSkill = actualNpc?.skill ?? -1,
                dayBefore = before.progress.day, periodBefore = (int)before.progress.period, balanceBefore = before.glassMarbles
            };
            observation.contractVerified = pending.isReal && !pending.soloPractice && pending.route == route && pending.stageId == stage.id &&
                pending.npcId == stage.npcId && pending.locationId == stage.locationId && pending.rule == stage.rule && pending.layout == stage.layout &&
                observation.playerStakeId == card.instanceId && observation.npcStakeId == shownOpponentId &&
                pending.environment.locationId == stage.locationId && pending.environment.geometryVersion == 2 &&
                JsonUtility.ToJson(pending.environment) == JsonUtility.ToJson(game.World.Environment) &&
                JsonUtility.ToJson(pending.environment) == JsonUtility.ToJson(game.Pogs.MatchEnvironment) && Mathf.Abs(observation.actualNpcSkill - stage.skill) < .000001f;
            Require(observation.contractVerified, "Runtime challenge differs from catalog, shown stake, environment, layout or NPC skill.");
            report.attempts.Add(observation); activeAttempt = observation; ObserveTurn();
            string frozenEnvironment = JsonUtility.ToJson(pending.environment);
            float started = Time.realtimeSinceStartup; int lastDirectedTurn = -1;
            while (game.Screen == AppScreen.Pogs && Time.realtimeSinceStartup - started < MatchTimeoutSeconds)
            {
                if (game.Pogs.Phase == MatchPhase.PlayerAim && game.Pogs.Turn != lastDirectedTurn)
                {
                    Require(frozenEnvironment == JsonUtility.ToJson(game.Pogs.MatchEnvironment) && frozenEnvironment == JsonUtility.ToJson(game.Repository.GetPendingMatch().environment),
                        "Environment changed during an active match.");
                    Time.timeScale = SimulationScale;
                    var shot = PlanShot(stage.rule, number, observation.directedShots);
                    shot.turn = game.Pogs.Turn; shot.playerScoreBefore = game.Pogs.PlayerScore; shot.npcScoreBefore = game.Pogs.NpcScore;
                    int previousShots = game.Pogs.ShotsUsed;
                    lastDirectedTurn = game.Pogs.Turn;
                    game.Pogs.Launch(shot.direction, shot.power, shot.spin, (ShotKind)Enum.Parse(typeof(ShotKind), shot.kind));
                    Require(game.Pogs.Phase == MatchPhase.Flying && game.Pogs.ShotsUsed == previousShots + 1, "A directed shot was not accepted as a real launch.");
                    observation.shots.Add(shot); observation.directedShots++; report.directedShots++;
                    report.actions.Add(stage.id + " real shot turn=" + shot.turn + " kind=" + shot.kind + " power=" + shot.power.ToString("0.000") + " spin=" + shot.spin.ToString("0.000") + " target=" + shot.targetArtIndex);
                }
                yield return null;
            }
            observation.elapsedSeconds = Time.realtimeSinceStartup - started;
            if (game.Screen != AppScreen.Result)
            {
                observation.timedOut = true; observation.outcome = "Timeout"; Flush();
                throw new InvalidOperationException(stage.id + " actual match did not settle within " + MatchTimeoutSeconds + " real seconds.");
            }
            ObserveTurn(); activeAttempt = null;
            var receipt = game.LastSettlement;
            Require(receipt != null && receipt.matchId == pending.matchId && receipt.isReal && receipt.route == route && !game.Repository.HasPendingMatch,
                "Result UI has no matching real settlement.");
            observation.outcome = receipt.outcome.ToString(); observation.totalShots = receipt.shotsUsed;
            observation.firstClear = receipt.firstClear; observation.rewardMarbles = receipt.rewardMarbles;
            observation.dayAfter = receipt.dayAfter; observation.periodAfter = (int)receipt.periodAfter; observation.balanceAfter = receipt.balanceAfter;
            observation.playerOwnerAfter = game.Repository.GetInstance(observation.playerStakeId).ownerId;
            observation.npcOwnerAfter = game.Repository.GetInstance(observation.npcStakeId).ownerId;
            observation.cardsCreatedInSettlement = game.Repository.Current.cards.Count - before.cards.Count;
            observation.playerCardDelta = game.Repository.PlayerCards.Count - before.cards.Count(item => item.ownerId == GameCatalog.PlayerOwnerId);
            observation.signatureTransferIds.AddRange(receipt.rewardCardIds);
            string expectedPlayerOwner = receipt.outcome == MatchOutcome.NpcWin ? stage.npcId : GameCatalog.PlayerOwnerId;
            string expectedNpcOwner = receipt.outcome == MatchOutcome.PlayerWin ? GameCatalog.PlayerOwnerId : stage.npcId;
            observation.ownershipVerified = observation.playerOwnerAfter == expectedPlayerOwner && observation.npcOwnerAfter == expectedNpcOwner &&
                receipt.gainedCardIds.SequenceEqual(receipt.outcome == MatchOutcome.PlayerWin ? new[] { observation.npcStakeId } : Array.Empty<string>()) &&
                receipt.lostCardIds.SequenceEqual(receipt.outcome == MatchOutcome.NpcWin ? new[] { observation.playerStakeId } : Array.Empty<string>()) &&
                receipt.rewardCardIds.Count == receipt.rewardCardIds.Distinct().Count() && receipt.rewardCardIds.All(id => id == observation.npcStakeId &&
                    receipt.gainedCardIds.Contains(id) && game.Repository.GetInstance(id).Definition.artIndex >= 30 && game.Repository.GetInstance(id).Definition.artIndex <= 33) &&
                observation.cardsCreatedInSettlement == 0 && observation.playerCardDelta == (receipt.outcome == MatchOutcome.PlayerWin ? 1 : receipt.outcome == MatchOutcome.NpcWin ? -1 : 0);
            observation.timeAdvancedOnce = Tick(observation.dayAfter, observation.periodAfter) == Tick(observation.dayBefore, observation.periodBefore) + 1 &&
                receipt.balanceAfter == observation.balanceBefore + receipt.rewardMarbles;
            observation.persisted = VerifyPersistence();
            Require(observation.ownershipVerified && observation.timeAdvancedOnce && observation.persisted, "Real settlement ownership, reward/time transaction or persistence failed.");
            yield return Capture(stage.id + "-attempt-" + report.attempts.Count.ToString("000") + "-" + observation.outcome);
            Debug.Log("Campaign actual result: " + stage.id + " " + observation.outcome + " score=" + observation.playerScore + ":" + observation.npcScore + " directedShots=" + observation.directedShots);
        }

        ShotObservation PlanShot(RuleKind rule, int attempt, int shotIndex)
        {
            var targets = game.Pogs.Targets.Where(target => !target.Claimed).ToArray();
            Require(targets.Length > 0, "Player aim has no unclaimed targets.");
            var launch = new Vector3(0, 0, -2.25f);
            DiscBody chosen; Vector3 point; ShotKind kind; float power, spin;
            string strategy;
            float pathStep = 0;
            if (rule == RuleKind.RingOut)
            {
                // 中心在平地不代表圆片能穿过砖角。查询整条通道的真实 .31 m 足迹。
                // 环出圈复用已有真实夺冠证据的低地通道打法；补上圈边收尾和无接触停滞记忆。
                chosen = targets.OrderBy(target => SkimPathStep(InwardPoint(target.Body.position, .09f)) + MissesOn(target) * .10f -
                        (FlatDistance(target.Body.position, new Vector3(0, 0, .55f)) > 1.7f ? .12f : 0) - (LastRadialProgress(target) > .03f ? .025f : 0))
                    .ThenByDescending(target => FlatDistance(target.Body.position, new Vector3(0, 0, .55f)))
                    .ThenBy(target => FlatDistance(target.Body.position, launch)).First();
                point = InwardPoint(chosen.Body.position, attempt % 3 == 0 ? .18f : .09f);
                pathStep = SkimPathStep(point);
                power = attempt % 2 == 0 ? 1 : .90f; spin = 0; kind = ShotKind.Skim; strategy = "Ground-first inward skim";
                int misses = MissesOn(chosen);
                float targetSupport = FootprintHeight(chosen.Body.position);
                if (pathStep > .014f || targetSupport > .025f || FlatDistance(chosen.Body.position, launch) > 3.4f || misses >= 2)
                {
                    // 场地本来就允许三种出手。砖墙挡擦时合法改为空中拍击，绝不挪片或降计分线。
                    float innerOffset = FlatDistance(chosen.Body.position, new Vector3(0, 0, .55f)) > 1.7f ? .12f :
                        attempt % 3 == 0 ? .45f : misses > 0 ? .32f : .22f;
                    point = InwardPoint(chosen.Body.position, innerOffset);
                    kind = (attempt % 3 == 0 || misses >= 2) && shotIndex % 2 == 1 ? ShotKind.Smash : ShotKind.Slap;
                    power = BallisticPower(point, kind, attempt % 2 == 0 ? .035f : 0);
                    strategy = "Terrain-blocked skim replaced by legal airborne shot";
                }
            }
            else
            {
                // 合法策略轮换：近片、可覆盖小簇、单片掀边；所有落点只通过发射刚体实现。
                bool cluster = attempt % 3 == 2;
                chosen = cluster ? targets.OrderByDescending(target => targets.Count(other => Vector3.Distance(target.Body.position, other.Body.position) < 1.2f))
                    .ThenBy(target => target.Body.position.z).First() :
                    targets.OrderByDescending(target => 1 - Mathf.Clamp01(Vector3.Dot(target.Body.rotation * Vector3.up, Vector3.up)))
                        .ThenBy(target => target.Body.position.z).First();
                point = chosen.Body.position;
                if (cluster)
                {
                    var neighbours = targets.Where(target => Vector3.Distance(target.Body.position, point) < 1.12f).ToArray();
                    Vector3 sum = Vector3.zero; foreach (var neighbour in neighbours) sum += neighbour.Body.position;
                    point = Vector3.Lerp(point, sum / neighbours.Length, .55f);
                }
                var outward = point - launch; outward.y = 0;
                var lateral = Vector3.Cross(Vector3.up, outward.normalized);
                float edgeOffset = attempt % 3 == 0 ? .48f : cluster ? .18f : .30f;
                point += lateral * edgeOffset * (shotIndex % 2 == 0 ? 1 : -1);
                kind = attempt == 6 && shotIndex % 2 == 1 ? ShotKind.Smash : ShotKind.Slap;
                power = BallisticPower(point, kind, attempt % 2 == 0 ? .035f : 0);
                spin = attempt % 3 == 0 ? (shotIndex % 2 == 0 ? .22f : -.22f) : 0;
                strategy = cluster ? "Cluster gap ballistic slap" : "Near tilted target edge ballistic shot";
            }
            var direction = point - launch; direction.y = 0;
            return new ShotObservation { targetArtIndex = chosen.ArtIndex, targetPrototype = GameCatalog.Cards[chosen.ArtIndex].prototypeId,
                targetPosition = chosen.Body.position, targetHeight = game.World.SurfaceHeight(chosen.Body.position), aimPoint = point,
                footprintHeight = FootprintHeight(chosen.Body.position), pathStepHeight = pathStep,
                direction = direction.normalized, power = Mathf.Clamp01(power), spin = spin, kind = kind.ToString(), strategy = strategy };
        }

        static Vector3 InwardPoint(Vector3 point, float distance)
        {
            var radial = point - new Vector3(0, 0, .55f); radial.y = 0;
            return point - radial.normalized * distance;
        }
        static float FlatDistance(Vector3 first, Vector3 second)
        { first.y = second.y = 0; return Vector3.Distance(first, second); }
        int MissesOn(DiscBody target) => activeAttempt?.shots.Count(shot => shot.targetArtIndex == target.ArtIndex && shot.settled &&
            shot.playerScoreAfter == shot.playerScoreBefore && shot.contactedTargetCount == 0 && shot.targetMovement < .03f) ?? 0;
        float LastRadialProgress(DiscBody target) => activeAttempt?.shots.LastOrDefault(shot => shot.targetArtIndex == target.ArtIndex && shot.settled)?.radialProgress ?? 0;
        float FootprintHeight(Vector3 point)
        {
            float highest = game.World.SurfaceHeight(point);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                highest = Mathf.Max(highest, game.World.SurfaceHeight(point + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .31f));
            }
            return highest;
        }
        float SkimPathStep(Vector3 point)
        {
            var launch = new Vector3(0, 0, -2.25f); point.y = 0;
            var difference = point - launch;
            // 首次接触目标时圆片中心仍相隔两倍半径；只检查接触之前会先碰到的静态地形。
            float travel = Mathf.Max(0, difference.magnitude - .62f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(travel / .08f));
            Vector3 direction = difference.normalized;
            float previous = FootprintHeight(launch), largestStep = 0;
            for (int i = 1; i <= samples; i++)
            {
                float height = FootprintHeight(launch + direction * (travel * i / samples));
                largestStep = Mathf.Max(largestStep, height - previous); previous = height;
            }
            return largestStep;
        }

        float BallisticPower(Vector3 point, ShotKind kind, float extra)
        {
            var launch = new Vector3(0, 0, -2.25f);
            float height = kind == ShotKind.Smash ? 1.5f : 1.15f;
            float flatTime = (-2 + Mathf.Sqrt(4 + 2 * 9.81f * height)) / 9.81f;
            // 圆片落地前已有俯仰：接触发生于其中心抵达地面之前。只修正常规瞄准值。
            float fallHeight = Mathf.Max(.1f, height + .1f + game.World.SurfaceHeight(launch) - game.World.SurfaceHeight(point) - .19f);
            float targetTime = (-2 + Mathf.Sqrt(4 + 2 * 9.81f * fallHeight)) / 9.81f;
            float dampingTravelTime = (1 - Mathf.Exp(-.16f * targetTime)) / .16f;
            point.y = 0;
            float requestedRange = Vector3.Distance(point, launch) * flatTime / Mathf.Max(.1f, dampingTravelTime);
            return Mathf.Clamp01((requestedRange - 1.25f) / 3.3f + extra);
        }

        IEnumerator EnsureSupply(RuleKind rule)
        {
            var cards = game.Repository.PlayerCards.Where(card => game.Repository.CanStake(card.instanceId)).ToArray();
            bool wantMetal = rule == RuleKind.RingOut && !cards.Any(card => card.Definition.prototypeId == "metal");
            if (cards.Length > 0 && !wantMetal) yield break;
            string offerId = wantMetal ? "buy_pog_10" : "buy_pog_01";
            var offer = CampaignCatalog.ShopOffers.Single(item => item.id == offerId);
            while (game.Repository.Current.glassMarbles < offer.buyPrice) yield return EarnSixMarbles();
            yield return ReturnHub(); ClickText("小卖部交易"); yield return UiTick();
            ClickNamed("shop-tab-0"); yield return UiTick();
            var before = game.Repository.Current;
            ClickNamed("buy-" + offerId); yield return UiTick();
            var after = game.Repository.Current;
            var receipt = after.transactions.Skip(before.transactions.Count).SingleOrDefault();
            Require(receipt != null && receipt.kind == TradeKind.Buy && receipt.offerId == offerId && receipt.gainedCardIds.Count == 1,
                "Real shop button did not create exactly one purchase receipt.");
            var purchased = game.Repository.GetInstance(receipt.gainedCardIds[0]);
            var supply = new SupplyObservation { kind = "Purchase", receiptId = receipt.transactionId, offerId = offerId, cardId = purchased.instanceId,
                balanceBefore = before.glassMarbles, balanceAfter = after.glassMarbles, dayBefore = before.progress.day, periodBefore = (int)before.progress.period,
                dayAfter = after.progress.day, periodAfter = (int)after.progress.period };
            supply.verified = after.glassMarbles == before.glassMarbles - offer.buyPrice && purchased.ownerId == GameCatalog.PlayerOwnerId && !purchased.isFoundation &&
                Tick(supply.dayBefore, supply.periodBefore) == Tick(supply.dayAfter, supply.periodAfter) && VerifyPersistence();
            report.supplies.Add(supply); Require(supply.verified, "UI purchase changed time, gave a practice card or failed atomic persistence.");
            report.cardsPurchased++; yield return CloseNotice();
        }

        IEnumerator EarnSixMarbles()
        {
            yield return ReturnHub(); ClickText("接一趟跑腿"); yield return UiTick();
            if (game.Repository.Current.activeErrand == null) { ClickText("接下这一趟"); yield return UiTick(); }
            var accepted = game.Repository.Current.activeErrand;
            Require(accepted != null, "UI errand acceptance did not save a completion token.");
            if (!accepted.destinationVisited || game.LocationId != accepted.destinationLocationId)
            { ClickText("前往目的地"); yield return UiTick(); ClickText("接一趟跑腿"); yield return UiTick(); }
            var before = game.Repository.Current;
            ClickText("已经送到 · 交付领取 6 颗"); yield return UiTick();
            var after = game.Repository.Current;
            var receipt = after.activities.SingleOrDefault(item => item.transactionId == accepted.completionId);
            var supply = new SupplyObservation { kind = "Errand", receiptId = accepted.completionId, destinationLocationId = accepted.destinationLocationId,
                balanceBefore = before.glassMarbles, balanceAfter = after.glassMarbles, dayBefore = before.progress.day, periodBefore = (int)before.progress.period,
                dayAfter = after.progress.day, periodAfter = (int)after.progress.period };
            supply.verified = receipt != null && receipt.marbleDelta == 6 && after.glassMarbles == before.glassMarbles + 6 && after.activeErrand == null &&
                before.cards.Count == after.cards.Count && Tick(supply.dayAfter, supply.periodAfter) == Tick(supply.dayBefore, supply.periodBefore) + 1 && VerifyPersistence();
            report.supplies.Add(supply); Require(supply.verified, "Errand buttons did not atomically award six marbles and advance exactly one period.");
            report.errandsCompleted++; yield return CloseNotice();
        }

        CardInstance ChooseCard(RuleKind rule)
        {
            return game.Repository.PlayerCards.Where(card => game.Repository.CanStake(card.instanceId))
                .OrderByDescending(card => rule == RuleKind.RingOut ? card.Definition.prototypeId == "metal" ? 4 : card.Definition.prototypeId == "rubber" ? 3 : 1 :
                    card.Definition.prototypeId == "paper" ? 4 : card.Definition.prototypeId == "foil" ? 3 : 1)
                .ThenBy(card => card.wear).FirstOrDefault();
        }

        void ObserveTurn()
        {
            if (activeAttempt == null || game == null) return;
            var match = game.Pogs; var previous = activeAttempt.turns.LastOrDefault();
            activeAttempt.playerScore = match.PlayerScore; activeAttempt.npcScore = match.NpcScore; activeAttempt.totalShots = match.ShotsUsed;
            if (previous == null || previous.turn != match.Turn || previous.phase != match.Phase.ToString() || previous.playerScore != match.PlayerScore || previous.npcScore != match.NpcScore)
                activeAttempt.turns.Add(new TurnObservation { turn = match.Turn, phase = match.Phase.ToString(), playerScore = match.PlayerScore,
                    npcScore = match.NpcScore, totalShots = match.ShotsUsed, feedback = match.LastFeedback });
            var shot = activeAttempt.shots.LastOrDefault();
            if (shot != null && !shot.settled && match.Phase == MatchPhase.Waiting && match.Turn == shot.turn)
            {
                shot.settled = true; shot.playerScoreAfter = match.PlayerScore; shot.npcScoreAfter = match.NpcScore;
                var target = match.Targets.FirstOrDefault(item => item.ArtIndex == shot.targetArtIndex);
                shot.targetPositionAfter = target != null ? target.SnapshotPosition : shot.targetPosition;
                shot.targetMovement = FlatDistance(shot.targetPosition, shot.targetPositionAfter);
                shot.radialProgress = FlatDistance(shot.targetPositionAfter, new Vector3(0, 0, .55f)) - FlatDistance(shot.targetPosition, new Vector3(0, 0, .55f));
            }
        }
        void ObservePhysics(string value)
        {
            report.physics.Add((activeAttempt?.matchId ?? "startup") + " " + value);
            var shot = activeAttempt?.shots.LastOrDefault();
            if (shot == null || shot.settled || game.Pogs.Turn != shot.turn || !value.StartsWith("Pog settle ", StringComparison.Ordinal)) return;
            int start = value.LastIndexOf("contacts=", StringComparison.Ordinal);
            if (start >= 0 && int.TryParse(value.Substring(start + 9), out int contacts)) shot.contactedTargetCount = contacts;
        }
        void OnLog(string value, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(value + "\n" + stack); }

        IEnumerator ReturnHub()
        {
            yield return CloseNotice();
            Require(!game.Repository.HasPendingMatch, "Navigation would abandon an unfinished real match.");
            if (game.Screen != AppScreen.Hub)
            {
                ClickText(game.Screen == AppScreen.Result ? "回街区，换个地方玩" : game.Screen == AppScreen.Lobby ? "收起" : "回到街区");
                yield return UiTick();
            }
            Require(game.Screen == AppScreen.Hub, "Return-to-hub button failed.");
        }
        IEnumerator CloseNotice()
        {
            if (!game.UI.AlertVisible) yield break;
            ClickText("知道了"); yield return UiTick();
        }
        static IEnumerator UiTick() { yield return null; yield return new WaitForSecondsRealtime(.06f); }
        void ClickNamed(string name) { Click(button => button.name == name, "name=" + name); }
        void ClickText(string value) { Click(button => button.text == value, "text=" + value); }
        void Click(Func<Button, bool> predicate, string description)
        {
            var button = game.GetComponentsInChildren<UIDocument>(true).SelectMany(document => document.rootVisualElement.Query<Button>().ToList())
                .FirstOrDefault(item => predicate(item) && item.enabledInHierarchy && item.panel != null && item.resolvedStyle.display != DisplayStyle.None);
            Require(button != null, "No enabled UI button: " + description + "; screen=" + game.Screen + "; alert=" + game.UI.AlertMessage);
            button.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(button);
            report.actions.Add(game.Screen + " UI NavigationSubmitEvent " + description);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        bool VerifyPersistence()
        {
            string before = JsonUtility.ToJson(game.Repository.Current);
            bool same = before == JsonUtility.ToJson(game.Repository.Reload());
            report.persistedResultsVerified &= same; return same;
        }
        static StageObservation StageRecord(ChallengeDefinition stage) => new StageObservation { stageId = stage.id, locationId = stage.locationId,
            npcId = stage.npcId, rule = stage.rule.ToString(), layout = stage.layout, skill = stage.skill };
        static int Tick(int day, int period) => (day - 1) * 4 + period;
        static string FrozenRounds(TournamentRun run) => run.randomSeed + "|" + string.Join("|", run.participants) + "|" +
            string.Join("|", run.rounds.Select(round => round.stageId + ":" + round.npcId + ":" + round.locationId + ":" + round.rule + ":" + round.layout + ":" + round.randomSeed));
        static string Argument(string name)
        { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            int width = UnityEngine.Screen.width, height = UnityEngine.Screen.height;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
            var camera = game.World.MainCamera; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render();
                string error = game.UI.RenderVerificationFrame(target); Require(error == null, "Campaign screenshot UI: " + error);
                RenderTexture.active = target; texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Destroy(target); if (texture != null) Destroy(texture);
            }
        }
        void Update()
        {
            if (finished) return;
            if (game != null && game.Screen == AppScreen.Pogs && !game.UI.AlertVisible && Time.timeScale > 0) Time.timeScale = SimulationScale;
            if (Time.realtimeSinceStartup > watchdog)
            { report.errors.Add("Campaign real-time watchdog reached 1760 seconds; incomplete clears remain failures."); CompleteReport(); }
            else if (Time.realtimeSinceStartup > nextFlush) { nextFlush = Time.realtimeSinceStartup + 20; Flush(); }
        }
        void Flush()
        {
            report.elapsedSeconds = Time.realtimeSinceStartup - startTime;
            File.WriteAllText(Path.Combine(output, "campaign-report.json"), JsonUtility.ToJson(report, true));
        }
        void CompleteReport()
        {
            if (finished) return;
            finished = true; Time.timeScale = 1;
            report.completed = true; report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.passed = report.freshSaveVerified && report.storyStagesVerified == 8 && report.tournamentRoundsVerified == 3 &&
                report.tournamentChampion && report.championRewardVerified && report.storyRewardMarbles == 56 && report.persistedResultsVerified &&
                report.directedShots > 0 && report.errors.Count == 0;
            Flush(); Debug.Log("Campaign QA completed: passed=" + report.passed + " story=" + report.storyStagesVerified + "/8 cup=" + report.tournamentRoundsVerified + "/3 shots=" + report.directedShots);
            DisplaySettings.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (game != null && game.Pogs != null) { game.Pogs.Changed -= ObserveTurn; game.Pogs.PhysicsDiagnostic -= ObservePhysics; }
        }
    }
}
