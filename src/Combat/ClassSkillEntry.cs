namespace IdleLineage.Combat;

public readonly record struct ClassSkillEntry(string SkillId, int RequiredLevel, int Tier, bool LevelMet, bool ElementMet, bool Learned, bool Granted);
