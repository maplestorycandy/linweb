namespace IdleLineage.Combat;

public readonly record struct SkillLearningResult(bool Success, SkillLearningFailure Failure, string ItemKey = "", string SkillId = "", int RequiredLevel = 0, string RequiredElement = "", long QuantityConsumed = 0L, SkillLearningOutcome Outcome = SkillLearningOutcome.None)
{
	public static SkillLearningResult Failed(SkillLearningEvaluation evaluation)
	{
		return new SkillLearningResult(Success: false, evaluation.Failure, evaluation.ItemKey, evaluation.SkillId, evaluation.RequiredLevel, evaluation.RequiredElement, 0L);
	}
}
