using System;
using Tanglebeep.Speech;
using Tanglebeep.Ui;
using Tanglebeep.Ui.Graph;

namespace Tanglebeep.Overlays {
    /// <summary>
    /// A generic quantity-prompt <b>auxiliary</b> overlay: a mod-owned slider ("how many? N") plus a
    /// cancel control, anchored to the parent node that opened it (via
    /// <see cref="IOverlayController.OpenAuxiliary"/>). It replaces the game's own quantity-slider
    /// dialog for shop stacks, so we never hand off to <see cref="DialogOverlay"/> and never lose the
    /// shop overlay's list position.
    ///
    /// <para>It carries <b>no</b> commit closure: confirming reports the chosen quantity via
    /// <see cref="IOverlayController.CommitAuxiliary"/>, and the framework hands that scalar to the
    /// parent anchor node's <see cref="NodeVtable.OnAuxCommit"/>, which runs the transaction against
    /// the parent's <i>live</i> state on its next rebuild. Cancel just closes (the parent refocuses
    /// the anchor). The transaction's own game-log line carries the spoken result, so closing is
    /// silent.</para>
    ///
    /// <para>All <see cref="OverlayId.Auxiliary"/> auxiliaries share one focus-cache slot (only one is
    /// ever live), so this is a parameterized instance pushed into the dispatcher, not a registered
    /// handler.</para>
    /// </summary>
    internal sealed class ShopQuantityOverlay : IUiOverlay {
        private readonly int _max;
        private readonly string _name;
        private int _qty;

        public ShopQuantityOverlay(int max, string name) {
            _max = max < 1 ? 1 : max;
            _name = name;
            // Start at 1 (conservative): a reflexive confirm does the minimal thing, not "all". The
            // game's own slider is sticky/undefined here, which is fine sighted but bad blind.
            _qty = 1;
        }

        public OverlayId Id => OverlayId.Auxiliary;

        public void Build(IOverlayBuilder builder) {
            builder.CaptureInput();

            // One node: the slider. Enter commits the chosen quantity; Escape cancels (handled by the
            // dispatcher's aux path, which returns to the parent). No cancel node is needed.
            builder.AddItem(
                ControlId.Structural("aux:qty"),
                new NodeVtable {
                    Label = ctx => {
                        ctx.Message.Fragment(ModStrings.HowMany(_qty));
                        ctx.Message.Fragment(_name);
                    },
                    OnHorizontalAdjust = Adjust,
                    OnClick = (ctx, mods) => ctx.Controller.CommitAuxiliary(_qty),
                }
            );
        }

        // Mirror the dialog slider feel: a small step is +/-1, a large (Shift) step +/-10. Clamp to
        // 1..max and speak just the new number, for rapid adjustment.
        private void Adjust(OverlayCtx ctx, int sign, bool large) {
            _qty = Math.Max(1, Math.Min(_max, _qty + sign * (large ? 10 : 1)));
            ctx.Message.Fragment(_qty.ToString());
        }
    }
}
