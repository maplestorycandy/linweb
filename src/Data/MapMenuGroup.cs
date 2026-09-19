using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record MapMenuGroup(string Key, string Name, IReadOnlyList<MapDestination> Destinations);
