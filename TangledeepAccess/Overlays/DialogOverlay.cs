using TangledeepAccess.Ui;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// In-game modal dialogue (NPC conversation, yes/no prompts during play). We own it the same
    /// way as the title narrative dialogs (<see cref="TitleDialogOverlay"/>): the dialog box
    /// unraveled into an owned vertical menu via <see cref="OwnedChoices"/>, capturing input so
    /// navigation is uniform regardless of how the game keys the dialog. The only difference from the
    /// title version is the scope — this claims non-title dialogs, driven by the in-game input
    /// chokepoint rather than the title pump.
    /// </summary>
    internal sealed class DialogOverlay : IUiOverlay, ISubIdentified {
        public OverlayId Id => OverlayId.Dialog;

        public OverlayResult Handler() {
            bool inGame = GameMasterScript.gmsSingleton == null
                || !GameMasterScript.gmsSingleton.titleScreenGMS;
            return inGame && UIManagerScript.dialogBoxOpen
                ? OverlayResult.Active(this)
                : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            OwnedChoices.Build(builder);
        }

        public string SubIdentity() {
            return OwnedChoices.SubIdentity();
        }
    }
}
