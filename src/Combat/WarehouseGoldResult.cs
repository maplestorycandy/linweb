namespace IdleLineage.Combat;

public readonly record struct WarehouseGoldResult(bool Success, WarehouseGoldFailure Failure = WarehouseGoldFailure.None, long Amount = 0L)
{
	public static WarehouseGoldResult Failed(WarehouseGoldFailure failure)
	{
		return new WarehouseGoldResult(Success: false, failure, 0L);
	}

	public static WarehouseGoldResult Moved(long amount)
	{
		return new WarehouseGoldResult(Success: true, WarehouseGoldFailure.None, amount);
	}
}
