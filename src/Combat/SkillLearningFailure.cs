namespace IdleLineage.Combat;

public enum SkillLearningFailure
{
	None,
	UnsupportedActor,
	ItemNotFound,
	ItemDefinitionMissing,
	NotSkillBook,
	SkillReferenceMissing,
	SkillDefinitionMissing,
	ClassMismatch,
	LevelTooLow,
	ElementNotSelected,
	ElementMismatch,
	AlreadyLearned
}
