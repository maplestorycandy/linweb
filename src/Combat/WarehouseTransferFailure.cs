namespace IdleLineage.Combat;

public enum WarehouseTransferFailure
{
	None,
	InvalidQuantity,
	ItemNotFound,
	MissingItemDefinition,
	Locked,
	NotStorable,
	NotTradable,
	Sealed,
	WarehouseFull,
	QuantityOverflow,
	DuplicateUid,
	CorruptState,
	UidExhausted
}
