namespace IdleLineage.Combat;

public readonly record struct PetAcquisitionResult(bool Success, PetAcquisitionFailure Failure, PetInstance? Pet = null, string ItemKey = "", string PetForm = "", long QuantityConsumed = 0L)
{
	public static PetAcquisitionResult Failed(PetAcquisitionFailure failure, string itemKey = "", string petForm = "", long quantityConsumed = 0L)
	{
		return new PetAcquisitionResult(Success: false, failure, null, itemKey, petForm, quantityConsumed);
	}
}
