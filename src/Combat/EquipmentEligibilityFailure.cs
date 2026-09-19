namespace IdleLineage.Combat;

public enum EquipmentEligibilityFailure
{
	None,
	MissingItemDefinition,
	NotPlayerEquipment,
	PetEquipmentOnly,
	AvatarMismatch,
	ClassMismatch,
	SlotLockedByLevel,
	UniqueItemAlreadyEquipped,
	DuplicateEarring,
	RingCopyLimit,
	CursedEquipmentConflict,
	LevelTooLow,
	LevelTooHigh
}
