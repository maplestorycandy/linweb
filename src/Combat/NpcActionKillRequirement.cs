using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record NpcActionKillRequirement(string CounterId, string TargetName, int RequiredCount, IReadOnlyList<string> MobKeys);
