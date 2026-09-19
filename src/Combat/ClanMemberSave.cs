namespace IdleLineage.Combat;

public sealed class ClanMemberSave
{
	public string Identity { get; set; } = string.Empty;

	public ClanRank Rank { get; set; }
}
