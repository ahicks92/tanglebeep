using UnityEngine;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// Static-terrain passability queries. A tile counts as an impassable wall only on its
    /// *terrain* — the map edge, a wall/void tile, or solid terrain — never because an actor
    /// (monster, NPC) stands on it. This is the geometry the room-shape reader and the wall-echo
    /// cue both ask about, so it lives in one place rather than being duplicated.
    /// </summary>
    internal static class TerrainQuery {
        public static bool IsImpassableWall(Vector2 p) {
            if (!MapMasterScript.InBounds(p)) {
                return true;
            }

            MapTileData t = MapMasterScript.GetTile(p);
            if (t == null) {
                return true;
            }

            return t.tileType == TileTypes.WALL
                || t.tileType == TileTypes.NOTHING
                || t.tileType == TileTypes.MAPEDGE
                || t.CheckTag(LocationTags.SOLIDTERRAIN);
        }
    }
}
