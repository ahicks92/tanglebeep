using System;
using TangledeepAccess.Speech;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// The spoken identity of a tile for the exploration cursor: its <see cref="Terrain"/> word and
    /// its <see cref="Shape"/>. Movement announces only the parts of this key that changed since the
    /// previous tile (so "ground alcove" → "hallway" when only the shape changed), which keeps
    /// stepping terse. Dynamic contents (entities, items) are NOT in the key — they are read every
    /// time, not differentially — and more fields may join the key later, so callers must not treat
    /// these two as the complete description of a tile.
    ///
    /// <para>Pure (BCL + Core only) so it lives in Core and is unit-tested off-engine; the engine
    /// layer builds it from a live <c>MapTileData</c>.</para>
    /// </summary>
    public readonly struct TileKey : IEquatable<TileKey> {
        public readonly string Terrain;
        public readonly TileShape Shape;

        /// <summary>Explored but not currently in sight — the tile is known and minimap-tracked but
        /// not seen now. Part of the key so crossing the sight boundary announces "blurred" / "clear"
        /// once on the differential, the same way a shape change is announced once.</summary>
        public readonly bool Blurred;

        public TileKey(string terrain, TileShape shape, bool blurred = false) {
            Terrain = terrain;
            Shape = shape;
            Blurred = blurred;
        }

        /// <summary>
        /// Append only the fields that differ from <paramref name="previous"/> — the blurred boundary,
        /// terrain word, and/or shape. A null <paramref name="previous"/> (no prior context) appends a
        /// full read: "blurred" only if blurred (clear is the unmarked default), then terrain and shape.
        /// </summary>
        public void AppendChanges(MessageBuilder message, TileKey? previous) {
            bool full = previous == null;
            if (full) {
                if (Blurred) {
                    message.Fragment("blurred");
                }
            } else if (previous.Value.Blurred != Blurred) {
                message.Fragment(Blurred ? "blurred" : "clear");
            }
            if (full || previous.Value.Terrain != Terrain) {
                message.Fragment(Terrain);
            }
            if (full || previous.Value.Shape != Shape) {
                message.Fragment(Shape.Speak()); // null (open) ignored by the builder
            }
        }

        /// <summary>Append the whole key (blurred marker + terrain + shape), no differencing.</summary>
        public void AppendFull(MessageBuilder message) {
            if (Blurred) {
                message.Fragment("blurred");
            }
            message.Fragment(Terrain);
            message.Fragment(Shape.Speak());
        }

        public bool Equals(TileKey other) => Terrain == other.Terrain && Shape == other.Shape && Blurred == other.Blurred;

        public override bool Equals(object obj) => obj is TileKey other && Equals(other);

        public override int GetHashCode() => unchecked((((Terrain?.GetHashCode() ?? 0) * 397) ^ Shape.GetHashCode()) * 397 ^ (Blurred ? 1 : 0));

        public static bool operator ==(TileKey a, TileKey b) => a.Equals(b);

        public static bool operator !=(TileKey a, TileKey b) => !a.Equals(b);
    }
}
