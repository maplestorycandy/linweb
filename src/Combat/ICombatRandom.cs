namespace IdleLineage.Combat;

public interface ICombatRandom
{
	double NextDouble();

	int Roll(int count, int sides);
}
