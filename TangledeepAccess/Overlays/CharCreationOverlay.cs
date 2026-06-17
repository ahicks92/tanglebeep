using System;
using HarmonyLib;
using TangledeepAccess.Focus;
using TangledeepAccess.Ui;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// Speaks Tangledeep's character-creation job-selection grid. The job buttons are
    /// image-only (an <c>Animatable</c> walk sprite, no TMP text), so the generic fallback
    /// reads nothing — the description lives in a separate label the game fills in only as a
    /// side effect of hovering. This overlay instead derives each job's text itself, the way
    /// the game would: <c>jobEnumOrder[i]</c> → job enum → <c>GetFullJobReadout</c> (pure
    /// synchronous string assembly, available the instant focus lands), or the locked-job
    /// string when the job is not yet unlocked.
    ///
    /// <para>Scope: only the job grid (detected by the focused control being a job button), so
    /// the other creation stages stay on the generic mirror / dialog overlay until each gets
    /// its own handling. The button graph is mirrored exactly as the generic fallback does;
    /// only the label provider differs. See docs/new-game-menu.md.</para>
    /// </summary>
    internal sealed class CharCreationOverlay : IUiOverlay {
        // jobEnumOrder maps a button slot to a CharacterJobs enum value; it is private static.
        private static readonly AccessTools.FieldRef<int[]> JobEnumOrder =
            AccessTools.StaticFieldRefAccess<int[]>(AccessTools.Field(typeof(CharCreation), "jobEnumOrder"));

        public OverlayId Id => OverlayId.CharCreation;

        /// <summary>
        /// Active while character creation is showing the job grid: creation is live and the
        /// focused control is one of the job buttons. Keying off the focused button (rather
        /// than a stage flag) scopes us precisely to the grid and naturally cedes to the dialog
        /// overlay when an intro/prompt dialog is up.
        /// </summary>
        public OverlayResult Handler() {
            return CharCreation.creationActive && FocusedJobIndex() >= 0
                ? OverlayResult.Active(this)
                : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            UIManagerScript.UIObject[] buttons = CharCreation.jobButtons;
            GameMenuMirror.Build(builder, uo => JobLabel(uo, buttons));
        }

        /// <summary>The spoken text for one job button: its full readout, or the locked string.</summary>
        private static string JobLabel(UIManagerScript.UIObject uo, UIManagerScript.UIObject[] buttons) {
            int idx = Array.IndexOf(buttons, uo);
            if (idx < 0) {
                // Not a job button (some other control reachable in the graph) — read it plainly.
                return GameLabelReader.ReadLabel(uo);
            }

            int[] order = JobEnumOrder();
            if (order == null || idx >= order.Length) {
                return null;
            }

            int jobEnum = order[idx];
            if (!SharedBank.CheckIfJobIsUnlocked((CharacterJobs)jobEnum)) {
                return GameLabelReader.Clean(StringManager.GetString("ui_job_locked"));
            }

            CharacterJobData data = CharacterJobData.GetJobDataByEnum(jobEnum);
            // Null while masterJobList is still loading; a later tick re-speaks once it is ready.
            return data != null ? GameLabelReader.Clean(data.GetFullJobReadout("")) : null;
        }

        /// <summary>Index of the focused job button in <c>jobButtons</c>, or -1.</summary>
        private static int FocusedJobIndex() {
            UIManagerScript.UIObject[] buttons = CharCreation.jobButtons;
            UIManagerScript.UIObject focus = UIManagerScript.uiObjectFocus;
            return buttons != null && focus != null ? Array.IndexOf(buttons, focus) : -1;
        }
    }
}
