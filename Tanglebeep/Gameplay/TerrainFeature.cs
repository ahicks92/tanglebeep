using System;
using System.Collections.Generic;

namespace Tanglebeep.Gameplay {
    /// <summary>
    /// The single, tight definition of "this actor is a terrain tile" and the bridge from live
    /// terrain actors to the engine-agnostic <see cref="TerrainClusterer"/>.
    ///
    /// <para>Terrain is identified by the game's own authoritative flag <see cref="Destructible.isTerrainTile"/>
    /// (set from the prefab — TerrainTile/MudTile/ElectricTile/LaserTile — so water, mud, lava, electric
    /// and laser tiles), read off the entity rather than the tile's <c>LocationTags</c>: tags are
    /// map-generation state, but player abilities spawn and remove terrain at runtime, and the spawned
    /// actor carries the truth. Deliberately NOT a flag-combo heuristic: anything that is conceptually
    /// terrain but lacks this flag stays an ordinary object, surfacing the discrepancy instead of being
    /// silently absorbed.</para>
    /// </summary>
    internal static class TerrainFeature {
        /// <summary>Whether an actor is a terrain tile, per the game's own <c>isTerrainTile</c> flag.</summary>
        public static bool Is(Actor a) {
            return a is Destructible d && d.isTerrainTile && !d.destroyed && !d.isDestroyed;
        }

        /// <summary>The terrain kind id (the game's <c>SpecialMapObject</c> as int) — the cluster key.</summary>
        public static int Kind(Actor a) {
            return (int)((Destructible)a).mapObjType;
        }

        /// <summary>The spoken word for a terrain kind, matching the cursor's terrain wording.</summary>
        public static string Name(int kind) {
            switch ((SpecialMapObject)kind) {
                case SpecialMapObject.WATER:
                    return "water";
                case SpecialMapObject.MUD:
                    return "mud";
                case SpecialMapObject.LAVA:
                    return "lava";
                case SpecialMapObject.ELECTRIC:
                    return "electrified";
                default:
                    return ((SpecialMapObject)kind).ToString().ToLowerInvariant();
            }
        }

        /// <summary>
        /// Cluster the map's terrain tiles whose position passes <paramref name="include"/> (the
        /// caller's visibility gate — explored for the scanner, in-sight for the F2 radar), so the
        /// "never cluster across unexplored" rule lives in the predicate the caller hands in.
        /// </summary>
        public static List<TerrainCluster> Cluster(Map map, Func<int, int, bool> include) {
            var cells = new List<TerrainCell>();
            if (map != null) {
                foreach (Actor a in map.actorsInMap) {
                    if (a == null || !Is(a)) {
                        continue;
                    }

                    int x = (int)a.GetPos().x;
                    int y = (int)a.GetPos().y;
                    if (include(x, y)) {
                        cells.Add(new TerrainCell(x, y, Kind(a)));
                    }
                }
            }

            return TerrainClusterer.Cluster(cells);
        }
    }
}
