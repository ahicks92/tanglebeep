namespace TangledeepAccess.Controls {
    /// <summary>
    /// One frame's recognized input, handed from the hook (which claimed it) to the pump (which
    /// realizes it). It carries a reference to the <see cref="InputDrainer"/> that produced it, so
    /// the pump dispatches straight back to that drainer's <see cref="InputDrainer.Realize"/> —
    /// the claiming decision made in the hook stays authoritative and is never re-derived.
    /// </summary>
    public struct PendingInput {
        public InputDrainer Source;
        public ModInputAction Action;
    }
}
