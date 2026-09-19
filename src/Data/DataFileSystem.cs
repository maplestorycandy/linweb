using System;
using System.IO;

namespace IdleLineage.Data;

public static class DataFileSystem
{
	private static Func<string, bool>? _exists;

	private static Func<string, string>? _readAllText;

	private static Func<string, byte[]>? _readAllBytes;

	public static bool IsVirtual(string path)
	{
		if (!path.StartsWith("res://", StringComparison.Ordinal))
		{
			return path.StartsWith("user://", StringComparison.Ordinal);
		}
		return true;
	}

	public static void Install(Func<string, bool> exists, Func<string, string> readAllText, Func<string, byte[]> readAllBytes)
	{
		ArgumentNullException.ThrowIfNull(exists, "exists");
		ArgumentNullException.ThrowIfNull(readAllText, "readAllText");
		ArgumentNullException.ThrowIfNull(readAllBytes, "readAllBytes");
		_exists = exists;
		_readAllText = readAllText;
		_readAllBytes = readAllBytes;
	}

	public static bool Exists(string path)
	{
		return _exists?.Invoke(path) ?? File.Exists(path);
	}

	public static string ReadAllText(string path)
	{
		return _readAllText?.Invoke(path) ?? File.ReadAllText(path);
	}

	public static byte[] ReadAllBytes(string path)
	{
		return _readAllBytes?.Invoke(path) ?? File.ReadAllBytes(path);
	}

	public static string Combine(string basePath, string relative)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(basePath, "basePath");
		ArgumentException.ThrowIfNullOrWhiteSpace(relative, "relative");
		if (!IsVirtual(basePath))
		{
			return Path.Combine(basePath, relative);
		}
		if (!basePath.EndsWith('/'))
		{
			return basePath + "/" + relative;
		}
		return basePath + relative;
	}

	public static string? GetParent(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path, "path");
		if (!IsVirtual(path))
		{
			return Directory.GetParent(path)?.FullName;
		}
		string text = path.TrimEnd('/');
		int num = text.LastIndexOf('/');
		if (num > text.IndexOf("://", StringComparison.Ordinal) + 2)
		{
			return text.Substring(0, num);
		}
		return null;
	}

	public static string FullPath(string path)
	{
		if (!IsVirtual(path))
		{
			return Path.GetFullPath(path);
		}
		return path;
	}
}
