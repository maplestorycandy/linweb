namespace IdleLineage.Combat;

public sealed record MobSkillTrigger(int SelfHpPercent, int CompanionHpPercent, int RangeCells, int MaxUses, MobSkillTargetSwap TargetSwap, double DamageMultiplier)
{
	public bool Gated
	{
		get
		{
			if (SelfHpPercent <= 0 && CompanionHpPercent <= 0 && RangeCells == 0)
			{
				return MaxUses > 0;
			}
			return true;
		}
	}

	public static readonly MobSkillTrigger None = new MobSkillTrigger(0, 0, 0, 0, MobSkillTargetSwap.None, 1.0);

	public bool IsTriggerDistance(double cells)
	{
		if (RangeCells >= 0 || !(cells <= (double)(-RangeCells)))
		{
			if (RangeCells > 0)
			{
				return cells >= (double)RangeCells;
			}
			return false;
		}
		return true;
	}

	public bool DistanceSatisfied(double cells)
	{
		if (RangeCells != 0)
		{
			return IsTriggerDistance(cells);
		}
		return true;
	}
}
