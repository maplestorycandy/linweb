namespace IdleLineage.Combat;

public enum EquipmentChangeFailure
{
	None,
	InvalidOwner,
	ItemNotFound,
	SlotNotEquipped,
	EligibilityRejected,
	CursedEquipment,
	InventoryOverflow
}
