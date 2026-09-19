namespace IdleLineage.Combat;

public readonly record struct WarehouseTransferResult(bool Success, WarehouseTransferFailure Failure = WarehouseTransferFailure.None, string ItemKey = "", long Quantity = 0L)
{
	public static WarehouseTransferResult Failed(WarehouseTransferFailure failure, string itemKey = "")
	{
		return new WarehouseTransferResult(Success: false, failure, itemKey, 0L);
	}

	public static WarehouseTransferResult Moved(string itemKey, long quantity)
	{
		return new WarehouseTransferResult(Success: true, WarehouseTransferFailure.None, itemKey, quantity);
	}
}
