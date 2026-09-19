namespace IdleLineage.Combat;

public readonly record struct TeleportMemoryResult(bool Success, TeleportMemoryFailure Failure, TeleportMemoryLocation? Location = null, TeleportPaymentSource Payment = TeleportPaymentSource.None)
{
	public static TeleportMemoryResult Failed(TeleportMemoryFailure failure)
	{
		return new TeleportMemoryResult(Success: false, failure);
	}
}
