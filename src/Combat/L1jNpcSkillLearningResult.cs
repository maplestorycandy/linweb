using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public readonly record struct L1jNpcSkillLearningResult(bool Success, L1jNpcSkillLearningFailure Failure, IReadOnlyList<string> LearnedSkillIds, long GoldSpent)
{
	public static L1jNpcSkillLearningResult Failed(L1jNpcSkillLearningFailure failure)
	{
		return new L1jNpcSkillLearningResult(Success: false, failure, Array.Empty<string>(), 0L);
	}
}
