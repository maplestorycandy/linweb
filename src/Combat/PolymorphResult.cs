namespace IdleLineage.Combat;

public sealed record PolymorphResult(bool Success, string FormName, PolymorphFailure Failure)
{
	public static PolymorphResult Fail(PolymorphFailure failure)
	{
		return new PolymorphResult(Success: false, "", failure);
	}
}
