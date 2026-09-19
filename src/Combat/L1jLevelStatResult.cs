namespace IdleLineage.Combat;

public readonly record struct L1jLevelStatResult(bool Success, L1jLevelStatFailure Failure, string AttributeKey, int AttributeValue, int RemainingPoints);
