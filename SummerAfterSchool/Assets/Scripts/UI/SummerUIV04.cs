using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    public sealed partial class SummerUI
    {
        PanelSettings alertSettings;
        VisualElement alertRoot;
        Label displayStatus;
        Button displayConfirm, displayRevert;
        Button[] displayModes;
        int shopTab;
        float alertPreviousScale = 1;
        public bool AlertVisible => alertRoot != null && alertRoot.childCount > 0;
        public string AlertMessage { get; private set; }
        public bool SettingsVisible => audioOverlay != null;
        public bool ExitVisible => exitOverlay != null;
        public bool PauseVisible => pauseOverlay != null;

        void InitializeAlerts()
        {
            alertSettings = ScriptableObject.CreateInstance<PanelSettings>();
            alertSettings.scaleMode = settings.scaleMode; alertSettings.referenceResolution = settings.referenceResolution;
            alertSettings.screenMatchMode = settings.screenMatchMode; alertSettings.match = settings.match;
            alertSettings.sortingOrder = 200; alertSettings.themeStyleSheet = settings.themeStyleSheet;
            alertSettings.clearColor = false; alertSettings.clearDepthStencil = false;
            var owner = new GameObject("放学后 · 最上层提示"); owner.transform.SetParent(transform, false); owner.SetActive(false);
            var alertDocument = owner.AddComponent<UIDocument>(); alertDocument.panelSettings = alertSettings; owner.SetActive(true);
            alertRoot = alertDocument.rootVisualElement; alertRoot.pickingMode = PickingMode.Ignore;
            if (font != null) alertRoot.style.unityFontDefinition = FontDefinition.FromFont(font);
            var css = Resources.Load<StyleSheet>("UI/Summer"); if (css != null) alertRoot.styleSheets.Add(css);
            InitializeMobileAlertPanel();
        }

        VisualElement AlertPanel(string title, string body)
        {
            if (!AlertVisible)
            {
                alertPreviousScale = Time.timeScale;
                if (game.Screen == AppScreen.Pogs) { game.Pogs.CancelAim(); Time.timeScale = 0; }
            }
            alertRoot.Clear(); AlertMessage = body;
            var overlay = Box("overlay", true); overlay.name = "priority-alert"; alertRoot.Add(overlay);
            var panel = Box("priority-panel", true); overlay.Add(panel);
            VisualElement content = panel;
            if (mobileUI)
            {
                var scroll = new ScrollView(); scroll.AddToClassList("priority-scroll"); panel.Add(scroll); content = scroll;
            }
            content.Add(Text(title, "modal-title")); content.Add(Text(body, "priority-copy"));
            UpdateShotSetupUI(game.Pogs);
            return content;
        }

        public void CloseAlert()
        {
            if (!AlertVisible) return;
            alertRoot.Clear(); AlertMessage = null; Time.timeScale = alertPreviousScale;
            UpdateShotSetupUI(game.Pogs);
        }

        public void ShowNotice(string title, string body)
        {
            var panel = AlertPanel(title, body);
            panel.Add(Click("知道了", CloseAlert, "primary"));
        }

        public void ShowPracticeRestriction()
        {
            game.SetError("练习片不能玩真的，请选择真片。");
            var panel = AlertPanel("这张是练习片", "练习片不能玩真的，请选择真片。\n故事和锦标赛双方都押真片，输了会把约定的这一张留在对方手里。");
            var pick = Click("选择真片", () =>
            {
                CloseAlert();
                var eligible = game.Repository.PlayerCards.FirstOrDefault(card => game.Repository.CanStake(card.instanceId));
                if (eligible == null) { ShowNoCards(); return; }
                game.SelectCard(eligible.instanceId);
            }, "primary");
            pick.name = "choose-real-card"; panel.Add(pick);
            var shop = Click("去兑换真片", () => { CloseAlert(); game.ShowShop(); }, "secondary");
            shop.name = "exchange-real-card"; panel.Add(shop);
            panel.Add(Click("回去看看", CloseAlert, "small-button"));
        }

        public void ShowNoCards()
        {
            var panel = AlertPanel("铁盒先空一会儿", "没有能押的真片也能继续这个夏天。\n接一趟跑腿，赚 6 颗玻璃珠，再去小卖部换一张普通真片。");
            panel.Add(Click("去接跑腿", () => { CloseAlert(); game.ShowErrands(); }, "primary"));
            panel.Add(Click("去小卖部", () => { CloseAlert(); game.ShowShop(); }, "secondary"));
            panel.Add(Click("回街区", () => { CloseAlert(); game.GoHub(); }, "small-button"));
        }

        static string WeatherLabel(WeatherKind weather) => weather == WeatherKind.Rainy ? "下雨" : weather == WeatherKind.Cloudy ? "阴天" : "晴天";
        string ClockLabel()
        {
            var progress = game.Repository.Current.progress;
            return "第 " + progress.day + " 天 · " + GameCatalog.PeriodName(progress.period) + " · " +
                WeatherLabel(progress.environment?.weather ?? WeatherKind.Sunny);
        }
        string WalletLabel() => "玻璃珠  " + game.Repository.Current.glassMarbles + "  颗";
        void AddWallet(VisualElement parent) { parent.Add(Text(WalletLabel(), "wallet-pill")); }

        void BuildHub()
        {
            if (mobileUI) { BuildMobileHub(); return; }
            Topbar();
            var hero = Box("hub-hero", true); hero.AddToClassList("v04-hero"); screen.Add(hero);
            hero.Add(Text("一九九六 · 夏天 · " + Application.version, "eyebrow")); hero.Add(Text("放学后", "hero-title"));
            hero.Add(Text("把一整个夏天，装进铁盒。", "hero-subtitle"));
            hero.Add(Text(GameCatalog.GetLocation(game.LocationId).description, "body"));
            var pending = game.Repository.GetPendingMatch();
            if (pending != null)
            {
                var resume = Click("继续那场约战", game.ResumePogs, "primary"); resume.name = "resume-match"; hero.Add(resume);
                hero.Add(Click("认输，下一次再赢回来", ConfirmForfeit, "secondary"));
                hero.Add(Text(GameCatalog.GetNpc(pending.npcId).displayName + " · " + (pending.isReal ? "双方各押一张" : "练习切磋"), "warning"));
            }
            else
            {
                var routes = Box("route-buttons"); hero.Add(routes);
                var story = Click("街区故事", game.ShowStory, "primary"); story.name = "open-story"; routes.Add(story);
                var tournament = Click("八人锦标赛", game.ShowTournament, "primary"); tournament.name = "open-tournament"; routes.Add(tournament);
                hero.Add(Click("自由约战 / 自己练手", game.ShowLobby, "secondary"));
                var guidedEntry = ToyClick("跟小雨学出手", game.StartGuidedPractice, "primary", "primary");
                guidedEntry.name = "open-guided-practice"; hero.Add(guidedEntry);
            }
            var actions = Box("route-buttons"); hero.Add(actions);
            actions.Add(Click("我的铁盒", game.ShowCollection, "secondary")); actions.Add(Click("小卖部交易", game.ShowShop, "secondary"));
            var extras = Box("route-buttons"); hero.Add(extras);
            extras.Add(Click("接一趟跑腿", game.ShowErrands, "small-button")); extras.Add(Click("歇到下一时段", game.Rest, "small-button"));
            var progress = game.Repository.Current;
            hero.Add(Text("故事  " + progress.story.clearedStageIds.Count + " / 8    ·    所有故事都玩真片", "muted"));
            if (progress.activeErrand != null)
                hero.Add(Text("跑腿：" + GameCatalog.GetLocation(progress.activeErrand.destinationLocationId).displayName +
                    (progress.activeErrand.destinationVisited && game.LocationId == progress.activeErrand.destinationLocationId ? " · 已到达，可以交付" : " · 等你送到"), "warning"));
            var locations = Box("hub-locations", true); locations.AddToClassList("v04-locations"); screen.Add(locations);
            locations.Add(Text("今天，去哪儿玩？", "section-title"));
            var scroll = new ScrollView(); locations.Add(scroll); scroll.style.flexGrow = 1; scroll.style.minHeight = 0;
            foreach (var location in GameCatalog.Locations)
            {
                string id = location.id; var choice = Click("", () => game.SelectLocation(id), "location-card"); choice.name = "location-" + id;
                choice.Add(Text(location.displayName, "location-title")); choice.Add(Text(location.surfaceName, "location-detail"));
                choice.EnableInClassList("selected", id == game.LocationId); scroll.Add(choice);
            }
            var footer = Box("hub-footer"); screen.Add(footer);
            AddWallet(footer);
            footer.Add(Text("铁盒  " + game.Repository.PlayerCards.Select(card => card.definitionId).Distinct().Count() + " / " +
                GameCatalog.Cards.Length + "    ·    " + progress.progress.wins + " 胜  " + progress.progress.losses + " 负", "status-pill"));
            footer.Add(Text(ClockLabel(), "status-pill"));
            footer.Add(ToyClick("退出游戏", game.RequestQuit, "secondary", "small-button"));
        }

        void BuildLobby()
        {
            if (mobileUI) { BuildMobileLobby(); return; }
            var overlay = Box("overlay", true); screen.Add(overlay);
            var modal = Box("modal", true); modal.AddToClassList("lobby-modal"); overlay.Add(modal); modal.style.height = Length.Percent(94);
            var header = Box("modal-header"); modal.Add(header);
            var challenge = game.SelectedChallenge;
            header.Add(Text(challenge?.title ?? "放下书包，约一场", "modal-title")); header.Add(Click("收起", game.GoHub));
            var contract = Text(game.RealMatch ? "玩真的 · 双方各押一张真片 · 练习片不能使用" : "自己练手与玩假的不会转移圆片", "contract-banner");
            contract.name = "stake-contract"; contract.EnableInClassList("real-contract", game.RealMatch); modal.Add(contract);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; scroll.style.minHeight = 0; modal.Add(scroll);
            if (challenge != null)
            {
                scroll.Add(Text(challenge.introLine, "story-dialogue"));
                scroll.Add(Text(challenge.hintLine, "label-caption"));
                scroll.Add(Text(GameCatalog.GetNpc(challenge.npcId).displayName + " · " + GameCatalog.RuleName(challenge.rule) +
                    " · " + GameplayLayouts.Name((PracticeLayout)challenge.layout), "section-title"));
            }
            else
            {
                var opponents = Box("cards-row"); scroll.Add(opponents);
                foreach (var npc in GameCatalog.Npcs)
                {
                    string id = npc.id; var option = Click("", () => game.SelectNpc(id), "opponent-card");
                    option.name = "opponent-" + id;
                    option.EnableInClassList("selected", game.NpcId == id && !game.Training);
                    option.Add(Text(npc.displayName, "npc-name"));
                    option.Add(Text(GameCatalog.GetLocation(npc.locationId).displayName + "\n擅长" + GameCatalog.ShotName(npc.preferredShot), "npc-desc"));
                    opponents.Add(option);
                }
                var controls = Box("selection-bar"); scroll.Add(controls);
                foreach (RuleKind rule in Enum.GetValues(typeof(RuleKind)))
                {
                    var option = Click(GameCatalog.RuleName(rule) + "制", () => { game.SelectedRule = rule; Render(); }, "choice");
                    option.EnableInClassList("selected", game.SelectedRule == rule); controls.Add(option);
                }
                controls.Add(Click("自己练手", () => game.SelectPlayMode(true, false), "choice"));
                controls.Add(Click("玩假的", () => game.SelectPlayMode(false, false), "choice"));
                var real = Click("玩真的", () =>
                {
                    game.SelectPlayMode(false, true);
                    if (game.SelectedCard?.isFoundation == true) ShowPracticeRestriction();
                }, "choice"); real.name = "choose-real-match"; real.EnableInClassList("selected", game.RealMatch); controls.Add(real);
                var layouts = Box("layout-options"); scroll.Add(layouts);
                foreach (PracticeLayout layout in Enum.GetValues(typeof(PracticeLayout)))
                {
                    var option = Click(GameplayLayouts.Name(layout), () => { game.SelectedPracticeLayout = layout; Render(); }, "choice");
                    option.EnableInClassList("selected", game.SelectedPracticeLayout == layout); layouts.Add(option);
                }
                if (!game.Training)
                {
                    var greeting = Text(GameCatalog.GetNpc(game.NpcId).displayName + "：" + CompanionPersonality.Greeting(game.NpcId), "story-dialogue");
                    greeting.name = "companion-greeting"; scroll.Add(greeting);
                }
            }
            scroll.Add(Text(GameCatalog.GetLocation(game.LocationId).displayName + " · " + game.World.Terrain.Description, "label-caption"));
            var environment = game.World.Environment;
            scroll.Add(Text(GameCatalog.PeriodName(environment.period) + " · " + WeatherLabel(environment.weather) +
                (environment.weather == WeatherKind.Rainy && environment.frictionMultiplier < 1 ? " · 雨天地面稍滑，井盖更滑" : " · 开局后环境保持不变"), "warning"));
            scroll.Add(Box("divider")); scroll.Add(Text("选一张出手片", "section-title"));
            var cards = Box("cards-row"); scroll.Add(cards);
            foreach (var card in game.Repository.PlayerCards)
            {
                string id = card.instanceId; var option = PogChoice(card.Definition, () => game.SelectCard(id));
                option.name = "card-" + id; option.AddToClassList("instance-choice");
                option.EnableInClassList("selected", game.SelectedInstanceId == id);
                option.Add(Text(card.isFoundation ? "练习片 · 禁止押注" : card.treasured ? "舍不得 · 受到保护" :
                    game.Repository.IsCardLocked(id) ? "正在约战 · 已锁定" : "真片 · 可以押注", card.isFoundation ? "warning" : "muted"));
                cards.Add(option);
            }
            var selected = game.SelectedCard;
            if (selected == null) scroll.Add(Text("铁盒空了。可以接跑腿或去小卖部补给。", "empty-caption"));
            else scroll.Add(Text(selected.Definition.weightFeel + " / " + selected.Definition.gripFeel, "label-caption"));
            var stake = game.OpponentStake;
            if (game.RealMatch && stake != null)
                scroll.Add(Text("对方押：" + stake.Definition.displayName + "；你押：" + (selected?.Definition.displayName ?? "尚未选择") +
                    "。\n赢了带走对方的这一张，输了把自己选的这一张留下；场上五张是计分目标。", "warning"));
            bool invalid = selected == null || game.RealMatch && (selected.isFoundation || selected.treasured || game.Repository.IsCardLocked(selected.instanceId));
            string caption = selected == null ? "没有圆片 · 先去补给" : selected.isFoundation && game.RealMatch ? "练习片不能玩真的" :
                selected.treasured && game.RealMatch ? "珍藏片受保护 · 换一张" : game.RealMatch ? "双方押这张 · 开始约战" : "开始练习";
            var start = Click(caption, game.StartPogs, "primary"); start.name = "start-pogs"; start.SetEnabled(!invalid); modal.Add(start);
            if (invalid)
            {
                var remedies = Box("route-buttons"); modal.Add(remedies);
                remedies.Add(Click("去兑换真片", game.ShowShop)); remedies.Add(Click("去接跑腿", game.ShowErrands));
            }
        }

        ScrollView Page(string title, string caption)
        {
            Topbar(); var page = Box("collection", true); page.AddToClassList("v04-page"); screen.Add(page);
            var header = Box("modal-header"); page.Add(header); header.Add(Text(title, "modal-title")); AddWallet(header);
            page.Add(Text(caption + "\n" + ClockLabel(), "label-caption"));
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; scroll.style.minHeight = 0; page.Add(scroll);
            return scroll;
        }
        void BuildStory()
        {
            if (mobileUI) { BuildMobileStory(); return; }
            var body = Page("街区故事 · 盛夏之约", "八场约定，四位守擂伙伴。每局都玩真的；胜利解锁下一段故事。");
            var save = game.Repository.Current; var grid = Box("cards-row"); body.Add(grid);
            foreach (var stage in CampaignCatalog.Stages)
            {
                bool unlocked = stage.requiredStageIds.All(id => save.story.clearedStageIds.Contains(id));
                bool cleared = save.story.clearedStageIds.Contains(stage.id); string id = stage.id;
                var card = Click("", () => game.OpenStoryStage(id), "story-card"); card.name = "stage-" + id;
                card.EnableInClassList("boss-card", stage.bossLevel > 0); card.SetEnabled(unlocked);
                card.Add(Text((cleared ? "✓ " : unlocked ? "○ " : "锁定 · ") + stage.title, "section-title"));
                card.Add(Text(GameCatalog.GetLocation(stage.locationId).displayName + " · " + GameCatalog.GetNpc(stage.npcId).displayName +
                    " · " + GameCatalog.RuleName(stage.rule), "detail-text"));
                card.Add(Text(stage.description, "detail-text"));
                card.Add(Text(cleared ? "已经完成 · 可以再赴约" : "首次胜利 +" + stage.rewardMarbles + " 颗玻璃珠" +
                    (stage.bossLevel > 0 ? " · 对方押招牌片" : ""), "warning"));
                grid.Add(card);
            }
            if (save.story.clearedStageIds.Count >= 8) body.Add(Text("这一整个夏天，你都记下来了。故事通关后，仍可挑战伙伴、收集圆片和参加锦标赛。", "story-dialogue"));
            body.Add(Click("真片不够？去跑腿补给", game.ShowErrands, "secondary"));
        }
        void BuildTournament()
        {
            if (mobileUI) { BuildMobileTournament(); return; }
            var body = Page("盛夏锦标赛 · 八人签表", "校门八强、胡同半决赛、操场决赛。三轮真片约战，冠军奖励 24 颗玻璃珠。");
            var run = game.Repository.Current.tournament;
            if (run == null || string.IsNullOrEmpty(run.runId))
            {
                body.Add(Text("小雨、大志、桃桃、林林、豆豆、阿岳、阿砚都在等你。\n报名后保存整张签表，暂停或退出游戏都不会重新抽签。", "story-dialogue"));
                var enter = Click("报名 · 八个人的盛夏", game.EnterTournament, "primary"); enter.name = "enter-tournament"; body.Add(enter); return;
            }
            body.Add(Text(run.champion ? "冠军：你！" : run.active ? "当前轮次：" + (run.roundIndex + 1) + " / 3" : "本届已结束 · 下次再来", "section-title"));
            var grid = Box("cards-row"); body.Add(grid);
            for (int i = 0; i < CampaignCatalog.TournamentStages.Length; i++)
            {
                var stage = CampaignCatalog.TournamentStages[i]; var card = Box("story-card", true); grid.Add(card);
                card.Add(Text((i == 0 ? "八强赛" : i == 1 ? "半决赛" : "决赛") + " · " + GameCatalog.GetNpc(stage.npcId).displayName, "section-title"));
                card.Add(Text(GameCatalog.GetLocation(stage.locationId).displayName + " · " + GameCatalog.RuleName(stage.rule), "detail-text"));
                var round = run.rounds.FirstOrDefault(item => item.roundIndex == i);
                card.Add(Text(round?.completed == true ? round.outcome == MatchOutcome.PlayerWin ? "已获胜" : "已结束" :
                    i == run.roundIndex && run.active ? "等你上场" : "等待晋级", "warning"));
            }
            var next = Click(run.active ? "赴约 · 开始这一轮" : "再报名一届锦标赛", run.active ? (Action)game.OpenTournamentRound : game.EnterTournament, "primary");
            next.name = "next-tournament-round"; body.Add(next);
            body.Add(Text("完整签表", "history-title"));
            foreach (var item in run.bracket)
            {
                string left = item.leftOwnerId == "player" ? "你" : GameCatalog.GetNpc(item.leftOwnerId).displayName;
                string right = item.rightOwnerId == "player" ? "你" : GameCatalog.GetNpc(item.rightOwnerId).displayName;
                string winner = string.IsNullOrEmpty(item.winnerOwnerId) ? "待比赛" : item.winnerOwnerId == "draw" ? "平局，重赛" :
                    item.winnerOwnerId == "player" ? "你" : GameCatalog.GetNpc(item.winnerOwnerId).displayName;
                body.Add(Text("第 " + (item.roundIndex + 1) + " 轮 · " + left + " 对 " + right + " → " + winner, "history-entry"));
            }
        }

        void BuildShop()
        {
            if (mobileUI) { BuildMobileShop(); return; }
            var body = Page("小卖部 · 圆片换玻璃珠", "买片、卖片，也能找伙伴直接换。练习片可交易；珍藏片需针对本次交易确认。");
            var tabs = Box("selection-bar"); body.Add(tabs);
            string[] names = { "兑换圆片", "卖出我的片", "找伙伴换片" };
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i; var choice = Click(names[i], () => { shopTab = tab; Render(); }, "choice");
                choice.name = "shop-tab-" + tab;
                choice.EnableInClassList("selected", i == shopTab); tabs.Add(choice);
            }
            var grid = Box("cards-row"); body.Add(grid); var save = game.Repository.Current;
            if (shopTab == 0)
            {
                foreach (var offer in CampaignCatalog.ShopOffers)
                {
                    var definition = GameCatalog.GetCard(offer.cardDefinitionId); string id = offer.id;
                    bool unlocked = CampaignCatalog.IsOfferUnlocked(save, offer);
                    var card = PogChoice(definition, () => PrepareTrade(TradeKind.Buy, id)); card.name = "buy-" + id;
                    card.AddToClassList("shop-card"); card.Add(Text(offer.buyPrice + " 颗玻璃珠", "warning"));
                    card.Add(Text(unlocked ? offer.buyPrice == 2 ? "练习片 · 可以练手" : "真片 · 可以押注" : "先战胜招牌片的 Boss", "muted"));
                    card.SetEnabled(unlocked && save.glassMarbles >= offer.buyPrice); grid.Add(card);
                }
            }
            else if (shopTab == 1)
            {
                foreach (var instance in game.Repository.PlayerCards)
                {
                    string id = instance.instanceId; bool locked = game.Repository.IsCardLocked(id);
                    var card = PogChoice(instance.Definition, () => PrepareTrade(TradeKind.Sell, "shop", id)); card.name = "sell-" + id;
                    card.AddToClassList("shop-card");
                    int price = instance.isFoundation ? 1 : CampaignCatalog.SellPrice(instance.Definition);
                    card.Add(Text("换 " + price + " 颗玻璃珠", "warning"));
                    card.Add(Text(locked ? "对局已锁定" : instance.isFoundation ? "练习片" : instance.treasured ? "舍不得 · 需确认" : instance.WearDescription, "muted"));
                    card.Add(Text(instance.origin, "muted"));
                    card.SetEnabled(!locked); grid.Add(card);
                }
                if (game.Repository.PlayerCards.Count == 0) grid.Add(Text("铁盒是空的。可以去接跑腿，或者切到兑换页。", "empty-caption"));
            }
            else
            {
                foreach (var offer in CampaignCatalog.SwapOffers)
                {
                    var definition = GameCatalog.GetCard(offer.offeredDefinitionId);
                    var owned = game.Repository.PlayerCards.FirstOrDefault(c => c.definitionId == offer.wantedDefinitionId &&
                        !game.Repository.IsCardLocked(c.instanceId) && !c.isFoundation);
                    string id = offer.id, instanceId = owned?.instanceId;
                    bool usedToday = save.transactions.Any(receipt => receipt.kind == TradeKind.Swap && receipt.offerId == id && receipt.day == save.progress.day);
                    bool inStock = game.Repository.NpcCards(offer.npcId).Any(stockCard => stockCard.definitionId == offer.offeredDefinitionId && !game.Repository.IsCardLocked(stockCard.instanceId));
                    var card = PogChoice(definition, () => PrepareTrade(TradeKind.Swap, id, instanceId)); card.name = "swap-" + id; card.AddToClassList("shop-card");
                    card.Add(Text(GameCatalog.GetNpc(offer.npcId).displayName + "想要：\n" + GameCatalog.GetCard(offer.wantedDefinitionId).displayName, "detail-text"));
                    card.Add(Text(usedToday ? "这份清单今天已换过" : !inStock ? "伙伴手里暂时缺这一张" : "同稀有度 · 每份清单每天一次", "muted"));
                    card.SetEnabled(owned != null && !usedToday && inStock); grid.Add(card);
                }
            }
        }
        void PrepareTrade(TradeKind kind, string offerId, string playerCardId = null)
        {
            try
            {
                var quote = game.Repository.GetTradeQuote(kind, offerId, playerCardId);
                if (quote.requiresTreasuredConfirmation)
                {
                    string outgoingName = game.Repository.GetInstance(quote.playerCardId)?.Definition.displayName ?? "这张珍藏片";
                    string exchange = kind == TradeKind.Swap ? "换成“" + game.Repository.GetInstance(quote.npcCardId).Definition.displayName + "”" : "换成 " + quote.marbleDelta + " 颗玻璃珠";
                    var panel = AlertPanel("这一笔涉及珍藏片", "将“" + outgoingName + "”" + exchange + "，确认本次转手吗？\n本次确认不会取消圆片的珍藏标记。");
                    panel.Add(Click("确认这一次交易", () => { CloseAlert(); game.ExecuteTrade(quote, true); }, "primary"));
                    panel.Add(Click("留下它", CloseAlert, "secondary"));
                }
                else game.ExecuteTrade(quote, false);
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }
        void BuildErrands()
        {
            if (mobileUI) { BuildMobileErrands(); return; }
            var body = Page("街坊的小委托", "接一趟、送到指定地点、交付。无需任何圆片，每次能挣 6 颗玻璃珠。");
            var active = game.Repository.Current.activeErrand;
            if (active != null)
            {
                var definition = CampaignCatalog.Errands.First(item => item.id == active.errandId);
                body.Add(Text(definition.title, "section-title")); body.Add(Text(definition.acceptLine, "story-dialogue"));
                body.Add(Text("目的地：" + GameCatalog.GetLocation(active.destinationLocationId).displayName, "detail-text"));
                string destination = active.destinationLocationId, completion = active.completionId;
                bool canDeliver = active.destinationVisited && game.LocationId == destination;
                body.Add(Click(canDeliver ? "已经送到 · 交付领取 6 颗" : "前往目的地", () =>
                { if (canDeliver) game.CompleteErrand(completion); else game.SelectLocation(destination); }, "primary"));
                if (canDeliver) body.Add(Text("完成后时段推进一次，奖励和进度一起保存。", "muted"));
                return;
            }
            foreach (var errand in CampaignCatalog.Errands)
            {
                string id = errand.id; var card = Box("errand-card", true); body.Add(card);
                card.Add(Text(errand.title, "section-title")); card.Add(Text(errand.description, "detail-text"));
                card.Add(Text("送到：" + GameCatalog.GetLocation(errand.destinationLocationId).displayName + " · 奖励 6 颗", "warning"));
                card.Add(Click("接下这一趟", () => game.AcceptErrand(id), "secondary"));
            }
        }

        void BuildMatch()
        {
            if (mobileUI) { BuildMobileMatch(); return; }
            var top = Box("match-top"); screen.Add(top); top.Add(Click("歇一会儿", ShowPause));
            viewToggle = Click("俯视视角", game.ToggleMatchCamera, "small-button");
            viewToggle.name = "match-view-toggle"; viewToggle.AddToClassList("view-toggle"); top.Add(viewToggle);
            var board = Box("scoreboard", true); top.Add(board); score = Text("", "score"); board.Add(score);
            ruleCaption = Text("", "rule-caption"); board.Add(ruleCaption); AddWallet(top);
            turnLabel = Text("", "turn-label"); screen.Add(turnLabel); highlight = Text("", "highlight"); screen.Add(highlight);
            if (game.GuidedPracticeMode)
            {
                var guide = Box("guided-practice", true); screen.Add(guide);
                guide.AddToClassList("toy-guided-practice");
                guide.Add(Text("跟小雨学出手", "section-title"));
                guidedMessage = Text("", "detail-text"); guide.Add(guidedMessage);
                BuildGuidedAction(guide);
            }
            var bottom = Box("match-bottom", true); screen.Add(bottom); var controls = Box("shot-buttons"); bottom.Add(controls);
            shotButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var kind = (ShotKind)i; shotButtons[i] = Click((i + 1) + "  " + GameCatalog.ShotName(kind),
                    () => { game.Pogs.Shot = kind; UpdateMatch(); }, "shot-button"); controls.Add(shotButtons[i]);
            }
            var power = Box("power-wrap"); controls.Add(power); power.Add(Text("向后拉得越远，出手越有劲", "power-title"));
            var bar = Box("power-bar"); power.Add(bar); powerFill = Box("power-fill"); bar.Add(powerFill);
            if (game.GuidedPracticeMode) BuildGuidedPower(bar, power);
            BuildDesktopShotSetup(controls);
            var aimingHelp = Text("A D 换边 · 中键环绕 · W S 俯仰\n滚轮远近 · Q E 旋转 · 右键取消", "shot-camera-help");
            aimingHelp.name = "shot-camera-help";
            aimingHelp.pickingMode = PickingMode.Position;
            aimingHelp.tooltip = "按住出手片，朝出击方向的反面拖动，松手出击。站位可绕场一整圈；前后倾与左右倾相对当前出手方向。中键按住拖动镜头，W / S 调俯仰，滚轮拉近或拉远。";
            controls.Add(aimingHelp);
            BuildMobileMatchControls(controls);
            message = Text("", "message"); bottom.Add(message); UpdateMatch();
        }
        void BuildGuidedPower(VisualElement bar, VisualElement parent)
        {
            bar.AddToClassList("guided-power-bar");
            guidedPowerRange = Box("guided-power-range"); guidedPowerRange.name = "guided-power-range";
            guidedPowerRange.style.display = DisplayStyle.None; bar.Add(guidedPowerRange);
            guidedPowerStatus = Text("", "guided-power-status"); guidedPowerStatus.name = "guided-power-status";
            parent.Add(guidedPowerStatus);
        }

        void BuildGuidedAction(VisualElement parent)
        {
            guidedAction = Click("", ActivateGuidedAction, mobileUI ? "mobile-primary" : "primary");
            guidedAction.name = "guided-action"; guidedAction.AddToClassList("guided-action");
            guidedAction.AddToClassList("toy-button"); guidedAction.style.display = DisplayStyle.None;
            parent.Add(guidedAction);
        }

        void ActivateGuidedAction()
        {
            if (!game.GuidedPracticeMode || game.GuidedPractice == null) return;
            var progress = game.GuidedPractice.Progress;
            if (game.Pogs.Phase == MatchPhase.Flying || game.Pogs.IsDragging) return;
            if (progress.allCompleted) game.GoHub();
            else if (progress.stepCompleted && progress.step == GuidedPracticeStep.SkimRingOut)
            {
                game.ContinueGuidedPractice();
                if (game.GuidedPractice != null && game.GuidedPractice.IsComplete) game.GoHub();
            }
            else if (progress.stepCompleted) game.ContinueGuidedPractice();
            else if (progress.awaitingRetry) game.RetryGuidedPractice();
        }

        void UpdateGuidedPresentation(PogMatch match)
        {
            if (!game.GuidedPracticeMode || game.GuidedPractice == null) return;
            var progress = game.GuidedPractice.Progress;
            bool result = progress.resultPending || progress.stepCompleted || progress.awaitingRetry || progress.allCompleted;
            bool completed = progress.allCompleted || progress.stepCompleted && progress.step == GuidedPracticeStep.SkimRingOut;
            score.text = completed ? "三个动作练会了" : mobileUI ? "小雨教你" : "跟小雨学出手";
            score.EnableInClassList("guided-score", true);
            turnLabel.text = match.Phase == MatchPhase.Flying ? "等这一手自然停稳" : result ?
                (progress.stepCompleted || progress.allCompleted ? "这一手成功了 · 记住这份手感" : "这一手停稳了 · 看看结果") :
                progress.step == GuidedPracticeStep.DragAndCancel ? "先练拖拽与取消，再松手出击" :
                progress.step == GuidedPracticeStep.SlapFlip ? "找薄荷绿纸边 · 掌拍掀起来" : "顺着金色箭头 · 从圈内向外擦";
            if (guidedMessage != null)
                guidedMessage.text = mobileUI ? progress.feedback.Replace("右键取消一次", "点“取消蓄力”一次") : progress.feedback;
            if (message != null) message.style.display = DisplayStyle.None;
            // 结果保留在教学卡中，中央只留真实圆片和动作示意。
            if (highlight != null) highlight.style.display = DisplayStyle.None;
            bool showRange = progress.step == GuidedPracticeStep.SlapFlip;
            if (guidedPowerRange != null) guidedPowerRange.style.display = showRange ? DisplayStyle.Flex : DisplayStyle.None;
            if (guidedPowerStatus != null)
            {
                string value;
                if (result) value = "这一手的结果已停稳";
                else if (match.Phase == MatchPhase.Flying) value = "圆片正在飞行";
                else if (match.IsDragging)
                {
                    string strength = showRange ? match.Power < .25f ? "偏轻，再拉一点" : match.Power > .78f ? "偏重，收回一点" : "中等力道，可以松手" :
                        match.Power < .25f ? "轻轻一手" : match.Power > .78f ? "有劲的一手" : "力道适中";
                    value = strength + " · " + Mathf.RoundToInt(match.Power * 100) + "%";
                }
                else value = showRange ? "亮框是中等力道区域" : progress.step == GuidedPracticeStep.DragAndCancel ?
                    (mobileUI ? "向后拖，点取消蓄力" : "向后拖，右键取消") : "从圈内侧向外送力";
                guidedPowerStatus.text = value;
                guidedPowerStatus.EnableInClassList("guided-power-ready", showRange && match.IsDragging && match.Power >= .25f && match.Power <= .78f);
            }
            if (guidedAction != null)
            {
                bool show = result && match.Phase != MatchPhase.PlayerAim && match.Phase != MatchPhase.Flying && match.Phase != MatchPhase.NpcThinking && !match.IsDragging;
                guidedAction.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                guidedAction.text = completed ? "练会了，回街区" : progress.stepCompleted ? "继续下一步" : "再试一次";
                guidedAction.SetEnabled(show);
            }
        }

        public void UpdateMatch()
        {
            if (score == null) return; var match = game.Pogs;
            score.text = game.Training ? "练手  " + match.PlayerScore + " / 3" : "你  " + match.PlayerScore + " : " + match.NpcScore + "  " + GameCatalog.GetNpc(game.NpcId).displayName;
            if (game.GuidedPracticeMode && game.GuidedPractice != null)
            {
                var progress = game.GuidedPractice.Progress;
                string step = progress.step == GuidedPracticeStep.DragAndCancel ? "第 1 步 · 拖拽与取消" : progress.step == GuidedPracticeStep.SlapFlip ? "第 2 步 · 掌拍翻面" : "第 3 步 · 斜擦出圈";
                ruleCaption.text = mobileUI ? "教学 " + (progress.step == GuidedPracticeStep.DragAndCancel ? "1" : progress.step == GuidedPracticeStep.SlapFlip ? "2" : "3") + " / 3" : "出手教学 / " + step;
                if (guidedMessage != null) guidedMessage.text = mobileUI ? progress.feedback.Replace("右键取消一次", "点“取消蓄力”一次") : progress.feedback;
            }
            else
            ruleCaption.text = GameCatalog.RuleName(match.Rule) + "制 / " + GameplayLayouts.Name(match.Layout) + " / " +
                (game.Training ? "自己练手" : game.RealMatch ? "玩真的 · 双方押一张" : "玩假的");
            turnLabel.text = match.Phase == MatchPhase.PlayerAim ? "轮到你了 · 第 " + match.Turn + " 手" :
                match.Phase == MatchPhase.NpcThinking ? "朋友正在找角度" : "等圆片停稳，再看谁翻了";
            message.text = match.Message + "    旋转：" + (match.Spin > .1 ? "向右" : match.Spin < -.1 ? "向左" : "不旋") + " " + Math.Abs(match.Spin).ToString("0.0");
            if (mobileUI)
            {
                if (!game.GuidedPracticeMode)
                    ruleCaption.text = GameCatalog.RuleName(match.Rule) + " · " + (game.Training ? "练手" : game.RealMatch ? "押片" : "切磋");
                string feedback = match.Phase == MatchPhase.PlayerAim ?
                    (string.IsNullOrEmpty(match.LastFeedback) ? "按住圆片 · 向后拖 · 松手出击" : match.LastFeedback) : match.Message;
                message.text = feedback + "\n旋转 " + (match.Spin > .1 ? "向右 " : match.Spin < -.1 ? "向左 " : "归零 ") + Math.Abs(match.Spin).ToString("0.0");
            }
            if (viewToggle != null)
            {
                viewToggle.text = game.World.IsTopDownMatchView ? "斜俯视" : "俯视视角";
                viewToggle.EnableInClassList("top-down", game.World.IsTopDownMatchView);
            }
            powerFill.style.width = Length.Percent(match.Power * 100);
            if (shotButtons != null) for (int i = 0; i < 3; i++)
            {
                shotButtons[i].EnableInClassList("selected", (int)match.Shot == i);
                shotButtons[i].SetEnabled(match.Phase == MatchPhase.PlayerAim);
            }
            UpdateGuidedPresentation(match);
            UpdateShotSetupUI(match);
        }
        public void ShowAudioSettings()
        {
            if (audioOverlay != null) return;
            if (mobileUI) { ShowMobileAudioSettings(); UpdateShotSetupUI(game.Pogs); return; }
            game.Pogs.CancelAim(); audioPreviousScale = Time.timeScale;
            if (game.Screen == AppScreen.Pogs) Time.timeScale = 0;
            UpdateShotSetupUI(game.Pogs);
            audioOverlay = Box("overlay", true); audioOverlay.name = "game-settings"; screen.Add(audioOverlay);
            var panel = Box("modal", true); panel.AddToClassList("settings-panel"); audioOverlay.Add(panel);
            var header = Box("modal-header"); panel.Add(header); header.Add(Text("游戏设置", "modal-title")); header.Add(Click("收起", CloseAudioSettings));
            VisualElement content = panel;
            if (mobileUI)
            {
                var scroll = new ScrollView(); scroll.AddToClassList("mobile-settings-scroll"); panel.Add(scroll); content = scroll;
                content.Add(Text("手机显示由系统管理", "muted"));
            }
            else
            {
                content.Add(Text("显示模式", "section-title")); var modes = Box("selection-bar"); content.Add(modes);
                var values = new[] { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
                displayModes = new Button[3];
                for (int i = 0; i < values.Length; i++)
                {
                    var mode = values[i]; displayModes[i] = Click(DisplaySettings.ModeName(mode), () => game.Display.Preview(mode), "choice");
                    displayModes[i].name = "display-" + mode; modes.Add(displayModes[i]);
                }
                displayStatus = Text("", "warning"); content.Add(displayStatus);
                var confirmation = Box("route-buttons"); content.Add(confirmation);
                displayConfirm = Click("保留这个显示模式", game.Display.Confirm, "small-button"); displayConfirm.name = "confirm-display"; confirmation.Add(displayConfirm);
                displayRevert = Click("恢复原来的模式", game.Display.Revert, "small-button"); confirmation.Add(displayRevert);
            }
            content.Add(Box("divider")); AddVolumeSlider(content, "总音量", game.Audio.MasterVolume, 0);
            AddVolumeSlider(content, "音乐", game.Audio.MusicVolume, 1); AddVolumeSlider(content, "音效", game.Audio.EffectsVolume, 2);
            AddVolumeSlider(content, "环境", game.Audio.AmbienceVolume, 3);
            audioMuteButton = Click(game.Audio.Muted ? "打开声音" : "暂时静音", () => { game.Audio.ToggleMute(); UpdateMute(); }, "secondary"); content.Add(audioMuteButton);
            content.Add(Box("divider"));
            var exitRow = Box("route-buttons"); content.Add(exitRow);
            exitRow.Add(ToyClick("保存并退出游戏", game.RequestQuit, "danger", "secondary"));
            RefreshDisplayStatus();
        }
        void RefreshDisplayStatus()
        {
            if (audioOverlay == null || displayStatus == null) return;
            bool pending = game.Display.AwaitingConfirmation;
            displayStatus.text = pending ? "还剩 " + Mathf.CeilToInt(game.Display.ConfirmationSecondsRemaining) + " 秒确认，超时会恢复原模式。" :
                "已保存：" + DisplaySettings.ModeName(game.Display.CurrentMode) + " · 窗口 " + game.Display.WindowWidth + " × " + game.Display.WindowHeight;
            displayConfirm.SetEnabled(pending); displayRevert.SetEnabled(pending);
            var values = new[] { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
            if (displayModes == null) return;
            for (int i = 0; i < 3; i++) displayModes[i].EnableInClassList("selected", values[i] == game.Display.CurrentMode);
        }
        internal string VerifyPolishControls()
        {
            float master = game.Audio.MasterVolume, music = game.Audio.MusicVolume, effects = game.Audio.EffectsVolume, ambience = game.Audio.AmbienceVolume;
            float previousScale = Time.timeScale;
            try
            {
                ShowAudioSettings();
                if (audioOverlay.Query<Slider>().ToList().Count != 4) return "Expected four independent audio controls";
                if (audioOverlay.Query<Button>().ToList().Count(button => button.name.StartsWith("display-")) != 3) return "Expected three display modes";
                audioOverlay.Q<Slider>("volume-1").value = 37;
                if (Mathf.Abs(game.Audio.MusicVolume - .37f) > .001f || game.Audio.EffectsVolume != effects) return "Music slider changed wrong mixer route";
                if (!HandleEscape() || audioOverlay != null || Time.timeScale != previousScale) return "Settings Escape failed";
                return null;
            }
            finally { CloseAudioSettings(); game.Audio.SetVolumes(master, music, effects, ambience); Time.timeScale = previousScale; }
        }
    }
}
