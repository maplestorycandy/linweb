using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal sealed class ConvertSkill
{
	public string Id { get; set; } = "";

	public int MpCost { get; set; }

	public int HpCost { get; set; }

	public int MpGain { get; set; }

	public bool Drain { get; set; }

	public static bool TryRead(string id, JsonObject source, out ConvertSkill? skill)
	{
		if (!string.Equals(CombatSkill.ReadString(source, "type"), "convert", StringComparison.Ordinal))
		{
			skill = null;
			return false;
		}
		ConvertSkill convertSkill = new ConvertSkill
		{
			Id = id,
			MpCost = Math.Max(0, CombatSkill.ReadInt(source, "mp"))
		};
		ConvertSkill convertSkill2 = convertSkill;
		JsonObject jsonObject = source["l1j"] as JsonObject;
		bool flag = jsonObject != null;
		if (flag)
		{
			int num = CombatSkill.ReadInt(jsonObject, "officialId");
			bool flag2 = ((num == 130 || num == 146) ? true : false);
			flag = flag2;
		}
		convertSkill2.HpCost = (flag ? Math.Max(0, CombatSkill.ReadInt(jsonObject, "hpConsume")) : Math.Max(0, CombatSkill.ReadInt(source, "hpCost")));
		ConvertSkill convertSkill3 = convertSkill;
		int mpGain = ((!(source["l1j"] is JsonObject source2)) ? Math.Max(0, CombatSkill.ReadInt(source, "mpGain")) : (CombatSkill.ReadInt(source2, "officialId") switch
		{
			130 => 2, 
			146 => 80, 
			_ => Math.Max(0, CombatSkill.ReadInt(source, "mpGain")), 
		}));
		if (string.Equals(id, "sk_elf_soul", StringComparison.OrdinalIgnoreCase) || string.Equals(CombatSkill.ReadString(source, "n"), "魂體轉換", StringComparison.Ordinal))
		{
			mpGain = Math.Max(80, mpGain);
		}
		convertSkill3.MpGain = mpGain;
		convertSkill.Drain = CombatSkill.ReadBool(source, "drain");
		skill = convertSkill;
		return true;
	}
}
