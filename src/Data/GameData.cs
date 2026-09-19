#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public sealed class GameData : IGameData
{
	private readonly string _tableDirectory;

	private readonly JsonObject _index;

	private readonly Dictionary<string, JsonNode?> _tables = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

	private readonly Dictionary<string, string> _loadErrors = new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly ReadOnlyCollection<string> _tableNames;

	private readonly DataOverlay _overlay;

	public string GameVersion => _index["gameVersion"]?.GetValue<string>() ?? string.Empty;

	public int SaveVersion => _index["saveVersion"]?.GetValue<int>() ?? 0;

	public IReadOnlyCollection<string> TableNames => _tableNames;

	public JsonObject Db => (Table("DB") as JsonObject) ?? new JsonObject();

	public JsonObject Items => Section("items");

	public JsonObject Mobs => Section("mobs");

	public JsonObject Maps => Section("maps");

	public JsonObject Skills => Section("skills");

	public JsonObject Towns => Section("towns");

	public JsonObject Sets => Section("sets");

	public GameData(string tableDirectory, string? indexPath = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableDirectory, "tableDirectory");
		_tableDirectory = DataFileSystem.FullPath(tableDirectory);
		string text = DataFileSystem.GetParent(_tableDirectory) ?? throw new InvalidOperationException("The table directory must have a data parent.");
		if (indexPath == null)
		{
			indexPath = DataFileSystem.Combine(text, "index.json");
		}
		_overlay = DataOverlay.Load(text);
		try
		{
			_index = JsonNode.Parse(DataFileSystem.ReadAllText(indexPath))?.AsObject() ?? throw new InvalidDataException("data/index.json must be a JSON object.");
		}
		catch (Exception ex) when (((ex is IOException || ex is JsonException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("Unable to load data index: " + indexPath, ex);
		}
		List<string> list = new List<string>();
		if (_index["tables"] is JsonArray jsonArray)
		{
			foreach (JsonNode item in jsonArray)
			{
				string text2 = item?["name"]?.GetValue<string>();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					list.Add(text2);
				}
			}
		}
		_tableNames = list.AsReadOnly();
	}

	public void InvalidateTable(string name)
	{
		_tables.Remove(name);
	}

	public bool HasTable(string name)
	{
		return _tableNames.Contains<string>(name, StringComparer.Ordinal);
	}

	public bool LoadFailed(string name)
	{
		return _loadErrors.ContainsKey(name);
	}

	public string? LoadError(string name)
	{
		if (!_loadErrors.TryGetValue(name, out string value))
		{
			return null;
		}
		return value;
	}

	public JsonNode? Table(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name, "name");
		if (_tables.TryGetValue(name, out JsonNode value))
		{
			return value;
		}
		if (!HasTable(name))
		{
			return Fail(name, "Table is not present in data/index.json.");
		}
		string path = DataFileSystem.Combine(_tableDirectory, name + ".json");
		try
		{
			JsonNode node = JsonNode.Parse(DataFileSystem.ReadAllText(path));
			JsonNode jsonNode = Restore(node);
			JsonNode jsonNode2 = ResolveTree(jsonNode, jsonNode, name);
			_overlay.Apply(name, jsonNode2);
			_tables[name] = jsonNode2;
			return jsonNode2;
		}
		catch (Exception ex) when (((ex is IOException || ex is JsonException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			return Fail(name, ex.Message);
		}
	}

	public JsonObject? Item(string id)
	{
		return Items[id] as JsonObject;
	}

	public JsonObject? Mob(string key)
	{
		return Mobs[key] as JsonObject;
	}

	public JsonObject? Skill(string id)
	{
		return Skills[id] as JsonObject;
	}

	public JsonNode? Resolve(JsonNode? node)
	{
		if (!(node is JsonObject { Count: 1 } jsonObject) || !(jsonObject["__ref"] is JsonValue jsonValue))
		{
			return node;
		}
		string value = jsonValue.GetValue<string>();
		if (!string.IsNullOrWhiteSpace(value))
		{
			return ResolveReference(value);
		}
		return null;
	}

	private JsonObject Section(string name)
	{
		return (Db[name] as JsonObject) ?? new JsonObject();
	}

	private JsonNode? Fail(string name, string reason)
	{
		_loadErrors[name] = reason;
		_tables[name] = null;
		Trace.TraceError("[GameData] " + name + ": " + reason);
		Godot.GD.PushError("[GameData] Table " + name + " failed to load: " + reason);
		return null;
	}

	private JsonNode? Restore(JsonNode? node)
	{
		if (node != null)
		{
			if (!(node is JsonArray jsonArray))
			{
				if (node is JsonObject jsonObject)
				{
					if (jsonObject["__type"]?.GetValue<string>() == "Set")
					{
						return Restore(jsonObject["values"]);
					}
					JsonObject jsonObject2 = jsonObject;
					if (jsonObject2["__type"]?.GetValue<string>() == "Map")
					{
						return Restore(jsonObject2["entries"]);
					}
					JsonObject jsonObject3 = new JsonObject();
					{
						foreach (KeyValuePair<string, JsonNode> item in jsonObject)
						{
							jsonObject3[item.Key] = Restore(item.Value);
						}
						return jsonObject3;
					}
				}
				return node.DeepClone();
			}
			JsonArray jsonArray2 = new JsonArray();
			{
				foreach (JsonNode item2 in jsonArray)
				{
					jsonArray2.Add(Restore(item2));
				}
				return jsonArray2;
			}
		}
		return null;
	}

	private JsonNode? ResolveTree(JsonNode? node, JsonNode? root, string tableName)
	{
		if (node is JsonObject { Count: 1 } jsonObject && jsonObject["__ref"] is JsonValue jsonValue)
		{
			string value = jsonValue.GetValue<string>();
			return ((string.IsNullOrWhiteSpace(value) ? null : ResolveReference(value, root, tableName)) ?? throw new InvalidDataException("Unable to resolve __ref '" + value + "'.")).DeepClone();
		}
		if (node is JsonObject jsonObject2)
		{
			string[] array = jsonObject2.Select<KeyValuePair<string, JsonNode>, string>((KeyValuePair<string, JsonNode> pair) => pair.Key).ToArray();
			foreach (string propertyName in array)
			{
				JsonNode jsonNode = jsonObject2[propertyName];
				JsonNode jsonNode2 = ResolveTree(jsonNode, root, tableName);
				if (jsonNode != jsonNode2)
				{
					jsonObject2[propertyName] = jsonNode2;
				}
			}
		}
		else if (node is JsonArray jsonArray)
		{
			for (int num2 = 0; num2 < jsonArray.Count; num2++)
			{
				JsonNode jsonNode3 = jsonArray[num2];
				JsonNode jsonNode4 = ResolveTree(jsonNode3, root, tableName);
				if (jsonNode3 != jsonNode4)
				{
					jsonArray[num2] = jsonNode4;
				}
			}
		}
		return node;
	}

	private JsonNode? ResolveReference(string reference, JsonNode? root = null, string? tableName = null)
	{
		int num = reference.IndexOfAny(new char[3] { '.', '[', '#' });
		string text = ((num < 0) ? reference : reference.Substring(0, num));
		JsonNode jsonNode = ((text == tableName) ? root : Table(text));
		if (jsonNode == null || reference.Length == text.Length)
		{
			return jsonNode;
		}
		JsonNode jsonNode2 = jsonNode;
		int num2 = text.Length;
		while (num2 < reference.Length)
		{
			char c = reference[num2++];
			int i = num2;
			int num4;
			switch (c)
			{
			case '[':
			{
				int num3 = reference.IndexOf(']', num2);
				if (num3 >= 0)
				{
					num4 = num2;
					if (int.TryParse(reference.Substring(num4, num3 - num4), out var result) && jsonNode2 is JsonArray jsonArray && result >= 0 && result < jsonArray.Count)
					{
						jsonNode2 = jsonArray[result];
						num2 = num3 + 1;
						continue;
					}
				}
				return null;
			}
			default:
				return null;
			case '#':
			case '.':
				break;
			}
			for (; i < reference.Length; i++)
			{
				char c2 = reference[i];
				if (c2 == '.' || c2 == '[' || c2 == '#')
				{
					break;
				}
			}
			num4 = num2;
			string text2 = reference.Substring(num4, i - num4);
			if (c == '#' && jsonNode2 is JsonArray jsonArray2 && int.TryParse(text2, out var result2))
			{
				if (result2 < 0 || result2 >= jsonArray2.Count)
				{
					return null;
				}
				jsonNode2 = jsonArray2[result2];
			}
			else
			{
				if (!(jsonNode2 is JsonObject jsonObject))
				{
					return null;
				}
				jsonNode2 = jsonObject[text2];
			}
			num2 = i;
		}
		return jsonNode2;
	}
}
