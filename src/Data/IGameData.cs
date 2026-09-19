using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public interface IGameData
{
	string GameVersion { get; }

	int SaveVersion { get; }

	IReadOnlyCollection<string> TableNames { get; }

	JsonObject Db { get; }

	JsonObject Items { get; }

	JsonObject Mobs { get; }

	JsonObject Maps { get; }

	JsonObject Skills { get; }

	JsonObject Towns { get; }

	JsonObject Sets { get; }

	JsonNode? Table(string name);

	bool HasTable(string name);

	bool LoadFailed(string name);

	JsonObject? Item(string id);

	JsonObject? Mob(string key);

	JsonObject? Skill(string id);

	JsonNode? Resolve(JsonNode? node);
}
