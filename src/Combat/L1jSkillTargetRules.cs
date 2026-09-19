using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jSkillTargetRules
{
	public const int TargetToPc = 1;

	public const int TargetToNpc = 2;

	public const int TargetToClan = 4;

	public const int TargetToParty = 8;

	public const int TargetToPet = 16;

	public const int TargetToPlace = 32;

	private const int CharacterTargetMask = 31;

	public static bool RequiresManualCharacterTarget(IGameData data, string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		JsonObject jsonObject = data.Skill(skillId);
		if (jsonObject != null)
		{
			return RequiresManualCharacterTarget(jsonObject);
		}
		return false;
	}

	internal static bool RequiresManualCharacterTarget(JsonObject source)
	{
		L1jSkillFields l1jSkillFields = L1jSkillFields.TryRead(source["l1j"] as JsonObject);
		if (l1jSkillFields != null)
		{
			if (string.Equals(l1jSkillFields.Target, "buff", StringComparison.Ordinal) || l1jSkillFields.IsHeal || string.Equals(l1jSkillFields.Category, "heal", StringComparison.Ordinal))
			{
				return (l1jSkillFields.TargetTo & 0x1F) != 0;
			}
		}
		return false;
	}

	public static bool IsAttackSkill(IGameData data, string skillId)
	{
		if (data == null || string.IsNullOrWhiteSpace(skillId))
		{
			return false;
		}
		JsonObject jsonObject = data.Skill(skillId);
		if (jsonObject != null)
		{
			return IsAttackSkill(jsonObject);
		}
		return false;
	}

	public static bool IsAttackSkill(JsonObject source)
	{
		if (source == null)
		{
			return false;
		}
		string text = CombatSkill.ReadString(source, "type");
		if (string.Equals(text, "atk", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		L1jSkillFields l1jSkillFields = L1jSkillFields.TryRead(source["l1j"] as JsonObject);
		if (l1jSkillFields != null)
		{
			if (string.Equals(l1jSkillFields.Target, "attack", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if ((l1jSkillFields.Type & L1jSkillFields.TypeAttack) != 0)
			{
				return true;
			}
			if (!string.IsNullOrEmpty(l1jSkillFields.Category) && l1jSkillFields.Category.StartsWith("attack", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	internal static bool AllowsDeadCharacterTarget(JsonObject source)
	{
		L1jSkillFields l1jSkillFields = L1jSkillFields.TryRead(source["l1j"] as JsonObject);
		bool flag = l1jSkillFields != null;
		if (flag)
		{
			string category = l1jSkillFields.Category;
			bool flag2 = ((category == "restore" || category == "death") ? true : false);
			flag = flag2;
		}
		return flag;
	}

	public static bool AllowsCharacterTarget(JsonObject source, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(target, "target");

		if (IsAttackSkill(source))
		{
			if (!target.IsAlive)
			{
				return false;
			}
			if (target.Kind == CombatantKind.Mob)
			{
				return true;
			}
			if (target.Kind == CombatantKind.Player || target.Kind == CombatantKind.Ally)
			{
				return true;
			}
			return false;
		}

		L1jSkillFields l1jSkillFields = L1jSkillFields.TryRead(source["l1j"] as JsonObject);
		if (l1jSkillFields == null)
		{
			return false;
		}
		if (!string.Equals(l1jSkillFields.Target, "buff", StringComparison.Ordinal) && !l1jSkillFields.IsHeal && !string.Equals(l1jSkillFields.Category, "heal", StringComparison.Ordinal))
		{
			return false;
		}
		int targetTo = l1jSkillFields.TargetTo;
		CombatantKind kind = target.Kind;
		if ((uint)(kind - 3) <= 1u)
		{
			return (targetTo & 0x12) != 0;
		}
		if (MonsterCompanionRules.IsCompanion(target))
		{
			return (targetTo & 0xE) != 0;
		}
		if (target.Kind == CombatantKind.Mob)
		{
			return (targetTo & 2) != 0;
		}
		kind = target.Kind;
		if ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false)
		{
			return (targetTo & 0xD) != 0;
		}
		return false;
	}

	internal static bool AllowsFriendlyMonsterCompanion(JsonObject source, Combatant target)
	{
		if (MonsterCompanionRules.IsCompanion(target))
		{
			return AllowsCharacterTarget(source, target);
		}
		return false;
	}
}
