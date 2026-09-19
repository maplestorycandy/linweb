using System.Collections.Generic;

namespace IdleLineage.App;

public sealed record PlayerBuild(string ClassId, string ClassName, string Avatar, string WeaponPrefix, bool Male, int Level)
{
	public string CharacterName { get; init; } = "";

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(CharacterName))
			{
				return CharacterName;
			}
			return ClassName;
		}
	}

	public IReadOnlyDictionary<string, int> Allocations { get; init; } = new Dictionary<string, int>();

	public string ReturnTown { get; init; } = "town_talking";

	public long StartingGold { get; init; } = 5000L;
}
