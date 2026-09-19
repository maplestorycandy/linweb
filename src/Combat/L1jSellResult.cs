namespace IdleLineage.Combat;

public readonly record struct L1jSellResult(bool Success, L1jShopFailure Failure, string ItemKey, long Quantity, long GoldGained);
