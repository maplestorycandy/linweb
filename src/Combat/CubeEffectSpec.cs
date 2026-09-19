namespace IdleLineage.Combat;

internal sealed record CubeEffectSpec(string SkillId, CubeEffectKind Kind, int IntervalTicks, int StatusDurationTicks, int MpRestore, CombatSkill? DamageSkill);
