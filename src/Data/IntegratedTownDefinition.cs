namespace IdleLineage.Data;

public sealed record IntegratedTownDefinition(string MapKey, string TownKey, string SafeAreaName, string HuntingAreaName, string HuntingMapKey, string? EntryLandmarkId = null, (int GameX, int GameY)? EntryGameCell = null);
