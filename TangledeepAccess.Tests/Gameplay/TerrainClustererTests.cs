using System.Collections.Generic;
using System.Linq;
using TangledeepAccess.Gameplay;
using Xunit;

namespace TangledeepAccess.Tests.Gameplay {
    public class TerrainClustererTests {
        private const int Water = 0;
        private const int Mud = 1;

        private static List<TerrainCluster> Cluster(params TerrainCell[] cells) {
            return TerrainClusterer.Cluster(cells);
        }

        private static TerrainCell C(int x, int y, int kind = Water) => new TerrainCell(x, y, kind);

        [Fact]
        public void EmptyInputYieldsNoClusters() {
            Assert.Empty(Cluster());
        }

        [Fact]
        public void SingleCellIsItsOwnCluster() {
            var clusters = Cluster(C(3, 4));
            TerrainCluster only = Assert.Single(clusters);
            Assert.Equal(1, only.CellCount);
            Assert.Equal(1, only.Width);
            Assert.Equal(1, only.Height);
            Assert.Equal(1.0, only.FillFraction);
        }

        [Fact]
        public void OrthogonallyAdjacentSameKindMerge() {
            var clusters = Cluster(C(0, 0), C(1, 0), C(2, 0));
            TerrainCluster only = Assert.Single(clusters);
            Assert.Equal(3, only.CellCount);
            Assert.Equal(3, only.Width);
            Assert.Equal(1, only.Height);
        }

        [Fact]
        public void DiagonalNeighborsMerge() {
            // 8-connected: a diagonal touch is one cluster, not two.
            var clusters = Cluster(C(0, 0), C(1, 1));
            Assert.Single(clusters);
        }

        [Fact]
        public void DifferentKindsDoNotMergeEvenWhenAdjacent() {
            var clusters = Cluster(C(0, 0, Water), C(1, 0, Mud));
            Assert.Equal(2, clusters.Count);
            Assert.Contains(clusters, c => c.Kind == Water);
            Assert.Contains(clusters, c => c.Kind == Mud);
        }

        [Fact]
        public void DisconnectedSameKindStayApart() {
            // Two water blobs with a gap wider than one tile.
            var clusters = Cluster(C(0, 0), C(1, 0), C(5, 0), C(6, 0));
            Assert.Equal(2, clusters.Count);
            Assert.All(clusters, c => Assert.Equal(2, c.CellCount));
        }

        [Fact]
        public void BoundingBoxAndFillFractionForRaggedRegion() {
            // An L: 3 cells inside a 2x2 box → 3/4 filled.
            var clusters = Cluster(C(0, 0), C(1, 0), C(0, 1));
            TerrainCluster only = Assert.Single(clusters);
            Assert.Equal(2, only.Width);
            Assert.Equal(2, only.Height);
            Assert.Equal(3, only.CellCount);
            Assert.Equal(0.75, only.FillFraction);
        }

        [Fact]
        public void NearestCellPicksLeastKingMoveDistance() {
            // Hero at origin; a horizontal run from x=3..6. Nearest by king-move is x=3.
            var clusters = Cluster(C(3, 0), C(4, 0), C(5, 0), C(6, 0));
            TerrainCell nearest = clusters.Single().NearestCellTo(0, 0);
            Assert.Equal(3, nearest.X);
            Assert.Equal(0, nearest.Y);
        }

        [Fact]
        public void NearestCellUsesChebyshevNotManhattan() {
            // (2,2): king-move 2, Manhattan 4. (3,0): king-move 3, Manhattan 3. King-move prefers the
            // diagonal cell; Manhattan would prefer the other. Built directly so the non-adjacent
            // candidates coexist in one cluster (the flood fill would split them).
            var cluster = new TerrainCluster(
                Water, new[] { C(2, 2), C(3, 0) }, minX: 2, minY: 0, maxX: 3, maxY: 2);
            TerrainCell nearest = cluster.NearestCellTo(0, 0);
            Assert.Equal(2, nearest.X);
            Assert.Equal(2, nearest.Y);
        }

        [Fact]
        public void DuplicatePositionsAreTolerated() {
            var clusters = Cluster(C(0, 0), C(0, 0), C(1, 0));
            TerrainCluster only = Assert.Single(clusters);
            Assert.Equal(2, only.CellCount);
        }

        [Fact]
        public void AllInputCellsAreAccountedForExactlyOnce() {
            var input = new[] { C(0, 0), C(1, 0), C(1, 1), C(5, 5), C(2, 2, Mud) };
            var clusters = Cluster(input);
            int total = clusters.Sum(c => c.CellCount);
            Assert.Equal(input.Length, total);
        }
    }
}
