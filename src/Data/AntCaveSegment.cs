namespace IdleLineage.Data;

public sealed record AntCaveSegment(string MapKey, int SourceMapId, string EntranceLetter, string DisplayFloorName, bool IsBottomFloor = false);
