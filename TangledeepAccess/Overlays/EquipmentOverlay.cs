using System.Collections.Generic;
using HarmonyLib;
using TangledeepAccess.Focus;
using TangledeepAccess.Speech;
using TangledeepAccess.Ui;
using TangledeepAccess.Ui.Graph;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// The gear screen (<c>Switch_UIEquipmentScreen</c>, the "E" tab). The game models it as a
    /// scrolling item column plus an equipped-gear panel, two view tabs (Current Gear / Gear
    /// Bonuses), and three button groups (category, rarity filter, sort). We ignore that topology —
    /// including the tab, since we surface both the gear and the bonuses at once — and re-present it
    /// as one owned grid, built fresh every tick:
    ///
    /// <list type="bullet">
    /// <item>an <b>equipped</b> row — the four weapon-hotbar slots (the active one marked) then
    /// offhand, armor, the two accessories, and the emblem; confirm unequips a slot;</item>
    /// <item>a <b>bonuses</b> row — one cell per line of the game's own aggregated gear-bonus text;</item>
    /// <item>a <b>category</b> row and a <b>filter</b> row (rarity/favorites) and a <b>sort</b> row,
    /// each driving the game's own button handlers so our item list (read from the live column)
    /// reflects them;</item>
    /// <item>one <b>item</b> row per equippable item in the bag — the item (confirm equips to the
    /// best slot, read-info reads the tooltip, Ctrl+K compares against the gear it would replace)
    /// then its equip/pair/favorite/trash/drop action cells.</item>
    /// </list>
    ///
    /// <para>The hero's HP/resources/JP/XP/gold are NOT here: like on every full-screen panel, they
    /// are persistent PlayerHUD chrome, not part of this screen.</para>
    ///
    /// <para><b>Data source:</b> the hero's live <c>myEquipment</c> for the equipped panel, and the
    /// screen's own <c>itemColumn.listHeldObjects</c> (the complete filtered+sorted list, which holds
    /// only UNequipped gear) for the item rows. Read live each build; never cached.</para>
    ///
    /// <para><b>Active weapon:</b> we do not offer "switch active weapon" — the player does that with
    /// the weapon F-keys / brackets — but we mark which hotbar slot is active so the state is audible.</para>
    /// </summary>
    internal sealed class EquipmentOverlay : IUiOverlay {
        // The complete filtered+sorted list backing the item column; private on the column type.
        private static readonly AccessTools.FieldRef<Switch_UIButtonColumn, List<ISelectableUIObject>>
            HeldObjects = AccessTools.FieldRefAccess<Switch_UIButtonColumn, List<ISelectableUIObject>>(
                "listHeldObjects"
            );

        // The screen's currently-selected gear category; private on the screen type.
        private static readonly AccessTools.FieldRef<Switch_UIEquipmentScreen, GearFilters> CategoryField =
            AccessTools.FieldRefAccess<Switch_UIEquipmentScreen, GearFilters>("filterSelectedCategory");

        // Button labels reuse the game's own strings, keyed exactly as the screen keys them.
        private static readonly Dictionary<GearFilters, string> CategoryKeys =
            new Dictionary<GearFilters, string> {
                { GearFilters.VIEWALL, "item_filters_view_all" },
                { GearFilters.WEAPON, "eq_slot_weapon_plural" },
                { GearFilters.OFFHAND, "eq_slot_offhand_plural" },
                { GearFilters.ARMOR, "eq_slot_armor_plural" },
                { GearFilters.ACCESSORY, "eq_slot_accessory_plural" },
            };

        private static readonly Dictionary<GearFilters, string> FilterKeys =
            new Dictionary<GearFilters, string> {
                { GearFilters.COMMON, "misc_rarity_0" },
                { GearFilters.MAGICAL, "misc_rarity_2" },
                { GearFilters.LEGENDARY, "misc_rarity_4b" },
                { GearFilters.GEARSET, "misc_rarity_5" },
                { GearFilters.FAVORITES, "item_filters_favorites" },
            };

        private static readonly Dictionary<InventorySortTypes, string> SortKeys =
            new Dictionary<InventorySortTypes, string> {
                { InventorySortTypes.ITEMTYPE, "item_sort_type_type" },
                { InventorySortTypes.ALPHA, "item_sort_type_alpha" },
                { InventorySortTypes.VALUE, "item_sort_type_value" },
                { InventorySortTypes.RANK, "item_sort_type_rank" },
                { InventorySortTypes.RARITY, "item_sort_type_rarity" },
            };

        // Ctrl+K comparison state: the item last compared, and which equipped slot we compared it
        // against, so repeating Ctrl+K on the same item cycles the compared slot (weapon vs offhand,
        // accessory 1 vs 2). A live reference, not cached game data — re-read on every compare.
        private static Item _compareItem;
        private static bool _compareAlt;

        public OverlayId Id => OverlayId.Equipment;

        public OverlayResult Handler() {
            return Screen() != null ? OverlayResult.Active(this) : OverlayResult.Inactive;
        }

        /// <summary>The live equipment screen if it is the open full-screen UI, else null.</summary>
        private static Switch_UIEquipmentScreen Screen() {
            UIManagerScript ums = UIManagerScript.singletonUIMS;
            if (ums == null) {
                return null;
            }

            if (!(ums.currentFullScreenUI is Switch_UIEquipmentScreen eq)) {
                return null;
            }

            return eq.gameObject.activeInHierarchy ? eq : null;
        }

        public void Build(IOverlayBuilder builder) {
            builder.CaptureInput();

            Switch_UIEquipmentScreen screen = Screen();
            if (screen == null) {
                return;
            }

            BuildEquippedRow(builder);
            BuildBonusRow(builder, screen);
            BuildCategoryRow(builder, screen);
            BuildFilterRow(builder, screen);
            BuildSortRow(builder, screen);
            BuildItemRows(builder, screen);
        }

        // --- Equipped row ----------------------------------------------------------------------

        private static void BuildEquippedRow(IOverlayBuilder builder) {
            EquipmentBlock eq = GameMasterScript.heroPCActor.myEquipment;
            int active = UIManagerScript.GetActiveWeaponSlot();

            builder.StartRow("equipped");
            builder.AddLabel(
                ControlId.Structural("eq:row:equipped"),
                ctx => ctx.Message.Fragment(ModStrings.EquippedGearRow)
            );

            // The four weapon-hotbar slots. A slot holding nothing or bare fists reads "empty".
            for (int i = 0; i < 4; i++) {
                Weapon w = UIManagerScript.hotbarWeapons[i];
                bool empty = w == null || eq.IsDefaultWeapon(w, onlyActualFists: true);
                AddSlotCell(builder, "weapon" + i, ModStrings.WeaponSlot(i + 1), empty ? null : w, i == active && !empty, weaponHotbar: true, i);
            }

            AddSlotCell(builder, "offhand", ModStrings.OffhandSlot, eq.GetOffhand(), active: false, weaponHotbar: false, -1);
            AddSlotCell(builder, "armor", ModStrings.ArmorSlot, eq.GetArmor(), active: false, weaponHotbar: false, -1);
            AddSlotCell(builder, "acc1", ModStrings.AccessorySlot(1), eq.GetEquipmentInSlot(EquipmentSlots.ACCESSORY), active: false, weaponHotbar: false, -1);
            AddSlotCell(builder, "acc2", ModStrings.AccessorySlot(2), eq.GetEquipmentInSlot(EquipmentSlots.ACCESSORY2), active: false, weaponHotbar: false, -1);
            AddSlotCell(builder, "emblem", ModStrings.EmblemSlot, eq.GetEmblem(), active: false, weaponHotbar: false, -1);

            builder.EndRow();
        }

        private static void AddSlotCell(
            IOverlayBuilder builder,
            string idPart,
            string slotName,
            Equipment item,
            bool active,
            bool weaponHotbar,
            int hotbarIdx
        ) {
            var vtable = new NodeVtable {
                Label = ctx => {
                    ctx.Message.Fragment(slotName);
                    ctx.Message.Fragment(item == null ? ModStrings.EmptySlot : GameLabelReader.Clean(item.displayName));
                    if (active) {
                        ctx.Message.Fragment(ModStrings.ActiveWeapon);
                    }
                },
            };

            ControlId id = ControlId.Structural("eq:slot:" + idPart);
            if (item == null) {
                builder.AddItem(id, vtable);
                return;
            }

            // Confirm unequips; read-info reads the tooltip. Plus the row-wide favorite/trash keys.
            Equipment captured = item;
            vtable.OnClick = (ctx, mods) => UnequipSlot(ctx, captured, weaponHotbar, hotbarIdx);
            vtable.OnReadInfo = ctx => ctx.Message.Fragment(GameLabelReader.Clean(captured.GetInformationForTooltip()));
            AddRowCell(builder, id, item, vtable);
        }

        // Unequip the item in a slot. A weapon-hotbar slot: if it is the active (truly equipped) one,
        // unequip and clear it from the actives; if it is only a carried alternate, just remove it
        // from the hotbar. Any other slot unequips directly.
        private static void UnequipSlot(OverlayCtx ctx, Equipment item, bool weaponHotbar, int hotbarIdx) {
            HeroPC hero = GameMasterScript.heroPCActor;

            if (weaponHotbar && UIManagerScript.GetActiveWeaponSlot() != hotbarIdx) {
                // A carried (non-active) hotbar weapon: just drop it from the hotbar. No game-log
                // line fires for this, so we announce it ourselves.
                UIManagerScript.hotbarWeapons[hotbarIdx] = null;
                UIManagerScript.UpdateFullScreenUIContent();
                ctx.Message.Fragment(GameLabelReader.Clean(item.displayName)).Fragment(ModStrings.Unequipped);
                return;
            }

            // The active weapon, or any other slot: a real unequip. The game writes "Unequipped X.",
            // which the game-log speech path announces, so we add no message of our own.
            hero.Unequip(item, showText: true);
            if (weaponHotbar) {
                UIManagerScript.RemoveWeaponFromActives(item as Weapon);
            }

            UIManagerScript.UpdateFullScreenUIContent();
        }

        // --- Bonuses row -----------------------------------------------------------------------

        private static void BuildBonusRow(IOverlayBuilder builder, Switch_UIEquipmentScreen screen) {
            string[] lines = BonusLines(screen);

            builder.StartRow("bonuses");
            builder.AddLabel(
                ControlId.Structural("eq:row:bonuses"),
                ctx => {
                    ctx.Message.Fragment(ModStrings.GearBonusesRow);
                    if (lines.Length == 0) {
                        ctx.Message.Fragment(ModStrings.None);
                    }
                }
            );

            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];
                builder.AddLabel(ControlId.Structural("eq:bonus:" + i), ctx => ctx.Message.Fragment(line));
            }

            builder.EndRow();
        }

        // The game's own aggregated bonus text (built in UpdateGearBonusText), one entry per line.
        // Each game line is a "* ..." bullet; strip the bullet and the rich-text tags for speech.
        private static string[] BonusLines(Switch_UIEquipmentScreen screen) {
            string raw = screen.txt_GearBonuses != null ? screen.txt_GearBonuses.text : null;
            if (string.IsNullOrEmpty(raw)) {
                return new string[0];
            }

            var list = new List<string>();
            foreach (string piece in raw.Split('\n')) {
                // Clean returns null for a blank/all-markup line (the text has a trailing newline).
                string clean = GameLabelReader.Clean(piece);
                if (string.IsNullOrEmpty(clean)) {
                    continue;
                }

                clean = clean.TrimStart('*', ' ');
                if (!string.IsNullOrEmpty(clean)) {
                    list.Add(clean);
                }
            }

            return list.ToArray();
        }

        // --- Category / filter / sort rows -----------------------------------------------------

        private static void BuildCategoryRow(IOverlayBuilder builder, Switch_UIEquipmentScreen screen) {
            builder.StartRow("category");
            builder.AddLabel(
                ControlId.Structural("eq:row:category"),
                ctx => {
                    ctx.Message.Fragment(ModStrings.CategoryRow);
                    ctx.Message.ListItem(CategoryLabel(CategoryField(screen)));
                }
            );

            foreach (GearFilters g in screen.list_categoryTypes) {
                GearFilters cat = g;
                string label = CategoryLabel(cat);
                builder.AddClickable(
                    ControlId.Structural("eq:cat:" + cat),
                    ctx => {
                        ctx.Message.Fragment(label);
                        if (CategoryField(screen) == cat) {
                            ctx.Message.Fragment(ModStrings.Selected);
                        }
                    },
                    (ctx, mods) => {
                        screen.OnSubmit_CategoryButton(new int[] { (int)cat }, Switch_InvItemButton.ELastInputSource.keyboard_or_gamepad);
                        ctx.Message.Fragment(label).Fragment(ModStrings.Selected);
                    }
                );
            }

            builder.EndRow();
        }

        private static void BuildFilterRow(IOverlayBuilder builder, Switch_UIEquipmentScreen screen) {
            builder.StartRow("filter");
            builder.AddLabel(ControlId.Structural("eq:row:filter"), ctx => ctx.Message.Fragment(ModStrings.FilterRow));

            foreach (GearFilters g in screen.list_filterTypes) {
                GearFilters f = g;
                string label = FilterLabel(f);
                builder.AddClickable(
                    ControlId.Structural("eq:filter:" + f),
                    ctx => {
                        ctx.Message.Fragment(label);
                        if (UIManagerScript.itemFilterTypes[(int)f]) {
                            ctx.Message.Fragment(ModStrings.On);
                        }
                    },
                    (ctx, mods) => {
                        screen.OnSubmit_FilterButton(new int[] { (int)f }, Switch_InvItemButton.ELastInputSource.keyboard_or_gamepad);
                        ctx.Message.Fragment(label).Fragment(UIManagerScript.itemFilterTypes[(int)f] ? ModStrings.On : ModStrings.Off);
                    }
                );
            }

            builder.EndRow();
        }

        private static void BuildSortRow(IOverlayBuilder builder, Switch_UIEquipmentScreen screen) {
            builder.StartRow("sort");
            builder.AddLabel(
                ControlId.Structural("eq:row:sort"),
                ctx => {
                    ctx.Message.Fragment(ModStrings.SortRow);
                    ctx.Message.ListItem(SortLabel(Switch_UIInventoryScreen.lastSortType));
                    if (!Switch_UIInventoryScreen.lastSortForward) {
                        ctx.Message.Fragment("reversed");
                    }
                }
            );

            foreach (InventorySortTypes s in screen.list_sortTypes) {
                InventorySortTypes sort = s;
                string label = SortLabel(sort);
                builder.AddClickable(
                    ControlId.Structural("eq:sort:" + sort),
                    ctx => {
                        ctx.Message.Fragment(label);
                        if (Switch_UIInventoryScreen.lastSortType == sort) {
                            ctx.Message.Fragment(ModStrings.Selected);
                        }
                    },
                    (ctx, mods) => {
                        UIManagerScript.singletonUIMS.SortPlayerInventory((int)sort);
                        screen.UpdateContent();
                        ctx.Message.Fragment(label).Fragment(ModStrings.Selected);
                        if (!Switch_UIInventoryScreen.lastSortForward) {
                            ctx.Message.Fragment("reversed");
                        }
                    }
                );
            }

            builder.EndRow();
        }

        private static string CategoryLabel(GearFilters cat) {
            return CategoryKeys.TryGetValue(cat, out string key) ? GameLabel(key) : cat.ToString().ToLowerInvariant();
        }

        private static string FilterLabel(GearFilters f) {
            return FilterKeys.TryGetValue(f, out string key) ? GameLabel(key) : f.ToString().ToLowerInvariant();
        }

        private static string SortLabel(InventorySortTypes sort) {
            return SortKeys.TryGetValue(sort, out string key) ? GameLabel(key) : sort.ToString().ToLowerInvariant();
        }

        private static string GameLabel(string key) {
            return GameLabelReader.Clean(StringManager.GetString(key));
        }

        // --- Item rows -------------------------------------------------------------------------

        private static void BuildItemRows(IOverlayBuilder builder, Switch_UIEquipmentScreen screen) {
            List<ISelectableUIObject> items =
                screen.itemColumn != null ? HeldObjects(screen.itemColumn) : null;

            if (items == null || items.Count == 0) {
                builder.AddLabel(ControlId.Structural("eq:empty"), ctx => ctx.Message.Fragment(ModStrings.None));
                return;
            }

            foreach (ISelectableUIObject selectable in items) {
                if (!(selectable is Equipment item)) {
                    continue;
                }

                int uid = item.actorUniqueID;
                builder.StartRow("item:" + uid);

                // Item identity: confirm equips to the best slot; read-info (K) reads the tooltip;
                // read-secondary (Ctrl+K) compares against the gear it would replace.
                AddRowCell(
                    builder,
                    ControlId.Structural("eq:item:" + uid),
                    item,
                    new NodeVtable {
                        Label = ctx => ItemSummary(ctx.Message, item),
                        OnClick = (ctx, mods) => EquipBest(ctx, item),
                        OnReadInfo = ctx => ctx.Message.Fragment(GameLabelReader.Clean(item.GetInformationForTooltip())),
                        OnReadSecondary = ctx => Compare(ctx, item),
                    }
                );

                AddEquipTargets(builder, item, uid);

                // Pair / unpair an offhand-capable item with the current main-hand weapon.
                if (item.IsOffhandable()) {
                    Equipment offhandable = item;
                    AddRowCell(
                        builder,
                        ControlId.Structural("eq:act:pair:" + uid),
                        item,
                        new NodeVtable {
                            Label = ctx => ctx.Message.Fragment(IsPaired(offhandable) ? ModStrings.UnpairAction : ModStrings.PairAction),
                            OnClick = (ctx, mods) => TogglePair(ctx, offhandable),
                        }
                    );
                }

                // Inventory actions, last (favorite, trash, drop).
                AddRowCell(
                    builder,
                    ControlId.Structural("eq:act:fav:" + uid),
                    item,
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment(item.favorite ? "unfavorite" : "favorite"),
                        OnClick = (ctx, mods) => ToggleFavorite(ctx, item),
                    }
                );
                AddRowCell(
                    builder,
                    ControlId.Structural("eq:act:trash:" + uid),
                    item,
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment(item.vendorTrash ? "untrash" : "trash"),
                        OnClick = (ctx, mods) => ToggleTrash(ctx, item),
                    }
                );
                AddRowCell(
                    builder,
                    ControlId.Structural("eq:act:drop:" + uid),
                    item,
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment(ModStrings.DropAction),
                        OnClick = (ctx, mods) => DropItem(ctx, item),
                    }
                );

                builder.EndRow();
            }
        }

        // The type-specific equip targets for an unequipped item: a weapon offers its four hotbar
        // slots (and an offhand slot if it can dual-wield), an accessory offers both accessory slots,
        // an offhand its slot, and armor/emblem a single slot.
        private static void AddEquipTargets(IOverlayBuilder builder, Equipment item, int uid) {
            if (item is Weapon) {
                for (int slot = 0; slot < 4; slot++) {
                    int target = slot;
                    AddRowCell(
                        builder,
                        ControlId.Structural("eq:act:wpn" + target + ":" + uid),
                        item,
                        new NodeVtable {
                            Label = ctx => ctx.Message.Fragment(ModStrings.EquipWeaponSlotAction(target + 1)),
                            OnClick = (ctx, mods) => EquipTo(ctx, item, target),
                        }
                    );
                }

                if (item.IsOffhandable()) {
                    AddRowCell(
                        builder,
                        ControlId.Structural("eq:act:off:" + uid),
                        item,
                        new NodeVtable {
                            Label = ctx => ctx.Message.Fragment(ModStrings.EquipOffhandAction),
                            OnClick = (ctx, mods) => EquipTo(ctx, item, 4),
                        }
                    );
                }

                return;
            }

            if (item is Accessory) {
                AddRowCell(
                    builder,
                    ControlId.Structural("eq:act:acc1:" + uid),
                    item,
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment(ModStrings.EquipAccessoryAction(1)),
                        OnClick = (ctx, mods) => EquipTo(ctx, item, 6),
                    }
                );
                AddRowCell(
                    builder,
                    ControlId.Structural("eq:act:acc2:" + uid),
                    item,
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment(ModStrings.EquipAccessoryAction(2)),
                        OnClick = (ctx, mods) => EquipTo(ctx, item, 7),
                    }
                );
                return;
            }

            if (item is Offhand) {
                AddRowCell(
                    builder,
                    ControlId.Structural("eq:act:off:" + uid),
                    item,
                    new NodeVtable {
                        Label = ctx => ctx.Message.Fragment(ModStrings.EquipOffhandAction),
                        OnClick = (ctx, mods) => EquipTo(ctx, item, 4),
                    }
                );
                return;
            }

            // Armor and emblem each have a single slot (5 and 8).
            int onlyTarget = item is Emblem ? 8 : 5;
            AddRowCell(
                builder,
                ControlId.Structural("eq:act:equip:" + uid),
                item,
                new NodeVtable {
                    Label = ctx => ctx.Message.Fragment(ModStrings.EquipAction),
                    OnClick = (ctx, mods) => EquipTo(ctx, item, onlyTarget),
                }
            );
        }

        private static void ItemSummary(MessageBuilder message, Item item) {
            if (item.favorite) {
                message.Fragment("favorite");
            }

            if (item.vendorTrash) {
                message.Fragment("trash");
            }

            message.Fragment(GameLabelReader.Clean(item.displayName));
        }

        // Add a cell to the current row, attaching the row-wide favorite/trash key handlers so F /
        // Minus act on this item from any cell in its row — exactly as the inventory overlay does.
        private static void AddRowCell(IOverlayBuilder builder, ControlId id, Item item, NodeVtable vtable) {
            vtable.OnMarkFavorite = ctx => ToggleFavorite(ctx, item);
            vtable.OnMarkTrash = ctx => ToggleTrash(ctx, item);
            builder.AddItem(id, vtable);
        }

        // --- Actions ---------------------------------------------------------------------------

        // Equip to the slot the game would guess, mirroring EquipItemAndGuessBestSlot.
        private static void EquipBest(OverlayCtx ctx, Equipment item) {
            EquipmentBlock eq = GameMasterScript.heroPCActor.myEquipment;
            if (item is Armor) {
                EquipTo(ctx, item, 5);
            } else if (item is Emblem) {
                EquipTo(ctx, item, 8);
            } else if (item is Accessory) {
                bool acc1Empty = eq.GetEquipmentInSlot(EquipmentSlots.ACCESSORY) == null;
                bool acc2Full = eq.GetEquipmentInSlot(EquipmentSlots.ACCESSORY2) != null;
                EquipTo(ctx, item, (acc1Empty || acc2Full) ? 6 : 7);
            } else if (item is Offhand) {
                EquipTo(ctx, item, 4);
            } else if (item is Weapon) {
                int slot = 0;
                for (int i = 0; i < 4; i++) {
                    Weapon w = UIManagerScript.hotbarWeapons[i];
                    if (w == null || eq.IsDefaultWeapon(w, onlyActualFists: true)) {
                        slot = i;
                        break;
                    }
                }

                EquipTo(ctx, item, slot);
            }
        }

        // Equip `item` into a target, mirroring the game's EquipItem slot codes: 0-3 weapon-hotbar
        // slots, 4 offhand, 5 armor, 6 accessory 1, 7 accessory 2, 8 emblem.
        private static void EquipTo(OverlayCtx ctx, Equipment item, int target) {
            HeroPC hero = GameMasterScript.heroPCActor;
            bool weaponSlot = target >= 0 && target <= 3;

            // The game pops a quest-fail confirm dialog here (dialog_confirm_changegear_failrumor)
            // when WouldChangingEquipmentFailQuest(); we deliberately suppress that dialog for now
            // and just equip. Either way the game writes "Equipped X!", which the game-log speech
            // path announces, so a successful equip needs no message of our own.
            if (weaponSlot) {
                // A weapon slot equips via the hotbar switch (which makes that slot active).
                // EquipOnlyIfValid can then report false because the weapon is already equipped, so we
                // do NOT treat its result as failure — mirroring the game's EquipItem, which ignores
                // it for weapon slots. Weapons never pass a turn either.
                UIManagerScript.AddWeaponToActiveSlot(item as Weapon, target);
                UIManagerScript.SwitchActiveWeaponSlot(target, silent: true);
                hero.myEquipment.EquipOnlyIfValid(item, SND.PLAY, 0, showText: true);
                UIManagerScript.UpdateFullScreenUIContent();
                return;
            }

            int subSlot = (target == 4 || target == 7) ? 1 : 0;
            if (!hero.myEquipment.EquipOnlyIfValid(item, SND.PLAY, subSlot, showText: true)) {
                UIManagerScript.PlayCursorSound("Error");
                ctx.Message.Fragment(ModStrings.CantEquip);
                return;
            }

            UIManagerScript.UpdateFullScreenUIContent();
            // A non-weapon equip in a dangerous area passes a turn; the game writes
            // "log_skipturn_equip" (announced via the game-log path) and force-closes this screen.
            UIManagerScript.singletonUIMS.CheckIfPassTurnFromEquipping();
        }

        private static bool IsPaired(Equipment item) {
            Weapon weapon = GameMasterScript.heroPCActor.myEquipment.GetWeapon();
            return weapon != null && item.CheckIfPairedWithSpecificItem(weapon);
        }

        // Pair (or unpair) an offhand-capable item with the current main-hand weapon, so equipping
        // that weapon auto-equips this as the offhand. Mirrors the game's PairWithWeapon.
        private static void TogglePair(OverlayCtx ctx, Equipment item) {
            Weapon weapon = GameMasterScript.heroPCActor.myEquipment.GetWeapon();
            if (weapon == null || weapon == item || !item.IsOffhandable()) {
                UIManagerScript.PlayCursorSound("Error");
                ctx.Message.Fragment(ModStrings.CantEquip);
                return;
            }

            string name = GameLabelReader.Clean(item.displayName);
            if (item.CheckIfPairedWithSpecificItem(weapon)) {
                weapon.RemovePairedItemByRef(item);
                item.RemovePairedItemByRef(weapon);
                UIManagerScript.PlayCursorSound("Cancel");
                ctx.Message.Fragment(name).Fragment(ModStrings.Unpaired);
            } else {
                item.PairWithItem(weapon, isMainHand: false, reciprocate: true);
                UIManagerScript.PlayCursorSound("Equip Item");
                ctx.Message.Fragment(name).Fragment(ModStrings.Paired);
            }

            UIManagerScript.UpdateFullScreenUIContent();
        }

        // Read the item's stat change versus the gear it would replace, cycling the compared slot
        // (weapon vs offhand, accessory 1 vs 2) when Ctrl+K is repeated on the same item.
        private static void Compare(OverlayCtx ctx, Equipment item) {
            if (_compareItem == item) {
                _compareAlt = !_compareAlt;
            } else {
                _compareItem = item;
                _compareAlt = false;
            }

            Equipment against = EquipmentBlock.FindEquipmentToCompareAgainst(item, _compareAlt);
            if (against == null) {
                ctx.Message.Fragment(ModStrings.NothingToCompare);
                return;
            }

            ctx.Message.Fragment(ModStrings.ComparedTo(GameLabelReader.Clean(against.displayName)));
            string delta = GameLabelReader.Clean(EquipmentBlock.CompareItems(against, item, item.slot));
            ctx.Message.Fragment(string.IsNullOrEmpty(delta) ? ModStrings.NoDifference : delta);
        }

        // Favorite and trash are mutually exclusive toggles (setting one clears the other). We flip
        // the flag directly, as the inventory overlay does, to avoid the game's button-lookup path.
        private static void ToggleFavorite(OverlayCtx ctx, Item item) {
            item.favorite = !item.favorite;
            if (item.favorite) {
                item.vendorTrash = false;
                UIManagerScript.PlayCursorSound("GetSparkle");
                ctx.Message.Fragment(ModStrings.Favorited);
            } else {
                UIManagerScript.PlayCursorSound("UITock");
                ctx.Message.Fragment(ModStrings.NoLongerFavorite);
            }
        }

        private static void ToggleTrash(OverlayCtx ctx, Item item) {
            item.vendorTrash = !item.vendorTrash;
            if (item.vendorTrash) {
                item.favorite = false;
                UIManagerScript.PlayCursorSound("UITick");
                ctx.Message.Fragment(ModStrings.MarkedTrash);
            } else {
                UIManagerScript.PlayCursorSound("UITock");
                ctx.Message.Fragment(ModStrings.NoLongerTrash);
            }
        }

        private static void DropItem(OverlayCtx ctx, Item item) {
            if (GameMasterScript.heroPCActor.myEquipment.IsDefaultWeapon(item, onlyActualFists: true)) {
                UIManagerScript.PlayCursorSound("Error");
                ctx.Message.Fragment(ModStrings.CantDrop);
                return;
            }

            string name = GameLabelReader.Clean(item.displayName);
            UIManagerScript.DropItemFromSheet(item);
            UIManagerScript.UpdateFullScreenUIContent();
            ctx.Message.Fragment(ModStrings.Dropped).Fragment(name);
        }
    }
}
