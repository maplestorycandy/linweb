using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jMobAggroRules
{
	private static readonly IReadOnlyDictionary<int, string> ClassByGfxSelector = new Dictionary<int, string>
	{
		[0] = "royal",
		[1] = "knight",
		[2] = "elf",
		[3] = "mage",
		[4] = "dark"
	};

	public const int PlayerRecognizeRangeCells = 20;

	public const double FamilyLinkRange = 960.0;

	public static bool AttacksPolymorphedPlayer(IGameData? data, Combatant mob)
	{
		JsonObject jsonObject = Definition(data, mob);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "agroSosc");
		}
		return false;
	}

	public static bool DetectsInvisiblePlayer(IGameData? data, Combatant mob)
	{
		JsonObject jsonObject = Definition(data, mob);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "agroCoi");
		}
		return false;
	}

	public static int FamilyAggroMode(IGameData? data, Combatant mob)
	{
		JsonObject jsonObject = Definition(data, mob);
		if (jsonObject == null)
		{
			return 0;
		}
		return CombatSkill.ReadInt(jsonObject, "agroFamily");
	}

	public static IReadOnlyList<int> SpecificPlayerGfxSelectors(IGameData? data, Combatant mob)
	{
		JsonObject jsonObject = Definition(data, mob);
		if (jsonObject == null)
		{
			return Array.Empty<int>();
		}
		List<int> list = new List<int>(2);
		string[] array = new string[2] { "agroGfxId1", "agroGfxId2" };
		foreach (string text in array)
		{
			if (jsonObject[text] != null)
			{
				int num = CombatSkill.ReadInt(jsonObject, text);
				if (num >= 0 && !list.Contains(num))
				{
					list.Add(num);
				}
			}
		}
		return list;
	}

	public static bool CanAcquireOrKeep(IGameData? data, Combatant mob, Combatant candidate, bool alreadyKnown)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		ArgumentNullException.ThrowIfNull(candidate, "candidate");
		if (alreadyKnown)
		{
			return true;
		}
		if (mob.L1jWorldNpcImpl == "L1Guard")
		{
			if (candidate.Kind == CombatantKind.Player)
			{
				return candidate.WantedByGuards;
			}
			return false;
		}
		if (mob.L1jWorldNpcImpl == "L1Guardian")
		{
			if (candidate.Kind == CombatantKind.Player)
			{
				if (ElfElementRules.IsElf(candidate))
				{
					return candidate.WantedForElfGuardians;
				}
				return true;
			}
			return false;
		}
		if (mob.TrainingScarecrow)
		{
			return false;
		}
		if (candidate.TrainingScarecrow)
		{
			return false;
		}
		bool flag = candidate.Kind == CombatantKind.Player;
		JsonObject jsonObject = Definition(data, mob);
		int num = ((jsonObject != null) ? CombatSkill.ReadInt(jsonObject, "npcid") : 0);
		if (flag && num == 45215 && candidate.Alignment <= -1.0)
		{
			return true;
		}
		if (flag && mob.Passive && !AttacksPolymorphedPlayer(data, mob) && SpecificPlayerGfxSelectors(data, mob).Count == 0 && candidate.Alignment < -1000.0)
		{
			return true;
		}
		if (StealthRules.IsInvisible(data, candidate) && !DetectsInvisiblePlayer(data, mob))
		{
			return false;
		}
		PolymorphForm polymorphForm = ((flag && data != null) ? PolymorphRules.ActiveForm(data, candidate) : null);
		PolymorphForm polymorphForm2 = ((flag && data != null) ? PolymorphRules.CurrentForm(data, candidate) : null);
		if (flag && MatchesSpecificPlayerGfx(data, mob, candidate, polymorphForm2?.Gfx))
		{
			return true;
		}
		if ((object)polymorphForm != null)
		{
			return AttacksPolymorphedPlayer(data, mob);
		}
		if (!mob.Passive)
		{
			return true;
		}
		return false;
	}

	private static bool MatchesSpecificPlayerGfx(IGameData? data, Combatant mob, Combatant candidate, int? effectiveGfx)
	{
		foreach (int item in SpecificPlayerGfxSelectors(data, mob))
		{
			if (item <= 4)
			{
				if (!effectiveGfx.HasValue && ClassByGfxSelector.TryGetValue(item, out string value) && string.Equals(ClassKitRegistry.NormalizeClassId(candidate.ClassId), value, StringComparison.Ordinal))
				{
					return true;
				}
			}
			else if (effectiveGfx == item)
			{
				return true;
			}
		}
		return false;
	}

	public static bool SupportsAttackedFamily(IGameData? data, Combatant recipient, Combatant attacked)
	{
		ArgumentNullException.ThrowIfNull(recipient, "recipient");
		ArgumentNullException.ThrowIfNull(attacked, "attacked");
		int num = FamilyAggroMode(data, recipient);
		if (num <= 0)
		{
			return false;
		}
		if (num > 1)
		{
			return true;
		}
		string text = Family(data, recipient);
		string b = Family(data, attacked);
		if (text.Length > 0)
		{
			return string.Equals(text, b, StringComparison.Ordinal);
		}
		return false;
	}

	private static string Family(IGameData? data, Combatant mob)
	{
		JsonObject jsonObject = Definition(data, mob);
		if (jsonObject == null)
		{
			return "";
		}
		return CombatSkill.ReadString(jsonObject, "family");
	}

	private static JsonObject? Definition(IGameData? data, Combatant mob)
	{
		return data?.Mob(mob.Avatar);
	}
}
