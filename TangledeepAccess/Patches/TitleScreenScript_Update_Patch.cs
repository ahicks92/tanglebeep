using HarmonyLib;
using TangledeepAccess.Ui;

namespace TangledeepAccess.Patches {
    /// <summary>
    /// Title-screen input chokepoint. The new-game flow runs in title context, where
    /// <c>TDInputHandler.UpdateInput</c> (our in-game hook) is never called — the title menu,
    /// slot screen, and story dialogs are all pumped by <c>TitleScreenScript.Update</c>. That
    /// method also runs the background-scroll animation, so we must not blunt-suppress it.
    ///
    /// <para>The capture decision itself lives in <see cref="MenuInput.RouteNav"/> (shared with
    /// the in-game hook). Here we only supply the title context's specifics: whether the active
    /// overlay declared it owns input (<see cref="OverlayDispatcher.CapturesInput"/>), and that we
    /// keep suppressing while a nav key is held, since the title pump auto-repeats and would
    /// otherwise double-step focus alongside us.</para>
    /// </summary>
    [HarmonyPatch(typeof(TitleScreenScript), "Update")]
    internal static class TitleScreenScript_Update_Patch {
        private static bool Prefix() {
            OverlayDispatcher dispatcher = UiRuntime.Dispatcher;
            bool capturing = dispatcher != null && dispatcher.CapturesInput;
            return MenuInput.RouteNav(capturing, suppressWhileHeld: true);
        }
    }
}
