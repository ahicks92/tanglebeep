using System.Collections.Generic;
using TangledeepAccess.Speech;
using UnityEngine;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// A discrete tile cursor for examining the map without moving the hero. Tangledeep's own
    /// Examine Mode is a smooth analog free-cursor (an icon nudged by a delta), which does not
    /// map cleanly to arrow-key tile stepping, so the mod keeps its own integer cursor and
    /// reads each tile it lands on. The input layer captures the arrow keys while the cursor is
    /// active (suppressing hero movement); the pump moves the cursor and speaks.
    ///
    /// <para>Line of sight is respected: a tile the hero can currently see is fully described
    /// (actor/feature, terrain, items); a tile out of sight reads only "not visible" plus its
    /// direction, so the cursor never reveals what the hero cannot see. State is plain ints on
    /// the main thread — no caching of game objects.</para>
    /// </summary>
    internal static class LookCursor {
        public static bool Active { get; private set; }
        private static int _x;
        private static int _y;

        /// <summary>Toggle the cursor on (centered on the hero) or off. Returns what to speak.</summary>
        public static string Toggle() {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (Active || hero == null) {
                Active = false;
                return "Look cursor off";
            }

            Active = true;
            CenterOnHero(hero);
            var message = new MessageBuilder();
            message.Fragment("Look cursor");
            Describe(message, hero);
            return message.Build();
        }

        /// <summary>Re-center the cursor on the hero and describe that tile.</summary>
        public static string Recenter() {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (!Active || hero == null) {
                return null;
            }

            CenterOnHero(hero);
            var message = new MessageBuilder();
            message.Fragment("Centered");
            Describe(message, hero);
            return message.Build();
        }

        /// <summary>Step the cursor by (dx, dy) in tile space (+x east, +y north), then describe it.</summary>
        public static string Move(int dx, int dy) {
            HeroPC hero = GameMasterScript.heroPCActor;
            Map map = MapMasterScript.activeMap;
            if (!Active || hero == null || map == null) {
                return null;
            }

            int nx = Mathf.Clamp(_x + dx, 0, map.columns - 1);
            int ny = Mathf.Clamp(_y + dy, 0, map.rows - 1);
            var message = new MessageBuilder();
            if (nx == _x && ny == _y) {
                message.Fragment("Edge"); // clamped at the map border; re-read current tile
            }

            _x = nx;
            _y = ny;
            Describe(message, hero);
            return message.Build();
        }

        /// <summary>
        /// Jump the cursor to the next (<paramref name="dir"/> +1) or previous (-1) point of
        /// interest in line of sight, nearest-first, wrapping. Lets the player tour visible
        /// actors and items without stepping tile by tile (the Factorio-Access cursor model).
        /// </summary>
        public static string JumpToPoi(int dir) {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (!Active || hero == null || MapMasterScript.activeMap == null) {
                return null;
            }

            List<Poi> pois = Surroundings.CollectVisible(hero);
            if (pois.Count == 0) {
                return "nothing in view";
            }

            pois.Sort((a, b) => a.Steps - b.Steps);

            // Where is the cursor now in that order? -1 if not on a POI.
            int current = -1;
            for (int i = 0; i < pois.Count; i++) {
                if ((int)pois[i].Pos.x == _x && (int)pois[i].Pos.y == _y) {
                    current = i;
                    break;
                }
            }

            // From "nowhere", forward starts at the nearest, backward at the farthest.
            int next = current < 0 ? (dir > 0 ? 0 : pois.Count - 1) : (current + dir + pois.Count) % pois.Count;
            _x = (int)pois[next].Pos.x;
            _y = (int)pois[next].Pos.y;

            var message = new MessageBuilder();
            Describe(message, hero);
            return message.Build();
        }

        /// <summary>Drop the cursor if the hero/level went away (e.g. on level change).</summary>
        public static void Reset() {
            Active = false;
        }

        private static void CenterOnHero(HeroPC hero) {
            Vector2 p = hero.GetPos();
            _x = (int)p.x;
            _y = (int)p.y;
        }

        private static void Describe(MessageBuilder message, HeroPC hero) {
            var pos = new Vector2(_x, _y);
            Vector2 hp = hero.GetPos();
            int dx = _x - (int)hp.x;
            int dy = _y - (int)hp.y;
            string offset = GridDirection.Offset(dx, dy);

            bool[,] visible = hero.visibleTilesArray;
            bool inSight = visible != null && visible[_x, _y];
            if (inSight) {
                TileDescriber.Contents(message, MapMasterScript.GetTile(pos), includeActor: true);
            } else {
                message.Fragment("not visible");
            }

            message.ListItem(offset);
        }
    }
}
