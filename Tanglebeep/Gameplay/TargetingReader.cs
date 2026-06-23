using System.Collections.Generic;
using System.Reflection;
using Tanglebeep.Focus;
using Tanglebeep.Speech;
using UnityEngine;

namespace Tanglebeep.Gameplay {
    /// <summary>
    /// Speaks the ranged/ability targeting state. Two distinct modes, because the game splits
    /// targeting on the <c>CURSORTARGET</c> tag (see <c>docs/targeting.md</c>):
    ///
    /// <list type="bullet">
    /// <item><b>Cursor abilities</b> (point / placed-AoE / ranged weapon) drive a tile cursor.
    /// <see cref="Aim"/> reads the single tile under the cursor as it moves, fed by the hook on
    /// <c>PlayerInputTargetingManager.UpdateCurrentTargetingInformation</c>.</item>
    /// <item><b>Directional abilities</b> (lines, cones, claws, arcs) never turn that cursor on;
    /// the player rotates a shape anchored on the hero. <see cref="DescribeShape"/> reads the
    /// whole affected footprint — shape, aim direction, and the actors caught in it — fed by hooks
    /// on <c>EnterTargeting</c> and both <c>TryRotateTargetingShape</c> overloads.</item>
    /// </list>
    ///
    /// <para>Both write one pending <see cref="MessageBuilder"/> the pump consumes (interrupting —
    /// it tracks active aiming). The cursor path dedups by tile; the shape path dedups by an
    /// orientation signature so an unchanged rotation (e.g. a diagonal that snaps to the same
    /// cardinal) does not repeat. Hook records, pump speaks. Re-queries the game live every
    /// time.</para>
    /// </summary>
    internal static class TargetingReader {
        // lineDir (the stored 8-way aim) is private on UIManagerScript; everything else we need is
        // public (abilityInTargeting, GetAllValidTargetTiles, CheckTargeting).
        private static readonly FieldInfo LineDirField =
            typeof(UIManagerScript).GetField("lineDir", BindingFlags.Instance | BindingFlags.NonPublic);

        private const int MaxTargetsSpoken = 5;

        private static MessageBuilder _pending;
        private static int _lastX = int.MinValue;
        private static int _lastY = int.MinValue;
        private static string _lastShapeSig;

        /// <summary>Record the targeted tile (cursor mode). Call from the targeting hook (main thread).</summary>
        public static void Aim(Vector2 location, bool isGoodTile) {
            int x = (int)location.x;
            int y = (int)location.y;
            if (x == _lastX && y == _lastY) {
                return;
            }

            _pending = BuildTileMessage(location, isGoodTile);
            _lastX = x;
            _lastY = y;
        }

        /// <summary>
        /// Announce a targeted tile unconditionally, bypassing the per-tile dedup. Used by the
        /// target-cycle keys: the game's own cursor hook dedups a move onto the same tile (so cycling
        /// to the sole valid target, or re-confirming the current one, would stay silent), and this
        /// guarantees the landing target is always spoken.
        /// </summary>
        public static void ForceAnnounce(Vector2 location, bool isGoodTile) {
            _pending = BuildTileMessage(location, isGoodTile);
            _lastX = (int)location.x;
            _lastY = (int)location.y;
        }

        private static MessageBuilder BuildTileMessage(Vector2 location, bool isGoodTile) {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null || MapMasterScript.activeMap == null) {
                return null;
            }

            var message = new MessageBuilder();
            MapTileData tile = MapMasterScript.GetTile(location);
            TileDescriber.Contents(message, tile, includeActor: true);

            Vector2 hp = hero.GetPos();
            message.ListItem().PushRelativeCoordinates(location - hp);
            message.ListItem(isGoodTile ? "valid target" : "invalid target");
            return message;
        }

        /// <summary>
        /// Describe the directional/area targeting footprint: shape, aim direction (when the shape
        /// can rotate), range, and the actors caught in the affected tiles. Used for line/cone/claw/
        /// arc abilities — and any rotatable cursor shape (e.g. Spell Shaper's augmented forms),
        /// where it announces the orientation change the per-tile cursor read does not. Reads the
        /// live ability and mesh; deduped by orientation so an unchanged rotation stays silent.
        /// </summary>
        public static void DescribeShape(bool includeName = false) {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            if (ums == null || !ums.CheckTargeting()) {
                return;
            }

            AbilityScript abil = ums.abilityInTargeting;
            HeroPC hero = GameMasterScript.heroPCActor;
            if (abil == null || hero == null || MapMasterScript.activeMap == null) {
                return;
            }

            bool cursor = abil.CheckAbilityTag(AbilityTags.CURSORTARGET);
            bool canRotate = abil.CheckAbilityTag(AbilityTags.CANROTATE);
            Directions dir = SpeakDirection(abil, cursor, ReadLineDir(ums));
            List<Vector2> tiles = ums.GetAllValidTargetTiles();

            // Dedup on what actually changes between rotations: the ability, the aim, and how many
            // tiles ended up valid. `dir` is already collapsed for symmetric lines, so flipping a
            // centered line end-for-end (north <-> south) is correctly treated as no change.
            // Cleared by Reset() so re-entering targeting always re-speaks.
            string sig = abil.refName + "|" + dir + "|" + (tiles?.Count ?? 0);
            if (sig == _lastShapeSig) {
                return;
            }

            _lastShapeSig = sig;

            var message = new MessageBuilder();
            if (includeName) {
                message.Fragment(GameLabelReader.Clean(abil.abilityName));
            }

            message.Fragment(ShapeName(abil, cursor));
            if (canRotate) {
                message.Fragment(DirectionWord(dir));
            }

            // Two different size concepts: a directional shape (ray/cone, anchored on the hero) has a
            // "range" — its length — held in `range`. A cursor-placed shape (line/square/circle) has a
            // "radius" — the blast size around the cursor — held in `targetRange`; its `range` is just
            // how far the cursor can move. Speak the one that describes the shape's own extent.
            int extent = cursor ? abil.targetRange : abil.range;
            if (extent > 0) {
                message.ListItem((cursor ? "radius " : "range ") + extent);
            }

            AppendTargets(message, tiles, hero);
            _pending = message;
        }

        /// <summary>Take the pending targeting description (or null), clearing it.</summary>
        public static MessageBuilder Consume() {
            MessageBuilder p = _pending;
            _pending = null;
            return p;
        }

        /// <summary>Forget all dedup state so the next read always speaks (call when targeting starts/ends).</summary>
        public static void Reset() {
            _lastX = int.MinValue;
            _lastY = int.MinValue;
            _lastShapeSig = null;
        }

        // The actors the shape will hit, nearest first, capped so a wide blast does not read forever.
        private static void AppendTargets(MessageBuilder message, List<Vector2> tiles, HeroPC hero) {
            if (tiles == null || tiles.Count == 0) {
                message.ListItem("no valid tiles");
                return;
            }

            Vector2 hp = hero.GetPos();
            var enemies = new List<Actor>();
            foreach (Actor a in MapMasterScript.GetAllTargetableInV2Tiles(tiles)) {
                if (a != hero && a.GetActorType() == ActorTypes.MONSTER) {
                    enemies.Add(a);
                }
            }

            if (enemies.Count == 0) {
                message.ListItem("no targets in area");
                return;
            }

            enemies.Sort((a, b) =>
                (a.GetPos() - hp).sqrMagnitude.CompareTo((b.GetPos() - hp).sqrMagnitude));

            message.ListItem(enemies.Count == 1 ? "1 target" : enemies.Count + " targets");
            int spoken = enemies.Count < MaxTargetsSpoken ? enemies.Count : MaxTargetsSpoken;
            for (int i = 0; i < spoken; i++) {
                Actor a = enemies[i];
                string name = a is Monster mn
                    ? GameLabelReader.Clean(mn.displayName) ?? mn.actorRefName
                    : GameLabelReader.Clean(a.displayName);
                message.ListItem();
                TileDescriber.AppendShortForm(message, a, name, a.GetPos() - hp);
            }

            if (enemies.Count > spoken) {
                message.ListItem("and " + (enemies.Count - spoken) + " more");
            }
        }

        // The direction to speak for an aim. A centered line is symmetric — the same tiles whether the
        // game aims it north or south — yet the game still cycles all 8 lineDir values. Collapse those
        // opposite halves onto one canonical end (south->north, west->east, and likewise the diagonals)
        // so a vertical line always reads "north" and a half-turn doesn't announce a phantom flip.
        // Rays (non-centered lines) are one-directional, so they keep the full 8-way aim.
        private static Directions SpeakDirection(AbilityScript abil, bool cursor, Directions dir) {
            TargetShapes shape = cursor ? abil.targetShape : abil.boundsShape;
            bool isLine = shape == TargetShapes.FLEXLINE || shape == TargetShapes.HLINE
                || shape == TargetShapes.VLINE || shape == TargetShapes.DLINE_NE
                || shape == TargetShapes.DLINE_SE;
            int i = (int)dir;
            if (isLine && abil.CheckAbilityTag(AbilityTags.CENTERED) && i >= 0 && i < 8) {
                // Directions are NORTH..NORTHWEST = 0..7; an opposite is +4, so i % 4 picks the
                // near half (north, northeast, east, southeast).
                return (Directions)(i % 4);
            }

            return dir;
        }

        // The aim's compass word, via the game's own direction-to-offset table so it matches the
        // grid convention GridDirection already speaks. Out-of-range (NEUTRAL, etc.) reads as nothing.
        private static string DirectionWord(Directions dir) {
            int i = (int)dir;
            if (i < 0 || i >= MapMasterScript.xDirections.Length) {
                return null;
            }

            Vector2 v = MapMasterScript.xDirections[i];
            return GridDirection.Compass((int)v.x, (int)v.y);
        }

        // The spoken shape name, preferring the game's localized "misc_shape_*" string (per the
        // reuse-the-game's-strings rule); falls back to a friendly word if that key is missing.
        private static string ShapeName(AbilityScript abil, bool cursor) {
            string raw = GameLabelReader.Clean(cursor ? abil.GetTargetShapeText() : abil.GetBoundsShapeText());
            if (!string.IsNullOrEmpty(raw) && !raw.StartsWith("misc_shape")) {
                return raw.ToLowerInvariant();
            }

            return FriendlyShape(cursor ? abil.targetShape : abil.boundsShape);
        }

        private static string FriendlyShape(TargetShapes shape) {
            switch (shape) {
                case TargetShapes.CONE:
                case TargetShapes.FLEXCONE:
                    return "cone";
                case TargetShapes.CLAW:
                    return "claw";
                case TargetShapes.SEMICIRCLE:
                    return "arc";
                case TargetShapes.FLEXLINE:
                case TargetShapes.HLINE:
                case TargetShapes.VLINE:
                case TargetShapes.DLINE_NE:
                case TargetShapes.DLINE_SE:
                case TargetShapes.DIRECTLINE:
                case TargetShapes.DIRECTLINE_THICK:
                    return "line";
                case TargetShapes.RECT:
                case TargetShapes.FLEXRECT:
                    return "square";
                case TargetShapes.CIRCLE:
                case TargetShapes.CIRCLECORNERS:
                    return "circle";
                case TargetShapes.BURST:
                    return "burst";
                case TargetShapes.CROSS:
                case TargetShapes.FLEXCROSS:
                case TargetShapes.XCROSS:
                    return "cross";
                case TargetShapes.POINT:
                    return "point";
                default:
                    return shape.ToString().ToLowerInvariant();
            }
        }

        private static Directions ReadLineDir(UIManagerScript ums) {
            return LineDirField != null ? (Directions)LineDirField.GetValue(ums) : Directions.NORTH;
        }
    }
}
