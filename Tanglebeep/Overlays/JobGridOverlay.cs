using System.Collections.Generic;
using HarmonyLib;
using Tanglebeep.Focus;
using Tanglebeep.Ui;

namespace Tanglebeep.Overlays {
    /// <summary>
    /// The character-creation job grid (the <c>JOBSELECT</c> stage). The game models it as a 2-D
    /// button grid navigated by its own cursor; we do not mirror that topology. Instead we extract
    /// the full set of jobs from the game and re-present them as one <b>owned</b> vertical menu,
    /// built identically every tick regardless of where the game's focus sits: an instruction line
    /// first, then every unlocked job (each reading its full readout), then the locked jobs last.
    ///
    /// <para>The instruction line reuses the game's own prompt for this screen — the create
    /// button's "SELECT JOB" label, which the game otherwise keeps hidden until a job is picked —
    /// rather than a mod-authored sentence.</para>
    ///
    /// <para>We own input (the title pump drives our cursor), so navigation is our flat list, not
    /// the game's grid. Each job node still carries its backing <c>UIObject</c>, so moving onto it
    /// syncs the game's visual focus (firing <c>HoverJobInfo</c>, which updates the on-screen
    /// description and plays the move sound). Activating an unlocked job runs the game's own
    /// <c>SelectJob</c> + <c>ConfirmJobSelection</c>, advancing to feat select exactly as the game
    /// would; activating a locked one is a no-op beyond the game's own mystery-job feedback.</para>
    /// </summary>
    internal sealed class JobGridOverlay : IUiOverlay {
        // jobEnumOrder maps a button slot to a CharacterJobs enum value; it is private static.
        private static readonly AccessTools.FieldRef<int[]> JobEnumOrder =
            AccessTools.StaticFieldRefAccess<int[]>(AccessTools.Field(typeof(CharCreation), "jobEnumOrder"));

        // The 8-slot compass orthogonals, used only to discover which buttons are actually on the
        // grid (DLC-gated buttons are rewired out of the neighbor graph, not disabled).
        private static readonly int[] Orthogonals = { 0, 2, 4, 6 };

        public OverlayId Id => OverlayId.JobGrid;

        public OverlayResult Handler() {
            bool onTitle = GameMasterScript.gmsSingleton != null
                && GameMasterScript.gmsSingleton.titleScreenGMS;
            return onTitle
                && TitleScreenScript.CreateStage == CreationStages.JOBSELECT
                && CharCreation.creationActive
                ? OverlayResult.Active(this)
                : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            // The game's own prompt for this screen, not a mod-authored one. A no-op on Enter so
            // this header line never confirms a default through the game.
            builder.AddClickable(
                ControlId.Structural("jobgrid:instructions"),
                ctx => {
                    string prompt = GameLabelReader.Clean(StringManager.GetString("ui_btn_selectjob"));
                    if (!string.IsNullOrEmpty(prompt)) {
                        ctx.Message.Fragment(prompt);
                    }
                },
                (ctx, mods) => { }
            );

            UIManagerScript.UIObject[] buttons = CharCreation.jobButtons;
            int[] order = JobEnumOrder();
            if (buttons != null && order != null) {
                HashSet<UIManagerScript.UIObject> present = PresentButtons(buttons);
                // Two passes over the buttons in slot order: unlocked jobs first, locked jobs
                // last, so the list reads the same every tick.
                AddJobs(builder, buttons, order, present, locked: false);
                AddJobs(builder, buttons, order, present, locked: true);
            }

            // We drive this screen ourselves; the title pump engages on the explicit flag.
            builder.CaptureInput();
        }

        private static void AddJobs(
            IOverlayBuilder builder,
            UIManagerScript.UIObject[] buttons,
            int[] order,
            HashSet<UIManagerScript.UIObject> present,
            bool locked
        ) {
            for (int i = 0; i < buttons.Length; i++) {
                if (buttons[i] == null || !present.Contains(buttons[i]) || i >= order.Length) {
                    continue;
                }

                if (IsUnlocked(order[i]) == locked) {
                    continue; // wrong partition for this pass
                }

                int idx = i; // capture per node
                builder.AddClickable(
                    ControlId.Referenced(buttons[i], "jobgrid:job:" + idx),
                    ctx => {
                        string label = JobReadout(order, idx);
                        if (!string.IsNullOrEmpty(label)) {
                            ctx.Message.Fragment(label);
                        }
                    },
                    (ctx, mods) => SelectJob(idx)
                );
            }
        }

        /// <summary>
        /// Run the game's own selection for the job at <paramref name="idx"/>. For an unlocked job
        /// this sets it and confirms straight through to feat select; for a locked one SelectJob
        /// only shows the mystery-job feedback and leaves jobSelected false, so we never confirm.
        /// </summary>
        private static void SelectJob(int idx) {
            CharCreation cc = CharCreation.singleton;
            if (cc == null) {
                return;
            }

            cc.SelectJob(idx);
            if (CharCreation.jobSelected) {
                cc.ConfirmJobSelection(0);
            }
        }

        /// <summary>The full readout for a job slot, or the locked string for a locked job.</summary>
        private static string JobReadout(int[] order, int idx) {
            int jobEnum = order[idx];
            if (!IsUnlocked(jobEnum)) {
                return GameLabelReader.Clean(StringManager.GetString("ui_job_locked"));
            }

            CharacterJobData data = CharacterJobData.GetJobDataByEnum(jobEnum);
            // Null while masterJobList is still loading; a later tick re-speaks once it is ready.
            return data != null ? GameLabelReader.Clean(data.GetFullJobReadout("")) : null;
        }

        private static bool IsUnlocked(int jobEnum) {
            return SharedBank.CheckIfJobIsUnlocked((CharacterJobs)jobEnum);
        }

        /// <summary>
        /// The job buttons actually on the grid this run. DLC- and unlock-gated buttons are not
        /// disabled (BeginCharCreation_JobSelection force-enables every button); they are removed
        /// from the neighbor graph and shoved off-screen. So grid membership is reachability from
        /// the first button over the orthogonal neighbor links, which is stable run to run.
        /// </summary>
        private static HashSet<UIManagerScript.UIObject> PresentButtons(
            UIManagerScript.UIObject[] buttons
        ) {
            var seen = new HashSet<UIManagerScript.UIObject>();
            if (buttons.Length == 0 || buttons[0] == null) {
                return seen;
            }

            var queue = new Queue<UIManagerScript.UIObject>();
            queue.Enqueue(buttons[0]);
            while (queue.Count > 0) {
                UIManagerScript.UIObject uo = queue.Dequeue();
                if (uo == null || !seen.Add(uo)) {
                    continue;
                }

                UIManagerScript.UIObject[] neighbors = uo.neighbors;
                if (neighbors == null) {
                    continue;
                }

                foreach (int slot in Orthogonals) {
                    if (slot < neighbors.Length && neighbors[slot] != null) {
                        queue.Enqueue(neighbors[slot]);
                    }
                }
            }

            return seen;
        }
    }
}
