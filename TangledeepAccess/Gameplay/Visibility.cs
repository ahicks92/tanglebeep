using UnityEngine;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// The one place "what does the player know about this tile" is answered. Two predicates, read
    /// live off the game's own arrays (never cached, never recomputed):
    ///
    /// <list type="bullet">
    /// <item><see cref="Explored"/> — the tile has ever been seen (<c>map.exploredTiles</c>). This is
    /// minimap parity: the player knows the terrain and sees live actors there even out of sight.</item>
    /// <item><see cref="VisibleNow"/> — the hero currently sees it (<c>hero.visibleTilesArray</c>),
    /// which the game recomputes each turn with sight-reducing effects already folded in, so we just
    /// consume it.</item>
    /// </list>
    ///
    /// <para><see cref="Blurred"/> is the gap between them — explored but not currently in sight, the
    /// "you remember this, and the minimap still tracks it, but you can't see it right now" state.
    /// Out-of-bounds or out-of-play reads are false, never throw.</para>
    /// </summary>
    internal static class Visibility {
        public static bool Explored(int x, int y) {
            Map map = MapMasterScript.activeMap;
            if (map == null) {
                return false;
            }

            bool[,] e = map.exploredTiles;
            return e != null && x >= 0 && y >= 0 && x < e.GetLength(0) && y < e.GetLength(1) && e[x, y];
        }

        public static bool VisibleNow(int x, int y) {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null) {
                return false;
            }

            bool[,] v = hero.visibleTilesArray;
            return v != null && x >= 0 && y >= 0 && x < v.GetLength(0) && y < v.GetLength(1) && v[x, y];
        }

        /// <summary>Explored but not currently in sight — known, minimap-tracked, but not seen now.</summary>
        public static bool Blurred(int x, int y) {
            return Explored(x, y) && !VisibleNow(x, y);
        }

        public static bool Explored(Vector2 p) => Explored((int)p.x, (int)p.y);

        public static bool VisibleNow(Vector2 p) => VisibleNow((int)p.x, (int)p.y);

        public static bool Blurred(Vector2 p) => Blurred((int)p.x, (int)p.y);
    }
}
