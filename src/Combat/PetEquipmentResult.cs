namespace IdleLineage.Combat;

public readonly record struct PetEquipmentResult(bool Success, PetEquipmentFailure Failure, PetInstance? Pet, string Slot, string ItemKey)
{
	public static PetEquipmentResult Failed(PetEquipmentFailure failure, PetInstance? pet = null, string slot = "", string itemKey = "")
	{
		return new PetEquipmentResult(Success: false, failure, pet, slot, itemKey);
	}
}
