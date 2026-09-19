using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

internal sealed class CollectionCatalog
{
	private readonly Dictionary<CollectionBookKind, IReadOnlyList<CollectionCategory>> _categories = new Dictionary<CollectionBookKind, IReadOnlyList<CollectionCategory>>();

	private readonly Dictionary<(CollectionBookKind Book, string Key), IReadOnlyList<string>> _requiredItems = new Dictionary<(CollectionBookKind, string), IReadOnlyList<string>>();

	private readonly Dictionary<string, string> _miscStaticCategory = new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly HashSet<string> _miscExcluded = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, string> _cardItemByMob = new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly Dictionary<string, string> _cardByItem = new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly IGameData _data;

	internal IReadOnlyDictionary<string, string> EquipmentCategoryByItem { get; }

	internal IReadOnlyDictionary<string, string> RelicCategoryByItem { get; }

	internal HashSet<string> CardMobs { get; } = new HashSet<string>(StringComparer.Ordinal);

	internal HashSet<string> CardMobKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

	internal CollectionCatalog(IGameData data)
	{
		_data = data;
		EquipmentCategoryByItem = LoadCategories(data, CollectionBookKind.Equipment, "EQUIP_CATEGORIES", "EQUIP_CAT_ITEMS", "EQUIP_CAT_BONUS");
		string key2;
		foreach (KeyValuePair<string, string> item2 in LoadCategories(data, CollectionBookKind.Misc, "MISC_CATEGORIES", "MISC_CAT_ITEMS", "MISC_CAT_BONUS"))
		{
			item2.Deconstruct(out var key, out key2);
			string key3 = key;
			string value = key2;
			_miscStaticCategory[key3] = value;
		}
		RelicCategoryByItem = LoadCategories(data, CollectionBookKind.Relic, "EQUIP_CATEGORIES", "RELIC_CAT_ITEMS", null);
		JsonNode value2;
		foreach (KeyValuePair<string, JsonNode> item3 in data.Items)
		{
			item3.Deconstruct(out key2, out value2);
			string text = key2;
			if (value2 is JsonObject jsonObject)
			{
				string text2 = ReadString(jsonObject["cardMob"]);
				if (text2.Length != 0)
				{
					_cardItemByMob.TryAdd(text2, text);
					_cardByItem.TryAdd(text, text2);
					CardMobKeys.Add(text2);
				}
			}
		}
		LoadCards(data);
		if (!(data.Table("MISC_BOOK_EXCLUDED") is JsonObject jsonObject2))
		{
			return;
		}
		foreach (KeyValuePair<string, JsonNode> item4 in jsonObject2)
		{
			item4.Deconstruct(out key2, out value2);
			string item = key2;
			if (ReadBool(value2))
			{
				_miscExcluded.Add(item);
			}
		}
	}

	internal IReadOnlyList<CollectionCategory> Categories(CollectionBookKind book)
	{
		return _categories.GetValueOrDefault(book) ?? Array.Empty<CollectionCategory>();
	}

	internal IReadOnlyList<string> RequiredItems(CollectionBookKind book, string categoryKey)
	{
		if (!_requiredItems.TryGetValue((book, categoryKey), out IReadOnlyList<string> value))
		{
			return Array.Empty<string>();
		}
		if (book != CollectionBookKind.Misc)
		{
			return value;
		}
		return value.Concat(from pair in _miscStaticCategory
			where pair.Value == categoryKey
			select pair.Key).Distinct<string>(StringComparer.Ordinal).ToArray();
	}

	internal bool BelongsToBook(CollectionBookKind book, string itemKey)
	{
		string category;
		return book switch
		{
			CollectionBookKind.Equipment => EquipmentCategoryByItem.ContainsKey(itemKey), 
			CollectionBookKind.Misc => TryClassifyMisc(itemKey, out category), 
			CollectionBookKind.Relic => RelicCategoryByItem.ContainsKey(itemKey), 
			_ => false, 
		};
	}

	internal bool IsCardItem(string itemKey)
	{
		return _cardByItem.ContainsKey(itemKey);
	}

	internal bool TryGetCardItem(string mobKey, out string itemKey)
	{
		return _cardItemByMob.TryGetValue(mobKey, out itemKey);
	}

	internal bool TryReadCard(string itemKey, out string mobKey)
	{
		return _cardByItem.TryGetValue(itemKey, out mobKey);
	}

	internal bool TryClassifyMisc(string itemKey, out string category)
	{
		if (_miscExcluded.Contains(itemKey) || EquipmentCategoryByItem.ContainsKey(itemKey) || RelicCategoryByItem.ContainsKey(itemKey) || IsCardItem(itemKey))
		{
			category = "";
			return false;
		}
		if (_miscStaticCategory.TryGetValue(itemKey, out category))
		{
			return true;
		}
		JsonObject jsonObject = _data.Item(itemKey);
		if (jsonObject == null)
		{
			category = "";
			return false;
		}
		string text = ReadString(jsonObject["type"]).ToLowerInvariant();
		string text2 = ReadString(jsonObject["n"]);
		if (text == "pot" || itemKey.StartsWith("potion_", StringComparison.Ordinal))
		{
			category = "pot";
		}
		else if (text == "scroll" || itemKey.StartsWith("scroll_", StringComparison.Ordinal) || text2.Contains("卷軸", StringComparison.Ordinal))
		{
			category = "scroll";
		}
		else
		{
			bool flag = ((text == "skillbk" || text == "book") ? true : false);
			if (flag || itemKey.StartsWith("bk_", StringComparison.Ordinal) || itemKey.StartsWith("mem_", StringComparison.Ordinal))
			{
				category = "skillbk";
			}
			else
			{
				flag = ((text == "mat" || text == "material") ? true : false);
				if (flag || itemKey.StartsWith("mat_", StringComparison.Ordinal) || itemKey.StartsWith("new_item_", StringComparison.Ordinal))
				{
					category = "mat";
				}
				else
				{
					switch (text)
					{
					case "misc":
					case "special":
					case "food":
					case "ticket":
					case "etc":
					case "other":
						flag = true;
						break;
					default:
						flag = itemKey.StartsWith("doll_", StringComparison.OrdinalIgnoreCase);
						break;
					}
					if (!flag)
					{
						category = "";
						return false;
					}
					category = "special";
				}
			}
		}
		_miscStaticCategory[itemKey] = category;
		return true;
	}

	private Dictionary<string, string> LoadCategories(IGameData data, CollectionBookKind book, string categoryTableName, string itemsTableName, string? bonusTableName)
	{
		JsonArray obj = data.Table(categoryTableName) as JsonArray;
		if (obj == null || !(data.Table(itemsTableName) is JsonObject jsonObject))
		{
			throw new InvalidDataException($"Collection tables '{categoryTableName}' and '{itemsTableName}' are required.");
		}
		JsonObject jsonObject2 = ((bonusTableName == null) ? new JsonObject() : ((data.Table(bonusTableName) as JsonObject) ?? throw new InvalidDataException("Collection bonus table '" + bonusTableName + "' is required.")));
		List<CollectionCategory> list = new List<CollectionCategory>();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (JsonNode item2 in obj)
		{
			if (!(item2 is JsonObject jsonObject3))
			{
				continue;
			}
			string text = ReadString(jsonObject3["key"]);
			if (text.Length != 0 && jsonObject[text] is JsonArray source)
			{
				string[] array = (from node in source
					select ReadString(node) into value
					where value.Length > 0
					select value).Distinct<string>(StringComparer.Ordinal).ToArray();
				string[] array2 = array;
				foreach (string key in array2)
				{
					dictionary.TryAdd(key, text);
				}
				JsonObject jsonObject4 = jsonObject2[text] as JsonObject;
				CollectionCategory item = new CollectionCategory(text, ReadString(jsonObject3["name"], text), ReadString(jsonObject3["group"]), array, ReadString(jsonObject4?["stat"]), ReadDouble(jsonObject4?["val"]), ReadString(jsonObject4?["label"]), Array.Empty<double>());
				list.Add(item);
				_requiredItems[(book, text)] = array;
			}
		}
		_categories[book] = list;
		return dictionary;
	}

	private void LoadCards(IGameData data)
	{
		JsonArray obj = data.Table("CARD_REGIONS") as JsonArray;
		if (obj == null || !(data.Table("CARD_REGION_MOBS") is JsonObject jsonObject))
		{
			throw new InvalidDataException("Collection card tables CARD_REGIONS and CARD_REGION_MOBS are required.");
		}
		List<CollectionCategory> list = new List<CollectionCategory>();
		foreach (JsonNode item2 in obj)
		{
			if (!(item2 is JsonObject jsonObject2))
			{
				continue;
			}
			string text = ReadString(jsonObject2["key"]);
			if (text.Length != 0 && jsonObject[text] is JsonArray source)
			{
				string[] array = (from node in source
					select ReadString(node) into mobKey
					where mobKey.Length > 0 && data.Mob(mobKey) != null && CardMobKeys.Contains(mobKey)
					select mobKey).Distinct<string>(StringComparer.Ordinal).ToArray();
				string[] array2 = array;
				foreach (string item in array2)
				{
					CardMobs.Add(item);
				}
				double[] tierValues = ((jsonObject2["vals"] is JsonArray source2) ? source2.Select(ReadDouble).ToArray() : Array.Empty<double>());
				list.Add(new CollectionCategory(text, ReadString(jsonObject2["name"], text), "", array, ReadString(jsonObject2["stat"]), 0.0, "", tierValues));
				_requiredItems[(CollectionBookKind.Card, text)] = array;
			}
		}
		_categories[CollectionBookKind.Card] = list;
	}

	private static string ReadString(JsonNode? node, string fallback = "")
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return fallback;
		}
		return value ?? fallback;
	}

	private static int ReadInt(JsonNode? node)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}

	private static double ReadDouble(JsonNode? node)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0.0;
		}
		return value;
	}

	private static bool ReadBool(JsonNode? node)
	{
		bool value = default(bool);
		return node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
