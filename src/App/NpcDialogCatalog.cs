using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class NpcDialogCatalog
{
	private static Dictionary<int, JsonObject>? _dialogs;

	private static Dictionary<int, JsonObject>? _bindings;

	private static Dictionary<string, JsonObject>? _htmlDialogs;

	public static void Reload()
	{
		_dialogs = null;
		_bindings = null;
		_htmlDialogs = null;
	}

	private static JsonObject? Root()
	{
		GodotDataFiles.EnsureInstalled();
		string path = "res://data/npc-dialog.json";
		string? jsonText = null;
		if (DataFileSystem.Exists(path))
		{
			jsonText = DataFileSystem.ReadAllText(path);
		}
		else if (FileAccess.FileExists(path))
		{
			jsonText = FileAccess.GetFileAsString(path);
		}

		if (string.IsNullOrEmpty(jsonText))
		{
			return null;
		}
		return JsonNode.Parse(jsonText) as JsonObject;
	}

	private static Dictionary<int, JsonObject> Dialogs()
	{
		if (_dialogs != null)
		{
			return _dialogs;
		}
		Dictionary<int, JsonObject> dictionary = new Dictionary<int, JsonObject>();
		JsonObject jsonObject = Root();
		if (jsonObject != null && jsonObject["dialogs"] is JsonObject jsonObject2)
		{
			foreach (var (s, jsonNode2) in jsonObject2)
			{
				if (jsonNode2 is JsonObject value && int.TryParse(s, out var result))
				{
					dictionary[result] = value;
				}
			}
		}
		_dialogs = dictionary;
		return dictionary;
	}

	private static Dictionary<int, JsonObject> Bindings()
	{
		if (_bindings != null)
		{
			return _bindings;
		}
		Dictionary<int, JsonObject> dictionary = new Dictionary<int, JsonObject>();
		JsonObject jsonObject = Root();
		if (jsonObject != null && jsonObject["bindings"] is JsonObject jsonObject2)
		{
			foreach (var (s, jsonNode2) in jsonObject2)
			{
				if (jsonNode2 is JsonObject value && int.TryParse(s, out var result))
				{
					dictionary[result] = value;
				}
			}
		}
		_bindings = dictionary;
		return dictionary;
	}

	private static Dictionary<string, JsonObject> HtmlDialogs()
	{
		if (_htmlDialogs != null)
		{
			return _htmlDialogs;
		}
		Dictionary<string, JsonObject> dictionary = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
		JsonObject jsonObject = Root();
		if (jsonObject != null && jsonObject["htmlDialogs"] is JsonObject jsonObject2)
		{
			foreach (var (key, jsonNode2) in jsonObject2)
			{
				if (jsonNode2 is JsonObject value)
				{
					dictionary[key] = value;
				}
			}
		}
		_htmlDialogs = dictionary;
		return dictionary;
	}

	public static string? DefaultHtmlId(int npcId, double lawful)
	{
		if (!Bindings().TryGetValue(npcId, out JsonObject value))
		{
			return null;
		}
		string text = value["normal"]?.GetValue<string>()?.Trim() ?? "";
		string text2 = value["chaotic"]?.GetValue<string>()?.Trim() ?? "";
		string text3 = ((lawful < -1000.0 && text2.Length > 0) ? text2 : text);
		if (text3.Length <= 0 || !HasHtmlDialog(text3))
		{
			return null;
		}
		return text3;
	}

	public static bool HasHtmlDialog(string? htmlId)
	{
		if (!string.IsNullOrWhiteSpace(htmlId))
		{
			return HtmlDialogs().ContainsKey(htmlId);
		}
		return false;
	}

	public static string SpeakerLine(int npcId, string fallbackName)
	{
		JsonObject value;
		string value2;
		return ((Dialogs().TryGetValue(npcId, out value) && value["speaker"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value2) && !string.IsNullOrWhiteSpace(value2)) ? value2 : fallbackName) + "：";
	}

	public static string SpeakerLineByHtml(string htmlId, string fallbackName)
	{
		JsonObject value;
		string value2;
		return ((HtmlDialogs().TryGetValue(htmlId, out value) && value["speaker"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value2) && !string.IsNullOrWhiteSpace(value2)) ? value2 : fallbackName) + "：";
	}

	public static IReadOnlyList<string> Lines(int npcId)
	{
		if (!Dialogs().TryGetValue(npcId, out JsonObject value) || !(value["lines"] is JsonArray { Count: not 0 } jsonArray))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>(jsonArray.Count);
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value2) && !string.IsNullOrWhiteSpace(value2))
			{
				list.Add(value2);
			}
		}
		return list;
	}

	public static IReadOnlyList<string> LinesByHtml(string htmlId)
	{
		JsonObject value;
		return ReadLines(HtmlDialogs().TryGetValue(htmlId, out value) ? value : null);
	}

	private static IReadOnlyList<string> ReadLines(JsonObject? row)
	{
		if (!(row?["lines"] is JsonArray { Count: not 0 } jsonArray))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>(jsonArray.Count);
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
			{
				list.Add(value);
			}
		}
		return list;
	}

	public static IReadOnlyList<NpcDialogAction> Actions(int npcId)
	{
		if (!Dialogs().TryGetValue(npcId, out JsonObject value) || !(value["l1jActions"] is JsonArray { Count: not 0 } jsonArray))
		{
			return Array.Empty<NpcDialogAction>();
		}
		List<NpcDialogAction> list = new List<NpcDialogAction>(jsonArray.Count);
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonObject jsonObject)
			{
				string text = jsonObject["action"]?.GetValue<string>()?.Trim() ?? "";
				string text2 = jsonObject["label"]?.GetValue<string>()?.Trim() ?? "";
				if (text.Length > 0 && text2.Length > 0)
				{
					list.Add(new NpcDialogAction(text, text2));
				}
			}
		}
		return list;
	}

	public static IReadOnlyList<NpcDialogAction> ActionsByHtml(string htmlId)
	{
		JsonObject value;
		return ReadActions(HtmlDialogs().TryGetValue(htmlId, out value) ? value : null);
	}

	private static IReadOnlyList<NpcDialogAction> ReadActions(JsonObject? row)
	{
		if (!(row?["l1jActions"] is JsonArray { Count: not 0 } jsonArray))
		{
			return Array.Empty<NpcDialogAction>();
		}
		List<NpcDialogAction> list = new List<NpcDialogAction>(jsonArray.Count);
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonObject jsonObject)
			{
				string text = jsonObject["action"]?.GetValue<string>()?.Trim() ?? "";
				string text2 = jsonObject["label"]?.GetValue<string>()?.Trim() ?? "";
				if (text.Length > 0 && text2.Length > 0)
				{
					list.Add(new NpcDialogAction(text, text2));
				}
			}
		}
		return list;
	}

	public static IReadOnlyList<NpcDialogLink> LinksByHtml(string htmlId)
	{
		if (!HtmlDialogs().TryGetValue(htmlId, out JsonObject value) || !(value["l1jLinks"] is JsonArray { Count: not 0 } jsonArray))
		{
			return Array.Empty<NpcDialogLink>();
		}
		List<NpcDialogLink> list = new List<NpcDialogLink>(jsonArray.Count);
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonObject jsonObject)
			{
				string text = jsonObject["htmlId"]?.GetValue<string>()?.Trim() ?? "";
				string text2 = jsonObject["label"]?.GetValue<string>()?.Trim() ?? "";
				if (text.Length > 0 && text2.Length > 0 && HasHtmlDialog(text))
				{
					list.Add(new NpcDialogLink(text, text2));
				}
			}
		}
		return list;
	}

	public static string? ActionLabel(int npcId, string actionName)
	{
		foreach (NpcDialogAction item in Actions(npcId))
		{
			if (string.Equals(item.Action, actionName, StringComparison.OrdinalIgnoreCase))
			{
				return item.Label;
			}
		}
		return null;
	}

	public static string? ActionLabelByHtml(string htmlId, string actionName)
	{
		foreach (NpcDialogAction item in ActionsByHtml(htmlId))
		{
			if (string.Equals(item.Action, actionName, StringComparison.OrdinalIgnoreCase))
			{
				return item.Label;
			}
		}
		return null;
	}

	public static bool HasOriginalDialog(int npcId)
	{
		return Dialogs().ContainsKey(npcId);
	}
}
