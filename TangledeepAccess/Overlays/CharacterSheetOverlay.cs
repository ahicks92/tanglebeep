using System.Collections.Generic;
using HarmonyLib;
using TangledeepAccess.Focus;
using TangledeepAccess.Speech;
using TangledeepAccess.Ui;
using TangledeepAccess.Ui.Graph;
using ECSValue = Switch_UICharacterSheet.ECharacterSheetValueType;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// The character sheet (<c>Switch_UICharacterSheet</c>, the "C" tab) — a read-only stats viewer.
    /// The game splits it into a CORE STATS tab and an ADVENTURE tab; like the equipment sheet, we
    /// ignore that topology and flatten everything into one owned vertical list so there is no tab to
    /// switch — just scroll.
    ///
    /// <para><b>Data source:</b> the screen's own per-stat <c>CharacterSheetInfoPoint</c> objects (held
    /// in the private <c>listAllInfoLines[tab][column]</c>). Each point's <c>Update()</c> recomputes its
    /// label/value from live hero data even when its tab is inactive, so we read both tabs without
    /// switching. Status effects and feats come straight from the hero's own text builders. Read live
    /// each build; never cached (we hold live references to the points and read on demand).</para>
    ///
    /// <para><b>Actions:</b> none — it is informational. Navigation reads each stat (label + value, plus
    /// a defense and damage value for the elemental lines); <c>K</c> reads the game's own detailed
    /// tooltip for the focused stat (the description-with-live-math via
    /// <c>SetTooltipForCharacterSheetOption</c>). The pet-info block is deferred.</para>
    /// </summary>
    internal sealed class CharacterSheetOverlay : IUiOverlay {
        // The screen's per-tab, per-column stat lines; private on the screen type.
        private static readonly AccessTools.FieldRef<Switch_UICharacterSheet,
            List<List<List<UIManagerScript.UIObject>>>> InfoLines =
            AccessTools.FieldRefAccess<Switch_UICharacterSheet,
                List<List<List<UIManagerScript.UIObject>>>>("listAllInfoLines");

        // The value type an info point represents; private on the point type. Drives the K tooltip.
        private static readonly AccessTools.FieldRef<CharacterSheetInfoPoint, ECSValue> PointValueType =
            AccessTools.FieldRefAccess<CharacterSheetInfoPoint, ECSValue>("myValueType");

        public OverlayId Id => OverlayId.CharacterSheet;

        public OverlayResult Handler() {
            return Screen() != null ? OverlayResult.Active(this) : OverlayResult.Inactive;
        }

        private static Switch_UICharacterSheet Screen() {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            if (ums == null || !(ums.currentFullScreenUI is Switch_UICharacterSheet cs)) {
                return null;
            }

            return cs.gameObject.activeInHierarchy ? cs : null;
        }

        public void Build(IOverlayBuilder builder) {
            builder.CaptureInput();

            Switch_UICharacterSheet screen = Screen();
            if (screen == null) {
                return;
            }

            List<List<List<UIManagerScript.UIObject>>> lines = InfoLines(screen);
            if (lines == null) {
                return;
            }

            BuildHeader(builder);

            // Core tab: column 0 is attributes/offense, column 1 is elements + defense.
            AddStatsSection(builder, screen, Column(lines, 0, 0), "core", ModStrings.CharSheetCore);
            AddStatsSection(builder, screen, Column(lines, 0, 1), "elem", ModStrings.CharSheetElements);

            AddTextSection(
                builder,
                "status",
                ModStrings.CharSheetStatusEffects,
                () => GameMasterScript.heroPCActor.GetStatusEffectsTextForCharacterSheet()
            );

            // Adventure tab: column 0 is the lifetime stats; the feats list is its own text block.
            AddStatsSection(builder, screen, Column(lines, 1, 0), "adv", ModStrings.CharSheetAdventure);
            AddTextSection(
                builder,
                "feats",
                ModStrings.CharSheetFeats,
                () => GameMasterScript.heroPCActor.GetFeatsTextForCharacterSheet()
            );
        }

        private static List<UIManagerScript.UIObject> Column(
            List<List<List<UIManagerScript.UIObject>>> lines,
            int tab,
            int column
        ) {
            return tab < lines.Count && column < lines[tab].Count ? lines[tab][column] : null;
        }

        // --- Header ----------------------------------------------------------------------------

        private static void BuildHeader(IOverlayBuilder builder) {
            builder.AddLabel(
                ControlId.Structural("cs:header"),
                ctx => {
                    HeroPC hero = GameMasterScript.heroPCActor;
                    ctx.Message.Fragment(GameLabelReader.Clean(hero.displayName));
                    ctx.Message.ListItem(ModStrings.JobLevel(
                        GameLabelReader.Clean(hero.myJob.DisplayName), hero.myStats.GetLevel()));
                    ctx.Message.ListItem(ModStrings.Jp((int)hero.GetCurJP()));
                    ctx.Message.ListItem(ModStrings.Xp(hero.myStats.GetXP(), hero.myStats.GetXPToNextLevel()));
                    ctx.Message.ListItem(ModStrings.Gold(hero.GetMoney()));
                }
            );
        }

        // --- Stat sections ---------------------------------------------------------------------

        private static void AddStatsSection(
            IOverlayBuilder builder,
            Switch_UICharacterSheet screen,
            List<UIManagerScript.UIObject> column,
            string keyPart,
            string sectionLabel
        ) {
            if (column == null) {
                return;
            }

            builder.AddLabel(
                ControlId.Structural("cs:sec:" + keyPart),
                ctx => ctx.Message.Fragment(sectionLabel)
            );

            foreach (UIManagerScript.UIObject obj in column) {
                // Only the real stat lines are CharacterSheetInfoPoints; the status/feats/pet shadow
                // objects also live here and are handled separately.
                if (!(obj is CharacterSheetInfoPoint point)) {
                    continue;
                }

                ECSValue valueType = PointValueType(point);
                bool isElement = valueType >= ECSValue.element_physical && valueType <= ECSValue.element_shadow;

                builder.AddItem(
                    ControlId.Structural("cs:stat:" + valueType),
                    new NodeVtable {
                        Label = ctx => StatLabel(ctx.Message, point, isElement),
                        OnReadInfo = ctx => ReadTooltip(ctx.Message, screen, valueType),
                    }
                );
            }
        }

        // Refresh the point from live hero data (works for the inactive tab too), then read its label
        // and value. Elemental lines carry a resistance and a bonus-damage value.
        private static void StatLabel(MessageBuilder message, CharacterSheetInfoPoint point, bool isElement) {
            point.Update();

            string label = point.txt_label != null ? GameLabelReader.Clean(point.txt_label.text) : null;
            if (!string.IsNullOrEmpty(label)) {
                message.Fragment(label);
            }

            if (isElement) {
                message.ListItem(ModStrings.ElementDefense(ValueText(point.txt_value)));
                message.ListItem(ModStrings.ElementDamage(ValueText(point.txt_secondaryValue)));
            } else {
                string value = ValueText(point.txt_value);
                if (!string.IsNullOrEmpty(value)) {
                    message.Fragment(value);
                }
            }
        }

        private static string ValueText(TMPro.TextMeshProUGUI tmp) {
            return tmp != null ? GameLabelReader.Clean(tmp.text) : null;
        }

        // The game's own detailed tooltip for a stat (stat name + description + live calc). Setting it
        // writes the shared tooltip scroll, which we read back immediately.
        private static void ReadTooltip(MessageBuilder message, Switch_UICharacterSheet screen, ECSValue valueType) {
            screen.SetTooltipForCharacterSheetOption(valueType);
            string tip = screen.txt_TooltipScroll != null
                ? GameLabelReader.Clean(screen.txt_TooltipScroll.text)
                : null;
            if (!string.IsNullOrEmpty(tip)) {
                message.Fragment(tip);
            }
        }

        // --- Text sections (status effects, feats) ---------------------------------------------

        private static void AddTextSection(
            IOverlayBuilder builder,
            string keyPart,
            string sectionLabel,
            System.Func<string> text
        ) {
            builder.AddLabel(
                ControlId.Structural("cs:text:" + keyPart),
                ctx => {
                    ctx.Message.Fragment(sectionLabel);
                    string body = GameLabelReader.Clean(text());
                    ctx.Message.Fragment(string.IsNullOrEmpty(body) ? ModStrings.None : body);
                }
            );
        }
    }
}
