using System.Collections.Generic;

namespace IdleLineage.Data;

public readonly record struct MapPage(int X, int Y, string File, int PixelX, int PixelY, string? Foreground = null, string? ForegroundMask = null, IReadOnlyList<int>? ForegroundGroups = null);
