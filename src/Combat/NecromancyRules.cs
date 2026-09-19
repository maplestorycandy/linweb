using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class NecromancyRules
{
	public const string BookItemId = "relic_necro_book";

	public const string AnimateDeadSkillId = "sk_zombie";

	public const string SkeletonContractId = "_necro_skeleton";

	public const string SkeletonDisplayName = "骷髏";

	public const string SkeletonAvatar = "骷髏召喚物";

	public static bool ReplacesAnimateDead(IGameData? data, Combatant owner, string skillId)
	{
		if (string.Equals(skillId, "sk_zombie", StringComparison.Ordinal))
		{
			return IsBookEquipped(data, owner);
		}
		return false;
	}

	public static bool IsBookEquipped(IGameData? data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (data == null)
		{
			return false;
		}
		return EquippedItemKeys(owner).Any(delegate(string itemKey)
		{
			JsonObject jsonObject = data.Item(itemKey);
			return jsonObject != null && CombatSkill.ReadBool(jsonObject, "necroBook");
		});
	}

	public static bool PassiveEnabled(IGameData? data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		CombatantKind kind = owner.Kind;
		bool flag = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
		if (flag && owner.IsAlive && IsBookEquipped(data, owner))
		{
			if (!owner.LearnedSkills.Contains("sk_zombie"))
			{
				return owner.GrantedSkills.Contains("sk_zombie");
			}
			return true;
		}
		return false;
	}

	public static double TeamHealPercent(IGameData? data, Combatant holder)
	{
		ArgumentNullException.ThrowIfNull(holder, "holder");
		if (data == null || !holder.IsAlive)
		{
			return 0.0;
		}
		return EquippedItemKeys(holder).Select(delegate(string itemKey)
		{
			JsonObject jsonObject = data.Item(itemKey);
			return (jsonObject == null) ? 0.0 : Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "killTeamHealPct"));
		}).DefaultIfEmpty(0.0).Max();
	}

	public static int MaximumSkeletons(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Math.Max(1, ReadTableInt(data, "NECRO_SKELETON_MAX", 6));
	}

	public static bool TryCreateSkeletonPlan(IGameData data, Combatant owner, out SummonUnitPlan? plan)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		plan = null;
		if (!(data.Table("NECRO_SKELETON_TIERS") is JsonArray source) || !(data.Table("SUMMON_TIERS") is JsonArray jsonArray))
		{
			return false;
		}
		JsonObject jsonObject = source.OfType<JsonObject>().FirstOrDefault(delegate(JsonObject tier)
		{
			int num20 = CombatSkill.ReadInt(tier, "min");
			double num21 = ReadNullableMaximum(tier);
			return owner.Level >= num20 && (double)owner.Level <= num21;
		});
		if (jsonObject == null)
		{
			return false;
		}
		string referenceName = CombatSkill.ReadString(jsonObject, "ref");
		JsonObject jsonObject2 = null;
		JsonObject jsonObject3 = null;
		int val = -1;
		for (int num = 0; num < jsonArray.Count; num++)
		{
			if (jsonArray[num] is JsonObject jsonObject4 && jsonObject4["mobs"] is JsonArray source2)
			{
				JsonObject jsonObject5 = source2.OfType<JsonObject>().FirstOrDefault((JsonObject candidate) => string.Equals(CombatSkill.ReadString(candidate, "n"), referenceName, StringComparison.Ordinal));
				if (jsonObject5 != null)
				{
					jsonObject2 = jsonObject4;
					jsonObject3 = jsonObject5;
					val = num;
					break;
				}
			}
		}
		if (jsonObject2 == null || jsonObject3 == null)
		{
			return false;
		}
		double num2 = Math.Max(0.0, owner.D.Cha);
		double num3 = MedianMobHp(jsonObject2);
		double num4 = Math.Max(1.0, CombatSkill.ReadDouble(jsonObject3, "hp", num3));
		double num5 = Math.Pow(num3 / num4, 0.35);
		double num6 = Math.Max(0.01, CombatSkill.ReadDouble(jsonObject2, "premium", 1.0));
		double num7 = (39.0 + 0.09 * num2 * (double)Math.Max(1, owner.Level)) * (1.0 + (double)Math.Max(0, val) * 0.06) * num6 * num5;
		int num8 = Math.Max(1, CombatSkill.ReadInt(jsonObject2, "cap"));
		double num9 = Math.Max(1.0, CombatSkill.ReadDouble(jsonObject3, "aspd", 20.0));
		double num10 = num7 / (double)num8 * (num9 / 10.0);
		double num11 = Math.Round(num10 * 0.55);
		int num12 = Math.Max(1, (int)Math.Round((num10 - num11) * 2.0));
		double num13 = EquippedItemKeys(owner).Select(delegate(string itemKey)
		{
			JsonObject jsonObject6 = data.Item(itemKey);
			return (jsonObject6 == null) ? 0.0 : CombatSkill.ReadDouble(jsonObject6, "summonMdmg");
		}).Sum();
		bool flag = false;
		double num14 = (flag ? 1.2 : 1.0) * (1.0 + Math.Min(12.0, Math.Max(0.0, owner.D.MagicDamage + num13)) / 80.0);
		double num15 = (num11 + (double)(num12 + 1) / 2.0) * num14 * (10.0 / num9) * Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "ratio", 1.0));
		double num16 = Math.Max(1.0, ReadTableDouble(data, "NECRO_SKELETON_ASPD", 10.0));
		double num17 = Math.Max(1.0, num15 * (num16 / 10.0));
		double num18 = Math.Round(num17 * 0.55);
		int attackDice = Math.Max(1, (int)Math.Round((num17 - num18) * 2.0 - 1.0));
		int num19 = Math.Max(1, CombatSkill.ReadInt(jsonObject, "lv"));
		double meleeHit = (double)Math.Max(1, owner.Level) + Math.Floor((double)Math.Max(1, owner.Level) * 0.75 + num2 * 0.35) + Math.Floor((double)num19 / 8.0) + (double)Math.Max(0, val) + (double)(flag ? 5 : 0) + ReadTableDouble(data, "NECRO_SKELETON_HIT_BONUS", 5.0);
		plan = new SummonUnitPlan("骷髏", num19, Math.Max(1.0, CombatSkill.ReadDouble(jsonObject, "hp")), num16 / 10.0, 12.0, 10.0 - Math.Floor((double)num19 / 4.0), Math.Floor((double)num19 / 10.0), meleeHit, Math.Max(0.0, num18), attackDice, "none", null, Array.Empty<SummonProcProfile>(), null, "gfx:30");
		return true;
	}

	private static IEnumerable<string> EquippedItemKeys(Combatant owner)
	{
		return owner.EquippedItems.Values.Select((ItemStack stack) => stack.ItemKey).Concat(owner.Equip.Values.OfType<string>()).Distinct<string>(StringComparer.Ordinal);
	}

	private static double MedianMobHp(JsonObject tier)
	{
		double[] array = (from mob in (tier["mobs"] as JsonArray)?.OfType<JsonObject>()
			select Math.Max(1.0, CombatSkill.ReadDouble(mob, "hp", 1.0)) into value
			orderby value
			select value).ToArray() ?? Array.Empty<double>();
		if (array.Length != 0)
		{
			return array[array.Length / 2];
		}
		return 1.0;
	}

	private static double ReadNullableMaximum(JsonObject source)
	{
		if (!(source["max"] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return double.PositiveInfinity;
		}
		return value;
	}

	private static int ReadTableInt(IGameData data, string tableName, int fallback)
	{
		return (int)Math.Floor(ReadTableDouble(data, tableName, fallback));
	}

	private static double ReadTableDouble(IGameData data, string tableName, double fallback)
	{
		if (!(data.Table(tableName) is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return fallback;
		}
		return value;
	}
}
