using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record ClientEnvironmentSoundRule(int SourceOrder, IReadOnlyList<int> MainSounds, IReadOnlyList<string> Groups);
