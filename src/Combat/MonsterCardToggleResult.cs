using System;

namespace IdleLineage.Combat;

public readonly record struct MonsterCardToggleResult(bool Success, bool Joined, string MobKey, MonsterCardToggleFailure Failure, long RemainingCooldownMilliseconds)
{
	public static MonsterCardToggleResult Failed(string mobKey, MonsterCardToggleFailure failure, long remaining = 0L)
	{
		return new MonsterCardToggleResult(Success: false, Joined: false, mobKey, failure, Math.Max(0L, remaining));
	}
}
