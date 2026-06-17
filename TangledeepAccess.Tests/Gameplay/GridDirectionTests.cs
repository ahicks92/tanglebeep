using TangledeepAccess.Gameplay;
using Xunit;

namespace TangledeepAccess.Tests.Gameplay {
    public class GridDirectionTests {
        [Fact]
        public void OffsetZeroIsHere() {
            Assert.Equal("here", GridDirection.Offset(0, 0));
        }

        [Fact]
        public void OffsetCardinalsUseGameConvention() {
            // +y north, +x east.
            Assert.Equal("3 north", GridDirection.Offset(0, 3));
            Assert.Equal("2 south", GridDirection.Offset(0, -2));
            Assert.Equal("5 east", GridDirection.Offset(5, 0));
            Assert.Equal("4 west", GridDirection.Offset(-4, 0));
        }

        [Fact]
        public void OffsetCombinesNorthSouthBeforeEastWest() {
            Assert.Equal("2 north, 3 east", GridDirection.Offset(3, 2));
            Assert.Equal("1 south, 4 west", GridDirection.Offset(-4, -1));
        }

        [Fact]
        public void CompassEightWay() {
            Assert.Equal("north", GridDirection.Compass(0, 1));
            Assert.Equal("south", GridDirection.Compass(0, -1));
            Assert.Equal("east", GridDirection.Compass(1, 0));
            Assert.Equal("west", GridDirection.Compass(-1, 0));
            Assert.Equal("northeast", GridDirection.Compass(1, 1));
            Assert.Equal("northwest", GridDirection.Compass(-1, 1));
            Assert.Equal("southeast", GridDirection.Compass(1, -1));
            Assert.Equal("southwest", GridDirection.Compass(-1, -1));
            Assert.Equal("here", GridDirection.Compass(0, 0));
        }

        [Fact]
        public void StepsIsChebyshev() {
            Assert.Equal(3, GridDirection.Steps(3, 2));
            Assert.Equal(4, GridDirection.Steps(-1, -4));
            Assert.Equal(0, GridDirection.Steps(0, 0));
        }
    }
}
