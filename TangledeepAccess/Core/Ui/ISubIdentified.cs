namespace TangledeepAccess.Ui {
    /// <summary>
    /// Optional capability for an overlay whose content can change in place while it keeps the same
    /// <see cref="OverlayId"/> — e.g. a dialog whose body and choices change as the conversation
    /// advances through branches. The overlay reports a "generation" string built from every piece
    /// that identifies its current content; when that string changes between ticks, the dispatcher
    /// treats it as a fresh open: focus resets to the start node and the new content is re-announced.
    ///
    /// <para>Overlays whose content is re-derived live each tick from the same backing object
    /// (inventory, equipment) do not need this — their focus follows the data via
    /// <see cref="ControlId"/> reconciliation and no re-announce is wanted.</para>
    ///
    /// <para>Deliberately exclude volatile, in-place-editable values (a slider's current value)
    /// from the subidentity: including them would re-fire the "just opened" reset on every nudge.</para>
    /// </summary>
    public interface ISubIdentified {
        /// <summary>The current content generation, or null when nothing distinguishes it.</summary>
        string SubIdentity();
    }
}
