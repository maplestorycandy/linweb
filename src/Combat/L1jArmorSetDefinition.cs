using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record L1jArmorSetDefinition(int SetId, string Code, string Name, IReadOnlyList<int> ItemIds, int PolyId, string? MorphName, string Description, L1jArmorSetBonus Bonus);
