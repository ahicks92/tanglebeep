using TangledeepAccess.Gameplay;
using TangledeepAccess.Ui;

namespace TangledeepAccess.Controls {
    /// <summary>
    /// The priority chain and single entry point for both input chokepoints. Each frame it offers
    /// the active drainers the frame in priority order; the first to <see cref="InputDrainer.Claim"/>
    /// it owns the frame and the game's own input is suppressed. A drainer that does not apply
    /// (no menu open, look cursor off) or does not recognize the key returns false and the next is
    /// tried; if none claims, the game runs. This replaces a state machine that picked one handler:
    /// the drainers decide for themselves whether they apply, so the priority order is the only
    /// policy that lives here.
    /// </summary>
    internal static class InputChain {
        // ui → look → scanner → gameplay. The menu wins over the look cursor (a dialog opened
        // mid-look still takes keys); the look cursor wins over free play (it owns its movement keys
        // while active); the scanner is modeless and claims only its own dedicated nav keys, so its
        // position relative to look/gameplay does not matter; free play is the floor (it claims only
        // our query hotkeys, passing movement to the game).
        private static readonly InputDrainer[] InGame = {
            MenuInputDrainer.Instance,
            LookInputDrainer.Instance,
            ScannerInputDrainer.Instance,
            GameplayInputDrainer.Instance,
        };

        /// <summary>In-game pump: offer ui, then the look cursor, then free play.</summary>
        public static bool RouteInGame() {
            foreach (InputDrainer drainer in InGame) {
                if (drainer.Claim(suppressWhileHeld: false)) {
                    return false; // claimed — suppress the game this frame
                }
            }

            return true; // nobody claimed — the game runs
        }

        /// <summary>
        /// Title pump: only menu overlays exist here (no gameplay, no look cursor), and the title
        /// auto-repeats, so we keep suppressing while a nav key is held.
        /// </summary>
        public static bool RouteTitle() {
            return !MenuInputDrainer.Instance.Claim(suppressWhileHeld: true);
        }
    }
}
