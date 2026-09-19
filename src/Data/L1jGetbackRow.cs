using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record L1jGetbackRow(L1jGetbackArea Area, IReadOnlyList<L1jGetbackDestination> Destinations, int DefaultTownId, int ElfTownId, int DarkElfTownId, bool ScrollEscape, string Note);
