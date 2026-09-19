namespace IdleLineage.Data;

public sealed record MapDestination(string RegionKey, string RegionName, int RegionIndex, int DestinationIndex, string Key, string Name, string Category, string CategoryName, string? Color, MapDestinationKind Kind, bool ClassicHide, string? QuestRequirement, string? HeldKeyRequirement, string? ConsumedKeyRequirement, string? PrideBossRequirement, int? PrideFloorRequirement, string? TravelMapKey = null, int? LandingGameX = null, int? LandingGameY = null, long? TravelPriceAdena = null)
{
	public string MapKey => TravelMapKey ?? Key;

	public bool HasFixedLanding
	{
		get
		{
			if (LandingGameX.HasValue)
			{
				return LandingGameY.HasValue;
			}
			return false;
		}
	}
}
