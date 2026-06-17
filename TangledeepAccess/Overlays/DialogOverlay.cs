using TangledeepAccess.Focus;
using TangledeepAccess.Ui;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// In-game modal dialogue (NPC conversation, yes/no prompts during play). We own it the same
    /// way as the title narrative dialogs (<see cref="TitleDialogOverlay"/>): an unraveled vertical
    /// menu of the body (a fake control) plus one node per choice button, via
    /// <see cref="OwnedChoices"/>, capturing input so navigation is uniform regardless of how the
    /// game keys the dialog. The only difference from the title version is the scope — this claims
    /// non-title dialogs, driven by the in-game input chokepoint rather than the title pump.
    /// </summary>
    internal sealed class DialogOverlay : IUiOverlay {
        public OverlayId Id => OverlayId.Dialog;

        public OverlayResult Handler() {
            bool inGame = GameMasterScript.gmsSingleton == null
                || !GameMasterScript.gmsSingleton.titleScreenGMS;
            return inGame && UIManagerScript.dialogBoxOpen
                ? OverlayResult.Active(this)
                : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            OwnedChoices.Build(builder, ReadBody());
        }

        private static string ReadBody() {
            DialogBoxScript dbs = UIManagerScript.myDialogBoxComponent;
            TMPro.TextMeshProUGUI text = dbs != null ? dbs.GetDialogText() : null;
            return text != null ? GameLabelReader.Clean(text.text) : null;
        }
    }
}
