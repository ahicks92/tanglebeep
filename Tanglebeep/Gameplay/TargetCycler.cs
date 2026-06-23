using System.Collections.Generic;
using Tanglebeep.Speech;
using UnityEngine;

namespace Tanglebeep.Gameplay {
    /// <summary>
    /// Jumps the targeting cursor between the monsters a cursor ability can validly hit, on the bare
    /// brackets (] next, [ previous). The game's auto-pick and free cursor movement land on any tile,
    /// empty ones included; this skips straight to "something you can shoot," the equivalent of a
    /// sighted player clicking each enemy in turn.
    ///
    /// <para>Only for cursor (free-movement) abilities — directional shapes rotate, they have no
    /// cursor to jump. A candidate is a monster whose tile the game reports as a valid target
    /// (<c>UIManagerScript.CheckValidTarget</c>, which honors the ability's range, line of sight, and
    /// friendly-fire rules), so the list is exactly the enemies this cast can actually reach.</para>
    /// </summary>
    internal static class TargetCycler {
        /// <summary>True when a cursor (free-movement) targeting session is open, so the brackets cycle.</summary>
        public static bool Active() {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            return ums != null
                && ums.CheckTargeting()
                && ums.abilityInTargeting != null
                && ums.abilityInTargeting.CheckAbilityTag(AbilityTags.CURSORTARGET);
        }

        /// <summary>
        /// Move the cursor to the next (<paramref name="dir"/> &gt; 0) or previous valid monster
        /// target, wrapping, and announce it. Returns null on success (the move announces itself via
        /// <see cref="TargetingReader.ForceAnnounce"/>); returns a message when there is nothing to
        /// target.
        /// </summary>
        public static MessageBuilder Cycle(int dir) {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            HeroPC hero = GameMasterScript.heroPCActor;
            if (ums == null || hero == null || !Active()) {
                return null;
            }

            List<Vector2> targets = ValidMonsterTargets(ums, hero);
            if (targets.Count == 0) {
                return new MessageBuilder().Fragment("no targets in range");
            }

            Vector2 cursor = ums.GetVirtualCursorPosition();
            int current = targets.FindIndex(t => t == cursor);
            int next = current >= 0
                ? ((current + dir) % targets.Count + targets.Count) % targets.Count
                : (dir > 0 ? 0 : targets.Count - 1);

            Vector2 pick = targets[next];
            ums.SetVirtualCursorPosition(pick);
            TargetingReader.ForceAnnounce(pick, isGoodTile: true);
            return null;
        }

        // Visible-or-not monsters the current ability can validly target, nearest first (Manhattan,
        // ties broken by offset for a stable cycle order). Validity is the game's own — CheckValidTarget
        // tests the ground targeting mesh, which already encodes range, LOS, and friendly-fire filtering.
        private static List<Vector2> ValidMonsterTargets(UIManagerScript ums, HeroPC hero) {
            Vector2 hp = hero.GetPos();
            int hx = (int)hp.x;
            int hy = (int)hp.y;

            var found = new List<(Vector2 Pos, int Dist, int Dx, int Dy)>();
            foreach (Actor a in MapMasterScript.activeMap.actorsInMap) {
                if (a == hero || ActorPresence.IsGone(a) || a.GetActorType() != ActorTypes.MONSTER) {
                    continue;
                }

                Vector2 p = a.GetPos();
                if (!ums.CheckValidTarget(p)) {
                    continue;
                }

                int dx = (int)p.x - hx;
                int dy = (int)p.y - hy;
                found.Add((p, System.Math.Abs(dx) + System.Math.Abs(dy), dx, dy));
            }

            found.Sort((l, r) => l.Dist != r.Dist ? l.Dist - r.Dist : l.Dx != r.Dx ? l.Dx - r.Dx : l.Dy - r.Dy);

            var result = new List<Vector2>(found.Count);
            foreach (var f in found) {
                result.Add(f.Pos);
            }

            return result;
        }
    }
}
