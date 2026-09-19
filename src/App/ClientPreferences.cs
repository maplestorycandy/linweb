using Godot;

namespace IdleLineage.App;

public static class ClientPreferences
{
	private const string Path = "user://client_preferences.cfg";

	public static bool EffectsEnabled { get; private set; } = true;

	static ClientPreferences()
	{
		ConfigFile configFile = new ConfigFile();
		if (configFile.Load("user://client_preferences.cfg") == Error.Ok)
		{
			EffectsEnabled = (bool)configFile.GetValue("display", "effects", true);
		}
	}

	public static void SetEffects(bool enabled)
	{
		EffectsEnabled = enabled;
		Save();
	}

	private static void Save()
	{
		ConfigFile configFile = new ConfigFile();
		configFile.SetValue("display", "effects", EffectsEnabled);
		if (configFile.Save("user://client_preferences.cfg") != Error.Ok)
		{
			GD.PushWarning("[ClientPreferences] 無法儲存顯示設定。");
		}
	}
}
