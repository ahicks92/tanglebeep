using System;
using HarmonyLib;
using Tanglebeep.Gameplay;
using Tanglebeep.Util;
using UnityEngine;

namespace Tanglebeep.Patches {
    /// <summary>
    /// The directional and cursor-placement halves of ability targeting.
    ///
    /// <para><b>Directional abilities</b> (lines, cones, claws, arcs, and any rotatable shape such as
    /// Spell Shaper's augmented forms) never turn on <c>PlayerInputTargetingManager</c> — that is
    /// cursor-only — so the existing <see cref="PlayerInputTargeting_Patch"/> never fires for them.
    /// We speak them via:</para>
    /// <list type="bullet">
    /// <item><c>EnterTargeting</c> postfix: announce the shape on entry (non-cursor abilities).</item>
    /// <item>both <c>TryRotateTargetingShape</c> overloads (directional <c>Directions</c> form and the
    /// inline <c>bool clockwise</c> wheel/axis form): re-announce after every rotation.</item>
    /// </list>
    ///
    /// <para><b>Cursor abilities</b> get a usability fix for blind play: the game's auto-pick, when it
    /// finds no enemy, leaves the cursor on a random/adjacent valid tile rather than on the hero — an
    /// unpredictable start with no visual to orient from. We re-home the cursor to the hero whenever it
    /// is not sitting on a monster the ability actually targets, both on entry and on each non-final
    /// pick of a multi-target ability (the only caller of <c>UpdateTargetingMeshes</c>). Directional
    /// abilities are never re-homed — they have no free cursor. Re-homing speaks for free: it flows
    /// through <c>SetVirtualCursorPosition → UpdateCurrentTargetingInformation → TargetingReader.Aim</c>.</para>
    ///
    /// Postfixes run even when the patched method early-returns; <see cref="TargetingReader"/>
    /// re-queries live state and no-ops when targeting is not actually active.
    /// </summary>
    [HarmonyPatch(typeof(UIManagerScript))]
    internal static class TargetingShape_Patch {
        [HarmonyPostfix]
        [HarmonyPatch("EnterTargeting")]
        private static void EnterTargetingPostfix(AbilityScript abil) {
            try {
                TargetingReader.Reset();
                if (abil == null) {
                    return;
                }

                if (abil.CheckAbilityTag(AbilityTags.CURSORTARGET)) {
                    // Entry: trust the game's pick only if it found a monster this ability targets.
                    RecenterCursorOnHero(abil, forceToHero: false);
                } else {
                    TargetingReader.DescribeShape(includeName: true);
                }
            } catch (Exception e) {
                Log.Warn("targeting entry handling failed: " + e.Message);
            }
        }

        // Sole caller is the non-final confirm of a multi-target ability (TDInputHandler), which leaves
        // the cursor stranded on the just-picked target. Re-home cursor abilities to the hero so the
        // next pick starts from a known spot; directional multi-targets keep their rotation untouched.
        [HarmonyPostfix]
        [HarmonyPatch("UpdateTargetingMeshes")]
        private static void UpdateTargetingMeshesPostfix() {
            try {
                AbilityScript abil = UIManagerScript.singletonUIMS?.abilityInTargeting;
                if (abil != null && abil.CheckAbilityTag(AbilityTags.CURSORTARGET)) {
                    RecenterCursorOnHero(abil, forceToHero: true);
                }
            } catch (Exception e) {
                Log.Warn("targeting multi-target re-home failed: " + e.Message);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("TryRotateTargetingShape", new[] { typeof(Directions) })]
        private static void RotateDirectionPostfix() {
            Describe();
        }

        [HarmonyPostfix]
        [HarmonyPatch("TryRotateTargetingShape", new[] { typeof(bool) })]
        private static void RotateClockwisePostfix() {
            Describe();
        }

        // Put the cursor on the hero unless the game already landed it on a monster this ability
        // targets (and we are not forcing). The hero's own tile is always a valid cursor position
        // (it sits in the ground mesh), so this never snaps back; SetVirtualCursorPosition refreshes
        // the blast preview and speaks the new tile through the existing targeting hook.
        private static void RecenterCursorOnHero(AbilityScript abil, bool forceToHero) {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            HeroPC hero = GameMasterScript.heroPCActor;
            if (ums == null || hero == null || !ums.CheckTargeting()) {
                return;
            }

            if (!forceToHero && CursorIsOnTargetableMonster(ums, abil)) {
                return;
            }

            ums.SetVirtualCursorPosition(hero.GetPos());
        }

        private static bool CursorIsOnTargetableMonster(UIManagerScript ums, AbilityScript abil) {
            bool wantsMonsters = abil.CheckAbilityTag(AbilityTags.MONSTERAFFECTED)
                && abil.targetForMonster == AbilityTarget.ENEMY;
            if (!wantsMonsters) {
                return false;
            }

            Actor onTile = MapMasterScript.GetTargetableAtLocation(ums.GetVirtualCursorPosition());
            return onTile != null && onTile.GetActorType() == ActorTypes.MONSTER;
        }

        private static void Describe() {
            try {
                TargetingReader.DescribeShape();
            } catch (Exception e) {
                Log.Warn("targeting rotate describe failed: " + e.Message);
            }
        }
    }
}
