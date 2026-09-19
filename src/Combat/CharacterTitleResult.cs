namespace IdleLineage.Combat;

public readonly record struct CharacterTitleResult(bool Success, CharacterTitleFailure Failure, string Title)
{
	public static CharacterTitleResult Failed(CharacterTitleFailure failure)
	{
		return new CharacterTitleResult(Success: false, failure, "");
	}
}
