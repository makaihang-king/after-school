using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>同一套界面服务所有童年玩法。对局时仅更新标签，不重建视觉树。</summary>
    public sealed class SummerUI : MonoBehaviour
    {
        SummerGame game;
        UIDocument document;
        VisualElement root, screen, powerFill, pauseOverlay, audioOverlay, dismissOverlay;
        Label score, ruleCaption, turnLabel, message, highlight, debugLabel;
        Button mute, audioMuteButton;
        Button[] shotButtons;
        PanelSettings settings;
        Font font;
        float audioPreviousScale = 1;
        int collectionFilter;
        bool collectionSortRarity;
        string inspectedInstanceId;

        public void Initialize(SummerGame owner)
        {
            game = owner;
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = .5f; settings.sortingOrder = 20;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/SummerTheme");
            settings.clearColor = false;
            settings.clearDepthStencil = false;
            var uiObject = new GameObject("放学后 · 界面"); uiObject.transform.SetParent(transform, false); uiObject.SetActive(false);
            document = uiObject.AddComponent<UIDocument>(); document.panelSettings = settings; uiObject.SetActive(true);
            root = document.rootVisualElement;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 24);
            root.style.unityFontDefinition = FontDefinition.FromFont(font);
            var css = Resources.Load<StyleSheet>("UI/Summer"); if (css != null) root.styleSheets.Add(css);
            root.pickingMode = PickingMode.Ignore;
        }

        static Label Text(string value, string cls = null)
        {
            var label = new Label(value); label.pickingMode = PickingMode.Ignore;
            if (cls != null) label.AddToClassList(cls); return label;
        }
        static VisualElement Box(string cls, bool block = false)
        {
            var element = new VisualElement(); element.AddToClassList(cls);
            element.pickingMode = block ? PickingMode.Position : PickingMode.Ignore;
            if (block) element.AddToClassList("input-block"); return element;
        }
        Button Click(string value, Action action, string cls = "small-button")
        {
            var button = new Button(() => { game.Audio.Play("ui", .25f); action(); }) { text = value };
            button.AddToClassList(cls); button.AddToClassList("input-block"); return button;
        }

        public void Render()
        {
            if (audioOverlay != null || pauseOverlay != null) Time.timeScale = 1;
            audioOverlay?.RemoveFromHierarchy(); audioOverlay = null; audioMuteButton = null;
            root.Clear(); score = ruleCaption = turnLabel = message = highlight = debugLabel = null;
            powerFill = pauseOverlay = dismissOverlay = null; shotButtons = null; mute = null;
            screen = Box("screen"); root.Add(screen);
            switch (game.Screen)
            {
                case AppScreen.Hub: BuildHub(); break;
                case AppScreen.Lobby: BuildHub(); BuildLobby(); break;
                case AppScreen.Collection: BuildCollection(); break;
                case AppScreen.Pogs: BuildMatch(false); break;
                case AppScreen.Marbles: BuildMatch(true); break;
                case AppScreen.Result: BuildResult(); break;
            }
            ToggleDebug(game.ShowDebug);
        }

        void Topbar()
        {
            var bar = Box("topbar"); screen.Add(bar); bar.Add(Text("放学后  /  SUMMER DAYS", "brand"));
            var actions = Box("top-actions"); bar.Add(actions);
            actions.Add(Click("夏天的记忆", ShowMemory));
            actions.Add(Click("声音设置", ShowAudioSettings));
            mute = Click(game.Audio.Muted ? "声音 · 关" : "声音 · 开", () => { game.Audio.ToggleMute(); UpdateMute(); }); actions.Add(mute);
            if (game.Screen != AppScreen.Hub) actions.Add(Click("回到街区", game.GoHub));
        }

        void BuildHub()
        {
            Topbar();
            var hero = Box("hub-hero", true); screen.Add(hero);
            hero.Add(Text("一九九六 · 夏天 · 第 " + game.Repository.Current.progress.day + " 天", "eyebrow"));
            hero.Add(Text("放学后", "hero-title"));
            hero.Add(Text("把一整个夏天，装进铁盒。", "hero-subtitle"));
            hero.Add(Text("书包一扔，粉笔画圈。\n朋友们已经到了，就等你这一手。", "body"));
            var pending = game.Repository.GetPendingMatch();
            if (pending != null)
            {
                hero.Add(Click("继续那场约战", game.ResumePogs, "primary"));
                hero.Add(Click("认输，明天赢回来", () => ConfirmForfeit(), "secondary"));
                hero.Add(Text("对手：" + GameCatalog.GetNpc(pending.npcId).displayName + " · " + (pending.isReal ? "玩真的" : "切磋局"), "warning"));
            }
            else
            {
                hero.Add(Click("去打圆片", game.ShowLobby, "primary"));
                hero.Add(Click("弹玻璃球  ·  六手清圈", ShowMarbleLobby, "secondary"));
            }
            hero.Add(Click("打开我的铁盒", game.ShowCollection, "secondary"));
            hero.Add(Text("两种童年玩法，共享一个小街区。", "muted"));
            if (!string.IsNullOrEmpty(game.Error)) hero.Add(Text(game.Error, "warning"));
            var locations = Box("hub-locations", true); screen.Add(locations);
            locations.Add(Text("今天，去哪儿玩？", "section-title"));
            foreach (var location in GameCatalog.Locations)
            {
                string id = location.id;
                var choice = Click("", () => game.SelectLocation(id), "location-card");
                choice.Add(Text(location.displayName, "location-title"));
                choice.Add(Text(location.surfaceName + " · " + (location.id == "home" ? "安心练手" : string.Join("、", location.npcIds.Select(n => GameCatalog.GetNpc(n).displayName))), "location-detail"));
                choice.EnableInClassList("selected", game.LocationId == location.id); locations.Add(choice);
            }
            var footer = Box("hub-footer"); screen.Add(footer);
            int distinct = game.Repository.PlayerCards.Select(c => c.definitionId).Distinct().Count();
            footer.Add(Text("铁盒  " + distinct + " / 30    ·    " + game.Repository.Current.progress.wins + " 胜  " + game.Repository.Current.progress.losses + " 负", "status-pill"));
            footer.Add(Text(GameCatalog.GetLocation(game.LocationId).displayName + "  /  " + GameCatalog.PeriodName(game.Repository.Current.progress.period), "status-pill"));
        }

        void BuildLobby()
        {
            var overlay = Box("overlay", true); screen.Add(overlay); var modal = Box("modal", true); overlay.Add(modal);
            modal.AddToClassList("lobby-modal");
            var header = Box("modal-header"); modal.Add(header); header.Add(Text("放下书包，约一场", "modal-title")); header.Add(Click("收起", game.GoHub));
            modal.style.height = Length.Percent(94);
            var lobbyScroll = new ScrollView(ScrollViewMode.Vertical);
            lobbyScroll.style.flexGrow = 1; lobbyScroll.style.minHeight = 0;
            lobbyScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            modal.Add(lobbyScroll);
            var body = Box("lobby-content"); body.style.flexShrink = 0; lobbyScroll.Add(body);
            var opponents = Box("row"); body.Add(opponents);
            foreach (var npc in GameCatalog.Npcs)
            {
                string id = npc.id;
                var choice = Click("", () => game.SelectNpc(id), "opponent-card");
                choice.EnableInClassList("selected", game.NpcId == id && !game.Training);
                choice.Add(Text(npc.displayName, "npc-name"));
                choice.Add(Text(GameCatalog.GetLocation(npc.locationId).displayName + "\n擅长" + GameCatalog.ShotName(npc.preferredShot), "npc-desc")); opponents.Add(choice);
            }
            body.Add(Text(GameCatalog.GetNpc(game.NpcId).introduction, "label-caption"));
            var controls = Box("selection-bar"); body.Add(controls);
            var flip = Click("翻面制", () => { game.SelectedRule = RuleKind.Flip; Render(); }, "choice"); flip.EnableInClassList("selected", game.SelectedRule == RuleKind.Flip); controls.Add(flip);
            var ring = Click("出圈制", () => { game.SelectedRule = RuleKind.RingOut; Render(); }, "choice"); ring.EnableInClassList("selected", game.SelectedRule == RuleKind.RingOut); controls.Add(ring);
            var training = Click("自己练手", () => { game.Training = !game.Training; game.RealMatch = false; Render(); }, "choice"); training.EnableInClassList("selected", game.Training); controls.Add(training);
            var fake = Click("玩假的 · 不输片", () => { game.RealMatch = false; Render(); }, "choice"); fake.EnableInClassList("selected", !game.RealMatch); controls.Add(fake);
            var real = Click("玩真的 · 押一张", () => { game.RealMatch = true; game.Training = false; Render(); }, "choice"); real.EnableInClassList("selected", game.RealMatch); controls.Add(real);
            var layouts = Box("layout-options"); body.Add(layouts); layouts.Add(Text("摆阵", "layout-caption"));
            foreach (PracticeLayout layout in Enum.GetValues(typeof(PracticeLayout)))
            {
                var option = Click(GameplayLayouts.Name(layout), () => { game.SelectedPracticeLayout = layout; Render(); }, "choice");
                option.EnableInClassList("selected", game.SelectedPracticeLayout == layout); layouts.Add(option);
            }
            body.Add(Text(GameplayLayouts.Description(game.SelectedPracticeLayout), "label-caption"));
            body.Add(Text(GameCatalog.GetLocation(game.LocationId).displayName + " · " + game.World.Terrain.Description + "。留心高低差，井盖比石砖更滑。", "label-caption"));
            body.Add(Box("divider")); body.Add(Text("选一张出手片  ·  力度、落点、旋转和打法决定胜负", "label-caption"));
            var cardScroll = new ScrollView(ScrollViewMode.Horizontal);
            cardScroll.style.height = 190; cardScroll.style.flexShrink = 0;
            cardScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            cardScroll.horizontalScrollerVisibility = ScrollerVisibility.Auto; body.Add(cardScroll);
            var cards = Box("row"); cardScroll.Add(cards);
            foreach (var instance in game.Repository.PlayerCards)
            {
                string id = instance.instanceId;
                var choice = PogChoice(instance.Definition, () => game.SelectCard(id));
                choice.EnableInClassList("selected", game.SelectedInstanceId == id);
                if (instance.isFoundation || instance.treasured) choice.Add(Text(instance.isFoundation ? "练习片" : "舍不得", "muted"));
                cards.Add(choice);
            }
            var selected = game.SelectedCard;
            if (selected != null) body.Add(Text(selected.Definition.weightFeel + "  /  " + selected.Definition.gripFeel, "label-caption"));
            if (game.RealMatch && game.OpponentStake != null)
            {
                bool recover = game.OpponentStake.originalOwnerId == GameCatalog.PlayerOwnerId;
                body.Add(Text((recover ? "夺回机会！" : "对方押上：") + game.OpponentStake.Definition.displayName + "。赢了进你的铁盒，输了你选中的这一张会留在对方手里。", "warning"));
            }
            else body.Add(Text(game.Training ? "自己练手，没有对手回合，也不会转移圆片。" : "先练一局，看看这块地的手感。切磋只记战绩，圆片各自带回。", "label-caption"));
            if (!string.IsNullOrEmpty(game.Error)) body.Add(Text(game.Error, "warning"));
            modal.Add(Click(game.RealMatch ? "敢！就押这张" : game.Training ? "试试这一手" : "来，玩一局", game.StartPogs, "primary"));
        }

        Button PogChoice(CardDefinition definition, Action clicked)
        {
            var button = Click("", clicked, "pog-choice");
            var art = Box("pog-art"); art.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(definition.artResourcePath)); button.Add(art);
            button.Add(Text(definition.displayName, "pog-name")); return button;
        }

        public void ShowMarbleLobby()
        {
            dismissOverlay = Box("overlay", true); screen.Add(dismissOverlay);
            var modal = Box("modal", true); modal.AddToClassList("challenge-modal"); dismissOverlay.Add(modal);
            var header = Box("modal-header"); modal.Add(header);
            header.Add(Text("六手，八颗玻璃球", "modal-title")); header.Add(Click("收起", CloseDismissOverlay));
            modal.Add(Text("先挑一个阵。看清接力的路线，再把球往圈外送。", "label-caption"));
            var options = Box("challenge-options"); modal.Add(options);
            foreach (MarbleLayout layout in Enum.GetValues(typeof(MarbleLayout)))
            {
                var option = Click("", () => { game.SelectedMarbleLayout = layout; CloseDismissOverlay(); ShowMarbleLobby(); }, "challenge-card");
                option.EnableInClassList("selected", game.SelectedMarbleLayout == layout);
                option.Add(LayoutDiagram(GameplayLayouts.MarblePositions(layout), false));
                option.Add(Text(GameplayLayouts.Name(layout), "section-title"));
                option.Add(Text(GameplayLayouts.Description(layout), "detail-text")); options.Add(option);
            }
            modal.Add(Text("六次出手 · 整颗越过粉笔圈才算 · 最好成绩 " + game.Repository.Current.progress.marbleBestScore + " / 8", "label-caption"));
            modal.Add(Click("就玩这个阵", game.StartMarbles, "primary"));
        }

        VisualElement LayoutDiagram(Vector3[] positions, bool pogs)
        {
            var diagram = Box("layout-diagram"); var circle = Box("diagram-circle"); diagram.Add(circle);
            foreach (var position in positions)
            {
                var dot = Box(pogs ? "diagram-pog" : "diagram-marble");
                dot.style.left = 70 + position.x * 32; dot.style.top = 70 - (position.z - .55f) * 32; circle.Add(dot);
            }
            return diagram;
        }

        void BuildCollection()
        {
            Topbar(); var collection = Box("collection", true); screen.Add(collection);
            var header = Box("modal-header"); collection.Add(header);
            header.Add(Text("我的铁盒", "modal-title"));
            header.Add(Text("每一张，都有一个夏天的故事。", "muted"));
            var filters = Box("collection-filters"); collection.Add(filters);
            string[] filterNames = { "全部图鉴", "我的圆片", "舍不得", "等着赢回来" };
            for (int i = 0; i < filterNames.Length; i++)
            {
                int index = i; var filter = Click(filterNames[i], () => { collectionFilter = index; Render(); }, "choice");
                filter.EnableInClassList("selected", collectionFilter == i); filters.Add(filter);
            }
            var sorting = Click(collectionSortRarity ? "排列 · 珍稀优先" : "排列 · 系列顺序", () => { collectionSortRarity = !collectionSortRarity; Render(); }); filters.Add(sorting);
            var main = Box("collection-main"); collection.Add(main);
            var scroll = new ScrollView(); scroll.AddToClassList("collection-grid"); main.Add(scroll);
            var grid = Box("cards-row"); scroll.Add(grid);
            var definitions = GameCatalog.Cards.Where(d => collectionFilter == 0 ||
                collectionFilter == 1 && game.Repository.PlayerCards.Any(c => c.definitionId == d.id) ||
                collectionFilter == 2 && game.Repository.PlayerCards.Any(c => c.definitionId == d.id && c.treasured) ||
                collectionFilter == 3 && game.Repository.LostPlayerCards.Any(c => c.definitionId == d.id));
            if (collectionSortRarity) definitions = definitions.OrderByDescending(d => d.rarity).ThenBy(d => d.id);
            var visibleDefinitions = definitions.ToList();
            if (visibleDefinitions.Count > 0 && !visibleDefinitions.Any(d => d.id == game.InspectedCardId))
            { game.InspectedCardId = visibleDefinitions[0].id; inspectedInstanceId = null; }
            int shown = 0;
            foreach (var definition in visibleDefinitions)
            {
                string id = definition.id;
                var choice = PogChoice(definition, () => { game.InspectedCardId = id; inspectedInstanceId = null; Render(); });
                int ownedCount = game.Repository.PlayerCards.Count(c => c.definitionId == id); bool owned = ownedCount > 0;
                choice.EnableInClassList("locked", !owned); choice.EnableInClassList("selected", game.InspectedCardId == id);
                choice.Add(Text(owned ? "铁盒里 · " + ownedCount + " 张" : "还在朋友手里", "muted")); grid.Add(choice);
                shown++;
            }
            if (shown == 0) grid.Add(Text(collectionFilter == 2 ? "还没有标记“舍不得”的圆片。点开喜欢的一张，给它留个位置。" : "铁盒里没有这一类圆片。去街区再玩一局吧。", "empty-caption"));
            var inspected = GameCatalog.GetCard(game.InspectedCardId); var detailScroll = new ScrollView(); detailScroll.AddToClassList("detail-scroll"); main.Add(detailScroll);
            var detail = Box("detail", true); detailScroll.Add(detail);
            var copies = game.Repository.PlayerCards.Where(c => c.definitionId == inspected.id)
                .Concat(game.Repository.LostPlayerCards.Where(c => c.definitionId == inspected.id))
                .Where(c => collectionFilter != 1 || c.ownerId == GameCatalog.PlayerOwnerId)
                .Where(c => collectionFilter != 2 || c.ownerId == GameCatalog.PlayerOwnerId && c.treasured)
                .Where(c => collectionFilter != 3 || c.ownerId != GameCatalog.PlayerOwnerId).ToList();
            if (copies.Count == 0) copies = game.Repository.PlayerCards.Where(c => c.definitionId == inspected.id).ToList();
            var selectedCopy = copies.FirstOrDefault(c => c.instanceId == inspectedInstanceId) ?? copies.FirstOrDefault();
            inspectedInstanceId = selectedCopy?.instanceId;
            if (copies.Count > 1)
            {
                var copyLabels = copies.Select((c, index) => "第 " + (index + 1) + " 张 · " + (c.ownerId == GameCatalog.PlayerOwnerId ? c.treasured ? "舍不得" : "在我的铁盒" : GameCatalog.GetNpc(c.ownerId).displayName + "手里")).ToList();
                var copyPicker = new DropdownField("", copyLabels, copies.IndexOf(selectedCopy)); copyPicker.AddToClassList("layout-picker"); detail.Add(copyPicker);
                StyleDropdown(copyPicker);
                copyPicker.RegisterValueChangedCallback(evt => { inspectedInstanceId = copies[copyPicker.index].instanceId; Render(); });
            }
            var image = Box("detail-art"); image.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(inspected.artResourcePath)); detail.Add(image);
            detail.Add(Text(inspected.displayName, "detail-name")); detail.Add(Text(inspected.series + " · " + GameCatalog.RarityName(inspected.rarity), "muted"));
            detail.Add(Text(inspected.story, "detail-text")); detail.Add(Box("divider"));
            detail.Add(Text(inspected.weightFeel + "\n" + inspected.gripFeel + "\n" + inspected.springFeel, "detail-text"));
            var instance = selectedCopy != null && selectedCopy.ownerId == GameCatalog.PlayerOwnerId ? selectedCopy : null;
            if (instance != null)
            {
                detail.Add(Text("来源：" + instance.origin + "\n" + instance.wins + " 胜 / " + instance.losses + " 负 · " + instance.WearDescription, "detail-text"));
                var wear = Box("wear-track"); var fill = Box("wear-fill"); fill.style.width = Length.Percent(instance.wear * 100); wear.Add(fill); detail.Add(wear);
                if (instance.history.Count > 0)
                {
                    detail.Add(Text("它记得的下午", "history-title"));
                    foreach (var memory in instance.history.AsEnumerable().Reverse().Take(6)) detail.Add(Text("· " + memory.description, "history-entry"));
                }
                string id = instance.instanceId;
                detail.Add(Click(instance.treasured ? "取消“舍不得”标记" : "这张舍不得 · 保护起来", () =>
                { try { game.Repository.SetTreasured(id, !instance.treasured); Render(); } catch (Exception e) { ShowError(e.Message); } }, "secondary"));
                detail.Add(Click("拿它去打一局", () => { game.SelectCard(id); game.ShowLobby(); }, "primary"));
            }
            else
            {
                var lost = selectedCopy ?? game.Repository.LostPlayerCards.FirstOrDefault(c => c.definitionId == inspected.id);
                detail.Add(Text(lost != null ? "你记得这张。它现在在" + GameCatalog.GetNpc(lost.ownerId).displayName + "手里，约战时会优先出现。" : "去和朋友约战，看看谁的铁盒里藏着它。", "detail-text"));
                if (lost != null) detail.Add(Text("来源：" + lost.origin + "\n" + lost.WearDescription + " · " + lost.wins + " 胜 / " + lost.losses + " 负", "detail-text"));
                if (lost != null) detail.Add(Click("去把它赢回来", () => { game.SelectNpc(lost.ownerId); game.PreferredRecoveryInstanceId = lost.instanceId; game.RealMatch = true; game.ShowLobby(); }, "primary"));
            }
        }

        void BuildMatch(bool marbles)
        {
            var top = Box("match-top"); screen.Add(top);
            top.Add(Click("歇一会儿", ShowPause)); var board = Box("scoreboard", true); top.Add(board);
            score = Text("", "score"); board.Add(score); ruleCaption = Text("", "rule-caption"); board.Add(ruleCaption);
            top.Add(Click("怎么玩", ShowPause));
            turnLabel = Text("", "turn-label"); screen.Add(turnLabel);
            highlight = Text("", "highlight"); screen.Add(highlight);
            var bottom = Box("match-bottom", true); screen.Add(bottom); var controls = Box("shot-buttons"); bottom.Add(controls);
            if (!marbles)
            {
                shotButtons = new Button[3];
                for (int i = 0; i < 3; i++)
                {
                    var kind = (ShotKind)i;
                    shotButtons[i] = Click((i + 1) + "  " + GameCatalog.ShotName(kind), () => { game.Pogs.Shot = kind; UpdateMatch(); }, "shot-button"); controls.Add(shotButtons[i]);
                }
            }
            else controls.Add(Text("玻璃球 · 看准碰撞线路", "section-title"));
            var power = Box("power-wrap"); controls.Add(power); power.Add(Text("力道  ·  向后拉得越远，出手越有劲", "power-title"));
            var bar = Box("power-bar"); power.Add(bar); powerFill = Box("power-fill"); bar.Add(powerFill);
            controls.Add(Text(marbles ? "六手之内，把玻璃球打出圈。" : "滚轮 / Q E 调旋转\n右键取消蓄力", "muted"));
            message = Text("", "message"); bottom.Add(message); UpdateMatch();
        }

        public void UpdateMatch()
        {
            if (score == null) return;
            if (game.Screen == AppScreen.Marbles)
            {
                score.text = game.Marbles.Score + " / 8"; ruleCaption.text = GameplayLayouts.Name(game.Marbles.Layout) + "  /  剩余 " + game.Marbles.ShotsRemaining + " 手";
                turnLabel.text = game.Marbles.Phase == MatchPhase.PlayerAim ? "你的出手 · 按住玻璃球往后拉" : "看准线路，让它停稳";
                message.text = game.Marbles.Message; powerFill.style.width = Length.Percent(game.Marbles.Power * 100);
            }
            else
            {
                var match = game.Pogs;
                score.text = game.Training ? "练手  " + match.PlayerScore + " / 3" : "你  " + match.PlayerScore + " : " + match.NpcScore + "  " + GameCatalog.GetNpc(game.NpcId).displayName;
                ruleCaption.text = GameCatalog.RuleName(match.Rule) + "制  /  " + GameplayLayouts.Name(match.Layout) + "  /  " + (game.Training ? "自己练手" : game.RealMatch ? "玩真的" : "玩假的");
                turnLabel.text = match.Phase == MatchPhase.PlayerAim ? "轮到你了 · 第 " + match.Turn + " 手" : match.Phase == MatchPhase.NpcThinking ? "朋友正在找角度" : "等圆片停稳，再看谁翻了";
                message.text = match.Message + "    旋转：" + (match.Spin > .1 ? "向右" : match.Spin < -.1 ? "向左" : "不旋") + " " + Math.Abs(match.Spin).ToString("0.0");
                powerFill.style.width = Length.Percent(match.Power * 100);
                if (shotButtons != null) for (int i = 0; i < 3; i++)
                { shotButtons[i].EnableInClassList("selected", (int)match.Shot == i); shotButtons[i].SetEnabled(match.Phase == MatchPhase.PlayerAim); }
            }
        }

        void BuildResult()
        {
            var overlay = Box("overlay", true); screen.Add(overlay); var result = Box("result", true); overlay.Add(result);
            result.Add(Text(game.ResultTitle, "result-title")); result.Add(Text(game.ResultText, "body"));
            result.Add(Click("再来一局", game.Retry, "primary"));
            result.Add(Click("打开铁盒看看", game.ShowCollection, "secondary")); result.Add(Click("回街区，换个地方玩", game.GoHub, "secondary"));
        }

        public void ShowPause()
        {
            if (pauseOverlay != null) return;
            game.Pogs.CancelAim(); game.Marbles.CancelAim();
            Time.timeScale = 0;
            pauseOverlay = Box("overlay", true); screen.Add(pauseOverlay); var modal = Box("result", true); pauseOverlay.Add(modal);
            modal.Add(Text("喝口汽水，歇一会儿", "modal-title"));
            modal.Add(Text(game.Screen == AppScreen.Pogs ? "向后拖动蓄力，松手打片。1 / 2 / 3 切换掌拍、重拍、斜擦；滚轮或 Q / E 调旋转。\n\n翻面要等圆片停稳，出圈要整张越过边界。" : "按住玻璃球向后拖动，松手击球。6 次机会，把目标球整颗撞出粉笔圈。", "detail-text"));
            modal.Add(Text(game.Screen == AppScreen.Pogs ? "这场约战会保留。回到街区后，可以从首页继续。" : "回到街区后，可以重新开始这一轮玻璃球。", "body"));
            modal.Add(Click("继续玩", ResumePause, "primary"));
            modal.Add(Click("调整音乐与声音", ShowAudioSettings, "secondary"));
            modal.Add(Click("回到街区", game.GoHub, "secondary"));
            if (game.Screen == AppScreen.Pogs) modal.Add(Click("这局认输", () => { Time.timeScale = 1; game.Forfeit(); }, "secondary"));
        }

        void ConfirmForfeit()
        {
            var overlay = Box("overlay", true); dismissOverlay = overlay; screen.Add(overlay); var panel = Box("result", true); overlay.Add(panel);
            panel.Add(Text("这局就算朋友赢了？", "modal-title")); panel.Add(Text("玩真的约定过的这一张会留在朋友手里，明天可以再约战赢回来。", "body"));
            panel.Add(Click("认输，下回再来", game.Forfeit, "primary")); panel.Add(Click("再想想", CloseDismissOverlay, "secondary"));
        }

        public bool BlocksInput(Vector2 screenPosition)
        {
            if (root.panel == null) return true;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, UnityEngine.Screen.height - screenPosition.y));
            var picked = root.panel.Pick(point);
            for (var current = picked; current != null; current = current.parent) if (current.ClassListContains("input-block")) return true;
            return false;
        }
        public void UpdateMute()
        {
            if (mute != null) mute.text = game.Audio.Muted ? "声音 · 关" : "声音 · 开";
            if (audioMuteButton != null) audioMuteButton.text = game.Audio.Muted ? "打开声音" : "暂时静音";
        }
        public void ShowAudioSettings()
        {
            if (audioOverlay != null) return;
            game.Pogs.CancelAim(); game.Marbles.CancelAim();
            audioPreviousScale = Time.timeScale;
            if (game.Screen == AppScreen.Pogs || game.Screen == AppScreen.Marbles) Time.timeScale = 0;
            audioOverlay = Box("overlay", true); screen.Add(audioOverlay);
            var panel = Box("result", true); panel.AddToClassList("audio-panel"); audioOverlay.Add(panel);
            var header = Box("modal-header"); panel.Add(header);
            header.Add(Text("听见那个夏天", "modal-title")); header.Add(Click("收起", CloseAudioSettings));
            panel.Add(Text("给音乐留一点空间，也听听圆片擦过地面的声音。", "label-caption"));
            AddVolumeSlider(panel, "总音量", game.Audio.MasterVolume, 0);
            AddVolumeSlider(panel, "音乐", game.Audio.MusicVolume, 1);
            AddVolumeSlider(panel, "出手与碰撞", game.Audio.EffectsVolume, 2);
            AddVolumeSlider(panel, "街区环境", game.Audio.AmbienceVolume, 3);
            panel.Add(Click("听一下 · 玻璃球轻碰", () => game.Audio.PlayImpact(ImpactMaterial.Glass, 2.4f, .8f), "secondary"));
            audioMuteButton = Click(game.Audio.Muted ? "打开声音" : "暂时静音", () => { game.Audio.ToggleMute(); UpdateMute(); }, "secondary"); panel.Add(audioMuteButton);
            panel.Add(Text("调整会自动记住 · M 静音 · Esc 收起", "muted"));
        }

        void StyleDropdown(DropdownField field)
        {
            var input = field.Q(className: "unity-base-popup-field__input") ?? field.Q(className: "unity-base-field__input");
            if (input != null)
            {
                input.style.backgroundImage = StyleKeyword.None;
                input.style.backgroundColor = new Color(.19f, .27f, .27f);
                input.style.borderTopColor = input.style.borderBottomColor = input.style.borderLeftColor = input.style.borderRightColor = new Color(.44f, .49f, .40f);
            }
            var text = field.Q<TextElement>(className: "unity-base-popup-field__text");
            if (text != null) text.style.color = new Color(.95f, .88f, .71f);
        }

        void AddVolumeSlider(VisualElement panel, string caption, float value, int channel)
        {
            var row = Box("volume-row"); panel.Add(row);
            var label = Text(caption + "  " + Mathf.RoundToInt(value * 100) + "%", "volume-caption"); row.Add(label);
            var slider = new Slider(0, 100) { value = value * 100, name = "volume-" + channel };
            slider.AddToClassList("volume-slider"); slider.AddToClassList("input-block"); row.Add(slider);
            slider.RegisterValueChangedCallback(evt => {
                float volume = evt.newValue / 100f;
                game.Audio.SetVolumes(channel == 0 ? volume : game.Audio.MasterVolume, channel == 1 ? volume : game.Audio.MusicVolume,
                    channel == 2 ? volume : game.Audio.EffectsVolume, channel == 3 ? volume : game.Audio.AmbienceVolume);
                label.text = caption + "  " + Mathf.RoundToInt(evt.newValue) + "%";
            });
        }

        void CloseAudioSettings()
        {
            if (audioOverlay == null) return;
            audioOverlay.RemoveFromHierarchy(); audioOverlay = null; audioMuteButton = null; Time.timeScale = audioPreviousScale;
        }
        void ResumePause()
        {
            if (pauseOverlay == null) return;
            pauseOverlay.RemoveFromHierarchy(); pauseOverlay = null; Time.timeScale = 1;
        }
        void CloseDismissOverlay() { dismissOverlay?.RemoveFromHierarchy(); dismissOverlay = null; }
        public bool HandleEscape()
        {
            if (audioOverlay != null) { CloseAudioSettings(); return true; }
            if (dismissOverlay != null) { CloseDismissOverlay(); return true; }
            if (pauseOverlay != null) { ResumePause(); return true; }
            return false;
        }

        internal string VerifyPolishControls()
        {
            float master = game.Audio.MasterVolume, music = game.Audio.MusicVolume, effects = game.Audio.EffectsVolume, ambience = game.Audio.AmbienceVolume;
            float previousScale = Time.timeScale;
            try
            {
                ShowAudioSettings();
                if (audioOverlay.Query<Slider>().ToList().Count != 4) return "Expected four independent audio controls";
                audioOverlay.Q<Slider>("volume-1").value = 37;
                if (Mathf.Abs(game.Audio.MusicVolume - .37f) > .001f || game.Audio.EffectsVolume != effects) return "Music slider changed wrong mixer route";
                if (!HandleEscape() || audioOverlay != null || Time.timeScale != previousScale) return "Escape failed to close audio settings or restore time scale";
                ShowMarbleLobby();
                if (dismissOverlay.Query<Button>(className: "challenge-card").ToList().Count != 4) return "Expected four marble challenge choices";
                if (!HandleEscape() || dismissOverlay != null) return "Escape failed to close marble challenge choices";
                return null;
            }
            finally { CloseAudioSettings(); CloseDismissOverlay(); game.Audio.SetVolumes(master, music, effects, ambience); Time.timeScale = previousScale; }
        }
        void ShowMemory()
        {
            var overlay = Box("overlay", true); dismissOverlay = overlay; screen.Add(overlay); var panel = Box("modal", true); overlay.Add(panel);
            var header = Box("modal-header"); panel.Add(header); header.Add(Text("属于我们的夏天", "modal-title"));
            header.Add(Click("收起相片", CloseDismissOverlay));
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("memory-art"); art.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("Art/summer_keyart")); panel.Add(art);
            panel.Add(Text("那时候，最舍不得的宝贝能装进一个小铁盒，最开心的下午可以一直玩到天黑。", "body"));
        }
        public void SetHighlight(string value) { if (highlight != null) highlight.text = value; }
        public void ClearHighlight() { if (highlight != null) highlight.text = ""; }
        internal string RenderVerificationFrame(RenderTexture target)
        {
            // 隐藏的 Windows 窗口不绘制 backbuffer；验证时显式渲染同一个原生 UI 面板。
            settings.targetTexture = target;
            try
            {
                var panel = root.panel;
                var method = panel.GetType().GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) return "Runtime panel Repaint method unavailable";
                method.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                var render = panel.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (render == null) return "Runtime panel Render method unavailable";
                render.Invoke(panel, null);
                if (root.worldBound.width < 1 || root.worldBound.height < 1) return "UI root has no viewport";
                return null;
            }
            catch (Exception e) { return e.InnerException?.Message ?? e.Message; }
            finally { settings.targetTexture = null; }
        }
        internal string VerifyInputBlocking()
        {
            var button = root.Query<Button>().ToList().FirstOrDefault(b => b.enabledInHierarchy && b.worldBound.width > 1 && b.worldBound.height > 1);
            if (button == null) return "No interactive button in current screen";
            var center = button.worldBound.center;
            var pointer = new Vector2(center.x * UnityEngine.Screen.width / root.worldBound.width,
                UnityEngine.Screen.height - center.y * UnityEngine.Screen.height / root.worldBound.height);
            if (!BlocksInput(pointer)) return "Button does not block gameplay input: " + button.text;
            if ((game.Screen == AppScreen.Pogs || game.Screen == AppScreen.Marbles) &&
                BlocksInput(new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f)))
                return "Arena center unexpectedly blocks gameplay input";
            return null;
        }
        public void ShowError(string value) { game.SetError(value); if (message != null) message.text = value; else Render(); }
        public void ToggleDebug(bool show)
        {
            if (debugLabel != null) { debugLabel.RemoveFromHierarchy(); debugLabel = null; }
            if (show) { debugLabel = Text("", "debug"); screen.Add(debugLabel); UpdateDebug(); }
        }
        public void UpdateDebug()
        {
            if (debugLabel == null) return;
            string targets = string.Join("\n", game.Pogs.Targets.Where(d => !d.Claimed).Select(d => "片 " + d.ArtIndex + "  v " + d.Body.linearVelocity.magnitude.ToString("0.00") + "  ω " + d.Body.angularVelocity.magnitude.ToString("0.00") + "  " + (d.FaceDown ? "背面" : "正面")));
            debugLabel.text = "F3 物理调试\n固定步长 " + Time.fixedDeltaTime.ToString("0.000") + "s\n" + game.Pogs.Phase + " / " + game.Pogs.Turn + " 手\n" + targets;
        }
        void OnDestroy() { if (settings != null) Destroy(settings); if (font != null) Destroy(font); }
    }
}
