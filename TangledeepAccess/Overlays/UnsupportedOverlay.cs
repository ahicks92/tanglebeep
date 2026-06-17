using TangledeepAccess.Ui;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// The bottom-of-stack fallback for any legacy <c>uiObjectFocus</c> screen we have not written
    /// a bespoke overlay for. It deliberately does NOT mirror the game's focus graph — that rarely
    /// produced a usable reading and was the only thing in the framework that followed game focus
    /// and captured input per-tick. Instead it is a single owned node that announces the screen is
    /// unsupported and captures input like every other owned overlay, so navigation keys are
    /// swallowed deterministically while non-navigation keys (Escape, hotkeys) still pass through
    /// to the game (so the player can at least back out). Screens worth supporting get their own
    /// overlay, registered above this one.
    /// </summary>
    internal sealed class UnsupportedOverlay : IUiOverlay {
        public OverlayId Id => OverlayId.Unsupported;

        /// <summary>Active whenever the game reports a focused UI element no overlay above claimed.</summary>
        public OverlayResult Handler() {
            return UIManagerScript.uiObjectFocus != null
                ? OverlayResult.Active(this)
                : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            // One owned node: no graph to mirror, just an honest "not supported", and a no-op on
            // Enter so we never confirm a default on a screen we do not understand.
            builder.AddClickable(
                ControlId.Structural("unsupported"),
                ctx => ctx.Message.Fragment("Unsupported menu"),
                (ctx, mods) => { }
            );
            builder.CaptureInput();
        }
    }
}
