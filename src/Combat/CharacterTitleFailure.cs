namespace IdleLineage.Combat;

public enum CharacterTitleFailure
{
	None,
	Empty,
	TooLong,
	ClanLeaderBelowLevel10,
	ClanMemberCannotSelfTitle,
	IndependentBelowLevel40,
	NotPlayer
}
