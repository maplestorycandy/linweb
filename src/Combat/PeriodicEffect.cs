namespace IdleLineage.Combat;

public sealed class PeriodicEffect
{
	public int TickEvery = 10;

	public int TicksUntilNext = 10;

	public int TicksRemaining;

	public double Damage;

	public double BonusTrueDamage;

	public DamageType DamageType = DamageType.Dot;

	public string Element = "none";

	public Combatant? Source;
}
