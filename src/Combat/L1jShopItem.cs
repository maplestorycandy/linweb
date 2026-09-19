namespace IdleLineage.Combat;

public sealed record L1jShopItem(int L1jItemId, string? ItemKey, ItemBlessing Blessing, int Order, int SellPrice, int PackCount, int BuyPrice);
