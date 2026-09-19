namespace IdleLineage.Combat;

public readonly record struct ClanHouseResult(bool Success, ClanHouseFailure Failure, int HouseId, long GoldSpent, long DeadlineUnixMilliseconds)
{
	public static ClanHouseResult Failed(ClanHouseFailure failure)
	{
		return new ClanHouseResult(Success: false, failure, 0, 0L, 0L);
	}
}
