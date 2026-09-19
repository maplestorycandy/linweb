namespace IdleLineage.Combat;

public readonly record struct SkillBookInventoryEntry(string ItemUid, long Quantity, SkillLearningEvaluation Evaluation);
