namespace IdleLineage.App;

public sealed record ClassDef(string Id, string Name, string MaleAvatar, string FemaleAvatar, string Weapon, string ReturnTown, int BonusPoints, int Str, int Dex, int Con, int Int, int Wis, int Cha, string Description)
{
	public string Avatar(bool male)
	{
		if (!male)
		{
			return FemaleAvatar;
		}
		return MaleAvatar;
	}

	public int BaseStat(string stat)
	{
		return stat switch
		{
			"str" => Str, 
			"dex" => Dex, 
			"con" => Con, 
			"int" => Int, 
			"wis" => Wis, 
			"cha" => Cha, 
			_ => 0, 
		};
	}
}
