using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record ClientEnvironmentSoundGroup(string Name, double IntervalSeconds, IReadOnlyList<int> Sounds);
