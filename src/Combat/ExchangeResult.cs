namespace IdleLineage.Combat;

public readonly record struct ExchangeResult(bool Success, ExchangeFailure Failure, string OptionId, string ItemKey, long Quantity, long ProducedQuantity, long BlessedQuantity, long ItemGainAttemptsConsumed)
{
	public static ExchangeResult Failed(ExchangeFailure failure, string optionId = "", string itemKey = "")
	{
		return new ExchangeResult(Success: false, failure, optionId, itemKey, 0L, 0L, 0L, 0L);
	}

	public static ExchangeResult Completed(string optionId, string itemKey, long quantity, long producedQuantity, long blessedQuantity, long itemGainAttemptsConsumed)
	{
		return new ExchangeResult(Success: true, ExchangeFailure.None, optionId, itemKey, quantity, producedQuantity, blessedQuantity, itemGainAttemptsConsumed);
	}
}
