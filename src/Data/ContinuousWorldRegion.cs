using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record ContinuousWorldRegion(string MapKey, string Name, IReadOnlyList<string> AreaKeys, IReadOnlyList<string> TownKeys, IReadOnlyList<WorldTravelRoute> Routes);
