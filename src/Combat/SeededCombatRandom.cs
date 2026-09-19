using System;

namespace IdleLineage.Combat;

public sealed class SeededCombatRandom : ICombatRandom
{
	private readonly Random _random;

	public SeededCombatRandom(int seed)
	{
		_random = new Random(seed);
	}

	public double NextDouble()
	{
		return _random.NextDouble();
	}

	public int Roll(int count, int sides)
	{
		if (count <= 0 || sides <= 0)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < count; i++)
		{
			num += (int)Math.Floor(_random.NextDouble() * (double)sides) + 1;
		}
		return num;
	}
}
