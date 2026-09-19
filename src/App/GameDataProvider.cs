using Godot;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class GameDataProvider
{
	private static GameData? _shared;

	public static GameData Shared => _shared ?? (_shared = Load());

	private static GameData Load()
	{
		GodotDataFiles.EnsureInstalled();
		GameData gameData = new GameData("res://data/tables");
		ContentAdditions.Apply(gameData);
		GD.Print("[GameDataProvider] 內容層：新增套用完成（物品退役已全數實體化——見 data/l1j-item-retirements.json）。");
		return gameData;
	}
}
