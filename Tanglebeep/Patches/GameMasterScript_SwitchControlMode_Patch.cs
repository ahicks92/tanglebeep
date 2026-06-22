using HarmonyLib;
using Tanglebeep.Controls;

namespace Tanglebeep.Patches {
    /// <summary>
    /// <c>GameMasterScript.SwitchControlMode</c> tears down and rebuilds the keyboard map from
    /// scratch (clear, load the chosen layout, re-enable), which both reintroduces the game's stock
    /// bindings on keys we claim and can load an unsupported (WASD) layout. Re-assert the mod's
    /// forced layout + key evacuations after the rebuild completes.
    /// </summary>
    [HarmonyPatch(typeof(GameMasterScript), "SwitchControlMode")]
    internal static class GameMasterScript_SwitchControlMode_Patch {
        private static void Postfix() {
            KeymapPatch.Apply();
        }
    }
}
