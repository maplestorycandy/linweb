namespace IdleLineage.Combat;

public sealed record L1jUbSupply(int ItemId, string ItemKey, int StackCount, int Piles, long? GrantTotalOverride = null)
{
	public long SourceTotal => (long)StackCount * (long)Piles;

	public long Total => GrantTotalOverride ?? SourceTotal;
}
