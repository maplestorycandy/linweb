namespace IdleLineage.Combat;

public sealed record ConsumableUseContext
{
	public bool ItemUseBlocked { get; init; }

	public bool HealingPotionsBlocked { get; init; }

	public bool Automatic { get; init; }

	public double AdditionalPotionHealingPercent { get; init; }
}
