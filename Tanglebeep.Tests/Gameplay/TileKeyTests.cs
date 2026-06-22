using Tanglebeep.Gameplay;
using Tanglebeep.Speech;
using Xunit;

namespace Tanglebeep.Tests.Gameplay {
    public class TileKeyTests {
        private static readonly TileShape Open = new TileShape(TileShapeKind.Open, Direction.None, 0);
        private static readonly TileShape Alcove = new TileShape(TileShapeKind.Alcove, Direction.North, 1);

        private static string Changes(TileKey key, TileKey? previous) {
            var b = new MessageBuilder();
            key.AppendChanges(b, previous);
            return b.Build();
        }

        [Fact]
        public void NoPreviousReadsBothFields() {
            Assert.Equal("ground north alcove", Changes(new TileKey("ground", Alcove), null));
        }

        [Fact]
        public void OpenShapeReadsOnlyTerrain() {
            // Open's Speak() is null, so it contributes no word.
            Assert.Equal("ground", Changes(new TileKey("ground", Open), null));
        }

        [Fact]
        public void SpeaksOnlyTheChangedShape() {
            var prev = new TileKey("ground", Open);
            Assert.Equal("north alcove", Changes(new TileKey("ground", Alcove), prev));
        }

        [Fact]
        public void SpeaksOnlyTheChangedTerrain() {
            var prev = new TileKey("ground", Alcove);
            Assert.Equal("water", Changes(new TileKey("water", Alcove), prev));
        }

        [Fact]
        public void SpeaksNothingWhenUnchanged() {
            var prev = new TileKey("ground", Alcove);
            Assert.Null(Changes(new TileKey("ground", Alcove), prev));
        }

        [Fact]
        public void FullReadMarksBlurredButNotClear() {
            Assert.Equal("blurred ground", Changes(new TileKey("ground", Open, blurred: true), null));
            Assert.Equal("ground", Changes(new TileKey("ground", Open, blurred: false), null));
        }

        [Fact]
        public void AnnouncesBlurredBoundaryOnceInEitherDirection() {
            var clear = new TileKey("ground", Open, blurred: false);
            var blur = new TileKey("ground", Open, blurred: true);
            Assert.Equal("blurred", Changes(blur, clear));   // entering blur
            Assert.Equal("clear", Changes(clear, blur));     // leaving blur
            Assert.Null(Changes(blur, blur));                // unchanged: nothing
        }

        [Fact]
        public void BlurredChangeLeadsTerrainAndShapeChanges() {
            var prev = new TileKey("ground", Open, blurred: false);
            Assert.Equal("blurred water north alcove", Changes(new TileKey("water", Alcove, blurred: true), prev));
        }
    }
}
