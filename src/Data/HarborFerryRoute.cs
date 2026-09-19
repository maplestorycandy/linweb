namespace IdleLineage.Data;

public sealed record HarborFerryRoute(string NpcId, string OriginTownKey, string OriginMapKey, string OriginLandmarkId, string DestinationTownKey, string DestinationMapKey, string DestinationLandmarkId, string DestinationName);
