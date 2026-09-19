namespace IdleLineage.Combat;

public readonly record struct L1jBuyResult(bool Success, L1jShopFailure Failure, string ItemKey, long Quantity, long GoldSpent);
