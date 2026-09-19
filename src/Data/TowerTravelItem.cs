namespace IdleLineage.Data;

public readonly record struct TowerTravelItem(TowerTravelItemKind Kind, int FloorNumber, string DestinationMapKey, string ArrivalLandmarkId);
