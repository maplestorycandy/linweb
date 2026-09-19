namespace IdleLineage.Combat;

public sealed record NpcActionItem(int L1jItemId, int Count, string? ItemKey, ItemBlessing Blessing = ItemBlessing.Normal)
{
	public bool IsAdena => L1jItemId == 40308;
}
