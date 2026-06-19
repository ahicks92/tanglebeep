using Rewired;
using TangledeepAccess.Util;

namespace TangledeepAccess.Controls {
    /// <summary>
    /// Neutralizes the one game keybinding that collides with a screen reader: the default
    /// <b>Ctrl</b> binding on the Rewired action <c>"Cycle Hotbars"</c>. Every screen reader taps
    /// Ctrl to silence speech, so a blind player cannot have that also cycle their hotbar. We strip
    /// the keyboard binding off the action entirely (the mod owns hotbar cycling on its own key —
    /// backtick — instead); Ctrl then does nothing in-game and belongs to the reader.
    ///
    /// <para>This is the <i>only</i> binding that genuinely has to move — Ctrl is unique in being
    /// unavoidably claimed by the reader — so there is no general keymap-override layer, just this.
    /// Removal is idempotent and re-applied whenever the game rebuilds its keyboard map (startup,
    /// and the layout-switch / restore-defaults path via
    /// <c>GameMasterScript_SwitchControlMode_Patch</c>).</para>
    /// </summary>
    internal static class KeymapPatch {
        // The Rewired action whose default Ctrl binding we remove. Read at TDInputHandler.UpdateInput
        // as player.GetButtonDown("Cycle Hotbars") -> UIManagerScript.ToggleSecondaryHotbar().
        private const string CycleHotbars = "Cycle Hotbars";

        /// <summary>
        /// Apply the unbind if Rewired is initialized. Returns true once it has run (so the caller
        /// can stop polling). Safe to call repeatedly — removing an already-absent binding is a no-op.
        /// </summary>
        public static bool TryApplyWhenReady() {
            if (!ReInput.isReady) {
                return false;
            }

            UnbindCtrlCycleHotbars();
            return true;
        }

        /// <summary>
        /// Strip every keyboard binding for "Cycle Hotbars" from all players' keyboard maps. Leaves
        /// joystick bindings alone — only the keyboard Ctrl is the reader collision.
        /// </summary>
        public static void UnbindCtrlCycleHotbars() {
            if (!ReInput.isReady) {
                return;
            }

            int removed = 0;
            foreach (Player player in ReInput.players.Players) {
                // controllerId 0 is the keyboard; a player can hold several keyboard maps (Default,
                // MenuControls, …). Strip the action from each it appears in.
                foreach (ControllerMap map in player.controllers.maps.GetMaps(ControllerType.Keyboard, 0)) {
                    if (map.DeleteElementMapsWithAction(CycleHotbars)) {
                        removed++;
                    }
                }
            }

            if (removed > 0) {
                Log.Info("Freed Ctrl: removed " + removed + " keyboard binding(s) from \"" + CycleHotbars + "\"");
            }
        }
    }
}
