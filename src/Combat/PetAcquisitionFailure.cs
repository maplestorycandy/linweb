namespace IdleLineage.Combat;

public enum PetAcquisitionFailure
{
	None,
	InvalidActor,
	ActorDead,
	ItemUseBlocked,
	ItemNotFound,
	ItemDefinitionMissing,
	UnsupportedItem,
	InvalidTarget,
	TargetNotTameable,
	WrongTamingItem,
	TargetHealthTooHigh,
	ResurrectedTarget,
	TamingRollFailed,
	InventoryFull,
	UnknownPet,
	PetUidUnavailable,
	UnknownLure,
	InvalidDefeatedTarget,
	NoMatchingLure,
	StorageFull
}
