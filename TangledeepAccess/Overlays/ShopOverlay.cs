using System;
using System.Collections.Generic;
using TangledeepAccess.Focus;
using TangledeepAccess.Speech;
using TangledeepAccess.Ui;
using TangledeepAccess.Ui.Graph;

namespace TangledeepAccess.Overlays {
    /// <summary>
    /// The shop screen (<c>ShopUIScript</c>) — buying from / selling to a merchant, and the banker's
    /// withdraw/deposit, which the game models on the same screen via <c>ShopUIScript.shopState</c>
    /// (BUY/SELL). Unlike the inventory/equipment panels this is <b>not</b> a
    /// <c>currentFullScreenUI</c>; it lives under the PlayerHUD and liveness is
    /// <c>ShopUIScript.CheckShopInterfaceState()</c>. We ignore the game's 13-button scroll window and
    /// re-present the full <c>playerItemList</c> as one owned grid, built fresh every tick.
    ///
    /// <para><b>Acting on an item</b> goes through the game's own <c>InteractShopItem</c>, which is
    /// keyed to the focused button + scroll offset rather than to an item. So before invoking it we
    /// align the game's window+cursor to our target (<see cref="CommitSingle"/>): with a 13-wide
    /// window, <c>offset = min(k, max(0, count-13))</c> and the item rests at button <c>k - offset</c>
    /// (never assume 0 — short lists can't scroll, the last item of a long list sits at button 12).</para>
    ///
    /// <para><b>Two paths the game would resolve with a dialog, we keep in-shop instead:</b> a stack
    /// quantity opens our <see cref="ShopQuantityOverlay"/> auxiliary (committing through this node's
    /// <see cref="NodeVtable.OnAuxCommit"/>); a favorited single sell warns on plain Enter and sells
    /// on Ctrl+Enter (<see cref="ModInputKind.DangerousConfirm"/>). Both avoid handing off to
    /// <see cref="DialogOverlay"/> and losing the player's list position.</para>
    ///
    /// <para>Successful transactions are announced by the game's own <c>GameLogScript</c> lines via the
    /// game-log speech path (as equipping relies on), so the overlay speaks only the favorite warning,
    /// affordability errors, reads, and the quantity prompt.</para>
    /// </summary>
    internal sealed class ShopOverlay : IUiOverlay {
        // Ctrl+K comparison state, as in the equipment sheet: the last item compared and which slot we
        // compared against, so repeating on the same item cycles weapon vs offhand / accessory 1 vs 2.
        private static Item _compareItem;
        private static bool _compareAlt;

        public OverlayId Id => OverlayId.Shop;

        public OverlayResult Handler() {
            // Yield to the dialog overlay if a game dialog is up (our own paths never open one, so this
            // is the rare uncontrolled case — e.g. buying out the merchant). Otherwise own the shop.
            if (UIManagerScript.dialogBoxOpen) {
                return OverlayResult.Inactive;
            }

            return ShopUIScript.CheckShopInterfaceState()
                ? OverlayResult.Active(this)
                : OverlayResult.Inactive;
        }

        public void Build(IOverlayBuilder builder) {
            builder.CaptureInput();

            NPC npc = Merchant();
            List<Item> items = ShopUIScript.playerItemList;
            bool buying = ShopUIScript.shopState == ShopState.BUY;

            BuildHeader(builder, npc, buying, items);
            BuildSortRow(builder);
            BuildItemRows(builder, npc, buying, items);
        }

        // --- Header + sort ---------------------------------------------------------------------

        private static void BuildHeader(IOverlayBuilder builder, NPC npc, bool buying, List<Item> items) {
            string merchant = npc != null ? GameLabelReader.Clean(npc.displayName) : "shop";
            int count = items?.Count ?? 0;

            builder.AddLabel(
                ControlId.Structural("shop:header"),
                ctx => {
                    ctx.Message.Fragment(buying ? ModStrings.ShopBuying(merchant) : ModStrings.ShopSelling(merchant));
                    ctx.Message.ListItem(ModStrings.Gold(GameMasterScript.heroPCActor.GetMoney()));
                    ctx.Message.ListItem(ModStrings.ItemCount(count));
                }
            );
        }

        private static void BuildSortRow(IOverlayBuilder builder) {
            builder.StartRow("sort");
            builder.AddLabel(ControlId.Structural("shop:sortrow"), ctx => ctx.Message.Fragment(ModStrings.SortRow));
            AddSortButton(builder, ModStrings.SortByType, 3); // shopItemSortType.onSubmitValue
            AddSortButton(builder, ModStrings.SortByValue, 2); // shopItemSortValue.onSubmitValue
            builder.EndRow();
        }

        // Drive the game's own sort callback (exactly the shop sort buttons), then refresh so our next
        // rebuild reads the new order. The cursor stays on the button (stable key).
        private static void AddSortButton(IOverlayBuilder builder, string word, int sortValue) {
            builder.AddClickable(
                ControlId.Structural("shop:sort:" + sortValue),
                ctx => ctx.Message.Fragment(word),
                (ctx, mods) => {
                    UIManagerScript.singletonUIMS.SortPlayerInventory_UICallback(sortValue);
                    ShopUIScript.UpdateShop();
                    ctx.Message.Fragment(word);
                }
            );
        }

        // --- Item rows -------------------------------------------------------------------------

        private static void BuildItemRows(IOverlayBuilder builder, NPC npc, bool buying, List<Item> items) {
            if (items == null || items.Count == 0) {
                builder.AddLabel(ControlId.Structural("shop:empty"), ctx => ctx.Message.Fragment(ModStrings.None));
                return;
            }

            foreach (Item item in items) {
                Item captured = item;
                int uid = item.actorUniqueID;

                // Distinct row key per item so up/down always lands on the item cell (see InventoryOverlay).
                builder.StartRow("shopitem:" + uid);
                builder.AddItem(
                    ControlId.Structural("shop:item:" + uid),
                    new NodeVtable {
                        Label = ctx => ItemLabel(ctx.Message, captured, npc, buying),
                        OnClick = (ctx, mods) => Activate(ctx, captured, npc, buying, mods),
                        OnAuxCommit = ctx => CommitQuantity(captured, npc, buying, ctx.Arg),
                        OnReadInfo = ctx => ctx.Message.Fragment(GameLabelReader.Clean(captured.GetInformationForTooltip())),
                        OnReadSecondary = ctx => Compare(ctx, captured),
                        OnMarkFavorite = ctx => ToggleFavorite(ctx, captured),
                        OnMarkTrash = ctx => ToggleTrash(ctx, captured),
                    }
                );
                builder.EndRow();
            }
        }

        private static void ItemLabel(MessageBuilder message, Item item, NPC npc, bool buying) {
            if (item.favorite) {
                message.Fragment("favorite");
            }

            if (item.vendorTrash) {
                message.Fragment("trash");
            }

            message.Fragment(GameLabelReader.Clean(item.displayName));
            message.PushQuantity(item.GetQuantity());

            if (buying) {
                if (!IsBanker(npc)) {
                    int price = item.GetIndividualShopPrice();
                    message.Fragment(ModStrings.Gold(price));
                    if (!CanAfford(item)) {
                        message.Fragment(ModStrings.TooExpensive);
                    }
                }

                int owned = GameMasterScript.heroPCActor.myInventory.GetItemQuantity(item.actorRefName);
                if (owned > 0) {
                    message.Fragment(ModStrings.Owned(owned));
                }
            } else {
                message.Fragment(ModStrings.Gold(item.GetIndividualSalePrice(SaleMult(npc))));
            }
        }

        // --- Actions ---------------------------------------------------------------------------

        private static void Activate(OverlayCtx ctx, Item item, NPC npc, bool buying, Modifiers mods) {
            if (buying) {
                // Banker withdraw of a stack uses the quantity prompt; everything else buys one unit.
                if (IsBanker(npc) && item.GetQuantity() > 1) {
                    OpenQuantity(ctx, item);
                    return;
                }

                if (!IsBanker(npc) && !CanAfford(item)) {
                    UIManagerScript.PlayCursorSound("Error");
                    ctx.Message.Fragment(ModStrings.TooExpensive);
                    return;
                }

                CommitSingle(item);
                return;
            }

            // SELL / deposit.
            if (item.GetQuantity() > 1) {
                int max = MaxQuantity(item, npc);
                if (max < 1) {
                    UIManagerScript.PlayCursorSound("Error");
                    ctx.Message.Fragment(ModStrings.TooExpensive);
                    return;
                }

                OpenQuantity(ctx, item, max);
                return;
            }

            // A favorited single sell pops the game's confirm dialog; we replace that with Ctrl+Enter.
            if (item.favorite && !IsBanker(npc)) {
                if (!mods.Control) {
                    UIManagerScript.PlayCursorSound("Error");
                    ctx.Message.Fragment(ModStrings.FavoriteSellConfirm);
                    return;
                }

                SellFavoriteSingle(item, npc);
                return;
            }

            CommitSingle(item);
        }

        private static void OpenQuantity(OverlayCtx ctx, Item item, int max) {
            ctx.Controller.OpenAuxiliary(new ShopQuantityOverlay(max, GameLabelReader.Clean(item.displayName)));
        }

        private static void OpenQuantity(OverlayCtx ctx, Item item) {
            OpenQuantity(ctx, item, item.GetQuantity());
        }

        // Act on a single item through the game's own InteractShopItem by first aligning the game's
        // scroll window + cursor to it (the action is keyed to button index + offset, not the item).
        private static void CommitSingle(Item item) {
            List<Item> list = ShopUIScript.playerItemList;
            int k = list.IndexOf(item);
            if (k < 0) {
                return;
            }

            int window = ShopUIScript.shopItemButtonList.Length;
            int offset = Math.Min(k, Math.Max(0, list.Count - window));
            int button = k - offset;

            UIManagerScript.SetListOffset(offset);
            ShopUIScript.UpdateShop();
            UIManagerScript.ChangeUIFocusAndAlignCursor(ShopUIScript.shopItemButtonList[button]);
            ShopUIScript.singleton.InteractShopItem(button);
        }

        // Sell a single favorited item directly (Ctrl+Enter confirmed), bypassing InteractShopItem's
        // favorite-confirm dialog. Mirrors ShopUIScript.SellItem's caller setup.
        private static void SellFavoriteSingle(Item item, NPC npc) {
            GameMasterScript.gmsSingleton.SetTempGameData("merchantid", npc.actorUniqueID);
            ShopUIScript.SellItem(item, 1);
            ShopUIScript.UpdateShop();
        }

        // The quantity-prompt commit, run against this item's live state on the parent rebuild. Mirrors
        // DialogEventsScript.ConfirmQuantityInDialog's sell / deposit / withdraw cases.
        private static void CommitQuantity(Item item, NPC npc, bool buying, int qty) {
            if (qty < 1) {
                return;
            }

            HeroPC hero = GameMasterScript.heroPCActor;
            GameMasterScript.gmsSingleton.SetTempGameData("merchantid", npc.actorUniqueID);

            if (!buying) {
                Item part = hero.myInventory.GetItemAndSplitIfNeeded(item, qty);
                if (IsBanker(npc)) {
                    ShopUIScript.DepositItem(part, qty);
                } else {
                    ShopUIScript.SellItem(part, qty);
                }
            } else {
                // Banker withdraw: the item lives in the banker's inventory.
                Item part = npc.myInventory.GetItemAndSplitIfNeeded(item, qty);
                hero.myInventory.AddItemRemoveFromPrevCollection(part, stackItems: true);
                StringManager.SetTag(0, part.displayName + part.GetQuantityText());
                GameLogScript.LogWriteStringRef("log_player_withdrawitem");
            }

            ShopUIScript.UpdateShop();
        }

        // Read an equippable shop item's stat change vs the gear it would replace, cycling the compared
        // slot on repeat — as the equipment sheet does. Non-equipment has nothing to compare.
        private static void Compare(OverlayCtx ctx, Item item) {
            if (!(item is Equipment equipment)) {
                ctx.Message.Fragment(ModStrings.NothingToCompareShop);
                return;
            }

            if (_compareItem == item) {
                _compareAlt = !_compareAlt;
            } else {
                _compareItem = item;
                _compareAlt = false;
            }

            Equipment against = EquipmentBlock.FindEquipmentToCompareAgainst(equipment, _compareAlt);
            if (against == null) {
                ctx.Message.Fragment(ModStrings.NothingToCompareShop);
                return;
            }

            ctx.Message.Fragment(ModStrings.ComparedTo(GameLabelReader.Clean(against.displayName)));
            string delta = GameLabelReader.Clean(EquipmentBlock.CompareItems(against, equipment, equipment.slot));
            ctx.Message.Fragment(string.IsNullOrEmpty(delta) ? ModStrings.NoDifference : delta);
        }

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

        // --- Helpers ---------------------------------------------------------------------------

        private static NPC Merchant() {
            return UIManagerScript.currentConversation != null ? UIManagerScript.currentConversation.whichNPC : null;
        }

        private static bool IsBanker(NPC npc) {
            return npc != null && npc.actorRefName == "npc_banker";
        }

        private static bool CanAfford(Item item) {
            return GameMasterScript.heroPCActor.GetMoney() >= item.GetIndividualShopPrice();
        }

        // The merchant's sale multiplier, defaulting to 1 when unavailable.
        private static float SaleMult(NPC npc) {
            if (npc == null) {
                return 1f;
            }

            var keeper = npc.GetShop();
            return keeper != null && keeper.GetShop() != null ? keeper.GetShop().saleMult : 1f;
        }

        // The largest quantity the hero can act on: the full stack, except a banker deposit is capped
        // by how many deposit fees the hero can afford (mirrors ShopUIScript's deposit-slider clamp).
        private static int MaxQuantity(Item item, NPC npc) {
            int q = item.GetQuantity();
            if (ShopUIScript.shopState == ShopState.SELL && IsBanker(npc)) {
                int price = item.GetBankPrice();
                if (price > 0) {
                    int afford = GameMasterScript.heroPCActor.GetMoney() / price;
                    if (afford < q) {
                        q = afford;
                    }

                    if (q * price > GameMasterScript.heroPCActor.GetMoney()) {
                        q--;
                    }
                }
            }

            return q;
        }
    }
}
