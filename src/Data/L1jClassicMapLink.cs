namespace IdleLineage.Data;

public sealed record L1jClassicMapLink(string Id, int SourceMapId, string SourceMapKey, (int X, int Y) SourceGameCell, int DestinationMapId, string DestinationMapKey, string DestinationName, (int X, int Y) DestinationGameCell, int Heading);
