namespace IdleLineage.Combat;

public readonly record struct RoiBagResult(bool Success, RoiBagFailure Failure, string RewardItemKey = "", int RewardItemId = 0, long RewardQuantity = 0L);
