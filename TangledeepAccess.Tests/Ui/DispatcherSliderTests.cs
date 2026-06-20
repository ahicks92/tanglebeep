using TangledeepAccess.Controls;
using TangledeepAccess.Ui;
using TangledeepAccess.Ui.Graph;
using Xunit;

namespace TangledeepAccess.Tests.Ui {
    public class DispatcherSliderTests {
        // A value control (OnHorizontalAdjust) followed by a plain control. Left/right adjust the
        // value; up/down still navigate. The plain control has no adjust handler.
        private sealed class SliderOverlay : IUiOverlay {
            public int Value;
            public OverlayId Id => OverlayId.Inventory;

            public void Build(IOverlayBuilder builder) {
                builder.AddItem(
                    ControlId.Structural("slider"),
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment("amount " + Value),
                        OnHorizontalAdjust = (ctx, sign, large) => {
                            Value += sign * (large ? 10 : 1);
                            ctx.Message.Fragment(Value.ToString());
                        },
                    }
                );
                builder.AddItem(
                    ControlId.Structural("ok"),
                    new NodeVtable { Label = ctx => ctx.Message.Fragment("ok") }
                );
                builder.CaptureInput();
            }
        }

        [Fact]
        public void HorizontalAdjustsValueAndDoesNotMove() {
            var overlay = new SliderOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            Assert.Equal("amount 0", d.Tick().Message?.Build()); // settle on the slider

            TickResult right = d.Tick(ModInputAction.Move(1, 0));
            Assert.Equal("1", right.Message?.Build());
            Assert.Equal(1, overlay.Value);
            Assert.False(right.Moved);

            TickResult left = d.Tick(ModInputAction.Move(-1, 0));
            Assert.Equal("0", left.Message?.Build());
            Assert.Equal(0, overlay.Value);
            Assert.False(left.Moved);
        }

        [Fact]
        public void SkipToEdgeTakesLargeStep() {
            var overlay = new SliderOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            d.Tick(); // settle
            TickResult r = d.Tick(ModInputAction.MoveToEdge(1, 0));

            Assert.Equal("10", r.Message?.Build());
            Assert.Equal(10, overlay.Value);
            Assert.False(r.Moved);
        }

        [Fact]
        public void VerticalStillNavigatesPastTheSlider() {
            var overlay = new SliderOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            d.Tick(); // on the slider
            TickResult down = d.Tick(ModInputAction.Move(0, -1));

            Assert.Equal("ok", down.Message?.Build());
            Assert.True(down.Moved);
            Assert.Equal(0, overlay.Value); // navigation did not touch the value
        }

        [Fact]
        public void NodeWithoutAdjustHandlerNavigatesHorizontally() {
            var overlay = new SliderOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            d.Tick(); // slider
            d.Tick(ModInputAction.Move(0, -1)); // move down to "ok" (no adjust handler)

            // Right on a non-adjust control is a normal move; at the row edge it just re-reads.
            TickResult r = d.Tick(ModInputAction.Move(1, 0));
            Assert.Equal("ok", r.Message?.Build());
            Assert.False(r.Moved);
        }
    }
}
