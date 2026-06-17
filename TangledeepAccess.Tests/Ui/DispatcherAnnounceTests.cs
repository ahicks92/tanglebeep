using System.Collections.Generic;
using TangledeepAccess.Ui;
using Xunit;

namespace TangledeepAccess.Tests.Ui {
    public class DispatcherAnnounceTests {
        // An overlay with one focusable node plus a one-shot announcement, both adjustable
        // between ticks to exercise the announce-vs-focus interaction.
        private sealed class AnnounceOverlay : IUiOverlay {
            public OverlayId Id => OverlayId.Dialog;
            public object NodeRef = new object();
            public string NodeLabel = "node";
            public object AnnounceKey;
            public string AnnounceText;

            public void Build(IOverlayBuilder builder) {
                if (AnnounceText != null) {
                    string t = AnnounceText;
                    builder.Announce(AnnounceKey, ctx => ctx.Message.Fragment(t));
                }

                string n = NodeLabel;
                builder.AddLabel(ControlId.ForObject(NodeRef), ctx => ctx.Message.Fragment(n));
            }
        }

        [Fact]
        public void AnnouncementPrependsFocusLabelOnce() {
            var o = new AnnounceOverlay { AnnounceKey = "body1", AnnounceText = "the body" };
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(o));

            // First tick: announcement + focus label, joined.
            Assert.Equal("the body node", d.Tick().Speak);
            // Same key, focus unchanged: silent (announcement is one-shot, focus deduped).
            Assert.Null(d.Tick().Speak);
        }

        [Fact]
        public void AnnouncementRepeatsWhenKeyChanges() {
            var o = new AnnounceOverlay { AnnounceKey = "p1", AnnounceText = "page one" };
            var d = new OverlayDispatcher();
            d.Register(() => OverlayResult.Active(o));

            Assert.Equal("page one node", d.Tick().Speak);

            // The text changes (a new dialog page) but focus stays put: announce again, alone.
            o.AnnounceKey = "p2";
            o.AnnounceText = "page two";
            Assert.Equal("page two", d.Tick().Speak);
        }

        [Fact]
        public void AnnouncementResetsAfterOverlayInactive() {
            var o = new AnnounceOverlay { AnnounceKey = "body", AnnounceText = "hello" };
            bool active = true;
            var d = new OverlayDispatcher();
            d.Register(() => active ? OverlayResult.Active(o) : OverlayResult.Inactive);

            Assert.Equal("hello node", d.Tick().Speak);
            active = false;
            Assert.Null(d.Tick().Speak); // cache + announce key cleared

            // Reopening with the same key announces again (it is a fresh appearance).
            active = true;
            Assert.Equal("hello node", d.Tick().Speak);
        }
    }
}
