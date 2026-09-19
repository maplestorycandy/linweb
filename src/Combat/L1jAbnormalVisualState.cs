namespace IdleLineage.Combat;

public readonly record struct L1jAbnormalVisualState(L1jParalysisVisual Paralysis, L1jPoisonVisual Poison)
{
	public bool FreezesAnimation => Paralysis != L1jParalysisVisual.None;
}
