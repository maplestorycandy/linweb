namespace IdleLineage.Combat;

public readonly record struct L1jResolventResult(bool Attempted, L1jResolventFailure Failure, string TargetItemKey, int TargetEnhancement, int BaseCrystalCount, int Roll, int CrystalCount)
{
	public bool Success
	{
		get
		{
			if (Attempted)
			{
				return CrystalCount > 0;
			}
			return false;
		}
	}
}
