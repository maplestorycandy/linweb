namespace IdleLineage.Data;

public sealed record DragonValleyDungeonFloor(string MapKey, int SourceMapId, int FloorNumber, string DisplayName, bool IsAntharasHabitat = false);
