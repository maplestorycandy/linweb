namespace IdleLineage.Data;

public readonly record struct MapLandmark(string Id, int GameX, int GameY, int LocalX, int LocalY, int NativePixelX, int NativePixelY, int ClearWidthCells, string Status, string Rule);
