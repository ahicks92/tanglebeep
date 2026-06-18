using System;
using System.Collections.Generic;
using System.Text;
using TangledeepAccess.Util;

namespace TangledeepAccess.Gameplay {
    /// <summary>One of the eight compass headings, or <see cref="None"/>. The integer
    /// values are indices clockwise from north, so a 90-degree clockwise turn is +2 (mod 8);
    /// this is what lets a shape mask be rotated by a simple index shift.</summary>
    public enum Direction {
        None = -1,
        North = 0,
        Northeast = 1,
        East = 2,
        Southeast = 3,
        South = 4,
        Southwest = 5,
        West = 6,
        Northwest = 7,
    }

    /// <summary>The recognized kinds of local terrain shape around the hero. <see cref="Open"/>
    /// (every side passable) and <see cref="Irregular"/> (no recognized pattern) are the two
    /// non-mask outcomes; the rest come from authored masks in <see cref="TileShapes"/>.</summary>
    public enum TileShapeKind {
        Open,
        Side,
        Niche,
        Corner,
        Alcove,
        DeadEnd,
        Hallway,
        Enclosed,
        Irregular,
    }

    /// <summary>
    /// A classified description of the eight tiles around the hero: a semantic
    /// <see cref="Kind"/> and (where it has one) the <see cref="Direction"/> it faces, plus the
    /// raw <see cref="WallMask"/> it was derived from. Structured rather than a bare string so
    /// callers can compare shapes for non-speech purposes (audio cues, change detection); the
    /// spoken form is <see cref="Speak"/>. Value identity is the wall pattern — <see cref="Kind"/>
    /// and <see cref="Direction"/> are pure functions of it.
    /// </summary>
    public readonly struct TileShape : IEquatable<TileShape> {
        public readonly TileShapeKind Kind;

        /// <summary>The heading the shape faces (e.g. the closed end of an alcove, the diagonal
        /// of a corner, the axis of a hallway), or <see cref="Direction.None"/> for shapes
        /// without an orientation (<see cref="TileShapeKind.Open"/>,
        /// <see cref="TileShapeKind.Enclosed"/>, <see cref="TileShapeKind.Irregular"/>).</summary>
        public readonly Direction Direction;

        /// <summary>The raw wall pattern: bit i set means direction i (clockwise from north,
        /// see <see cref="TileShapes.DirectionsCW"/>) is a wall.</summary>
        public readonly byte WallMask;

        public TileShape(TileShapeKind kind, Direction direction, byte wallMask) {
            Kind = kind;
            Direction = direction;
            WallMask = wallMask;
        }

        /// <summary>The spoken form, or null when there is nothing worth saying (an open tile).
        /// Recognized shapes get a name ("north alcove", "vertical hallway"); an
        /// <see cref="TileShapeKind.Irregular"/> tile enumerates its walls.</summary>
        public string Speak() {
            switch (Kind) {
                case TileShapeKind.Open:
                    return null;
                case TileShapeKind.Enclosed:
                    return "no exits";
                case TileShapeKind.Hallway:
                    return (Direction == Direction.North || Direction == Direction.South)
                        ? "vertical hallway"
                        : "horizontal hallway";
                case TileShapeKind.Irregular:
                    return TileShapes.EnumerateWalls(WallMask);
                case TileShapeKind.Side:
                    return TileShapes.Label(Direction) + " side";
                case TileShapeKind.Niche:
                    return TileShapes.Label(Direction) + " niche";
                case TileShapeKind.Corner:
                    return TileShapes.Label(Direction) + " corner";
                case TileShapeKind.Alcove:
                    return TileShapes.Label(Direction) + " alcove";
                case TileShapeKind.DeadEnd:
                    return TileShapes.Label(Direction) + " dead end";
                default:
                    return null;
            }
        }

        public bool Equals(TileShape other) => WallMask == other.WallMask;

        public override bool Equals(object obj) => obj is TileShape other && Equals(other);

        public override int GetHashCode() => WallMask;

        public static bool operator ==(TileShape a, TileShape b) => a.Equals(b);

        public static bool operator !=(TileShape a, TileShape b) => !a.Equals(b);
    }

    /// <summary>
    /// Recognizes the shape of the eight tiles surrounding the hero from their wall/passable
    /// pattern. A blind player can't see the room they're standing in; classifying it into a
    /// named shape ("north alcove", "northwest corner", "vertical hallway", ...) conveys it far
    /// faster than enumerating every open direction.
    ///
    /// <para>Shapes are authored as 3x3 ASCII masks (<c>x</c> wall, <c>.</c> passable, <c>p</c>
    /// the hero) together with a reference direction and a rotation count: the mask is rotated
    /// in 90-degree steps to generate the directional variants, so e.g. one "corner" mask
    /// anchored at northwest with <c>rotations: 4</c> yields all four corners. Add a shape by
    /// adding one mask. The list is deliberately incomplete — more common terrain shapes will be
    /// added once the game can be played and we learn what they are.</para>
    ///
    /// <para>Pure (BCL only) so it lives in Core and is unit-tested off-engine. The engine layer
    /// computes the eight passabilities (applying fog-of-war rules) in
    /// <see cref="DirectionsCW"/> order and calls <see cref="Describe"/>.</para>
    /// </summary>
    public static class TileShapes {
        // Direction indices, clockwise from north. A 90-degree clockwise rotation maps index i
        // to i+2 (mod 8), which is what makes rotating a mask a simple index shift.
        private const int N = (int)Direction.North,
            NE = (int)Direction.Northeast,
            E = (int)Direction.East,
            SE = (int)Direction.Southeast,
            S = (int)Direction.South,
            SW = (int)Direction.Southwest,
            W = (int)Direction.West,
            NW = (int)Direction.Northwest;

        /// <summary>
        /// The eight neighbor offsets in the canonical order shapes are encoded in: clockwise
        /// from north. (+x east, +y north, the game's convention.) The engine iterates this to
        /// build the passability array it hands to <see cref="Describe"/>, so the index coupling
        /// lives in exactly one place.
        /// </summary>
        public static readonly (int Dx, int Dy)[] DirectionsCW = {
            (0, 1),   // N
            (1, 1),   // NE
            (1, 0),   // E
            (1, -1),  // SE
            (0, -1),  // S
            (-1, -1), // SW
            (-1, 0),  // W
            (-1, 1),  // NW
        };

        private static readonly string[] DirName = {
            "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest",
        };

        /// <summary>The spoken name of a heading ("north", "northwest", ...).</summary>
        internal static string Label(Direction d) => DirName[(int)d];

        /// <summary>Maps a 3x3 ASCII cell (row, col) to its direction index; -1 is the center.</summary>
        private static readonly int[,] CellDir = {
            { NW, N, NE },
            { W, -1, E },
            { SW, S, SE },
        };

        /// <summary>
        /// An authored shape: an ASCII mask in a reference orientation, the number of 90-degree
        /// clockwise variants to generate from it, and the kind they classify as.
        /// <see cref="Rotations"/> is how many variants are distinct: a corner has 4 (one per
        /// diagonal), a hallway only 2 (rotating 180 degrees repeats it), an enclosed tile 1.
        /// <see cref="RefDir"/> is the reference heading, or -1 when the kind has no orientation.
        /// </summary>
        private sealed class ShapeMask {
            public readonly string Art;
            public readonly TileShapeKind Kind;
            public readonly int RefDir;
            public readonly int Rotations;

            public ShapeMask(string art, TileShapeKind kind, int refDir, int rotations) {
                Art = art;
                Kind = kind;
                RefDir = refDir;
                Rotations = rotations;
            }
        }

        // The starter set of shapes. Each is drawn from the hero's point of view: top row is
        // north, bottom is south, 'p' is the hero. Walls are 'x', passable is '.'.
        private static readonly ShapeMask[] Masks = {
            // A pocket closed on three sides, open only toward (and diagonally past) one cardinal.
            new ShapeMask(
                @"xxx
                  xpx
                  ...",
                TileShapeKind.Alcove, N, 4),

            // Closed on all but one cardinal step — the classic corridor terminus.
            new ShapeMask(
                @"xxx
                  xpx
                  x.x",
                TileShapeKind.DeadEnd, N, 4),

            // An L of wall wrapping one diagonal: two cardinals and the diagonal between them.
            new ShapeMask(
                @"xxx
                  xp.
                  x..",
                TileShapeKind.Corner, NW, 4),

            // A small notch — one diagonal plus its two flanking cardinals are wall.
            new ShapeMask(
                @"xx.
                  xp.
                  ...",
                TileShapeKind.Niche, NW, 4),

            // Wall along one whole cardinal side; everything else open.
            new ShapeMask(
                @"xxx
                  .p.
                  ...",
                TileShapeKind.Side, N, 4),

            // A one-tile-wide corridor. Two variants only (vertical, horizontal).
            new ShapeMask(
                @"x.x
                  xpx
                  x.x",
                TileShapeKind.Hallway, N, 2),

            // Walled in on every side. One variant; no orientation.
            new ShapeMask(
                @"xxx
                  xpx
                  xxx",
                TileShapeKind.Enclosed, -1, 1),
        };

        // Wall-bitmask (bit i set = wall at direction i) -> classified shape.
        private static readonly Dictionary<byte, (TileShapeKind Kind, Direction Dir)> ByWall = BuildLookup();

        private static Dictionary<byte, (TileShapeKind Kind, Direction Dir)> BuildLookup() {
            var map = new Dictionary<byte, (TileShapeKind Kind, Direction Dir)>();
            foreach (ShapeMask mask in Masks) {
                byte baseWall = ParseWalls(mask.Art);
                for (int k = 0; k < mask.Rotations; k++) {
                    byte wall = Rotate(baseWall, k);
                    var dir = mask.RefDir < 0 ? Direction.None : (Direction)((mask.RefDir + 2 * k) % 8);

                    if (map.TryGetValue(wall, out var existing)) {
                        if (existing.Kind != mask.Kind || existing.Dir != dir) {
                            Log.Warn($"TileShapes: mask collision, {existing} vs ({mask.Kind}, {dir}) for the same pattern");
                        }

                        continue;
                    }

                    map[wall] = (mask.Kind, dir);
                }
            }

            return map;
        }

        /// <summary>
        /// Classify the local shape from the eight neighbor passabilities, in
        /// <see cref="DirectionsCW"/> order (true = the hero could stand there, i.e. not a wall).
        /// Every input yields a <see cref="TileShape"/>: all-open is
        /// <see cref="TileShapeKind.Open"/>, a recognized pattern its kind, anything else
        /// <see cref="TileShapeKind.Irregular"/> (carrying the raw wall mask).
        /// </summary>
        public static TileShape Describe(bool[] passableCW) {
            if (passableCW == null || passableCW.Length != 8) {
                throw new ArgumentException("Describe expects 8 passabilities, clockwise from north", nameof(passableCW));
            }

            byte wall = 0;
            for (int i = 0; i < 8; i++) {
                if (!passableCW[i]) {
                    wall |= (byte)(1 << i);
                }
            }

            if (wall == 0) {
                return new TileShape(TileShapeKind.Open, Direction.None, 0);
            }

            return ByWall.TryGetValue(wall, out var hit)
                ? new TileShape(hit.Kind, hit.Dir, wall)
                : new TileShape(TileShapeKind.Irregular, Direction.None, wall);
        }

        internal static string EnumerateWalls(byte wall) {
            var sb = new StringBuilder("wall to the ");
            bool first = true;
            for (int i = 0; i < 8; i++) {
                if ((wall & (1 << i)) != 0) {
                    if (!first) {
                        sb.Append(", ");
                    }

                    sb.Append(DirName[i]);
                    first = false;
                }
            }

            return sb.ToString();
        }

        // Rotate a wall mask k quarter-turns clockwise: each direction index shifts by +2 per turn.
        private static byte Rotate(byte mask, int quarterTurns) {
            int shift = (2 * quarterTurns) % 8;
            byte rotated = 0;
            for (int i = 0; i < 8; i++) {
                if ((mask & (1 << i)) != 0) {
                    rotated |= (byte)(1 << ((i + shift) % 8));
                }
            }

            return rotated;
        }

        private static byte ParseWalls(string art) {
            string[] rows = SplitRows(art);
            if (rows.Length != 3) {
                throw new ArgumentException($"A shape mask must be 3 rows, got {rows.Length}: '{art}'");
            }

            byte wall = 0;
            for (int r = 0; r < 3; r++) {
                if (rows[r].Length != 3) {
                    throw new ArgumentException($"A shape mask row must be 3 columns: '{rows[r]}'");
                }

                for (int c = 0; c < 3; c++) {
                    char ch = rows[r][c];
                    int dir = CellDir[r, c];
                    if (dir < 0) {
                        if (ch != 'p' && ch != '@') {
                            throw new ArgumentException($"A shape mask's center must be 'p': '{art}'");
                        }

                        continue;
                    }

                    if (ch == 'x' || ch == '#') {
                        wall |= (byte)(1 << dir);
                    } else if (ch != '.') {
                        throw new ArgumentException($"A shape mask cell must be 'x', '.', or 'p', got '{ch}'");
                    }
                }
            }

            return wall;
        }

        private static string[] SplitRows(string art) {
            var rows = new List<string>();
            foreach (string raw in art.Split('\n')) {
                string line = raw.Trim();
                if (line.Length > 0) {
                    rows.Add(line);
                }
            }

            return rows.ToArray();
        }
    }
}
