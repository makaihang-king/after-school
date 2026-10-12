using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    /// <summary>0.5 的保存退出确认。旧类先保证现有样式，toy-* 类为微缩玩具风新版式预留。</summary>
    public sealed partial class SummerUI
    {
        Button ToyClick(string label, Action action, string variant, string legacyClass)
        {
            var button = Click(label, action, legacyClass);
            button.AddToClassList("toy-button"); button.AddToClassList("toy-button--" + variant);
            return button;
        }

        public void ShowExitConfirm()
        {
            if (exitOverlay != null) return;
            game.Pogs.CancelAim();
            if (game.Screen == AppScreen.Pogs) Time.timeScale = 0;
            UpdateShotSetupUI(game.Pogs);
            exitOverlay = Box("overlay", true); exitOverlay.name = "exit-confirm"; exitOverlay.AddToClassList("toy-overlay"); screen.Add(exitOverlay);
            var dialog = Box("result", true); dialog.AddToClassList("toy-dialog"); exitOverlay.Add(dialog);
            BuildExitDialog(dialog, null);
        }

        void BuildExitDialog(VisualElement dialog, string errorText)
        {
            dialog.Clear();
            var title = Text("要收起铁盒了吗？", "modal-title"); title.AddToClassList("toy-dialog__title"); dialog.Add(title);
            string body = "退出前会保存进度。";
            if (game.ActiveMatch != null || game.Repository.HasPendingMatch)
                body += "\n这场约战会保留，包括场景布置与双方赌注，下次启动可以直接继续。";
            if (game.Screen == AppScreen.Pogs && game.Pogs.Phase == MatchPhase.Flying)
                body += "\n飞行中的圆片会回到最近的稳定回合。";
            var bodyLabel = Text(body, "body"); bodyLabel.AddToClassList("toy-dialog__body"); dialog.Add(bodyLabel);
            if (errorText != null) dialog.Add(Text(errorText, "warning"));
            var actions = Box("route-buttons"); actions.AddToClassList("toy-actions"); dialog.Add(actions);
            var stay = ToyClick("继续玩", CloseExitOverlay, "secondary", "secondary"); stay.name = "exit-continue"; actions.Add(stay);
            var save = ToyClick("保存并退出", game.ConfirmExit, "danger", "primary"); save.name = "exit-save"; actions.Add(save);
            stay.Focus();
        }

        public void ShowExitSaveFailed(string message)
        {
            var dialog = exitOverlay?.Q(className: "toy-dialog");
            if (dialog != null) BuildExitDialog(dialog, "保存没成功：" + message + "\n可以再试一次，或先留在游戏里。");
        }

        public void CloseExitOverlay()
        {
            if (exitOverlay == null) return;
            exitOverlay.RemoveFromHierarchy(); exitOverlay = null;
            // 取消退出时按底下的浮层恢复时间：对局页里设置/暂停层仍开则保持暂停，其余情况继续游戏。
            Time.timeScale = game.Screen == AppScreen.Pogs && (audioOverlay != null || pauseOverlay != null) ? 0 : 1;
            UpdateShotSetupUI(game.Pogs);
        }

        // 探针只验证取消路径；点保存并退出会真的退出测试进程。
        internal string VerifyExitConfirm()
        {
            float previousScale = Time.timeScale;
            try
            {
                ShowExitConfirm();
                if (exitOverlay == null) return "Exit confirm did not open";
                var buttons = exitOverlay.Query<Button>().ToList();
                if (!buttons.Any(button => button.name == "exit-continue") || !buttons.Any(button => button.name == "exit-save"))
                    return "Exit dialog actions missing";
                if (!HandleEscape() || exitOverlay != null) return "Exit confirm Escape failed";
                if (Time.timeScale != 1) return "Exit confirm left timescale changed";
                return null;
            }
            finally { CloseExitOverlay(); Time.timeScale = previousScale; }
        }
    }
}
