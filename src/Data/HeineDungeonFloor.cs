namespace IdleLineage.Data;

public sealed record HeineDungeonFloor(string MapKey, int SourceMapId, int FloorNumber, string DisplayName, bool IsEvaKingdom = false);
