using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record L1jTownGetbackLocation(int TownId, string? TownKey, int MapId, IReadOnlyList<L1jGetbackDestination> Destinations);
