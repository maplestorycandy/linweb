namespace IdleLineage.Combat;

public sealed record L1jTrapDefinition(int TrapId, string Note, L1jTrapKind Kind, int GfxId, bool Detectionable, int Base, int Dice, int DiceCount, string PoisonType, int PoisonDelayMs, int PoisonTimeMs, int PoisonDamage, string? MonsterMobKey, int MonsterCount, string? TeleportMapKey, int TeleportCellX, int TeleportCellY, string? SkillKey, int SkillTimeSeconds);
