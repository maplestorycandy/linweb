namespace IdleLineage.Combat;

public readonly record struct ClanCreateResult(bool Success, ClanFailure Failure, string Name, long GoldSpent)
{
	public static ClanCreateResult Failed(ClanFailure failure)
	{
		return new ClanCreateResult(Success: false, failure, "", 0L);
	}
}
