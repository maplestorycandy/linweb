namespace IdleLineage.Data;

public sealed record L1jGetbackRoute(L1jGetbackDestination Destination, string Note, bool ScrollEscape, bool UsedFallback, int TownId);
