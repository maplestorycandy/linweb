using System;
using System.Collections.Generic;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed class GameSession
{
	public string HuntMap = "l1j_map_2005";

	public string LastHuntMap = "";

	public double LastHuntX;

	public double LastHuntY;

	public string TownKey = "town_aden";

	public PlayerBuild Build { get; }

	public Combatant Player { get; }

	public WarehouseState Warehouse { get; set; } = new WarehouseState();

	public string? PendingMapEntryLandmark { get; set; }

	public (double X, double Y)? PendingHuntSpawn { get; set; }

	public bool SuppressPetDeploymentOnce { get; set; }

	public bool PendingRestartFeelGood { get; set; }

	public HashSet<string> AutoCast { get; set; } = new HashSet<string>(StringComparer.Ordinal);

	public string?[] QuickItems { get; set; } = new string[24];

	public string?[] QuickSkills { get; set; } = new string[24];

	public int QuickPage { get; set; }

	public Godot.Vector2 FloatingQuickBarPos { get; set; } = Godot.Vector2.Zero;
	public Godot.Vector2 UtilityBarPos { get; set; } = Godot.Vector2.Zero;

	public string ActiveDollItemKey { get; set; } = "";
	public string ActiveDollItemUid { get; set; } = "";
	public double ActiveDollRemainingSeconds { get; set; } = 0.0;

	public bool FloatingQuickBarVisible { get; set; } = true;

	public HashSet<string> AutoUseItems { get; set; } = new HashSet<string>(StringComparer.Ordinal);
	public HashSet<string> FilteredItems { get; set; } = new HashSet<string>(StringComparer.Ordinal);

	public int AutoPotionHpPercent { get; set; } = 70;

	public int CompanionAutoHealSkillHpPercent { get; set; } = 70;

	public int CompanionAutoPotionHpPercent { get; set; } = 70;

	public int AutoSkillMpPercent { get; set; }

	public int AutoSkillHpPercent { get; set; } = 50;

	public string[] AttackPriorities { get; set; } = Array.Empty<string>();

	public string[] CompanionAttackPriorities { get; set; } = Array.Empty<string>();

	public HashSet<string> LootFilterItemKeys { get; set; } = new HashSet<string>(StringComparer.Ordinal);
	public bool ElfNightVisionEnabled { get; set; } = true;

	public MercenaryParty Party { get; set; }

	public CollectionState Collections { get; set; }

	public string Identity { get; set; } = Guid.NewGuid().ToString("N");

	public L1jUbSession? UltimateBattle { get; set; }

	public bool PvpEnabled { get; set; }

	public long LastPlayerKillUnixSeconds { get; set; }

	public long LastElfKillUnixSeconds { get; set; }

	public PetRoster Pets { get; set; } = new PetRoster();

	public ShopBuybackLedger Buyback { get; } = new ShopBuybackLedger();

	public GameSession(PlayerBuild build)
		: this(build, EngineAdapter.CreatePlayerCombatant(build))
	{
		TownKey = build.ReturnTown;
		GameData shared = GameDataProvider.Shared;
		BeginnerKitRules.Grant(shared, Player, build.ClassId);
		BeginnerKitRules.GrantCreationSkills(Player);
		CombatantBuilder.RefreshPlayer(Player, shared);
		Player.Hp = Player.MaxHp;
		Player.Mp = Player.MaxMp;
	}

	public GameSession(PlayerBuild build, Combatant player)
	{
		Build = build;
		Player = player;
		BeginnerKitRules.RemoveLegacyCreationSkill(Player);
		Party = new MercenaryParty(string.IsNullOrWhiteSpace(player.Key) ? "player" : player.Key);
		Collections = new CollectionState(GameDataProvider.Shared);
		CollectionRules.Attach(GameDataProvider.Shared, player, Collections);
	}

	public static GameSession CreateNewCharacter(PlayerBuild build, MapTopology startTopology)
	{
		var (num, num2) = L1jCharacterStartCatalog.ResolveDisplaySpawn(startTopology);
		return new GameSession(build)
		{
			HuntMap = "l1j_map_2005",
			PendingHuntSpawn = (num, num2),
			LastHuntMap = "l1j_map_2005",
			LastHuntX = num,
			LastHuntY = num2
		};
	}
}
