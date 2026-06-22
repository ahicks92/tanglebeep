using Tanglebeep.Ui;

namespace Tanglebeep.Overlays {
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

        /// <summary>
        /// Active when there is a live focused UI element no overlay above claimed AND the game is
        /// actually presenting an interactive UI. Reads the focus watcher's published
        /// <see cref="Controls.FocusWatcher.CurrentFocus"/> — the single, edge-detected, validated
        /// focus — never the raw <c>uiObjectFocus</c> (the game leaves that dangling on a closed
        /// dialog's control).
        ///
        /// <para>The extra <see cref="UIManagerScript.AnyInteractableWindowOpen"/> gate matters
        /// because <c>activeInHierarchy</c> alone is not enough: some dialogs tear down
        /// non-atomically — the banker's, for one, flips <c>dialogBoxOpen</c> false but leaves its
        /// focused button active in the hierarchy for ~9 more frames. During that window the focus
        /// reads "live" though the dialog is gone, which latched this fallback on for a tick
        /// ("Unsupported menu" on close). AnyInteractableWindowOpen flips false in lockstep with
        /// <c>dialogBoxOpen</c>, so it correctly reports "no UI open" across that lingering window.
        /// (When a real dialog/window is open, the bespoke overlay above us claims it first, so this
        /// gate never suppresses a screen we actually support.)</para>
        /// </summary>
        public OverlayResult Handler() {
            return Controls.FocusWatcher.CurrentFocus != null && UIManagerScript.AnyInteractableWindowOpen()
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
