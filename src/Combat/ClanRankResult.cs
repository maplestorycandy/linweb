namespace IdleLineage.Combat;

public readonly record struct ClanRankResult(bool Success, ClanRankFailure Failure)
{
	public static ClanRankResult Failed(ClanRankFailure failure)
	{
		return new ClanRankResult(Success: false, failure);
	}
}
