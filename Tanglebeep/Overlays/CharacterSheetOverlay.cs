using System.Collections.Generic;
using HarmonyLib;
using Tanglebeep.Focus;
using Tanglebeep.Speech;
using Tanglebeep.Ui;
using Tanglebeep.Ui.Graph;
using ECSValue = Switch_UICharacterSheet.ECharacterSheetValueType;

namespace Tanglebeep.Overlays {
    /// <summary>
    /// The character sheet (<c>Switch_UICharacterSheet</c>, the "C" tab) — a read-only stats viewer.
    /// The game splits it into a CORE STATS tab and an ADVENTURE tab; like the equipment sheet, we
    /// ignore that topology and surface everything at once. Rather than one tall flat list, we lay it
    /// out as an owned grid: each logical section is one <b>row</b> whose leftmost cell is the section
    /// <b>header</b> and whose remaining cells are that section's <b>values</b>. So up/down moves
    /// between sections (always landing on the header) and left/right walks a section's values.
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

            BuildHeroRow(builder);

            // Core tab: column 0 is attributes/offense, column 1 is elements + defense.
            AddStatsSection(builder, screen, Column(lines, 0, 0), "core", ModStrings.CharSheetCore);
            AddStatsSection(builder, screen, Column(lines, 0, 1), "elem", ModStrings.CharSheetElements);

            AddTextSection(
                builder,
                "status",
                ModStrings.CharSheetStatusEffects,
                () => GameMasterScript.heroPCActor.GetStatusEffectsTextForCharacterSheet(),
                StringManager.GetString("ui_current_statuses")
            );

            // Adventure tab: column 0 is the lifetime stats; the feats list is its own text block.
            AddStatsSection(builder, screen, Column(lines, 1, 0), "adv", ModStrings.CharSheetAdventure);
            AddTextSection(
                builder,
                "feats",
                ModStrings.CharSheetFeats,
                () => GameMasterScript.heroPCActor.GetFeatsTextForCharacterSheet(),
                StringManager.GetString("misc_feats_plural")
            );
        }

        private static List<UIManagerScript.UIObject> Column(
            List<List<List<UIManagerScript.UIObject>>> lines,
            int tab,
            int column
        ) {
            return tab < lines.Count && column < lines[tab].Count ? lines[tab][column] : null;
        }

        // --- Hero row --------------------------------------------------------------------------

        // The identity row: name is the header cell, then job/level, JP, XP, and gold as value cells.
        private static void BuildHeroRow(IOverlayBuilder builder) {
            builder.StartRow("hero");

            builder.AddLabel(
                ControlId.Structural("cs:hero:name"),
                ctx => ctx.Message.Fragment(GameLabelReader.Clean(GameMasterScript.heroPCActor.displayName))
            );
            builder.AddLabel(
                ControlId.Structural("cs:hero:joblevel"),
                ctx => {
                    HeroPC hero = GameMasterScript.heroPCActor;
                    ctx.Message.Fragment(ModStrings.JobLevel(
                        GameLabelReader.Clean(hero.myJob.DisplayName), hero.myStats.GetLevel()));
                }
            );
            builder.AddLabel(
                ControlId.Structural("cs:hero:jp"),
                ctx => ctx.Message.Fragment(ModStrings.Jp((int)GameMasterScript.heroPCActor.GetCurJP()))
            );
            builder.AddLabel(
                ControlId.Structural("cs:hero:xp"),
                ctx => {
                    HeroPC hero = GameMasterScript.heroPCActor;
                    ctx.Message.Fragment(ModStrings.Xp(hero.myStats.GetXP(), hero.myStats.GetXPToNextLevel()));
                }
            );
            builder.AddLabel(
                ControlId.Structural("cs:hero:gold"),
                ctx => ctx.Message.Fragment(ModStrings.Gold(GameMasterScript.heroPCActor.GetMoney()))
            );

            builder.EndRow();
        }

        // --- Stat sections ---------------------------------------------------------------------

        // One row per stat section: the section label is the header (leftmost) cell, then one value
        // cell per stat line.
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

            builder.StartRow(keyPart);

            builder.AddLabel(
                ControlId.Structural("cs:hdr:" + keyPart),
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

            builder.EndRow();
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

        // One row per text section: the section label is the header (leftmost) cell, then one value
        // cell per line of the game's own text (so each status/feat is its own navigable value). An
        // empty section gets a single "none" value cell so the row is never empty.
        // <paramref name="dropLeadingHeader"/> is the game's own redundant section header (e.g.
        // "Current Status Effects:") that leads its text block; we drop it because our row already
        // carries that header in its leftmost cell.
        private static void AddTextSection(
            IOverlayBuilder builder,
            string keyPart,
            string sectionLabel,
            System.Func<string> text,
            string dropLeadingHeader
        ) {
            builder.StartRow(keyPart);

            builder.AddLabel(
                ControlId.Structural("cs:hdr:" + keyPart),
                ctx => ctx.Message.Fragment(sectionLabel)
            );

            string[] values = TextLines(text(), dropLeadingHeader);
            if (values.Length == 0) {
                builder.AddLabel(
                    ControlId.Structural("cs:" + keyPart + ":none"),
                    ctx => ctx.Message.Fragment(ModStrings.None)
                );
            } else {
                for (int i = 0; i < values.Length; i++) {
                    string value = values[i];
                    builder.AddLabel(
                        ControlId.Structural("cs:" + keyPart + ":" + i),
                        ctx => ctx.Message.Fragment(value)
                    );
                }
            }

            builder.EndRow();
        }

        // Split a game text block into one cleaned line per value, dropping markup-only / blank lines.
        // If the first non-empty line is the game's own redundant section header, drop it too.
        private static string[] TextLines(string raw, string dropLeadingHeader) {
            if (string.IsNullOrEmpty(raw)) {
                return new string[0];
            }

            var list = new List<string>();
            bool first = true;
            foreach (string piece in raw.Split('\n')) {
                string clean = GameLabelReader.Clean(piece);
                if (string.IsNullOrEmpty(clean)) {
                    continue;
                }

                if (first) {
                    first = false;
                    if (HeaderMatches(clean, dropLeadingHeader)) {
                        continue;
                    }
                }

                list.Add(clean);
            }

            return list.ToArray();
        }

        // True if a text line is the game's own section header, ignoring a trailing colon and case
        // (the game wraps these in markup and appends ":" inconsistently across the two builders).
        private static bool HeaderMatches(string line, string header) {
            return !string.IsNullOrEmpty(header) && Normalize(line) == Normalize(header);
        }

        private static string Normalize(string s) {
            return GameLabelReader.Clean(s)?.Trim().TrimEnd(':').Trim().ToLowerInvariant();
        }
    }
}
