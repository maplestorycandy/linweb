namespace IdleLineage.Combat;

public readonly record struct PetCommandResult(PetCommandStatus Status, int Applied, int Defied)
{
	public bool Success => Applied > 0;
}
