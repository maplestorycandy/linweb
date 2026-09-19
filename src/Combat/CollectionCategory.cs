using System.Collections.Generic;

namespace IdleLineage.Combat;

internal sealed record CollectionCategory(string Key, string Name, string Group, IReadOnlyList<string> Items, string BonusStat, double BonusValue, string BonusLabel, IReadOnlyList<double> TierValues);
