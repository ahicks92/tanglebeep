using Tanglebeep.Controls;
using Tanglebeep.Ui;
using Tanglebeep.Ui.Graph;
using Xunit;

namespace Tanglebeep.Tests.Ui {
    public class DispatcherAuxTests {
        // A mod-owned quantity slider as an auxiliary overlay: left/right adjust, confirm commits the
        // value to the anchor, the cancel node closes with no commit. Carries no closure of its own.
        private sealed class AuxOverlay : IUiOverlay {
            public int Value = 1;
            public OverlayId Id => OverlayId.Auxiliary;

            public void Build(IOverlayBuilder builder) {
                builder.CaptureInput();
                builder.AddItem(
                    ControlId.Structural("aux:slider"),
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment("how many " + Value),
                        OnHorizontalAdjust = (ctx, sign, large) => {
                            Value += sign;
                            ctx.Message.Fragment(Value.ToString());
                        },
                        OnClick = (ctx, mods) => ctx.Controller.CommitAuxiliary(Value),
                    }
                );
                builder.AddItem(
                    ControlId.Structural("aux:cancel"),
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment("cancel"),
                        OnClick = (ctx, mods) => ctx.Controller.Close(),
                    }
                );
            }
        }

        // The main overlay: an item whose confirm opens the aux, and whose OnAuxCommit records the
        // committed scalar; plus a second control to prove silent resume on the anchor. The item can
        // be made to vanish to exercise the orphaned-aux path.
        private sealed class MainOverlay : IUiOverlay {
            public AuxOverlay Aux;
            public int Committed = -1;
            public bool ItemPresent = true;
            public OverlayId Id => OverlayId.Inventory;

            public void Build(IOverlayBuilder builder) {
                builder.CaptureInput();
                if (ItemPresent) {
                    builder.AddItem(
                        ControlId.Structural("main:item"),
                        new NodeVtable {
                            Label = ctx => ctx.Message.Fragment("item"),
                            OnClick = (ctx, mods) => ctx.Controller.OpenAuxiliary(Aux),
                            OnAuxCommit = ctx => Committed = ctx.Arg,
                        }
                    );
                }

                builder.AddItem(
                    ControlId.Structural("main:other"),
                    new NodeVtable { Label = ctx => ctx.Message.Fragment("other") }
                );
            }
        }

        private static OverlayDispatcher Wire(MainOverlay main) {
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(main));
            return d;
        }

        [Fact]
        public void CommitDeliversResultToAnchorAndResumesSilently() {
            var aux = new AuxOverlay();
            var main = new MainOverlay { Aux = aux };
            OverlayDispatcher d = Wire(main);

            Assert.Equal("item", d.Tick().Message?.Build()); // settle on the item

            d.Tick(ModInputAction.Of(ModInputKind.Confirm)); // opens the aux
            Assert.Equal("how many 1", d.Tick().Message?.Build()); // aux announces itself

            Assert.Equal("2", d.Tick(ModInputAction.Move(1, 0)).Message?.Build()); // adjust up
            Assert.Equal(2, aux.Value);

            d.Tick(ModInputAction.Of(ModInputKind.Confirm)); // commit the quantity
            Assert.Equal(2, main.Committed); // delivered to the anchor's OnAuxCommit

            Assert.Null(d.Tick().Message?.Build()); // main resumed on the anchor, silently
        }

        [Fact]
        public void CancelClosesWithoutCommitting() {
            var aux = new AuxOverlay();
            var main = new MainOverlay { Aux = aux };
            OverlayDispatcher d = Wire(main);

            Assert.Equal("item", d.Tick().Message?.Build());
            d.Tick(ModInputAction.Of(ModInputKind.Confirm)); // open
            Assert.Equal("how many 1", d.Tick().Message?.Build());

            Assert.Equal("cancel", d.Tick(ModInputAction.Move(0, -1)).Message?.Build()); // to cancel
            d.Tick(ModInputAction.Of(ModInputKind.Confirm)); // cancel-close

            Assert.Equal(-1, main.Committed); // no commit fired
            Assert.Null(d.Tick().Message?.Build()); // resumed on the anchor, silently
        }

        [Fact]
        public void CancelInputClosesTheAuxWithoutCommitting() {
            var aux = new AuxOverlay();
            var main = new MainOverlay { Aux = aux };
            OverlayDispatcher d = Wire(main);

            Assert.Equal("item", d.Tick().Message?.Build());
            d.Tick(ModInputAction.Of(ModInputKind.Confirm)); // open
            Assert.Equal("how many 1", d.Tick().Message?.Build());
            Assert.True(d.AuxActive);

            d.Tick(ModInputAction.Of(ModInputKind.Cancel)); // Escape
            Assert.False(d.AuxActive);
            Assert.Equal(-1, main.Committed); // no commit fired
            Assert.Null(d.Tick().Message?.Build()); // resumed on the anchor, silently
        }

        [Fact]
        public void VanishedAnchorAutoClosesTheAux() {
            var aux = new AuxOverlay();
            var main = new MainOverlay { Aux = aux };
            OverlayDispatcher d = Wire(main);

            Assert.Equal("item", d.Tick().Message?.Build());
            d.Tick(ModInputAction.Of(ModInputKind.Confirm)); // open
            Assert.Equal("how many 1", d.Tick().Message?.Build()); // aux active

            main.ItemPresent = false; // the anchor node disappears from the main overlay
            // The aux is orphaned: it tears down and the main resumes (focus falls to the survivor).
            Assert.Equal("other", d.Tick().Message?.Build());
            Assert.Equal(-1, main.Committed);
        }
    }
}
