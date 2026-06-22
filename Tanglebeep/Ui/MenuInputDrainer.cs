using Tanglebeep.Controls;
using Tanglebeep.Speech;

namespace Tanglebeep.Ui {
    /// <summary>
    /// Input for the menu/overlay system, beside the <see cref="OverlayDispatcher"/> it drives. We
    /// recognize only our own nav/confirm keys and pass everything else through, so the game's own
    /// menu hotkeys keep working with no need to enumerate them. We claim keys only when the active
    /// overlay declared it owns input (<see cref="OverlayDispatcher.CapturesInput"/>); a non-capturing
    /// overlay like the save-slot screen leaves navigation to the game.
    /// </summary>
    public sealed class MenuInputDrainer : InputDrainer {
        public static readonly MenuInputDrainer Instance = new MenuInputDrainer();

        public override bool Claim(bool suppressWhileHeld) {
            if (TargetingActive()) {
                return false; // targeting owns input; the overlay system stands down (see below)
            }

            OverlayDispatcher dispatcher = UiRuntime.Dispatcher;
            bool capturing = dispatcher != null && dispatcher.CapturesInput;
            if (!capturing) {
                return false; // claim nothing this frame — the game runs normally
            }

            // While an auxiliary overlay (e.g. a quantity prompt) owns input, Escape cancels just it
            // and returns to the parent — claimed here so it does not leak to the game and close the
            // whole underlying screen. A normal screen's Escape is left to the game (not claimed).
            if (dispatcher.AuxActive) {
                ModInputAction? cancel = InputKeys.MenuCancel();
                if (cancel.HasValue) {
                    InputQueue.Enqueue(this, cancel.Value);
                    return true;
                }
            }

            ModInputAction? action = InputKeys.MenuNav();
            if (action.HasValue) {
                InputQueue.Enqueue(this, action.Value);
                return true; // an owned key — the pump drives it, suppress the game
            }

            // Not one of our keys: pass through (game hotkeys keep working), unless a nav key is
            // still held in a context that would otherwise auto-repeat over us.
            if (suppressWhileHeld && InputKeys.AnyMenuNavHeld()) {
                return true;
            }

            return false;
        }

        public override void Realize(ModInputAction action, PrismSpeech speech) {
            TickAndAct(action, speech);
        }

        /// <summary>
        /// Tick the dispatcher on an input-free frame so it still follows the game's own focus
        /// changes. Called by the pump only when no menu event was realized this frame.
        /// </summary>
        public void IdleTick(PrismSpeech speech) {
            if (TargetingActive()) {
                return; // dormant during targeting — do not tick, capture, or speak
            }

            TickAndAct(null, speech);
        }

        /// <summary>
        /// True while the game is in ranged/ability targeting. The whole overlay system stands down
        /// then: targeting is game-driven and narrated by <see cref="Gameplay.TargetingReader"/>, so
        /// the menu drainer must neither capture input nor tick/speak. Without this, an overlay still
        /// mid-close (its full-screen UI fading out, so it or the Unsupported fallback still reads as
        /// "open") keeps capturing the arrow keys targeting needs, and can speak over the aim readout.
        /// </summary>
        private static bool TargetingActive() {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            return ums != null && ums.CheckTargeting();
        }

        // The dispatcher is BCL-only and cannot touch the engine, so it returns a TickResult and we
        // apply the side effects here: follow the game's focus when we moved under our own nav (and
        // play its move sound, since the game didn't move it — we suppressed its input), confirm a
        // game-backed control when activated, and speak. Whenever we write the game's focus we tell
        // the FocusWatcher, so it does not re-observe our own write as an external FocusChanged echo.
        private static void TickAndAct(ModInputAction? command, PrismSpeech speech) {
            TickResult result = UiRuntime.Dispatcher.Tick(command);

            if (result.Moved) {
                // Our cursor moved under our own nav, so the game did not move it — play the move
                // sound ourselves. The sound is owed to the edge traversal, not to a game-focus
                // change: sync the game's visual focus only when the node maps to a game widget,
                // but a pure mod-side control (no UIObject) still moved and must still be audible.
                if (result.FocusReference is UIManagerScript.UIObject moveTarget) {
                    UIManagerScript.ChangeUIFocusAndAlignCursor(moveTarget);
                    FocusWatcher.NoticeSelfWrite(moveTarget);
                }

                UIManagerScript.PlayCursorSound("Move");
            }

            if (result.Activated) {
                if (result.FocusReference is UIManagerScript.UIObject confirmTarget) {
                    UIManagerScript.ChangeUIFocusAndAlignCursor(confirmTarget);
                    FocusWatcher.NoticeSelfWrite(confirmTarget);
                }

                UIManagerScript.singletonUIMS?.CursorConfirm();
            }

            // Speak no-ops on a null/empty builder, so no guard is needed.
            speech.Speak(result.Message);
        }
    }
}
