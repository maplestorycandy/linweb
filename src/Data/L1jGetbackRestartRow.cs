using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record L1jGetbackRestartRow(int AreaMapId, IReadOnlyList<string> AreaMapKeys, string Note, L1jGetbackDestination Destination);
