using System;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    /// <summary>身份决定外观与动作；场边站位和访问次数不再改变一个孩子的样子。</summary>
    public static class CompanionPersonality
    {
        public enum Motion { Wave, Fist, Arc, Nod, Salute }

        public sealed class Profile
        {
            public readonly string NpcId, DisplayName, Greeting, GestureName;
            public readonly int Style;
            public readonly Color ShirtColor, BagColor, ShortsColor, ShoeColor;
            public readonly float Height, BodyWidth, Phase, ReactionDelay, IdleLean;
            public readonly bool Girl, Cap;
            public readonly Motion Signature;

            public Profile(string npcId, string displayName, int style, Color shirtColor, Color bagColor,
                float height, float bodyWidth, bool girl, bool cap, Motion signature, string gestureName,
                float reactionDelay, float idleLean, string greeting)
            {
                NpcId = npcId; DisplayName = displayName; Style = style;
                ShirtColor = shirtColor; BagColor = bagColor; Height = height; BodyWidth = bodyWidth;
                Girl = girl; Cap = cap; Signature = signature; GestureName = gestureName;
                ReactionDelay = reactionDelay; IdleLean = idleLean; Greeting = greeting; Phase = style * 1.84f;
                ShortsColor = new Color(.24f + style * .018f, .34f, .43f - style * .018f);
                ShoeColor = style % 2 == 0 ? new Color(.37f, .53f, .60f) : new Color(.65f, .28f, .19f);
            }
        }

        static readonly Profile[] profiles =
        {
            new Profile("xiaoyu", "小雨", 0, new Color(.33f,.68f,.61f), new Color(.27f,.43f,.58f),
                .98f, .97f, false, false, Motion.Wave, "wave", .025f, -1.8f,
                "书包放这儿，我在旁边看着你呢。"),
            new Profile("dazhi", "大志", 1, new Color(.91f,.62f,.26f), new Color(.58f,.38f,.23f),
                1.08f, 1.10f, false, true, Motion.Fist, "fist", .055f, 1.2f,
                "刚喝完汽水，精神足着呢！来，站稳了再出手。"),
            new Profile("taotao", "桃桃", 2, new Color(.91f,.46f,.60f), new Color(.44f,.55f,.37f),
                .97f, .94f, true, false, Motion.Arc, "arc", .105f, -2.2f,
                "我给你留了个位置，看看今天这道纸边。"),
            new Profile("linlin", "林林", 3, new Color(.55f,.47f,.73f), new Color(.37f,.47f,.53f),
                1.03f, .93f, true, false, Motion.Nod, "nod", .19f, .5f,
                "别着急。我想看看你最拿手的那一下。"),
            new Profile("doudou", "豆豆", 4, new Color(.78f,.63f,.31f), new Color(.46f,.57f,.39f),
                .94f, 1.02f, false, false, Motion.Wave, "wave", .08f, -1.0f,
                "粉笔圈画好啦！我把风车先放在书包里。"),
            new Profile("ayue", "阿岳", 5, new Color(.40f,.61f,.75f), new Color(.47f,.36f,.27f),
                1.04f, 1.04f, false, false, Motion.Salute, "salute", .13f, 1.0f,
                "门口这块地我熟，书包搁路沿上就好。"),
            new Profile("ayan", "阿砚", 6, new Color(.66f,.50f,.33f), new Color(.34f,.41f,.45f),
                1.10f, .98f, false, false, Motion.Nod, "nod", .23f, 0f,
                "一路走到操场了。歇口气，我等你准备好。")
        };
        static readonly IReadOnlyList<string> npcIds = Array.AsReadOnly(new[]
            { "xiaoyu", "dazhi", "taotao", "linlin", "doudou", "ayue", "ayan" });

        public static IReadOnlyList<string> NpcIds => npcIds;
        public static bool TryGet(string npcId, out Profile profile)
        {
            foreach (var candidate in profiles)
                if (candidate.NpcId == npcId) { profile = candidate; return true; }
            profile = null; return false;
        }
        public static Profile Get(string npcId)
        {
            if (TryGet(npcId, out var profile)) return profile;
            throw new ArgumentException("未知伙伴身份：" + npcId, nameof(npcId));
        }
        public static string Greeting(string npcId)
        {
            return TryGet(npcId, out var profile) ? profile.Greeting : "我在场边等你，准备好了就来。";
        }
    }
}
