using System.IO;
using Godot;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class GodotDataFiles
{
	private static bool _installed;

	public static void EnsureInstalled()
	{
		if (!_installed)
		{
			_installed = true;
			DataFileSystem.Install(Exists, ReadAllText, ReadAllBytes);
		}
	}

	public static string? TryResolveDiskPath(string virtualPath)
	{
		try
		{
			if (virtualPath.StartsWith("res://", System.StringComparison.Ordinal))
			{
				string rel = virtualPath.Substring(6).Replace('/', Path.DirectorySeparatorChar);
				string exeDir = OS.GetExecutablePath().GetBaseDir();
				string direct = Path.Combine(exeDir, rel);
				if (File.Exists(direct))
				{
					return direct;
				}
				string global = ProjectSettings.GlobalizePath(virtualPath);
				if (File.Exists(global))
				{
					return global;
				}
				string cur = Path.Combine(Directory.GetCurrentDirectory(), rel);
				if (File.Exists(cur))
				{
					return cur;
				}
			}
		}
		catch { }
		return null;
	}

	public static Texture2D? TryLoadTexture(string virtualPath)
	{
		string? disk = TryResolveDiskPath(virtualPath);
		if (disk != null && File.Exists(disk))
		{
			try
			{
				var img = Image.LoadFromFile(disk);
				if (img != null) return ImageTexture.CreateFromImage(img);
			}
			catch { }
		}

		try
		{
			if (ResourceLoader.Exists(virtualPath))
			{
				var res = ResourceLoader.Load<Texture2D>(virtualPath);
				if (res != null) return res;
			}
		}
		catch { }

		return null;
	}

	private static bool Exists(string path)
	{
		if (!DataFileSystem.IsVirtual(path))
		{
			return File.Exists(path);
		}
		string? disk = TryResolveDiskPath(path);
		if (disk != null)
		{
			return true;
		}
		if (Godot.FileAccess.FileExists(path))
		{
			return true;
		}
		return false;
	}

	private static string ReadAllText(string path)
	{
		if (!DataFileSystem.IsVirtual(path))
		{
			return File.ReadAllText(path);
		}
		string? disk = TryResolveDiskPath(path);
		if (disk != null)
		{
			return File.ReadAllText(disk);
		}
		using Godot.FileAccess fileAccess = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (fileAccess != null)
		{
			return fileAccess.GetAsText();
		}
		throw Missing(path);
	}

	private static byte[] ReadAllBytes(string path)
	{
		if (!DataFileSystem.IsVirtual(path))
		{
			return File.ReadAllBytes(path);
		}
		string? disk = TryResolveDiskPath(path);
		if (disk != null)
		{
			return File.ReadAllBytes(disk);
		}
		using Godot.FileAccess fileAccess = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (fileAccess != null)
		{
			return fileAccess.GetBuffer((long)fileAccess.GetLength());
		}
		throw Missing(path);
	}

	private static IOException Missing(string path)
	{
		return new IOException($"Unable to read '{path}': {Godot.FileAccess.GetOpenError()}.");
	}
}
