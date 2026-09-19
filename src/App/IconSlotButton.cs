using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public partial class IconSlotButton : Button
{
	public const int SkillIconInset = 2;

	public const int SkillIconShift = 1;

	public const int QuickBarSkillLeadingInset = 1;

	private static readonly Color DisabledTint = new Color(1f, 1f, 1f, 0.4f);

	private TextureRect? _blessingGlowRect;

	private TextureRect? _iconRect;

	private ItemBlessing _blessingState;

	private bool _brokenBlade;

	private bool _dimmed;

	public override Control? _MakeCustomTooltip(string forText)
	{
		return ClassicItemTooltip.BuildText(forText);
	}

	public void SetDimmed(bool dimmed)
	{
		if (_dimmed != dimmed)
		{
			_dimmed = dimmed;
			QueueRedraw();
		}
	}

	public void SetBrokenBlade(bool broken)
	{
		if (_brokenBlade != broken)
		{
			_brokenBlade = broken;
			QueueRedraw();
		}
	}

	public void SetBlessingGlow(ItemBlessing blessing)
	{
		if (_blessingState != blessing)
		{
			_blessingState = blessing;
			RebuildBlessingGlow();
		}
	}

	public void SetIcon(Texture2D? icon, int inset = 0, int shiftX = 0, int shiftY = 0)
	{
		if (icon != null)
		{
			_iconRect = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				Texture = icon,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore
			};
			AddChild(_iconRect, forceReadableName: false, InternalMode.Disabled);
			_iconRect.SetAnchorsPreset(LayoutPreset.FullRect);
			if (inset > 0)
			{
				_iconRect.OffsetLeft = inset + shiftX;
				_iconRect.OffsetTop = inset + shiftY;
				_iconRect.OffsetRight = -inset + shiftX;
				_iconRect.OffsetBottom = -inset + shiftY;
			}
			RebuildBlessingGlow();
		}
	}

	private void RebuildBlessingGlow()
	{
		if (_blessingGlowRect != null)
		{
			RemoveChild(_blessingGlowRect);
			_blessingGlowRect.QueueFree();
			_blessingGlowRect = null;
		}
		if (_iconRect?.Texture != null && _blessingState != ItemBlessing.Normal)
		{
			_blessingGlowRect = ItemBlessingGlow.Create(_iconRect.Texture, _blessingState, _iconRect.StretchMode);
			if (_blessingGlowRect != null)
			{
				AddChild(_blessingGlowRect, forceReadableName: false, InternalMode.Disabled);
				_blessingGlowRect.SetAnchorsPreset(LayoutPreset.FullRect);
				_blessingGlowRect.OffsetLeft = _iconRect.OffsetLeft;
				_blessingGlowRect.OffsetTop = _iconRect.OffsetTop;
				_blessingGlowRect.OffsetRight = _iconRect.OffsetRight;
				_blessingGlowRect.OffsetBottom = _iconRect.OffsetBottom;
				MoveChild(_blessingGlowRect, _iconRect.GetIndex());
			}
		}
	}

	public void SetSkillIcon(Texture2D? icon)
	{
		SetIcon(icon, 2, 1, 1);
	}

	public void SetQuickBarSkillIcon(Texture2D? icon)
	{
		SetIcon(icon);
		if (_iconRect != null)
		{
			_iconRect.OffsetLeft = 1f;
			_iconRect.OffsetTop = 1f;
		}
	}

	public override void _Draw()
	{
		if (_brokenBlade)
		{
			DrawRect(new Rect2(new Vector2(2f, 2f), base.Size - new Vector2(4f, 4f)), InventoryGridSlot.BrokenWash);
		}
		if (_iconRect != null)
		{
			Color color = (_brokenBlade ? InventoryGridSlot.BrokenTint : Colors.White);
			if (base.Disabled || _dimmed)
			{
				color = new Color(color.R, color.G, color.B, DisabledTint.A);
			}
			if (_iconRect.Modulate != color)
			{
				_iconRect.Modulate = color;
			}
		}
	}
}
