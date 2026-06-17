using TangledeepAccess.Focus;
using TangledeepAccess.Ui;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// Speaks the game's modal dialog box: NPC dialogue, the new-game narrative intros,
    /// yes/no prompts, and any other text-plus-choices popup. Two halves:
    ///
    /// <para><b>Body</b> — the message text (<c>txtDialogBoxMessage</c>) appears without a
    /// focus move, so it rides the framework's one-shot <see cref="IOverlayBuilder.Announce"/>
    /// channel, keyed by the text itself: it is spoken once when the dialog opens (and again
    /// only if the text changes, e.g. a multi-page conversation), prepended to the focused
    /// choice. The typewriter reveal only limits <c>maxVisibleCharacters</c>; the TMP
    /// <c>.text</c> already holds the full string, so we read the whole message immediately.</para>
    ///
    /// <para><b>Choices</b> — the buttons are legacy <c>UIObject</c>s wired into the standard
    /// neighbor graph (<c>dialogUIObjects</c>, focus on <c>uiObjectFocus</c>), so we mirror them
    /// with <see cref="GameMenuMirror"/> exactly like the generic fallback. A single-Continue
    /// dialog is one node (input passes through to the game's own confirm); a multi-choice
    /// prompt is several nodes (we drive navigation, Enter passes the confirm through).</para>
    ///
    /// Registered above the per-screen overlays because a dialog is modal — when one is open it
    /// owns the screen, including over character creation (whose intros are themselves dialogs).
    /// </summary>
    internal sealed class DialogOverlay : IUiOverlay {
        public OverlayId Id => OverlayId.Dialog;

        public OverlayResult Handler() {
            return UIManagerScript.dialogBoxOpen ? OverlayResult.Active(this) : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            string body = ReadBody();
            if (body != null) {
                // Key by the text: a new/changed message re-announces; the same message,
                // re-rendered every tick, announces only once.
                builder.Announce(body, ctx => ctx.Message.Fragment(body));
            }

            // The choices use the normal focus model; mirror them like the generic fallback.
            // If the dialog has no focused button yet, the announcement still needs a node to
            // ride along, so fall back to a single body node.
            if (UIManagerScript.uiObjectFocus != null) {
                GameMenuMirror.Build(builder, GameLabelReader.ReadLabel);
            } else if (body != null) {
                // Silent placeholder: the announcement already speaks the body; this node only
                // exists so the announcement (which needs at least one node) can ride along.
                builder.AddLabel(ControlId.Structural("dialogbody"), ctx => { });
            }
        }

        /// <summary>The dialog's full message text, color-stripped, or null if unavailable.</summary>
        private static string ReadBody() {
            DialogBoxScript dbs = UIManagerScript.myDialogBoxComponent;
            TMPro.TextMeshProUGUI text = dbs != null ? dbs.GetDialogText() : null;
            return text != null ? GameLabelReader.Clean(text.text) : null;
        }
    }
}
