using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public static class L1jNpcSpriteCatalog
{
	private sealed record Loaded(int TicksPerSecond, int TicksDefault, IReadOnlyDictionary<int, L1jGfxSprite> ByGfx, IReadOnlyDictionary<int, string> MissingSource);

	public const string TableName = "L1J_NPC_SPRITES";

	public const string IdleAction = "idle";

	private static readonly ConditionalWeakTable<IGameData, Loaded> Cache = new ConditionalWeakTable<IGameData, Loaded>();

	public static int TicksPerSecond(IGameData data)
	{
		return Cache.GetValue(Required(data), Build).TicksPerSecond;
	}

	public static int TicksDefault(IGameData data)
	{
		return Cache.GetValue(Required(data), Build).TicksDefault;
	}

	public static bool TryGet(IGameData data, int gfx, out L1jGfxSprite sprite)
	{
		sprite = null;
		return Cache.GetValue(Required(data), Build).ByGfx.TryGetValue(gfx, out sprite);
	}

	public static IReadOnlyDictionary<int, L1jGfxSprite> All(IGameData data)
	{
		return Cache.GetValue(Required(data), Build).ByGfx;
	}

	public static IReadOnlyDictionary<int, string> MissingSource(IGameData data)
	{
		return Cache.GetValue(Required(data), Build).MissingSource;
	}

	private static IGameData Required(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return data;
	}

	private static Loaded Build(IGameData data)
	{
		if (!(data.Table("L1J_NPC_SPRITES") is JsonObject jsonObject))
		{
			throw new InvalidDataException("L1J_NPC_SPRITES table failed to load.");
		}
		Dictionary<int, L1jGfxSprite> dictionary = new Dictionary<int, L1jGfxSprite>();
		foreach (KeyValuePair<string, JsonNode> item in jsonObject["byGfx"].AsObject())
		{
			item.Deconstruct(out var key, out var value);
			string s = key;
			JsonObject jsonObject2 = value.AsObject();
			Dictionary<string, L1jSpriteAction> dictionary2 = new Dictionary<string, L1jSpriteAction>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, JsonNode> item2 in jsonObject2["actions"].AsObject())
			{
				item2.Deconstruct(out key, out value);
				string text = key;
				JsonObject jsonObject3 = value.AsObject();
				dictionary2[text] = new L1jSpriteAction(text, (jsonObject3["block"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value2)) ? new int?(value2) : ((int?)null), (jsonObject3["ticks"] is JsonArray source) ? source.Select((JsonNode entry) => entry.GetValue<double>()).ToArray() : null);
			}
			int num = int.Parse(s);
			dictionary[num] = new L1jGfxSprite(num, jsonObject2["sourceGfx"].GetValue<int>(), jsonObject2["name"].GetValue<string>(), jsonObject2["attr"]?.GetValue<int>() ?? 0, (jsonObject2["renderedClothes"] is JsonArray source2) ? source2.Select(delegate(JsonNode layerNode, int index)
			{
				JsonObject jsonObject5 = layerNode.AsObject();
				return new L1jSpriteLayer((index == 0) ? "_w" : $"_w{index + 1}", jsonObject5["gfx"].GetValue<int>(), jsonObject5["attr"]?.GetValue<int>() ?? 0, jsonObject5["blend"]?.GetValue<string>() ?? "mix");
			}).ToArray() : Array.Empty<L1jSpriteLayer>(), (jsonObject2["shadow"] is JsonValue jsonValue2 && jsonValue2.TryGetValue<int>(out var value3)) ? new int?(value3) : ((int?)null), jsonObject2["static"].GetValue<bool>(), jsonObject2["inferred"].GetValue<bool>(), (from entry in jsonObject2["headings"].AsArray()
				select entry.GetValue<int>()).ToArray(), (jsonObject2["box"] is JsonObject jsonObject4) ? new L1jSpriteBox(jsonObject4["x0"].GetValue<int>(), jsonObject4["y0"].GetValue<int>(), jsonObject4["x1"].GetValue<int>(), jsonObject4["y1"].GetValue<int>()) : null, dictionary2);
		}
		Dictionary<int, string> missingSource = (from node in jsonObject["missingSource"].AsArray()
			select node.AsObject()).ToDictionary((JsonObject row) => row["gfx"].GetValue<int>(), (JsonObject row) => row["reason"].GetValue<string>());
		if (dictionary.Count == 0)
		{
			throw new InvalidDataException("L1J_NPC_SPRITES has no sprite.");
		}
		return new Loaded(jsonObject["ticksPerSecond"].GetValue<int>(), jsonObject["ticksDefault"].GetValue<int>(), dictionary, missingSource);
	}
}
