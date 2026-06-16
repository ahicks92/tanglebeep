using HarmonyLib;
using TangledeepAccess.Focus;

namespace TangledeepAccess.Patches
{
    /// <summary>
    /// The universal focus chokepoint: every menu (title, dialogs, shop, hotbar,
    /// options, and the inventory/equipment/skills columns) routes focus changes
    /// through UIManagerScript.ChangeUIFocus. The postfix records the newly focused
    /// element; FocusAnnouncer speaks it from the per-frame pump. The postfix runs
    /// regardless of the method's processEvent flag, so the column path (which calls
    /// with processEvent: false) is covered too.
    /// </summary>
    [HarmonyPatch(typeof(UIManagerScript), "ChangeUIFocus")]
    internal static class UIManagerScript_ChangeUIFocus_Patch
    {
        private static void Postfix(UIManagerScript.UIObject obj)
        {
            FocusAnnouncer.OnFocusChanged(obj);
        }
    }
}
