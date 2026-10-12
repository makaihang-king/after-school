using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    public sealed partial class SummerUI
    {
        int mobileCollectionPage;

        void BuildMobileHub()
        {
            var page = MobilePage("放学后");
            page.name = "mobile-hub";
            page.AddToClassList("mobile-hub");
            page.style.flexDirection = FlexDirection.Row;
            page.style.alignItems = Align.Stretch;

            var hero = Box("mobile-hub-welcome", true);
            hero.AddToClassList("v04-hero");
            // 保留旧验收 hook，但手机首页不用旧页面的绝对坐标。
            hero.style.position = Position.Relative;
            hero.style.left = StyleKeyword.Auto; hero.style.right = StyleKeyword.Auto;
            hero.style.top = StyleKeyword.Auto; hero.style.bottom = StyleKeyword.Auto;
            hero.style.width = Length.Percent(34);
            hero.style.minWidth = 0; hero.style.flexShrink = 0;
            hero.style.paddingLeft = 14; hero.style.paddingRight = 14;
            hero.style.paddingTop = 12; hero.style.paddingBottom = 12;
            page.Add(hero);
            hero.Add(MobileHomeText("一九九六 · 夏天 · " + Application.version, "mobile-kicker", 18));
            hero.Add(MobileHomeText("把夏天装进铁盒", "mobile-home-title", 30));
            var location = GameCatalog.GetLocation(game.LocationId);
            var place = MobileHomeText(location.displayName, "mobile-place-name", 24);
            place.style.marginTop = 8; hero.Add(place);
            hero.Add(MobileHomeText(ClockLabel(), "mobile-caption", 18));
            hero.Add(MobileHomeText(MobileLocationCaption(location.id), "mobile-caption", 18));

            var pending = game.Repository.GetPendingMatch();
            var status = pending != null
                ? GameCatalog.GetNpc(pending.npcId).displayName + "还在等你 · " + (pending.isReal ? "双方各押一张" : "练习切磋")
                : "收集 " + game.Repository.PlayerCards.Select(card => card.definitionId).Distinct().Count() + " / " + GameCatalog.Cards.Length + " 张";
            var progress = MobileHomeText(status, "mobile-home-status", 18);
            progress.style.whiteSpace = WhiteSpace.Normal; progress.style.marginTop = 10;
            hero.Add(progress);
            var breathingRoom = Box("mobile-home-space"); breathingRoom.style.flexGrow = 1; hero.Add(breathingRoom);
            var play = Click(pending != null ? "继续那场约战" : "自由约战 / 练手", pending != null ? (Action)game.ResumePogs : game.ShowLobby, "mobile-primary");
            play.name = pending != null ? "resume-match" : "open-free-play";
            MobileHomeButtonSize(play, 56, 22); hero.Add(play);
            var footer = Box("mobile-home-local-actions"); footer.style.flexDirection = FlexDirection.Row;
            footer.style.marginTop = 8; hero.Add(footer);
            var chooseLocation = Click("换个地方", ShowMobileLocations, "mobile-secondary");
            chooseLocation.name = "open-locations"; MobileHomeButtonSize(chooseLocation, 50, 20);
            chooseLocation.style.flexGrow = 1; chooseLocation.style.flexBasis = 0; footer.Add(chooseLocation);
            if (pending != null)
            {
                var forfeit = Click("认输", ConfirmForfeit, "mobile-secondary");
                forfeit.name = "forfeit-match"; MobileHomeButtonSize(forfeit, 50, 20);
                forfeit.style.width = 76; forfeit.style.marginLeft = 8; footer.Add(forfeit);
            }

            var sceneWindow = Box("mobile-hub-scene-window"); sceneWindow.style.flexGrow = 1; page.Add(sceneWindow);
            var routes = Box("mobile-hub-routes");
            routes.style.width = Length.Percent(49); routes.style.flexShrink = 0;
            routes.style.justifyContent = Justify.Center;
            page.Add(routes);
            var save = game.Repository.Current;
            var errandCaption = save.activeErrand == null ? "跑腿赚玻璃珠" :
                "送到" + GameCatalog.GetLocation(save.activeErrand.destinationLocationId).displayName;
            var first = MobileHomeTileRow(routes);
            first.Add(MobileHubTile("01", "街区故事", "已完成 " + save.story.clearedStageIds.Count + " / 8", game.ShowStory, "open-story", "mobile-tile-story"));
            first.Add(MobileHubTile("02", "锦标赛", "八人签表，三轮夺冠", game.ShowTournament, "open-tournament", "mobile-tile-tournament"));
            var second = MobileHomeTileRow(routes);
            second.Add(MobileHubTile("03", "我的铁盒", "珍藏与夏日记忆", game.ShowCollection, "open-collection", "mobile-tile-collection"));
            second.Add(MobileHubTile("04", "小卖部", "买片 · 卖片 · 换片", game.ShowShop, "open-shop", "mobile-tile-shop"));
            var third = MobileHomeTileRow(routes);
            third.Add(MobileHubTile("05", "跑一趟腿", errandCaption, game.ShowErrands, "open-errands", "mobile-tile-errands"));
            third.Add(MobileHubTile("06", "出手教学", "跟小雨练会第一拍", game.StartGuidedPractice, "open-guided-practice", "mobile-tile-practice"));
        }

        VisualElement MobileHomeTileRow(VisualElement parent)
        {
            var row = Box("mobile-hub-tile-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = 106; row.style.flexShrink = 0;
            row.style.marginTop = 5; row.style.marginBottom = 5;
            parent.Add(row); return row;
        }

        Button MobileHubTile(string number, string title, string caption, Action action, string name, string variation)
        {
            var tile = Click("", action, "mobile-hub-tile");
            tile.name = name; tile.AddToClassList(variation);
            tile.style.flexGrow = 1; tile.style.flexBasis = 0; tile.style.minWidth = 0;
            tile.style.marginLeft = 4; tile.style.marginRight = 4;
            tile.style.paddingLeft = 12; tile.style.paddingRight = 12;
            tile.style.paddingTop = 9; tile.style.paddingBottom = 9;
            tile.style.alignItems = Align.FlexStart; tile.style.justifyContent = Justify.Center;
            tile.Add(MobileHomeText(number + "  /", "mobile-tile-number", 18));
            tile.Add(MobileHomeText(title, "mobile-tile-title", 24));
            tile.Add(MobileHomeText(caption, "mobile-tile-caption", 18));
            return tile;
        }

        void ShowMobileLocations()
        {
            ShowMobileSheet("今天去哪儿玩？", body =>
            {
                var grid = Box("mobile-location-grid"); grid.AddToClassList("v04-locations");
                grid.style.position = Position.Relative;
                grid.style.left = StyleKeyword.Auto; grid.style.right = StyleKeyword.Auto;
                grid.style.top = StyleKeyword.Auto; grid.style.bottom = StyleKeyword.Auto;
                grid.style.width = Length.Percent(100); grid.style.height = StyleKeyword.Auto;
                grid.style.paddingLeft = 0; grid.style.paddingRight = 0;
                grid.style.paddingTop = 0; grid.style.paddingBottom = 0;
                grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap;
                body.Add(grid);
                foreach (var location in GameCatalog.Locations)
                {
                    string id = location.id;
                    var button = Click("", () => game.SelectLocation(id), "mobile-location-card");
                    button.name = "location-" + id;
                    button.style.width = Length.Percent(31.333f); button.style.height = 66;
                    button.style.marginLeft = Length.Percent(1); button.style.marginRight = Length.Percent(1);
                    button.style.marginBottom = 8; button.style.flexShrink = 0;
                    button.style.alignItems = Align.FlexStart;
                    button.Add(MobileHomeText(location.displayName, "mobile-location-title", 22));
                    button.Add(MobileHomeText(location.surfaceName, "mobile-caption", 18));
                    button.EnableInClassList("selected", id == game.LocationId);
                    grid.Add(button);
                }
                var rest = Click("歇到下一时段", game.Rest, "mobile-secondary");
                rest.name = "rest-next-period"; MobileHomeButtonSize(rest, 56, 22); rest.style.width = 400;
                var actions = Box("mobile-fixed-sheet-actions"); actions.Add(rest); body.Add(actions);
            });
        }

        static string MobileLocationCaption(string id)
        {
            switch (id)
            {
                case "home": return "葡萄架下，安心练手。";
                case "school_gate": return "放下书包，路沿就是擂台。";
                case "alley": return "汽水还凉，砖墙晒得暖。";
                case "old_tree": return "借一点树荫，试一试掀边。";
                case "square": return "粉笔圈里，谁来守擂？";
                case "shop": return "檐下不怕雨，顺便换张片。";
                case "corridor": return "窗影落满地，轻一点出手。";
                default: return "旧跑道边，等一场盛夏约定。";
            }
        }

        void BuildMobileCollection()
        {
            var page = MobilePage("我的铁盒"); page.name = "mobile-collection";
            page.AddToClassList("collection"); page.AddToClassList("mobile-collection");
            page.style.paddingLeft = 0; page.style.paddingRight = 0;
            page.style.paddingTop = 0; page.style.paddingBottom = 0;
            var filters = Box("mobile-collection-filters"); filters.style.flexDirection = FlexDirection.Row;
            filters.style.height = 48; filters.style.flexShrink = 0; filters.style.marginBottom = 8;
            page.Add(filters);
            string[] names = { "全部图鉴", "我的圆片", "舍不得", "赢回来" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var filter = Click(names[i], () => { collectionFilter = index; mobileCollectionPage = 0; inspectedInstanceId = null; Render(); }, "mobile-choice");
                filter.name = "collection-filter-" + index; filter.EnableInClassList("selected", collectionFilter == i);
                MobileHomeButtonSize(filter, 48, 20); filter.style.flexGrow = 1; filter.style.flexBasis = 0;
                filter.style.marginRight = 6; filters.Add(filter);
            }
            var sorting = Click(collectionSortRarity ? "珍稀优先" : "系列顺序", () =>
            { collectionSortRarity = !collectionSortRarity; mobileCollectionPage = 0; Render(); }, "mobile-secondary");
            sorting.name = "collection-sort"; MobileHomeButtonSize(sorting, 48, 20);
            sorting.style.width = 120; filters.Add(sorting);

            var definitions = GameCatalog.Cards.Where(d => collectionFilter == 0 ||
                collectionFilter == 1 && game.Repository.PlayerCards.Any(c => c.definitionId == d.id) ||
                collectionFilter == 2 && game.Repository.PlayerCards.Any(c => c.definitionId == d.id && c.treasured) ||
                collectionFilter == 3 && game.Repository.LostPlayerCards.Any(c => c.definitionId == d.id));
            if (collectionSortRarity) definitions = definitions.OrderByDescending(d => d.rarity).ThenBy(d => d.id);
            var visible = definitions.ToList();
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(visible.Count / 6f));
            mobileCollectionPage = Mathf.Clamp(mobileCollectionPage, 0, pageCount - 1);
            var cardsOnPage = visible.Skip(mobileCollectionPage * 6).Take(6).ToList();
            if (cardsOnPage.Count > 0 && !cardsOnPage.Any(d => d.id == game.InspectedCardId))
            { game.InspectedCardId = cardsOnPage[0].id; inspectedInstanceId = null; }

            var main = Box("mobile-collection-main"); main.style.flexDirection = FlexDirection.Row;
            main.style.flexGrow = 1; main.style.minHeight = 0; page.Add(main);
            var left = Box("mobile-collection-browser"); left.style.width = Length.Percent(64);
            left.style.flexShrink = 0; left.style.minWidth = 0; left.style.minHeight = 0; main.Add(left);
            var grid = Box("mobile-collection-grid"); grid.style.flexGrow = 1; grid.style.minHeight = 0; left.Add(grid);
            if (cardsOnPage.Count == 0)
            {
                var empty = Text(collectionFilter == 2 ? "还没有舍不得的圆片。\n打开喜欢的一张，标记珍藏。" :
                    collectionFilter == 3 ? "没有待追回的圆片。\n铁盒里的夏天，都还在。" : "这里暂时没有圆片。\n去约战或小卖部看看吧。", "mobile-empty");
                empty.style.fontSize = 22; empty.style.whiteSpace = WhiteSpace.Normal;
                grid.Add(empty);
            }
            else
            {
                for (int rowIndex = 0; rowIndex < 2; rowIndex++)
                {
                    var row = Box("mobile-collection-card-row"); row.style.flexDirection = FlexDirection.Row;
                    row.style.flexGrow = 1; row.style.flexBasis = 0; row.style.minHeight = 0;
                    if (rowIndex == 0) row.style.marginBottom = 8; grid.Add(row);
                    for (int column = 0; column < 3; column++)
                    {
                        int itemIndex = rowIndex * 3 + column;
                        if (itemIndex >= cardsOnPage.Count)
                        {
                            var blank = Box("mobile-collection-card-blank"); blank.style.flexGrow = 1; blank.style.flexBasis = 0;
                            blank.style.marginRight = 6; row.Add(blank); continue;
                        }
                        var definition = cardsOnPage[itemIndex]; string id = definition.id;
                        var choice = PogChoice(definition, () => { game.InspectedCardId = id; inspectedInstanceId = null; Render(); });
                        choice.name = "collection-card-" + id; choice.AddToClassList("mobile-collection-card");
                        choice.style.width = StyleKeyword.Auto; choice.style.height = StyleKeyword.Auto;
                        choice.style.flexGrow = 1; choice.style.flexBasis = 0; choice.style.minWidth = 0; choice.style.minHeight = 0;
                        choice.style.marginRight = 6; choice.style.marginBottom = 0;
                        choice.style.paddingLeft = 5; choice.style.paddingRight = 5;
                        choice.style.paddingTop = 4; choice.style.paddingBottom = 4;
                        var art = choice.Q(className: "pog-art"); art.style.width = 72; art.style.height = 72; art.style.flexShrink = 0;
                        var title = choice.Q<Label>(className: "pog-name");
                        title.style.fontSize = 22; title.style.marginTop = 3;
                        title.style.maxWidth = Length.Percent(100); title.style.whiteSpace = WhiteSpace.NoWrap;
                        title.style.overflow = Overflow.Hidden; title.style.textOverflow = TextOverflow.Ellipsis;
                        int count = game.Repository.PlayerCards.Count(c => c.definitionId == id);
                        choice.EnableInClassList("locked", count == 0);
                        choice.EnableInClassList("selected", id == game.InspectedCardId);
                        choice.Add(MobileHomeText(count > 0 ? "拥有 " + count + " 张" : "未拥有", "mobile-card-count", 18));
                        row.Add(choice);
                    }
                }
            }
            MobilePager(left, mobileCollectionPage, pageCount, next =>
            { mobileCollectionPage = next; inspectedInstanceId = null; Render(); });

            var preview = Box("mobile-collection-preview", true); preview.name = "collection-detail";
            preview.style.flexGrow = 1; preview.style.flexBasis = 0; preview.style.minWidth = 0;
            preview.style.marginLeft = 8; preview.style.paddingLeft = 12; preview.style.paddingRight = 12;
            preview.style.paddingTop = 12; preview.style.paddingBottom = 12; main.Add(preview);
            // 空筛选不读取旧 InspectedCardId，也不显示上一类圆片的操作。
            if (cardsOnPage.Count == 0)
            {
                preview.Add(MobileHomeText("点开一张圆片", "mobile-preview-title", 24));
                var empty = Text("这里会显示实物、珍藏状态和约战入口。", "mobile-caption");
                empty.style.fontSize = 18; empty.style.whiteSpace = WhiteSpace.Normal; preview.Add(empty);
                return;
            }
            var inspected = cardsOnPage.First(d => d.id == game.InspectedCardId);
            var copies = MobileCollectionCopies(inspected.id);
            var selected = copies.FirstOrDefault(c => c.instanceId == inspectedInstanceId) ?? copies.FirstOrDefault();
            inspectedInstanceId = selected?.instanceId;
            BuildMobileCollectionPreview(preview, inspected, copies, selected);
        }

        List<CardInstance> MobileCollectionCopies(string definitionId)
        {
            var copies = game.Repository.PlayerCards.Where(c => c.definitionId == definitionId)
                .Concat(game.Repository.LostPlayerCards.Where(c => c.definitionId == definitionId))
                .Where(c => collectionFilter != 1 || c.ownerId == GameCatalog.PlayerOwnerId)
                .Where(c => collectionFilter != 2 || c.ownerId == GameCatalog.PlayerOwnerId && c.treasured)
                .Where(c => collectionFilter != 3 || c.ownerId != GameCatalog.PlayerOwnerId).ToList();
            if (copies.Count == 0) copies = game.Repository.PlayerCards.Where(c => c.definitionId == definitionId).ToList();
            return copies;
        }

        void BuildMobileCollectionPreview(VisualElement preview, CardDefinition definition, List<CardInstance> copies, CardInstance selected)
        {
            var header = Box("mobile-preview-header"); header.style.flexDirection = FlexDirection.Row; preview.Add(header);
            var art = Box("mobile-preview-art"); art.style.width = 66; art.style.height = 66;
            art.style.flexShrink = 0; art.style.marginRight = 8;
            art.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(definition.artResourcePath)); header.Add(art);
            var titles = Box("mobile-preview-heading"); titles.style.flexGrow = 1; titles.style.minWidth = 0; header.Add(titles);
            titles.Add(MobileHomeText(definition.displayName, "mobile-preview-title", 24));
            titles.Add(MobileHomeText(GameCatalog.RarityName(definition.rarity), "mobile-caption", 18));
            var series = MobileHomeText(definition.series, "mobile-caption", 18); series.style.marginTop = 6; preview.Add(series);

            if (copies.Count > 1)
            {
                int index = copies.IndexOf(selected);
                var pick = Click("第 " + (index + 1) + " 张 / " + copies.Count + " 张 · 更换", () => ShowMobileCollectionCopies(definition, copies), "mobile-secondary");
                pick.name = "collection-copy-picker"; MobileHomeButtonSize(pick, 46, 20);
                pick.style.marginTop = 8; preview.Add(pick);
            }
            var instance = selected != null && selected.ownerId == GameCatalog.PlayerOwnerId ? selected : null;
            var lost = instance == null ? selected ?? game.Repository.LostPlayerCards.FirstOrDefault(c => c.definitionId == definition.id) : null;
            string status = instance != null
                ? instance.isFoundation ? "练习片 · 禁止押注" : instance.treasured ? "舍不得 · 已保护" :
                    game.Repository.IsCardLocked(instance.instanceId) ? "正在约战 · 已锁定" : "我的真片 · 可以押注"
                : lost != null ? "在" + GameCatalog.GetNpc(lost.ownerId).displayName + "手里" : "还没有收进铁盒";
            var ownership = MobileHomeText(status, "mobile-preview-status", 18);
            ownership.style.whiteSpace = WhiteSpace.Normal; ownership.style.marginTop = 8; preview.Add(ownership);
            if (selected != null) preview.Add(MobileHomeText(selected.WearDescription + " · " + selected.wins + " 胜 / " + selected.losses + " 负", "mobile-caption", 18));
            var spacer = Box("mobile-preview-space"); spacer.style.flexGrow = 1; preview.Add(spacer);
            var actions = Box("mobile-preview-actions"); actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 8; preview.Add(actions);
            var info = Click("详情 / 履历", () => ShowMobileCollectionDetail(definition, selected), "mobile-secondary");
            info.name = "collection-show-history"; MobileHomeButtonSize(info, 48, 20);
            info.style.flexGrow = 1; info.style.flexBasis = 0; actions.Add(info);
            if (instance != null)
            {
                string instanceId = instance.instanceId;
                var protect = Click(instance.treasured ? "取消珍藏" : "标记珍藏", () =>
                {
                    try { game.Repository.SetTreasured(instanceId, !instance.treasured); Render(); }
                    catch (Exception exception) { ShowError(exception.Message); }
                }, "mobile-secondary");
                protect.name = "collection-toggle-treasured"; MobileHomeButtonSize(protect, 48, 20);
                protect.style.flexGrow = 1; protect.style.flexBasis = 0; protect.style.marginLeft = 6; actions.Add(protect);
                var play = Click("拿它去约战", () => { game.SelectCard(instanceId); game.ShowLobby(); }, "mobile-primary");
                play.name = "collection-play-card"; MobileHomeButtonSize(play, 56, 22); play.style.marginTop = 8; preview.Add(play);
            }
            else if (lost != null)
            {
                var recover = Click("把这张赢回来", () =>
                {
                    game.SelectNpc(lost.ownerId); game.PreferredRecoveryInstanceId = lost.instanceId;
                    game.RealMatch = true; game.ShowLobby();
                }, "mobile-primary");
                recover.name = "collection-recover-card"; MobileHomeButtonSize(recover, 56, 22);
                recover.style.marginTop = 8; preview.Add(recover);
            }
            else
            {
                var challenge = Click("去找朋友约战", game.ShowLobby, "mobile-primary");
                MobileHomeButtonSize(challenge, 56, 22); challenge.style.marginTop = 8; preview.Add(challenge);
            }
        }

        void ShowMobileCollectionCopies(CardDefinition definition, List<CardInstance> copies)
        {
            ShowMobileSheet(definition.displayName + " · 选择实物", body =>
            {
                for (int i = 0; i < copies.Count; i++)
                {
                    var copy = copies[i]; string id = copy.instanceId;
                    string owner = copy.ownerId == GameCatalog.PlayerOwnerId ? copy.treasured ? "舍不得" : "在我的铁盒" : GameCatalog.GetNpc(copy.ownerId).displayName + "手里";
                    var button = Click("第 " + (i + 1) + " 张 · " + owner + "\n" + copy.WearDescription + " · " + copy.wins + " 胜 / " + copy.losses + " 负", () =>
                    { inspectedInstanceId = id; Render(); }, "mobile-copy-choice");
                    button.name = "collection-instance-" + id;
                    MobileHomeButtonSize(button, 70, 20); button.style.marginBottom = 8;
                    button.style.whiteSpace = WhiteSpace.Normal;
                    button.EnableInClassList("selected", id == inspectedInstanceId); body.Add(button);
                }
            });
        }

        void ShowMobileCollectionDetail(CardDefinition definition, CardInstance selected)
        {
            ShowMobileSheet(definition.displayName + " · 夏日记忆", body =>
            {
                var story = Text(definition.story, "mobile-detail-copy"); story.style.fontSize = 20; story.style.whiteSpace = WhiteSpace.Normal; body.Add(story);
                var feel = Text(definition.weightFeel + "\n" + definition.gripFeel + "\n" + definition.springFeel + "\n" + definition.spinFeel, "mobile-detail-copy");
                feel.style.fontSize = 18; feel.style.whiteSpace = WhiteSpace.Normal; feel.style.marginTop = 12; body.Add(feel);
                if (selected == null) return;
                var origin = Text("来源：" + selected.origin + "\n" + selected.wins + " 胜 / " + selected.losses + " 负 · " + selected.WearDescription +
                    (selected.isFoundation ? "\n练习片可以交易，不能玩真的。" : ""), "mobile-detail-copy");
                origin.style.fontSize = 18; origin.style.whiteSpace = WhiteSpace.Normal; origin.style.marginTop = 12; body.Add(origin);
                if (selected.history.Count == 0) return;
                var title = MobileHomeText("它记得的下午", "mobile-preview-title", 24); title.style.marginTop = 16; body.Add(title);
                foreach (var memory in selected.history.AsEnumerable().Reverse())
                {
                    var entry = Text("· " + memory.description, "mobile-detail-copy");
                    entry.style.fontSize = 18; entry.style.whiteSpace = WhiteSpace.Normal; entry.style.marginTop = 8; body.Add(entry);
                }
            });
        }

        static Label MobileHomeText(string text, string className, int size)
        {
            var label = Text(text, className); label.style.fontSize = size;
            label.style.minWidth = 0; label.style.maxWidth = Length.Percent(100);
            label.style.whiteSpace = WhiteSpace.NoWrap; label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
        }

        static void MobileHomeButtonSize(Button button, int height, int fontSize)
        {
            button.style.height = height; button.style.minHeight = height;
            button.style.flexShrink = 0; button.style.fontSize = fontSize;
            button.style.marginTop = 0; button.style.marginBottom = 0;
            button.style.paddingLeft = 8; button.style.paddingRight = 8;
        }
    }
}
