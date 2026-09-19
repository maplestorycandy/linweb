using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record MapRegionDefinition(string Key, string Name, int RegionIndex, IReadOnlyList<MapDestination> Destinations, string? CastleCityKey, int? CastleInsertIndex);
