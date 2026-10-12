using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>同一套界面服务所有童年玩法。对局时仅更新标签，不重建视觉树。</summary>
    public sealed partial class SummerUI : MonoBehaviour
    {
        SummerGame game;
        UIDocument document;
        VisualElement root, screen, powerFill, guidedPowerRange, pauseOverlay, audioOverlay, dismissOverlay, exitOverlay;
        Label score, ruleCaption, turnLabel, message, highlight, debugLabel, guidedMessage, guidedPowerStatus;
        Button mute, audioMuteButton;
        Button[] shotButtons;
        Button viewToggle, guidedAction;
        PanelSettings settings;
        Font font;
        bool ownsFont;
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
            if (root == null) throw new InvalidOperationException("主界面的 UIDocument 没有创建根节点。");
            if (!TryInitializeMobileFont())
                font = CreateRuntimeFont(24, out ownsFont);
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
            var css = Resources.Load<StyleSheet>("UI/Summer"); if (css != null) root.styleSheets.Add(css);
            InitializeMobilePanel();
            root.pickingMode = PickingMode.Ignore;
            InitializeAlerts(); game.Display.Changed += RefreshDisplayStatus;
        }

        internal static Font CreateRuntimeFont(int size, out bool runtimeCreated)
        {
            Font result = null;
            try { result = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, size); }
            catch (Exception exception) { Debug.LogWarning("Runtime font creation failed: " + exception.Message); }
            runtimeCreated = result != null;
            return result ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            root.Clear(); score = ruleCaption = turnLabel = message = highlight = debugLabel = guidedMessage = guidedPowerStatus = null;
            powerFill = guidedPowerRange = pauseOverlay = dismissOverlay = exitOverlay = null; shotButtons = null; viewToggle = guidedAction = null; mute = null;
            ClearShotSetupUI();
            screen = Box("screen"); root.Add(screen);
            switch (game.Screen)
            {
                case AppScreen.Hub: BuildHub(); break;
                case AppScreen.Lobby: if (!mobileUI) BuildHub(); BuildLobby(); break;
                case AppScreen.Collection: BuildCollection(); break;
                case AppScreen.Pogs: BuildMatch(); break;
                case AppScreen.Story: BuildStory(); break;
                case AppScreen.Tournament: BuildTournament(); break;
                case AppScreen.Shop: BuildShop(); break;
                case AppScreen.Errands: BuildErrands(); break;
                case AppScreen.Result: BuildResult(); break;
            }
            ToggleDebug(game.ShowDebug);
            ApplyMobileLayout();
            if (AlertVisible) Time.timeScale = alertPreviousScale == 0 ? 0 : (game.Screen == AppScreen.Pogs ? 0 : 1);
        }

        void Topbar()
        {
            if (mobileUI) { BuildMobileTopbar("放学后"); return; }
            var bar = Box("topbar"); screen.Add(bar); bar.Add(Text("放学后  /  SUMMER DAYS", "brand"));
            var actions = Box("top-actions"); bar.Add(actions);
            actions.Add(Click("夏天的记忆", ShowMemory));
            actions.Add(Click("游戏设置", ShowAudioSettings));
            if (game.Screen == AppScreen.Collection) AddWallet(actions);
            mute = Click(game.Audio.Muted ? "声音 · 关" : "声音 · 开", () => { game.Audio.ToggleMute(); UpdateMute(); }); actions.Add(mute);
            actions.Add(ToyClick("退出游戏", game.RequestQuit, "danger", "small-button"));
            if (game.Screen != AppScreen.Hub) actions.Add(Click("回到街区", game.GoHub));
        }

        Button PogChoice(CardDefinition definition, Action clicked)
        {
            var button = Click("", clicked, "pog-choice");
            var art = Box("pog-art"); art.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(definition.artResourcePath)); button.Add(art);
            button.Add(Text(definition.displayName, "pog-name")); return button;
        }

        void BuildCollection()
        {
            if (mobileUI) { BuildMobileCollection(); return; }
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
                if (instance.isFoundation) detail.Add(Text("练习片 · 可以交易，不能玩真的", "warning"));
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

        void BuildResult()
        {
            if (mobileUI) { BuildMobileResult(); return; }
            var overlay = Box("overlay", true); screen.Add(overlay); var result = Box("result", true); overlay.Add(result);
            result.Add(Text(game.ResultTitle, "result-title")); result.Add(Text(game.ResultText, "body"));
            result.Add(Click(game.LastSettlement?.route == MatchRoute.Story ? "继续故事" : game.LastSettlement?.route == MatchRoute.Tournament ? "查看锦标赛" : "再来一局", game.Retry, "primary"));
            result.Add(Click("去小卖部补给", game.ShowShop, "secondary"));
            result.Add(Click("打开铁盒看看", game.ShowCollection, "secondary")); result.Add(Click("回街区，换个地方玩", game.GoHub, "secondary"));
        }

        public void ShowPause()
        {
            if (pauseOverlay != null) return;
            if (mobileUI) { ShowMobilePause(); UpdateShotSetupUI(game.Pogs); return; }
            game.Pogs.CancelAim();
            Time.timeScale = 0;
            UpdateShotSetupUI(game.Pogs);
            pauseOverlay = Box("overlay", true); screen.Add(pauseOverlay); var modal = Box("result", true); pauseOverlay.Add(modal);
            modal.Add(Text("喝口汽水，歇一会儿", "modal-title"));
            modal.Add(Text("按住出手片，向后拖动蓄力，松手打片。1 / 2 / 3 切换掌拍、重拍、斜擦。\nA / D 换边，中键拖动环绕；W / S 调俯仰，滚轮调远近，Q / E 调旋转。\n底部“站位与片角”可绕场选位置、调整前后倾与左右倾；右键取消蓄力。\n\n翻面要等圆片停稳，出圈要整张越过边界。\n故事和锦标赛每局双方各押一张真片。", "detail-text"));
            modal.Add(Text("这场约战会保留，包括天气、场景布置与双方赌注。回到街区后，可以从首页继续。", "body"));
            modal.Add(Click("继续玩", ResumePause, "primary"));
            modal.Add(Click("调整音乐与声音", ShowAudioSettings, "secondary"));
            modal.Add(Click("回到街区", game.GoHub, "secondary"));
            if (game.Screen == AppScreen.Pogs) modal.Add(Click("这局认输", ConfirmForfeit, "secondary"));
            modal.Add(ToyClick("保存并退出", game.RequestExit, "danger", "secondary"));
        }

        void ConfirmForfeit()
        {
            var overlay = Box("overlay", true); dismissOverlay = overlay; screen.Add(overlay); var panel = Box("result", true); overlay.Add(panel);
            panel.Add(Text("这局就算朋友赢了？", "modal-title")); panel.Add(Text("玩真的约定过的这一张会留在朋友手里，明天可以再约战赢回来。", "body"));
            panel.Add(Click("认输，下回再来", game.Forfeit, "primary")); panel.Add(Click("再想想", CloseDismissOverlay, "secondary"));
        }

        public bool BlocksInput(Vector2 screenPosition)
        {
            if (AlertVisible) return true;
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
            if (game.Display.AwaitingConfirmation) game.Display.Revert();
            audioOverlay.RemoveFromHierarchy(); audioOverlay = null; audioMuteButton = null; Time.timeScale = audioPreviousScale;
            UpdateShotSetupUI(game.Pogs);
        }
        void ResumePause()
        {
            if (pauseOverlay == null) return;
            pauseOverlay.RemoveFromHierarchy(); pauseOverlay = null; Time.timeScale = 1;
            UpdateShotSetupUI(game.Pogs);
        }
        void CloseDismissOverlay() { dismissOverlay?.RemoveFromHierarchy(); dismissOverlay = null; }
        public bool HandleEscape()
        {
            if (AlertVisible) { CloseAlert(); return true; }
            if (exitOverlay != null) { game.CancelQuit(); CloseExitOverlay(); return true; }
            if (audioOverlay != null) { CloseAudioSettings(); return true; }
            if (dismissOverlay != null) { CloseDismissOverlay(); return true; }
            if (pauseOverlay != null) { ResumePause(); return true; }
            return false;
        }

        public void ShowExitWaiting()
        {
            if (exitOverlay == null) return;
            var body = exitOverlay.Q<Label>(className: "toy-dialog__body");
            if (body != null) body.text = "正在等这一手圆片自然停稳，再保存最近的稳定回合。\n请稍等片刻。";
            var save = exitOverlay.Q<Button>("exit-save");
            if (save != null) save.SetEnabled(false);
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
            alertSettings.targetTexture = target;
            try
            {
                var panel = root.panel;
                var method = panel.GetType().GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) return "Runtime panel Repaint method unavailable";
                method.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                var render = panel.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (render == null) return "Runtime panel Render method unavailable";
                render.Invoke(panel, null);
                if (AlertVisible)
                {
                    var priority = alertRoot.panel;
                    var repaint = priority.GetType().GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var priorityRender = priority.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (repaint == null || priorityRender == null) return "Priority panel renderer unavailable";
                    repaint.Invoke(priority, new object[] { new Event { type = EventType.Repaint } });
                    priorityRender.Invoke(priority, null);
                }
                if (root.worldBound.width < 1 || root.worldBound.height < 1) return "UI root has no viewport";
                return null;
            }
            catch (Exception e) { return e.InnerException?.Message ?? e.Message; }
            finally { settings.targetTexture = null; alertSettings.targetTexture = null; }
        }
        internal string VerifyInputBlocking()
        {
            var inputRoot = AlertVisible ? alertRoot : root;
            var button = inputRoot.Query<Button>().ToList().FirstOrDefault(b => b.enabledInHierarchy && b.worldBound.width > 1 && b.worldBound.height > 1);
            if (button == null) return "No interactive button in current screen";
            var center = button.worldBound.center;
            var pointer = new Vector2(center.x * UnityEngine.Screen.width / inputRoot.worldBound.width,
                UnityEngine.Screen.height - center.y * UnityEngine.Screen.height / inputRoot.worldBound.height);
            if (!BlocksInput(pointer)) return "Button does not block gameplay input: " + button.text;
            if (!AlertVisible && pauseOverlay == null && audioOverlay == null && exitOverlay == null && dismissOverlay == null && (game.Screen == AppScreen.Pogs) &&
                BlocksInput(new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f)))
                return "Arena center unexpectedly blocks gameplay input";
            return null;
        }
        public void ShowError(string value) { game.SetError(value); ShowNotice("先等一等", value); }
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
        void OnDestroy() { if (game != null && game.Display != null) game.Display.Changed -= RefreshDisplayStatus; if (settings != null) Destroy(settings); if (alertSettings != null) Destroy(alertSettings); if (font != null && ownsFont) Destroy(font); }
    }
}
