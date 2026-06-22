using HarmonyLib;

namespace Tanglebeep.Patches {
    /// <summary>
    /// Makes Escape/Cancel close the equipment screen, the way it already closes the inventory and
    /// character sheets.
    ///
    /// <para>In vanilla, <c>TDInputHandler.HandleInteractableWindowInput</c> has explicit
    /// close-on-cancel branches for the character sheet and inventory (they match their window state
    /// against "Cancel"/"Options Menu" and call <c>TryCloseFullScreenUI</c>), but <b>none</b> for the
    /// equipment screen — it is only ever dismissed by re-pressing its own toggle key (View
    /// Equipment, E). So pressing Escape on the equipment screen matches none of those branches and
    /// falls through to the handler's default, <c>OpenFullScreenUI(UITabs.OPTIONS)</c> — the options
    /// menu pops open on top of the still-open gear screen. For a sighted player that is a minor
    /// quirk; for a speech-only player it is a dead end with no obvious way back.</para>
    ///
    /// <para>This prefix adds the missing branch: when the equipment screen is the open full-screen
    /// UI and Cancel or Options Menu is pressed, close it and report the input as handled, suppressing
    /// the original body (and its options-menu fallthrough). Any other key (a direction, confirm, the
    /// toggle key) leaves the method to run normally.</para>
    ///
    /// <para><b>Why a forced close, not <c>TryCloseFullScreenUI</c>.</b> Two game guards otherwise make
    /// the first Escape a no-op, so closing took a wasted press plus a second one:</para>
    /// <list type="number">
    /// <item>The equipment screen's <c>TryTurnOff</c> short-circuits when its cursor is in the
    /// <c>tooltip_has_cursor</c> sub-state (which it opens in): it just releases the tooltip cursor and
    /// returns false, so <c>TryCloseFullScreenUI</c> does not close — the *first* Escape only dismisses a
    /// tooltip the speech UI never showed. <c>ForceCloseFullScreenUI</c> skips <c>TryTurnOff</c> entirely,
    /// so the gear screen closes on the first press. (The character sheet's <c>TryTurnOff</c> is an
    /// unconditional <c>return true</c>, which is why it never had this problem.)</item>
    /// <item>Opening any full-screen UI starts an 0.8s <c>fPreventOptionMenuToggleTimer</c>;
    /// <c>CloseFullScreenUI</c> itself bails out while <c>PreventingOptionMenuToggle()</c> is true, so even
    /// a forced close is refused for the first 0.8s after opening. That timer debounces the Escape *toggle*
    /// in free play (Escape opens options; the same key-hold must not instantly re-close it) — irrelevant
    /// to a screen opened by its own key. <c>UnlockOptionsMenuToggle()</c> clears it so the close lands
    /// immediately.</item>
    /// </list>
    /// </summary>
    [HarmonyPatch(typeof(TDInputHandler), "HandleInteractableWindowInput")]
    internal static class TDInputHandler_EquipmentClose_Patch {
        private static bool Prefix(ref bool __result) {
            if (!UIManagerScript.GetWindowState(UITabs.EQUIPMENT)
                || UIManagerScript.GetWindowState(UITabs.OPTIONS)) {
                return true;
            }

            Rewired.Player player = TDInputHandler.player;
            if (player == null
                || !(player.GetButtonDown("Cancel") || player.GetButtonDown("Options Menu"))) {
                return true;
            }

            // Clear the just-opened toggle debounce (guard 2) and force past the tooltip-cursor
            // short-circuit (guard 1) so a single Escape closes, even immediately after opening.
            UIManagerScript.UnlockOptionsMenuToggle();
            UIManagerScript.ForceCloseFullScreenUI();
            __result = true;
            return false;
        }
    }
}
