namespace IdleLineage.Combat;

public readonly record struct SkillLearningEvaluation(bool Allowed, SkillLearningFailure Failure, string ItemKey = "", string SkillId = "", int RequiredLevel = 0, string RequiredElement = "")
{
	public static SkillLearningEvaluation Success(string itemKey, string skillId, int requiredLevel, string requiredElement)
	{
		return new SkillLearningEvaluation(Allowed: true, SkillLearningFailure.None, itemKey, skillId, requiredLevel, requiredElement);
	}

	public static SkillLearningEvaluation Failed(SkillLearningFailure failure, string itemKey = "", string skillId = "", int requiredLevel = 0, string requiredElement = "")
	{
		return new SkillLearningEvaluation(Allowed: false, failure, itemKey, skillId, requiredLevel, requiredElement);
	}
}
