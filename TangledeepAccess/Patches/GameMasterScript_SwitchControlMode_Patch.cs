using HarmonyLib;
using TangledeepAccess.Controls;

namespace TangledeepAccess.Patches {
    /// <summary>
    /// <c>GameMasterScript.SwitchControlMode</c> rebuilds the keyboard controller map from scratch
    /// (clear, reload the chosen layout, re-enable), which re-introduces the default Ctrl binding on
    /// "Cycle Hotbars". It runs on a layout switch and on restore-defaults — both rare for a blind
    /// player, but if either fires we must immediately strip Ctrl again. Re-assert our unbind after
    /// the rebuild completes.
    /// </summary>
    [HarmonyPatch(typeof(GameMasterScript), "SwitchControlMode")]
    internal static class GameMasterScript_SwitchControlMode_Patch {
        private static void Postfix() {
            KeymapPatch.UnbindCtrlCycleHotbars();
        }
    }
}
