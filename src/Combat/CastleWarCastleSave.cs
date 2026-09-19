namespace IdleLineage.Combat;

public sealed class CastleWarCastleSave
{
	public int CastleId { get; set; }

	public string OwnerIdentity { get; set; } = "";

	public string OwnerDisplayName { get; set; } = "";

	public double RetryCooldownSeconds { get; set; }

	public double ProtectionSeconds { get; set; }

	public long Treasury { get; set; }

	public double IncomeSeconds { get; set; }
}
