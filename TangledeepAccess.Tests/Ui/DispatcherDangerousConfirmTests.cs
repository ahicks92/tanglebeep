using TangledeepAccess.Controls;
using TangledeepAccess.Ui;
using TangledeepAccess.Ui.Graph;
using Xunit;

namespace TangledeepAccess.Tests.Ui {
    public class DispatcherDangerousConfirmTests {
        // One clickable node that records the Control modifier it was activated with.
        private sealed class ClickOverlay : IUiOverlay {
            public bool? LastControl;
            public OverlayId Id => OverlayId.Inventory;

            public void Build(IOverlayBuilder builder) {
                builder.AddItem(
                    ControlId.Structural("btn"),
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment("button"),
                        OnClick = (ctx, mods) => {
                            LastControl = mods.Control;
                            ctx.Message.Fragment(mods.Control ? "dangerous" : "plain");
                        },
                    }
                );
                builder.CaptureInput();
            }
        }

        [Fact]
        public void PlainConfirmHasNoControlModifier() {
            var overlay = new ClickOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            d.Tick(); // settle on the button

            TickResult r = d.Tick(ModInputAction.Of(ModInputKind.Confirm));
            Assert.Equal("plain", r.Message?.Build());
            Assert.Equal(false, overlay.LastControl);
        }

        [Fact]
        public void DangerousConfirmSetsControlModifier() {
            var overlay = new ClickOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            d.Tick(); // settle

            TickResult r = d.Tick(ModInputAction.Of(ModInputKind.DangerousConfirm));
            Assert.Equal("dangerous", r.Message?.Build());
            Assert.Equal(true, overlay.LastControl);
        }
    }
}
