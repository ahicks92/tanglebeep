using System;
using System.Collections.Generic;
using TangledeepAccess.Gameplay;
using Xunit;

namespace TangledeepAccess.Tests.Gameplay {
    public class TileShapesTests {
        // Parse a 3x3 art block (top row north, 'x' wall, '.' passable, 'p' hero) into the
        // eight-element passability array Describe expects, clockwise from north. Independent of
        // TileShapes' own parser so the two cross-check the row/col -> direction mapping.
        private static bool[] Passable(string art) {
            string[] rows = art.Trim().Split('\n');
            Assert.Equal(3, rows.Length);
            int[,] cellDir = { { 7, 0, 1 }, { 6, -1, 2 }, { 5, 4, 3 } };
            var passable = new bool[8];
            for (int r = 0; r < 3; r++) {
                string row = rows[r].Trim();
                Assert.Equal(3, row.Length);
                for (int c = 0; c < 3; c++) {
                    int dir = cellDir[r, c];
                    if (dir < 0) {
                        continue;
                    }

                    passable[dir] = row[c] == '.';
                }
            }

            return passable;
        }

        private static string Speak(string art) => TileShapes.Describe(Passable(art)).Speak();

        [Fact]
        public void AllOpenSaysNothing() {
            TileShape shape = TileShapes.Describe(Passable("...\n.p.\n..."));
            Assert.Equal(TileShapeKind.Open, shape.Kind);
            Assert.Null(shape.Speak());
        }

        [Fact]
        public void Alcove() {
            Assert.Equal("north alcove", Speak("xxx\nxpx\n..."));
            // Rotated 180: open only to the north.
            Assert.Equal("south alcove", Speak("...\nxpx\nxxx"));
        }

        [Fact]
        public void DeadEnd() {
            Assert.Equal("north dead end", Speak("xxx\nxpx\nx.x"));
            Assert.Equal("east dead end", Speak("xxx\n.px\nxxx"));
        }

        [Fact]
        public void Corner() {
            Assert.Equal("northwest corner", Speak("xxx\nxp.\nx.."));
            // Rotated one quarter-turn clockwise -> faces northeast.
            Assert.Equal("northeast corner", Speak("xxx\n.px\n..x"));
            Assert.Equal("southeast corner", Speak("..x\n.px\nxxx"));
            Assert.Equal("southwest corner", Speak("x..\nxp.\nxxx"));
        }

        [Fact]
        public void Niche() {
            Assert.Equal("northwest niche", Speak("xx.\nxp.\n..."));
            Assert.Equal("southeast niche", Speak("...\n.px\n.xx"));
        }

        [Fact]
        public void Side() {
            Assert.Equal("north side", Speak("xxx\n.p.\n..."));
            Assert.Equal("east side", Speak("..x\n.px\n..x"));
            Assert.Equal("south side", Speak("...\n.p.\nxxx"));
            Assert.Equal("west side", Speak("x..\nxp.\nx.."));
        }

        [Fact]
        public void Hallway() {
            Assert.Equal("vertical hallway", Speak("x.x\nxpx\nx.x"));
            Assert.Equal("horizontal hallway", Speak("xxx\n.p.\nxxx"));
        }

        [Fact]
        public void Enclosed() {
            TileShape shape = TileShapes.Describe(Passable("xxx\nxpx\nxxx"));
            Assert.Equal(TileShapeKind.Enclosed, shape.Kind);
            Assert.Equal(Direction.None, shape.Direction);
            Assert.Equal("no exits", shape.Speak());
        }

        [Fact]
        public void UnrecognizedEnumeratesWallsClockwiseFromNorth() {
            // Walls only to the west and northwest.
            TileShape shape = TileShapes.Describe(Passable("x..\nxp.\n..."));
            Assert.Equal(TileShapeKind.Irregular, shape.Kind);
            Assert.Equal("wall to the west, northwest", shape.Speak());
        }

        [Fact]
        public void SingleWallEnumerates() {
            Assert.Equal("wall to the north", Speak(".x.\n.p.\n..."));
        }

        [Fact]
        public void EqualityIsTheWallPattern() {
            TileShape a = TileShapes.Describe(Passable("xxx\nxpx\n..."));
            TileShape b = TileShapes.Describe(Passable("xxx\nxpx\n..."));
            TileShape c = TileShapes.Describe(Passable("...\nxpx\nxxx"));
            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.NotEqual(a, c);
            Assert.True(a != c);
        }

        [Fact]
        public void DescribeRejectsWrongLength() {
            Assert.Throws<ArgumentException>(() => TileShapes.Describe(new bool[7]));
            Assert.Throws<ArgumentException>(() => TileShapes.Describe(null));
        }

        // Every authored shape and its rotations must occupy a distinct wall pattern. A mask
        // collision makes BuildLookup keep the first and drop the rest, so a shadowed shape goes
        // missing — which shows up as fewer than the expected count of recognized patterns.
        // 4 alcove + 4 dead end + 4 corner + 4 niche + 4 side + 2 hallway + 1 enclosed = 23.
        [Fact]
        public void AllAuthoredVariantsAreDistinctlyRecognized() {
            int recognized = 0;
            for (int wall = 0; wall < 256; wall++) {
                var passable = new bool[8];
                for (int i = 0; i < 8; i++) {
                    passable[i] = (wall & (1 << i)) == 0;
                }

                TileShape shape = TileShapes.Describe(passable);
                if (shape.Kind != TileShapeKind.Open && shape.Kind != TileShapeKind.Irregular) {
                    recognized++;
                }
            }

            Assert.Equal(23, recognized);
        }
    }
}
