namespace IdleLineage.Data;

public sealed record TowerSurfaceEntrance(int MainlandX, int MainlandY, int MainlandArrivalX, int MainlandArrivalY, string DestinationMapKey, string ArrivalLandmarkId, string SurfaceExitLandmarkId);
