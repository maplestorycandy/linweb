namespace IdleLineage.Combat;

public readonly record struct ConsumableEvaluation(bool Allowed, ConsumableUseFailure Failure, ConsumableKind Kind, string ItemKey = "", string EffectKey = "", double DurationSeconds = 0.0, double SatietyRestore = 0.0)
{
	public static ConsumableEvaluation Success(ConsumableKind kind, string itemKey, string effectKey = "", double durationSeconds = 0.0, double satietyRestore = 0.0)
	{
		return new ConsumableEvaluation(Allowed: true, ConsumableUseFailure.None, kind, itemKey, effectKey, durationSeconds, satietyRestore);
	}

	public static ConsumableEvaluation Failed(ConsumableUseFailure failure, string itemKey = "", ConsumableKind kind = ConsumableKind.None, string effectKey = "")
	{
		return new ConsumableEvaluation(Allowed: false, failure, kind, itemKey, effectKey);
	}
}
