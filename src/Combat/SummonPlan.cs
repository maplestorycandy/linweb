using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record SummonPlan(string SkillId, double DurationSeconds, IReadOnlyList<SummonUnitPlan> Units, int PetCostPerUnit);
