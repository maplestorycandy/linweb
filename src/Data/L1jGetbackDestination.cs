namespace IdleLineage.Data;

public sealed record L1jGetbackDestination(int GameX, int GameY, int MapId, string? MapKey, int? LocalX, int? LocalY, double? DisplayX, double? DisplayY, string? TownKey)
{
	public bool IsRuntimeResolved
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(MapKey) && DisplayX.HasValue)
			{
				return DisplayY.HasValue;
			}
			return false;
		}
	}
}
