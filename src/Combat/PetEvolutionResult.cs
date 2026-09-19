namespace IdleLineage.Combat;

public readonly record struct PetEvolutionResult(bool Success, PetEvolutionFailure Failure, PetInstance? Pet, string PreviousForm, string TargetForm)
{
	public static PetEvolutionResult Failed(PetEvolutionFailure failure, PetInstance? pet = null)
	{
		return new PetEvolutionResult(Success: false, failure, pet, pet?.Form ?? "", "");
	}
}
