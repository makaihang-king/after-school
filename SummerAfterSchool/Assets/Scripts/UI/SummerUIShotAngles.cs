using UnityEngine;
using UnityEngine.UIElements;

namespace Childhood
{
    public sealed partial class SummerUI
    {
        Slider shotAzimuth, shotTiltPitch, shotTiltRoll;
        Label shotAzimuthValue, shotPitchValue, shotRollValue, shotSetupRestriction;
        Button shotSetupReset, shotSetupToggle;
        VisualElement shotSetupPanel, mobileShotSelection;
        bool mobileShotSetupExpanded;

        void ClearShotSetupUI()
        {
            shotAzimuth = shotTiltPitch = shotTiltRoll = null;
            shotAzimuthValue = shotPitchValue = shotRollValue = shotSetupRestriction = null;
            shotSetupReset = shotSetupToggle = null;
            shotSetupPanel = mobileShotSelection = null;
            mobileShotSetupExpanded = false;
        }

        void BuildDesktopShotSetup(VisualElement parent)
        {
            BuildShotSetupPanel(parent, false);
        }

        void BuildMobileShotSetup(VisualElement selection)
        {
            mobileShotSelection = selection;
            shotSetupToggle = Click("站位 / 片角", () => ToggleMobileShotSetup(true), "mobile-shot-setup-toggle");
            shotSetupToggle.name = "shot-setup-toggle";
            shotSetupToggle.tooltip = "绕场选择出手位置，调整圆片的倾斜角度。";
            selection.Add(shotSetupToggle);
            BuildShotSetupPanel(screen, true);
            ToggleMobileShotSetup(false);
        }

        void BuildShotSetupPanel(VisualElement parent, bool compactMobile)
        {
            shotSetupPanel = Box("shot-setup-panel", true); shotSetupPanel.name = "shot-setup-panel";
            shotSetupPanel.AddToClassList(compactMobile ? "mobile-shot-setup" : "desktop-shot-setup");
            parent.Add(shotSetupPanel);
            var header = Box("shot-setup-header"); shotSetupPanel.Add(header);
            header.Add(Text("站位与片角", "shot-setup-title"));
            shotSetupReset = Click("复位", () => { game.Pogs.ResetShotSetup(); UpdateMatch(); }, "shot-setup-reset");
            shotSetupReset.name = "shot-setup-reset";
            shotSetupReset.tooltip = "回到正面站位，将前后倾、左右倾和旋转归零。";
            header.Add(shotSetupReset);
            if (compactMobile)
            {
                var close = Click("收起", () => ToggleMobileShotSetup(false), "shot-setup-close");
                close.name = "shot-setup-close"; header.Add(close);
            }
            shotSetupRestriction = Text("教学先按示意练习", "shot-setup-restriction");
            shotSetupRestriction.name = "shot-setup-restriction"; shotSetupPanel.Add(shotSetupRestriction);

            shotAzimuth = AddShotSetupSlider("站位", "shot-azimuth", 0, 360, out shotAzimuthValue);
            shotAzimuth.tooltip = "绕场一整圈选择出手位置；0° 与 360° 是同一位置。蓄力时先松开或取消，再换站位。";
            shotAzimuth.RegisterValueChangedCallback(evt => { game.Pogs.SetPlayerAzimuth(evt.newValue); UpdateMatch(); });
            shotTiltPitch = AddShotSetupSlider("前后倾", "shot-tilt-pitch", -35, 35, out shotPitchValue);
            shotTiltPitch.tooltip = "相对出手方向让圆片向前或向后倾斜，待出手片会直接显示角度。";
            shotTiltPitch.RegisterValueChangedCallback(evt => { game.Pogs.SetDiscAngles(evt.newValue, game.Pogs.TiltRoll); UpdateMatch(); });
            shotTiltRoll = AddShotSetupSlider("左右倾", "shot-tilt-roll", -35, 35, out shotRollValue);
            shotTiltRoll.tooltip = "相对出手方向让圆片向左或向右倾斜，可以配合旋转调整出手。";
            shotTiltRoll.RegisterValueChangedCallback(evt => { game.Pogs.SetDiscAngles(game.Pogs.TiltPitch, evt.newValue); UpdateMatch(); });
        }

        Slider AddShotSetupSlider(string caption, string controlName, float low, float high, out Label valueLabel)
        {
            var row = Box("shot-setup-row"); shotSetupPanel.Add(row);
            valueLabel = Text(caption + " 0°", "shot-setup-value"); valueLabel.name = controlName + "-value"; row.Add(valueLabel);
            var slider = new Slider(low, high) { name = controlName };
            slider.AddToClassList("shot-setup-slider"); slider.AddToClassList("input-block"); row.Add(slider);
            return slider;
        }

        void ToggleMobileShotSetup(bool expanded)
        {
            mobileShotSetupExpanded = expanded;
            if (shotSetupPanel != null) shotSetupPanel.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (mobileShotSelection != null) mobileShotSelection.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
            screen?.EnableInClassList("mobile-setup-open", expanded);
            UpdateShotSetupUI(game.Pogs);
        }

        void UpdateShotSetupUI(PogMatch match)
        {
            if (shotAzimuth == null || match == null) return;
            // 镜头、快捷键与滑块共享同一份真实出手设置，刷新不再触发回调。
            shotAzimuth.SetValueWithoutNotify(match.PlayerAzimuth);
            shotTiltPitch.SetValueWithoutNotify(match.TiltPitch);
            shotTiltRoll.SetValueWithoutNotify(match.TiltRoll);
            shotAzimuthValue.text = "站位 " + Mathf.RoundToInt(match.PlayerAzimuth) + "°";
            shotPitchValue.text = "前后倾 " + SignedShotAngle(match.TiltPitch);
            shotRollValue.text = "左右倾 " + SignedShotAngle(match.TiltRoll);
            bool teaching = game.GuidedPracticeMode;
            bool aiming = !teaching && match.Phase == MatchPhase.PlayerAim && Time.timeScale > 0 && !AlertVisible;
            shotAzimuth.SetEnabled(aiming && !match.IsDragging);
            shotTiltPitch.SetEnabled(aiming);
            shotTiltRoll.SetEnabled(aiming);
            shotSetupReset.SetEnabled(aiming && !match.IsDragging);
            shotSetupRestriction.style.display = teaching ? DisplayStyle.Flex : DisplayStyle.None;
            if (shotSetupToggle != null)
            {
                shotSetupToggle.EnableInClassList("selected", mobileShotSetupExpanded);
            }
        }

        static string SignedShotAngle(float value)
        {
            int degrees = Mathf.RoundToInt(value);
            return (degrees > 0 ? "+" : "") + degrees + "°";
        }
    }
}
