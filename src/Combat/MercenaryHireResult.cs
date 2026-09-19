namespace IdleLineage.Combat;

public readonly record struct MercenaryHireResult(bool Success, MercenaryHireFailure Failure, MercenaryContract? Contract)
{
	public static MercenaryHireResult Failed(MercenaryHireFailure failure)
	{
		return new MercenaryHireResult(Success: false, failure, null);
	}
}
