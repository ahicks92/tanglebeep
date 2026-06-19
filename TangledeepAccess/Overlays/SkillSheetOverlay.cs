using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TangledeepAccess.Focus;
using TangledeepAccess.Speech;
using TangledeepAccess.Ui;
using TangledeepAccess.Ui.Graph;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// The skill / job sheet (<c>Switch_UISkillSheet</c>, the "J" tab). The game models it as two
    /// side-by-side scrolling button columns plus a mode-toggle row, a vertical hotbar, and a
    /// selected-info panel, and it runs in one of two modes: <b>Learn</b> (buy job abilities with
    /// JP) and <b>Slot</b> (assign learned actives to the hotbar, equip passives). We re-present it
    /// as one owned grid, built fresh every tick.
    ///
    /// <para><b>Scope (staged):</b> this first cut narrates <b>Learn mode</b> fully — the buyable
    /// job-ability list (read each ability's status, confirm to learn/master, read-info for the full
    /// tooltip) and the innate job-bonus text. The mode toggle is always present, so the player can
    /// switch the game between modes; <b>Slot mode</b> is not narrated yet and renders a spoken
    /// notice instead of silently doing nothing. Hotbar review/binding will be a separate overlay.</para>
    ///
    /// <para><b>Data source:</b> the job's own <c>JobAbilities</c> (filtered exactly as the game's
    /// <c>FillJobAbilitiesList</c> does — drop innates and post-mastery skills until the job is
    /// mastered). Read live each build; never cached.</para>
    ///
    /// <para><b>Navigation (a 2-D grid):</b> a header row (status anchor + the two mode buttons),
    /// then an <b>innate</b> row (a summary cell that reads the whole bonus text, then one cell per
    /// passive tier — tier 2/3 announce "locked" until their JP / mastery gate is met), then a
    /// <b>learn</b> row (a label cell, then one cell per buyable ability). Each row's first cell names
    /// the row; left/right walks its contents, up/down moves between rows. Rows do not share keys, so
    /// up/down always lands on the row's first cell.</para>
    /// </summary>
    internal sealed class SkillSheetOverlay : IUiOverlay {
        // The screen's current mode; private on the screen type.
        private static readonly AccessTools.FieldRef<Switch_UISkillSheet, ESkillSheetMode> SheetMode =
            AccessTools.FieldRefAccess<Switch_UISkillSheet, ESkillSheetMode>("sheetMode");

        // The screen's own builder for the right-column innate-bonus text (tier 1/2/3 + infusions,
        // with live lock states). Private; reflected so we speak the game's exact string.
        private static readonly MethodInfo InnateText =
            AccessTools.Method(typeof(Switch_UISkillSheet), "GetStringForJobInnateBonuses");

        public OverlayId Id => OverlayId.Skills;

        public OverlayResult Handler() {
            return Screen() != null ? OverlayResult.Active(this) : OverlayResult.Inactive;
        }

        /// <summary>The live skill sheet if it is the open full-screen UI, else null.</summary>
        private static Switch_UISkillSheet Screen() {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            if (ums == null) {
                return null;
            }

            if (!(ums.currentFullScreenUI is Switch_UISkillSheet sheet)) {
                return null;
            }

            return sheet.gameObject.activeInHierarchy ? sheet : null;
        }

        public void Build(IOverlayBuilder builder) {
            builder.CaptureInput();

            Switch_UISkillSheet screen = Screen();
            if (screen == null) {
                return;
            }

            BuildHeaderRow(builder, screen);

            if (SheetMode(screen) == ESkillSheetMode.purchase_abilities) {
                BuildLearnBody(builder, screen);
            } else {
                builder.AddLabel(
                    ControlId.Structural("skill:slotstub"),
                    ctx => ctx.Message
                        .Fragment(ModStrings.SlotUnsupportedHead)
                        .ListItem(ModStrings.SlotUnsupportedHint)
                );
            }
        }

        // --- Header / mode row -----------------------------------------------------------------

        private static void BuildHeaderRow(IOverlayBuilder builder, Switch_UISkillSheet screen) {
            builder.StartRow("header");

            builder.AddLabel(ControlId.Structural("skill:header"), ctx => HeaderLabel(ctx.Message, screen));

            AddModeButton(builder, screen, "learn", ModStrings.LearnMode, ESkillSheetMode.purchase_abilities);
            AddModeButton(builder, screen, "slot", ModStrings.SlotMode, ESkillSheetMode.assign_abilities);

            builder.EndRow();
        }

        private static void HeaderLabel(MessageBuilder message, Switch_UISkillSheet screen) {
            HeroPC hero = GameMasterScript.heroPCActor;
            message.Fragment(ModStrings.SkillsHeader);
            message.ListItem(GameLabelReader.Clean(hero.myJob.DisplayName));
            message.ListItem(ModStrings.Jp((int)hero.GetCurJP()));
            message.ListItem(
                SheetMode(screen) == ESkillSheetMode.purchase_abilities
                    ? ModStrings.ModeLearning
                    : ModStrings.ModeSlotting
            );
        }

        // Confirm switches the game into this mode (its own EnterNewMode — plays the cue, rebuilds
        // the columns). Our cursor stays on the button (stable key); the body rebuilds to match.
        private static void AddModeButton(
            IOverlayBuilder builder,
            Switch_UISkillSheet screen,
            string key,
            string word,
            ESkillSheetMode mode
        ) {
            builder.AddClickable(
                ControlId.Structural("skill:mode:" + key),
                ctx => ctx.Message.Fragment(word).Fragment(SheetMode(screen) == mode ? ModStrings.Selected : null),
                (ctx, mods) => {
                    screen.EnterNewMode(mode);
                    ctx.Message.Fragment(word);
                }
            );
        }

        // --- Learn-mode body -------------------------------------------------------------------

        private static void BuildLearnBody(IOverlayBuilder builder, Switch_UISkillSheet screen) {
            HeroPC hero = GameMasterScript.heroPCActor;
            CharacterJobData job = hero.myJob;
            bool jobMastered = hero.HasMasteredJob(job);

            // Innate row: a summary cell (reads the whole bonus text, infusions included) then one
            // cell per defined passive tier. Tier 2 unlocks at 1000 JP spent in the job, tier 3 at
            // job mastery — mirroring the game's GetStringForJobInnateBonuses gating.
            builder.StartRow("innate");
            builder.AddItem(
                ControlId.Structural("skill:innate"),
                new NodeVtable {
                    Label = ctx => ctx.Message.Fragment(ModStrings.InnateBonuses),
                    OnClick = (ctx, mods) => ReadInnate(ctx, screen),
                    OnReadInfo = ctx => ReadInnate(ctx, screen),
                }
            );
            AddTier(builder, 1, job.BonusDescription1, locked: false);
            AddTier(builder, 2, job.BonusDescription2, locked: hero.jobJPspent[(int)job.jobEnum] < 1000f);
            AddTier(builder, 3, job.BonusDescription3, locked: !jobMastered);
            builder.EndRow();

            // Learn row: a label cell, then one cell per buyable ability. Mirror the game's
            // FillJobAbilitiesList filter: innates are passive/automatic, and post-mastery skills only
            // appear once the job is mastered.
            builder.StartRow("learn");
            builder.AddLabel(
                ControlId.Structural("skill:learn"),
                ctx => ctx.Message.Fragment(ModStrings.LearnAbilityRow)
            );
            foreach (JobAbility ja in job.JobAbilities) {
                if (ja.ability == null || ja.innate || (ja.postMasteryAbility && !jobMastered)) {
                    continue;
                }

                JobAbility ability = ja;
                builder.AddItem(
                    ControlId.Structural("skill:abil:" + ability.ability.refName),
                    new NodeVtable {
                        Label = ctx => AbilityLabel(ctx.Message, ability),
                        // Confirm is the primary action: learn (or master) the ability.
                        OnClick = (ctx, mods) => LearnAbility(ctx, ability),
                        // Read-info (K) is the full tooltip: cost-to-learn, description, repeat-buy.
                        OnReadInfo = ctx =>
                            ctx.Message.Fragment(GameLabelReader.Clean(ability.GetInformationForTooltip())),
                    }
                );
            }

            builder.EndRow();
        }

        // One passive-tier cell, skipped entirely when the job does not define that tier (empty
        // description). The label reads the game's own tier header (which already states the JP /
        // mastery requirement when locked) and the tier's effect text, prefixed with "locked" when
        // its gate is unmet so the state is explicit rather than inferred.
        private static void AddTier(IOverlayBuilder builder, int tier, string description, bool locked) {
            if (string.IsNullOrEmpty(description)) {
                return;
            }

            builder.AddItem(
                ControlId.Structural("skill:tier:" + tier),
                new NodeVtable {
                    Label = ctx => {
                        if (locked) {
                            ctx.Message.Fragment(ModStrings.Locked);
                        }

                        ctx.Message.Fragment(GameLabelReader.Clean(TierHeader(tier, locked)));
                        ctx.Message.Fragment(
                            GameLabelReader.Clean(CustomAlgorithms.ParseRichText(description, false))
                        );
                    },
                }
            );
        }

        // The game's own tier header string; when locked it appends the requirement clause, exactly
        // as GetStringForJobInnateBonuses builds it ("Tier 2 Passive Bonus (Spend 1000+ JP)").
        private static string TierHeader(int tier, bool locked) {
            switch (tier) {
                case 1:
                    return StringManager.GetString("ui_job_innate_bonus1");
                case 2:
                    return StringManager.GetString("ui_job_innate_bonus2")
                        + (locked ? " " + StringManager.GetString("ui_job_bonus2_jp_req") : "");
                case 3:
                    return StringManager.GetString("ui_job_innate_bonus3")
                        + (locked ? " " + StringManager.GetString("ui_job_bonus3_jp_req") : "");
                default:
                    return "";
            }
        }

        // Spoken status of a buyable ability, mirroring the game's eligibility coloring
        // (Action_CheckJobAbilityEligibility): owned/mastered vs. cost-to-learn vs. unaffordable.
        private static void AbilityLabel(MessageBuilder message, JobAbility ja) {
            HeroPC hero = GameMasterScript.heroPCActor;
            AbilityScript ab = ja.ability;

            message.Fragment(GameLabelReader.Clean(ab.abilityName));

            bool isMastered = ja.masterCost > 0 && hero.myAbilities.HasMasteredAbility(ab);
            bool owned = hero.myAbilities.HasAbility(ab);

            if (isMastered) {
                message.ListItem(ModStrings.Mastered);
            } else if (owned && !ja.repeatBuyPossible) {
                message.ListItem(ModStrings.Learned);
                // Owned but still masterable: the game greys it, but confirm will master it.
                if (ja.masterCost > 0) {
                    message.ListItem(ModStrings.CanMasterFor(ja.masterCost));
                    if (hero.GetCurJP() < ja.masterCost) {
                        message.Fragment(ModStrings.NotEnough);
                    }
                }
            } else {
                int cost = hero.GetCostForAbilityBecauseWeDoStuffIfWeArentInOurStartingJob(ja);
                message.ListItem(ModStrings.Costs(cost));
                if (hero.GetCurJP() < cost) {
                    message.Fragment(ModStrings.NotEnough);
                }

                if (ja.repeatBuyPossible) {
                    message.ListItem(ModStrings.Repeatable);
                }
            }

            if (ab.toggled) {
                message.ListItem(ModStrings.ToggledOn);
            }
        }

        // Learn or master the ability via the game's own TryLearnAbility (which picks the master
        // path automatically when the ability is owned, masterable, and not yet mastered, spends the
        // JP, and refreshes the screen). We pre-check so the spoken reason is specific, and play the
        // learn cue ourselves (TryLearnAbility does not — that lives in the game's click animation).
        private static void LearnAbility(OverlayCtx ctx, JobAbility ja) {
            HeroPC hero = GameMasterScript.heroPCActor;
            AbilityScript ab = ja.ability;

            bool owned = hero.myAbilities.HasAbility(ab);
            bool canMaster = owned && ja.masterCost > 0 && !hero.myAbilities.HasMasteredAbility(ab);

            if (owned && !ja.repeatBuyPossible && !canMaster) {
                UIManagerScript.PlayCursorSound("Error");
                ctx.Message.Fragment(ModStrings.AlreadyLearned);
                return;
            }

            int cost = canMaster
                ? ja.masterCost
                : hero.GetCostForAbilityBecauseWeDoStuffIfWeArentInOurStartingJob(ja);
            if (hero.GetCurJP() < cost) {
                UIManagerScript.PlayCursorSound("Error");
                ctx.Message.Fragment(ModStrings.NotEnoughJp);
                return;
            }

            if (hero.TryLearnAbility(ja)) {
                UIManagerScript.PlayCursorSound("Ultra Learn");
                ctx.Message.Fragment(canMaster ? ModStrings.Mastered : ModStrings.Learned);
                ctx.Message.Fragment(GameLabelReader.Clean(ab.GetNameForUI()));
                ctx.Message.ListItem(ModStrings.JpRemaining((int)hero.GetCurJP()));
            } else {
                // Pre-checks passed but the game still refused (e.g. wrong job) — it logs the reason.
                UIManagerScript.PlayCursorSound("Error");
                ctx.Message.Fragment(ModStrings.CantLearn);
            }
        }

        private static void ReadInnate(OverlayCtx ctx, Switch_UISkillSheet screen) {
            string raw = (string)InnateText.Invoke(screen, null);
            ctx.Message.Fragment(GameLabelReader.Clean(raw));
        }
    }
}
