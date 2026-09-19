namespace IdleLineage.Combat;

public static class MobBehaviorRules
{
	public const int MaximumSimultaneousPlayerPursuers = 12;

	public const int HateLeashDistanceCells = 30;

	public const double HateLeashIdleSeconds = 10.0;

	public const double ActiveAggroRange = 576.0;

	public const double IdleWanderMinimumCells = 1.0;

	public const double IdleWanderMaximumCells = 3.0;

	public const double IdleWanderTimeoutSeconds = 10.0;

	public const double PassiveWanderPauseMinimumSeconds = 2.0;

	public const double PassiveWanderPauseMaximumSeconds = 5.0;
}
