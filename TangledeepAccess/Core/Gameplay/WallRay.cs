using System;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// Marches a cardinal ray out from a tile to find the nearest impassable wall, for the wall-echo
    /// cue. Pure integer logic over three injected tile predicates so it is testable off-engine; the
    /// engine supplies bounds/wall/visibility from live game state.
    ///
    /// <para>The ray passes through every passable tile regardless of whether that tile is currently
    /// visible — an unseen open tile along the way is NOT the limit of sight (the game's per-tile LOS
    /// can leave a tile in a straight column unflagged while the wall beyond it is flagged). Sight is
    /// applied to the wall itself: the first impassable tile within range is reported only if that
    /// tile is visible, so a wall in an unexplored/dark area is not pinged.</para>
    /// </summary>
    public static class WallRay {
        /// <summary>
        /// Tiles from (ox, oy) to the first impassable, visible wall along (dx, dy), within
        /// <paramref name="maxTiles"/>, or null if none. Predicates take tile (x, y).
        /// </summary>
        public static int? DistanceToWall(
            int ox,
            int oy,
            int dx,
            int dy,
            int maxTiles,
            Func<int, int, bool> inBounds,
            Func<int, int, bool> isWall,
            Func<int, int, bool> isVisible) {
            for (int d = 1; d <= maxTiles; d++) {
                int x = ox + dx * d;
                int y = oy + dy * d;
                if (!inBounds(x, y)) {
                    return null; // walked off the map without hitting a wall
                }
                if (isWall(x, y)) {
                    // The wall blocks the ray. Ping it only if it is within the player's sight.
                    return isVisible(x, y) ? (int?)d : null;
                }
                // Passable tile: keep going, even if this tile is not currently visible.
            }
            return null; // open all the way to the range limit
        }
    }
}
