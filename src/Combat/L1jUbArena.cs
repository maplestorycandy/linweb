using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record L1jUbArena(int UbId, string Name, int MapId, string MapKey, int MinLevel, int MaxLevel, int MaxPlayer, IReadOnlySet<string> AllowedClasses, bool AllowsMale, bool AllowsFemale, bool AllowsPotion, IReadOnlyList<int> ManagerNpcIds, IReadOnlyList<int> OpenTimes, IReadOnlyDictionary<int, IReadOnlyDictionary<int, IReadOnlyList<L1jUbWaveGroup>>> Patterns);
