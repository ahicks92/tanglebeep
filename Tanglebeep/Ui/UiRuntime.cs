namespace Tanglebeep {
    /// <summary>
    /// Process-wide handle to the live <see cref="Ui.OverlayDispatcher"/>. The dispatcher is created
    /// in <c>Plugin.Awake</c>; the static input handlers and patches reach it here to record
    /// game-driven focus changes. Recognized input is no longer routed through here — it goes onto
    /// <see cref="Controls.InputQueue"/> and is drained by the pump. Outside Core/ but Unity-free.
    /// </summary>
    internal static class UiRuntime {
        internal static Ui.OverlayDispatcher Dispatcher;
    }
}
