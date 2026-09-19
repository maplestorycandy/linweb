using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public sealed class DataOverlay
{
	public const string FileName = "content-overlay.json";

	public const string SkillRangesFileName = "skill-ranges.json";

	public const string L1jMobsFileName = "l1j-mobs.json";

	public const string L1jMobSkillsFileName = "l1j-mob-skill-defs.json";

	public const string L1jSkillFieldsFileName = "l1j-skill-fields.json";

	public const string L1jSummonsFileName = "l1j-summons.json";

	public const string L1jNewSkillsFileName = "l1j-new-skills.json";

	public const string WebSkillAcquisitionFileName = "web-skill-acquisition.json";

	public const string UserRestoredSkillAcquisitionFileName = "user-restored-skill-acquisition.json";

	public const string CustomWarriorWebContentFileName = "custom-warrior-web-content.json";

	public const string MobArtFileName = "mob-art.json";

	private readonly JsonObject _root;

	private readonly JsonObject _skillRanges;

	private readonly JsonObject _l1jMobSkills;

	private readonly JsonObject _l1jSkillFields;

	private readonly JsonObject _l1jSummons;

	private readonly JsonObject _l1jNewSkills;

	private readonly JsonObject _webSkillAcquisition;

	private readonly JsonObject _userRestoredSkillAcquisition;

	private readonly JsonObject _customWarriorWebContent;

	private readonly JsonObject _mobArt;

	private readonly JsonObject _l1jMobs;

	private static readonly string[] MobSkillSlotNames = new string[12]
	{
		"mag", "mag2", "mag3", "mag4", "mag5", "mag6", "mag7", "mag8", "mag9", "mag10",
		"mag11", "mag12"
	};

	public static DataOverlay Empty { get; } = new DataOverlay(new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject());

	private DataOverlay(JsonObject root, JsonObject skillRanges, JsonObject l1jMobSkills, JsonObject l1jSkillFields, JsonObject l1jSummons, JsonObject l1jNewSkills, JsonObject webSkillAcquisition, JsonObject userRestoredSkillAcquisition, JsonObject customWarriorWebContent, JsonObject mobArt, JsonObject l1jMobs)
	{
		_root = root;
		_skillRanges = skillRanges;
		_l1jMobSkills = l1jMobSkills;
		_l1jSkillFields = l1jSkillFields;
		_l1jSummons = l1jSummons;
		_l1jNewSkills = l1jNewSkills;
		_webSkillAcquisition = webSkillAcquisition;
		_userRestoredSkillAcquisition = userRestoredSkillAcquisition;
		_customWarriorWebContent = customWarriorWebContent;
		_mobArt = mobArt;
		_l1jMobs = l1jMobs;
	}

	public static DataOverlay Load(string dataDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory, "dataDirectory");
		string path = DataFileSystem.Combine(dataDirectory, "content-overlay.json");
		string path2 = DataFileSystem.Combine(dataDirectory, "skill-ranges.json");
		string path3 = DataFileSystem.Combine(dataDirectory, "l1j-mob-skill-defs.json");
		string path4 = DataFileSystem.Combine(dataDirectory, "l1j-skill-fields.json");
		string path5 = DataFileSystem.Combine(dataDirectory, "l1j-summons.json");
		string path6 = DataFileSystem.Combine(dataDirectory, "l1j-new-skills.json");
		string path7 = DataFileSystem.Combine(dataDirectory, "web-skill-acquisition.json");
		string path8 = DataFileSystem.Combine(dataDirectory, "user-restored-skill-acquisition.json");
		string path9 = DataFileSystem.Combine(dataDirectory, "custom-warrior-web-content.json");
		string path10 = DataFileSystem.Combine(dataDirectory, "mob-art.json");
		string path11 = DataFileSystem.Combine(dataDirectory, "l1j-mobs.json");
		if (!DataFileSystem.Exists(path) && !DataFileSystem.Exists(path2) && !DataFileSystem.Exists(path3) && !DataFileSystem.Exists(path4) && !DataFileSystem.Exists(path5) && !DataFileSystem.Exists(path6) && !DataFileSystem.Exists(path7) && !DataFileSystem.Exists(path8) && !DataFileSystem.Exists(path9) && !DataFileSystem.Exists(path10) && !DataFileSystem.Exists(path11))
		{
			return Empty;
		}
		try
		{
			string ratesPath = DataFileSystem.Combine(dataDirectory, "server-rates.json");
			if (DataFileSystem.Exists(ratesPath))
			{
				try
				{
					JsonObject ratesObj = ReadObject(ratesPath, "server-rates.json");
					IdleLineage.Combat.GameRateConfig.ApplyRates(ratesObj);
				}
				catch { }
			}
			return new DataOverlay(ReadObject(path, "content-overlay.json"), ReadObject(path2, "skill-ranges.json"), ReadObject(path3, "l1j-mob-skill-defs.json"), ReadObject(path4, "l1j-skill-fields.json"), ReadObject(path5, "l1j-summons.json"), ReadObject(path6, "l1j-new-skills.json"), ReadObject(path7, "web-skill-acquisition.json"), ReadObject(path8, "user-restored-skill-acquisition.json"), ReadObject(path9, "custom-warrior-web-content.json"), ReadObject(path10, "mob-art.json"), ReadObject(path11, "l1j-mobs.json"));
		}
		catch (Exception ex) when (((ex is IOException || ex is JsonException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("Unable to load data overlay from '" + dataDirectory + "'.", ex);
		}
	}

	public bool Covers(string tableName)
	{
		if (!string.IsNullOrEmpty(tableName))
		{
			return _root[tableName] != null;
		}
		return false;
	}

	public void Apply(string tableName, JsonNode? table)
	{
		if (table != null && !string.IsNullOrEmpty(tableName))
		{
			JsonNode jsonNode = _root[tableName];
			if (jsonNode != null)
			{
				Merge(jsonNode, table);
			}
			ApplySkillAcquisition(tableName, table, _webSkillAcquisition, "web-skill-acquisition.json");
			ApplySkillAcquisition(tableName, table, _userRestoredSkillAcquisition, "user-restored-skill-acquisition.json");
			ApplyCustomWarriorWebContent(tableName, table);
			if (string.Equals(tableName, "DB", StringComparison.Ordinal))
			{
				ApplyMobArtCoverage(table);
				ApplyL1jNewSkills(table);
			}
			JsonNode jsonNode2 = _root[tableName + "_OVERRIDES"];
			if (jsonNode2 != null)
			{
				ReplaceExisting(jsonNode2, table, tableName);
			}
			ApplyTableRemovals(tableName, table);
			if (string.Equals(tableName, "DB", StringComparison.Ordinal))
			{
				ApplyDbRemovals(table);
				ApplySkillRanges(table);
				ApplyL1jSkillFields(table);
				ApplyL1jSummons(table);
				ApplyL1jMobs(table);
				ApplyL1jMobSkills(table);
			}
		}
	}

	private void ApplyTableRemovals(string tableName, JsonNode table)
	{
		if (_root["TABLE_REMOVALS"] == null)
		{
			return;
		}
		if (!(_root["TABLE_REMOVALS"] is JsonObject jsonObject))
		{
			throw new InvalidDataException("TABLE_REMOVALS must be a JSON object.");
		}
		if (jsonObject[tableName] == null)
		{
			return;
		}
		foreach (JsonNode item in (jsonObject[tableName] as JsonArray) ?? throw new InvalidDataException("TABLE_REMOVALS." + tableName + " must be an array of keys or string values."))
		{
			string text = string.Empty;
			string value = null;
			if (item is JsonValue jsonValue)
			{
				jsonValue.TryGetValue<string>(out value);
			}
			else if (item is JsonObject jsonObject2)
			{
				text = jsonObject2["path"]?.GetValue<string>() ?? string.Empty;
				value = jsonObject2["value"]?.GetValue<string>();
			}
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidDataException("TABLE_REMOVALS." + tableName + " entries require a non-empty value.");
			}
			JsonNode jsonNode = table;
			if (!string.IsNullOrWhiteSpace(text))
			{
				string[] array = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
				int num = 0;
				while (num < array.Length)
				{
					string propertyName = array[num];
					if (jsonNode is JsonObject jsonObject3)
					{
						JsonNode jsonNode2 = jsonObject3[propertyName];
						if (jsonNode2 != null)
						{
							jsonNode = jsonNode2;
							num++;
							continue;
						}
					}
					throw new InvalidDataException($"Table removal path '{tableName}.{text}' does not exist.");
				}
			}
			bool flag = false;
			if (jsonNode is JsonObject jsonObject4)
			{
				flag = jsonObject4.Remove(value);
			}
			else
			{
				if (!(jsonNode is JsonArray jsonArray))
				{
					throw new InvalidDataException($"TABLE_REMOVALS.{tableName}.{text} cannot target this JSON type.");
				}
				for (int num2 = jsonArray.Count - 1; num2 >= 0; num2--)
				{
					JsonNode jsonNode3 = jsonArray[num2];
					string a;
					if (!(jsonNode3 is JsonValue jsonValue2))
					{
						if (!(jsonNode3 is JsonArray jsonArray2))
						{
							if (!(jsonNode3 is JsonObject jsonObject5) || !(jsonObject5["v"] is JsonValue jsonValue3))
							{
								goto IL_0282;
							}
							a = (jsonValue3.TryGetValue<string>(out string value2) ? value2 : null);
						}
						else
						{
							if (jsonArray2.Count <= 0 || !(jsonArray2[0] is JsonValue jsonValue4))
							{
								goto IL_0282;
							}
							a = (jsonValue4.TryGetValue<string>(out string value3) ? value3 : null);
						}
					}
					else
					{
						a = (jsonValue2.TryGetValue<string>(out string value4) ? value4 : null);
					}
					goto IL_0285;
					IL_0282:
					a = null;
					goto IL_0285;
					IL_0285:
					if (string.Equals(a, value, StringComparison.Ordinal))
					{
						jsonArray.RemoveAt(num2);
						flag = true;
					}
				}
			}
			if (!flag)
			{
				throw new InvalidDataException($"Table removal target '{tableName}.{text}.{value}' does not exist.");
			}
		}
	}

	private void ApplyDbRemovals(JsonNode table)
	{
		if (_root["DB_REMOVALS"] == null)
		{
			return;
		}
		JsonObject obj = (_root["DB_REMOVALS"] as JsonObject) ?? throw new InvalidDataException("DB_REMOVALS must be a JSON object.");
		if (!(table is JsonObject jsonObject))
		{
			throw new InvalidDataException("DB_REMOVALS requires DB to be a JSON object.");
		}
		foreach (var (text2, jsonNode2) in obj)
		{
			if (text2.StartsWith("__", StringComparison.Ordinal))
			{
				continue;
			}
			JsonArray obj2 = (jsonNode2 as JsonArray) ?? throw new InvalidDataException("DB_REMOVALS." + text2 + " must be an array of keys.");
			if (!(jsonObject[text2] is JsonObject jsonObject2))
			{
				throw new InvalidDataException("DB_REMOVALS section 'DB." + text2 + "' does not exist.");
			}
			foreach (JsonNode item in obj2)
			{
				if (!(item is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || string.IsNullOrWhiteSpace(value))
				{
					throw new InvalidDataException("DB_REMOVALS." + text2 + " must contain non-empty string keys.");
				}
				if (!jsonObject2.Remove(value))
				{
					throw new InvalidDataException($"DB removal target 'DB.{text2}.{value}' does not exist.");
				}
			}
		}
	}

	private static JsonObject ReadObject(string path, string label)
	{
		if (!DataFileSystem.Exists(path))
		{
			return new JsonObject();
		}
		return JsonNode.Parse(DataFileSystem.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException(label + " must be a JSON object.");
	}

	private void ApplyMobArtCoverage(JsonNode table)
	{
		if (_mobArt.Count == 0)
		{
			return;
		}
		JsonArray obj = (_mobArt["noArt"] as JsonArray) ?? throw new InvalidDataException("mob-art.json.noArt must be a JSON array.");
		if (!(table is JsonObject jsonObject) || !(jsonObject["mobs"] is JsonObject jsonObject2))
		{
			throw new InvalidDataException("DB.mobs is unavailable while applying mob art coverage.");
		}
		foreach (JsonNode item in obj)
		{
			if (!(item is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidDataException("mob-art.json.noArt must hold mob keys.");
			}
			((jsonObject2[value] as JsonObject) ?? throw new InvalidDataException("mob-art.json.noArt lists '" + value + "', which is not a live mob. Re-run tools/l1j-import/build_mob_art_coverage.py."))["noArt"] = true;
		}
	}

	private void ApplyL1jMobs(JsonNode table)
	{
		if (_l1jMobs == null || _l1jMobs.Count == 0)
		{
			return;
		}
		JsonObject? mobsObj = _l1jMobs["mobs"] as JsonObject ?? _l1jMobs;
		if (table is JsonObject jsonObject && jsonObject["mobs"] is JsonObject dbMobs)
		{
			foreach (KeyValuePair<string, JsonNode> kvp in mobsObj)
			{
				if (kvp.Key.StartsWith("__", StringComparison.Ordinal))
				{
					continue;
				}
				if (kvp.Value is JsonObject mobObj)
				{
					if (dbMobs[kvp.Key] is JsonObject targetMob)
					{
						foreach (var prop in mobObj)
						{
							targetMob[prop.Key] = prop.Value?.DeepClone();
						}
					}
					else
					{
						dbMobs[kvp.Key] = mobObj.DeepClone();
					}
				}
			}
		}
	}

	private void ApplyL1jMobSkills(JsonNode table)
	{
		if (_l1jMobSkills.Count == 0)
		{
			return;
		}
		JsonObject obj = (_l1jMobSkills["mobs"] as JsonObject) ?? throw new InvalidDataException("l1j-mob-skill-defs.json.mobs must be a JSON object.");
		if (!(table is JsonObject jsonObject) || !(jsonObject["mobs"] is JsonObject jsonObject2))
		{
			throw new InvalidDataException("DB.mobs is unavailable while applying L1J mob skills.");
		}
		foreach (KeyValuePair<string, JsonNode> item in obj)
		{
			item.Deconstruct(out var key, out var value);
			string text = key;
			JsonNode jsonNode = value;
			if (text.StartsWith("__", StringComparison.Ordinal))
			{
				continue;
			}
			if (!(jsonNode is JsonObject jsonObject3))
			{
				throw new InvalidDataException("l1j-mob-skill-defs.json.mobs." + text + " must be a JSON object.");
			}
			if (!(jsonObject2[text] is JsonObject jsonObject4))
			{
				throw new InvalidDataException("l1j-mob-skill-defs.json.mobs." + text + " is not in DB.mobs. Mob skills may only be attached to a monster that exists.");
			}
			string[] mobSkillSlotNames = MobSkillSlotNames;
			foreach (string propertyName in mobSkillSlotNames)
			{
				jsonObject4.Remove(propertyName);
			}
			foreach (KeyValuePair<string, JsonNode> item2 in jsonObject3)
			{
				item2.Deconstruct(out key, out value);
				string propertyName2 = key;
				jsonObject4[propertyName2] = value?.DeepClone();
			}
		}
	}

	private void ApplySkillRanges(JsonNode table)
	{
		if (_skillRanges.Count == 0)
		{
			return;
		}
		JsonObject obj = (_skillRanges["skills"] as JsonObject) ?? throw new InvalidDataException("skill-ranges.json.skills must be a JSON object.");
		if (!(table is JsonObject jsonObject) || !(jsonObject["skills"] is JsonObject jsonObject2))
		{
			throw new InvalidDataException("DB.skills is unavailable while applying skill ranges.");
		}
		foreach (var (text2, jsonNode2) in obj)
		{
			if (!(jsonNode2 is JsonObject jsonObject3))
			{
				throw new InvalidDataException("skill-ranges.json.skills." + text2 + " must be an object.");
			}
			if (!(jsonObject2[text2] is JsonObject jsonObject4))
			{
				throw new InvalidDataException("Skill range entry '" + text2 + "' does not exist in DB.skills.");
			}
			string[] array = new string[3] { "castCells", "radiusCells", "targetMode" };
			foreach (string propertyName in array)
			{
				JsonNode jsonNode3 = jsonObject3[propertyName];
				if (jsonNode3 != null)
				{
					jsonObject4[propertyName] = jsonNode3.DeepClone();
				}
				else
				{
					jsonObject4.Remove(propertyName);
				}
			}
		}
	}

	private void ApplyL1jSkillFields(JsonNode table)
	{
		if (_l1jSkillFields.Count == 0)
		{
			return;
		}
		JsonObject obj = (_l1jSkillFields["skills"] as JsonObject) ?? throw new InvalidDataException("l1j-skill-fields.json.skills must be a JSON object.");
		if (!(table is JsonObject jsonObject) || !(jsonObject["skills"] is JsonObject jsonObject2))
		{
			throw new InvalidDataException("DB.skills is unavailable while applying the L1J skill fields.");
		}
		foreach (var (text2, jsonNode2) in obj)
		{
			if (!text2.StartsWith("__", StringComparison.Ordinal))
			{
				if (!(jsonNode2 is JsonObject jsonObject3))
				{
					throw new InvalidDataException("l1j-skill-fields.json.skills." + text2 + " must be an object.");
				}
				((jsonObject2[text2] as JsonObject) ?? throw new InvalidDataException("L1J skill field entry '" + text2 + "' does not exist in DB.skills."))["l1j"] = jsonObject3.DeepClone();
			}
		}
	}

	private void ApplyL1jNewSkills(JsonNode table)
	{
		if (_l1jNewSkills.Count != 0)
		{
			if (!(table is JsonObject jsonObject) || !(jsonObject["skills"] is JsonObject target) || !(jsonObject["items"] is JsonObject target2))
			{
				throw new InvalidDataException("DB.skills / DB.items is unavailable while applying the new official skills.");
			}
			AddAll("skills", target);
			AddAll("books", target2);
		}
		void AddAll(string section, JsonObject jsonObject4)
		{
			if (!(_l1jNewSkills[section] is JsonObject jsonObject2))
			{
				return;
			}
			foreach (var (text2, jsonNode2) in jsonObject2)
			{
				if (!text2.StartsWith("__", StringComparison.Ordinal))
				{
					if (!(jsonNode2 is JsonObject jsonObject3))
					{
						throw new InvalidDataException($"{"l1j-new-skills.json"}.{section}.{text2} must be a JSON object.");
					}
					jsonObject4[text2] = jsonObject3.DeepClone();
				}
			}
		}
	}

	private static void ApplySkillAcquisition(string tableName, JsonNode table, JsonObject acquisition, string sourceFileName)
	{
		if (acquisition.Count == 0)
		{
			return;
		}
		string key;
		JsonNode value;
		if (string.Equals(tableName, "DB", StringComparison.Ordinal))
		{
			if (!(table is JsonObject jsonObject) || !(jsonObject["items"] is JsonObject jsonObject2))
			{
				throw new InvalidDataException("DB.items is unavailable while applying " + sourceFileName + ".");
			}
			{
				foreach (KeyValuePair<string, JsonNode> item in (acquisition["books"] as JsonObject) ?? throw new InvalidDataException(sourceFileName + ".books must be a JSON object."))
				{
					item.Deconstruct(out key, out value);
					string text = key;
					JsonNode jsonNode = value;
					if (!text.StartsWith("__", StringComparison.Ordinal))
					{
						if (!(jsonNode is JsonObject jsonObject3))
						{
							throw new InvalidDataException(sourceFileName + ".books." + text + " must be an object.");
						}
						jsonObject2[text] = jsonObject3.DeepClone();
					}
				}
				return;
			}
		}
		if (!string.Equals(tableName, "ITEM_WEIGHTS", StringComparison.Ordinal))
		{
			return;
		}
		if (!(table is JsonObject jsonObject4))
		{
			throw new InvalidDataException("ITEM_WEIGHTS must be a JSON object.");
		}
		foreach (KeyValuePair<string, JsonNode> item2 in (acquisition["itemWeights"] as JsonObject) ?? throw new InvalidDataException(sourceFileName + ".itemWeights must be a JSON object."))
		{
			item2.Deconstruct(out key, out value);
			string text2 = key;
			JsonNode jsonNode2 = value;
			if (!text2.StartsWith("__", StringComparison.Ordinal))
			{
				if (!(jsonNode2 is JsonValue))
				{
					throw new InvalidDataException(sourceFileName + ".itemWeights." + text2 + " must be numeric.");
				}
				jsonObject4[text2] = jsonNode2.DeepClone();
			}
		}
	}

	private void ApplyCustomWarriorWebContent(string tableName, JsonNode table)
	{
		if (_customWarriorWebContent.Count == 0)
		{
			return;
		}
		if (string.Equals(tableName, "DB", StringComparison.Ordinal))
		{
			if (!(table is JsonObject jsonObject) || !(jsonObject["items"] is JsonObject target))
			{
				throw new InvalidDataException("DB.items is unavailable while applying custom-warrior-web-content.json.");
			}
			AddOnlyObjectSection(_customWarriorWebContent, "items", target, "custom-warrior-web-content.json");
			return;
		}
		if (string.Equals(tableName, "ITEM_WEIGHTS", StringComparison.Ordinal))
		{
			if (!(table is JsonObject target2))
			{
				throw new InvalidDataException("ITEM_WEIGHTS must be a JSON object.");
			}
			AddOnlyObjectSection(_customWarriorWebContent, "itemWeights", target2, "custom-warrior-web-content.json");
			return;
		}
		if (string.Equals(tableName, "WEAPON_TAGS", StringComparison.Ordinal))
		{
			if (!(table is JsonObject target3))
			{
				throw new InvalidDataException("WEAPON_TAGS must be a JSON object.");
			}
			AddOnlyObjectSection(_customWarriorWebContent, "weaponTags", target3, "custom-warrior-web-content.json");
			return;
		}
		if (string.Equals(tableName, "MOB_DROPS", StringComparison.Ordinal))
		{
			if (!(table is JsonObject jsonObject2) || !(_customWarriorWebContent["drops"] is JsonObject jsonObject3))
			{
				throw new InvalidDataException("custom-warrior-web-content.json.drops and MOB_DROPS must be objects.");
			}
			{
				foreach (var (text2, jsonNode2) in jsonObject3)
				{
					if (!text2.StartsWith("l1j_", StringComparison.Ordinal) || !(jsonNode2 is JsonArray jsonArray))
					{
						throw new InvalidDataException("custom-warrior-web-content.json.drops." + text2 + " is invalid.");
					}
					JsonArray jsonArray2 = (jsonObject2[text2] as JsonArray) ?? new JsonArray();
					if (!jsonObject2.ContainsKey(text2))
					{
						jsonObject2[text2] = jsonArray2;
					}
					foreach (JsonNode item in jsonArray)
					{
						if (!(item is JsonArray { Count: >=2 } jsonArray3) || !(jsonArray3[0] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string itemKey) || string.IsNullOrWhiteSpace(itemKey))
						{
							throw new InvalidDataException("custom-warrior-web-content.json.drops." + text2 + " contains an invalid row.");
						}
						if (jsonArray2.OfType<JsonArray>().Any((JsonArray existing) => existing.Count > 0 && existing[0] is JsonValue jsonValue3 && jsonValue3.TryGetValue<string>(out string value2) && string.Equals(value2, itemKey, StringComparison.Ordinal)))
						{
							continue;
						}
						jsonArray2.Add(jsonArray3.DeepClone());
					}
				}
				return;
			}
		}
		if (!string.Equals(tableName, "NPC_ACTIONS", StringComparison.Ordinal))
		{
			return;
		}
		if (!(table is JsonObject jsonObject4) || !(jsonObject4["actions"] is JsonArray jsonArray4) || !(_customWarriorWebContent["actions"] is JsonArray jsonArray5))
		{
			throw new InvalidDataException("custom-warrior-web-content.json.actions and NPC_ACTIONS.actions must be arrays.");
		}
		HashSet<int> hashSet = (from row in jsonArray4.OfType<JsonObject>()
			select row["seq"]?.GetValue<int>() ?? int.MinValue).ToHashSet();
		foreach (JsonNode item2 in jsonArray5)
		{
			if (!(item2 is JsonObject jsonObject5) || !(jsonObject5["seq"] is JsonValue jsonValue2))
			{
				throw new InvalidDataException("custom-warrior-web-content.json.actions contains an invalid row.");
			}
			int value = jsonValue2.GetValue<int>();
			if (!hashSet.Add(value))
			{
				continue;
			}
			jsonArray4.Add(jsonObject5.DeepClone());
		}
	}

	private static void AddOnlyObjectSection(JsonObject source, string sectionName, JsonObject target, string sourceFileName)
	{
		foreach (var (text2, jsonNode2) in (source[sectionName] as JsonObject) ?? throw new InvalidDataException(sourceFileName + "." + sectionName + " must be an object."))
		{
			if (!text2.StartsWith("__", StringComparison.Ordinal))
			{
				if (jsonNode2 == null)
				{
					throw new InvalidDataException($"{sourceFileName}.{sectionName}.{text2} is null.");
				}
				target[text2] = jsonNode2.DeepClone();
			}
		}
	}

	private void ApplyL1jSummons(JsonNode table)
	{
		if (_l1jSummons.Count == 0)
		{
			return;
		}
		JsonObject obj = (_l1jSummons["skills"] as JsonObject) ?? throw new InvalidDataException("l1j-summons.json.skills must be a JSON object.");
		if (!(table is JsonObject jsonObject) || !(jsonObject["skills"] is JsonObject jsonObject2))
		{
			throw new InvalidDataException("DB.skills is unavailable while applying the L1J summon roster.");
		}
		foreach (var (text2, jsonNode2) in obj)
		{
			if (!text2.StartsWith("__", StringComparison.Ordinal))
			{
				if (!(jsonNode2 is JsonObject jsonObject3))
				{
					throw new InvalidDataException("l1j-summons.json.skills." + text2 + " must be an object.");
				}
				((jsonObject2[text2] as JsonObject) ?? throw new InvalidDataException("L1J summon entry '" + text2 + "' does not exist in DB.skills."))["l1jSummon"] = jsonObject3.DeepClone();
			}
		}
	}

	private static void ReplaceExisting(JsonNode patch, JsonNode target, string path)
	{
		string key;
		JsonNode value;
		if (patch is JsonObject jsonObject && target is JsonArray jsonArray)
		{
			{
				foreach (KeyValuePair<string, JsonNode> item in jsonObject)
				{
					item.Deconstruct(out key, out value);
					string text = key;
					JsonNode jsonNode = value;
					if (text.StartsWith("__", StringComparison.Ordinal) || jsonNode == null)
					{
						continue;
					}
					if (int.TryParse(text, out var result) && result >= 0 && result < jsonArray.Count)
					{
						JsonNode jsonNode2 = jsonArray[result];
						if (jsonNode2 != null)
						{
							string path2 = $"{path}[{result}]";
							bool flag = jsonNode is JsonObject;
							if (flag)
							{
								bool flag2 = ((jsonNode2 is JsonObject || jsonNode2 is JsonArray) ? true : false);
								flag = flag2;
							}
							if (flag)
							{
								ReplaceExisting(jsonNode, jsonNode2, path2);
							}
							else
							{
								jsonArray[result] = jsonNode.DeepClone();
							}
							continue;
						}
					}
					throw new InvalidDataException($"Overlay override '{path}[{text}]' does not exist in exported data.");
				}
				return;
			}
		}
		JsonObject obj = patch as JsonObject;
		if (obj == null || !(target is JsonObject jsonObject2))
		{
			throw new InvalidDataException("Overlay override '" + path + "' must target an existing JSON object.");
		}
		foreach (KeyValuePair<string, JsonNode> item2 in obj)
		{
			item2.Deconstruct(out key, out value);
			string text2 = key;
			JsonNode jsonNode3 = value;
			if (!text2.StartsWith("__", StringComparison.Ordinal) && jsonNode3 != null)
			{
				string text3 = path + "." + text2;
				if (!jsonObject2.TryGetPropertyValue(text2, out JsonNode jsonNode4) || jsonNode4 == null)
				{
					jsonObject2[text2] = jsonNode3.DeepClone();
					continue;
				}
				bool flag = jsonNode3 is JsonObject;
				if (flag)
				{
					bool flag2 = ((jsonNode4 is JsonObject || jsonNode4 is JsonArray) ? true : false);
					flag = flag2;
				}
				if (flag)
				{
					ReplaceExisting(jsonNode3, jsonNode4, text3);
				}
				else
				{
					jsonObject2[text2] = jsonNode3.DeepClone();
				}
			}
		}
	}

	private static void Merge(JsonNode patch, JsonNode target)
	{
		if (patch is JsonObject jsonObject && target is JsonObject jsonObject2)
		{
			{
				foreach (var (text2, jsonNode2) in jsonObject)
				{
					if (!text2.StartsWith("__", StringComparison.Ordinal) && jsonNode2 != null)
					{
						JsonNode jsonNode3 = jsonObject2[text2];
						if (jsonNode3 != null)
						{
							Merge(jsonNode2, jsonNode3);
						}
						else
						{
							jsonObject2[text2] = jsonNode2.DeepClone();
						}
					}
				}
				return;
			}
		}
		if (!(patch is JsonArray jsonArray) || !(target is JsonArray jsonArray2))
		{
			return;
		}
		foreach (JsonNode item in jsonArray)
		{
			if (item != null && !Contains(jsonArray2, item))
			{
				jsonArray2.Add(item.DeepClone());
			}
		}
	}

	private static bool Contains(JsonArray array, JsonNode value)
	{
		string text = value.ToJsonString();
		foreach (JsonNode item in array)
		{
			if (item != null && item.ToJsonString() == text)
			{
				return true;
			}
		}
		return false;
	}
}
