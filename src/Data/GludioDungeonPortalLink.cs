namespace IdleLineage.Data;

public sealed record GludioDungeonPortalLink(string SourceMapKey, string PortalLandmarkId, string DestinationMapKey, string ArrivalLandmarkId, bool IsInternalPortal = false);
