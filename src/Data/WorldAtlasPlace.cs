namespace IdleLineage.Data;

public readonly record struct WorldAtlasPlace(string Name, double PixelX, double PixelY, bool EntranceOnly = false);
