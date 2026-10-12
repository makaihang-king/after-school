using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>横屏手机的约战与街区页面：可读的大卡片、有限分页和固定主操作。</summary>
    public sealed partial class SummerUI
    {
        int mobileLobbyStep, mobileOpponentPage, mobileStakePage;
        int mobileStoryPage, mobileShopPage, mobileErrandPage;
        string mobileLobbyRoute;

        void BuildMobileLobby()
        {
            var challenge = game.SelectedChallenge;
            string route = challenge?.id ?? "free";
            if (mobileLobbyRoute != route)
            {
                mobileLobbyRoute = route;
                mobileLobbyStep = challenge == null ? 0 : 2;
                mobileOpponentPage = mobileStakePage = 0;
            }
            var page = MobilePage(challenge?.title ?? "自由约战");
            page.name = "mobile-lobby";
            var steps = Box("mobile-steps");
            steps.style.flexDirection = FlexDirection.Row;
            steps.style.height = 52; steps.style.flexShrink = 0; steps.style.marginBottom = 8;
            page.Add(steps);
            string[] labels = { "1  玩法", "2  对手与场地", "3  选出手片" };
            for (int i = 0; i < labels.Length; i++)
            {
                int step = i;
                var tab = MobilePagesButton(labels[i], () => { mobileLobbyStep = step; Render(); }, "mobile-choice", 52, 22);
                tab.name = "mobile-lobby-step-" + i;
                tab.style.flexGrow = 1; tab.style.flexBasis = 0;
                tab.style.marginRight = i < 2 ? 8 : 0;
                tab.EnableInClassList("selected", mobileLobbyStep == i);
                steps.Add(tab);
            }

            var main = MobilePagesRow(page, "mobile-lobby-main");
            main.style.flexGrow = 1; main.style.minHeight = 0;
            var summary = MobilePagesPanel("mobile-lobby-summary");
            summary.style.width = Length.Percent(34); summary.style.flexShrink = 0;
            summary.style.marginRight = 10; main.Add(summary);
            var summaryCopy = new ScrollView(ScrollViewMode.Vertical);
            summaryCopy.name = "mobile-lobby-summary-copy";
            summaryCopy.style.flexGrow = 1; summaryCopy.style.minHeight = 0; summary.Add(summaryCopy);
            summaryCopy.Add(MobilePagesText(challenge == null ? game.Training ? "自己练手" : "与" + GameCatalog.GetNpc(game.NpcId).displayName + "约战" : "这场盛夏之约", "mobile-section-title", 24));
            summaryCopy.Add(MobilePagesText(GameCatalog.GetLocation(game.LocationId).displayName, "mobile-caption", 20));
            summaryCopy.Add(MobilePagesText(GameCatalog.RuleName(game.SelectedRule) + "制 · " + GameplayLayouts.Name(game.SelectedPracticeLayout), "mobile-caption", 18));
            var contract = MobilePagesText(game.RealMatch ? "真片 · 双方各押一张" : "练手 · 不转移圆片", "mobile-contract", 20);
            contract.name = "stake-contract"; contract.AddToClassList("contract-banner");
            contract.EnableInClassList("real-contract", game.RealMatch);
            contract.style.whiteSpace = WhiteSpace.Normal; contract.style.marginTop = 8;
            contract.style.paddingLeft = 8; contract.style.paddingRight = 8;
            contract.style.paddingTop = 8; contract.style.paddingBottom = 8;
            summaryCopy.Add(contract);
            if (challenge == null && !game.Training)
            {
                var greeting = MobilePagesText(GameCatalog.GetNpc(game.NpcId).displayName + "：" + CompanionPersonality.Greeting(game.NpcId), "mobile-caption", 18);
                greeting.name = "companion-greeting";
                greeting.style.whiteSpace = WhiteSpace.Normal; greeting.style.marginTop = 8;
                summaryCopy.Add(greeting);
            }
            if (game.RealMatch)
            {
                var stake = game.OpponentStake;
                summaryCopy.Add(MobilePagesText("对方押：" + (stake?.Definition.displayName ?? "暂无能押的片"), "mobile-reward", 20));
            }
            var details = MobilePagesButton(challenge == null ? "规则与场地说明" : "看看故事与提示", () => ShowMobileLobbyDetails(challenge), "mobile-secondary", 48, 20);
            details.name = "mobile-lobby-details"; summary.Add(details);

            var choices = Box("mobile-lobby-choices", true);
            choices.style.flexGrow = 1; choices.style.flexBasis = 0; choices.style.minWidth = 0; choices.style.minHeight = 0;
            main.Add(choices);
            if (mobileLobbyStep == 0) BuildMobileLobbyModes(choices, challenge);
            else if (mobileLobbyStep == 1) BuildMobileLobbyOpponents(choices, challenge);
            else BuildMobileLobbyCards(choices);
            BuildMobileLobbyStart(page);
        }

        void BuildMobileLobbyModes(VisualElement parent, ChallengeDefinition challenge)
        {
            if (challenge != null)
            {
                var fixedRules = MobilePagesPanel("mobile-page-card"); fixedRules.style.flexGrow = 1; parent.Add(fixedRules);
                fixedRules.Add(MobilePagesText("这场约战的规则已约好", "mobile-section-title", 26));
                fixedRules.Add(MobilePagesText("故事和锦标赛都玩真片，双方各押一张。", "mobile-caption", 20));
                fixedRules.Add(MobilePagesText(GameCatalog.RuleName(challenge.rule) + "制 · " + GameplayLayouts.Name((PracticeLayout)challenge.layout), "mobile-reward", 24));
                fixedRules.Add(MobilePagesText(challenge.hintLine, "mobile-caption", 20));
                var continuePick = MobilePagesButton("去选出手片", () => { mobileLobbyStep = 2; Render(); }, "mobile-primary", 56, 22);
                continuePick.style.marginTop = 12; fixedRules.Add(continuePick); return;
            }
            var modes = MobilePagesRow(parent, "mobile-lobby-modes"); modes.style.marginBottom = 8;
            AddMobileLobbyMode(modes, "自己练手", game.Training, () => game.SelectPlayMode(true, false), "choose-training");
            AddMobileLobbyMode(modes, "玩假的", !game.Training && !game.RealMatch, () => game.SelectPlayMode(false, false), "choose-fake-match");
            AddMobileLobbyMode(modes, "玩真的", game.RealMatch, () =>
            {
                game.SelectPlayMode(false, true);
                if (game.SelectedCard?.isFoundation == true) ShowPracticeRestriction();
            }, "choose-real-match");
            parent.Add(MobilePagesText("计分规则", "mobile-section-title", 22));
            var rules = MobilePagesRow(parent, "mobile-lobby-rules"); rules.style.marginBottom = 8;
            foreach (RuleKind rule in Enum.GetValues(typeof(RuleKind)))
            {
                var value = rule;
                var option = MobilePagesButton(GameCatalog.RuleName(value) + "制", () => { game.SelectedRule = value; Render(); }, "mobile-choice", 56, 22);
                option.name = "mobile-rule-" + value; option.style.flexGrow = 1; option.style.flexBasis = 0; option.style.marginRight = 6;
                option.EnableInClassList("selected", game.SelectedRule == value); rules.Add(option);
            }
            parent.Add(MobilePagesText("片阵", "mobile-section-title", 22));
            var layouts = MobilePagesRow(parent, "mobile-lobby-layouts");
            foreach (PracticeLayout layout in Enum.GetValues(typeof(PracticeLayout)))
            {
                var value = layout;
                var option = MobilePagesButton(GameplayLayouts.Name(value), () => { game.SelectedPracticeLayout = value; Render(); }, "mobile-choice", 56, 20);
                option.name = "mobile-layout-" + value; option.style.flexGrow = 1; option.style.flexBasis = 0;
                option.style.paddingLeft = 3; option.style.paddingRight = 3; option.style.marginRight = 5;
                option.EnableInClassList("selected", game.SelectedPracticeLayout == value); layouts.Add(option);
            }
        }

        void AddMobileLobbyMode(VisualElement parent, string caption, bool selected, Action action, string name)
        {
            var button = MobilePagesButton(caption, action, "mobile-choice", 56, 22);
            button.name = name; button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.marginRight = 6;
            button.EnableInClassList("selected", selected); parent.Add(button);
        }

        void BuildMobileLobbyOpponents(VisualElement parent, ChallengeDefinition challenge)
        {
            if (challenge != null)
            {
                var panel = MobilePagesPanel("mobile-page-card"); panel.style.flexGrow = 1; parent.Add(panel);
                var npc = GameCatalog.GetNpc(challenge.npcId);
                panel.Add(MobilePagesText(npc.displayName + "在等你", "mobile-section-title", 28));
                panel.Add(MobilePagesText(GameCatalog.GetLocation(challenge.locationId).displayName, "mobile-reward", 24));
                panel.Add(MobilePagesText(npc.introduction, "mobile-caption", 20));
                panel.Add(MobilePagesText("故事与锦标赛的对手、场地和片阵随约定固定。", "mobile-caption", 18));
                var continuePick = MobilePagesButton("去选出手片", () => { mobileLobbyStep = 2; Render(); }, "mobile-primary", 56, 22);
                continuePick.style.marginTop = 12; panel.Add(continuePick); return;
            }
            parent.Add(MobilePagesText("选位伙伴 · 场地跟着伙伴走", "mobile-section-title", 22));
            int pages = Mathf.Max(1, Mathf.CeilToInt(GameCatalog.Npcs.Length / 4f));
            mobileOpponentPage = Mathf.Clamp(mobileOpponentPage, 0, pages - 1);
            var visible = GameCatalog.Npcs.Skip(mobileOpponentPage * 4).Take(4).ToArray();
            var grid = Box("mobile-opponent-grid"); grid.style.flexGrow = 1; grid.style.minHeight = 0; parent.Add(grid);
            for (int rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                var row = MobilePagesRow(grid, "mobile-opponent-row");
                row.style.flexGrow = 1; row.style.flexBasis = 0; row.style.minHeight = 0; row.style.marginBottom = 6;
                for (int column = 0; column < 2; column++)
                {
                    int index = rowIndex * 2 + column;
                    if (index >= visible.Length) { AddMobilePagesBlank(row); continue; }
                    var npc = visible[index]; string id = npc.id;
                    var option = Click("", () => game.SelectNpc(id), "opponent-card");
                    option.name = "opponent-" + id; option.AddToClassList("mobile-opponent-card");
                    MobilePagesCardSize(option); option.style.paddingTop = 4; option.style.paddingBottom = 4;
                    option.Add(MobilePagesText(npc.displayName, "mobile-section-title", 24));
                    option.Add(MobilePagesText(GameCatalog.GetLocation(npc.locationId).displayName, "mobile-caption", 18));
                    option.EnableInClassList("selected", game.NpcId == id && !game.Training); row.Add(option);
                }
            }
            MobilePager(parent, mobileOpponentPage, pages, next => { mobileOpponentPage = next; Render(); });
        }

        void BuildMobileLobbyCards(VisualElement parent)
        {
            parent.Add(MobilePagesText("选一张出手片", "mobile-section-title", 22));
            var cards = game.Repository.PlayerCards;
            int pages = Mathf.Max(1, Mathf.CeilToInt(cards.Count / 3f));
            mobileStakePage = Mathf.Clamp(mobileStakePage, 0, pages - 1);
            var grid = MobilePagesRow(parent, "mobile-stake-grid"); grid.style.flexGrow = 1; grid.style.minHeight = 0;
            if (cards.Count == 0)
                grid.Add(MobilePagesText("铁盒空了。\n接一趟跑腿，再去小卖部换张片。", "mobile-empty", 24));
            foreach (var instance in cards.Skip(mobileStakePage * 3).Take(3))
            {
                string id = instance.instanceId;
                var option = MobilePagesPog(instance.Definition, () => game.SelectCard(id), "mobile-stake-card", 76);
                option.name = "card-" + id; option.AddToClassList("instance-choice");
                option.EnableInClassList("selected", game.SelectedInstanceId == id);
                string status = instance.isFoundation ? "练习片" : instance.treasured ? "珍藏 · 已保护" :
                    game.Repository.IsCardLocked(id) ? "约战中 · 锁定" : "真片 · 可押注";
                option.Add(MobilePagesText(status, "mobile-card-status", 18));
                grid.Add(option);
            }
            MobilePager(parent, mobileStakePage, pages, next => { mobileStakePage = next; Render(); });
        }

        void BuildMobileLobbyStart(VisualElement page)
        {
            var selected = game.SelectedCard;
            bool invalid = selected == null || game.RealMatch && (selected.isFoundation || selected.treasured || game.Repository.IsCardLocked(selected.instanceId));
            var bottom = MobilePagesRow(page, "mobile-lobby-footer");
            bottom.style.height = 64; bottom.style.flexShrink = 0; bottom.style.marginTop = 8; bottom.style.alignItems = Align.Center;
            var info = Box("mobile-lobby-selected"); info.style.flexGrow = 1; info.style.minWidth = 0; bottom.Add(info);
            var name = MobilePagesText("出手片：" + (selected?.Definition.displayName ?? "尚未选择"), "mobile-section-title", 22);
            name.style.whiteSpace = WhiteSpace.NoWrap; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; info.Add(name);
            string help = selected == null ? "跑腿和小卖部都能补给" : game.RealMatch && selected.isFoundation ? "练习片不能玩真的" :
                game.RealMatch && selected.treasured ? "珍藏片受到保护，请换一张" : game.RealMatch && game.Repository.IsCardLocked(selected.instanceId) ? "这张已被未完约战锁定" :
                selected.Definition.weightFeel + " / " + selected.Definition.gripFeel;
            var guidance = MobilePagesText(help, "mobile-caption", 18);
            guidance.style.whiteSpace = WhiteSpace.NoWrap; guidance.style.overflow = Overflow.Hidden;
            guidance.style.textOverflow = TextOverflow.Ellipsis; info.Add(guidance);
            if (invalid)
            {
                var shop = MobilePagesButton("小卖部", game.ShowShop, "mobile-secondary", 56, 20);
                shop.name = "lobby-shop"; shop.style.width = 94; shop.style.marginLeft = 8; bottom.Add(shop);
                var errands = MobilePagesButton("跑腿", game.ShowErrands, "mobile-secondary", 56, 20);
                errands.name = "lobby-errands"; errands.style.width = 84; errands.style.marginLeft = 6; bottom.Add(errands);
            }
            var start = MobilePagesButton(game.RealMatch ? "各押这张 · 开始约战" : game.Training ? "开始练手" : "开始切磋", game.StartPogs, "mobile-primary", 56, 22);
            start.name = "start-pogs"; start.style.width = 260; start.style.marginLeft = 10;
            start.SetEnabled(!invalid); bottom.Add(start);
        }

        void ShowMobileLobbyDetails(ChallengeDefinition challenge)
        {
            ShowMobileSheet(challenge == null ? "规则与场地说明" : challenge.title, body =>
            {
                if (challenge != null)
                {
                    body.Add(MobilePagesText(challenge.introLine, "mobile-detail-copy", 22));
                    body.Add(MobilePagesText(challenge.hintLine, "mobile-detail-copy", 20));
                }
                body.Add(MobilePagesText(game.World.Terrain.Description, "mobile-detail-copy", 20));
                var environment = game.World.Environment;
                body.Add(MobilePagesText(GameCatalog.PeriodName(environment.period) + " · " + WeatherLabel(environment.weather) +
                    (environment.weather == WeatherKind.Rainy && environment.frictionMultiplier < 1 ? " · 雨天地面稍滑，井盖更滑" : " · 开局后的场地和天气保持不变"), "mobile-detail-copy", 20));
                if (challenge == null && !game.Training)
                    body.Add(MobilePagesText(GameCatalog.GetNpc(game.NpcId).displayName + "擅长" + GameCatalog.ShotName(GameCatalog.GetNpc(game.NpcId).preferredShot), "mobile-detail-copy", 20));
                body.Add(MobilePagesText("翻面要等圆片停稳；出圈要整张越过边界。\n场上五张是计分目标。玩真的只交接开局约定的那一张。\n练习片和珍藏片不能押注，暂停和退出会保存未完约战。", "mobile-detail-copy", 20));
            });
        }

        void BuildMobileStory()
        {
            var page = MobilePage("街区故事"); page.name = "mobile-story";
            var save = game.Repository.Current;
            var status = MobilePagesRow(page, "mobile-story-status");
            status.style.height = 46; status.style.flexShrink = 0; status.style.alignItems = Align.Center; status.style.marginBottom = 8;
            var caption = MobilePagesText("已完成 " + save.story.clearedStageIds.Count + " / 8 · 每场双方各押一张真片", "mobile-caption", 20);
            caption.style.flexGrow = 1; status.Add(caption);
            var supply = MobilePagesButton("跑腿补给", game.ShowErrands, "mobile-secondary", 46, 20);
            supply.name = "story-supply"; supply.style.width = 136; status.Add(supply);
            int pages = Mathf.Max(1, Mathf.CeilToInt(CampaignCatalog.Stages.Length / 2f));
            mobileStoryPage = Mathf.Clamp(mobileStoryPage, 0, pages - 1);
            var grid = MobilePagesRow(page, "mobile-story-grid"); grid.style.flexGrow = 1; grid.style.minHeight = 0;
            foreach (var stage in CampaignCatalog.Stages.Skip(mobileStoryPage * 2).Take(2))
            {
                bool unlocked = stage.requiredStageIds.All(id => save.story.clearedStageIds.Contains(id));
                bool cleared = save.story.clearedStageIds.Contains(stage.id); string id = stage.id;
                var card = Click("", () => game.OpenStoryStage(id), "story-card");
                card.name = "stage-" + id; card.AddToClassList("mobile-story-card");
                MobilePagesCardSize(card); card.style.alignItems = Align.FlexStart;
                card.style.paddingLeft = 18; card.style.paddingRight = 18;
                card.EnableInClassList("boss-card", stage.bossLevel > 0); card.SetEnabled(unlocked);
                card.Add(MobilePagesText(id.Substring(id.Length - 2) + "  /  " + (cleared ? "已完成" : unlocked ? "可以赴约" : "未解锁"), "mobile-kicker", 20));
                card.Add(MobilePagesText(stage.title, "mobile-section-title", 26));
                card.Add(MobilePagesText(GameCatalog.GetLocation(stage.locationId).displayName + " · " + GameCatalog.GetNpc(stage.npcId).displayName + " · " + GameCatalog.RuleName(stage.rule), "mobile-caption", 18));
                var description = MobilePagesText(stage.description, "mobile-detail-copy", 20);
                description.style.marginTop = 8; card.Add(description);
                var spacer = Box("mobile-page-space"); spacer.style.flexGrow = 1; card.Add(spacer);
                card.Add(MobilePagesText(cleared ? "可以再次赴约" : "首次胜利 +" + stage.rewardMarbles + " 颗玻璃珠" + (stage.bossLevel > 0 ? " · 招牌片" : ""), "mobile-reward", 20));
                card.Add(MobilePagesText(unlocked ? "去赴约  →" : "先完成上一场", "mobile-story-action", 22));
                grid.Add(card);
            }
            MobilePager(page, mobileStoryPage, pages, next => { mobileStoryPage = next; Render(); });
        }

        void BuildMobileTournament()
        {
            var page = MobilePage("盛夏锦标赛"); page.name = "mobile-tournament";
            var run = game.Repository.Current.tournament;
            var main = MobilePagesRow(page, "mobile-tournament-main"); main.style.flexGrow = 1; main.style.minHeight = 0;
            var hero = MobilePagesPanel("mobile-tournament-hero");
            hero.style.flexGrow = 1; hero.style.flexBasis = 0; hero.style.minWidth = 0; hero.style.marginRight = 10; main.Add(hero);
            bool entered = run != null && !string.IsNullOrEmpty(run.runId);
            hero.Add(MobilePagesText(entered ? run.champion ? "冠军：你！" : run.active ? "当前第 " + (run.roundIndex + 1) + " 轮 / 3" : "本届已结束" : "八个人的盛夏", "mobile-section-title", 30));
            hero.Add(MobilePagesText("三轮真片约战 · 冠军奖励 24 颗玻璃珠", "mobile-reward", 22));
            if (entered && run.active)
            {
                var stage = CampaignCatalog.GetTournamentStage(run.roundIndex);
                hero.Add(MobilePagesText("这一轮：" + GameCatalog.GetNpc(stage.npcId).displayName, "mobile-section-title", 26));
                hero.Add(MobilePagesText(GameCatalog.GetLocation(stage.locationId).displayName + " · " + GameCatalog.RuleName(stage.rule) + "制", "mobile-caption", 22));
                hero.Add(MobilePagesText(stage.hintLine, "mobile-detail-copy", 20));
            }
            else hero.Add(MobilePagesText(entered && run.champion ? "三轮都赢下来了，街区伙伴为你敲响铁盒。还能再报名，继续下一届夏日约战。" :
                "七位伙伴等你上场，故事进度不影响报名。", "mobile-detail-copy", 22));
            hero.Add(MobilePagesText("双方各押一张真片，签表会自动保存。", "mobile-caption", 18));

            var path = MobilePagesPanel("mobile-tournament-path");
            path.style.width = Length.Percent(37); path.style.flexShrink = 0; main.Add(path);
            path.Add(MobilePagesText("晋级路线", "mobile-section-title", 24));
            for (int i = 0; i < CampaignCatalog.TournamentStages.Length; i++)
            {
                var stage = CampaignCatalog.TournamentStages[i];
                var card = MobilePagesPanel("mobile-tournament-round"); card.AddToClassList("story-card");
                card.style.width = Length.Percent(100); card.style.height = 78; card.style.minHeight = 0;
                card.style.marginTop = 6; card.style.marginBottom = 0; card.style.paddingTop = 6; card.style.paddingBottom = 6;
                card.style.marginRight = 0;
                var round = entered ? run.rounds.FirstOrDefault(item => item.roundIndex == i) : null;
                string status = round?.completed == true ? round.outcome == MatchOutcome.PlayerWin ? "已获胜" : "已结束" :
                    entered && i == run.roundIndex && run.active ? "等你上场" : "等待晋级";
                card.EnableInClassList("selected", entered && run.active && i == run.roundIndex);
                card.Add(MobilePagesText((i == 0 ? "八强" : i == 1 ? "半决赛" : "决赛") + " · " + GameCatalog.GetNpc(stage.npcId).displayName, "mobile-section-title", 22));
                card.Add(MobilePagesText(status + " · " + GameCatalog.RuleName(stage.rule) + "制", "mobile-caption", 18));
                path.Add(card);
            }
            var actions = MobilePagesRow(page, "mobile-tournament-actions");
            actions.style.height = 64; actions.style.flexShrink = 0; actions.style.marginTop = 10; actions.style.alignItems = Align.Center;
            var enter = MobilePagesButton(!entered ? "报名 · 八个人的盛夏" : run.active ? "赴约 · 开始这一轮" : "再报名一届锦标赛",
                entered && run.active ? (Action)game.OpenTournamentRound : game.EnterTournament, "mobile-primary", 56, 22);
            enter.name = entered ? "next-tournament-round" : "enter-tournament"; enter.style.width = 330; actions.Add(enter);
            if (entered)
            {
                var bracket = MobilePagesButton("查看完整签表", ShowMobileTournamentBracket, "mobile-secondary", 56, 22);
                bracket.name = "mobile-tournament-bracket"; bracket.style.width = 230; bracket.style.marginLeft = 10; actions.Add(bracket);
            }
        }

        void ShowMobileTournamentBracket()
        {
            var run = game.Repository.Current.tournament;
            if (run == null || string.IsNullOrEmpty(run.runId)) return;
            ShowMobileSheet("八人完整签表", body =>
            {
                foreach (var item in run.bracket)
                {
                    string left = item.leftOwnerId == "player" ? "你" : GameCatalog.GetNpc(item.leftOwnerId).displayName;
                    string right = item.rightOwnerId == "player" ? "你" : GameCatalog.GetNpc(item.rightOwnerId).displayName;
                    string winner = string.IsNullOrEmpty(item.winnerOwnerId) ? "待比赛" : item.winnerOwnerId == "draw" ? "平局，重赛" :
                        item.winnerOwnerId == "player" ? "你获胜" : GameCatalog.GetNpc(item.winnerOwnerId).displayName + "获胜";
                    var entry = MobilePagesPanel("mobile-bracket-entry"); entry.style.marginBottom = 8; body.Add(entry);
                    entry.Add(MobilePagesText("第 " + (item.roundIndex + 1) + " 轮  ·  " + left + "  对  " + right, "mobile-section-title", 22));
                    entry.Add(MobilePagesText(winner, "mobile-reward", 20));
                }
            });
        }

        void BuildMobileShop()
        {
            var page = MobilePage("小卖部"); page.name = "mobile-shop";
            var tabs = MobilePagesRow(page, "mobile-shop-tabs");
            tabs.style.height = 56; tabs.style.flexShrink = 0; tabs.style.marginBottom = 10;
            string[] labels = { "兑换圆片", "卖出我的片", "找伙伴换片" };
            for (int i = 0; i < labels.Length; i++)
            {
                int tab = i;
                var choice = MobilePagesButton(labels[i], () => { shopTab = tab; mobileShopPage = 0; Render(); }, "mobile-choice", 56, 22);
                choice.name = "shop-tab-" + i; choice.style.flexGrow = 1; choice.style.flexBasis = 0;
                choice.style.marginRight = i < 2 ? 8 : 0; choice.EnableInClassList("selected", i == shopTab); tabs.Add(choice);
            }
            int count = shopTab == 0 ? CampaignCatalog.ShopOffers.Length : shopTab == 1 ? game.Repository.PlayerCards.Count : CampaignCatalog.SwapOffers.Length;
            int pages = Mathf.Max(1, Mathf.CeilToInt(count / 3f)); mobileShopPage = Mathf.Clamp(mobileShopPage, 0, pages - 1);
            var grid = MobilePagesRow(page, "mobile-shop-grid"); grid.style.flexGrow = 1; grid.style.minHeight = 0;
            var save = game.Repository.Current;
            if (shopTab == 0)
            {
                foreach (var offer in CampaignCatalog.ShopOffers.Skip(mobileShopPage * 3).Take(3))
                {
                    string id = offer.id; bool unlocked = CampaignCatalog.IsOfferUnlocked(save, offer);
                    var card = MobilePagesPog(GameCatalog.GetCard(offer.cardDefinitionId), () => ShowMobileTradePreview(TradeKind.Buy, id), "mobile-shop-card", 92);
                    card.name = "buy-" + id; card.AddToClassList("shop-card");
                    card.Add(MobilePagesText(offer.buyPrice + " 颗玻璃珠", "mobile-price", 26));
                    card.Add(MobilePagesText(!unlocked ? "先战胜招牌片的 Boss" : offer.isFoundation ? "练习片 · 可以练手" : "真片 · 可以押注", "mobile-caption", 18));
                    card.Add(MobilePagesText(!unlocked ? "尚未上架" : save.glassMarbles < offer.buyPrice ? "玻璃珠不足" : "查看交易 →", "mobile-card-action", 20));
                    card.SetEnabled(unlocked && save.glassMarbles >= offer.buyPrice); grid.Add(card);
                }
            }
            else if (shopTab == 1)
            {
                foreach (var instance in game.Repository.PlayerCards.Skip(mobileShopPage * 3).Take(3))
                {
                    string id = instance.instanceId; bool locked = game.Repository.IsCardLocked(id);
                    var card = MobilePagesPog(instance.Definition, () => ShowMobileTradePreview(TradeKind.Sell, "shop", id), "mobile-shop-card", 92);
                    card.name = "sell-" + id; card.AddToClassList("shop-card");
                    int price = instance.isFoundation ? 1 : CampaignCatalog.SellPrice(instance.Definition);
                    card.Add(MobilePagesText("换 " + price + " 颗玻璃珠", "mobile-price", 26));
                    card.Add(MobilePagesText(locked ? "对局已锁定" : instance.isFoundation ? "练习片" : instance.treasured ? "舍不得 · 本次需确认" : instance.WearDescription, "mobile-caption", 18));
                    card.Add(MobilePagesText(locked ? "暂不能转手" : "查看交易 →", "mobile-card-action", 20));
                    card.SetEnabled(!locked); grid.Add(card);
                }
                if (count == 0) grid.Add(MobilePagesText("铁盒是空的。\n接一趟跑腿，或切到兑换页。", "mobile-empty", 24));
            }
            else
            {
                foreach (var offer in CampaignCatalog.SwapOffers.Skip(mobileShopPage * 3).Take(3))
                {
                    var owned = game.Repository.PlayerCards.FirstOrDefault(c => c.definitionId == offer.wantedDefinitionId && !game.Repository.IsCardLocked(c.instanceId) && !c.isFoundation);
                    string id = offer.id, instanceId = owned?.instanceId;
                    bool usedToday = save.transactions.Any(receipt => receipt.kind == TradeKind.Swap && receipt.offerId == id && receipt.day == save.progress.day);
                    bool inStock = game.Repository.NpcCards(offer.npcId).Any(c => c.definitionId == offer.offeredDefinitionId && !game.Repository.IsCardLocked(c.instanceId));
                    var card = MobilePagesPog(GameCatalog.GetCard(offer.offeredDefinitionId), () => ShowMobileTradePreview(TradeKind.Swap, id, instanceId), "mobile-shop-card", 80);
                    card.name = "swap-" + id; card.AddToClassList("shop-card");
                    card.Add(MobilePagesText(GameCatalog.GetNpc(offer.npcId).displayName + "想要：" + GameCatalog.GetCard(offer.wantedDefinitionId).displayName, "mobile-swap-request", 20));
                    card.Add(MobilePagesText(usedToday ? "今天已换过" : !inStock ? "伙伴暂时缺这张" : owned == null ? "还没有清单里的那张" : "同稀有度 · 一换一", "mobile-caption", 18));
                    card.Add(MobilePagesText(owned != null && !usedToday && inStock ? "查看交换 →" : "暂不能交换", "mobile-card-action", 20));
                    card.SetEnabled(owned != null && !usedToday && inStock); grid.Add(card);
                }
            }
            MobilePager(page, mobileShopPage, pages, next => { mobileShopPage = next; Render(); });
        }

        void ShowMobileTradePreview(TradeKind kind, string offerId, string playerCardId = null)
        {
            try
            {
                var quote = game.Repository.GetTradeQuote(kind, offerId, playerCardId);
                var definition = GameCatalog.GetCard(quote.cardDefinitionId);
                var incoming = quote.npcCardId == null ? null : game.Repository.GetInstance(quote.npcCardId);
                ShowMobileSheet(kind == TradeKind.Buy ? "确认兑换" : kind == TradeKind.Sell ? "确认卖出" : "确认换片", body =>
                {
                    var preview = MobilePagesRow(body, "mobile-trade-preview");
                    preview.style.marginBottom = 10;
                    var outgoing = MobilePagesPanel("mobile-trade-item");
                    outgoing.style.flexGrow = 1; outgoing.style.flexBasis = 0; preview.Add(outgoing);
                    AddMobileTradeArt(outgoing, definition);
                    outgoing.Add(MobilePagesText(kind == TradeKind.Buy ? quote.isFoundation ? "买入练习片" : "买入真片" : "交出你手里的这一张", "mobile-caption", 20));
                    if (incoming != null)
                    {
                        var next = MobilePagesPanel("mobile-trade-item"); next.style.flexGrow = 1; next.style.flexBasis = 0;
                        next.style.marginLeft = 10; preview.Add(next); AddMobileTradeArt(next, incoming.Definition);
                        next.Add(MobilePagesText("收到" + GameCatalog.GetNpc(quote.npcId).displayName + "的这一张", "mobile-caption", 20));
                    }
                    string price = quote.marbleDelta < 0 ? "花费 " + (-quote.marbleDelta) + " 颗玻璃珠" : quote.marbleDelta > 0 ? "收到 " + quote.marbleDelta + " 颗玻璃珠" : "同稀有度一换一 · 不花玻璃珠";
                    body.Add(MobilePagesText(price, "mobile-price", 26));
                    body.Add(MobilePagesText("交易后余额：" + (game.Repository.Current.glassMarbles + quote.marbleDelta) + " 颗", "mobile-caption", 20));
                    if (kind == TradeKind.Sell)
                    {
                        var instance = game.Repository.GetInstance(playerCardId);
                        body.Add(MobilePagesText(instance.isFoundation ? "练习片 · 可以交易" : instance.treasured ? "珍藏片 · 需再次确认" : instance.WearDescription, "mobile-detail-copy", 18));
                    }
                    if (quote.requiresTreasuredConfirmation)
                        body.Add(MobilePagesText("涉及珍藏片，下一步还会确认这一次转手。", "mobile-reward", 20));
                    var actions = MobilePagesRow(body, "mobile-trade-actions"); actions.style.marginTop = 12;
                    actions.AddToClassList("mobile-fixed-sheet-actions");
                    var cancel = MobilePagesButton("再看看", CloseDismissOverlay, "mobile-secondary", 56, 22);
                    cancel.style.flexGrow = 1; cancel.style.flexBasis = 0; actions.Add(cancel);
                    var confirm = MobilePagesButton(kind == TradeKind.Buy ? "确认兑换" : kind == TradeKind.Sell ? "确认卖出" : "确认交换", () =>
                    {
                        CloseDismissOverlay(); PrepareTrade(kind, offerId, playerCardId);
                    }, "mobile-primary", 56, 22);
                    confirm.name = "mobile-confirm-trade"; confirm.style.flexGrow = 1; confirm.style.flexBasis = 0; confirm.style.marginLeft = 10; actions.Add(confirm);
                });
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }

        void AddMobileTradeArt(VisualElement parent, CardDefinition definition)
        {
            var row = MobilePagesRow(parent, "mobile-trade-item-heading"); row.style.alignItems = Align.Center;
            var art = Box("mobile-trade-art"); art.style.width = 90; art.style.height = 90; art.style.flexShrink = 0;
            art.style.marginRight = 12; art.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(definition.artResourcePath)); row.Add(art);
            var title = MobilePagesText(definition.displayName + "\n" + GameCatalog.RarityName(definition.rarity), "mobile-section-title", 24);
            title.style.flexGrow = 1; row.Add(title);
        }

        void BuildMobileErrands()
        {
            var page = MobilePage("街坊的小委托"); page.name = "mobile-errands";
            var active = game.Repository.Current.activeErrand;
            if (active != null)
            {
                var definition = CampaignCatalog.Errands.First(item => item.id == active.errandId);
                string destination = active.destinationLocationId, completion = active.completionId;
                bool canDeliver = active.destinationVisited && game.LocationId == destination;
                var card = MobilePagesPanel("mobile-active-errand"); card.AddToClassList("errand-card");
                card.style.width = Length.Percent(82); card.style.alignSelf = Align.Center; card.style.flexGrow = 1;
                card.style.paddingLeft = 22; card.style.paddingRight = 22; card.style.marginBottom = 0; page.Add(card);
                card.Add(MobilePagesText("已经接下 · " + (canDeliver ? "可以交付" : active.destinationVisited ? "回到目的地交付" : "等你送到"), "mobile-kicker", 20));
                card.Add(MobilePagesText(definition.title, "mobile-section-title", 30));
                card.Add(MobilePagesText(definition.acceptLine, "mobile-detail-copy", 22));
                card.Add(MobilePagesText("送到：" + GameCatalog.GetLocation(destination).displayName, "mobile-section-title", 24));
                card.Add(MobilePagesText("奖励 6 颗玻璃珠", "mobile-reward", 22));
                var spacer = Box("mobile-page-space"); spacer.style.flexGrow = 1; card.Add(spacer);
                var deliver = MobilePagesButton(canDeliver ? "交付 · 领 6 颗" : "前往目的地", () =>
                {
                    if (canDeliver) game.CompleteErrand(completion); else game.SelectLocation(destination);
                }, "mobile-primary", 56, 22);
                deliver.name = canDeliver ? "deliver-errand" : "errand-go-destination";
                deliver.style.width = 350; card.Add(deliver);
                card.Add(MobilePagesText(canDeliver ? "交付后进入下一时段。" : "到达后，打开“跑腿”交付这次委托。", "mobile-caption", 18));
                return;
            }
            var caption = MobilePagesText("接一趟 → 送到目的地 → 交付领 6 颗 · 不用任何圆片", "mobile-caption", 20);
            caption.style.marginBottom = 10; page.Add(caption);
            int pages = Mathf.Max(1, Mathf.CeilToInt(CampaignCatalog.Errands.Length / 2f));
            mobileErrandPage = Mathf.Clamp(mobileErrandPage, 0, pages - 1);
            var grid = MobilePagesRow(page, "mobile-errand-grid"); grid.style.flexGrow = 1; grid.style.minHeight = 0;
            foreach (var errand in CampaignCatalog.Errands.Skip(mobileErrandPage * 2).Take(2))
            {
                string id = errand.id; var card = MobilePagesPanel("mobile-errand-card"); card.AddToClassList("errand-card");
                MobilePagesCardSize(card); card.style.paddingLeft = 18; card.style.paddingRight = 18; card.style.paddingBottom = 16; grid.Add(card);
                card.Add(MobilePagesText(errand.title, "mobile-section-title", 28));
                card.Add(MobilePagesText(errand.description.TrimEnd('。'), "mobile-detail-copy", 22));
                card.Add(MobilePagesText("送到：" + GameCatalog.GetLocation(errand.destinationLocationId).displayName, "mobile-caption", 20));
                card.Add(MobilePagesText("奖励 6 颗玻璃珠", "mobile-reward", 24));
                var spacer = Box("mobile-page-space"); spacer.style.flexGrow = 1; card.Add(spacer);
                var accept = MobilePagesButton("接下这一趟", () => game.AcceptErrand(id), "mobile-primary", 56, 22);
                accept.name = "accept-" + id; card.Add(accept);
            }
            MobilePager(page, mobileErrandPage, pages, next => { mobileErrandPage = next; Render(); });
        }

        static Label MobilePagesText(string value, string className, int size)
        {
            var label = Text(value, className); label.style.fontSize = size;
            label.style.minWidth = 0; label.style.maxWidth = Length.Percent(100);
            label.style.whiteSpace = WhiteSpace.Normal; label.style.flexShrink = 0;
            label.style.marginTop = 2; label.style.marginBottom = 3;
            return label;
        }

        Button MobilePagesButton(string value, Action action, string className, int height, int fontSize)
        {
            var button = Click(value, action, className);
            button.style.height = height; button.style.minHeight = height; button.style.flexShrink = 0;
            button.style.fontSize = fontSize; button.style.minWidth = 0;
            button.style.marginTop = 0; button.style.marginBottom = 0;
            button.style.paddingLeft = 8; button.style.paddingRight = 8;
            return button;
        }

        static VisualElement MobilePagesRow(VisualElement parent, string className)
        {
            var row = Box(className); row.style.flexDirection = FlexDirection.Row;
            row.style.minWidth = 0; row.style.alignItems = Align.Stretch; parent.Add(row); return row;
        }

        static VisualElement MobilePagesPanel(string className)
        {
            var panel = Box(className, true); panel.AddToClassList("mobile-page-card");
            panel.style.minWidth = 0; panel.style.minHeight = 0;
            panel.style.paddingLeft = 12; panel.style.paddingRight = 12;
            panel.style.paddingTop = 10; panel.style.paddingBottom = 10;
            return panel;
        }

        static void MobilePagesCardSize(VisualElement card)
        {
            card.style.width = StyleKeyword.Auto; card.style.height = StyleKeyword.Auto;
            card.style.minWidth = 0; card.style.minHeight = 0;
            card.style.flexGrow = 1; card.style.flexBasis = 0; card.style.flexShrink = 1;
            card.style.marginTop = 0; card.style.marginBottom = 0;
            card.style.marginLeft = 0; card.style.marginRight = 8;
            card.style.paddingLeft = 10; card.style.paddingRight = 10;
            card.style.paddingTop = 10; card.style.paddingBottom = 10;
            card.style.justifyContent = Justify.FlexStart;
        }

        Button MobilePagesPog(CardDefinition definition, Action action, string className, int artSize)
        {
            var card = PogChoice(definition, action); card.AddToClassList(className);
            MobilePagesCardSize(card); card.style.alignItems = Align.Center;
            var art = card.Q(className: "pog-art");
            art.style.width = artSize; art.style.height = artSize; art.style.flexShrink = 0;
            art.style.marginTop = 0; art.style.marginBottom = 4;
            var title = card.Q<Label>(className: "pog-name");
            title.style.fontSize = 24; title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minWidth = 0; title.style.maxWidth = Length.Percent(100);
            title.style.marginTop = 2; title.style.marginBottom = 4; title.style.flexShrink = 0;
            return card;
        }

        static void AddMobilePagesBlank(VisualElement row)
        {
            var blank = Box("mobile-page-blank"); blank.style.flexGrow = 1; blank.style.flexBasis = 0;
            blank.style.marginRight = 8; row.Add(blank);
        }
    }
}
