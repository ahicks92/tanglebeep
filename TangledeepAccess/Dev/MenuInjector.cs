using TangledeepAccess.Controls;
using TangledeepAccess.Ui;

namespace TangledeepAccess.Dev {
    /// <summary>
    /// Injects input into the mod's <b>own</b> menu/overlay system — the path a real key press
    /// takes — as opposed to <see cref="InputInjector"/>, which drives the <i>game's</i> UIObject
    /// neighbor compass. The distinction matters: an overlay that declared it owns input
    /// (<see cref="OverlayDispatcher.CapturesInput"/>, e.g. the inventory or skill sheet) runs its
    /// own cursor and ignores the game's focus, so <c>/input</c> can't drive it — only this can.
    ///
    /// <para>We enqueue a <see cref="ModInputAction"/> tagged with <see cref="MenuInputDrainer.Instance"/>,
    /// exactly as the physical-key drainer does when it claims one of our keys. The per-frame pump
    /// (<c>Plugin.Update</c>) drains it the same frame and realizes it through the dispatcher, so the
    /// move sound, game-focus sync, and speech all happen on the real path — observe the result via
    /// <c>/speech</c> and <c>/gui/mod</c>. Enqueue must run on the main thread (the queue is not
    /// locked); the dev server marshals this handler there.</para>
    /// </summary>
    internal static class MenuInjector {
        public static string Inject(string verb) {
            string v = (verb ?? "").Trim().ToLowerInvariant();
            ModInputAction action;
            switch (v) {
                // Directional: +x east, +y north (the dispatcher maps these to up/down/left/right).
                case "up":
                    action = ModInputAction.Move(0, 1);
                    break;
                case "down":
                    action = ModInputAction.Move(0, -1);
                    break;
                case "left":
                    action = ModInputAction.Move(-1, 0);
                    break;
                case "right":
                    action = ModInputAction.Move(1, 0);
                    break;
                case "confirm":
                case "enter":
                case "ok":
                    action = ModInputAction.Of(ModInputKind.Confirm);
                    break;
                case "readinfo":
                case "info":
                case "read":
                    action = ModInputAction.Of(ModInputKind.ReadInfo);
                    break;
                case "favorite":
                case "fav":
                    action = ModInputAction.Of(ModInputKind.MarkFavorite);
                    break;
                case "trash":
                    action = ModInputAction.Of(ModInputKind.MarkTrash);
                    break;
                default:
                    return "[unknown verb] '" + verb
                        + "' - menu: up|down|left|right|confirm|readinfo|favorite|trash\n";
            }

            OverlayDispatcher dispatcher = UiRuntime.Dispatcher;
            bool capturing = dispatcher != null && dispatcher.CapturesInput;
            InputQueue.Enqueue(MenuInputDrainer.Instance, action);
            return "menu " + v + " -> queued"
                + (capturing ? "" : " (warning: no overlay is capturing input now; read /speech)")
                + "\n";
        }
    }
}
