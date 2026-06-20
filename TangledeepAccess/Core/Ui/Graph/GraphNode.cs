using System;
using System.Collections.Generic;

namespace TangledeepAccess.Ui.Graph {
    /// <summary>The four navigable directions between graph nodes.</summary>
    public enum GraphDir {
        Up,
        Right,
        Down,
        Left,
    }

    /// <summary>
    /// The behaviors of a control. <see cref="Label"/> is required (it produces the spoken
    /// description); the rest are optional actions. Only the handlers needed now are
    /// present — the set grows as behaviors are defined (read-info, tooltips, etc.).
    /// </summary>
    public sealed class NodeVtable {
        /// <summary>Required. Append this control's spoken description to the message.</summary>
        public Action<OverlayCtx> Label;

        /// <summary>Optional. Primary activation; defaults to re-reading the label.</summary>
        public Action<OverlayCtx, Modifiers> OnClick;

        /// <summary>Optional. Secondary activation.</summary>
        public Action<OverlayCtx, Modifiers> OnRightClick;

        /// <summary>Optional. Read detailed info / tooltip about the control.</summary>
        public Action<OverlayCtx> OnReadInfo;

        /// <summary>Optional. Read secondary info about the control (Ctrl+K) — a second read channel
        /// beside <see cref="OnReadInfo"/>. The equipment sheet uses it for the item-vs-equipped
        /// stat comparison.</summary>
        public Action<OverlayCtx> OnReadSecondary;

        /// <summary>Optional. Read positional / coordinate info.</summary>
        public Action<OverlayCtx> OnReadCoords;

        /// <summary>Optional. Toggle the control's "favorite" mark.</summary>
        public Action<OverlayCtx> OnMarkFavorite;

        /// <summary>Optional. Toggle the control's "trash" mark.</summary>
        public Action<OverlayCtx> OnMarkTrash;

        /// <summary>Optional. Assign this control to a hotbar slot; the 1-8 slot arrives in
        /// <see cref="OverlayCtx.Arg"/>.</summary>
        public Action<OverlayCtx> OnAssignHotbar;

        /// <summary>Optional. Horizontal value adjust (a slider). When set, left/right do NOT
        /// navigate — they call this with sign -1 (left/decrease) or +1 (right/increase), and
        /// <c>large</c> true for a coarse Shift/skip step. null => left/right navigate normally.</summary>
        public Action<OverlayCtx, int, bool> OnHorizontalAdjust;

        /// <summary>If true, the control is skipped by search.</summary>
        public bool ExcludeFromSearch;
    }

    /// <summary>A directed edge to another node, with optional transition speech/sound.</summary>
    public sealed class Transition {
        public ControlId Destination;

        /// <summary>Optional. Spoken only while crossing this edge (e.g. lane changes).</summary>
        public Action<OverlayCtx> Label;

        /// <summary>Optional. A sound to play on this edge.</summary>
        public Action<OverlayCtx> PlaySound;
    }

    /// <summary>A control: its identity, behaviors, and up to four directional transitions.</summary>
    public sealed class GraphNode {
        public ControlId Id;
        public NodeVtable Vtable;
        public readonly Dictionary<GraphDir, Transition> Transitions =
            new Dictionary<GraphDir, Transition>();
    }
}
