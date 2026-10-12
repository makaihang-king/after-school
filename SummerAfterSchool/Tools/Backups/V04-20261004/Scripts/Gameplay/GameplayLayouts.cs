using System;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    public enum PracticeLayout { Classic, Fan, TwinIslands, EdgeRace }
    public enum MarbleLayout { Classic, SplitLanes, Crescent, Pinwheel }

    /// <summary>阵型只改变开局摆放；出手与得分仍由正式物理规则决定。</summary>
    public static class GameplayLayouts
    {
        public static string Name(PracticeLayout layout)
        {
            switch (layout)
            {
                case PracticeLayout.Fan: return "扇面开彩";
                case PracticeLayout.TwinIslands: return "两岸夹击";
                case PracticeLayout.EdgeRace: return "圈边五子";
                default: return "放学老阵";
            }
        }

        public static string Description(PracticeLayout layout)
        {
            switch (layout)
            {
                case PracticeLayout.Fan: return "五张片展开成扇面，练习左右落点与邻片连翻。";
                case PracticeLayout.TwinIslands: return "两组片分守左右，先选一岸，再寻找横向转移。";
                case PracticeLayout.EdgeRace: return "五张片靠近粉笔圈，练习从内侧斜擦向外。";
                default: return "熟悉的三前两后，适合初次练手和对照手感。";
            }
        }

        public static string Hint(PracticeLayout layout, RuleKind rule)
        {
            if (rule == RuleKind.RingOut) return layout == PracticeLayout.EdgeRace ? "从圈内侧擦向外，整张越线才算。" : "先找靠圈边的片，从内侧向外推。";
            switch (layout)
            {
                case PracticeLayout.Fan: return "落点偏一点，让掀边气流照顾相邻两张。";
                case PracticeLayout.TwinIslands: return "先专攻一岸，单张翻稳再换边。";
                case PracticeLayout.EdgeRace: return "远处用掌拍找落点，别一味拉满。";
                default: return "先试中等力道，圆片停稳后再看正反面。";
            }
        }

        public static string Name(MarbleLayout layout)
        {
            switch (layout)
            {
                case MarbleLayout.SplitLanes: return "两路包抄";
                case MarbleLayout.Crescent: return "月牙阵";
                case MarbleLayout.Pinwheel: return "风车阵";
                default: return "经典清圈";
            }
        }

        public static string Description(MarbleLayout layout)
        {
            switch (layout)
            {
                case MarbleLayout.SplitLanes: return "八颗分成左右两路，试着连碰同一侧的球。";
                case MarbleLayout.Crescent: return "七颗排成月牙，一颗在内侧，寻找贴圈侧击。";
                case MarbleLayout.Pinwheel: return "四颗守住内圈、四颗在外，试着借连续碰撞打开阵地。";
                default: return "错列的八颗玻璃球，六手里寻找清圈线路。";
            }
        }

        public static string Hint(MarbleLayout layout)
        {
            switch (layout)
            {
                case MarbleLayout.SplitLanes: return "先清同一侧，别把两路都撞回圈心。";
                case MarbleLayout.Crescent: return "月牙两端离边线近，擦边比正撞更有用。";
                case MarbleLayout.Pinwheel: return "先看外圈的接力球，再试内圈连碰。";
                default: return "瞄准球的内侧，把它推向最近的圈边。";
            }
        }

        public static Vector3[] PogPositions(PracticeLayout layout)
        {
            switch (layout)
            {
                case PracticeLayout.Fan:
                    return Pogs(-1.15f, .25f, -.60f, .95f, 0f, 1.26f, .60f, .95f, 1.15f, .25f);
                case PracticeLayout.TwinIslands:
                    return Pogs(-1.02f, .13f, -1.02f, .92f, .65f, .10f, 1.05f, .78f, .28f, 1.16f);
                case PracticeLayout.EdgeRace:
                    return Pogs(-1.42f, .55f, -.98f, 1.68f, 0f, 1.96f, .98f, 1.68f, 1.42f, .55f);
                default:
                    // 和已在 Windows Player 校准的三前两后坐标完全一致。
                    return Pogs(-.83f, .25f, 0f, .25f, .83f, .25f, -.83f, 1.20f, 0f, 1.20f);
            }
        }

        public static Vector3[] MarblePositions(MarbleLayout layout)
        {
            switch (layout)
            {
                case MarbleLayout.SplitLanes:
                    return Marbles(-.92f, -.12f, -.92f, .50f, -.92f, 1.12f, -.46f, 1.65f, .92f, -.12f, .92f, .50f, .92f, 1.12f, .46f, 1.65f);
                case MarbleLayout.Crescent:
                    return Marbles(-1.30f, .28f, -1.07f, 1.18f, -.60f, 1.69f, 0f, 1.84f, .60f, 1.69f, 1.07f, 1.18f, 1.30f, .28f, 0f, .25f);
                case MarbleLayout.Pinwheel:
                    return Marbles(-.27f, .28f, .27f, .28f, -.27f, .82f, .27f, .82f, -1.10f, .55f, 1.10f, .55f, 0f, -.55f, 0f, 1.65f);
                default:
                    return Marbles(-.60f, -.04f, 0f, -.10f, .60f, -.04f, -.82f, .64f, -.25f, .60f, .35f, .69f, .90f, .64f, .10f, 1.32f);
            }
        }

        // 给 RuntimeProbe 使用：只验证摆放几何，不代替 PhysX/实际输入验证。
        public static IReadOnlyList<string> ValidateGeometry()
        {
            var errors = new List<string>();
            foreach (PracticeLayout layout in Enum.GetValues(typeof(PracticeLayout)))
                ValidatePositions(PogPositions(layout), 5, .31f, 0f, "圆片 " + layout, errors);
            foreach (MarbleLayout layout in Enum.GetValues(typeof(MarbleLayout)))
                ValidatePositions(MarblePositions(layout), 8, .155f, .025f, "玻璃球 " + layout, errors);
            return errors;
        }

        static void ValidatePositions(Vector3[] positions, int count, float radius, float jitter, string label, List<string> errors)
        {
            if (positions.Length != count) errors.Add(label + "目标数量改变。");
            for (int i = 0; i < positions.Length; i++)
            {
                var offset = positions[i] - new Vector3(0, positions[i].y, .55f);
                if (offset.magnitude + radius + jitter > 1.85f) errors.Add(label + "第 " + i + " 个目标开局越线。");
                for (int j = 0; j < i; j++)
                    if (Vector3.Distance(positions[i], positions[j]) < radius * 2 + jitter * 2)
                        errors.Add(label + "第 " + j + "/" + i + " 个目标开局重叠。");
            }
        }

        static Vector3[] Pogs(params float[] pairs) { return Positions(.08f, pairs); }
        static Vector3[] Marbles(params float[] pairs) { return Positions(.156f, pairs); }
        static Vector3[] Positions(float height, float[] pairs)
        {
            var positions = new Vector3[pairs.Length / 2];
            for (int i = 0; i < positions.Length; i++) positions[i] = new Vector3(pairs[i * 2], height, pairs[i * 2 + 1]);
            return positions;
        }
    }
}
