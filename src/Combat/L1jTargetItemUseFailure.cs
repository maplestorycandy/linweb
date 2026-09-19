namespace IdleLineage.Combat;

public enum L1jTargetItemUseFailure
{
	None,
	SourceMissing,
	SourceLocked,
	UnsupportedSource,
	TargetMissing,
	TargetLocked,
	InvalidTarget,
	OutputMissing,
	QuantityOverflow
}
