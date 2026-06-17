using System.Collections.Generic;
using TangledeepAccess.Focus;
using TangledeepAccess.Speech;
using UnityEngine;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// Shared "what is on this tile" description, used by both the read-here query and the look
    /// cursor. Reuses the game's own hover builder (<c>HoverInfoScript.GetHoverText</c>) for the
    /// actor/feature on a tile — it returns the monster/NPC/object there, or empty for bare
    /// terrain — and falls back to the tile type plus any ground items. Always re-queries live.
    /// </summary>
    internal static class TileDescriber {
        /// <summary>
        /// Append a tile's contents to <paramref name="message"/>. When
        /// <paramref name="includeActor"/>, the actor/feature on the tile (the game's hover text)
        /// leads; otherwise only terrain and items are spoken (e.g. the hero's own tile, where
        /// the hover would just say the hero's name).
        /// </summary>
        public static void Contents(MessageBuilder message, MapTileData tile, bool includeActor) {
            if (tile == null) {
                message.Fragment("off map");
                return;
            }

            string actor = includeActor ? GameLabelReader.Clean(HoverInfoScript.GetHoverText(tile)) : null;
            if (actor != null) {
                message.Fragment(actor);
            } else {
                message.Fragment(Terrain(tile));
            }

            AppendItems(message, tile);
        }

        /// <summary>The coarse tile type, spoken lowercase ("ground", "water", "wall").</summary>
        public static string Terrain(MapTileData tile) {
            return tile.tileType.ToString().ToLowerInvariant().Replace('_', ' ');
        }

        private static void AppendItems(MessageBuilder message, MapTileData tile) {
            List<Item> items = tile.GetItemsInTile();
            if (items == null) {
                return;
            }

            foreach (Item item in items) {
                string name = GameLabelReader.Clean(item.GetNameForUI());
                if (name != null) {
                    message.ListItem("item: " + name);
                }
            }
        }
    }
}
