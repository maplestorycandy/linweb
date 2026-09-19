using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record WorldAtlasDefinition(string MapKey, string Title, string AssetPath, int PixelWidth, int PixelHeight, double BaseXFromGameX, double BaseXFromGameY, double BaseXOffset, double BaseYFromGameX, double BaseYFromGameY, double BaseYOffset, IReadOnlyList<WorldAtlasPlace> Places);
