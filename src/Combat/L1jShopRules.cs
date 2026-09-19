using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jShopRules
{
	public const int InventorySlotLimit = int.MaxValue;

	public const long MaximumOrderPrice = 2000000000L;

	public static int TaxPercentOf(IGameData data, int npcId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (npcId >= 888001 && npcId <= 888005)
		{
			return 0;
		}
		JsonObject jsonObject = TaxNode(data);
		int num = ReadMap(jsonObject, "npcTown", npcId);
		int num2 = ((num >= 1 && num <= 10) ? (ReadMap(jsonObject, "townTaxPercent", num) + jsonObject["townFixedTaxPercent"].GetValue<int>()) : 0);
		int num3 = ReadMap(jsonObject, "townCastle", num);
		return ((num3 != 0) ? ReadMap(jsonObject, "castleTaxPercent", num3) : 0) + num2 + jsonObject["warTaxPercent"].GetValue<int>();
	}

	public static long LayTax(long price, int taxPercent)
	{
		return price + price * taxPercent / 100;
	}

	public static long BuyTotalPrice(IGameData data, int npcId, L1jShopItem item, long packs)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		return LayTax(item.SellPrice, TaxPercentOf(data, npcId)) * packs;
	}

	public static long AssessedPrice(L1jShopItem item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		return item.BuyPrice / Math.Max(1, item.PackCount);
	}

	public static long AssessedPriceFor(IGameData data, int npcId, string itemKey, ItemBlessing blessing = ItemBlessing.Normal)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(itemKey, "itemKey");
		int num = L1jShopCatalog.BuyPriceOf(data, npcId, itemKey, blessing);
		return (num >= 0) ? num : 0;
	}

	public static L1jBuyResult TryBuy(IGameData data, Combatant buyer, int npcId, string itemKey, long packs = 1L, int? l1jItemId = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(buyer, "buyer");
		if (packs <= 0)
		{
			return new L1jBuyResult(Success: false, L1jShopFailure.InvalidCount, itemKey, 0L, 0L);
		}
		if (!L1jShopCatalog.Shops(data).ContainsKey(npcId))
		{
			return new L1jBuyResult(Success: false, L1jShopFailure.UnknownShop, itemKey, 0L, 0L);
		}
		L1jShopItem offer = L1jShopCatalog.SellList(data, npcId).FirstOrDefault((L1jShopItem row) => string.Equals(row.ItemKey, itemKey, StringComparison.Ordinal) && (!l1jItemId.HasValue || row.L1jItemId == l1jItemId));
		if ((object)offer == null)
		{
			return new L1jBuyResult(Success: false, L1jShopFailure.NotSold, itemKey, 0L, 0L);
		}
		long num = BuyTotalPrice(data, npcId, offer, packs);
		if ((num < 0 || num > 2000000000) ? true : false)
		{
			return new L1jBuyResult(Success: false, L1jShopFailure.PriceOverflow, itemKey, 0L, 0L);
		}
		if (CombatWallet.Balance(buyer) < num)
		{
			return new L1jBuyResult(Success: false, L1jShopFailure.InsufficientGold, itemKey, 0L, 0L);
		}
		long num2 = packs * Math.Max(1, offer.PackCount);
		string itemName = data.Item(itemKey)?["n"]?.GetValue<string>() ?? "";
		double num3 = WeightRules.ItemWeight(data, itemName) * (double)num2;
		WeightReport weightReport = WeightRules.Evaluate(data, buyer);
		if (!GameRateConfig.DisableWeightPenalty && weightReport.CurrentWeight + num3 > weightReport.TotalCapacity)
		{
			return new L1jBuyResult(Success: false, L1jShopFailure.Overweight, itemKey, 0L, 0L);
		}
		int enchant = PreEnhancedLootRules.SafeEnchantOf(data.Item(itemKey));
		if (!CombatWallet.TryCharge(buyer, num))
		{
			return new L1jBuyResult(Success: false, L1jShopFailure.InsufficientGold, itemKey, 0L, 0L);
		}
		CombatInventory.Add(data, buyer, new ItemStack(CombatInventory.NextUid(buyer), itemKey, num2)
		{
			Blessing = offer.Blessing,
			Enhancement = enchant
		});
		return new L1jBuyResult(Success: true, L1jShopFailure.None, itemKey, num2, num);
	}

	public static L1jShopFailure SellRefusalOf(IGameData data, Combatant seller, int npcId, ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (stack.Locked)
		{
			return L1jShopFailure.Locked;
		}
		if (stack.Sealed)
		{
			return L1jShopFailure.Sealed;
		}
		if (seller.EquippedItems.Values.Any((ItemStack equipped) => equipped == stack))
		{
			return L1jShopFailure.Equipped;
		}
		if (L1jShopCatalog.BuyPriceOf(data, npcId, stack.ItemKey, stack.Blessing) >= 0)
		{
			return L1jShopFailure.None;
		}
		return L1jShopFailure.NotPurchased;
	}

	public static L1jSellResult TrySell(IGameData data, Combatant seller, int npcId, string itemUid, long quantity = 1L, MercenaryParty? activeParty = null, CombatEngine? liveEngine = null, long? nowUnixMilliseconds = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(seller, "seller");
		if (quantity <= 0)
		{
			return new L1jSellResult(Success: false, L1jShopFailure.InvalidCount, "", 0L, 0L);
		}
		if (!L1jShopCatalog.IsBuybackShop(data, npcId))
		{
			return new L1jSellResult(Success: false, L1jShopFailure.UnknownShop, "", 0L, 0L);
		}
		ItemStack itemStack = seller.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (itemStack == null || itemStack.Quantity < quantity)
		{
			return new L1jSellResult(Success: false, L1jShopFailure.ItemNotFound, "", 0L, 0L);
		}
		L1jShopFailure l1jShopFailure = SellRefusalOf(data, seller, npcId, itemStack);
		if (l1jShopFailure != L1jShopFailure.None)
		{
			return new L1jSellResult(Success: false, l1jShopFailure, itemStack.ItemKey, 0L, 0L);
		}
		long num = AssessedPriceFor(data, npcId, itemStack.ItemKey, itemStack.Blessing) * quantity;
		if (activeParty != null && quantity == itemStack.Quantity)
		{
			MonsterCardPartyRules.RecallBeforeCardLeavesInventory(data, activeParty, itemStack, nowUnixMilliseconds ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), liveEngine);
		}
		if (!ItemStackInventory.TryRemoveByUid(seller.InventoryStacks, itemStack.Uid, quantity, out ItemStack _))
		{
			return new L1jSellResult(Success: false, L1jShopFailure.ItemNotFound, itemStack.ItemKey, 0L, 0L);
		}
		CombatInventory.SyncLegacyView(seller);
		CombatWallet.Add(seller, num);
		return new L1jSellResult(Success: true, L1jShopFailure.None, itemStack.ItemKey, quantity, num);
	}

	private static JsonObject TaxNode(IGameData data)
	{
		return ((data.Table("L1J_NPC_SHOPS") as JsonObject)?["tax"] as JsonObject) ?? throw new InvalidDataException("L1J_NPC_SHOPS.tax failed to load.");
	}

	private static int ReadMap(JsonObject tax, string mapName, int key)
	{
		if (key == 0 || !(tax[mapName] is JsonObject jsonObject) || !(jsonObject[key.ToString()] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}
}
