namespace IdleLineage.Combat;

public readonly record struct MapAccessResult(bool Allowed, MapAccessFailure Failure = MapAccessFailure.None, string RequirementKey = "", int RequiredValue = 0, string ConsumedItemKey = "")
{
	public bool ConsumesItem => ConsumedItemKey.Length > 0;

	public static MapAccessResult Denied(MapAccessFailure failure, string requirementKey = "", int requiredValue = 0)
	{
		return new MapAccessResult(Allowed: false, failure, requirementKey, requiredValue);
	}

	public static MapAccessResult Granted(string consumedItemKey = "")
	{
		return new MapAccessResult(Allowed: true, MapAccessFailure.None, "", 0, consumedItemKey);
	}
}
