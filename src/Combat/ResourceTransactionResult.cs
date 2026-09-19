namespace IdleLineage.Combat;

public readonly record struct ResourceTransactionResult(bool Success, ResourceTransactionFailure Failure = ResourceTransactionFailure.None, string ItemKey = "")
{
	public static ResourceTransactionResult Ok()
	{
		return new ResourceTransactionResult(Success: true);
	}

	public static ResourceTransactionResult Failed(ResourceTransactionFailure failure, string itemKey = "")
	{
		return new ResourceTransactionResult(Success: false, failure, itemKey);
	}
}
