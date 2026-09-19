namespace IdleLineage.Data;

public readonly record struct WorldAtlasPlaceAnchor(string Name, int GameX, int GameY, bool EntranceOnly = false, int RadiusCells = 0);
