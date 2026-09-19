namespace IdleLineage.Combat;

public enum ConsumableUseFailure
{
	None,
	ActorDead,
	ItemUseBlocked,
	ItemNotFound,
	ItemLocked,
	ItemDefinitionMissing,
	NotConsumable,
	DirectUseDisabled,
	ClassMismatch,
	HealingBlocked,
	PotionCooldown,
	ManualOnly,
	RequiresSpecialHandler,
	SatietyFull,
	NothingToCure,
	NothingToRepair,
	LevelTooLow,
	LevelTooHigh,
	ItemDelayActive,
	ItemReuseDelay
}
