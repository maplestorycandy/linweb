namespace IdleLineage.Combat;

public readonly record struct PetCollarResult(bool Success, PetCollarFailure Failure, PetCollarAction Action, PetInstance? Pet, string CollarItemKey, string WhistleItemKey)
{
	public static PetCollarResult Failed(PetCollarFailure failure, PetInstance? pet = null, string collarItemKey = "")
	{
		return new PetCollarResult(Success: false, failure, PetCollarAction.None, pet, collarItemKey, "l1j_item_41160");
	}
}
