namespace IdleLineage.Combat;

public readonly record struct PurifyStoneResult(bool Attempted, bool Upgraded, PurifyStoneFailure Failure, string OutputItemKey)
{
	public static PurifyStoneResult Refused(PurifyStoneFailure failure)
	{
		return new PurifyStoneResult(Attempted: false, Upgraded: false, failure, "");
	}
}
