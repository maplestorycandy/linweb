using System.Collections.Generic;

namespace IdleLineage.Combat;

public readonly record struct ConsumableUseResult(bool Success, ConsumableUseFailure Failure, ConsumableKind Kind, string ItemKey = "", string EffectKey = "", long QuantityConsumed = 0L, double CalculatedHealing = 0.0, double HpRestored = 0.0, double BuffDurationSeconds = 0.0, bool BuffApplied = false, double SatietyRestored = 0.0, int BrokenBladeStacksRemoved = 0, IReadOnlyList<string>? CuredStatusKinds = null, IReadOnlyList<string>? ReplacedBuffKeys = null)
{
	public static ConsumableUseResult Failed(ConsumableEvaluation evaluation)
	{
		return new ConsumableUseResult(Success: false, evaluation.Failure, evaluation.Kind, evaluation.ItemKey, evaluation.EffectKey, 0L);
	}
}
