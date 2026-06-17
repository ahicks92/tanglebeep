namespace TangledeepAccess.Dev {
    /// <summary>
    /// Injects logical UI controls by calling the game's own handlers, NOT OS synthetic keys:
    /// we drive the game while its window is unfocused, where SendInput (needs foreground) and
    /// PostMessage (won't reach Rewired's raw input) don't work. Confirm routes through the
    /// game's single CursorConfirm dispatcher; directional moves walk the focused UIObject's
    /// 8-slot neighbor compass (orthogonal slots) via ChangeUIFocus, which also trips the mod's
    /// focus hook so the move gets spoken.
    ///
    /// This covers the uiObjectFocus menu model (title, dialogs, most screens). In-game hero
    /// actions use the `step`/`wait`/`stairs`/`pickup` verbs, which commit a TurnData through
    /// GameMasterScript.TryNextTurn (the game resolves move/attack/NPC-interaction). Save-slot
    /// selection still has no verb (drive it via /eval OnSelectSlotConfirmPressed).
    /// </summary>
    internal static class InputInjector {
        // UIObject.neighbors is an 8-slot compass; orthogonals only.
        private const int Up = 0;
        private const int Right = 2;
        private const int Down = 4;
        private const int Left = 6;

        public static string Inject(string verb) {
            switch ((verb ?? "").Trim().ToLowerInvariant()) {
                case "confirm":
                case "enter":
                case "ok":
                    UIManagerScript ums = UIManagerScript.singletonUIMS;
                    if (ums == null) {
                        return "confirm: no UIManagerScript\n";
                    }
                    ums.CursorConfirm();
                    return "confirm -> CursorConfirm()\n";
                case "up":
                    return Move(Up, "up");
                case "right":
                    return Move(Right, "right");
                case "down":
                    return Move(Down, "down");
                case "left":
                    return Move(Left, "left");
                case "wait":
                    return Turn(TurnTypes.PASS, UnityEngine.Vector2.zero, "wait");
                case "stairs":
                    return TravelManager.TryTravelStairs() ? "stairs -> traveling\n" : "stairs: none here\n";
                case "pickup":
                    return TileInteractions.TryPickupItemsInHeroTile() ? "pickup -> picked up\n" : "pickup: nothing here\n";
                default:
                    string v = (verb ?? "").Trim().ToLowerInvariant();
                    if (v.StartsWith("step")) {
                        return Step(v.Substring(4).Trim());
                    }

                    return "[unknown verb] '" + verb
                        + "' - menu: up|down|left|right|confirm; game: step <dir>|wait|stairs|pickup\n";
            }
        }

        // In-game hero action: a one-tile step that the game resolves as move / attack / NPC
        // interaction, the same TurnData path the keyboard input commits. +x east, +y north.
        private static string Step(string dir) {
            UnityEngine.Vector2 off;
            switch (dir) {
                case "n":
                case "north":
                    off = new UnityEngine.Vector2(0, 1);
                    break;
                case "s":
                case "south":
                    off = new UnityEngine.Vector2(0, -1);
                    break;
                case "e":
                case "east":
                    off = new UnityEngine.Vector2(1, 0);
                    break;
                case "w":
                case "west":
                    off = new UnityEngine.Vector2(-1, 0);
                    break;
                case "ne":
                case "northeast":
                    off = new UnityEngine.Vector2(1, 1);
                    break;
                case "nw":
                case "northwest":
                    off = new UnityEngine.Vector2(-1, 1);
                    break;
                case "se":
                case "southeast":
                    off = new UnityEngine.Vector2(1, -1);
                    break;
                case "sw":
                case "southwest":
                    off = new UnityEngine.Vector2(-1, -1);
                    break;
                default:
                    return "step: bad direction '" + dir + "' (n|s|e|w|ne|nw|se|sw)\n";
            }

            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null) {
                return "step: no hero\n";
            }

            UnityEngine.Vector2 target = hero.GetPos() + off;
            MapTileData tile = MapMasterScript.GetTile(target);
            if (tile == null || tile.tileType == TileTypes.WALL) {
                return "step " + dir + ": blocked\n";
            }

            return Turn(TurnTypes.MOVE, target, "step " + dir);
        }

        private static string Turn(TurnTypes type, UnityEngine.Vector2 newPosition, string name) {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null || GameMasterScript.gmsSingleton == null) {
                return name + ": not in game\n";
            }

            var turn = new TurnData { actorThatInitiatedTurn = hero };
            turn.SetTurnType(type);
            if (type == TurnTypes.MOVE) {
                turn.newPosition = newPosition;
            }

            GameMasterScript.gmsSingleton.TryNextTurn(turn, newTurn: true);
            return name + " -> turn taken\n";
        }

        private static string Move(int slot, string name) {
            UIManagerScript.UIObject focus = UIManagerScript.uiObjectFocus;
            if (focus == null) {
                return name + ": no uiObjectFocus to move from\n";
            }
            UIManagerScript.UIObject[] neighbors = focus.neighbors;
            if (neighbors == null || slot >= neighbors.Length || neighbors[slot] == null) {
                return name + ": no neighbor in that direction\n";
            }
            UIManagerScript.ChangeUIFocusAndAlignCursor(neighbors[slot]);
            UIManagerScript.UIObject now = UIManagerScript.uiObjectFocus;
            return name + " -> focus now " + (now != null && now.gameObj != null ? now.gameObj.name : "?") + "\n";
        }
    }
}
