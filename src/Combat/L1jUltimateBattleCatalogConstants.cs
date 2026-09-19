namespace IdleLineage.Combat;

public static class L1jUltimateBattleCatalogConstants
{
	public const string TableName = "L1J_ULTIMATE_BATTLE";

	public const int EntryWindowMinutes = 5;

	public const int RoundCount = 4;

	public static long BalancedAdenaGrant(int round)
	{
		return round switch
		{
			1 => 10000, 
			2 => 40000, 
			3 => 50000, 
			_ => 0, 
		};
	}
}
