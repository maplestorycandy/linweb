using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jShopCatalog
{
	private sealed record Loaded(IReadOnlyDictionary<int, L1jShopDefinition> Shops, IReadOnlyList<L1jNpcSpawn> Spawns, IReadOnlyDictionary<string, int> CheapestSellUnitPrice);

	public const string TableName = "L1J_NPC_SHOPS";

	private static readonly ConditionalWeakTable<IGameData, Loaded> Cache = new ConditionalWeakTable<IGameData, Loaded>();

	public const int UnpricedBuybackUnitPrice = 1;

	public static IReadOnlyDictionary<int, L1jShopDefinition> Shops(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build).Shops;
	}

	public static IReadOnlyList<L1jNpcSpawn> Spawns(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build).Spawns;
	}

	public static bool TryResolveShopNpcId(IGameData data, string displayName, out int npcId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		npcId = 0;
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return false;
		}
		if (string.Equals(displayName, "戴捷爾", StringComparison.Ordinal) || string.Equals(displayName, "一元福利商人", StringComparison.Ordinal) || string.Equals(displayName, "福利商人", StringComparison.Ordinal))
		{
			npcId = 888001;
			return true;
		}
		if (!(data.Table("L1J_NPC_SHOPS") is JsonObject jsonObject) || !(jsonObject["shopByName"] is JsonObject jsonObject2) || !(jsonObject2[displayName] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return false;
		}
		npcId = value;
		return true;
	}

	public static IReadOnlyList<L1jShopItem> SellList(IGameData data, int npcId)
	{
		if (!Shops(data).TryGetValue(npcId, out L1jShopDefinition value))
		{
			return Array.Empty<L1jShopItem>();
		}
		return value.Items.Where((L1jShopItem item) => item.SellPrice >= 0 && item.ItemKey != null).ToArray();
	}

	public static bool IsBuybackShop(IGameData data, int npcId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!Shops(data).ContainsKey(npcId))
		{
			return L1jNpcSkillLearningRules.IsMainMagicInstructor(npcId);
		}
		return true;
	}

	public static int BuyPriceOf(IGameData data, int npcId, string itemKey, ItemBlessing blessing = ItemBlessing.Normal)
	{
		ArgumentNullException.ThrowIfNull(itemKey, "itemKey");
		Shops(data).TryGetValue(npcId, out L1jShopDefinition value);
		if ((object)value == null && !IsBuybackShop(data, npcId))
		{
			return -1;
		}
		L1jShopItem l1jShopItem = value?.Items.FirstOrDefault((L1jShopItem item) => string.Equals(item.ItemKey, itemKey, StringComparison.Ordinal) && item.Blessing == blessing && item.BuyPrice >= 0);
		if ((object)l1jShopItem != null)
		{
			return l1jShopItem.BuyPrice / Math.Max(1, l1jShopItem.PackCount);
		}
		return UniversalBuybackPrice(data, itemKey);
	}

	public static int UniversalBuybackPrice(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(itemKey, "itemKey");
		if (Cache.GetValue(data, Build).CheapestSellUnitPrice.TryGetValue(itemKey, out var value))
		{
			return value / 2;
		}
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return -1;
		}
		double num = CombatSkill.ReadDouble(jsonObject, "p");
		if (!(num >= 2.0))
		{
			return 1;
		}
		return (int)Math.Min(2147483647.0, Math.Floor(num) / 2.0);
	}

	private static Loaded Build(IGameData data)
	{
		if (!(data.Table("L1J_NPC_SHOPS") is JsonObject jsonObject))
		{
			throw new InvalidDataException("L1J_NPC_SHOPS table failed to load.");
		}
		Dictionary<int, L1jShopDefinition> dictionary = new Dictionary<int, L1jShopDefinition>();
		foreach (KeyValuePair<string, JsonNode> item in jsonObject["shops"].AsObject())
		{
			item.Deconstruct(out var key, out var value);
			JsonObject jsonObject2 = value.AsObject();
			L1jShopItem[] items = (from entry in jsonObject2["items"].AsArray()
				select new L1jShopItem(entry["id"].GetValue<int>(), (entry["key"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value4)) ? value4 : null, ReadBlessing(entry["blessing"]?.GetValue<string>()), entry["order"].GetValue<int>(), entry["sell"].GetValue<int>(), Math.Max(1, entry["pack"].GetValue<int>()), entry["buy"].GetValue<int>())).ToArray();
			int shopId = (int.TryParse(key, out int sKeyId) && sKeyId > 0) ? sKeyId : jsonObject2["npcId"].GetValue<int>();
			dictionary[shopId] = new L1jShopDefinition(shopId, jsonObject2["name"].GetValue<string>(), jsonObject2["impl"].GetValue<string>(), items);
		}
		int ReadInt(JsonObject r, string k, int def = 0) => (r.TryGetPropertyValue(k, out var n) && n is JsonValue v && v.TryGetValue<int>(out int val)) ? val : def;
		string ReadStr(JsonObject r, string k, string def = "") => (r.TryGetPropertyValue(k, out var n) && n is JsonValue v && v.TryGetValue<string>(out string val)) ? val : def;
		bool ReadBool(JsonObject r, string k, bool def = false) => (r.TryGetPropertyValue(k, out var n) && n is JsonValue v && v.TryGetValue<bool>(out bool val)) ? val : def;

		L1jNpcSpawn[] array = (from node in jsonObject["spawns"].AsArray()
			select node.AsObject() into row
			select new L1jNpcSpawn(
				ReadInt(row, "npcId"),
				ReadStr(row, "name"),
				ReadStr(row, "impl", "L1Merchant"),
				ReadInt(row, "gfx"),
				ReadStr(row, "mapKey"),
				ReadInt(row, "cellX"),
				ReadInt(row, "cellY"),
				ReadInt(row, "heading", 4),
				ReadBool(row, "hasShop", false),
				ReadInt(row, "level", 1),
				ReadInt(row, "hp", 100),
				ReadInt(row, "mp", 100),
				ReadInt(row, "ac", 10),
				ReadInt(row, "str", 10),
				ReadInt(row, "con", 10),
				ReadInt(row, "dex", 10),
				ReadInt(row, "wis", 10),
				ReadInt(row, "int", 10),
				ReadInt(row, "mr", 0),
				ReadInt(row, "exp", 0),
				ReadInt(row, "lawful", 0),
				ReadStr(row, "size", "small"),
				ReadInt(row, "ranged", 1),
				ReadInt(row, "moveIntervalMs", 640),
				ReadInt(row, "attackIntervalMs", 900),
				ReadBool(row, "aggressive", false),
				ReadBool(row, "detectInvisible", false),
				ReadStr(row, "family", ""),
				ReadInt(row, "damageReduction", 0)
			)).ToArray();

		// Custom 1-dollar all-item merchant in Giran at (926, 675)
		List<L1jShopItem> allCustomItems = new List<L1jShopItem>();
		List<L1jShopItem> skillBookItems = new List<L1jShopItem>();
		List<L1jShopItem> weaponItems = new List<L1jShopItem>();
		List<L1jShopItem> armorItems = new List<L1jShopItem>();
		List<L1jShopItem> scrollItems = new List<L1jShopItem>();
		List<L1jShopItem> consumableItems = new List<L1jShopItem>();
		List<L1jShopItem> etcItemItems = new List<L1jShopItem>();

		int orderIdx = 1;
		int l1jIdCounter = 950000;

		// Core scrolls placed at the very top (including complete set of polymorph scrolls & poly control ring)
		L1jShopItem accPolyRing = new L1jShopItem(20281, "acc_117", ItemBlessing.Normal, orderIdx++, 1, 1, 1);
		L1jShopItem[] coreScrolls = new L1jShopItem[]
		{
			new L1jShopItem(40088, "scroll_poly", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(140088, "scroll_poly_b", ItemBlessing.Blessed, orderIdx++, 1, 1, 1),
			accPolyRing,
			new L1jShopItem(49149, "l1j_item_49149", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49150, "l1j_item_49150", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49151, "l1j_item_49151", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49152, "l1j_item_49152", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49153, "l1j_item_49153", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49154, "l1j_item_49154", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49155, "l1j_item_49155", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49156, "l1j_item_49156", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49157, "l1j_item_49157", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(49159, "l1j_item_49159", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(40087, "scroll_weapon", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(140087, "scroll_weapon_b", ItemBlessing.Blessed, orderIdx++, 1, 1, 1),
			new L1jShopItem(240087, "scroll_weapon_c", ItemBlessing.Cursed, orderIdx++, 1, 1, 1),
			new L1jShopItem(40074, "scroll_armor", ItemBlessing.Normal, orderIdx++, 1, 1, 1),
			new L1jShopItem(140074, "scroll_armor_b", ItemBlessing.Blessed, orderIdx++, 1, 1, 1),
			new L1jShopItem(240074, "scroll_armor_c", ItemBlessing.Cursed, orderIdx++, 1, 1, 1),
		};
		foreach (L1jShopItem cs in coreScrolls)
		{
			allCustomItems.Add(cs);
			scrollItems.Add(cs);
		}
		armorItems.Add(accPolyRing);

		// 魔法結晶體 (1000顆 / 1元)
		L1jShopItem crystalItem = new L1jShopItem(41246, "l1j_item_41246", ItemBlessing.Normal, orderIdx++, 1, 1000, 1);
		allCustomItems.Add(crystalItem);
		consumableItems.Add(crystalItem);
		etcItemItems.Add(crystalItem);

		if (data.Items is JsonObject allItemsNode)
		{
			foreach (KeyValuePair<string, JsonNode?> kvp in allItemsNode)
			{
				string itemKey = kvp.Key;
				if (itemKey is "scroll_weapon" or "scroll_weapon_b" or "scroll_weapon_c" or "scroll_armor" or "scroll_armor_b" or "scroll_armor_c" or "l1j_item_41246" or "scroll_poly" or "scroll_poly_b" or "acc_117" or "l1j_item_49149" or "l1j_item_49150" or "l1j_item_49151" or "l1j_item_49152" or "l1j_item_49153" or "l1j_item_49154" or "l1j_item_49155" or "l1j_item_49156" or "l1j_item_49157" or "l1j_item_49159") continue;
				if (kvp.Value is not JsonObject itemObj) continue;

				string type = itemObj["type"]?.GetValue<string>() ?? "";
				string name = itemObj["n"]?.GetValue<string>() ?? "";
				string slot = itemObj["slot"]?.GetValue<string>() ?? "";
				string sk = itemObj["sk"]?.GetValue<string>() ?? "";
				string eff = itemObj["eff"]?.GetValue<string>() ?? "";
				int l1jId = (itemObj["l1jItemId"] is JsonValue jv && jv.TryGetValue<int>(out int idVal)) ? idVal : ++l1jIdCounter;

				// Strictly genuine skill books only
				bool isSkillBook = type == "skillbk" || itemKey.StartsWith("bk_", StringComparison.Ordinal)
					|| (!string.IsNullOrEmpty(sk) && (name.Contains("魔法書") || name.Contains("水晶") || name.Contains("技術書") || name.Contains("書板") || name.Contains("記憶水晶") || name.Contains("精靈水晶") || name.Contains("印記") || name.Contains("石刻")));

				if (isSkillBook)
				{
					if (type is "arm" or "wpn" or "acc" or "scroll" || slot is "weapon" or "twohand" or "helm" or "armor" or "cloak" or "glove" or "boots" or "shield"
						|| name.Contains("盔甲") || name.Contains("手套") || name.Contains("臂甲") || name.Contains("球") || name.Contains("牙") || name.Contains("傳送卷軸"))
					{
						isSkillBook = false;
					}
				}

				bool isWeapon = !isSkillBook && (type == "wpn" || slot == "weapon" || slot == "twohand");

				bool isArmor = !isSkillBook && !isWeapon && (
					type == "arm" || type == "acc" || slot is "helm" or "armor" or "cloak" or "glove"
					or "boots" or "shield" or "t" or "ring" or "amulet" or "belt" or "earring" or "guarder"
				);

				bool isScroll = !isSkillBook && !isWeapon && !isArmor && (
					type == "scroll" || itemKey.StartsWith("scroll_", StringComparison.Ordinal)
					|| name.Contains("卷軸") || name.Contains("咒語書") || name.Contains("傳送卷")
					|| name.Contains("軸") || itemKey.Contains("scroll")
				);

				bool isConsumable = !isSkillBook && !isWeapon && !isArmor && !isScroll && (
					type == "pot" || eff is "heal" or "haste" or "brave" or "wis" or "cure" or "food" or "buff"
					|| name.Contains("藥水") || name.Contains("治癒劑") || name.Contains("湯") || name.Contains("肉")
					|| name.Contains("煎餅") || name.Contains("乳酪") || name.Contains("沙拉") || name.Contains("餅乾")
					|| name.Contains("蛋糕")
					|| name.Contains("惡魔之血") || (name.Contains("水") && (name.Contains("綠") || name.Contains("勇") || name.Contains("慎") || name.Contains("紅") || name.Contains("白") || name.Contains("澄")))
				);

				bool isEtc = !isSkillBook && !isWeapon && !isArmor && !isScroll && !isConsumable;

				L1jShopItem shopItem = new L1jShopItem(
					L1jItemId: l1jId,
					ItemKey: itemKey,
					Blessing: ItemBlessing.Normal,
					Order: orderIdx++,
					SellPrice: 1,
					PackCount: 1,
					BuyPrice: 1
				);

				allCustomItems.Add(shopItem);
				if (isSkillBook) skillBookItems.Add(shopItem);
				else if (isWeapon) weaponItems.Add(shopItem);
				else if (isArmor) armorItems.Add(shopItem);
				else if (isScroll) scrollItems.Add(shopItem);
				else if (isConsumable) consumableItems.Add(shopItem);
				else if (isEtc) etcItemItems.Add(shopItem);
			}
		}

		if (!dictionary.ContainsKey(888001)) dictionary[888001] = new L1jShopDefinition(888001, "福利商人 · 全部商品 (1元)", "L1Merchant", allCustomItems.ToArray());
		if (!dictionary.ContainsKey(888002)) dictionary[888002] = new L1jShopDefinition(888002, "福利商人 · 技能書籍 (1元)", "L1Merchant", skillBookItems.ToArray());
		if (!dictionary.ContainsKey(888003)) dictionary[888003] = new L1jShopDefinition(888003, "福利商人 · 全部武器 (1元)", "L1Merchant", weaponItems.ToArray());
		if (!dictionary.ContainsKey(888004)) dictionary[888004] = new L1jShopDefinition(888004, "福利商人 · 全部防具 (1元)", "L1Merchant", armorItems.ToArray());
		if (!dictionary.ContainsKey(888005)) dictionary[888005] = new L1jShopDefinition(888005, "福利商人 · 全部卷軸 (1元)", "L1Merchant", scrollItems.ToArray());
		if (!dictionary.ContainsKey(888006)) dictionary[888006] = new L1jShopDefinition(888006, "福利商人 · 消耗品專區 (1元)", "L1Merchant", consumableItems.ToArray());
		if (!dictionary.ContainsKey(888007)) dictionary[888007] = new L1jShopDefinition(888007, "福利商人 · 常用道具 (1元)", "L1Merchant", etcItemItems.ToArray());

		List<L1jNpcSpawn> spawnList = new List<L1jNpcSpawn>(array);

		string ResolveNpcName(int npcId, string fallback)
		{
			var existing = spawnList.FirstOrDefault(s => s.NpcId == npcId && !string.IsNullOrWhiteSpace(s.Name));
			return existing?.Name ?? fallback;
		}

		void EnsureSpawn(int npcId, string defaultName, string impl, int gfx, string mapKey, int cellX, int cellY, int heading, bool hasShop)
		{
			bool alreadyExists = spawnList.Any(s => s.NpcId == npcId && string.Equals(s.MapKey, mapKey, StringComparison.OrdinalIgnoreCase) && Math.Abs(s.CellX - cellX) <= 3 && Math.Abs(s.CellY - cellY) <= 3);
			if (!alreadyExists)
			{
				string name = ResolveNpcName(npcId, defaultName);
				spawnList.Add(new L1jNpcSpawn(npcId, name, impl, gfx, mapKey, cellX, cellY, heading, hasShop));
			}
		}

		// 1. 銀騎士之村 / 奇岩 (mainland_south) 廣場
		EnsureSpawn(888001, "一元福利商人^戴捷爾", "L1Merchant", 2400, "mainland_south", 926, 675, 4, true);
		EnsureSpawn(91101, "天上聖母 · 媽祖", "L1Teleporter", 7348, "mainland_south", 928, 677, 4, false);
		EnsureSpawn(80058, "毀滅的說話之島", "L1Teleporter", 5460, "mainland_south", 931, 669, 4, false);
		EnsureSpawn(980062, "被襲擊的成長之島", "L1Teleporter", 5456, "mainland_south", 932, 663, 4, false);

		// 2. 說話之島村莊 (talking_island) 廣場（服務新手與初到說話之島玩家）
		EnsureSpawn(888001, "一元福利商人^戴捷爾", "L1Merchant", 2400, "talking_island", 325, 168, 4, true);
		EnsureSpawn(91101, "天上聖母 · 媽祖", "L1Teleporter", 7348, "talking_island", 327, 170, 4, false);
		EnsureSpawn(80058, "毀滅的說話之島", "L1Teleporter", 5460, "talking_island", 329, 165, 4, false);
		EnsureSpawn(980062, "被襲擊的成長之島", "L1Teleporter", 5456, "talking_island", 331, 165, 4, false);

		// 3. 隱藏之谷新手村 (l1j_map_2005) 廣場（服務新創角色出生地）
		EnsureSpawn(888001, "一元福利商人^戴捷爾", "L1Merchant", 2400, "l1j_map_2005", 168, 160, 4, true);
		EnsureSpawn(91101, "天上聖母 · 媽祖", "L1Teleporter", 7348, "l1j_map_2005", 170, 162, 4, false);
		EnsureSpawn(80058, "毀滅的說話之島", "L1Teleporter", 5460, "l1j_map_2005", 172, 160, 4, false);
		EnsureSpawn(980062, "被襲擊的成長之島", "L1Teleporter", 5456, "l1j_map_2005", 174, 160, 4, false);

		array = spawnList.ToArray();

		if (dictionary.Count == 0 || array.Length == 0)
		{
			throw new InvalidDataException("L1J_NPC_SHOPS is empty.");
		}
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (L1jShopItem item2 in dictionary.Values.Where(s => s.NpcId < 888001 || s.NpcId > 888007).SelectMany((L1jShopDefinition shop) => shop.Items))
		{
			if (item2.SellPrice >= 0 && item2.ItemKey != null)
			{
				int num = item2.SellPrice / Math.Max(1, item2.PackCount);
				if (!dictionary2.TryGetValue(item2.ItemKey, out var value3) || num < value3)
				{
					dictionary2[item2.ItemKey] = num;
				}
			}
		}
		return new Loaded(dictionary, array, dictionary2);
	}

	private static ItemBlessing ReadBlessing(string? value)
	{
		if (!(value == "blessed"))
		{
			if (value == "cursed")
			{
				return ItemBlessing.Cursed;
			}
			return ItemBlessing.Normal;
		}
		return ItemBlessing.Blessed;
	}
}
