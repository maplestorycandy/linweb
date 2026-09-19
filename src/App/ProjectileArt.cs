using System.Collections.Generic;
using Godot;

namespace IdleLineage.App;

public static class ProjectileArt
{
	public static readonly Vector2I ArrowCanvas = new Vector2I(49, 65);

	public static readonly Vector2 ArrowLatticeOrigin = new Vector2(3f, 55f);

	public const int ArrowFrames = 3;

	private static readonly Texture2D?[] _arrowTex = new Texture2D[24];

	private static bool _arrowLoaded;

	private static readonly Dictionary<string, Texture2D> _orbCache = new Dictionary<string, Texture2D>();

	internal static Texture2D? Arrow(int dir, int frame)
	{
		if (!_arrowLoaded)
		{
			_arrowLoaded = true;
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					_arrowTex[i * 3 + j] = LoadPng($"res://assets/fx/箭矢/arrow_d{i}_f{j}.png");
				}
			}
		}
		return _arrowTex[(dir & 7) * 3 + Mathf.PosMod(frame, 3)];
	}

	internal static Texture2D? LoadPng(string res)
	{
		Texture2D? diskTex = GodotDataFiles.TryLoadTexture(res);
		if (diskTex != null)
		{
			return diskTex;
		}
		if (ResourceLoader.Exists(res))
		{
			Texture2D texture2D = ResourceLoader.Load<Texture2D>(res, null, ResourceLoader.CacheMode.Reuse);
			if (texture2D != null)
			{
				return texture2D;
			}
		}
		using FileAccess fileAccess = FileAccess.Open(res, FileAccess.ModeFlags.Read);
		if (fileAccess == null)
		{
			return null;
		}
		byte[] buffer = fileAccess.GetBuffer((long)fileAccess.GetLength());
		Image image = new Image();
		if (image.LoadPngFromBuffer(buffer) != Error.Ok)
		{
			return null;
		}
		return ImageTexture.CreateFromImage(image);
	}

	internal static Texture2D Orb(Color col)
	{
		string key = col.ToHtml();
		if (_orbCache.TryGetValue(key, out Texture2D value))
		{
			return value;
		}
		int num = 30;
		float num2 = (float)(num - 1) / 2f;
		Image image = Image.CreateEmpty(num, num, useMipmaps: false, Image.Format.Rgba8);
		Color color = new Color(1f, 1f, 1f);
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < num; j++)
			{
				float num3 = new Vector2(((float)j - num2) / num2, ((float)i - num2) / num2).Length();
				Color color2;
				float a;
				if (num3 < 0.2f)
				{
					color2 = color;
					a = 1f;
				}
				else if (num3 < 0.6f)
				{
					color2 = color.Lerp(col, (num3 - 0.2f) / 0.4f);
					a = 1f;
				}
				else
				{
					color2 = col;
					a = Mathf.Clamp(1f - (num3 - 0.6f) / 0.4f, 0f, 1f);
				}
				image.SetPixel(j, i, new Color(color2.R, color2.G, color2.B, a));
			}
		}
		ImageTexture imageTexture = ImageTexture.CreateFromImage(image);
		_orbCache[key] = imageTexture;
		return imageTexture;
	}
}
