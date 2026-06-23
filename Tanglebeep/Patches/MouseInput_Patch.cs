using HarmonyLib;

namespace Tanglebeep.Patches {
    /// <summary>
    /// Stops the cursor from chasing the physical mouse. Tangledeep treats any mouse jog as intent:
    /// during targeting it snaps the virtual cursor onto the mouse's tile
    /// (<c>TDInputHandler.CheckForTargetingInput</c> via <c>PhysicalMouseTouched</c>), which for a
    /// keyboard-only blind player silently yanks the cursor off the hero — typically onto whatever
    /// tile the idle mouse happens to sit over (often one tile off the hero) the first time targeting
    /// opens and makes the cursor visible. There is no visual to notice or correct this.
    ///
    /// <para><c>PhysicalMouseTouched</c> is the single "did the mouse move?" gate behind both that
    /// targeting snap and a minor non-targeting hover bookkeeping path; forcing it false makes the mod
    /// ignore mouse movement entirely, which is the right default here — the cursor is driven only by
    /// the keyboard. Mouse <em>buttons</em> are unaffected; this is only movement detection.</para>
    /// </summary>
    [HarmonyPatch(typeof(TDInputHandler), nameof(TDInputHandler.PhysicalMouseTouched))]
    internal static class MouseInput_Patch {
        private static bool Prefix(ref bool __result) {
            __result = false;
            return false;
        }
    }
}
