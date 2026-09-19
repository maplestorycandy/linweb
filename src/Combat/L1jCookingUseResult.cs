namespace IdleLineage.Combat;

public readonly record struct L1jCookingUseResult(bool Success, string Text, int ItemId = 0, int CookingType = -1, bool Special = false);
