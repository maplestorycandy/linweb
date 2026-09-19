namespace IdleLineage.Combat;

public sealed class MercenaryContract
{
	public string CharacterKey { get; }

	public string CharacterBlob { get; internal set; }

	internal MercenaryContract(string characterKey, string characterBlob)
	{
		CharacterKey = characterKey;
		CharacterBlob = characterBlob;
	}
}
