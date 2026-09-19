namespace IdleLineage.Combat;

public enum ExchangeFailure
{
	None,
	InvalidNpc,
	InvalidOption,
	InvalidQuantity,
	MissingItemDefinition,
	InsufficientGold,
	InsufficientItem,
	GoldOverflow,
	InventoryOverflow,
	AttemptSequenceExhausted,
	UidExhausted,
	CorruptState
}
