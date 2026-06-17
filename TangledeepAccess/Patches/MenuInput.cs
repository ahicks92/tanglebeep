using TangledeepAccess.Ui;
using UnityEngine;

namespace TangledeepAccess.Patches {
    /// <summary>
    /// The single source of truth for menu/dialog input arbitration, shared by every input
    /// chokepoint we hook (the in-game <c>TDInputHandler.UpdateInput</c> and the title-screen
    /// <c>TitleScreenScript.Update</c>). The game has two disjoint input pumps so we must hook
    /// both, but the decision — "does the mod own the key pressed this frame, or does it pass
    /// through to the game?" — lives here once, so it can never drift between contexts.
    ///
    /// <para>The rule is deliberately one-sided: we recognize only <em>our own</em> keys
    /// (navigation + confirm). Anything we do not recognize passes straight through to the game.
    /// So the game's own hotkeys keep working with no need for us to enumerate them — when an
    /// owned menu later wants to leave, say, the inventory sort/equip keys to the game, that is
    /// already the default; we simply never claim them.</para>
    /// </summary>
    internal static class MenuInput {
        /// <summary>
        /// Decide whether the game's input pump should run this frame, stashing a nav command
        /// when the mod owns the key pressed. The shared body of both input hooks.
        ///
        /// <para><paramref name="capturing"/> is whether the active overlay is claiming input
        /// this frame (each hook supplies the flag its context honors). When false we own
        /// nothing and the game runs. When true, a navigation/confirm key is consumed (stashed
        /// for the pump, game suppressed); any other key passes through.</para>
        ///
        /// <para><paramref name="suppressWhileHeld"/> keeps the game suppressed on the repeat
        /// frames of a held nav key even though no fresh key-down fires, so a context whose own
        /// pump would auto-repeat (the title screen) cannot move focus alongside us — a doubled
        /// step. Contexts that do not auto-repeat pass these frames through.</para>
        /// </summary>
        /// <returns>True to let the game's input run this frame; false to suppress it.</returns>
        public static bool RouteNav(bool capturing, bool suppressWhileHeld) {
            if (!capturing) {
                return true; // we claim nothing this frame — the game runs normally
            }

            NavCommand? command = ReadNavKey();
            if (command.HasValue) {
                UiRuntime.SetPendingNav(command.Value);
                return false; // an owned key — the pump drives it, suppress the game
            }

            // Not one of our keys: let it through (the game's own hotkeys keep working), unless a
            // nav key is still held in a context that would otherwise auto-repeat over us.
            if (suppressWhileHeld && AnyNavKeyHeld()) {
                return false;
            }

            return true;
        }

        /// <summary>The nav/confirm key pressed this frame, or null. Arrows move; Enter activates.</summary>
        public static NavCommand? ReadNavKey() {
            if (Input.GetKeyDown(KeyCode.UpArrow)) {
                return NavCommand.Up;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow)) {
                return NavCommand.Down;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) {
                return NavCommand.Left;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow)) {
                return NavCommand.Right;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
                return NavCommand.Activate;
            }

            return null;
        }

        /// <summary>
        /// True while any nav/confirm key is held (not just the key-down frame). Used by
        /// <see cref="RouteNav"/> to keep an auto-repeating context suppressed on the repeat
        /// frames so a held key cannot leak through and double-step focus.
        /// </summary>
        public static bool AnyNavKeyHeld() {
            return Input.GetKey(KeyCode.UpArrow)
                || Input.GetKey(KeyCode.DownArrow)
                || Input.GetKey(KeyCode.LeftArrow)
                || Input.GetKey(KeyCode.RightArrow)
                || Input.GetKey(KeyCode.Return)
                || Input.GetKey(KeyCode.KeypadEnter);
        }
    }
}
