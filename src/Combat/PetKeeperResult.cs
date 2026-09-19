namespace IdleLineage.Combat;

public readonly record struct PetKeeperResult(bool Success, PetKeeperFailure Failure, int Affected, PetInstance? Pet)
{
	public static PetKeeperResult Failed(PetKeeperFailure failure, PetInstance? pet = null)
	{
		return new PetKeeperResult(Success: false, failure, 0, pet);
	}
}
