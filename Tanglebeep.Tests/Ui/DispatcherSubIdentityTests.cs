using System.Collections.Generic;
using Tanglebeep.Controls;
using Tanglebeep.Ui;
using Tanglebeep.Ui.Graph;
using Xunit;

namespace Tanglebeep.Tests.Ui {
    public class DispatcherSubIdentityTests {
        // A capturing overlay whose content (and reported generation) can change in place, like a
        // dialog advancing through conversation branches.
        private sealed class SubOverlay : IUiOverlay, ISubIdentified {
            public string Gen = "g1";
            public readonly List<string> Items = new() { "a", "b", "c" };
            public OverlayId Id => OverlayId.Dialog;

            public void Build(IOverlayBuilder builder) {
                foreach (string name in Items) {
                    string n = name;
                    builder.AddItem(
                        ControlId.Structural(n),
                        new NodeVtable { Label = ctx => ctx.Message.Fragment(n) }
                    );
                }
                builder.CaptureInput();
            }

            public string SubIdentity() => Gen;
        }

        [Fact]
        public void GenerationChangeResetsFocusToStartAndReannounces() {
            var overlay = new SubOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            Assert.Equal("a", d.Tick().Message?.Build()); // open: start node
            Assert.Equal("b", d.Tick(ModInputAction.Move(0, -1)).Message?.Build()); // move off start

            overlay.Gen = "g2"; // content swapped in place
            Assert.Equal("a", d.Tick().Message?.Build()); // re-announced from the start

            Assert.Null(d.Tick().Message?.Build()); // stable generation => no repeat
        }

        [Fact]
        public void GenerationChangeIgnoresASameFrameNavCommand() {
            var overlay = new SubOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            d.Tick(); // settle on "a"

            overlay.Gen = "g2";
            // A nav command arriving the same frame the generation changed is dropped; the new
            // content wins and we re-read from the start rather than moving.
            TickResult r = d.Tick(ModInputAction.Move(0, -1));
            Assert.Equal("a", r.Message?.Build());
            Assert.False(r.Moved);
        }

        [Fact]
        public void StableGenerationNavigatesNormally() {
            var overlay = new SubOverlay();
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(overlay));

            d.Tick(); // "a"
            Assert.Equal("b", d.Tick(ModInputAction.Move(0, -1)).Message?.Build());
            Assert.Equal("c", d.Tick(ModInputAction.Move(0, -1)).Message?.Build());
        }
    }
}
