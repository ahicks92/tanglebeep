using HarmonyLib;
using TangledeepAccess.Gameplay;
using TangledeepAccess.Ui;
using UnityEngine;

namespace TangledeepAccess.Patches {
    /// <summary>
    /// Our in-game key handling. TDInputHandler.UpdateInput is the single chokepoint for
    /// in-game input (it is NOT called on the title screen, which is why this naturally
    /// scopes us to gameplay menus and leaves the title flow on the game's own handling).
    ///
    /// When a menu is open and our active overlay is a real navigable tree, we read arrows +
    /// enter ourselves, stash the command for the pump, and return false to skip the game's
    /// input for the frame — replacing the game's navigation. Any other key (or no key, or no
    /// navigable overlay) returns true and passes straight through to the game.
    /// </summary>
    [HarmonyPatch(typeof(TDInputHandler), "UpdateInput")]
    internal static class TDInputHandler_UpdateInput_Patch {
        private static bool Prefix() {
            // Mod gameplay hotkeys (spatial queries) act only in free play — no menu/dialog open.
            // These keys are unbound in the game, so consuming the frame has no side effect.
            if (!UIManagerScript.AnyInteractableWindowOpen()) {
                GameplayCommand? gameplay = ReadGameplayKey();
                if (gameplay.HasValue) {
                    UiRuntime.SetPendingGameplay(gameplay.Value);
                    return false;
                }

                return true; // no menu, no mod key — game handles movement/actions
            }

            OverlayDispatcher dispatcher = UiRuntime.Dispatcher;
            if (dispatcher == null || !dispatcher.WantsInputCapture) {
                return true; // a menu is open but we have nothing to drive — let the game handle it
            }

            NavCommand? command = ReadNavKey();
            if (!command.HasValue) {
                return true; // unrecognized key — pass through to the game
            }

            UiRuntime.SetPendingNav(command.Value);
            return false; // we handled it; suppress the game's input this frame
        }

        // Mod gameplay hotkeys. K = read the current tile, L = scan everything in view. Chosen
        // from letters the Default control layout leaves unbound (see docs/controls.md), so they
        // never shadow a game action.
        private static GameplayCommand? ReadGameplayKey() {
            if (Input.GetKeyDown(KeyCode.K)) {
                return GameplayCommand.ReadHere;
            }

            if (Input.GetKeyDown(KeyCode.L)) {
                return GameplayCommand.Scan;
            }

            return null;
        }

        private static NavCommand? ReadNavKey() {
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
    }
}
