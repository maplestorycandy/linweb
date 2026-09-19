namespace IdleLineage.Combat;

public readonly record struct L1jElixirResult(bool Success, L1jElixirFailure Failure, string AttributeKey, string AttributeName, int AttributeValue, int ElixirStatus);
