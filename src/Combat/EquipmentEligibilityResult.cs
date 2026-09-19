namespace IdleLineage.Combat;

public readonly record struct EquipmentEligibilityResult(bool Allowed, EquipmentEligibilityFailure Failure, string Slot, string ItemKey)
{
	public static EquipmentEligibilityResult Ok(string slot, string itemKey)
	{
		return new EquipmentEligibilityResult(Allowed: true, EquipmentEligibilityFailure.None, slot, itemKey);
	}

	public static EquipmentEligibilityResult Failed(EquipmentEligibilityFailure failure, string itemKey, string slot = "")
	{
		return new EquipmentEligibilityResult(Allowed: false, failure, slot, itemKey);
	}
}
