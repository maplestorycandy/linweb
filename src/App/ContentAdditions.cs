using System;
using System.Globalization;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class ContentAdditions
{
	public const string ReturnScrollKey = "scroll_return";

	private const string ShopAnchorItemKey = "scroll_teleport";

	public const string FoodEffect = "food";

	public const string GoldKey = "gold";

	public const string GuardianSoulKey = "item_guardian_soul";

	private static readonly string[] HealingRangeKeys = new string[4] { "potion_heal", "potion_strong", "potion_ult", "new_item_141" };

	private static readonly (string Key, string Description)[] DescriptionRewrites = new(string, string)[8]
	{
		("scroll_teleport", "使用後傳送到目前地圖的任意一點。持有傳送控制戒指時，改為可選擇已記憶的位置。"),
		("acc_116", "刻著古老座標的戒指。帶在背包或戴在手上都生效：可記憶 5 個位置（時空行者專精再 +5），施放傳送術或使用瞬間移動卷軸時改為選擇已記憶的位置前往；戒指本身也能免費把你移動到目前地圖的其他位置，抵達時會驚動附近的怪物。"),
		("item_dk_book", "記載死亡騎士祕儀的古書。交給長老會議廳的 真．冥皇丹特斯，可選擇進入 黑暗妖精聖地 或 受詛咒的黑暗妖精聖地（每次進入消耗 1 本）。區域內的頭目被擊敗後，會在下一個整點自動復活，不需要再付出代價。"),
		("item_thebes_altar_key", "鑄有冥神紋章的古鑰，唯有持之者能踏入沉眠死神的聖殿。通往底比斯歐西里斯祭壇的鑰匙，**進場時消耗 1 把**；祭壇中的兩位王被擊敗後，各自在下一個整點自動再臨，不需要再付出鑰匙。"),
		("item_tikal_altar_key", "鐫刻羽蛇神紋章的古鑰，唯有持之者能踏入庫庫爾坎祭壇。通往提卡爾庫庫爾坎祭壇的鑰匙，**進場時消耗 1 把**；祭壇中的兩位王被擊敗後，各自在下一個整點自動再臨，不需要再付出鑰匙。"),
		("item_dragon_egg", "一顆尚在沉睡的幼龍蛋，蛋殼下彷彿有火在躍動。main 幼龍（npcid 45496）有 0.001% 機率掉落。直接使用會消耗這顆蛋，孵出 頑皮龍 並取得牠的專屬項圈；背包需保留一格項圈空間。可存入倉庫。"),
		("item_dragon_egg2", "一顆尚在沉睡的幼龍蛋，蛋殼下彷彿有風在流動。main 幼龍（npcid 45496）有 0.001% 機率掉落。直接使用會消耗這顆蛋，孵出 淘氣龍 並取得牠的專屬項圈；背包需保留一格項圈空間。可存入倉庫。"),
		("item_summonorb_full", "完整無缺的召喚球。離開受詛咒的黑暗妖精聖地時，若吉爾塔斯還活著且已經受傷，會自動消耗 1 顆保留牠當時的血量，下次進入時沿用；牠滿血、已被擊敗，或你身上沒有召喚球時都不會消耗，下次進入面對的是全新的吉爾塔斯。")
	};

	public static void Apply(GameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject items = data.Items;
		if (!items.ContainsKey("scroll_return"))
		{
			items["scroll_return"] = BuildReturnScroll();
		}
		if (!items.ContainsKey("gold"))
		{
			items["gold"] = BuildGold();
		}
		if (!items.ContainsKey(GuardianSoulKey))
		{
			items[GuardianSoulKey] = BuildGuardianSoul();
		}
		int value = RewriteDescriptions(data);
		int value2 = SyncHealingRanges(data);
		GD.Print($"[ContentAdditions] 內容新增：返回卷軸·肉·燈籠/燈油·金幣道具定義·說明改寫 {value} 筆·藥水區間同步 {value2} 筆。");
	}

	public static double FoodRestore(GameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return 0.0;
		}
		if (!(jsonObject["eff"] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || !string.Equals(value, "food", StringComparison.Ordinal))
		{
			return 0.0;
		}
		if (!(jsonObject["food"] is JsonValue jsonValue2) || !jsonValue2.TryGetValue<double>(out var value2) || !(value2 > 0.0))
		{
			return 0.0;
		}
		return value2;
	}

	private static int SyncHealingRanges(GameData data)
	{
		int num = 0;
		string[] healingRangeKeys = HealingRangeKeys;
		foreach (string text in healingRangeKeys)
		{
			JsonObject jsonObject = data.Item(text);
			if (jsonObject != null)
			{
				var (num2, num3) = ConsumableRules.BaseHealingRange(data, text);
				if (!ReadsAs(jsonObject, "valMin", num2) || !ReadsAs(jsonObject, "valMax", num3))
				{
					jsonObject["valMin"] = Number(num2);
					jsonObject["valMax"] = Number(num3);
					num++;
				}
			}
		}
		return num;
	}

	private static JsonNode Number(int value)
	{
		return JsonNode.Parse(value.ToString(CultureInfo.InvariantCulture));
	}

	private static bool ReadsAs(JsonObject definition, string field, int expected)
	{
		if (definition[field] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value))
		{
			return value == expected;
		}
		return false;
	}

	private static int RewriteDescriptions(GameData data)
	{
		int num = 0;
		(string, string)[] descriptionRewrites = DescriptionRewrites;
		for (int i = 0; i < descriptionRewrites.Length; i++)
		{
			(string, string) tuple = descriptionRewrites[i];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			JsonObject jsonObject = data.Item(item);
			if (jsonObject != null)
			{
				jsonObject["d"] = item2;
				num++;
			}
		}
		return num;
	}

	private static JsonObject BuildReturnScroll()
	{
		return (JsonObject)JsonNode.Parse("{\r\n  \"n\": \"返回卷軸\",\r\n  \"type\": \"scroll\",\r\n  \"req\": \"all\",\r\n  \"p\": 100,\r\n  \"c\": \"text-sky-300\",\r\n  \"d\": \"依原版所在圖、區域與職業分流返回對應城鎮。\",\n  \"eff\": \"return_scroll\",\r\n  \"gachaWeight\": 0\r\n}");
	}

	private static JsonObject BuildGold()
	{
		return (JsonObject)JsonNode.Parse("{\r\n  \"n\": \"金幣\",\r\n  \"type\": \"etc\",\r\n  \"noUse\": true,\r\n  \"c\": \"text-yellow-300\",\r\n  \"gachaWeight\": 0,\r\n  \"d\": \"通行於整片大陸的金幣，與商人交易的憑據。\"\r\n}");
	}

	private static JsonObject BuildGuardianSoul()
	{
		return (JsonObject)JsonNode.Parse("{\r\n  \"n\": \"守護者的靈魂\",\r\n  \"l1jIdentifiedName\": \"守護者的靈魂\",\r\n  \"l1jUnidentifiedName\": \"守護者的靈魂\",\r\n  \"type\": \"etc\",\r\n  \"noUse\": true,\r\n  \"c\": \"text-purple-300\",\r\n  \"gachaWeight\": 0,\r\n  \"d\": \"蘊含守護者之力的神秘靈魂。世界唯一。\\n能力：HP +500、MP +200、受到物理與魔法傷害減少 5%\"\r\n}");
	}
}
