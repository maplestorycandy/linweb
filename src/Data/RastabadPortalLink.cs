namespace IdleLineage.Data;

public sealed record RastabadPortalLink(string SourceMapKey, string PortalLandmarkId, string DestinationMapKey, string ArrivalLandmarkId, bool SyntheticReturn);
