using System;
using System.Collections.Generic;
using Godot;

namespace IdleLineage.App;

public static class PatchPacks
{
	private const string Prefix = "patch";

	private const string Suffix = ".pck";

	private static readonly object Sync = new object();

	private static readonly List<string> AppliedNames = new List<string>();

	private static bool _loaded;

	public static IReadOnlyList<string> Applied
	{
		get
		{
			lock (Sync)
			{
				return AppliedNames.ToArray();
			}
		}
	}

	public static void LoadAll()
	{
		lock (Sync)
		{
			if (_loaded)
			{
				return;
			}
			_loaded = true;
		}
		string baseDir = OS.GetExecutablePath().GetBaseDir();
		if (baseDir.Length == 0)
		{
			return;
		}
		List<string> list = new List<string>();
		using (DirAccess dirAccess = DirAccess.Open(baseDir))
		{
			if (dirAccess == null)
			{
				return;
			}
			string[] files = dirAccess.GetFiles();
			foreach (string text in files)
			{
				if (text.StartsWith("patch", StringComparison.OrdinalIgnoreCase) && text.EndsWith(".pck", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(text);
				}
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		list.Sort(StringComparer.OrdinalIgnoreCase);
		foreach (string item in list)
		{
			if (ProjectSettings.LoadResourcePack(baseDir.PathJoin(item)))
			{
				lock (Sync)
				{
					AppliedNames.Add(item);
				}
				GD.Print("[Patch] 已套用差異包：" + item);
			}
			else
			{
				GD.PushWarning("[Patch] 差異包載入失敗，已略過：" + item);
			}
		}
	}
}
