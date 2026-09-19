using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class AmmunitionRules
{
	public const string ArrowSlot = "arrow";

	public static bool RequiresArrow(IGameData data, Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if ((attacker.Kind != CombatantKind.Player && !HostilePlayerRules.IsHostilePlayer(attacker)) || !attacker.D.UsesRangedAttack)
		{
			return false;
		}
		JsonObject jsonObject = MainWeapon(data, attacker);
		if (jsonObject != null)
		{
			return !CombatSkill.ReadBool(jsonObject, "shahaBow");
		}
		return true;
	}

	public static bool UsesStings(IGameData data, Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		JsonObject jsonObject = MainWeapon(data, attacker);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "isGauntlet");
		}
		return false;
	}

	public static bool CanLaunchBasicShot(IGameData data, Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (!RequiresArrow(data, attacker))
		{
			return true;
		}
		ItemStack ammunition;
		JsonObject definition;
		return TryGetAmmunition(data, attacker, out ammunition, out definition);
	}

	public static void ApplyAttackDice(IGameData data, Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (attacker.D.UsesRangedAttack && TryGetAmmunition(data, attacker, out ItemStack _, out JsonObject definition))
		{
			int attackDiceSmall = Math.Max(1, CombatSkill.ReadInt(definition, "dmgS"));
			int attackDiceLarge = Math.Max(1, CombatSkill.ReadInt(definition, "dmgL"));
			attacker.D.AttackDiceSmall = attackDiceSmall;
			attacker.D.AttackDiceLarge = attackDiceLarge;
		}
	}

	public static bool ConsumeCommittedBasicShot(IGameData data, Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (!RequiresArrow(data, attacker))
		{
			return true;
		}
		if (!TryGetAmmunition(data, attacker, out ItemStack ammunition, out JsonObject definition))
		{
			return false;
		}
		if (CombatSkill.ReadBool(definition, "noConsume"))
		{
			return true;
		}
		ammunition.Quantity--;
		if (ammunition.Quantity > 0)
		{
			return true;
		}
		attacker.EquippedItems.Remove("arrow");
		CombatEquipment.SyncLegacyView(attacker);
		CombatantBuilder.RefreshPlayer(attacker, data);
		return true;
	}

	private static bool TryGetAmmunition(IGameData data, Combatant attacker, out ItemStack? ammunition, out JsonObject? definition)
	{
		definition = null;
		if (!attacker.EquippedItems.TryGetValue("arrow", out ammunition) || ammunition == null || ammunition.Quantity <= 0)
		{
			return false;
		}
		definition = data.Item(ammunition.ItemKey);
		string name = (UsesStings(data, attacker) ? "isSting" : "isArrow");
		if (definition != null)
		{
			return CombatSkill.ReadBool(definition, name);
		}
		return false;
	}

	private static JsonObject? MainWeapon(IGameData data, Combatant attacker)
	{
		if (attacker.EquippedItems.TryGetValue("wpn", out ItemStack value))
		{
			return data.Item(value.ItemKey);
		}
		return data.Item(attacker.MainWeaponId);
	}
}
