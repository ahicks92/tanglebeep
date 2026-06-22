using Tanglebeep.Focus;
using Tanglebeep.Ui;

namespace Tanglebeep.Overlays {
    /// <summary>
    /// The character-creation feat-select screen (the <c>PERKSELECT</c> dialog). The game models it
    /// as a dialog whose responses are <c>ButtonCombo</c>s laid out in a grid; we do not mirror that
    /// topology. Instead we read the dialog's response list and re-present it as one <b>owned</b>
    /// vertical menu, built identically every tick: the instruction (the dialog body) first, then
    /// every unlocked feat (name + description + whether it is selected), then the action buttons
    /// (pick-for-me, game modifiers, finished), then the locked feats last.
    ///
    /// <para>Every node is a game-backed dialog button, so we never reimplement selection: moving
    /// onto a node syncs the game's dialog cursor, and activating it confirms that button through
    /// the game — toggling a feat (the dialog stays open for the second pick), opening the modifier
    /// screen, or finishing creation, exactly as the game would. Locked feats confirm to nothing.</para>
    /// </summary>
    internal sealed class FeatSelectOverlay : IUiOverlay {
        public OverlayId Id => OverlayId.FeatSelect;

        public OverlayResult Handler() {
            bool onTitle = GameMasterScript.gmsSingleton != null
                && GameMasterScript.gmsSingleton.titleScreenGMS;
            return onTitle
                && TitleScreenScript.CreateStage == CreationStages.PERKSELECT
                && UIManagerScript.dialogBoxOpen
                ? OverlayResult.Active(this)
                : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            // The dialog body ("Select two feats…") as the first line — the game's own prompt. A
            // no-op on Enter so this header never confirms a default through the game.
            string body = DialogBody();
            if (!string.IsNullOrEmpty(body)) {
                builder.AddClickable(
                    ControlId.Structural("featselect:body"),
                    ctx => ctx.Message.Fragment(body),
                    (ctx, mods) => { }
                );
            }

            System.Collections.Generic.List<UIManagerScript.UIObject> objs =
                UIManagerScript.dialogUIObjects;
            if (objs != null) {
                // Three passes over the responses so the list reads the same every tick:
                // unlocked feats, then the action buttons, then locked feats last.
                AddButtons(builder, objs, Category.Feat);
                AddButtons(builder, objs, Category.Action);
                AddButtons(builder, objs, Category.Locked);
            }

            builder.CaptureInput();
        }

        private enum Category { Feat, Action, Locked }

        private static void AddButtons(
            IOverlayBuilder builder,
            System.Collections.Generic.List<UIManagerScript.UIObject> objs,
            Category category
        ) {
            for (int i = 0; i < objs.Count; i++) {
                UIManagerScript.UIObject uo = objs[i];
                ButtonCombo button = uo != null ? uo.button : null;
                if (button == null || Classify(button) != category) {
                    continue;
                }

                bool locked = category == Category.Locked;
                ButtonCombo captured = button;
                // Game-backed: no mod OnClick, so Enter pass-through confirms the dialog button.
                // The structural key is stable across dialog rebuilds (skill ref / response kind),
                // while the live UIObject reference drives focus sync and the confirm.
                builder.AddLabel(
                    ControlId.Referenced(uo, "featselect:" + Key(button, i)),
                    ctx => {
                        string label = ButtonLabel(captured, locked);
                        if (!string.IsNullOrEmpty(label)) {
                            ctx.Message.Fragment(label);
                        }
                    }
                );
            }
        }

        private static Category Classify(ButtonCombo b) {
            if (b.dbr == DialogButtonResponse.NOTHING) {
                return Category.Locked; // "?????" — present but unselectable
            }

            if (b.dbr == DialogButtonResponse.TOGGLE && b.actionRef != "randomfeats") {
                return Category.Feat;
            }

            return Category.Action; // pick-for-me (randomfeats), game modifiers, finished
        }

        private static string Key(ButtonCombo b, int index) {
            if (Classify(b) == Category.Feat) {
                return "feat:" + b.actionRef;
            }

            if (b.dbr == DialogButtonResponse.NOTHING) {
                return "locked:" + index;
            }

            return "action:" + b.dbr + ":" + b.actionRef;
        }

        /// <summary>
        /// A locked feat reads only its locked line (its name is "?????" — noise for speech). Any
        /// other button reads name + description, prefixed with "selected" when toggled on.
        /// </summary>
        private static string ButtonLabel(ButtonCombo button, bool locked) {
            string desc = GameLabelReader.Clean(button.buttonText);
            if (locked) {
                return desc;
            }

            string name = GameLabelReader.Clean(button.headerText);
            string text = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(desc)
                ? name + ". " + desc
                : name ?? desc;
            return button.toggled ? "selected, " + text : text;
        }

        private static string DialogBody() {
            DialogBoxScript dbs = UIManagerScript.myDialogBoxComponent;
            TMPro.TextMeshProUGUI text = dbs != null ? dbs.GetDialogText() : null;
            return text != null ? GameLabelReader.Clean(text.text) : null;
        }
    }
}
