using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record MapMenuRegion(string Key, string Name, IReadOnlyList<MapMenuGroup> Groups);
