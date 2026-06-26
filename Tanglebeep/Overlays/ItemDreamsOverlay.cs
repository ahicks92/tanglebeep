using System;
using System.Collections.Generic;
using Tanglebeep.Focus;
using Tanglebeep.Speech;
using Tanglebeep.Ui;
using Tanglebeep.Ui.Graph;
using UIObject = UIManagerScript.UIObject;

namespace Tanglebeep.Overlays {
    /// <summary>
    /// The Item Dreams window (the game's <c>ItemWorldUIScript</c> / Dreamcaster "Item World"
    /// interface), reached by talking to the Dreamcaster and choosing to dream an item. It enchants a
    /// piece of gear: pick the item, pick an Item World Orb, stake an optional gold/JP tribute (which
    /// buys a chance at an extra mod), then enter the generated dream dungeon to apply it.
    ///
    /// <para>The game models it as one backing list (<c>playerItemList</c>) refilled per
    /// <c>menuState</c> — equippable gear in SELECTITEM, orbs in SELECTORB — shown through a 12-button
    /// scroll window. We ignore the window and re-present the whole list as one owned set of rows,
    /// built fresh every tick, exactly like <see cref="ShopOverlay"/>:
    /// <list type="bullet">
    /// <item>a <b>header</b> naming the stage (and, in SELECTORB, the chosen item — whose item-world
    /// readout is on its <c>k</c> key);</item>
    /// <item>one <b>row per list entry</b> — confirm selects it, <c>k</c> reads its tooltip; orb rows
    /// also speak the orb's compatibility with the chosen item;</item>
    /// <item>once an orb is picked, an <b>actions</b> section — a tribute slider, enter dream, modify
    /// item (mod removal), exit.</item>
    /// </list></para>
    ///
    /// <para><b>Sub-identified</b> so focus resets as the player crosses stages (item → orb → orb
    /// chosen), mirroring how the game itself moves focus on each transition.</para>
    ///
    /// <para><b>Driving selection:</b> the game's <c>SelectItem</c> is keyed to a button index + scroll
    /// offset, not to an item, so (as in the shop) we align the game's window and cursor to our target
    /// before calling it. Every action goes through the game's own handlers (<c>SelectItem</c>,
    /// <c>TryAdjustGoldAmount</c>, <c>TryEnterItemWorld</c>, <c>ModifyItem</c>), so its validation,
    /// cursor sounds, and <c>GameLogScript</c> lines fire; we add speech only for reads, the slider,
    /// and local errors. The enter/modify confirm flows hand off to a dialog, which
    /// <see cref="DialogOverlay"/> owns — hence the <c>dialogBoxOpen</c> yield in
    /// <see cref="Handler"/>.</para>
    /// </summary>
    internal sealed class ItemDreamsOverlay : IUiOverlay, ISubIdentified {
        public OverlayId Id => OverlayId.ItemDreams;

        public OverlayResult Handler() {
            // Yield to the dialog overlay for the no-tribute / modify confirm dialogs the game pops.
            if (UIManagerScript.dialogBoxOpen) {
                return OverlayResult.Inactive;
            }

            return Open() ? OverlayResult.Active(this) : OverlayResult.Inactive;
        }

        private static bool Open() {
            return ItemWorldUIScript.itemWorldInterfaceOpen
                && ItemWorldUIScript.itemWorldInterface != null
                && ItemWorldUIScript.itemWorldInterface.activeInHierarchy;
        }

        // Stage + chosen-orb identity. A change resets focus to the start node and re-announces, so the
        // player lands on the fresh content after the game advances (or backs out) a stage. The tribute
        // is deliberately excluded — nudging the slider must not re-fire the "just opened" reset.
        public string SubIdentity() {
            if (ItemWorldUIScript.menuState != ItemWorldMenuState.SELECTORB) {
                return "item";
            }

            Item orb = ItemWorldUIScript.orbSelected;
            return orb == null ? "orb" : "orb:" + orb.actorUniqueID;
        }

        public void Build(IOverlayBuilder builder) {
            builder.CaptureInput();

            if (!Open()) {
                return;
            }

            bool selectingOrb = ItemWorldUIScript.menuState == ItemWorldMenuState.SELECTORB;
            BuildHeader(builder, selectingOrb);
            BuildList(builder, selectingOrb);

            if (selectingOrb && ItemWorldUIScript.orbSelected != null) {
                BuildActions(builder);
            }
        }

        // --- Header ----------------------------------------------------------------------------

        private static void BuildHeader(IOverlayBuilder builder, bool selectingOrb) {
            HeroPC hero = GameMasterScript.heroPCActor;
            int orbCount = hero.myInventory.GetItemQuantity("orb_itemworld");
            int gold = hero.GetMoney();
            int jp = (int)hero.jobJP[(int)hero.myJob.jobEnum];
            Item item = ItemWorldUIScript.itemSelected;

            builder.AddLabel(
                ControlId.Structural("dream:header"),
                ctx => {
                    if (selectingOrb && item != null) {
                        ctx.Message.Fragment(ModStrings.DreamSelectOrb(GameLabelReader.Clean(item.displayName)));
                    } else {
                        ctx.Message.Fragment(ModStrings.DreamSelectItem);
                    }

                    ctx.Message.ListItem(ModStrings.DreamOrbCount(orbCount));
                    ctx.Message.ListItem(ModStrings.Gold(gold));
                    ctx.Message.ListItem(ModStrings.Jp(jp));
                }
            );
        }

        // --- Item / orb list -------------------------------------------------------------------

        private static void BuildList(IOverlayBuilder builder, bool selectingOrb) {
            List<Item> list = ItemWorldUIScript.playerItemList;
            if (list == null || list.Count == 0) {
                builder.AddLabel(
                    ControlId.Structural("dream:empty"),
                    ctx => ctx.Message.Fragment(selectingOrb ? ModStrings.NoDreamOrbs : ModStrings.NoDreamItems)
                );
                return;
            }

            foreach (Item entry in list) {
                Item captured = entry;
                int uid = entry.actorUniqueID;

                // Distinct row key per entry so up/down lands on each (see ShopOverlay/InventoryOverlay).
                builder.StartRow("dream:row:" + uid);
                builder.AddItem(
                    ControlId.Structural("dream:item:" + uid),
                    new NodeVtable {
                        Label = ctx => EntryLabel(ctx.Message, captured, selectingOrb),
                        OnClick = (ctx, mods) => Select(captured),
                        OnReadInfo = ctx => ctx.Message.Fragment(EntryInfo(captured, selectingOrb)),
                    }
                );
                builder.EndRow();
            }
        }

        private static void EntryLabel(MessageBuilder message, Item item, bool selectingOrb) {
            if (item.favorite) {
                message.Fragment(ModStrings.Favorited);
            }

            if (item.vendorTrash) {
                message.Fragment(ModStrings.MarkedTrash);
            }

            message.Fragment(GameLabelReader.Clean(item.displayName));

            if (selectingOrb) {
                string compat = CompatibilityWord(item);
                if (compat != null) {
                    message.ListItem(compat);
                }
            }
        }

        // For an orb, the compatibility verdict against the chosen item — using the game's own reason
        // strings. POSSIBLE adds nothing (the common case); the rest name why it can't be applied.
        private static string CompatibilityWord(Item orb) {
            switch (ItemWorldUIScript.IsItemCompatibleWithOrb(orb)) {
                case MagicModCompatibility.ALREADY_HAS_MOD:
                    return GameLabelReader.Clean(StringManager.GetString("mod_compatibility_existing"));
                case MagicModCompatibility.WRONG_ITEM_TYPE:
                    return GameLabelReader.Clean(StringManager.GetString("mod_compatibility_slot"));
                case MagicModCompatibility.NO_MORE_MODS_POSSIBLE:
                    return GameLabelReader.Clean(StringManager.GetString("mod_compatibility_full"));
                case MagicModCompatibility.CONFLICTING_MOD:
                    return GameLabelReader.Clean(StringManager.GetString("mod_compatibility_conflict"));
                default:
                    return null;
            }
        }

        // The k readout: a gear item's item-world description (what an enchant would do), or an orb's
        // full tooltip. Items in SELECTITEM are all equipment, but fall back defensively.
        private static string EntryInfo(Item item, bool selectingOrb) {
            if (!selectingOrb && item is Equipment equipment) {
                return GameLabelReader.Clean(equipment.GetItemWorldDescription());
            }

            return GameLabelReader.Clean(item.GetInformationForTooltip());
        }

        // Select an item/orb through the game's own SelectItem, first aligning the game's scroll window
        // + cursor to it (the call is keyed to button index + offset, not the item). Mirrors
        // ShopOverlay.CommitSingle. The game advances the stage (item) or reveals the actions (orb).
        private static void Select(Item item) {
            List<Item> list = ItemWorldUIScript.playerItemList;
            int k = list.IndexOf(item);
            if (k < 0) {
                return;
            }

            int window = ItemWorldUIScript.itemListButtons.Length;
            int offset = Math.Min(k, Math.Max(0, list.Count - window));
            int button = k - offset;

            UIManagerScript.SetListOffset(offset);
            UIManagerScript.UpdateItemWorldList(ItemWorldUIScript.menuState == ItemWorldMenuState.SELECTORB);
            UIManagerScript.ChangeUIFocusAndAlignCursor(ItemWorldUIScript.itemListButtons[button]);
            ItemWorldUIScript.singleton.SelectItem(button);
        }

        // --- Actions (an orb is chosen) --------------------------------------------------------

        private static void BuildActions(IOverlayBuilder builder) {
            // The tribute slider lives on its own row, only while the game shows it (the item can take
            // more mods). It must NOT share a row with the action buttons: a slider intercepts left/right
            // to adjust its value, so a button to its side would be unreachable by horizontal nav.
            // Confirm on it is inert (an explicit no-op handler) so it never falls through to the game.
            if (ItemWorldUIScript.itemGoldSlider != null
                && ItemWorldUIScript.itemGoldSlider.gameObject.activeSelf) {
                builder.StartRow("dream:tribute");
                builder.AddItem(
                    ControlId.Structural("dream:tribute"),
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment(TributeLabel()),
                        OnHorizontalAdjust = AdjustTribute,
                        OnClick = (ctx, mods) => { },
                    }
                );
                builder.EndRow();
            }

            // Enter / modify appear only when the game enabled them; exit is always available (Escape
            // also backs out via the game's CancelPressed).
            builder.StartRow("dream:actions");
            ControlId enter = AddGameAction(builder, "enter", ItemWorldUIScript.itemWorldEnter, ModStrings.EnterDreamAction, EnterDream);
            ControlId modify = AddGameAction(builder, "modify", ItemWorldUIScript.itemWorldModify, ModStrings.ModifyItemAction, Modify);
            ControlId exit = AddAction(builder, "exit", ItemWorldUIScript.itemWorldExit, ModStrings.ExitDreamAction, Exit);
            builder.EndRow();

            // Land focus on the primary action when the orb is chosen (the stage's start node, used on
            // the subidentity reset), so the player arrives at "enter dream" rather than back on the
            // header — mirroring how the game moves focus to its Enter button.
            builder.SetStart(enter ?? modify ?? exit);
        }

        // A game-backed action button shown only while the game has it active; prefer its own caption.
        // Returns the added node's id, or null when the button is hidden.
        private static ControlId AddGameAction(
            IOverlayBuilder builder, string idPart, UIObject button, string fallback, Action onClick) {
            if (button == null || button.gameObj == null || !button.gameObj.activeSelf) {
                return null;
            }

            return AddAction(builder, idPart, button, fallback, onClick);
        }

        private static ControlId AddAction(
            IOverlayBuilder builder, string idPart, UIObject button, string fallback, Action onClick) {
            string label = button != null ? GameLabelReader.ReadLabel(button) : null;
            if (string.IsNullOrEmpty(label)) {
                label = fallback;
            }

            ControlId id = ControlId.Structural("dream:act:" + idPart);
            builder.AddItem(
                id,
                new NodeVtable {
                    Label = ctx => ctx.Message.Fragment(label),
                    OnClick = (ctx, mods) => onClick(),
                }
            );
            return id;
        }

        private static string TributeLabel() {
            int max = ItemWorldUIScript.GetEnchantMaxCost(forceGold: false);
            int tribute = ItemWorldUIScript.goldTribute;
            int chance = max > 0 ? (int)((float)tribute / max * 100f) : 0;
            return ModStrings.DreamTribute(tribute, ItemWorldUIScript.lucidSkillOrbSelected, chance);
        }

        // Step the tribute off its live value: a small step is 10% of the max cost, a large (Shift)
        // step jumps to the min/max. TryAdjustGoldAmount clamps to what the player can actually afford
        // and writes goldTribute; we speak the result.
        private static void AdjustTribute(OverlayCtx ctx, int sign, bool large) {
            int max = ItemWorldUIScript.GetEnchantMaxCost(forceGold: false);
            if (max <= 0) {
                ctx.Message.Fragment(TributeLabel());
                return;
            }

            int target;
            if (large) {
                target = sign < 0 ? 0 : max;
            } else {
                int step = Math.Max(1, max / 10);
                target = ItemWorldUIScript.goldTribute + sign * step;
            }

            target = Math.Max(0, Math.Min(max, target));
            ItemWorldUIScript.TryAdjustGoldAmount((float)target / max);
            ctx.Message.Fragment(TributeLabel());
        }

        private static void EnterDream() {
            // Begins the dream (closing the window) or pops the no-tribute confirm dialog, which the
            // dialog overlay owns; either way the game's own path narrates the result.
            ItemWorldUIScript.singleton.TryEnterItemWorld(0);
        }

        private static void Modify() {
            // Opens the dreamcaster_modify dialog (mod removal); handed off to the dialog overlay.
            ItemWorldUIScript.singleton.ModifyItem(0);
        }

        private static void Exit() {
            ItemWorldUIScript.singleton.CloseItemWorldInterface(0);
        }
    }
}
