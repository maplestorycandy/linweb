namespace IdleLineage.Combat;

public readonly record struct PetRosterResult(bool Success, PetRosterFailure Failure, PetInstance? Pet)
{
	public static PetRosterResult Failed(PetRosterFailure failure)
	{
		return new PetRosterResult(Success: false, failure, null);
	}
}
