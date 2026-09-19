namespace IdleLineage.Combat;

public readonly record struct ShopBuybackResult(bool Success, ShopBuybackFailure Failure, string ItemKey = "", long Quantity = 0L, long GoldSpent = 0L)
{
	public static ShopBuybackResult Failed(ShopBuybackFailure failure)
	{
		return new ShopBuybackResult(Success: false, failure, "", 0L, 0L);
	}
}
