using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jNpcChatCatalog
{
	public const string TableName = "L1J_NPC_CHAT";

	public IReadOnlyDictionary<(string MobKey, L1jNpcChatTiming Timing), L1jNpcChatDefinition> Rows { get; }

	public IReadOnlyDictionary<string, string> TextByToken { get; }

	private L1jNpcChatCatalog(IReadOnlyDictionary<(string MobKey, L1jNpcChatTiming Timing), L1jNpcChatDefinition> rows, IReadOnlyDictionary<string, string> textByToken)
	{
		Rows = rows;
		TextByToken = textByToken;
	}

	public string ResolveText(string token)
	{
		if (!TextByToken.TryGetValue(token, out string value))
		{
			throw new InvalidDataException("L1J_NPC_CHAT has no desc-c.tbl text for token '" + token + "'.");
		}
		return value;
	}

	public L1jNpcChatDefinition? Find(string mobKey, L1jNpcChatTiming timing)
	{
		if (!Rows.TryGetValue((mobKey, timing), out L1jNpcChatDefinition value))
		{
			return null;
		}
		return value;
	}

	public static L1jNpcChatCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject obj = (data.Table("L1J_NPC_CHAT") as JsonObject) ?? throw new InvalidDataException("L1J_NPC_CHAT must be a JSON object.");
		JsonArray jsonArray = (obj["rows"] as JsonArray) ?? throw new InvalidDataException("L1J_NPC_CHAT.rows must be an array.");
		JsonObject obj2 = (obj["textByToken"] as JsonObject) ?? throw new InvalidDataException("L1J_NPC_CHAT.textByToken must be an object.");
		Dictionary<string, string> textByToken = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, JsonNode> item in obj2)
		{
			item.Deconstruct(out var key, out var value);
			string text = key;
			string text2 = value?.GetValue<string>() ?? throw new InvalidDataException("L1J_NPC_CHAT.textByToken." + text + " must be a string.");
			if (text2.Length == 0 || !textByToken.TryAdd(text, text2))
			{
				throw new InvalidDataException("L1J_NPC_CHAT.textByToken has an empty or duplicate '" + text + "'.");
			}
		}
		Dictionary<(string, L1jNpcChatTiming), L1jNpcChatDefinition> dictionary = new Dictionary<(string, L1jNpcChatTiming), L1jNpcChatDefinition>();
		foreach (JsonNode item2 in jsonArray)
		{
			if (!(item2 is JsonObject jsonObject))
			{
				throw new InvalidDataException("Every L1J_NPC_CHAT row must be an object.");
			}
			int npcId = RequiredInt(jsonObject, "npcId");
			string text3 = RequiredString(jsonObject, "mobKey");
			int num = RequiredInt(jsonObject, "timing");
			if (!Enum.IsDefined(typeof(L1jNpcChatTiming), num))
			{
				throw new InvalidDataException($"NPC chat {npcId} has invalid timing {num}.");
			}
			L1jNpcChatTiming l1jNpcChatTiming = (L1jNpcChatTiming)num;
			if (data.Mob(text3) == null)
			{
				throw new InvalidDataException($"NPC chat {npcId} references missing mob '{text3}'.");
			}
			string[] array = ((jsonObject["chatTokens"] as JsonArray) ?? throw new InvalidDataException($"NPC chat {npcId}.chatTokens must be an array.")).Select((JsonNode token) => token?.GetValue<string>() ?? throw new InvalidDataException($"NPC chat {npcId} has a null chat token.")).ToArray();
			int num2 = array.Length;
			bool flag = ((num2 < 1 || num2 > 5) ? true : false);
			if (flag || array.Any((string token) => token.Length < 2 || token[0] != '$' || token.AsSpan(1).IndexOfAnyExceptInRange('0', '9') >= 0))
			{
				throw new InvalidDataException($"NPC chat {npcId} must contain one to five $NNN tokens.");
			}
			int num3 = RequiredInt(jsonObject, "startDelayMs");
			int num4 = RequiredInt(jsonObject, "chatIntervalMs");
			int num5 = RequiredInt(jsonObject, "repeatIntervalMs");
			int num6 = RequiredInt(jsonObject, "gameTime");
			bool flag2 = RequiredBool(jsonObject, "repeat");
			if (num3 < 0 || num4 < 0 || num5 < 0 || num6 < 0 || (flag2 && num5 <= 0))
			{
				throw new InvalidDataException($"NPC chat {npcId} has invalid timing values.");
			}
			L1jNpcChatDefinition value2 = new L1jNpcChatDefinition(npcId, text3, l1jNpcChatTiming, RequiredString(jsonObject, "note"), num3, new ReadOnlyCollection<string>(array), num4, RequiredBool(jsonObject, "shout"), RequiredBool(jsonObject, "worldChat"), flag2, num5, num6);
			if (!dictionary.TryAdd((text3, l1jNpcChatTiming), value2))
			{
				throw new InvalidDataException($"Duplicate NPC chat row for {text3}/{l1jNpcChatTiming}.");
			}
		}
		if (dictionary.Count != 38)
		{
			throw new InvalidDataException($"{"L1J_NPC_CHAT"} must contain main's 38 rows; got {dictionary.Count}.");
		}
		string[] referencedTokens = dictionary.Values.SelectMany((L1jNpcChatDefinition row) => row.ChatTokens).Distinct<string>(StringComparer.Ordinal).ToArray();
		if (referencedTokens.Length != 42 || referencedTokens.Any((string token) => !textByToken.ContainsKey(token)) || textByToken.Keys.Any((string token) => !referencedTokens.Contains<string>(token, StringComparer.Ordinal)))
		{
			throw new InvalidDataException("L1J_NPC_CHAT must resolve exactly all 42 referenced desc-c.tbl tokens.");
		}
		return new L1jNpcChatCatalog(new ReadOnlyDictionary<(string, L1jNpcChatTiming), L1jNpcChatDefinition>(dictionary), new ReadOnlyDictionary<string, string>(textByToken));
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_NPC_CHAT." + name + " must be an integer.")).GetValue<int>();
	}

	private static bool RequiredBool(JsonObject row, string name)
	{
		return (row[name] ?? throw new InvalidDataException("L1J_NPC_CHAT." + name + " must be a boolean.")).GetValue<bool>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw new InvalidDataException("L1J_NPC_CHAT." + name + " must be a string.");
	}
}
