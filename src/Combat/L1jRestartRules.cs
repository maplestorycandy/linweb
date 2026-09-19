using System;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jRestartRules
{
	public const int MainRestartFood = 40;

	public const int MainFoodMaximum = 225;

	public static double RestartSatiety => SatietyRules.Clamp(40.0);

	public static L1jRestartOutcome Apply(Combatant player, int destinationMapId)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		bool num = L1jHiddenValleyCatalog.GrantsRestartRefill(destinationMapId);
		player.Dead = false;
		if (num)
		{
			player.Hp = player.MaxHp;
			player.Mp = player.MaxMp;
		}
		else
		{
			player.Hp = Math.Clamp(player.Level, 1.0, player.MaxHp);
		}
		if (SatietyRules.UsesSatiety(player))
		{
			player.Satiety = RestartSatiety;
		}
		return new L1jRestartOutcome(num, player.Hp, player.Mp, player.Satiety);
	}
}
