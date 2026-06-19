using System.Collections.Generic;
using Rewired;
using TangledeepAccess.Util;
using UnityEngine;

namespace TangledeepAccess.Controls {
    /// <summary>
    /// Owns the in-game keyboard map so the mod can claim physical keys for itself. Run together
    /// whenever the game (re)builds its keyboard map:
    ///
    /// <para><b>1. Force the Default layout.</b> Tangledeep ships two keyboard layouts
    /// (<c>KeyboardControlMaps.DEFAULT</c> and <c>WASD</c>); the mod supports only Default. A
    /// non-Default layout is switched back via <c>Rewired.LayoutHelper.SwitchLayout</c>
    /// (gms-independent, safe even at the title screen).</para>
    ///
    /// <para><b>2. Evacuate claimed keys (<see cref="Table"/>).</b> The mod reads several keys raw
    /// via <c>UnityEngine.Input</c>; the game's binding on such a key must be cleared so it does not
    /// also fire. Each row is deleted outright or relocated (dragging <i>every</i> action on the
    /// source key, with axis polarity, onto a target key+modifier).</para>
    ///
    /// <para><b>3. Delete specific actions (<see cref="ActionDeletes"/>).</b> For a key that hosts
    /// several actions where we only want to drop one (e.g. Tab keeps Toggle Large Minimap but loses
    /// Jump to Searchbar).</para>
    ///
    /// <para><b>4. Mirror the numpad onto the left-hand letters (<see cref="Mirror"/>).</b> The 3x3
    /// movement block q/w/e a/s/d z/x/c is bound to the same actions as numpad 7/8/9 4/5/6 1/2/3,
    /// <i>without</i> removing the numpad bindings — both move. (Center s/5 is intentionally left
    /// alone.)</para>
    ///
    /// <para>Everything is idempotent within a pass (forcing a layout already Default is a no-op,
    /// evacuating an absent key removes nothing, the mirror skips an already-present binding).
    /// Applied at startup (the ready-poll in <c>Plugin.Update</c>) and re-applied after the game
    /// rebuilds its keyboard map via <c>GameMasterScript_SwitchControlMode_Patch</c>.</para>
    /// </summary>
    internal static class KeymapPatch {
        // Rewired layout id of the "Default" keyboard layout (WASD is 1). Verified live.
        private const int DefaultLayoutId = 0;

        // Modifier conventions matching the game's own (it binds both sides, e.g. "LeftControl,
        // RightControl"), so either physical modifier key works.
        private const ModifierKeyFlags Alt = ModifierKeyFlags.LeftAlt | ModifierKeyFlags.RightAlt;
        private const ModifierKeyFlags Ctrl = ModifierKeyFlags.LeftControl | ModifierKeyFlags.RightControl;
        private const ModifierKeyFlags Shift = ModifierKeyFlags.LeftShift | ModifierKeyFlags.RightShift;

        // "Out of the way" combo for game features we keep but never expect to use — frees the bare
        // key while leaving the feature reachable behind all three modifiers.
        private const ModifierKeyFlags CtrlAltShift = Ctrl | Alt | Shift;

        /// <summary>
        /// One key the mod claims. The game's binding(s) matching (<see cref="SourceKey"/>,
        /// <see cref="SourceMod"/>) are removed; if <see cref="TargetKey"/> is set, each is recreated
        /// on the target key carrying its original action and axis polarity plus
        /// <see cref="TargetMod"/>. A null target means delete only.
        /// </summary>
        private readonly struct KeyEvac {
            public readonly KeyCode SourceKey;
            public readonly ModifierKeyFlags SourceMod;
            public readonly KeyCode? TargetKey;
            public readonly ModifierKeyFlags TargetMod;

            private KeyEvac(KeyCode sourceKey, ModifierKeyFlags sourceMod, KeyCode? targetKey, ModifierKeyFlags targetMod) {
                SourceKey = sourceKey;
                SourceMod = sourceMod;
                TargetKey = targetKey;
                TargetMod = targetMod;
            }

            /// <summary>Clear a key, discarding whatever the game bound to it.</summary>
            public static KeyEvac Delete(KeyCode sourceKey, ModifierKeyFlags sourceMod = ModifierKeyFlags.None) {
                return new KeyEvac(sourceKey, sourceMod, null, default);
            }

            /// <summary>Clear a key, relocating every action on it onto <paramref name="targetKey"/>.</summary>
            public static KeyEvac MoveTo(KeyCode sourceKey, KeyCode targetKey, ModifierKeyFlags targetMod = ModifierKeyFlags.None, ModifierKeyFlags sourceMod = ModifierKeyFlags.None) {
                return new KeyEvac(sourceKey, sourceMod, targetKey, targetMod);
            }
        }

        /// <summary>A numpad key whose bindings are duplicated onto a letter key (numpad kept).</summary>
        private readonly struct KeyMirror {
            public readonly KeyCode Source;
            public readonly KeyCode Target;

            public KeyMirror(KeyCode source, KeyCode target) {
                Source = source;
                Target = target;
            }
        }

        /// <summary>
        /// Keys the mod claims, evacuated on top of the freshly forced Default layout. The Default
        /// layout supplies all stock bindings; this table lists only the deltas. See
        /// <c>docs/default-keymap.txt</c> for the stock layout.
        /// </summary>
        private static readonly KeyEvac[] Table = {
            // Free the right-hand mod block (u/i/j/...) — relocate game screens to Alt+digit.
            KeyEvac.MoveTo(KeyCode.I, KeyCode.Alpha1, Alt),   // View Consumables
            KeyEvac.MoveTo(KeyCode.E, KeyCode.Alpha2, Alt),   // View Equipment
            KeyEvac.MoveTo(KeyCode.J, KeyCode.Alpha3, Alt),   // View Skills
            KeyEvac.MoveTo(KeyCode.C, KeyCode.Alpha4, Alt),   // View Character Info
            KeyEvac.MoveTo(KeyCode.Q, KeyCode.Q, Alt),        // View Rumors -> Alt+Q
            KeyEvac.MoveTo(KeyCode.U, KeyCode.Semicolon),     // Healing Flask / Consumable / Unequip -> ;

            // D is freed for move-east. Both its actions are covered elsewhere: Use Stairs is
            // redundant with Confirm/Enter (TDInputHandler standing-on-stairs path), and Drop Item
            // is handled directly by the mod's inventory/equipment overlays.
            KeyEvac.Delete(KeyCode.D),

            // Kept but shoved out of the way behind Ctrl+Alt+Shift, freeing the bare letter.
            KeyEvac.MoveTo(KeyCode.M, KeyCode.M, CtrlAltShift),   // Toggle Monster Health Bars
            KeyEvac.MoveTo(KeyCode.O, KeyCode.O, CtrlAltShift),   // Toggle Pet HUD

            KeyEvac.Delete(KeyCode.X),                        // Examine Mode (replaced by the mod's look cursor)
            KeyEvac.Delete(KeyCode.S),                        // View Skills duplicate (also on J)
            KeyEvac.Delete(KeyCode.PageUp),                   // mod always needs PageUp
            KeyEvac.Delete(KeyCode.PageDown),                 // mod always needs PageDown

            // Ctrl+digit duplicates the F5-F8 weapon switches; drop the duplicates.
            KeyEvac.Delete(KeyCode.Alpha1, Ctrl),
            KeyEvac.Delete(KeyCode.Alpha2, Ctrl),
            KeyEvac.Delete(KeyCode.Alpha3, Ctrl),
            KeyEvac.Delete(KeyCode.Alpha4, Ctrl),

            // Ctrl belongs to the screen reader (stop-speech). Default "Cycle Hotbars" sits here.
            KeyEvac.Delete(KeyCode.LeftControl),
        };

        /// <summary>Actions removed entirely (a key may keep its other actions).</summary>
        private static readonly string[] ActionDeletes = {
            "Jump to Searchbar",   // Tab keeps Toggle Large Minimap, loses search-jump
        };

        /// <summary>
        /// Numpad-to-letter movement mirror: the q/w/e a/s/d z/x/c block gets the same bindings as
        /// numpad 7/8/9 4/5/6 1/2/3, with the numpad left intact. Center (5 -> s) is omitted on
        /// purpose.
        /// </summary>
        private static readonly KeyMirror[] Mirror = {
            new KeyMirror(KeyCode.Keypad7, KeyCode.Q),
            new KeyMirror(KeyCode.Keypad8, KeyCode.W),
            new KeyMirror(KeyCode.Keypad9, KeyCode.E),
            new KeyMirror(KeyCode.Keypad4, KeyCode.A),
            new KeyMirror(KeyCode.Keypad6, KeyCode.D),
            new KeyMirror(KeyCode.Keypad1, KeyCode.Z),
            new KeyMirror(KeyCode.Keypad2, KeyCode.X),
            new KeyMirror(KeyCode.Keypad3, KeyCode.C),
        };

        /// <summary>
        /// Apply the forced layout + remaps if Rewired is ready. Returns true once it has run (so the
        /// startup poll can stop). Safe to call repeatedly.
        /// </summary>
        public static bool TryApplyWhenReady() {
            if (!ReInput.isReady) {
                return false;
            }

            Apply();
            return true;
        }

        /// <summary>Force the Default layout, then run the remap pipeline for every player.</summary>
        public static void Apply() {
            if (!ReInput.isReady) {
                return;
            }

            foreach (Player player in ReInput.players.Players) {
                ForceDefaultLayout(player);
                ApplyTable(player);
                ApplyActionDeletes(player);
                ApplyMirror(player);
            }
        }

        // Switch the keyboard back to the Default layout if anything else is loaded. Uses
        // LayoutHelper directly (not GameMasterScript.SwitchControlMode) so it does not re-trigger
        // the SwitchControlMode patch — no recursion through our own postfix.
        private static void ForceDefaultLayout(Player player) {
            bool needsSwitch = false;
            foreach (ControllerMap map in player.controllers.maps.GetMaps(ControllerType.Keyboard, 0)) {
                if (map.layoutId != DefaultLayoutId) {
                    needsSwitch = true;
                    break;
                }
            }

            if (needsSwitch) {
                Log.Info("Forcing keyboard layout to Default (unsupported layout was active)");
                LayoutHelper.SwitchLayout(player.id, ControllerType.Keyboard, 0, "Default", "Default");
            }
        }

        private static void ApplyTable(Player player) {
            foreach (ControllerMap map in player.controllers.maps.GetMaps(ControllerType.Keyboard, 0)) {
                foreach (KeyEvac evac in Table) {
                    Evacuate(map, evac);
                }
            }
        }

        private static void ApplyActionDeletes(Player player) {
            foreach (ControllerMap map in player.controllers.maps.GetMaps(ControllerType.Keyboard, 0)) {
                foreach (string action in ActionDeletes) {
                    if (map.DeleteElementMapsWithAction(action)) {
                        Log.Info("Removed action binding: " + action);
                    }
                }
            }
        }

        private static void ApplyMirror(Player player) {
            foreach (ControllerMap map in player.controllers.maps.GetMaps(ControllerType.Keyboard, 0)) {
                foreach (KeyMirror mirror in Mirror) {
                    CopyKey(map, mirror.Source, mirror.Target);
                }
            }
        }

        // Snapshot the matching element maps first (we mutate the map below), then delete each and,
        // for a relocation, recreate it on the target carrying its action and axis polarity.
        private static void Evacuate(ControllerMap map, KeyEvac evac) {
            List<ActionElementMap> matches = new List<ActionElementMap>();
            foreach (ActionElementMap ae in map.GetElementMaps()) {
                if (ae.keyCode == evac.SourceKey && ae.modifierKeyFlags == evac.SourceMod) {
                    matches.Add(ae);
                }
            }

            foreach (ActionElementMap ae in matches) {
                int actionId = ae.actionId;
                Pole pole = ae.axisContribution;
                map.DeleteElementMap(ae.id);
                if (evac.TargetKey.HasValue) {
                    map.CreateElementMap(actionId, pole, evac.TargetKey.Value, evac.TargetMod);
                }
            }

            if (matches.Count > 0) {
                string dest = evac.TargetKey.HasValue ? "to " + Describe(evac.TargetKey.Value, evac.TargetMod) : "(deleted)";
                Log.Info("Evacuated " + matches.Count + " binding(s) from " + Describe(evac.SourceKey, evac.SourceMod) + " " + dest);
            }
        }

        // Duplicate every binding on the source key onto the target key, keeping the source.
        // Skips a binding already present on the target so a re-apply does not stack duplicates.
        private static void CopyKey(ControllerMap map, KeyCode source, KeyCode target) {
            List<ActionElementMap> sources = new List<ActionElementMap>();
            foreach (ActionElementMap ae in map.GetElementMaps()) {
                if (ae.keyCode == source) {
                    sources.Add(ae);
                }
            }

            int added = 0;
            foreach (ActionElementMap ae in sources) {
                if (HasMapping(map, ae.actionId, ae.axisContribution, target, ae.modifierKeyFlags)) {
                    continue;
                }
                if (map.CreateElementMap(ae.actionId, ae.axisContribution, target, ae.modifierKeyFlags)) {
                    added++;
                }
            }

            if (added > 0) {
                Log.Info("Mirrored " + added + " binding(s) from " + source + " to " + target);
            }
        }

        private static bool HasMapping(ControllerMap map, int actionId, Pole pole, KeyCode key, ModifierKeyFlags mod) {
            foreach (ActionElementMap ae in map.GetElementMaps()) {
                if (ae.actionId == actionId && ae.axisContribution == pole && ae.keyCode == key && ae.modifierKeyFlags == mod) {
                    return true;
                }
            }
            return false;
        }

        private static string Describe(KeyCode key, ModifierKeyFlags mod) {
            return mod == ModifierKeyFlags.None ? key.ToString() : mod + "+" + key;
        }
    }
}
