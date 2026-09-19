using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Core;
using IdleLineage.Data;
using IdleLineage.Network;
using IdleLineage.Ui;

namespace IdleLineage.App;

public sealed partial class ArpgEngineScreen : Control
{
	private sealed class ProjView
	{
		public Node2D Node;

		public Sprite2D Spr;

		public bool IsArrow;

		public int Dir = -1;

		public int Frame = -1;

		public double FrameSeconds;
	}

	private sealed class BarGauge
	{
		private readonly TextureRect _fill;

		private readonly Vector2 _full;

		public BarGauge(TextureRect fill, Vector2 full)
		{
			_fill = fill;
			_full = full;
		}

		public void Set(double percent, string fillPath)
		{
			AtlasTexture atlasTexture = (AtlasTexture)_fill.Texture;
			if (atlasTexture.Atlas.ResourcePath != fillPath)
			{
				atlasTexture.Atlas = GD.Load<Texture2D>(fillPath);
			}
			float num = (float)Mathf.Clamp(percent / 100.0, 0.0, 1.0);
			_fill.Visible = num * _full.X >= 1f;
			if (_fill.Visible)
			{
				atlasTexture.Region = new Rect2(0f, 0f, (float)atlasTexture.Atlas.GetWidth() * num, atlasTexture.Atlas.GetHeight());
				_fill.Size = new Vector2(_full.X * num, _full.Y);
			}
		}
	}

	private sealed record BagGridEntry(string ItemKey, string Tooltip, string Corner, Action? Activate, bool Draggable, bool Locked, Color? Quality = null, bool QualityFrame = false, bool BrokenBlade = false, string StackUid = "", Color? FrameQuality = null, ItemBlessing BlessingState = ItemBlessing.Normal);

	private sealed class GroundDrop
	{
		public readonly ItemStack Stack;

		public readonly string ItemKey;

		public readonly Label? CountLabel;

		public readonly ItemBlessing Blessing;

		public readonly int Enhancement;

		public readonly bool IsIdentified;

		public readonly int ItemLevel;

		public readonly IReadOnlyList<EquipmentAffixRoll> Affixes;

		public int Quantity;

		public float LifeSeconds;

		public readonly Node2D Node;

		public bool PickupRequested;

		public bool PickupArrivalStopped;

		public bool PickupAnimationStarted;

		public string DropId = "";

		public double PickupAnimationRemaining;

		public GroundDrop(Node2D node, ItemStack stack, float lifeSeconds, Label? countLabel)
		{
			Stack = stack;
			Node = node;
			ItemKey = stack.ItemKey;
			Quantity = (int)Math.Min(2147483647L, stack.Quantity);
			Blessing = stack.Blessing;
			Enhancement = stack.Enhancement;
			IsIdentified = stack.IsIdentified;
			ItemLevel = stack.ItemLevel;
			Affixes = stack.Affixes.ToArray();
			LifeSeconds = lifeSeconds;
			CountLabel = countLabel;
		}
	}

	private sealed partial class GroundDropClickTarget : Control
	{
		public Action? OnPressed;

		public override void _GuiInput(InputEvent inputEvent)
		{
			if (inputEvent is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed)
			{
				OnPressed?.Invoke();
				AcceptEvent();
			}
		}
	}

	private sealed partial class InventoryWorldDropTarget : Control
	{
		public Action<Vector2, string>? OnDropItem;

		public override void _Notification(int what)
		{
			if ((long)what == 21)
			{
				base.MouseFilter = MouseFilterEnum.Pass;
			}
			else if ((long)what == 22)
			{
				base.MouseFilter = MouseFilterEnum.Ignore;
			}
		}

		public override bool _CanDropData(Vector2 atPosition, Variant data)
		{
			if (data.VariantType != Variant.Type.String)
			{
				return false;
			}
			string text = data.AsString();
			if (text.Length == 0)
			{
				return false;
			}
			(string ItemKey, string StackUid, bool HasStackUid) tuple = ItemDragPayload.Decode(text);
			var (text2, _, _) = tuple;
			if (!tuple.HasStackUid)
			{
				text2 = text;
			}
			if (text2.Length > 0)
			{
				return !text2.StartsWith("skill:", StringComparison.Ordinal);
			}
			return false;
		}

		public override void _DropData(Vector2 atPosition, Variant data)
		{
			OnDropItem?.Invoke(atPosition, data.AsString());
		}
	}

	private sealed partial class BagFilterButton : Button
	{
		public Action<string>? OnItemDropped { get; set; }

		public override bool _CanDropData(Vector2 atPosition, Variant data)
		{
			if (data.VariantType != Variant.Type.String) return false;
			string text = data.AsString();
			if (string.IsNullOrEmpty(text)) return false;
			var (itemKey, _, _) = ItemDragPayload.Decode(text);
			return !string.IsNullOrEmpty(itemKey) && !itemKey.StartsWith("skill:", StringComparison.Ordinal);
		}

		public override void _DropData(Vector2 atPosition, Variant data)
		{
			string text = data.AsString();
			var (itemKey, _, _) = ItemDragPayload.Decode(text);
			if (!string.IsNullOrEmpty(itemKey))
			{
				OnItemDropped?.Invoke(itemKey);
			}
		}
	}

	private sealed partial class FilterDropPanel : PanelContainer
	{
		public Action<string>? OnItemDropped { get; set; }

		public override bool _CanDropData(Vector2 atPosition, Variant data)
		{
			if (data.VariantType != Variant.Type.String) return false;
			string text = data.AsString();
			if (string.IsNullOrEmpty(text)) return false;
			var (itemKey, _, _) = ItemDragPayload.Decode(text);
			return !string.IsNullOrEmpty(itemKey) && !itemKey.StartsWith("skill:", StringComparison.Ordinal);
		}

		public override void _DropData(Vector2 atPosition, Variant data)
		{
			string text = data.AsString();
			var (itemKey, _, _) = ItemDragPayload.Decode(text);
			if (!string.IsNullOrEmpty(itemKey))
			{
				OnItemDropped?.Invoke(itemKey);
			}
		}
	}

	private readonly record struct WorldNpcStaticPresentation(Node2D Root, Rect2 LocalVisualBounds, bool StaticSprite);

	private enum TeleportSource
	{
		Skill,
		Scroll
	}

	private const int AtlasWarmLimit = 24;

	private const double AtlasWarmIntervalSeconds = 0.12;

	private readonly Queue<string> _atlasWarmQueue = new Queue<string>();

	private double _atlasWarmCd;

	private const float BarPanelWidth = 309f;

	private const float BarPanelBodyWidth = 253f;

	private const float SettingsPanelWidth = 520f;

	private const float SettingsColumnWidth = 227f;

	private const float WideBarPanelBodyWidth = 464f;

	private static readonly Color BarPanelGold = Color.FromHtml("#e6c76a".AsSpan());

	private static readonly Color BarPanelText = Color.FromHtml("#c9d1de".AsSpan());

	private static readonly Color BarPanelDim = Color.FromHtml("#8b95a6".AsSpan());

	private static readonly Color BarPanelGood = Color.FromHtml("#8fdd8f".AsSpan());

	private string _clanPartyMessage = "";

	private bool _clanPartyMessageGood;

	private Node2D? _castleWarCrown;

	private double _castleWarSnapshotCountdown;

	private bool _castleWarAnnounced;

	private readonly Dictionary<Combatant, Sprite2D> _castleWarDoorVisuals = new Dictionary<Combatant, Sprite2D>();

	private readonly List<(Sprite2D Sprite, Texture2D OpenTexture)> _castleWarDoorRepairs = new List<(Sprite2D, Texture2D)>();

	private bool _charmTargeting;

	private Control? _classicLeftPanel;

	private Control? _classicRightPanel;

	private string _classicLeftKind = "";

	private string _classicRightKind = "";

	private string _classicEquipmentStatus = "";

	private static readonly (CollectionBookKind Book, string Label)[] HuntCollectionBooks = new(CollectionBookKind, string)[3]
	{
		(CollectionBookKind.Equipment, "裝備"),
		(CollectionBookKind.Misc, "道具"),
		(CollectionBookKind.Card, "怪物")
	};

	private const string CombatOnTexture = "res://assets/ui/buttons/combat-toggle/5208c.png";

	private const string CombatOffTexture = "res://assets/ui/buttons/combat-toggle/5209c.png";

	private static readonly Vector2 CombatToggleSize = new Vector2(24f, 12f);

	private TextureButton _combatToggle;

	private Texture2D _combatOnTexture;

	private Texture2D _combatOffTexture;

	private const float ViewW = 800f;

	private const float ViewH = 600f;

	private float _viewW = 800f;

	private float _viewH = 600f;

	private const float BarH = 134f;

	private const float WalkHoldSeconds = 0.08f;

	private const float OverlayMargin = 8f;

	private const int MaxEnemies = 6;

	private static readonly Color BoltColor = Color.FromHtml("#ce93d8".AsSpan());

	private AtlasBridge _atlas;

	private GameSession _session;

	private PlayerBuild _build;

	private string _mapKey = "zone_37";

	private L1jMapRule _mapRule;

	private string _mapName = "";

	private Node2D _world;

	private Node2D _groundDropLayer;

	private InventoryWorldDropTarget _bagDropTarget;

	private Node2D _arena;

	private SpellFx _spellFx;

	private Control _ui;

	private readonly List<GroundDrop> _groundDrops = new List<GroundDrop>();

	private struct PendingDeathTransform
	{
		public double Remaining;
		public string TargetMobKey;
		public WorldPoint Pos;
	}
	private readonly List<PendingDeathTransform> _pendingDeathTransforms = new List<PendingDeathTransform>();

	private Vector2 _camOffset;

	private sealed partial class PurpleFrameOverlay : Control
	{
		public float Alpha = 0f;
		public override void _Draw()
		{
			if (Alpha <= 0.001f) return;
			Vector2 sz = GetViewportRect().Size;
			Color c1 = new Color(0.62f, 0.18f, 0.95f, Alpha * 0.75f);
			Color c2 = new Color(0.85f, 0.45f, 1.0f, Alpha * 0.9f);
			float t = 14f;
			DrawRect(new Rect2(0, 0, sz.X, t), c1);
			DrawRect(new Rect2(0, sz.Y - t, sz.X, t), c1);
			DrawRect(new Rect2(0, 0, t, sz.Y), c1);
			DrawRect(new Rect2(sz.X - t, 0, t, sz.Y), c1);
			DrawRect(new Rect2(t, t, sz.X - 2 * t, 2f), c2);
			DrawRect(new Rect2(t, sz.Y - t - 2f, sz.X - 2 * t, 2f), c2);
			DrawRect(new Rect2(t, t, 2f, sz.Y - 2 * t), c2);
			DrawRect(new Rect2(sz.X - t - 2f, t, 2f, sz.Y - 2 * t), c2);
		}
	}

	private PurpleFrameOverlay? _purpleFrameOverlay;
	private float _screenShakeDuration;
	private float _screenShakeIntensity;
	private Vector2 _screenShakeOffset = Vector2.Zero;
	private bool _isGuardianActive;
	private double _guardianTimeRemaining;
	private float _purpleFramePulse;
	private float _purpleFrameRemaining;
	private string _savedOriginalPlayerName = "";

	private readonly Random _rng = new Random(20260726);

	private EngineAdapter _engine;

	private bool _initialised;

	private readonly Dictionary<Combatant, ArpgActor> _views = new Dictionary<Combatant, ArpgActor>();

	private readonly Dictionary<long, ProjView> _projViews = new Dictionary<long, ProjView>();

	private const float RenderCullMargin = 192f;

	private ArpgActor? _playerView;

	private double _elapsed;

	private readonly HashSet<Combatant> _killed = new HashSet<Combatant>();

	private Action _onExit;

	private Action _onChangeMap;

	private Action? _pendingScreenTransition;

	private bool _paused;

	private Control? _pauseMenu;

	private bool _dead;

	private Control? _deathPanel;

	private Control? _bagPanel;

	private int _bagPage;

	private Control? _summonPicker;

	private WorldCollisionGrid? _grid;

	private CollisionDebugLayer? _gridDebug;

	private WorldGridCell _probeCell = new WorldGridCell(-1, -1);

	private const double CombatBgmGraceSeconds = 3.0;

	private const double CombatBgmProbeSeconds = 0.2;

	private string _musicScene = "";

	private bool _combatBgmEngaged;

	private bool _combatBgmEngagedBoss;

	private bool _combatBgmGraceBoss;

	private double _combatBgmGrace;

	private double _combatBgmProbeRemaining;

	private Control _hud;

	private Control _overlayUi;

	private Label _areaName;

	private HBoxContainer _buffIcons;

	private string _buffIconSig = "";

	private TextureRect _hpFill;

	private Label _hpTxt;

	private TextureRect _mpFill;

	private Label _mpTxt;

	private const float HpMpFrameWidth = 334f;

	private const float HpMpFrameHeight = 34f;

	private const float HpMpFrameChatOverlapY = 12f;

	private const float HpMpGrooveHpX = 9f;

	private const float HpMpGrooveMpX = 184f;

	private const float HpMpBarInsetY = 10f;

	private const float HpBarWidth = 142f;

	private const float MpBarWidth = 142f;

	private const float VitalHeight = 14f;

	private const float VitalTextRise = 2f;

	private const float SlotNumRise = 3f;

	private float _hpFillRight;

	private Label _metaLv;

	private Label _metaExp;

	private BarGauge _expGauge;

	private Label _metaAc;

	private Label _alignValue;

	private Label _metaMr;

	private Label _metaMagicDmg;

	private Label _slabInfo;

	private Label _slabWeight;

	private TextureRect _alignIcon;

	private TextureRect _adenaIcon;

	private Label _adenaValue;

	private BarGauge _satietyGauge;

	private Label _satietyPct;

	private TextureRect _satietyWarn;

	private BarGauge _weightGauge;

	private Label _weightPct;

	private TextureRect _weightWarn;

	private ColorRect _underwaterEnvironment;

	private ColorRect _nightMask;

	private BackBufferCopy _backBuffer;

	private SubViewport _shadowViewport;

	private NightShadowCaster _shadowCaster;

	private bool _visionOccluded;

	private float _visionCutoffSq = float.PositiveInfinity;

	private readonly List<Rect2> _shadowRects = new List<Rect2>();

	private const float ShadowRangePx = 640f;

	private const float ShadowMapScale = 0.25f;

	private Vector2 _lastShadowPlayerScreen = new Vector2(float.NaN, float.NaN);

	private Vector2 _lastShadowOffset = new Vector2(float.NaN, float.NaN);

	private int _shadowCellKeyX = int.MinValue;

	private int _shadowCellKeyY = int.MinValue;

	private bool _lastDark;

	private int _shownLoadTier = -1;

	private WeightReport _weight;

	private double _weightCd;
	private double _companionAutoSaveCd = 30.0;

	private RichTextLabel _slabLog;
	private DraggableChatBar? _draggableChatBar;
	private DraggableHpMpBar? _draggableHpMpBar;
	private LineEdit? _chatInput;
	private PanelContainer? _playerChatBubble;
	private Label? _playerChatBubbleLabel;
	private float _playerChatBubbleTimer = 0f;
	private enum ChatLogTab { All, Chat, Drops }
	private ChatLogTab _currentLogTab = ChatLogTab.All;
	private readonly List<(ChatLogTab Category, string Line)> _allLogEntries = new();
	private Button? _tabBtnAll;
	private Button? _tabBtnChat;
	private Button? _tabBtnDrops;

	private readonly Queue<string> _slabLines = new Queue<string>();

	private readonly List<(string Id, IconSlotButton Btn, int Mp)> _skillBtns = new List<(string, IconSlotButton, int)>();

	private readonly ICombatRandom _potionRng = new SeededCombatRandom(System.Environment.TickCount);

	private bool _wasdW;

	private bool _wasdA;

	private bool _wasdS;

	private bool _wasdD;

	private bool _wasdMoving;

	private Vector2 _wasdDirection;

	private double _hudCd;

	private readonly HashSet<Combatant> _liveScratch = new HashSet<Combatant>();

	private Predicate<Combatant>? _notInLiveScratch;

	private float _revealEleCd;

	private bool _revealMobElement;

	private double _environmentSoundProbe;

	private const float RnLeftX = 0f;

	private const float RnChatX = 129f;

	private const float RnCellX = 22f;

	private const float RnCellY = 28f;

	private const float RnCellPitch = 34f;

	private const float RnCellSize = 31f;

	private const float RnIconY = 104f;

	private const float RnIconPitch = 20f;

	private const float RnIconW = 19f;

	private const float RnIconH = 26f;

	private const string GaugeLowPath = "res://assets/ui/rn_gauge_low.png";

	private const string GaugeMidPath = "res://assets/ui/rn_gauge_mid.png";

	private const string GaugeHighPath = "res://assets/ui/rn_gauge_high.png";

	private const string ExpBarFillPath = "res://assets/ui/rn_level_bar.png";

	private const string AdenaIconPath = "res://assets/ui/rn_adena_btn.png";

	private const string CarryWarnPath = "res://assets/ui/rn_carry_warn.png";

	private const string FoodWarnPath = "res://assets/ui/rn_food_warn.png";

	private static readonly Vector2 WeightBarSize = new Vector2(38f, 16f);

	private static readonly Vector2 ExpBarSize = new Vector2(98f, 16f);

	private const float LeftIconX = 11f;

	private const double WeightMidThreshold = 50.0;

	private const double WeightHighThreshold = 80.0;

	private const double SatietyWarnThreshold = 20.0;

	private const string AlignEvilPath = "res://assets/ui/rn_align_chaotic.png";

	private const string AlignJusticePath = "res://assets/ui/rn_align_lawful.png";

	private const string AlignNeutralPath = "res://assets/ui/rn_align_neutral.png";

	private const float PauseScale = 2.5f;

	private float _pauseBodyX;

	private float _pauseBodyW;

	private static readonly Vector2 QuickPageArrowSize = new Vector2(18f, 21f);

	private static readonly Vector2 TeleportMemoryButtonSize = new Vector2(18f, 20f);

	internal static readonly Vector2 QuickSlotSize = new Vector2(31f, 31f);

	private const double AutoHealBelow = 0.7;

	private static readonly Vector2 DeathPanelSize = new Vector2(430f, 190f);

	private const string L1jRestartFeelGoodSkillId = "sk_heal_mid";

	private const string L1jRestartFeelGoodMessage = "感覺舒服多了。";

	private bool _reviveTargeting;

	private const float ReviveTargetPickRadius = 56f;

	private string _darkEntBarkUid = "";

	private const float DarkEntBarkPickRadius = 56f;

	private Control? _consulDialog;

	private readonly List<(double X, double Y, JsonObject Door)> _pendingDoors = new List<(double, double, JsonObject)>();

	private JsonObject? _doorSprites;

	private double _doorMaterialiseCd;

	private const double DoorBuildRadius = 1400.0;

	private L1jDungeonRandomCatalog? _l1jDungeonRandom;

	private (int X, int Y)? _lastDungeonRandomPlayerCell;

	private const string MapAssetRoot = "assets/maps";

	private MapTopology? _topology;

	private ExplorationSpawnSession? _spawnSession;

	private IReadOnlySet<MapSpawnCell>? _villageCells;

	private IReadOnlyList<MapSpawnPoint> _fixedSpawnPoints = Array.Empty<MapSpawnPoint>();

	private readonly Dictionary<Combatant, string> _fixedSpawnSlots = new Dictionary<Combatant, string>();

	private MapSpawnCell? _lastPlayerCell;

	private double _lastSpawnCheckTime;

	private double _mapEntryTimeSeconds;

	private MapPageStreamingSession? _pageStream;

	private Node2D? _pageLayer;

	private Node2D? _forePageLayer;

	private readonly Dictionary<MapPageCoordinate, Sprite2D> _pageNodes = new Dictionary<MapPageCoordinate, Sprite2D>();

	private readonly Dictionary<MapPageCoordinate, (Sprite2D Node, MapPage Page)> _forePageNodes = new Dictionary<MapPageCoordinate, (Sprite2D, MapPage)>();

	private readonly Dictionary<MapPageCoordinate, MapPage> _pendingPages = new Dictionary<MapPageCoordinate, MapPage>();

	private readonly Dictionary<MapPageCoordinate, MapPage> _pendingForePages = new Dictionary<MapPageCoordinate, MapPage>();

	private readonly HashSet<MapPageCoordinate> _wantedPages = new HashSet<MapPageCoordinate>();

	private readonly List<MapPageCoordinate> _arrivedPages = new List<MapPageCoordinate>();

	private Vector2 _lastStreamCenter = new Vector2(float.NaN, float.NaN);

	private readonly List<int> _occlusionProbe = new List<int>();

	private readonly List<int> _playerOcclusionProbe = new List<int>();

	private readonly List<int> _actorOcclusionProbe = new List<int>();

	private int _occlusionSignature = -1;

	private bool _forePagesDirty;

	private static Shader? _occlusionFadeShader;

	private const int ForePageZIndex = 1500;

	private const float PlayerOcclusionHalfWidth = 24f;

	private const float PlayerOcclusionVisibleHeight = 54f;

	private const int ForeActiveIdCapacity = 16;

	private const float FlameConsulPickRadius = 56f;

	private const float FlameConsulTalkRangeSquared = 9216f;

	private Vector2? _flameConsulPos;

	private Control? _flameConsulDialog;

	private readonly List<(MapLinks.Gate Gate, Vector2 Pos)> _gates = new List<(MapLinks.Gate, Vector2)>();

	private bool _gatesArmed = true;

	private const double PortalFramesPerSecond = 8.0;

	private static readonly HashSet<string> TwistedSpacePortalLandmarks = new HashSet<string>(StringComparer.Ordinal) { "shadow_temple_portal", "desire_cave_portal" };

	private const float GroundDropMinimumHitSize = 26f;

	private const float GroundDropPickupRadius = 18f;

	private const float GroundDropLifeSeconds = 180f;

	private const float GroundDropPillarWidth = 28f;

	private const float GroundDropPillarHeight = 104f;

	private const float GroundDropGlowWidth = 52f;

	private const float GroundDropGlowHeight = 30f;

	private static readonly Color GroundDropGreen = new Color(0.49803922f, 0.8156863f, 46f / 85f);

	private static readonly Color GroundDropBlue = new Color(0.2627451f, 28f / 51f, 1f);

	private static readonly Color GroundDropPurple = new Color(0.6509804f, 0.36078432f, 1f);

	private static readonly Color GroundDropOrange = new Color(1f, 0.6156863f, 23f / 85f);

	private static readonly Color GroundDropGold = new Color(0.9490196f, 0.75686276f, 26f / 85f);

	private static readonly Dictionary<string, Texture2D> GroundDropGlowTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	private static Shader? _groundDropPillarShader;

	private static readonly Vector2[] DragDropHeadingSteps = new Vector2[8]
	{
		new Vector2(-1f, -0.5f).Normalized(),
		new Vector2(0f, -1f).Normalized(),
		new Vector2(1f, -0.5f).Normalized(),
		new Vector2(1f, 0f).Normalized(),
		new Vector2(1f, 0.5f).Normalized(),
		new Vector2(0f, 1f).Normalized(),
		new Vector2(-1f, 0.5f).Normalized(),
		new Vector2(-1f, 0f).Normalized()
	};

	private static readonly Vector2[] DragDropFacingOffsets = new Vector2[8]
	{
		new Vector2(-24f, -12f),
		new Vector2(0f, -24f),
		new Vector2(24f, -12f),
		new Vector2(48f, 0f),
		new Vector2(24f, 12f),
		new Vector2(0f, 24f),
		new Vector2(-24f, 12f),
		new Vector2(-48f, 0f)
	};

	private const float MapArtScale = 1.3333334f;

	private Node2D? _ferryShip;

	private readonly ICombatRandom _hostileRng = new SeededCombatRandom(System.Environment.TickCount ^ 0x2B17);

	private Action? _onQuitToMenu;

	private TownScreen? _townNpcHost;

	private bool? _integratedTownSafeState;

	private MapTopology? _integratedTownEntryCacheTopology;

	private readonly Dictionary<string, (int X, int Y)> _integratedTownEntryCells = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

	private IntegratedTownDefinition? _integratedTownLocation;

	private bool? _integratedTownLocationSafe;

	private bool _integratedTownLocationHasCell;

	private int _integratedTownLocationCellX;

	private int _integratedTownLocationCellY;

	private string _areaNameText = "";

	private string _worldAtlasCaptionName = "";

	private double _integratedTownCd;

	private readonly List<(double X, double Y, L1jNpcSpawn Npc)> _pendingWorldNpcs = new List<(double, double, L1jNpcSpawn)>();

	private readonly List<(double X, double Y, L1jNpcSpawn Npc, Combatant? Actor)> _liveWorldNpcs = new List<(double, double, L1jNpcSpawn, Combatant)>();

	private Control? _worldNpcPanel;

	private double _worldNpcCd;

	private int _worldNpcBornSeq;

	private readonly Dictionary<Combatant, L1jNpcCombatSpritePlayer> _worldNpcCombatVisuals = new Dictionary<Combatant, L1jNpcCombatSpritePlayer>();

	private readonly Dictionary<(int NpcId, int CellX, int CellY), WorldNpcStaticPresentation> _worldNpcStaticVisuals = new Dictionary<(int, int, int), WorldNpcStaticPresentation>();

	private readonly Dictionary<int, List<WorldNpcStaticPresentation>> _opaqueWorldObjectVisualsByXBucket = new Dictionary<int, List<WorldNpcStaticPresentation>>();

	private readonly Dictionary<(int NpcId, int CellX, int CellY), float> _worldNpcNameplateOffsets = new Dictionary<(int, int, int), float>();

	private readonly Dictionary<int, List<MapOcclusionGroup>> _worldNpcOccludersByXBucket = new Dictionary<int, List<MapOcclusionGroup>>();

	private const int WorldNpcFrontDepthMinimum = 1501;

	private const int WorldNpcFrontDepthMaximum = 1800;

	private const float WorldNpcOcclusionBucketWidth = 512f;

	private const float WorldNpcVisibleHalfWidth = 48f;

	private const float WorldNpcFallbackHeight = 54f;

	private readonly HashSet<string> _reportedMissingNpcArt = new HashSet<string>(StringComparer.Ordinal);

	private const double WorldNpcBuildRadius = 1400.0;

	private const double WorldNpcDematerialiseRadius = 2000.0;

	private Label? _mapSelectionStatus;

	private TextureRect? _miniMap;

	private ColorRect? _miniMapDot;

	private ColorRect? _miniMapDotEdge;

	private AtlasTexture? _miniMapAtlas;

	private Rect2 _miniMapRect;

	private string _miniMapMapKey = "";

	private WorldAtlasDefinition? _miniMapDefinition;

	private const float MiniMapDotSize = 3f;

	private const float MiniMapDotEdgeSize = 5f;
	private readonly List<(ColorRect Edge, ColorRect Fill)> _miniMapTeammateDots = new();

	private readonly L1jMobGroupRuntime _l1jMobGroups = new L1jMobGroupRuntime();

	private L1jMobGroupCatalog? _l1jMobGroupCatalog;

	private Dictionary<string, int[]>? _mobGroupsByLeader;

	private L1jNpcChatCatalog? _l1jNpcChatCatalog;

	private L1jNpcChatRuntime? _l1jNpcChatRuntime;

	private double _perfProbeRemaining = ReadPerfProbeSeconds();

	private bool _perfProbeStarted;

	private StringBuilder? _perfProbeRows;

	private ulong _perfProbeLastUsec;

	private long _perfProbeLastStep = -1L;

	private readonly Stopwatch _perfProbeWatch = new Stopwatch();

	private readonly double[] _perfProbeMarks = new double[23];

	private int _perfProbePlanCount;

	private double _perfProbeSpawnAccumMs;

	private double _perfProbeGroupAccumMs;

	private double _perfProbeCreateAccumMs;

	private double _perfProbeAddAccumMs;

	private long _perfProbeGen0;

	private long _perfProbeGen2;

	private string _petTamingItemUid = "";

	private string _petEvolutionItemUid = "";

	private const float PetTargetPickRadius = 56f;

	private Control? _duranDialog;

	private readonly List<(int Slot, string Assignment, string ItemKey, string StackUid, IconSlotButton Btn)> _quickItemBtns = new List<(int, string, string, string, IconSlotButton)>();

	private readonly Action?[] _quickSlotAction = new Action[8];

	private readonly List<Control> _quickEmptyTargets = new List<Control>();

	private Control? _floatingQuickBarPanel;

	private Button? _floatingQuickBarToggle;

	private Panel? _utilityBarPanel;

	private Button? _btnUtilityNightVision;

	private readonly Action?[] _floatingQuickSlotAction = new Action[8];

	private Action? _bagRefresh;

	private double _potionClock;

	private double _lastPotionAt = double.NegativeInfinity;

	private double _lastCompanionPotionAt = double.NegativeInfinity;

	private double _lastMagicScrollAt = double.NegativeInfinity;

	private const double MagicScrollCooldownSeconds = 0.8;

	private bool IsMagicScrollOnCooldown => (_potionClock - _lastMagicScrollAt) < MagicScrollCooldownSeconds;

	private double MagicScrollCooldownRemaining => Math.Max(0.0, MagicScrollCooldownSeconds - (_potionClock - _lastMagicScrollAt));

	private const int AutoBadgeSize = 12;

	private Control? _itemTargetOverlay;

	private Node2D? _roiFollowerRoot;

	private L1jNpcSpawn? _roiFollowerDialogNpc;

	private Vector2 _roiFollowerPosition;

	private bool _roiEscortActive;

	private string _manualSkillTargetId = "";
	private ItemStack? _manualSkillScrollStack;

	private Control? _tpMemoryPanel;

	private VBoxContainer? _tpMemoryList;

	private Label? _tpMemoryStatus;

	private LineEdit? _tpMemoryName;

	private const float TpMemoryFrameWidth = 640f;

	private const float TpMemoryFrameHeight = 430f;

	private L1jTrapRuntime? _l1jTraps;

	private (int X, int Y)? _lastTrapPlayerCell;

	private long _trapClockMs;

	private readonly ICombatRandom _ubRng = new SeededCombatRandom((int)Time.GetTicksUsec() ^ 0x5642);

	private Control? _worldAtlasPanel;

	private Control? _worldAtlasMarker;

	private Label? _worldAtlasCaption;

	private WorldAtlasDefinition? _worldAtlasDefinition;

	private Rect2 _worldAtlasImageRect;

	private const float WorldTargetFallbackPickRadius = 56f;

	private const float WorldTargetVisualPickPadding = 6f;

	private string CastleWarFactionIdentity => CastleWarStore.FactionIdentity(_session);

	private bool CharmTargeting => _charmTargeting;

	private Vector2 RightAnchor => new Vector2(View.X - 309f - 8f, 8f);

	public Rect2 Field { get; private set; } = new Rect2(0f, 0f, 2400f, 1500f);

	private Vector2 View => new Vector2(_viewW, _viewH);

	private float BarY => _viewH - 134f;

	private Vector2 WorldView => new Vector2(_viewW, _viewH - 134f);

	private Vector2 CamAnchor => new Vector2(_viewW / 2f, (_viewH - 134f) / 2f);

	private float RnChatTop => _viewH - 130f;

	private float RnMiniX => _viewW - 315f;

	private float RnChatW => RnMiniX + 10f - 129f;

	private float RnMacroX => _viewW - 167f;

	private float RnLeftY => _viewH - 127f;

	private float RnPanelY => _viewH - 134f;

	private Vector2 QuickPageArrowPos => new Vector2(RnMacroX + 2f, RnPanelY + 35f);

	private Vector2 TeleportMemoryButtonPos => new Vector2(RnMacroX + 2f, RnPanelY + 66f);

	private Vector2 DeathPanelOrigin => new Vector2((WorldView.X - DeathPanelSize.X) * 0.5f, (WorldView.Y - DeathPanelSize.Y) * 0.5f);

	private bool DarkEntBarkTargeting => _darkEntBarkUid.Length > 0;

	private bool HasTopology => _topology != null;

	public int LoadedPageCount => _pageNodes.Count;

	public IEnumerable<Combatant> LivingMobs
	{
		get
		{
			foreach (Combatant combatant in _engine.Combatants)
			{
				if (combatant.Kind == CombatantKind.Mob && combatant.IsAlive)
				{
					yield return combatant;
				}
			}
		}
	}

	public int LivingNormalMobCount => _engine.Engine.LivingNormalMobCount;

	private IReadOnlyList<IntegratedTownDefinition> IntegratedTowns
	{
		get
		{
			if (!HasTopology)
			{
				return Array.Empty<IntegratedTownDefinition>();
			}
			return IntegratedTownCatalog.FindAllByMap(_mapKey);
		}
	}

	private bool HasIntegratedTown => IntegratedTowns.Count > 0;

	private string IntegratedSpawnMapKey => CurrentIntegratedTown?.HuntingMapKey ?? _mapKey;

	private IntegratedTownDefinition? CurrentIntegratedTown
	{
		get
		{
			IntegratedTownDefinition integratedTownDefinition = IntegratedTownCatalog.FindByTown(_session.TownKey);
			if ((object)integratedTownDefinition != null && string.Equals(integratedTownDefinition.MapKey, _mapKey, StringComparison.Ordinal))
			{
				return integratedTownDefinition;
			}
			return IntegratedTowns.FirstOrDefault();
		}
	}

	private bool OwnsL1jWorldNpcs => L1jWorldNpcCatalog.Owns(GameDataProvider.Shared, _mapKey);

	private static long NowMs => DateTimeOffset.Now.ToUnixTimeMilliseconds();

	private L1jMobGroupCatalog MobGroupCatalog => _l1jMobGroupCatalog ?? (_l1jMobGroupCatalog = L1jMobGroupCatalog.Load(GameDataProvider.Shared));

	private Dictionary<string, int[]> MobGroupsByLeader => _mobGroupsByLeader ?? (_mobGroupsByLeader = MobGroupCatalog.Groups.Values.GroupBy<L1jMobGroupDefinition, string>((L1jMobGroupDefinition definition) => definition.LeaderMobKey, StringComparer.Ordinal).ToDictionary<IGrouping<string, L1jMobGroupDefinition>, string, int[]>((IGrouping<string, L1jMobGroupDefinition> bucket) => bucket.Key, (IGrouping<string, L1jMobGroupDefinition> bucket) => (from definition in bucket
		select definition.GroupId into id
		orderby id
		select id).ToArray(), StringComparer.Ordinal));

	private L1jNpcChatRuntime NpcChatRuntime => _l1jNpcChatRuntime ?? (_l1jNpcChatRuntime = new L1jNpcChatRuntime(_l1jNpcChatCatalog ?? (_l1jNpcChatCatalog = L1jNpcChatCatalog.Load(GameDataProvider.Shared))));

	private double PerfProbeNowMs
	{
		get
		{
			if (!(_perfProbeRemaining > 0.0) || !_perfProbeStarted)
			{
				return 0.0;
			}
			return _perfProbeWatch.Elapsed.TotalMilliseconds;
		}
	}

	private bool PetItemTargeting
	{
		get
		{
			if (_petTamingItemUid.Length <= 0)
			{
				return _petEvolutionItemUid.Length > 0;
			}
			return true;
		}
	}

	private double PotionIdleSeconds => _potionClock - _lastPotionAt;

	private double CompanionPotionIdleSeconds => _potionClock - _lastCompanionPotionAt;

	private bool ManualSkillTargeting => _manualSkillTargetId.Length > 0;

	private L1jUbSession? UltimateBattle => _session.UltimateBattle;

	private bool InUltimateBattle
	{
		get
		{
			L1jUbSession ultimateBattle = UltimateBattle;
			if (ultimateBattle != null && !ultimateBattle.IsFinished)
			{
				return string.Equals(_mapKey, ultimateBattle.Arena.MapKey, StringComparison.Ordinal);
			}
			return false;
		}
	}

	private bool UltimateBattleOwnsSpawning => InUltimateBattle;

	private void QueueMapAtlasWarmup()
	{
		_atlasWarmQueue.Clear();
		_atlasWarmCd = 0.6;
		if (_mapKey.Length == 0)
		{
			return;
		}
		int localX = 0;
		int localY = 0;
		bool flag = _topology?.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out localX, out localY) ?? false;
		Dictionary<string, int> nearest = new Dictionary<string, int>(StringComparer.Ordinal);
		try
		{
			if (_fixedSpawnPoints.Count > 0)
			{
				foreach (MapSpawnPoint fixedSpawnPoint in _fixedSpawnPoints)
				{
					int distance = (flag ? Math.Max(Math.Abs(fixedSpawnPoint.Cell.X - localX), Math.Abs(fixedSpawnPoint.Cell.Y - localY)) : 0);
					Note(fixedSpawnPoint.MobKey, distance);
				}
			}
			else
			{
				foreach (string mapMobKey in _engine.GetMapMobKeys(_mapKey))
				{
					Note(mapMobKey, 0);
				}
			}
			foreach (string item in from pair in nearest.OrderBy<KeyValuePair<string, int>, int>((KeyValuePair<string, int> pair) => pair.Value).ThenBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> pair) => pair.Key, StringComparer.Ordinal).Take(6)
				select pair.Key)
			{
				if (_atlas != null && _atlas.HasFrames("anim", item)) continue;
				_atlasWarmQueue.Enqueue(item);
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[Exploration] Warmup skipped for map {_mapKey}: {ex.Message}");
		}
		void Note(string mobKey, int num)
		{
			if (mobKey.Length != 0)
			{
				string text = MobAtlasName(mobKey);
				if (text.Length != 0 && (!nearest.TryGetValue(text, out var value) || num < value))
				{
					nearest[text] = num;
				}
			}
		}
	}

	private void AtlasWarmStep(double delta)
	{
		if (_atlasWarmQueue.Count == 0)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null || !HasIntegratedTown || (!_engine.Engine.IsSafeZone(_engine.Player.Pos) && topology.HasSafeZone))
		{
			_atlasWarmCd -= delta;
			if (!(_atlasWarmCd > 0.0))
			{
				_atlasWarmCd = 0.35;
				string mobAtlas = _atlasWarmQueue.Dequeue();
				if (_atlas != null && !_atlas.HasFrames("anim", mobAtlas))
				{
					_atlas.BuildFrames("anim", mobAtlas);
				}
			}
		}
	}

	private void OpenHuntAttributeScrollPanel(string scrollUid, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		int num = ((itemStack != null) ? L1jAttrEnchantRules.KindOfScroll(shared, itemStack.ItemKey) : 0);
		if (itemStack == null || num == 0)
		{
			return;
		}
		string text = L1jAttrEnchantRules.KindName(num);
		VBoxContainer vBoxContainer = CreateItemTargetFrame(text + "之武器強化卷軸", new Vector2(540f, 430f));
		vBoxContainer.AddChild(ItemPanelLabel($"選擇要賦予「{text}」屬性的武器。成功率 {10}%；" + "成功與失敗都消耗一張，取消則不消耗。", "#c9d1de", 14, 44f), forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(ItemPanelLabel(message, message.StartsWith("✓") ? "#8fdd8f" : "#e2938f", 14, 36f), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<ItemStack> readOnlyList = L1jAttrEnchantRules.EligibleTargets(shared, _engine.Player, num);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(ItemPanelLabel("沒有可以賦予「" + text + "」屬性的武器（安定值為負者不可強化；同屬性 3 階已滿）。", "#8b95a6", 14, 44f), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(520f, 320f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			bool isEquipped = _engine.Player.EquippedItems.Values.Any(eq => eq.Uid == captured.Uid);

			PanelContainer rowPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0f, 34f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			StyleBoxFlat rowStyle = new StyleBoxFlat
			{
				BgColor = isEquipped ? new Color(0.12f, 0.16f, 0.22f, 0.85f) : new Color(0.09f, 0.11f, 0.14f, 0.8f),
				BorderColor = isEquipped ? Color.FromHtml("#3d5475") : Color.FromHtml("#262c37"),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 2,
				CornerRadiusBottomRight = 2,
				CornerRadiusTopLeft = 2,
				CornerRadiusTopRight = 2
			};
			rowStyle.SetContentMarginAll(2f);
			rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

			HBoxContainer hBoxContainer = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.Begin
			};
			hBoxContainer.AddThemeConstantOverride("separation", 8);

			Control iconControl = ItemIcons.Slot(captured.ItemKey);
			iconControl.CustomMinimumSize = new Vector2(28f, 28f);
			iconControl.Size = new Vector2(28f, 28f);
			hBoxContainer.AddChild(iconControl, forceReadableName: false, InternalMode.Disabled);

			string equipTag = isEquipped ? "[裝備中] " : "";
			string text2 = (captured.IsIdentified ? ((captured.Enhancement == 0) ? "+0 " : $"{captured.Enhancement:+#;-#} ") : "");
			string attrStr = (captured.IsIdentified && captured.AttrEnchantLevel > 0) ? ("  " + AttributeScrollText.Describe(captured)) : "";
			string fullName = equipTag + text2 + L1jItemIdentityRules.DisplayName(shared, captured) + attrStr;

			Label label = new Label
			{
				Text = fullName,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Off,
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				CustomMinimumSize = new Vector2(280f, 28f)
			};
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", isEquipped ? Color.FromHtml("#8fd0ff") : Color.FromHtml("#d2d8e4"));
			hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);

			Button button = new Button
			{
				Text = "選擇賦予",
				CustomMinimumSize = new Vector2(96f, 28f),
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			button.AddThemeFontSizeOverride("font_size", 12);
			button.AddThemeColorOverride("font_color", Color.FromHtml("#e8c07a"));
			button.AddThemeColorOverride("font_hover_color", Color.FromHtml("#ffffff"));
			button.Pressed += delegate
			{
				OpenHuntAttributeScrollConfirmation(scrollUid, captured.Uid);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			rowPanel.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(rowPanel, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void OpenHuntAttributeScrollConfirmation(string scrollUid, string targetUid)
	{
		GameData data = GameDataProvider.Shared;
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		ItemStack itemStack2 = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid) ?? _engine.Player.EquippedItems.Values.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
		if (itemStack == null || itemStack2 == null)
		{
			OpenHuntAttributeScrollPanel(scrollUid, "物品已不在身上；未消耗任何物品。");
			return;
		}
		int num = L1jAttrEnchantRules.KindOfScroll(data, itemStack.ItemKey);
		int num2 = ((itemStack2.AttrEnchantKind != num) ? 1 : (itemStack2.AttrEnchantLevel + 1));
		VBoxContainer vBoxContainer = CreateItemTargetFrame("確認屬性強化", new Vector2(500f, 340f));
		vBoxContainer.AddChild(ItemPanelLabel("武器：" + HuntEquippedMark(itemStack2) + L1jItemIdentityRules.DisplayName(data, itemStack2) + "\n" + (itemStack2.IsIdentified ? ("目前屬性：" + AttributeScrollText.Describe(itemStack2) + "\n") : "") + $"成功後：{L1jAttrEnchantRules.KindName(num)} {num2} 階" + "（追加傷害 " + AttributeScrollText.BonusOf(num2) + "）\n\n" + $"成功率 {10}%；失敗時武器完全不變，" + "但卷軸仍會消耗。", "#e2938f", 15, 180f), forceReadableName: false, InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		hBoxContainer.AddThemeConstantOverride("separation", 28);
		hBoxContainer.AddChild(ClassicArtButtons.Confirm(delegate
		{
			L1jAttrEnchantResult result = L1jAttrEnchantRules.TryEnchant(data, _engine.Player, scrollUid, targetUid, confirmed: true, _potionRng);
			if (result.Attempted)
			{
				SaveManager.Save(_session);
			}
			string text = AttributeScrollText.Outcome(data, result);
			SlabLog(result.Succeeded ? ("[color=#8fdd8f]" + text + "[/color]") : ("[color=#e2938f]" + text + "[/color]"));
			if (_engine.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == scrollUid))
			{
				OpenHuntAttributeScrollPanel(scrollUid, text);
			}
			else
			{
				CloseItemTargetOverlay();
			}
		}, "同意並使用"), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(ClassicArtButtons.Cancel(delegate
		{
			OpenHuntAttributeScrollPanel(scrollUid);
		}, "取消；不消耗卷軸"), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private string HuntEquippedMark(ItemStack stack)
	{
		if (!_engine.Player.EquippedItems.Values.Any((ItemStack equipped) => equipped.Uid == stack.Uid))
		{
			return "";
		}
		return "〔裝備中〕";
	}

	private void ToggleClanParty()
	{
		if (_classicRightKind != "clanparty")
		{
			_clanPartyMessage = "";
		}
		ToggleRightAnchor("clanparty", BuildClanPartyPanel);
	}

	private Control BuildClanPartyPanel()
	{
		(Control Panel, VBoxContainer Body) tuple = BarPanelShell("血盟 · 隊伍", 400f);
		Control item = tuple.Panel;
		VBoxContainer item2 = tuple.Body;
		try
		{
			ClanBook book = ClanStore.Book;
			if (!book.Exists)
			{
				item2.AddChild(BarRow("尚未加入血盟。", BarPanelDim, 13), forceReadableName: false, InternalMode.Disabled);
				item2.AddChild(BarRow("血盟由王族在城鎮的血盟 NPC 創立，整個帳號共用。", BarPanelDim, 12, wrap: true, 464f), forceReadableName: false, InternalMode.Disabled);
			}
			else
			{
				item2.AddChild(BarRow($"「{book.Name}」\u3000成員 {book.MemberCount} 人", BarPanelGold, 16), forceReadableName: false, InternalMode.Disabled);
				item2.AddChild(BarRow(book.OwnsHouse ? $"持有盟屋：房屋編號 {book.HouseId}" : "持有盟屋：無", BarPanelText, 12), forceReadableName: false, InternalMode.Disabled);
				item2.AddChild(BarRow($"血盟倉庫：{book.Warehouse.Items.Count} / {200} 件", BarPanelText, 12), forceReadableName: false, InternalMode.Disabled);
				item2.AddChild(BarRow("血盟倉庫在城鎮的血盟執行人。", BarPanelDim, 11, wrap: true, 464f), forceReadableName: false, InternalMode.Disabled);
			}
			BuildPartyMenu(item2);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[BuildClanPartyPanel] Error: {ex}");
			item2.AddChild(BarRow("載入血盟隊伍資訊時發生異常：" + ex.Message, BarPanelDim, 12), forceReadableName: false, InternalMode.Disabled);
		}
		return item;
	}

	private void BuildPartyMenu(VBoxContainer body)
	{
		IGameData shared = GameDataProvider.Shared;
		Combatant player = _engine.Player;
		int count = _session.Party.Members.Count;
		int value = _session.Party.ActiveMonsterCharmCost(shared, player);
		int value2 = MonsterCardRules.ActiveCharmCapacity(player);
		int num = _engine.Combatants.Count((Combatant actor) => actor.Kind == CombatantKind.Pet && actor.IsAlive);
		body.AddChild(BarGap(4f), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(BarRow("── 隊伍 ──", BarPanelGold, 13), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(BarRow($"隊伍 {count + 1}/{8}\u3000魅力 {value}/{value2}", BarPanelText, 12), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(BarRow((num > 0) ? $"領隊：{player.Disp}\u3000另有出戰寵物 {num} 隻（不占隊伍格）" : ("領隊：" + player.Disp), BarPanelDim, 11, wrap: true, 464f), forceReadableName: false, InternalMode.Disabled);
		if (_clanPartyMessage.Length > 0)
		{
			body.AddChild(BarRow(_clanPartyMessage, _clanPartyMessageGood ? BarPanelGood : Color.FromHtml("#e2938f".AsSpan()), 11, wrap: true, 464f), forceReadableName: false, InternalMode.Disabled);
		}
		List<(ItemStack, string, string, MonsterCardGrade)> list = new List<(ItemStack, string, string, MonsterCardGrade)>();
		foreach (MercenaryContract member in _session.Party.Members)
		{
			try
			{
				if (MonsterCompanionRules.TryReadMobKey(member.CharacterKey, out string mobKey) && shared.Mob(mobKey) != null)
				{
					ItemStack itemStack = MonsterCardRules.OwnedCard(player, mobKey);
					if (itemStack != null)
					{
						list.Add((itemStack, mobKey, CombatSkill.ReadString(shared.Mob(mobKey), "n"), MonsterCardRules.Grade(shared, mobKey)));
					}
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[BuildPartyMenu] Member load error: {ex}");
			}
		}
		if (list.Count == 0)
		{
			body.AddChild(BarRow("目前沒有出戰中的怪物夥伴。請在背包雙擊怪物卡片召喚。", BarPanelDim, 11, wrap: true, 464f), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		body.AddChild(BarRow("出戰中的怪物夥伴", BarPanelGold, 12), forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Name = "DeployedMonsterScroll",
			CustomMinimumSize = new Vector2(464f, 150f),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Name = "DeployedMonsterList",
			CustomMinimumSize = new Vector2(448f, 0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer.AddThemeConstantOverride("separation", 3);
		scrollContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		body.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (var (card, mobKey2, name, grade) in list)
		{
			vBoxContainer.AddChild(DeployedMonsterRow(shared, card, mobKey2, name, grade), forceReadableName: false, InternalMode.Disabled);
		}
	}

	private Control DeployedMonsterRow(IGameData data, ItemStack card, string mobKey, string name, MonsterCardGrade grade)
	{
		int value = MonsterCardRules.ActiveCharmCostFor(data, mobKey, _engine.Player);
		string value2 = grade switch
		{
			MonsterCardGrade.Gold => "頭目", 
			MonsterCardGrade.Silver => "強力", 
			_ => "一般", 
		};
		Combatant combatant = _engine.Combatants.FirstOrDefault((Combatant actor) => string.Equals(actor.Key, MonsterCompanionRules.CardCharacterKey(mobKey), StringComparison.Ordinal));
		string value3 = ((combatant != null && !combatant.IsAlive) ? "倒下" : "出戰");
		int value4 = combatant?.Level ?? card.MonsterCardLevel;
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.CustomMinimumSize = new Vector2(448f, 38f);
		hBoxContainer.TooltipText = $"{value2}怪物；出戰魅力成本 {value}";
		hBoxContainer.AddThemeConstantOverride("separation", 6);
		Label label = BarRow($"{name}\u3000Lv{value4}\u3000{value2}／{value}\u3000{value3}", BarPanelGood, 11);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		string cardUid = card.Uid;
		Button button = new Button
		{
			Name = "PartyRecallButton",
			Text = "收回",
			CustomMinimumSize = new Vector2(64f, 30f),
			FocusMode = FocusModeEnum.None
		};
		button.AddThemeFontSizeOverride("font_size", 11);
		button.Pressed += delegate
		{
			RecallPartyCardFromMenu(cardUid);
		};
		hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		return hBoxContainer;
	}

	private void RecallPartyCardFromMenu(string cardUid)
	{
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => string.Equals(stack.Uid, cardUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			_clanPartyMessage = "找不到這張怪物卡片，請重新開啟隊伍選單。";
			_clanPartyMessageGood = false;
			RebuildClanPartyPanel();
			return;
		}
		IGameData shared = GameDataProvider.Shared;
		if (!MonsterCardRules.TryReadMobKey(shared, itemStack, out string mobKey) || _session.Party.FindMonsterCard(mobKey) == null)
		{
			_clanPartyMessage = "這隻怪物已不在出戰隊伍中。";
			_clanPartyMessageGood = false;
			RebuildClanPartyPanel();
			return;
		}
		MonsterCardToggleResult result = MonsterCardPartyRules.Toggle(shared, _session.Party, _engine.Player, itemStack, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _engine.Engine);
		if (!result.Success)
		{
			_clanPartyMessage = MonsterCardPartyRules.FailureText(result);
			_clanPartyMessageGood = false;
			RebuildClanPartyPanel();
		}
		else
		{
			string text = CombatSkill.ReadString(shared.Mob(result.MobKey), "n");
			bool flag = SaveManager.Save(_session);
			_clanPartyMessage = "✓ " + text + " 已收回卡片；冷卻 5 分鐘" + (flag ? "" : "（存檔失敗，請勿直接關閉遊戲）");
			_clanPartyMessageGood = flag;
			RebuildClanPartyPanel();
		}
	}

	private void RebuildClanPartyPanel()
	{
		CloseClassicRight();
		Control panel = (_classicRightPanel = BuildClanPartyPanel());
		_classicRightKind = "clanparty";
		AddAboveBarPanel(panel, keepRightEdge: true);
	}

	private void ToggleSettings()
	{
		ToggleRightAnchor("settings", BuildSettingsPanel);
	}

	private Control BuildSettingsPanel()
	{
		(Control Panel, VBoxContainer Body) tuple = SettingsPanelShell(460f);
		Control item = tuple.Panel;
		VBoxContainer item2 = tuple.Body;
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(464f, 0f)
		};
		hBoxContainer.AddThemeConstantOverride("separation", 10);
		item2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(227f, 0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(227f, 0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		hBoxContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		Label node = BarRow("", BarPanelDim, 12, wrap: true, 227f);
		Button fullscreen = new Button
		{
			Text = FullscreenLabel(),
			CustomMinimumSize = new Vector2(227f, 34f)
		};
		fullscreen.Pressed += delegate
		{
			ToggleFullscreen();
			fullscreen.Text = FullscreenLabel();
		};
		vBoxContainer.AddChild(fullscreen, forceReadableName: false, InternalMode.Disabled);
		Button nightVisionBtn = new Button
		{
			Text = $"🌙 妖精夜視: {(_session.ElfNightVisionEnabled ? "已開啟" : "已關閉")}",
			CustomMinimumSize = new Vector2(227f, 34f)
		};
		nightVisionBtn.Pressed += delegate
		{
			ToggleElfNightVision();
			nightVisionBtn.Text = $"🌙 妖精夜視: {(_session.ElfNightVisionEnabled ? "已開啟" : "已關閉")}";
		};
		vBoxContainer.AddChild(nightVisionBtn, forceReadableName: false, InternalMode.Disabled);
		BarAudioRow(vBoxContainer, "\ud83d\udd0a 音效", () => GameAudio.Instance?.SfxOn ?? false, () => GameAudio.Instance?.SfxVolume ?? 0f, delegate(bool on)
		{
			if (GameAudio.Instance != null)
			{
				GameAudio.Instance.SfxOn = on;
				GameAudio.Instance.SaveConfig();
				if (on)
				{
					GameAudio.Instance.PlayEvent("crit");
				}
			}
		}, delegate(float v)
		{
			if (GameAudio.Instance != null)
			{
				GameAudio.Instance.SfxVolume = v;
				GameAudio.Instance.SaveConfig();
			}
		});
		BarAudioRow(vBoxContainer, "\ud83c\udfb5 音樂", () => GameAudio.Instance?.BgmOn ?? false, () => GameAudio.Instance?.BgmVolume ?? 0f, delegate(bool on)
		{
			if (GameAudio.Instance != null)
			{
				GameAudio.Instance.BgmOn = on;
				GameAudio.Instance.SaveConfig();
			}
		}, delegate(float v)
		{
			if (GameAudio.Instance != null)
			{
				GameAudio.Instance.BgmVolume = v;
				GameAudio.Instance.SaveConfig();
			}
		});
		vBoxContainer.AddChild(BarGap(6f), forceReadableName: false, InternalMode.Disabled);
		BuildAutomationSettings(vBoxContainer);
		BuildAttackPrioritySettings(vBoxContainer2);
		vBoxContainer.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		return item;
	}

	private void BuildAttackPrioritySettings(VBoxContainer body)
	{
		body.AddChild(BarRow("── 玩家攻擊優先 ──", BarPanelGold, 13), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(BarRow("最多三項；未選不參與，全空預設最近敵人。", BarPanelDim, 10, wrap: true, 227f), forceReadableName: false, InternalMode.Disabled);
		PlayerAttackPriority[] array = PlayerAttackPriorityRules.Normalize(_session.AttackPriorities);
		_session.AttackPriorities = PlayerAttackPriorityRules.KeysOf(array);
		_engine.Player.AttackPriorities = array;
		OptionButton[] selectors = new OptionButton[3];
		for (int i = 0; i < selectors.Length; i++)
		{
			HBoxContainer hBoxContainer = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(227f, 30f)
			};
			hBoxContainer.AddThemeConstantOverride("separation", 4);
			Label label = BarRow($"{i + 1}.", BarPanelText, 12);
			label.CustomMinimumSize = new Vector2(22f, 28f);
			label.VerticalAlignment = VerticalAlignment.Center;
			hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			OptionButton optionButton = new OptionButton
			{
				CustomMinimumSize = new Vector2(201f, 28f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			optionButton.AddItem("未設定", 0);
			foreach (PlayerAttackPriorityOption option in PlayerAttackPriorityRules.Options)
			{
				optionButton.AddItem(option.Label, (int)(option.Value + 1));
			}
			StyleAttackPrioritySelector(optionButton);
			if (i < array.Length)
			{
				optionButton.Select((int)(array[i] + 1));
			}
			selectors[i] = optionButton;
			hBoxContainer.AddChild(optionButton, forceReadableName: false, InternalMode.Disabled);
			body.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
		for (int j = 0; j < selectors.Length; j++)
		{
			int changedIndex = j;
			selectors[j].ItemSelected += delegate
			{
				ApplyAttackPrioritySelectors(selectors, changedIndex);
			};
		}
		BuildCompanionAttackPrioritySettings(body);
	}

	private void BuildCompanionAttackPrioritySettings(VBoxContainer body)
	{
		body.AddChild(BarRow("── 夥伴攻擊優先 ──", BarPanelGold, 13), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(BarRow("最多三項；未選不參與，全空預設最近敵人。", BarPanelDim, 10, wrap: true, 227f), forceReadableName: false, InternalMode.Disabled);
		CompanionAttackPriority[] array = CompanionAttackPriorityRules.Normalize(_session.CompanionAttackPriorities);
		_session.CompanionAttackPriorities = CompanionAttackPriorityRules.KeysOf(array);
		_engine.Player.CompanionAttackPriorities = array;
		OptionButton[] selectors = new OptionButton[3];
		for (int i = 0; i < selectors.Length; i++)
		{
			HBoxContainer hBoxContainer = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(227f, 30f)
			};
			hBoxContainer.AddThemeConstantOverride("separation", 4);
			Label label = BarRow($"{i + 1}.", BarPanelText, 12);
			label.CustomMinimumSize = new Vector2(22f, 28f);
			label.VerticalAlignment = VerticalAlignment.Center;
			hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			OptionButton optionButton = new OptionButton
			{
				CustomMinimumSize = new Vector2(201f, 28f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			optionButton.AddItem("未設定", 0);
			foreach (CompanionAttackPriorityOption option in CompanionAttackPriorityRules.Options)
			{
				optionButton.AddItem(option.Label, (int)(option.Value + 1));
			}
			StyleAttackPrioritySelector(optionButton);
			if (i < array.Length)
			{
				optionButton.Select((int)(array[i] + 1));
			}
			selectors[i] = optionButton;
			hBoxContainer.AddChild(optionButton, forceReadableName: false, InternalMode.Disabled);
			body.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
		for (int j = 0; j < selectors.Length; j++)
		{
			int changedIndex = j;
			selectors[j].ItemSelected += delegate
			{
				ApplyCompanionAttackPrioritySelectors(selectors, changedIndex);
			};
		}
	}

	private void ApplyAttackPrioritySelectors(IReadOnlyList<OptionButton> selectors, int changedIndex)
	{
		int selectedId = selectors[changedIndex].GetSelectedId();
		if (selectedId > 0)
		{
			for (int i = 0; i < selectors.Count; i++)
			{
				if (i != changedIndex && selectors[i].GetSelectedId() == selectedId)
				{
					selectors[i].Select(0);
				}
			}
		}
		PlayerAttackPriority[] array = (from selector in selectors
			select selector.GetSelectedId() into id
			where id > 0
			select (PlayerAttackPriority)(id - 1)).Take(3).ToArray();
		_engine.Player.AttackPriorities = array;
		_session.AttackPriorities = PlayerAttackPriorityRules.KeysOf(array);
	}

	private void ApplyCompanionAttackPrioritySelectors(IReadOnlyList<OptionButton> selectors, int changedIndex)
	{
		int selectedId = selectors[changedIndex].GetSelectedId();
		if (selectedId > 0)
		{
			for (int i = 0; i < selectors.Count; i++)
			{
				if (i != changedIndex && selectors[i].GetSelectedId() == selectedId)
				{
					selectors[i].Select(0);
				}
			}
		}
		CompanionAttackPriority[] array = (from selector in selectors
			select selector.GetSelectedId() into id
			where id > 0
			select (CompanionAttackPriority)(id - 1)).Take(3).ToArray();
		_engine.Player.CompanionAttackPriorities = array;
		_session.CompanionAttackPriorities = CompanionAttackPriorityRules.KeysOf(array);
	}

	private static void StyleAttackPrioritySelector(OptionButton selector)
	{
		StyleBoxFlat styleBoxFlat = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#171714".AsSpan()),
			BorderColor = Color.FromHtml("#66552c".AsSpan()),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 6f,
			ContentMarginRight = 4f,
			ContentMarginTop = 2f,
			ContentMarginBottom = 2f
		};
		selector.AddThemeStyleboxOverride("normal", styleBoxFlat);
		selector.AddThemeStyleboxOverride("hover", styleBoxFlat.Duplicate() as StyleBoxFlat);
		selector.AddThemeStyleboxOverride("pressed", styleBoxFlat.Duplicate() as StyleBoxFlat);
		selector.AddThemeColorOverride("font_color", BarPanelText);
		selector.AddThemeColorOverride("font_hover_color", Colors.White);
		selector.AddThemeFontSizeOverride("font_size", 12);
	}

	private void BuildAutomationSettings(VBoxContainer body)
	{
		body.AddChild(BarRow("── 自動施放條件 ──", BarPanelGold, 13), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(AutomationThresholdRow("藥水\u3000HP 低於", _session.AutoPotionHpPercent, 1, delegate(int value)
		{
			_session.AutoPotionHpPercent = value;
		}, "自動喝治療藥水的血量門檻。"), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(AutomationSliderRow("夥伴治癒技低於", _session.CompanionAutoHealSkillHpPercent, 1, delegate(int value)
		{
			_session.CompanionAutoHealSkillHpPercent = value;
		}, "已開啟自動且 main 允許幫盟友施放的治癒技能，會治療低於此 HP 的範圍內怪物夥伴。"), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(AutomationSliderRow("夥伴藥水低於", _session.CompanionAutoPotionHpPercent, 1, delegate(int value)
		{
			_session.CompanionAutoPotionHpPercent = value;
		}, "低於門檻時自動從背包使用夥伴專用藥水；由恢復量高到低。冷卻長度與玩家藥水相同，但各自獨立。"), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(AutomationThresholdRow("技能\u3000MP 不低於", _session.AutoSkillMpPercent, 0, delegate(int value)
		{
			_session.AutoSkillMpPercent = value;
		}, "留一點 MP 不要被自動施放榨乾。0＝只看技能本身的 MP 消耗。"), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(AutomationThresholdRow("耗血技\u3000HP 高於", _session.AutoSkillHpPercent, 0, delegate(int value)
		{
			_session.AutoSkillHpPercent = value;
		}, "只影響會扣自己 HP 的技能（如血之契約系）；血量低於門檻就不自動放。\n0＝關掉這個條件。手動按熱鍵不受限制。"), forceReadableName: false, InternalMode.Disabled);
	}

	private static Control AutomationThresholdRow(string label, int value, int minimum, Action<int> setValue, string hint = "")
	{
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(227f, 32f)
		};
		Label label2 = BarRow(label, BarPanelText, 12);
		label2.VerticalAlignment = VerticalAlignment.Center;
		label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (hint.Length > 0)
		{
			label2.TooltipText = hint;
		}
		if (hint.Length > 0)
		{
			hBoxContainer.TooltipText = hint;
		}
		SpinBox spinBox = new SpinBox
		{
			MinValue = minimum,
			MaxValue = 100.0,
			Step = 1.0,
			Value = value,
			Suffix = "%",
			CustomMinimumSize = new Vector2(76f, 30f),
			AllowGreater = false,
			AllowLesser = false
		};
		spinBox.ValueChanged += delegate(double changed)
		{
			setValue((int)changed);
		};
		hBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(spinBox, forceReadableName: false, InternalMode.Disabled);
		return hBoxContainer;
	}

	private static Control AutomationSliderRow(string label, int value, int minimum, Action<int> setValue, string hint = "")
	{
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(227f, 32f)
		};
		hBoxContainer.AddThemeConstantOverride("separation", 4);
		Label label2 = BarRow(label, BarPanelText, 12);
		label2.VerticalAlignment = VerticalAlignment.Center;
		label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		Label valueLabel = BarRow($"{value}%", BarPanelText, 11);
		valueLabel.CustomMinimumSize = new Vector2(34f, 30f);
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.VerticalAlignment = VerticalAlignment.Center;
		HSlider hSlider = new HSlider
		{
			MinValue = minimum,
			MaxValue = 100.0,
			Step = 1.0,
			Value = value,
			CustomMinimumSize = new Vector2(72f, 30f),
			SizeFlagsHorizontal = SizeFlags.ShrinkEnd
		};
		hSlider.ValueChanged += delegate(double changed)
		{
			int num = (int)changed;
			valueLabel.Text = $"{num}%";
			setValue(num);
		};
		if (hint.Length > 0)
		{
			hBoxContainer.TooltipText = hint;
			label2.TooltipText = hint;
			hSlider.TooltipText = hint;
		}
		hBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(hSlider, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(valueLabel, forceReadableName: false, InternalMode.Disabled);
		return hBoxContainer;
	}

	private void SetAutoSkillEnabled(string skillId, bool enabled)
	{
		if (SkillExecutionRules.IsManualOnly(GameDataProvider.Shared, skillId))
		{
			_session.AutoCast.Remove(skillId);
		}
		else if (enabled)
		{
			_session.AutoCast.Add(skillId);
			if (IsMagicAttackSkill(skillId))
			{
				_engine.Player.AutoAttackSkillId = skillId;
			}
		}
		else
		{
			_session.AutoCast.Remove(skillId);
			if (_engine.Player.AutoAttackSkillId == skillId)
			{
				SyncAutoAttackSkill();
			}
		}
	}

	private (Control Panel, VBoxContainer Body) BarPanelShell(string title, float height, float titleYOffset = 0f)
	{
		(Control Root, Control Body) tuple = OrnateFrame.Create(new Vector2(View.X - 520f - 8f, 8f), new Vector2(520f, height), CloseClassicRight, 1950);
		Control item = tuple.Root;
		Control item2 = tuple.Body;
		Label label = ClassicMapFrame.Title(title);
		label.Position = new Vector2(23f, 8f + titleYOffset);
		item.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		float num = 30f + titleYOffset;
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Position = new Vector2(6f, Mathf.Max(0f, num - 34f)),
			Size = new Vector2(Mathf.Max(0f, item2.Size.X - 12f), Mathf.Max(0f, item2.Size.Y - num + 34f - 6f))
		};
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		item2.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		return (Panel: item, Body: vBoxContainer);
	}

	private (Control Panel, VBoxContainer Body) SettingsPanelShell(float height)
	{
		(Control Root, Control Body) tuple = OrnateFrame.Create(new Vector2(View.X - 520f - 8f, 8f), new Vector2(520f, height), CloseClassicRight, 1950);
		Control item = tuple.Root;
		Control item2 = tuple.Body;
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Position = new Vector2(6f, 4f),
			Size = new Vector2(Mathf.Max(0f, item2.Size.X - 12f), Mathf.Max(0f, item2.Size.Y - 8f))
		};
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		item2.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(BarRow("設定 · 存檔", BarPanelGold, 15), forceReadableName: false, InternalMode.Disabled);
		return (Panel: item, Body: vBoxContainer);
	}

	private static Label BarRow(string text, Color color, int size, bool wrap = false, float width = 253f)
	{
		Label label = new Label
		{
			Text = text
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		if (wrap)
		{
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(width, 0f);
		}
		return label;
	}

	private static Control BarGap(float height)
	{
		return new Control
		{
			CustomMinimumSize = new Vector2(0f, height)
		};
	}

	private static void SetBarStatus(Label status, string text, bool good)
	{
		status.Text = text;
		status.AddThemeColorOverride("font_color", good ? BarPanelGood : Color.FromHtml("#e2938f".AsSpan()));
	}

	private static void BarAudioRow(VBoxContainer body, string label, Func<bool> getOn, Func<float> getVolume, Action<bool> setOn, Action<float> setVolume)
	{
		CheckBox checkBox = new CheckBox
		{
			Text = label,
			ButtonPressed = getOn()
		};
		checkBox.AddThemeFontSizeOverride("font_size", 13);
		checkBox.Toggled += delegate(bool on)
		{
			setOn(on);
		};
		body.AddChild(checkBox, forceReadableName: false, InternalMode.Disabled);
		Texture2D texture = GD.Load<Texture2D>("res://assets/ui/windows/1976c.png");
		Texture2D texture2 = GD.Load<Texture2D>("res://assets/ui/windows/1977.png");
		CenterContainer centerContainer = new CenterContainer
		{
			CustomMinimumSize = new Vector2(227f, 27f)
		};
		Control control = new Control
		{
			CustomMinimumSize = new Vector2(125f, 27f)
		};
		centerContainer.AddChild(control, forceReadableName: false, InternalMode.Disabled);
		control.AddChild(new TextureRect
		{
			Texture = texture,
			Position = Vector2.Zero,
			Size = new Vector2(125f, 26f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		HSlider hSlider = new HSlider
		{
			Position = Vector2.Zero,
			Size = new Vector2(125f, 27f),
			MinValue = 0.0,
			MaxValue = 1.0,
			Step = 0.05,
			Value = getVolume(),
			TextureFilter = TextureFilterEnum.Nearest
		};
		StyleBoxEmpty stylebox = new StyleBoxEmpty();
		hSlider.AddThemeStyleboxOverride("slider", stylebox);
		hSlider.AddThemeStyleboxOverride("grabber_area", stylebox);
		hSlider.AddThemeStyleboxOverride("grabber_area_highlight", stylebox);
		hSlider.AddThemeIconOverride("grabber", texture2);
		hSlider.AddThemeIconOverride("grabber_highlight", texture2);
		hSlider.AddThemeIconOverride("grabber_disabled", texture2);
		hSlider.ValueChanged += delegate(double v)
		{
			setVolume((float)v);
		};
		control.AddChild(hSlider, forceReadableName: false, InternalMode.Disabled);
		body.AddChild(centerContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private CastleWarDefinition? CastleWarForNpc(L1jNpcSpawn npc)
	{
		return CastleWarRules.Registrar(_mapKey, npc.NpcId, npc.CellX, npc.CellY);
	}

	private CastleWarDefinition? ActiveCastleWarOnThisMap()
	{
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		CastleWarDefinition castleWarDefinition = ((active == null) ? null : CastleWarRules.Find(active.CastleId));
		if ((object)castleWarDefinition == null || !string.Equals(active.AttackerIdentity, CastleWarFactionIdentity, StringComparison.Ordinal) || !string.Equals(castleWarDefinition.MapKey, _mapKey, StringComparison.Ordinal))
		{
			return null;
		}
		return castleWarDefinition;
	}

	private bool IsActiveCastleSpawn(L1jNpcSpawn spawn, out CastleWarDefinition? castle)
	{
		castle = ActiveCastleWarOnThisMap();
		if ((object)castle != null)
		{
			return castle.Contains(_mapKey, spawn.CellX, spawn.CellY);
		}
		return false;
	}

	private void ConfigureCastleWarActor(Combatant actor, L1jNpcSpawn spawn, CastleWarDefinition castle)
	{
		CastleWarObjectKind castleWarObjectKind = ((!CastleWarRules.IsTower(spawn)) ? CastleWarObjectKind.Defender : CastleWarRules.TowerKind(castle, spawn));
		string text = CastleWarRules.NpcKey(castleWarObjectKind, spawn);
		actor.CastleWarId = castle.Id;
		actor.CastleWarObjectKind = castleWarObjectKind;
		actor.CastleWarObjectKey = text;
		actor.NeutralWorldNpc = false;
		actor.Passive = false;
		if ((uint)(castleWarObjectKind - 3) <= 1u)
		{
			actor.Hp = CastleWarStore.Book.RestoreHealth(text, actor.MaxHp);
			actor.CastleWarInvulnerable = castleWarObjectKind == CastleWarObjectKind.MainTower && !CastleWarStore.Book.MainTowerAttackable(castle.Id);
		}
	}

	private void CastleWarStep(double delta)
	{
		string text = CastleWarStore.Tick(delta);
		if (text != null)
		{
			SlabLog("[color=#e2938f]" + text + "[/color]");
			EndCastleWarRuntime();
			return;
		}
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		if (active == null)
		{
			_castleWarAnnounced = false;
			return;
		}
		CastleWarDefinition castleWarDefinition = CastleWarRules.Find(active.CastleId);
		if ((object)castleWarDefinition == null || !string.Equals(active.AttackerIdentity, CastleWarFactionIdentity, StringComparison.Ordinal))
		{
			return;
		}
		if (!string.Equals(castleWarDefinition.MapKey, _mapKey, StringComparison.Ordinal))
		{
			FailCastleWar("離開攻城地圖，攻城失敗。");
			return;
		}
		if (!_castleWarAnnounced)
		{
			_castleWarAnnounced = true;
			SlabLog($"[color=#e6c76a]{castleWarDefinition.Name}攻城進行中\u3000剩餘 {CastleWarBook.FormatDuration(active.RemainingSeconds)}[/color]");
		}
		_castleWarSnapshotCountdown -= delta;
		if (_castleWarSnapshotCountdown <= 0.0)
		{
			_castleWarSnapshotCountdown = 1.0;
			foreach (Combatant combatant in _engine.Combatants)
			{
				if (combatant.CastleWarId == castleWarDefinition.Id && combatant.CastleWarObjectKey.Length > 0 && combatant.IsAlive)
				{
					CastleWarStore.Book.SetHealth(combatant.CastleWarObjectKey, combatant.Hp);
				}
				if (combatant.CastleWarId == castleWarDefinition.Id && combatant.CastleWarObjectKind == CastleWarObjectKind.MainTower)
				{
					combatant.CastleWarInvulnerable = !CastleWarStore.Book.MainTowerAttackable(castleWarDefinition.Id);
				}
			}
		}
		BuildCastleWarCrownIfReady(castleWarDefinition);
	}

	private bool HandleCastleWarDeath(Combatant? actor)
	{
		if (actor == null || actor.CastleWarId <= 0 || actor.CastleWarObjectKey.Length == 0)
		{
			return false;
		}
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		if (active == null || active.CastleId != actor.CastleWarId)
		{
			return false;
		}
		CastleWarStore.Book.Destroy(actor.CastleWarObjectKey);
		if (_castleWarDoorVisuals.TryGetValue(actor, out Sprite2D value))
		{
			value.Visible = false;
		}
		CastleWarStore.Save();
		if (actor.CastleWarObjectKind == CastleWarObjectKind.SubTower)
		{
			int num = CastleWarStore.Book.DestroyedAdenSubTowers();
			SlabLog($"[color=#e6c76a]亞丁副塔已摧毀 {num}/4[/color]");
			if (num >= 3)
			{
				foreach (Combatant combatant in _engine.Combatants)
				{
					if (combatant.CastleWarObjectKind == CastleWarObjectKind.MainTower)
					{
						combatant.CastleWarInvulnerable = false;
					}
				}
				SlabLog("[color=#8fdd8f]亞丁守護者之塔的保護已解除。[/color]");
			}
		}
		if (actor.CastleWarObjectKind == CastleWarObjectKind.MainTower)
		{
			CastleWarDefinition castleWarDefinition = CastleWarRules.Find(actor.CastleWarId);
			if ((object)castleWarDefinition != null)
			{
				SlabLog("[color=#e6c76a]守護者之塔已倒下，取得王冠即可占領。[/color]");
				BuildCastleWarCrownIfReady(castleWarDefinition);
			}
		}
		return true;
	}

	private void BuildCastleWarCrownIfReady(CastleWarDefinition castle)
	{
		if (_castleWarCrown != null)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		if (active != null && active.CastleId == castle.Id && active.DestroyedObjects.Any((string key) => key.StartsWith("MainTower:", StringComparison.Ordinal)))
		{
			(double, double) tuple = topology.DisplayPixelCenter(castle.CrownCellX, castle.CrownCellY);
			double x = tuple.Item1;
			double y = tuple.Item2;
			Node2D node2D = new Node2D
			{
				Position = new Vector2((float)x, (float)y),
				ZIndex = Depth.Of((float)y)
			};
			L1jNpcSpriteRenderer.TryAddSprite(node2D, GameDataProvider.Shared, 1482, 0, out var _);
			Button button = new Button
			{
				Text = "王冠",
				Position = new Vector2(-42f, -72f),
				Size = new Vector2(84f, 72f),
				Flat = true,
				MouseFilter = MouseFilterEnum.Stop
			};
			button.AddThemeColorOverride("font_color", Color.FromHtml("#f0cf73".AsSpan()));
			button.Pressed += delegate
			{
				TryCaptureCastle(castle, new WorldPoint(x, y));
			};
			node2D.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			_arena.AddChild(node2D, forceReadableName: false, InternalMode.Disabled);
			_castleWarCrown = node2D;
		}
	}

	private void TryCaptureCastle(CastleWarDefinition castle, WorldPoint crownPosition)
	{
		double num = 96.0;
		double num2 = _engine.Player.Pos.X - crownPosition.X;
		double num3 = _engine.Player.Pos.Y - crownPosition.Y;
		if (num2 * num2 + num3 * num3 > num * num)
		{
			SlabLog("[color=#e2938f]必須走到王冠旁才能占領。[/color]");
			return;
		}
		if (!CastleWarStore.Book.TryCapture(castle.Id, CastleWarFactionIdentity, out string message))
		{
			SlabLog("[color=#e2938f]" + message + "[/color]");
			return;
		}
		CastleWarStore.Save();
		SlabLog($"[color=#8fdd8f]{message}\u3000稅率固定 {10}%。[/color]");
		EndCastleWarRuntime();
	}

	private void StartCastleWar(CastleWarDefinition castle, Label status)
	{
		if (!CastleWarStore.Book.TryStart(castle.Id, _engine.Player.Level, CastleWarFactionIdentity, CastleWarStore.FactionDisplayName(_session), out string message))
		{
			status.Text = message;
			return;
		}
		CastleWarStore.Save();
		CloseL1jWorldNpcPanel();
		_session.PendingHuntSpawn = (_engine.Player.Pos.X, _engine.Player.Pos.Y);
		SaveManager.Save(_session);
		_pendingScreenTransition = ChangeHuntMap;
	}

	private void WithdrawCastleTreasury(CastleWarDefinition castle, Label status)
	{
		long num = CastleWarStore.Book.Withdraw(castle.Id, CastleWarFactionIdentity);
		if (num <= 0)
		{
			status.Text = "目前沒有可領取的城堡收入。";
			return;
		}
		CombatWallet.Add(_engine.Player, num);
		CastleWarStore.Save();
		SaveManager.Save(_session);
		status.Text = $"已領取 {num:N0} 金幣。";
		_bagRefresh?.Invoke();
	}

	private void AddCastleWarDialogOptions(CastleWarDefinition castle, ClassicNpcDialogHandle dialog)
	{
		CastleWarCastleSave castleWarCastleSave = CastleWarStore.Book.State(castle.Id);
		string owner = CastleWarStore.Book.OwnerName(castle.Id);
		if (CastleWarStore.Book.IsOwner(castle.Id, CastleWarFactionIdentity))
		{
			dialog.AddOption($"城堡管理（公款 {castleWarCastleSave.Treasury:N0}）", delegate
			{
				WithdrawCastleTreasury(castle, dialog.Status);
			});
			return;
		}
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		if (active != null)
		{
			dialog.AddOption((active.CastleId == castle.Id) ? ("攻城進行中（" + CastleWarBook.FormatDuration(active.RemainingSeconds) + "）") : "其他城堡正在交戰", delegate
			{
				dialog.Status.Text = "目前城主：" + owner;
			});
		}
		else
		{
			dialog.AddOption("開始攻城（城主：" + owner + "）", delegate
			{
				StartCastleWar(castle, dialog.Status);
			});
		}
	}

	private void FailCastleWar(string reason)
	{
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		if (active != null && string.Equals(active.AttackerIdentity, CastleWarFactionIdentity, StringComparison.Ordinal) && CastleWarStore.Book.Fail(reason, out string message))
		{
			CastleWarStore.Save();
			SlabLog("[color=#e2938f]" + message + "[/color]");
			EndCastleWarRuntime();
		}
	}

	private void EndCastleWarRuntime()
	{
		_castleWarCrown?.QueueFree();
		_castleWarCrown = null;
		Combatant[] array = _engine.Combatants.ToArray();
		foreach (Combatant combatant in array)
		{
			if (combatant.CastleWarId > 0)
			{
				_engine.Engine.Remove(combatant);
			}
		}
		foreach (var castleWarDoorRepair in _castleWarDoorRepairs)
		{
			Sprite2D item = castleWarDoorRepair.Sprite;
			Texture2D item2 = castleWarDoorRepair.OpenTexture;
			item.Texture = item2;
			item.Visible = true;
		}
		_castleWarDoorVisuals.Clear();
		_castleWarDoorRepairs.Clear();
		_castleWarAnnounced = false;
	}

	private void BeginCharmTargeting()
	{
		CancelManualSkillTargeting(silent: true);
		_petTamingItemUid = "";
		_petEvolutionItemUid = "";
		_darkEntBarkUid = "";
		_reviveTargeting = false;
		_charmTargeting = true;
		Input.SetDefaultCursorShape(Input.CursorShape.Cross);
		SlabLog("[color=#7fd0ff]迷魅術：點選要捕捉的怪物（空地／右鍵／Esc 取消）[/color]");
	}

	private void CancelCharmTargeting(bool silent = false)
	{
		if (_charmTargeting)
		{
			_charmTargeting = false;
			Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
			if (!silent)
			{
				SlabLog("[color=#8b95a6]迷魅術：已取消（未選目標、未消耗卡片）[/color]");
			}
		}
	}

	private bool HandleCharmTargetClick(Vector2 world)
	{
		if (!_charmTargeting)
		{
			return false;
		}
		Combatant combatant = PickWorldCharacterTarget(world, (Combatant actor) => actor.IsAlive && actor.Kind == CombatantKind.Mob);
		if (combatant == null)
		{
			_charmTargeting = false;
			Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
			SlabLog("[color=#8b95a6]迷魅術：已取消（未選目標、未消耗卡片）[/color]");
			return true;
		}
		if (_engine.Engine.TryCastSkill(_engine.Player, "sk_charm", combatant))
		{
			_charmTargeting = false;
			Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
			RefreshHud();
		}
		return true;
	}

	private void ToggleRightAnchor(string kind, Func<Control?> build)
	{
		bool num = _classicRightKind == kind && _classicRightPanel != null;
		CloseClassicRight();
		if (num)
		{
			return;
		}
		Control control = build();
		if (control != null)
		{
			_classicRightPanel = control;
			_classicRightKind = kind;
			AddAboveBarPanel(control, keepRightEdge: true);
			if (kind == "bag")
			{
				GameAudio.Instance?.PlayUi("inventoryOpen", 0.0, 0.52f);
			}
			else if (kind == "skills")
			{
				GameAudio.Instance?.PlayUi("skillOpen", 0.0, 0.52f);
			}
		}
	}

	private void OpenClassicLeft(string kind, Control panel)
	{
		_classicLeftKind = kind;
		_classicLeftPanel = panel;
		AddAboveBarPanel(panel);
	}

	private void SyncEquipmentAndBagUi()
	{
		_bagRefresh?.Invoke();
		if (_classicLeftPanel != null && _classicLeftKind == "equipment")
		{
			OpenClassicEquipment();
		}
	}

	private void ToggleClassicEquipment()
	{
		if (_classicLeftPanel != null && _classicLeftKind == "equipment")
		{
			CloseClassicLeft();
		}
		else
		{
			OpenClassicEquipment();
		}
	}

	private void OpenClassicEquipment()
	{
		CloseClassicLeft();
		OpenClassicLeft("equipment", ClassicWebWindows.CreateCharacter(_session, new Vector2(8f, 8f), CloseClassicLeft, delegate(string slot)
		{
			_engine.Player.EquippedItems.TryGetValue(slot, out ItemStack value);
			ItemStack before = value?.Copy();
			(bool Ok, string Text) tuple = ItemActivation.Unequip(GameDataProvider.Shared, _engine.Player, slot, _potionRng);
			bool item = tuple.Ok;
			string item2 = tuple.Text;
			_classicEquipmentStatus = (item ? "✓ " : "") + item2;
			if (item)
			{
				if (before != null)
				{
					ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack itemStack2) => itemStack2.Uid == before.Uid) ?? _engine.Player.InventoryStacks.FirstOrDefault((ItemStack left) => ItemStackInventory.CanStack(left, before));
					if (itemStack != null)
					{
						QuickBar.RemapEquipmentAssignment(_session.QuickItems, before.Uid, itemStack.Uid, before.ItemKey);
					}
				}
				SaveManager.Save(_session);
				RebuildQuickBar();
				_bagRefresh?.Invoke();
			}
			return (Ok: item, Text: item2);
		}, OpenClassicEquipment, _classicEquipmentStatus, ToggleClassicAbility, ToggleClassicTitle));
	}

	private void ToggleClassicAbility()
	{
		if (_classicLeftPanel != null && _classicLeftKind == "ability")
		{
			CloseClassicLeft();
			return;
		}
		CloseClassicLeft();
		OpenClassicLeft("ability", ClassicWebWindows.CreateAbility(_session, new Vector2(8f, 8f), CloseClassicLeft, delegate(string key)
		{
			L1jLevelStatResult l1jLevelStatResult = L1jLevelStatRules.TryAllocate(GameDataProvider.Shared, _engine.Player, key);
			if (!l1jLevelStatResult.Success)
			{
				return (Ok: false, Text: LevelStatFailureText(l1jLevelStatResult.Failure));
			}
			SaveManager.Save(_session);
			RefreshHud();
			return (Ok: true, Text: $"能力值提高，剩餘 {l1jLevelStatResult.RemainingPoints} 點");
		}));
	}

	private static string LevelStatFailureText(L1jLevelStatFailure failure)
	{
		return failure switch
		{
			L1jLevelStatFailure.NoPoints => "沒有可分配的能力點數", 
			L1jLevelStatFailure.AttributeMaximum => "此能力值已達目前階段上限", 
			L1jLevelStatFailure.InvalidState => "角色配點資料不完整", 
			_ => "無法分配能力點數", 
		};
	}

	private void ToggleClassicTitle()
	{
		if (_classicLeftPanel != null && _classicLeftKind == "title")
		{
			CloseClassicLeft();
			return;
		}
		CloseClassicLeft();
		OpenClassicLeft("title", ClassicWebWindows.CreateTitleEditor(_engine.Player, new Vector2(8f, 8f), CloseClassicLeft, ApplyClassicTitle));
	}

	private (bool Ok, string Text) ApplyClassicTitle(string requestedTitle)
	{
		ClanBook book = ClanStore.Book;
		bool isClanMember = book.Exists && (object)book.Member(_session.Identity) != null;
		CharacterTitleResult characterTitleResult = CharacterTitleRules.TrySetSelf(_engine.Player, requestedTitle, isClanMember, book.IsLeader(_session.Identity));
		if (!characterTitleResult.Success)
		{
			return (Ok: false, Text: CharacterTitleRules.FailureText(characterTitleResult.Failure));
		}
		SaveManager.Save(_session);
		_playerView?.SetTitle(characterTitleResult.Title);
		SlabLog("[color=#e6c76a]角色稱號已改為「" + characterTitleResult.Title + "」。[/color]");
		return (Ok: true, Text: "稱號已設定為「" + characterTitleResult.Title + "」。");
	}

	private void ToggleClassicSkills()
	{
		ToggleRightAnchor("skills", () => ClassicWebWindows.CreateSkills(_engine.Player, RightAnchor, CloseClassicRight, CastClassicSkill, null, _session.AutoCast, delegate(string id, bool on)
		{
			SetAutoSkillEnabled(id, on);
		}));
	}

	private void CastClassicSkill(string skillId)
	{
		if (skillId == "sk_teleport")
		{
			CastTeleportSkill();
		}
		else if (SummonRules.SkillIds.Contains(skillId))
		{
			ToggleSummonPicker(skillId);
		}
		else
		{
			TryCastPlayerSkill(skillId);
		}
	}

	private void CloseClassicLeft()
	{
		_classicLeftPanel?.QueueFree();
		_classicLeftPanel = null;
		_classicLeftKind = "";
	}

	private void CloseClassicRight()
	{
		bool flag = _classicRightPanel != null;
		string classicRightKind = _classicRightKind;
		_classicRightPanel?.QueueFree();
		_classicRightPanel = null;
		_classicRightKind = "";
		_bagPanel = null;
		_bagRefresh = null;
		if (flag && classicRightKind == "bag")
		{
			GameAudio.Instance?.PlayUi("inventoryClose", 0.0, 0.52f);
		}
		else if (flag && classicRightKind == "skills")
		{
			GameAudio.Instance?.PlayUi("skillClose", 0.0, 0.52f);
		}
	}

	private void ToggleCollections()
	{
		bool num = _classicRightKind == "collections" && _classicRightPanel != null;
		CloseClassicRight();
		if (!num)
		{
			float y = Mathf.Min(470f, BarY - 24f);
			Control panel = (_classicRightPanel = CollectionBookUi.Build(_session, _atlas, HuntCollectionBooks, new Vector2((View.X - 683f) / 2f, 12f), new Vector2(683f, y), CloseClassicRight, ItemDisplayName, TownScreen.CollectionBonusText(_session), 1950));
			_classicRightKind = "collections";
			AddAboveBarPanel(panel);
		}
	}

	private void BuildCombatToggle()
	{
		_combatOnTexture = ProjectileArt.LoadPng("res://assets/ui/buttons/combat-toggle/5208c.png") ?? throw new InvalidOperationException("戰鬥圖素 5208c.png 載入失敗");
		_combatOffTexture = ProjectileArt.LoadPng("res://assets/ui/buttons/combat-toggle/5209c.png") ?? throw new InvalidOperationException("戰鬥圖素 5209c.png 載入失敗");
		_combatToggle = new TextureButton
		{
			Position = new Vector2(RnMacroX + 140f, RnPanelY + 10f),
			Size = CombatToggleSize,
			CustomMinimumSize = CombatToggleSize,
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Keep,
			TextureFilter = TextureFilterEnum.Nearest,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			ZIndex = 2050
		};
		_combatToggle.Pressed += ToggleAutomaticCombat;
		_hud.AddChild(_combatToggle, forceReadableName: false, InternalMode.Disabled);
		RefreshCombatToggle();
	}

	private void ToggleAutomaticCombat()
	{
		_engine.Player.AutomaticCombatEnabled = !_engine.Player.AutomaticCombatEnabled;
		RefreshCombatToggle();
		SlabLog(_engine.Player.AutomaticCombatEnabled ? "[color=#74e2a3]自動攻擊與自動施法：開啟[/color]" : "[color=#ef746f]自動攻擊與自動施法：關閉（可手動捕捉）[/color]");
	}

	private void RefreshCombatToggle()
	{
		bool automaticCombatEnabled = _engine?.Player?.AutomaticCombatEnabled ?? false;
		if (_combatToggle != null)
		{
			Texture2D texture2D = (automaticCombatEnabled ? _combatOnTexture : _combatOffTexture);
			_combatToggle.TextureNormal = texture2D;
			_combatToggle.TextureHover = texture2D;
			_combatToggle.TexturePressed = texture2D;
			_combatToggle.TooltipText = (automaticCombatEnabled ? "自動戰鬥：開啟（點擊關閉，方便手動捕捉寵物）" : "自動戰鬥：關閉（不自動攻擊／施法；手動捕捉仍可使用）");
		}
	}

	private void ReportCreationAcceptance()
	{
		if (OS.GetEnvironment("IDLE_LINEAGE_PREVIEW_CREATE_CLASS").Length != 0 || OS.GetEnvironment("IDLE_LINEAGE_PREVIEW_LOAD_SLOT").Length != 0)
		{
			Combatant player = _engine.Player;
			string text = "no-topology";
			string text2 = "n/a";
			MapTopology topology = _topology;
			if (topology != null && topology.TryLocalCellAtDisplayPixel(player.Pos.X, player.Pos.Y, out var localX, out var localY))
			{
				(int X, int Y) tuple = topology.ToGameCell(localX, localY);
				int item = tuple.X;
				int item2 = tuple.Y;
				text = $"game=({item},{item2}) local=({localX},{localY})";
				text2 = (topology.IsSafeCell(localX, localY) ? "safe" : "hunting/blocked");
			}
			ItemStack valueOrDefault = player.EquippedItems.GetValueOrDefault("wpn");
			string value = ((valueOrDefault == null) ? "none" : ((valueOrDefault.AttrEnchantLevel > 0) ? $"{L1jAttrEnchantRules.KindName(valueOrDefault.AttrEnchantKind)}{valueOrDefault.AttrEnchantLevel}" : "plain"));
			long value2 = player.InventoryStacks.Where((ItemStack stack) => L1jAttrEnchantRules.IsScroll(GameDataProvider.Shared, stack.ItemKey)).Sum((ItemStack stack) => stack.Quantity);
			GD.Print("[創角驗收] " + $"class={_build.ClassId} avatar={_build.Avatar} " + $"weaponAttr={value} attrScrolls={value2} " + $"map={_mapKey}({_mapName}) mapId={_mapRule.MapId} " + text + " zone=" + text2 + " " + $"pos=({player.Pos.X:0.##},{player.Pos.Y:0.##}) " + $"hp={player.Hp:0}/{player.MaxHp:0} mp={player.Mp:0}/{player.MaxMp:0} " + $"satiety={player.Satiety:0} " + "topology=" + ((_topology != null) ? "yes" : "no") + " collision=" + (_engine.Engine.IsSafeZone(player.Pos) ? "safe-zone" : "field") + " hud=" + ((_hpTxt != null && _slabLog != null) ? "built" : "MISSING") + " playerView=" + ((_playerView != null) ? "built" : "MISSING") + " bgm=" + (GameAudio.Instance?.BgmScene ?? "none") + " " + $"worldNpcs={_pendingWorldNpcs.Count + _liveWorldNpcs.Count} " + $"integratedTowns={IntegratedTowns.Count} " + $"fixedSlots={_fixedSpawnPoints.Count} " + $"fixedBossSlots={_fixedSpawnPoints.Count((MapSpawnPoint point) => point.IsBoss)} " + "lastHuntMap=" + _session.LastHuntMap);
		}
	}

	private void FitAboveBar(Control panel, bool keepRightEdge = false)
	{
		if (!GodotObject.IsInstanceValid(panel))
		{
			return;
		}
		panel.Scale = Vector2.One;
		float num = BarY - 8f;
		Vector2 size = panel.Size;
		if (size.Y <= 0f)
		{
			return;
		}
		if (panel.Position.Y + size.Y > num)
		{
			panel.Position = new Vector2(panel.Position.X, Mathf.Max(8f, num - size.Y));
		}
		float num2 = num - panel.Position.Y;
		if (!(size.Y <= num2))
		{
			float num3 = panel.Position.X + size.X;
			float num4 = num2 / size.Y;
			panel.Scale = new Vector2(num4, num4);
			if (keepRightEdge)
			{
				panel.Position = new Vector2(num3 - size.X * num4, panel.Position.Y);
			}
		}
	}

	private void AddAboveBarPanel(Control panel, bool keepRightEdge = false)
	{
		FitAboveBar(panel, keepRightEdge);
		if (panel.GetParent() == null)
		{
			_overlayUi.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
		}
		Callable.From(delegate
		{
			if (GodotObject.IsInstanceValid(panel))
			{
				FitAboveBar(panel, keepRightEdge);
			}
		}).CallDeferred();
	}

	private float VisionDistanceSquared(Combatant target)
	{
		double num = target.Pos.X - _engine.Player.Pos.X;
		double num2 = (target.Pos.Y - _engine.Player.Pos.Y) * 2.0;
		return (float)(num * num + num2 * num2);
	}

	public void Init(AtlasBridge atlas, GameSession session, Action onExit, Action? onChangeMap = null, Action? onQuitToMenu = null)
	{
		GameCursorManager.Init();
		GameCursorManager.SetDefault();
		_initialised = false;
		_atlas = atlas;
		_session = session;
		_build = session.Build;
		_mapKey = session.HuntMap;
		_onExit = onExit;
		_mapRule = L1jMapRuleCatalog.Load(GameDataProvider.Shared).RequireForMapKey(_mapKey);
		_onChangeMap = onChangeMap ?? onExit;
		_onQuitToMenu = onQuitToMenu;
		atlas.EvictOnMapChange(_mapKey);
		string text = MapLinks.DisplayName(GameDataProvider.Shared, _mapKey);
		_mapName = (string.Equals(text, _mapKey, StringComparison.Ordinal) ? "" : text);
		_topology = TryLoadTopology(_mapKey);
		if (_topology != null)
		{
			Field = TopologyField(_topology);
		}
		else if (_mapName.Length > 0)
		{
			Texture2D texture2D = Backgrounds.Area(_mapName);
			if (texture2D != null && texture2D.GetWidth() > 0 && texture2D.GetHeight() > 0)
			{
				Field = new Rect2(0f, 0f, texture2D.GetWidth(), texture2D.GetHeight());
			}
		}
		Depth.Configure(Field.Size.Y);
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		base.MouseFilter = MouseFilterEnum.Ignore;
		Vector2 size = GetViewportRect().Size;
		_viewW = Mathf.Max(800f, size.X);
		_viewH = Mathf.Max(600f, size.Y);
		ColorRect colorRect = new ColorRect
		{
			Color = Color.FromHtml("#0e1119".AsSpan()),
			MouseFilter = MouseFilterEnum.Ignore
		};
		colorRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		AddChild(colorRect, forceReadableName: false, InternalMode.Disabled);
		Control control = new Control
		{
			ClipContents = true,
			Position = Vector2.Zero,
			Size = View,
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_world = new Node2D();
		control.AddChild(_world, forceReadableName: false, InternalMode.Disabled);
		_hud = new Control
		{
			ZIndex = 2000,
			Position = Vector2.Zero,
			Size = View,
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(_hud, forceReadableName: false, InternalMode.Disabled);
		_overlayUi = new Control
		{
			Position = Vector2.Zero,
			Size = View,
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(_overlayUi, forceReadableName: false, InternalMode.Disabled);
		_underwaterEnvironment = new ColorRect
		{
			Position = Vector2.Zero,
			Size = View,
			ZIndex = 1800,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = _mapRule.Underwater,
			Material = new ShaderMaterial
			{
				Shader = GD.Load<Shader>("res://assets/ui/underwater_environment.gdshader")
			}
		};
		AddChild(_underwaterEnvironment, forceReadableName: false, InternalMode.Disabled);
		_backBuffer = new BackBufferCopy
		{
			CopyMode = BackBufferCopy.CopyModeEnum.Viewport,
			ZIndex = 1899,
			Visible = false
		};
		AddChild(_backBuffer, forceReadableName: false, InternalMode.Disabled);
		_nightMask = new ColorRect
		{
			Position = Vector2.Zero,
			Size = View,
			ZIndex = 1900,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
			Material = new ShaderMaterial
			{
				Shader = GD.Load<Shader>("res://assets/ui/night_vision.gdshader")
			}
		};
		((ShaderMaterial)_nightMask.Material).SetShaderParameter("screen_px", View);
		AddChild(_nightMask, forceReadableName: false, InternalMode.Disabled);
		_shadowCaster = new NightShadowCaster();
		_shadowCaster.Configure(View);
		_shadowCaster.Scale = new Vector2(0.25f, 0.25f);
		_shadowViewport = new SubViewport
		{
			Size = new Vector2I((int)(View.X * 0.25f), (int)(View.Y * 0.25f)),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
		};
		_shadowViewport.AddChild(_shadowCaster, forceReadableName: false, InternalMode.Disabled);
		AddChild(_shadowViewport, forceReadableName: false, InternalMode.Disabled);
		((ShaderMaterial)_nightMask.Material).SetShaderParameter("visibility_tex", _shadowViewport.GetTexture());
		Texture2D texture2D2 = (HasTopology ? null : ((_mapName.Length > 0) ? Backgrounds.Area(_mapName) : null));
		if (HasTopology)
		{
			BuildPageLayer();
		}
		else if (texture2D2 != null)
		{
			Sprite2D node = new Sprite2D
			{
				Texture = texture2D2,
				Centered = false,
				Position = Field.Position,
				Scale = new Vector2(Field.Size.X / (float)texture2D2.GetWidth(), Field.Size.Y / (float)texture2D2.GetHeight()),
				Modulate = new Color(0.6f, 0.63f, 0.71f)
			};
			_world.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		}
		else
		{
			ColorRect node2 = new ColorRect
			{
				Color = Color.FromHtml("#14181f".AsSpan()),
				Position = Field.Position,
				Size = Field.Size,
				MouseFilter = MouseFilterEnum.Ignore
			};
			_world.AddChild(node2, forceReadableName: false, InternalMode.Disabled);
			BuildGrid();
		}
		if (!HasTopology)
		{
			BuildEdges();
		}
		_groundDropLayer = new Node2D();
		_world.AddChild(_groundDropLayer, forceReadableName: false, InternalMode.Disabled);
		_bagDropTarget = new InventoryWorldDropTarget
		{
			Position = Field.Position,
			Size = Field.Size,
			MouseFilter = MouseFilterEnum.Ignore,
			OnDropItem = OnBagItemDrop
		};
		_world.AddChild(_bagDropTarget, forceReadableName: false, InternalMode.Disabled);
		_arena = new Node2D();
		_world.AddChild(_arena, forceReadableName: false, InternalMode.Disabled);
		_spellFx = new SpellFx(_arena);
		_ui = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		_world.AddChild(_ui, forceReadableName: false, InternalMode.Disabled);
		_engine = new EngineAdapter();
		_engine.Engine.ConfigureMapRuntime(_mapRule.DropRate, _mapRule.Underwater, RegenerationRules.MapHealthDrainPerCycle(_mapRule.MapId));
		_engine.BuildWithPlayer(session.Player, Field);
		_mapEntryTimeSeconds = _engine.Engine.CurrentTimeSeconds;
		SyncPvpFlag();
		bool flag = false;
		(double, double)? pendingHuntSpawn = session.PendingHuntSpawn;
		if (pendingHuntSpawn.HasValue)
		{
			(double, double) valueOrDefault = pendingHuntSpawn.GetValueOrDefault();
			double item = valueOrDefault.Item1;
			double item2 = valueOrDefault.Item2;
			session.PendingHuntSpawn = null;
			_engine.Player.Pos = new WorldPoint(Mathf.Clamp((float)item, Field.Position.X, Field.End.X), Mathf.Clamp((float)item2, Field.Position.Y, Field.End.Y));
			_engine.StopPlayer();
			flag = true;
		}
		MapObstacles.Layout layout = (HasTopology ? null : MapObstacles.For(_mapKey, _mapName));
		if (layout != null)
		{
			_grid = MapObstacles.Build(layout, Field);
			_engine.Engine.SetCollisionGrid(_grid);
			BuildObstacleProps(layout);
		}
		if (_topology != null)
		{
			_engine.Engine.SetExplorationTopology(_topology);
			LoadDoors(_topology);
			LoadL1jDungeonRandom(_topology);
			LoadL1jTraps(_topology);
			if (!flag)
			{
				PrepareIntegratedTownEntry();
				PrepareMapPortalEntry();
			}
			if (!_topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out int curCellX, out int curCellY) || !_topology.IsWalkableCell(curCellX, curCellY))
			{
				var (safeX, safeY) = _topology.FindDefaultSpawnLocalCell();
				var (safePx, safePy) = _topology.DisplayPixelCenter(safeX, safeY);
				_engine.Player.Pos = new WorldPoint(safePx, safePy);
				_engine.StopPlayer();
			}
		}
		BuildGates();
		BuildHarborFerryShip();
		if (_topology != null)
		{
			BuildL1jWorldNpcs(_topology);
		}
		BuildFlameShadowConsul();
		DeployPartyAllies();
		DeployOfflineCompanions();
		_session.SuppressPetDeploymentOnce = false;
		if (_session.Pets.Pets.Count > 0)
		{
			_session.Pets.DeployAll(GameDataProvider.Shared, _engine.Engine, _engine.Player);
		}
		SyncActors(0f);
		_camOffset = CamTarget();
		_world.Position = _camOffset;
		StreamPages();
		BuildHud(onExit);
		FlushPendingRestartMessage();
		SyncBattleBgm(force: true);
		QueueMapAtlasWarmup();
		ReportCreationAcceptance();
		InitMultiplayer();
		InitGuardianSoul();
		_initialised = true;
	}

	private void DeployPartyAllies()
	{
		if (_session.Party.Members.Count != 0)
		{
			IReadOnlyList<Combatant> actors = _session.Party.DeployAll(GameDataProvider.Shared, _engine.Engine, _engine.Player);
			CollectionRules.AttachParty(GameDataProvider.Shared, actors, _session.Collections);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (_chatInput != null && _chatInput.HasFocus())
		{
			if (@event is InputEventKey { Pressed: true, Echo: false } keyEvt && keyEvt.Keycode == Key.Escape)
			{
				_chatInput.ReleaseFocus();
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (!(@event is InputEventKey { Echo: false } inputEventKey))
		{
			return;
		}
		if (HandlePanelHotkey(inputEventKey))
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		Key key = ((inputEventKey.PhysicalKeycode != Key.None) ? inputEventKey.PhysicalKeycode : inputEventKey.Keycode);
		if (SetWasdKeyState(key, inputEventKey.Pressed))
		{
			ApplyWasdInput();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: not false, Echo: false } inputEventKey && inputEventKey.Keycode == Key.Escape)
		{
			if (_chatInput != null && _chatInput.HasFocus())
			{
				_chatInput.ReleaseFocus();
				GetViewport().SetInputAsHandled();
				return;
			}
			if (_paused || !TryCloseTopWindow())
			{
				TogglePause();
			}
			GetViewport().SetInputAsHandled();
		}
		else if (@event is InputEventKey { Pressed: not false, Echo: false } inputEventKey2 && inputEventKey2.Keycode == Key.G)
		{
			ToggleGridDebug();
			GetViewport().SetInputAsHandled();
		}
		else if (@event is InputEventKey { Pressed: not false, Echo: false } inputEventKey3 && (inputEventKey3.Keycode == Key.Enter || inputEventKey3.Keycode == Key.KpEnter))
		{
			if (_chatInput != null && !_chatInput.HasFocus())
			{
				_chatInput.GrabFocus();
			}
			GetViewport().SetInputAsHandled();
		}
		else if (!_paused && @event is InputEventKey { Pressed: not false, Echo: false } inputEventKey4 && inputEventKey4.Keycode >= Key.F5 && inputEventKey4.Keycode <= Key.F12)
		{
			int num = (int)(inputEventKey4.Keycode - 4194336);
			if (num < 8)
			{
				_quickSlotAction[num]?.Invoke();
			}
			GetViewport().SetInputAsHandled();
		}
		else if (!_paused && @event is InputEventKey { Pressed: not false, Echo: false } inputEventKeyNum && (_chatInput == null || !_chatInput.HasFocus()) && inputEventKeyNum.Keycode >= Key.Key1 && inputEventKeyNum.Keycode <= Key.Key8)
		{
			int num = (int)(inputEventKeyNum.Keycode - Key.Key1);
			if (num >= 0 && num < 8 && _session.FloatingQuickBarVisible)
			{
				_floatingQuickSlotAction[num]?.Invoke();
			}
			GetViewport().SetInputAsHandled();
		}
		else
		{
			if (_paused)
			{
				return;
			}
			if (CharmTargeting && @event is InputEventMouseButton { Pressed: not false } inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Right)
			{
				CancelCharmTargeting();
				GetViewport().SetInputAsHandled();
			}
			else if (ManualSkillTargeting && @event is InputEventMouseButton { Pressed: not false } inputEventMouseButton2 && inputEventMouseButton2.ButtonIndex == MouseButton.Right)
			{
				CancelManualSkillTargeting();
				GetViewport().SetInputAsHandled();
			}
			else if (PetItemTargeting && @event is InputEventMouseButton { Pressed: not false } inputEventMouseButton3 && inputEventMouseButton3.ButtonIndex == MouseButton.Right)
			{
				CancelPetItemTargeting();
				GetViewport().SetInputAsHandled();
			}
			else if (DarkEntBarkTargeting && @event is InputEventMouseButton { Pressed: not false } inputEventMouseButton4 && inputEventMouseButton4.ButtonIndex == MouseButton.Right)
			{
				CancelDarkEntBarkTargeting();
				GetViewport().SetInputAsHandled();
			}
			else if (_reviveTargeting && @event is InputEventMouseButton { Pressed: not false } inputEventMouseButton5 && inputEventMouseButton5.ButtonIndex == MouseButton.Right)
			{
				_reviveTargeting = false;
				SlabLog("[color=#8b95a6]復活卷軸：已取消（未消耗）[/color]");
				GetViewport().SetInputAsHandled();
			}
			else if (@event is InputEventMouseButton { Pressed: not false } inputEventMouseButton6 && inputEventMouseButton6.ButtonIndex == MouseButton.Right && inputEventMouseButton6.Position.Y < BarY)
			{
				if (inputEventMouseButton6.ShiftPressed || inputEventMouseButton6.CtrlPressed || inputEventMouseButton6.AltPressed)
				{
					Vector2 clickWorld = _arena.GetLocalMousePosition();
					int cellX = 0, cellY = 0;
					if (_topology != null && _topology.TryLocalCellAtDisplayPixel(clickWorld.X, clickWorld.Y, out int lx, out int ly))
					{
						cellX = lx;
						cellY = ly;
					}
					else
					{
						cellX = (int)(clickWorld.X / 48.0);
						cellY = (int)(clickWorld.Y / 48.0);
					}
					Combatant? mobToCommand = _engine.Engine.GetManualTarget(_engine.Player);
					if (mobToCommand == null || mobToCommand.Kind != CombatantKind.Mob || !mobToCommand.IsAlive)
					{
						mobToCommand = _engine.Combatants
							.Where(c => c.Kind == CombatantKind.Mob && c.IsAlive)
							.OrderBy(c => c.Pos.DistanceTo(new WorldPoint(clickWorld.X, clickWorld.Y)))
							.FirstOrDefault();
					}

					if (mobToCommand != null)
					{
						mobToCommand.MarchTarget = new WorldPoint(clickWorld.X, clickWorld.Y);
						_engine.Engine.ResetIdleWander(mobToCommand);
						SlabLog($"[color=#00fa9a][GM操控] 已指揮怪物【{mobToCommand.Disp}】往座標 ({cellX}, {cellY}) 前進行軍！[/color]");
						GetViewport().SetInputAsHandled();
						return;
					}
				}
				if (HandlePetCommandClick(_arena.GetLocalMousePosition()))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
			}
			else if (@event is InputEventMouseButton { Pressed: not false } inputEventMouseButton7 && inputEventMouseButton7.ButtonIndex == MouseButton.Left && !(inputEventMouseButton7.Position.Y >= BarY))
			{
				Vector2 localMousePosition = _arena.GetLocalMousePosition();
				if (HandleCharmTargetClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (HandleManualSkillTargetClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (HandlePetItemTargetClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (HandleDarkEntBarkTargetClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (HandleReviveTargetClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (HandleFlameShadowConsulClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (HandleWorldNpcClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (HandleMonsterClick(localMousePosition))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_chatInput != null && _chatInput.HasFocus())
				{
					_chatInput.ReleaseFocus();
				}
				StopAutoFollow(userCancelled: true);
				_engine.Engine.SetManualTarget(_engine.Player, null);
				Vector2 clickTarget = localMousePosition;
				foreach (var (gate, gatePos) in _gates)
				{
					if (localMousePosition.DistanceSquaredTo(gatePos) <= 50f * 50f)
					{
						clickTarget = gatePos;
						break;
					}
				}
				_engine.SetPlayerPathTarget(clickTarget);
				_playerView?.InterruptOneShot();
			}
		}
	}

	private bool SetWasdKeyState(Key key, bool pressed)
	{
		switch (key)
		{
		case Key.W:
			_wasdW = pressed;
			return true;
		case Key.A:
			_wasdA = pressed;
			return true;
		case Key.S:
			_wasdS = pressed;
			return true;
		case Key.D:
			_wasdD = pressed;
			return true;
		default:
			return false;
		}
	}

	private Vector2 CurrentWasdDirection()
	{
		return new Vector2((_wasdD ? 1f : 0f) - (_wasdA ? 1f : 0f), (_wasdS ? 1f : 0f) - (_wasdW ? 1f : 0f));
	}

	private void ApplyWasdInput()
	{
		if (_dead || _paused)
		{
			if (_wasdMoving)
			{
				_engine.StopPlayer();
			}
			_wasdMoving = false;
			_wasdDirection = Vector2.Zero;
			return;
		}
		Vector2 vector = CurrentWasdDirection();
		if (vector == Vector2.Zero)
		{
			if (_wasdMoving)
			{
				_engine.ReleasePlayerMoveDirection();
			}
			_wasdMoving = false;
			_wasdDirection = Vector2.Zero;
			return;
		}
		vector = vector.Normalized();
		if (!_wasdMoving || !_wasdDirection.IsEqualApprox(vector))
		{
			StopAutoFollow(userCancelled: true);
			_engine.Engine.SetManualTarget(_engine.Player, null);
			_wasdMoving = true;
			_wasdDirection = vector;
			_engine.SetPlayerMoveDirection(vector);
			_playerView?.InterruptOneShot();
		}
	}

	private void WasdStep()
	{
		bool flag = Input.IsPhysicalKeyPressed(Key.W);
		bool flag2 = Input.IsPhysicalKeyPressed(Key.A);
		bool flag3 = Input.IsPhysicalKeyPressed(Key.S);
		bool flag4 = Input.IsPhysicalKeyPressed(Key.D);
		if (flag == _wasdW && flag2 == _wasdA && flag3 == _wasdS && flag4 == _wasdD)
		{
			if (_dead && _wasdMoving)
			{
				ApplyWasdInput();
			}
		}
		else
		{
			_wasdW = flag;
			_wasdA = flag2;
			_wasdS = flag3;
			_wasdD = flag4;
			ApplyWasdInput();
		}
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if ((long)what == 1006)
		{
			try
			{
				SyncAllOfflineCompanions();
				SaveManager.Save(_session);
			}
			catch (Exception ex)
			{
				GD.PushWarning($"[ArpgEngineScreen] 關閉視窗存檔失敗: {ex.Message}");
			}
		}
	}

	public override void _Process(double delta)
	{
		if (!_initialised || _paused)
		{
			return;
		}
		float num = (float)delta;
		if (!_dead)
		{
			_elapsed += delta;
		}
		string text = ClanStore.Tick(_session);
		if (text != null)
		{
			SlabLog("[color=#e6c76a]" + text + "[/color]");
		}
		CastleWarStep(delta);
		WasdStep();
		PerfProbeStep(delta);
		MultiplayerStep(delta);
		if (_playerChatBubble != null && _playerChatBubble.Visible)
		{
			if (_playerChatBubbleTimer > 0f)
			{
				_playerChatBubbleTimer -= num;
				_playerChatBubble.Position = PlayerPos() + new Vector2(-_playerChatBubble.Size.X * 0.5f, -85f);
				if (_playerChatBubbleTimer <= 0f)
				{
					Tween t = _playerChatBubble.CreateTween();
					t.TweenProperty(_playerChatBubble, "modulate:a", 0f, 1.0);
					t.TweenCallback(Callable.From(() => _playerChatBubble.Visible = false));
				}
			}
		}
		bool flag = false;
		bool flag2 = false;
		foreach (CombatEvent item in _engine.Advance(delta))
		{
			switch (item.Kind)
			{
			case CombatEventKind.Spawn:
			{
				Combatant target = item.Target;
				if (target != null && target.Kind == CombatantKind.Mob)
				{
					StartL1jNpcChat(item.Target, L1jNpcChatTiming.Appearance);
				}
				break;
			}
			case CombatEventKind.Attack:
			{
				NoteCombatMusic(item.Source, item.Target);
				if (item.Source != null && item.Target != null)
				{
					float dx = (float)(item.Target.Pos.X - item.Source.Pos.X);
					float dy = (float)(item.Target.Pos.Y - item.Source.Pos.Y);
					if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f)
					{
						item.Source.Facing8 = ArpgActor.Vec2Dir(dx, dy);
						if (_views.TryGetValue(item.Source, out ArpgActor srcActor))
						{
							srcActor.FaceDirection(item.Source.Facing8);
						}
					}
				}
				PlayL1jWorldNpcAttack(item.Source);
				if (item.Source != null && _views.TryGetValue(item.Source, out ArpgActor value8))
				{
					value8.RevealCombatName();
					bool rangedAttacker = item.Source.AttackRange > 48.0;
					bool rangedShot = item.Target != null && CombatRangeRules.DiamondDistance(item.Source.Pos, item.Target.Pos) > 48.0;
					value8.PlayAttack(_rng, rangedAttacker, rangedShot, _engine.Engine.AttackCycleSeconds(item.Source), _engine.Engine.AttackSpeedRatio(item.Source));
					_engine.Engine.LockAction(item.Source, value8.LastOneShotSeconds);
					if (item.Source == _engine.Player)
					{
						SendLocalAction("attack");
					}
					else if (NetworkManager.Instance.IsHost && item.Source != null && item.Source.Kind == CombatantKind.Mob)
					{
						SendLocalAction("mob_attack", item.Source.Key, item.Target?.Pos.X ?? 0, item.Target?.Pos.Y ?? 0, "", item.Target?.Key ?? "");
					}
				}
				if (item.Target != null && _views.TryGetValue(item.Target, out ArpgActor value9))
				{
					value9.RevealCombatName();
					if (item.Source != null)
					{
						float dx2 = (float)(item.Source.Pos.X - item.Target.Pos.X);
						float dy2 = (float)(item.Source.Pos.Y - item.Target.Pos.Y);
						if (Mathf.Abs(dx2) > 0.01f || Mathf.Abs(dy2) > 0.01f)
						{
							item.Target.Facing8 = ArpgActor.Vec2Dir(dx2, dy2);
							value9.FaceDirection(item.Target.Facing8);
						}
					}
				}
				if (item.Source != null)
				{
					PlayAttackSfx(item.Source);
				}
				break;
			}
			case CombatEventKind.Damage:
			{
				NoteCombatMusic(item.Source, item.Target);
				if (item.Target != null && item.Source != null && !item.Target.Dead)
				{
					float dx = (float)(item.Source.Pos.X - item.Target.Pos.X);
					float dy = (float)(item.Source.Pos.Y - item.Target.Pos.Y);
					if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f)
					{
						item.Target.Facing8 = ArpgActor.Vec2Dir(dx, dy);
						if (_views.TryGetValue(item.Target, out ArpgActor targetActor))
						{
							targetActor.FaceDirection(item.Target.Facing8);
						}
					}
				}
				bool flag4 = DamageReactionRules.PlaysHurtAnimation(item.DmgType);
				if (flag4)
				{
					PlayL1jWorldNpcDamage(item.Target);
				}
				if (item.Target != null)
				{
					if (item.Amount > 0.0)
					{
						Vector2 atDmg = (item.Target == _engine.Player) ? PlayerPos() : ToVec(item.Target.Pos);
						FloatDamage(atDmg, (int)Math.Round(item.Amount), item.Crit);
					}
					if (item.Source != null && _views.TryGetValue(item.Source, out ArpgActor value5))
					{
						value5.RevealCombatName();
					}
					if (_views.TryGetValue(item.Target, out ArpgActor value6))
					{
						value6.RevealCombatName();
					}
					if (flag4 && !item.Target.Dead && !MobFlinchCatalog.NeverFlinches(item.Target.Avatar) && _views.TryGetValue(item.Target, out ArpgActor value7))
					{
						bool isPlayerMoving = (item.Target == _engine.Player) && (_wasdMoving || _engine.Player.MoveTarget.HasValue || _engine.Player.ManualMoveActive);
						if (!isPlayerMoving)
						{
							value7.PlayOneShot("hurt");
						}
					}
					if (item.Crit)
					{
						GameAudio.Instance?.PlayEvent("crit");
					}
					if (UsesPlayerAudio(item.Target))
					{
						GameAudio.Instance?.PlayPartyHurt(item.Target);
					}
					else if (UsesMonsterPresentation(item.Target))
					{
						GameAudio.Instance?.PlayMobHurt(item.Target.Avatar, item.Target.Disp);
					}
					NoteCombatDamageEvent(item.Source, item.Target, item.Amount);
				}
				break;
			}
			case CombatEventKind.Death:
				NoteCombatDeathEvent(item.Target);
				PlayL1jWorldNpcDeath(item.Target);
				ReleaseFixedSpawnSlot(item.Target, killed: true);
				if (HandleCastleWarDeath(item.Target))
				{
					break;
				}
				if (item.Target != null)
				{
					SettleGuardExecution(item.Target, item.Source);
				}
				if (item.Target != null && item.Target.Kind == CombatantKind.Mob && _killed.Add(item.Target))
				{
					StartL1jNpcChat(item.Target, L1jNpcChatTiming.Death);
					RecordL1jMobGroupDeath(item.Target);
					bool num2 = HostilePlayerRules.IsHostilePlayer(item.Target);
					if (num2)
					{
						SettleHostilePlayerKill(item.Target, item.Source);
					}
					if (!num2)
					{
						GameAudio.Instance?.PlayMobKill(item.Target.Avatar, item.Target.Disp);
					}
					if (item.Target.IsBoss)
					{
						var (gameX, gameY) = CurrentGameCell();
						GameAudio.Instance?.PlaySting(MapMusicCatalog.ResolveVictorySting(GameDataProvider.Shared, _mapKey, gameX, gameY));
					}

					if (!string.IsNullOrWhiteSpace(item.Target.DeathTransformMob))
					{
						double chance = item.Target.DeathTransformChance >= 0 ? item.Target.DeathTransformChance : 100.0;
						if (chance >= 100.0 || (Random.Shared.NextDouble() * 100.0 <= chance))
						{
							string fx = string.IsNullOrWhiteSpace(item.Target.DeathTransformFx) ? "流星雨" : item.Target.DeathTransformFx;
							if (!string.Equals(fx, "none", StringComparison.OrdinalIgnoreCase) && !string.Equals(fx, "無", StringComparison.OrdinalIgnoreCase))
							{
								_spellFx?.Play(fx, item.Target, item.Target, fx == "流星雨" ? "74" : null);
							}
							double delay = item.Target.DeathTransformDelay > 0 ? item.Target.DeathTransformDelay : 1.0;
							_pendingDeathTransforms.Add(new PendingDeathTransform
							{
								Remaining = delay,
								TargetMobKey = item.Target.DeathTransformMob,
								Pos = item.Target.Pos
							});
						}
					}
				}
				break;
			case CombatEventKind.Heal:
				if (item.Target != null && item.Amount > 0.0)
				{
					Vector2 atHeal = (item.Target == _engine.Player) ? PlayerPos() : ToVec(item.Target.Pos);
					Float(atHeal, $"+{(int)item.Amount}", Color.FromHtml("#4ade80"), big: false);
					if (_views.TryGetValue(item.Target, out ArpgActor healActorView))
					{
						healActorView.PlayOneShot("skill");
					}
					GameAudio.Instance?.PlayEvent("heal");
					if (item.Target == _engine.Player)
					{
						RefreshHud();
					}
					RefreshPartyHud();

					if (item.Target.IsRemote && !string.IsNullOrEmpty(item.Target.Key))
					{
						NetworkManager.Instance.SendPlayerHpSync(new PlayerHpSyncPacket
						{
							PlayerId = item.Target.Key,
							Hp = item.Target.Hp,
							MaxHp = item.Target.MaxHp,
							Mp = item.Target.Mp,
							MaxMp = item.Target.MaxMp,
							DamageTaken = -item.Amount,
							IsPoisoned = L1jPoisonAttackRules.IsPoisoned(item.Target)
						});
					}
				}
				break;
			case CombatEventKind.MpChange:
				if (item.Target != null && item.Amount > 0.0)
				{
					Vector2 atMp = (item.Target == _engine.Player) ? PlayerPos() : ToVec(item.Target.Pos);
					Float(atMp, $"+{(int)item.Amount} MP", Color.FromHtml("#60a5fa"), big: false);
					if (item.Target == _engine.Player)
					{
						SlabLog($"[color=#60a5fa]魔力恢復了 {(int)item.Amount} 點。[/color]");
						RefreshHud();
					}
					RefreshPartyHud();
					if (item.Target.IsRemote && !string.IsNullOrEmpty(item.Target.Key))
					{
						NetworkManager.Instance.SendPlayerHpSync(new PlayerHpSyncPacket
						{
							PlayerId = item.Target.Key,
							Hp = item.Target.Hp,
							MaxHp = item.Target.MaxHp,
							Mp = item.Target.Mp,
							MaxMp = item.Target.MaxMp,
							DamageTaken = 0,
							IsPoisoned = L1jPoisonAttackRules.IsPoisoned(item.Target)
						});
					}
				}
				else if (item.Target == _engine.Player)
				{
					RefreshHud();
				}
				break;
			case CombatEventKind.ExpGain:
				if (item.Target != null && item.Amount > 0.0)
				{
					Vector2 atExp = (item.Target == _engine.Player) ? PlayerPos() : ToVec(item.Target.Pos);
					FloatExp(atExp, item.Amount);
					if (item.Target == _engine.Player)
					{
						SlabLog($"[color=#4ade80]獲得 經驗值 +{(long)item.Amount:N0}[/color]");
						RefreshHud();
						try { SaveManager.Save(_session); } catch { }
					}
				}
				break;
			case CombatEventKind.GoldGain:
				if (item.Target != null && item.Amount > 0.0)
				{
					long value4 = (long)item.Amount;
					Vector2 atGold = (item.Target == _engine.Player) ? PlayerPos() : ToVec(item.Target.Pos);
					FloatGold(atGold, value4);
					if (item.Target == _engine.Player)
					{
						SlabLog($"[color=#e6c76a]獲得 金幣 ×{value4:N0}[/color]");
						_bagRefresh?.Invoke();
						RefreshHud();
						try { SaveManager.Save(_session); } catch { }
					}
				}
				break;
			case CombatEventKind.Drop:
			{
				if (item.ItemKey == null)
				{
					break;
				}
				string text3 = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, item.ItemKey, item.ItemIdentified);
				if (_session.FilteredItems.Contains(item.ItemKey) || LootFilterRules.ShouldDropToGround(_session.LootFilterItemKeys, item.ItemKey))
				{
					Vector2 dropPos = (item.X != 0.0 || item.Y != 0.0) ? new Vector2((float)item.X, (float)item.Y) : PlayerPos();
					SpawnGroundDrop(item, dropPos);
					SlabLog($"[color=#9ca3af]〔過濾清單〕{text3}" + ((item.IntArg > 1) ? $" ×{item.IntArg}" : "") + " 符合過濾設定，已直接掉落於地面[/color]");
					break;
				}
				if (TryReceiveCombatDrop(item, out var received))
				{
					if (item.ItemKey == ContentAdditions.GuardianSoulKey)
					{
						ActivateGuardianSoul();
					}
					Float(PlayerPos(), (received > 1) ? $"{text3} ×{received}" : text3, Color.FromHtml("#e6c76a".AsSpan()), big: false);
					SlabLog("[color=#e6c76a]獲得 " + text3 + ((received > 1) ? $" ×{received}" : "") + "[/color]");
					flag = true;
					flag2 = true;
					if (received < item.IntArg)
					{
						SlabLog($"[color=#e2938f]{text3} 超過持有上限，未取得 ×{item.IntArg - received}[/color]");
					}
				}
				else
				{
					SlabLog("[color=#e2938f]無法取得 " + text3 + ((item.IntArg > 1) ? $" ×{item.IntArg}" : "") + "[/color]");
				}
				break;
			}
			case CombatEventKind.ItemGain:
				if (item.ItemKey != null)
				{
					Vector2 at = ((item.Source != null) ? ToVec(item.Source.Pos) : PlayerPos());
					string text2 = ItemDisplayName(item.ItemKey);
					Float(at, (item.IntArg > 1) ? $"{text2} ×{item.IntArg}" : text2, Color.FromHtml("#e6c76a".AsSpan()), big: false);
					SlabLog("[color=#e6c76a]獲得 " + text2 + ((item.IntArg > 1) ? $" ×{item.IntArg}" : "") + "[/color]");
					flag = true;
				}
				break;
			case CombatEventKind.LevelUp:
				if (item.Target == _engine.Player)
				{
					PlayLevelUpEffect(PlayerPos());
					_playerView?.PlayOneShot("skill");
					GameAudio.Instance?.PlayEvent("levelup");
					SlabLog($"[color=#ffd76a]等級提升！Lv {_engine.Player.Level}[/color]");
					CheckHiddenValleyLevelExit();
				}
				else if (item.Target != null)
				{
					PlayLevelUpEffect(ToVec(item.Target.Pos));
					if (_views.TryGetValue(item.Target, out ArpgActor value3))
					{
						value3.PlayOneShot("skill");
					}
					SlabLog($"[color=#ffd76a]{item.Target.Disp} 等級提升！Lv {item.IntArg}[/color]");
					if (item.Target.Key.StartsWith("offline_slot_") && int.TryParse(item.Target.Key.Substring("offline_slot_".Length), out int slot))
					{
						LocalOfflinePartyManager.SyncProgressBackToSlot(slot, item.Target);
					}
				}
				break;
			case CombatEventKind.Cast:
			{
				NoteCombatMusic(item.Source, item.Target);
				if (item.Source == _engine.Player)
				{
					DetectL1jTraps(item.SkillId);
					if (string.Equals(item.SkillId, "sk_charm", StringComparison.Ordinal))
					{
						flag = true;
						flag2 = true;
					}
				}
				bool flag3 = string.Equals(item.SkillId, "sk_charm", StringComparison.Ordinal) || item.IntArg == 1;
				if (item.Source != null && _views.TryGetValue(item.Source, out ArpgActor value))
				{
					value.RevealCombatName();
					PlayCastAnim(value, item.Source, item.Target, item.SkillId, !flag3);
					if (!flag3 && !IsTripleArrowSkill(item.SkillId))
					{
						_engine.Engine.LockAction(item.Source, value.LastOneShotSeconds);
					}
					if (item.Source == _engine.Player)
					{
						SendLocalAction("cast", item.SkillId);
					}
					else if (NetworkManager.Instance.IsHost && item.Source != null && item.Source.Kind == CombatantKind.Mob)
					{
						SendLocalAction("mob_cast", item.SkillId, item.Target?.Pos.X ?? 0, item.Target?.Pos.Y ?? 0, item.Source.Key, item.Target?.Key ?? "");
					}
				}
				if (item.Target != null && _views.TryGetValue(item.Target, out ArpgActor value2))
				{
					value2.RevealCombatName();
				}
				break;
			}
			case CombatEventKind.Log:
				if (!string.IsNullOrWhiteSpace(item.Text))
				{
					SlabLog("[color=#d8d0b0]" + item.Text + "[/color]");
				}
				break;
			}
		}
		if (GroundDropStep(delta))
		{
			flag = true;
		}
		if (flag2)
		{
			SaveManager.Save(_session);
		}
		_companionAutoSaveCd -= delta;
		if (_companionAutoSaveCd <= 0.0)
		{
			_companionAutoSaveCd = 30.0;
			SyncAllOfflineCompanions();
			SaveManager.Save(_session);
		}
		if (flag)
		{
			_bagRefresh?.Invoke();
		}
		PerfProbeMark(0);
		if (_pendingScreenTransition != null)
		{
			Action? pendingScreenTransition = _pendingScreenTransition;
			_pendingScreenTransition = null;
			pendingScreenTransition();
			return;
		}
		CheckGates();
		if (!L1jDungeonRandomStep())
		{
			L1jTrapStep(delta);
		}
		PerfProbeMark(1);
		SyncActors(num);
		PerfProbeMark(16);
		L1jNpcChatStep();
		PerfProbeMark(17);
		SyncProjectiles(num);
		PerfProbeMark(18);
		_spellFx.Process(num);
		ProcessDeathTransforms(num);
		PerfProbeMark(2);
		_camOffset = _camOffset.Lerp(CamTarget(), 1f - Mathf.Exp((0f - num) * 10f));
		UpdateGuardianSoul(delta);
		_world.Position = _camOffset + _screenShakeOffset;
		PerfProbeMark(3);
		StreamPages();
		UpdateOcclusionFade(delta);
		PerfProbeMark(6);
		if (!_dead && !UltimateBattleOwnsSpawning)
		{
			ExplorationSpawnStep();
		}
		PerfProbeMark(7);
		if (!_dead)
		{
			IntegratedTownStep(delta);
		}
		PerfProbeMark(8);
		DoorStep(delta);
		PerfProbeMark(9);
		if (!_dead)
		{
			L1jWorldNpcStep(delta);
		}
		PerfProbeMark(10);
		PerfProbeMark(11);
		UltimateBattleStep(delta);
		PerfProbeMark(4);
		if (!_dead && _engine.Player.Dead)
		{
			FailCastleWar("攻城中死亡，攻城失敗。");
			_session.LastHuntMap = "";
			ApplyEvilDeathLoss();
			ShowDeath();
		}
		WeightStep(delta);
		AutoCastStep();
		AutoUseStep(delta);
		UpdateGridProbe();
		SyncBattleBgm(force: false, delta);
		SyncEnvironmentSound(delta);
		RefreshDayNight(_engine.Player);
		_hudCd -= delta;
		if (_hudCd <= 0.0)
		{
			_hudCd = 0.1;
			RefreshHud();
			RefreshMiniMap();
		}
		PerfProbeMark(5);
		AtlasWarmStep(delta);
		UpdateWorldHoverCursor();
		PerfProbeFlush(delta);
	}

	private void ProcessDeathTransforms(double dt)
	{
		for (int i = _pendingDeathTransforms.Count - 1; i >= 0; i--)
		{
			PendingDeathTransform p = _pendingDeathTransforms[i];
			p.Remaining -= dt;
			if (p.Remaining <= 0)
			{
				_pendingDeathTransforms.RemoveAt(i);
				_engine.SpawnMob(p.TargetMobKey, p.Pos);
			}
			else
			{
				_pendingDeathTransforms[i] = p;
			}
		}
	}

	private void UpdateWorldHoverCursor()
	{
		if (!_initialised || _paused)
		{
			return;
		}
		if (DragCursorFeedback.IsDragging)
		{
			return;
		}
		if (ManualSkillTargeting || CharmTargeting || PetItemTargeting || DarkEntBarkTargeting || _reviveTargeting)
		{
			GameCursorManager.SetSkillCross();
			return;
		}
		if (_itemTargetOverlay != null)
		{
			GameCursorManager.SetScroll();
			return;
		}
		Vector2 mousePos = GetViewport().GetMousePosition();
		if (mousePos.Y >= BarY)
		{
			GameCursorManager.SetDefault();
			return;
		}

		Vector2 worldPos = _arena.GetLocalMousePosition();

		if (_worldNpcPanel == null && _liveWorldNpcs.Count > 0)
		{
			bool overNpc = false;
			foreach (var live in _liveWorldNpcs)
			{
				if (!WorldNpcIsInteractive(live.Npc))
				{
					continue;
				}
				Vector2 npcPos = new Vector2((float)live.X, (float)live.Y);
				Rect2 npcRect;
				if (L1jNpcSpriteRenderer.TryMeasureVisualBounds(GameDataProvider.Shared, live.Npc.Gfx, live.Npc.Heading, out Rect2 vBounds) && vBounds.Size.X > 0 && vBounds.Size.Y > 0)
				{
					npcRect = new Rect2(npcPos + vBounds.Position, vBounds.Size);
				}
				else
				{
					npcRect = new Rect2(npcPos.X - 20f, npcPos.Y - 55f, 40f, 55f);
				}
				if (npcRect.HasPoint(worldPos))
				{
					overNpc = true;
					break;
				}
			}
			if (overNpc)
			{
				GameCursorManager.SetNpc();
				return;
			}
		}

		Combatant? mob = PickWorldCharacterTarget(worldPos, actor => actor.IsAlive && actor.Kind == CombatantKind.Mob);
		if (mob != null)
		{
			GameCursorManager.SetAttack();
			return;
		}

		GameCursorManager.SetDefault();
	}

	private void SyncActors(float dt)
	{
		_liveScratch.Clear();
		foreach (Combatant combatant2 in _engine.Combatants)
		{
			_liveScratch.Add(combatant2);
		}
		if (_notInLiveScratch == null)
		{
			_notInLiveScratch = (Combatant c) => !_liveScratch.Contains(c);
		}
		_killed.RemoveWhere(_notInLiveScratch);
		Rect2 rect = ActiveRenderRect();
		_revealEleCd -= dt;
		if (_revealEleCd <= 0f)
		{
			_revealEleCd = 0.5f;
			_revealMobElement = false;
			bool value = default(bool);
			foreach (ItemStack value5 in _engine.Player.EquippedItems.Values)
			{
				if (GameDataProvider.Shared.Item(value5.ItemKey)?["showMobEle"] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value)
				{
					_revealMobElement = true;
					break;
				}
			}
		}
		bool revealMobElement = _revealMobElement;
		foreach (Combatant combatant3 in _engine.Combatants)
		{
			if (_worldNpcCombatVisuals.ContainsKey(combatant3) || _castleWarDoorVisuals.ContainsKey(combatant3))
			{
				continue;
			}
			if (combatant3.Kind != CombatantKind.Player && !rect.HasPoint(ToVec(combatant3.Pos)))
			{
				if (_views.Remove(combatant3, out ArpgActor value2))
				{
					value2.Free();
					if (value2 == _playerView)
					{
						_playerView = null;
					}
				}
				continue;
			}
			if (combatant3.IsRemote)
			{
				if (_views.Remove(combatant3, out ArpgActor remoteGhost))
				{
					remoteGhost.Free();
				}
				continue;
			}
			if (!_views.TryGetValue(combatant3, out ArpgActor value3))
			{
				bool flag = combatant3.Dead;
				if (flag)
				{
					CombatantKind kind = combatant3.Kind;
					bool flag2 = (uint)(kind - 2) <= 1u;
					flag = !flag2;
				}
				if (flag)
				{
					continue;
				}
				value3 = CreateView(combatant3);
				_views[combatant3] = value3;
			}
			else if (!combatant3.Dead && value3.Dead)
			{
				value3.Free();
				value3 = CreateView(combatant3);
				_views[combatant3] = value3;
			}
			else
			{
				bool flag = !combatant3.Dead;
				if (flag)
				{
					CombatantKind kind = combatant3.Kind;
					bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
					flag = flag2 || (combatant3.Kind == CombatantKind.Mob && combatant3.PolymorphForm.Length > 0);
				}
				if (flag && !string.Equals(value3.VisualKey, CharacterVisualKey(combatant3), StringComparison.Ordinal))
				{
					value3.Free();
					value3 = CreateView(combatant3);
					_views[combatant3] = value3;
				}
			}
			(Vector2 Anchor, float Progress, bool Stepping) walk;
			if (NetworkManager.Instance.IsConnected && !NetworkManager.Instance.IsHost && combatant3.Kind == CombatantKind.Mob && _clientMobTargets.TryGetValue(combatant3.Key, out var mobState))
			{
				value3.IsRemote = true;
				Vector2 mobAnchor = mobState.VisualPos != Vector2.Zero ? mobState.VisualPos : ToVec(combatant3.Pos);
				bool mobMoving = mobState.Stepping || mobAnchor.DistanceTo(mobState.TargetPos) > 2.0f;
				walk = (mobAnchor, mobState.WalkProgress, mobMoving);
			}
			else
			{
				value3.IsRemote = false;
				walk = _engine.RenderWalk(combatant3);
			}
			UpdateView(value3, combatant3, walk, dt, revealMobElement && combatant3.Kind == CombatantKind.Mob);
			value3.SetOccluded(_visionOccluded && combatant3.Kind == CombatantKind.Mob && !combatant3.Dead && (VisionDistanceSquared(combatant3) > _visionCutoffSq || !_engine.Engine.HasLineOfSight(_engine.Player.Pos, combatant3.Pos)));
		}
		List<Combatant>? viewsToRemove = null;
		foreach (KeyValuePair<Combatant, ArpgActor> view in _views)
		{
			view.Deconstruct(out var key, out var value4);
			Combatant combatant = key;
			ArpgActor arpgActor = value4;
			bool flag3 = !_liveScratch.Contains(combatant);
			CombatantKind kind = combatant.Kind;
			bool flag = (uint)(kind - 2) <= 1u;
			bool flag4 = flag;
			if (flag3 && arpgActor.Dead && !flag4)
			{
				arpgActor.DeadTimer -= dt;
				float fade = (float)Mathf.Clamp(arpgActor.DeadTimer / 0.8f, 0.0, 1.0);
				arpgActor.Sync(fade, dt);
			}
			if ((arpgActor.Dead && arpgActor.DeadTimer <= 0.0) || (flag3 && (!arpgActor.Dead || flag4)))
			{
				arpgActor.Free();
				(viewsToRemove ??= new List<Combatant>()).Add(combatant);
				if (arpgActor == _playerView)
				{
					_playerView = null;
				}
			}
		}
		if (viewsToRemove != null)
		{
			foreach (Combatant c in viewsToRemove)
			{
				_views.Remove(c);
			}
		}
	}

	private Rect2 ActiveRenderRect()
	{
		return new Rect2(-_world.Position - new Vector2(192f, 192f), WorldView + new Vector2(384f, 384f));
	}

	private ArpgActor CreateView(Combatant c)
	{
		ArpgActor arpgActor;
		if (c.Kind == CombatantKind.Player && !c.IsRemote)
		{
			CharacterMorphAnimation.Spec spec = ResolveCharacterVisual(c, _build.Avatar, _build.WeaponPrefix);
			arpgActor = (_playerView = ArpgActor.Create(_atlas, _arena, _ui, spec.Group, spec.Atlas, spec.WeaponPrefix, isPlayer: true, 1f, spec.ThreeDirection));
		}
		else if (c.Kind == CombatantKind.Player && c.IsRemote)
		{
			string avatar = string.IsNullOrEmpty(c.Avatar) ? "男騎士" : c.Avatar;
			string weaponPrefix = string.IsNullOrEmpty(c.WeaponPrefix) ? "sword1" : c.WeaponPrefix;
			CharacterMorphAnimation.Spec spec2 = ResolveCharacterVisual(c, avatar, weaponPrefix);
			arpgActor = ArpgActor.Create(_atlas, _arena, _ui, spec2.Group, spec2.Atlas, spec2.WeaponPrefix, isPlayer: true, 0.98f, spec2.ThreeDirection);
		}
		else if (UsesCharacterPresentation(c))
		{
			(string Avatar, string WeaponPrefix) tuple = AllyRender(c);
			string item = tuple.Avatar;
			string item2 = tuple.WeaponPrefix;
			CharacterMorphAnimation.Spec spec2 = ResolveCharacterVisual(c, item, item2);
			arpgActor = ArpgActor.Create(_atlas, _arena, _ui, spec2.Group, spec2.Atlas, spec2.WeaponPrefix, isPlayer: true, 0.98f, spec2.ThreeDirection);
		}
		else if (c.Kind == CombatantKind.Summon)
		{
			string atlasName = ResolveMobAtlas(_atlas, c.Avatar);
			if (string.IsNullOrEmpty(atlasName) && !string.IsNullOrEmpty(c.Disp))
			{
				atlasName = ResolveMobAtlas(_atlas, c.Disp);
			}
			arpgActor = ArpgActor.Create(_atlas, _arena, _ui, "anim", atlasName, "", isPlayer: false, 0.82f);
		}
		else
		{
			PolymorphForm polymorphForm = PolymorphRules.ActiveForm(GameDataProvider.Shared, c);
			string text = (((object)polymorphForm == null) ? MobAtlasName(c.Avatar) : CharacterMorphAnimation.ResolveForm(GameDataProvider.Shared, polymorphForm.Name).Atlas);
			if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(c.Disp))
			{
				text = ResolveMobAtlas(_atlas, c.Disp);
			}
			arpgActor = ArpgActor.Create(_atlas, _arena, _ui, "anim", text, "", isPlayer: false, 0.95f);
			arpgActor.FixedFacing = c.MoveSpeed <= 0.01 && !arpgActor.HasMobDir && !arpgActor.HasEightDir;
			arpgActor.WalkAnimRef = (float)IsometricMovementRules.BaseMoveSpeed;
			if (text.Length > 0 && HostilePlayerRules.IsHostilePlayer(c))
			{
				arpgActor.SetNameColor(HostilePlayerNameColor(c));
			}
		}
		arpgActor.VisualKey = CharacterVisualKey(c);
		arpgActor.SetNameWithoutLevel(c.Disp, c.Level);
		arpgActor.SetTitle(c.Title);
		if (c.Kind == CombatantKind.Player || c.Kind == CombatantKind.Ally)
		{
			arpgActor.ShowName();
		}
		else if (c.Kind == CombatantKind.Mob)
		{
			arpgActor.SetCombatNameOnly();
		}
		arpgActor.Pos = ToVec(c.Pos);
		return arpgActor;
	}

	private static string CharacterVisualKey(Combatant actor)
	{
		if (actor.Kind == CombatantKind.Player)
		{
			return ResolveCharacterVisual(actor, actor.Avatar, "").VisualKey;
		}
		if (!UsesCharacterPresentation(actor))
		{
			if (actor.Kind == CombatantKind.Mob)
			{
				PolymorphForm polymorphForm = PolymorphRules.ActiveForm(GameDataProvider.Shared, actor);
				if ((object)polymorphForm != null)
				{
					return CharacterMorphAnimation.ResolveForm(GameDataProvider.Shared, polymorphForm.Name).VisualKey;
				}
			}
			return $"{actor.Kind}:{actor.Avatar}:{actor.Disp}";
		}
		var (classAvatar, classWeaponPrefix) = AllyRender(actor);
		return ResolveCharacterVisual(actor, classAvatar, classWeaponPrefix).VisualKey;
	}

	private static CharacterMorphAnimation.Spec ResolveCharacterVisual(Combatant actor, string classAvatar, string classWeaponPrefix)
	{
		return CharacterMorphAnimation.Resolve(actor, GameDataProvider.Shared, classAvatar, classWeaponPrefix);
	}

	internal static bool UsesCharacterPresentation(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.Kind != CombatantKind.Player && (actor.Kind != CombatantKind.Ally || UsesMonsterPresentation(actor)))
		{
			return HostilePlayerRules.IsHostilePlayer(actor);
		}
		return true;
	}

	private static bool UsesMonsterPresentation(Combatant actor)
	{
		if (!actor.UsesMonsterTemplate)
		{
			return MonsterCompanionRules.IsCompanion(actor);
		}
		return true;
	}

	private static (string Avatar, string WeaponPrefix) AllyRender(Combatant c)
	{
		ClassDef classDef = ClassCatalog.Find(c.ClassId);
		if (classDef != null)
		{
			bool male = c.Avatar == classDef.MaleAvatar;
			return (Avatar: string.IsNullOrEmpty(c.Avatar) ? classDef.Avatar(male) : c.Avatar, WeaponPrefix: classDef.Weapon);
		}
		return (Avatar: c.Avatar, WeaponPrefix: "");
	}

	private void UpdateView(ArpgActor v, Combatant c, (Vector2 Anchor, float Progress, bool Stepping) walk, float dt, bool revealElement)
	{
		v.Hp = c.Hp;
		v.MaxHp = c.MaxHp;
		v.Mp = c.Mp;
		v.MaxMp = c.MaxMp;
		Vector2 vector = _engine.RenderPos(c);
		v.MinimumWorldDepth = ResolveOpaqueWorldObjectActorDepthFloor(new WorldPoint(vector.X, vector.Y));
		(v.Pos, _, _) = walk;
		v.SetInvisible(c.Kind == CombatantKind.Player && StealthRules.IsInvisible(GameDataProvider.Shared, c));
		CombatantKind kind;
		if (c.Kind == CombatantKind.Mob)
		{
			if (v.Dead || c.Dead)
			{
				if (!v.Dead)
				{
					v.Die(isMob: true);
				}
				v.DeadTimer -= dt;
				float fade = (float)Mathf.Clamp(v.DeadTimer / 0.8f, 0.0, 1.0);
				v.Sync(fade, dt);
				return;
			}
		}
		if (c.Dead)
		{
			if (!v.Dead)
			{
				v.Die(c.Kind == CombatantKind.Mob);
			}
			v.DeadTimer -= dt;
			kind = c.Kind;
			if ((uint)(kind - 2) <= 1u)
			{
				v.DeadTimer = Mathf.Max(v.DeadTimer, 0.05000000074505806);
				v.Sync(1.0, dt);
			}
			else
			{
				float fade = (float)Mathf.Clamp(v.DeadTimer / 0.8f, 0.0, 1.0);
				v.Sync(fade, dt);
			}
			return;
		}
		if (v.Dead && c.Kind != CombatantKind.Mob)
		{
			v.Revive();
		}
		v.SetAbnormalVisual(L1jAbnormalStateRules.Resolve(c));
		Vector2 vector2 = new Vector2((float)c.VelX, (float)c.VelY);
		v.WalkHold = (walk.Stepping ? 0.08f : Mathf.Max(0f, v.WalkHold - dt));
		bool moving = walk.Stepping || v.WalkHold > 0f;
		if (walk.Stepping && vector2.Length() > 4f)
		{
			v.InterruptOneShot();
		}
		if (walk.Stepping)
		{
			v.FaceDirection(c.Facing8);
		}
		else if (vector2.Length() > 4f)
		{
			v.Face(vector2.X, vector2.Y);
		}
		if (UsesCharacterPresentation(c))
		{
			var (desired, fallback) = CharacterWeaponAnimation.Resolve(c, GameDataProvider.Shared);
			v.SetWeaponPrefix(desired, fallback);
		}
		v.MoveSpeed = (float)CombatModifierRules.EffectiveMoveSpeed(c);
		v.DriveLoop(moving);
		v.SyncWalkFrame(walk.Stepping, walk.Progress);
		kind = c.Kind;
		bool flag = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
		v.SetStatus(flag ? "" : StatusLabels.Line(c, revealElement));
		v.Sync(1.0, dt);
	}

	private void SyncProjectiles(double delta)
	{
		HashSet<long> hashSet = new HashSet<long>();
		Rect2 rect = ActiveRenderRect();
		foreach (CombatProjectile projectile in _engine.Projectiles)
		{
			hashSet.Add(projectile.Id);
			if (!rect.HasPoint(ToVec(projectile.Pos)))
			{
				if (_projViews.Remove(projectile.Id, out ProjView value) && GodotObject.IsInstanceValid(value.Node))
				{
					value.Node.QueueFree();
				}
				continue;
			}
			if (!_projViews.TryGetValue(projectile.Id, out ProjView value2))
			{
				value2 = CreateProjView(projectile);
				_projViews[projectile.Id] = value2;
			}
			WorldPoint p = (value2.IsArrow ? projectile.GroundPos : projectile.Pos);
			value2.Node.Position = ToVec(p);
			value2.Node.ZIndex = Depth.Of(p.Y, 4);
			if (value2.IsArrow)
			{
				value2.FrameSeconds += delta;
				int num = (int)(value2.FrameSeconds / 0.04) % 3;
				if (projectile.Facing8 != value2.Dir || num != value2.Frame)
				{
					value2.Dir = projectile.Facing8;
					value2.Frame = num;
					value2.Spr.Texture = ProjectileArt.Arrow(projectile.Facing8, num);
				}
			}
		}
		foreach (var (num3, projView2) in _projViews.ToList())
		{
			if (!hashSet.Contains(num3))
			{
				if (!projView2.IsArrow)
				{
					Burst(projView2.Node.Position, BoltColor);
				}
				if (GodotObject.IsInstanceValid(projView2.Node))
				{
					projView2.Node.QueueFree();
				}
				_projViews.Remove(num3);
			}
		}
	}

	private ProjView CreateProjView(CombatProjectile p)
	{
		bool flag = p.Kind == "arrow";
		WorldPoint p2 = (flag ? p.GroundPos : p.Pos);
		Node2D node2D = new Node2D
		{
			Position = ToVec(p2),
			ZIndex = Depth.Of(p2.Y, 4)
		};
		Sprite2D sprite2D = new Sprite2D
		{
			Centered = !flag
		};
		if (flag)
		{
			sprite2D.Texture = ProjectileArt.Arrow(p.Facing8, 0);
			sprite2D.Offset = -ProjectileArt.ArrowLatticeOrigin;
		}
		else
		{
			sprite2D.Texture = ProjectileArt.Orb(BoltColor);
			sprite2D.Scale = new Vector2(1.3f, 1.3f);
		}
		node2D.AddChild(sprite2D, forceReadableName: false, InternalMode.Disabled);
		_arena.AddChild(node2D, forceReadableName: false, InternalMode.Disabled);
		return new ProjView
		{
			Node = node2D,
			Spr = sprite2D,
			IsArrow = flag,
			Dir = (flag ? p.Facing8 : (-1)),
			Frame = ((!flag) ? (-1) : 0)
		};
	}

	private static Vector2 ToVec(WorldPoint p)
	{
		return EngineAdapter.ToVec(p);
	}

	private Vector2 PlayerPos()
	{
		return ToVec(_engine.Player.Pos);
	}

	private void SyncBattleBgm(bool force, double deltaSeconds = 0.0)
	{
		if (SyncIntegratedTownMusic(ref force))
		{
			ResetCombatMusicState();
			return;
		}
		_combatBgmGrace = Math.Max(0.0, _combatBgmGrace - Math.Max(0.0, deltaSeconds));
		if (_combatBgmGrace <= 0.0)
		{
			_combatBgmGraceBoss = false;
		}
		_combatBgmProbeRemaining -= Math.Max(0.0, deltaSeconds);
		if (force || _combatBgmProbeRemaining <= 0.0)
		{
			_combatBgmProbeRemaining = 0.2;
			_combatBgmEngaged = false;
			_combatBgmEngagedBoss = false;
			foreach (Combatant combatant in _engine.Combatants)
			{
				if (combatant.Kind == CombatantKind.Mob && _engine.Engine.IsExplorationMobEngaged(combatant))
				{
					_combatBgmEngaged = true;
					_combatBgmEngagedBoss |= combatant.IsBoss;
				}
			}
		}
		bool flag = _combatBgmEngaged || _combatBgmGrace > 0.0;
		bool boss = flag && (_combatBgmEngagedBoss || _combatBgmGraceBoss);
		GameAudio instance = GameAudio.Instance;
		if (instance != null)
		{
			string text = ResolveMusicScene(instance, flag, boss);
			if (force || !string.Equals(text, _musicScene, StringComparison.Ordinal))
			{
				_musicScene = text;
				instance.PlayScene(text);
			}
		}
	}

	private string ResolveMusicScene(GameAudio audio, bool fighting, bool boss)
	{
		var (gameX, gameY) = CurrentGameCell();
		if (fighting && boss)
		{
			string text = MapMusicCatalog.ResolveBoss(GameDataProvider.Shared, _mapKey, gameX, gameY);
			if (text != null)
			{
				return text;
			}
		}
		string text2 = MapMusicCatalog.ResolveZone(GameDataProvider.Shared, _mapKey, gameX, gameY);
		if (text2 != null)
		{
			return text2;
		}
		string text3 = WorldAtlasCatalog.ResolveHuntBgmRegionKey(_mapKey, gameX, gameY);
		if (text3 != null)
		{
			string text4 = audio.HuntTrack(text3);
			if (text4 != null)
			{
				return text4;
			}
		}
		string text5 = MapMusicCatalog.ResolveMapTrack(GameDataProvider.Shared, _mapKey);
		if (text5 != null)
		{
			return text5;
		}
		string text6 = audio.HuntTrack(_mapKey);
		if (text6 != null)
		{
			return text6;
		}
		if (fighting)
		{
			if (!boss)
			{
				return "battle";
			}
			return "boss";
		}
		if (audio.LastAmbientScene.Length <= 0)
		{
			return audio.TownScene(_session.TownKey);
		}
		return audio.LastAmbientScene;
	}

	private void SyncEnvironmentSound(double deltaSeconds)
	{
		_environmentSoundProbe -= Math.Max(0.0, deltaSeconds);
		if (!(_environmentSoundProbe > 0.0))
		{
			_environmentSoundProbe = 0.5;
			(int GameX, int GameY) tuple = CurrentGameCell();
			int item = tuple.GameX;
			int item2 = tuple.GameY;
			bool night = !DayNight.IsDay(Time.GetUnixTimeFromSystem());
			GameAudio.Instance?.SetEnvironment(_mapKey, item, item2, night);
		}
	}

	private (int GameX, int GameY) CurrentGameCell()
	{
		MapTopology topology = _topology;
		if (topology == null || !topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY))
		{
			return (GameX: 0, GameY: 0);
		}
		return topology.ToGameCell(localX, localY);
	}

	private void NoteCombatMusic(Combatant? source, Combatant? target)
	{
		if (source != null && target != null && source.Kind == CombatantKind.Mob != (target.Kind == CombatantKind.Mob))
		{
			_combatBgmGrace = 3.0;
			_combatBgmGraceBoss |= source.IsBoss || target.IsBoss;
		}
	}

	private void ResetCombatMusicState()
	{
		_musicScene = "";
		_combatBgmEngaged = false;
		_combatBgmEngagedBoss = false;
		_combatBgmGraceBoss = false;
		_combatBgmGrace = 0.0;
		_combatBgmProbeRemaining = 0.0;
	}

	private int AliveMobs()
	{
		int num = 0;
		foreach (Combatant combatant in _engine.Combatants)
		{
			if (combatant.Kind == CombatantKind.Mob && !combatant.Dead)
			{
				num++;
			}
		}
		return num;
	}

	private Vector2 CamTarget()
	{
		Vector2 result = CamAnchor - _engine.RenderPos(_engine.Player);
		result.X = Mathf.Clamp(result.X, WorldView.X - Field.End.X, 0f - Field.Position.X);
		result.Y = Mathf.Clamp(result.Y, WorldView.Y - Field.End.Y, 0f - Field.Position.Y);
		return result;
	}

	private void BuildGrid()
	{
		Color color = Color.FromHtml("#1c2230".AsSpan());
		for (float num = Field.Position.X; num <= Field.End.X + 0.5f; num += 240f)
		{
			_world.AddChild(new ColorRect
			{
				Color = color,
				Position = new Vector2(num, Field.Position.Y),
				Size = new Vector2(2f, Field.Size.Y),
				MouseFilter = MouseFilterEnum.Ignore
			}, forceReadableName: false, InternalMode.Disabled);
		}
		for (float num2 = Field.Position.Y; num2 <= Field.End.Y + 0.5f; num2 += 240f)
		{
			_world.AddChild(new ColorRect
			{
				Color = color,
				Position = new Vector2(Field.Position.X, num2),
				Size = new Vector2(Field.Size.X, 2f),
				MouseFilter = MouseFilterEnum.Ignore
			}, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void BuildEdges()
	{
		Color color = new Color(0.05f, 0.06f, 0.09f, 0.82f);
		_world.AddChild(new ColorRect
		{
			Color = color,
			Position = Field.Position,
			Size = new Vector2(Field.Size.X, 4f),
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		_world.AddChild(new ColorRect
		{
			Color = color,
			Position = new Vector2(Field.Position.X, Field.End.Y - 4f),
			Size = new Vector2(Field.Size.X, 4f),
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		_world.AddChild(new ColorRect
		{
			Color = color,
			Position = Field.Position,
			Size = new Vector2(4f, Field.Size.Y),
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		_world.AddChild(new ColorRect
		{
			Color = color,
			Position = new Vector2(Field.End.X - 4f, Field.Position.Y),
			Size = new Vector2(4f, Field.Size.Y),
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
	}

	private void BuildObstacleProps(MapObstacles.Layout layout)
	{
		Color color = new Color(0.02f, 0.03f, 0.05f, 0.38f);
		Color color2 = Color.FromHtml("#39404f".AsSpan());
		Color color3 = Color.FromHtml("#5a6580".AsSpan());
		Color color4 = new Color(0.06f, 0.07f, 0.1f, 0.55f);
		foreach (Rect2 item in MapObstacles.WorldRects(layout, Field))
		{
			int z = (int)item.End.Y;
			Add(new Rect2(item.Position + new Vector2(10f, 12f), item.Size), color, z);
			Add(item, color2, z);
			Add(new Rect2(item.Position, new Vector2(item.Size.X, 9f)), color3, z);
			Add(new Rect2(item.Position.X, item.End.Y - 5f, item.Size.X, 5f), color4, z);
		}
		void Add(Rect2 rect, Color color5, int zIndex)
		{
			_arena.AddChild(new ColorRect
			{
				Color = color5,
				Position = rect.Position,
				Size = rect.Size,
				ZIndex = zIndex,
				MouseFilter = MouseFilterEnum.Ignore
			}, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void ToggleGridDebug()
	{
		if (_grid != null)
		{
			if (_gridDebug == null)
			{
				_gridDebug = new CollisionDebugLayer
				{
					ZIndex = 4000
				};
				_gridDebug.Bind(_grid);
				_world.AddChild(_gridDebug, forceReadableName: false, InternalMode.Disabled);
				_probeCell = new WorldGridCell(-1, -1);
			}
			else
			{
				_gridDebug.Visible = !_gridDebug.Visible;
			}
		}
	}

	private void UpdateGridProbe()
	{
		if (_grid != null && _gridDebug != null && _gridDebug.Visible)
		{
			Vector2 vector = _arena.GetGlobalTransformWithCanvas().AffineInverse() * GetViewport().GetMousePosition();
			vector.X = Mathf.Clamp(vector.X, Field.Position.X + 1f, Field.End.X - 1f);
			vector.Y = Mathf.Clamp(vector.Y, Field.Position.Y + 1f, Field.End.Y - 1f);
			WorldPoint worldPoint = EngineAdapter.ToWorld(vector);
			WorldGridCell probeCell = _grid.CellAt(worldPoint);
			if (probeCell.Equals(_probeCell))
			{
				_gridDebug.From = PlayerPos();
				_gridDebug.QueueRedraw();
				return;
			}
			_probeCell = probeCell;
			Combatant player = _engine.Player;
			_gridDebug.From = PlayerPos();
			_gridDebug.To = vector;
			_gridDebug.Reachable = _grid.CanReach(player.Pos, worldPoint, player.Radius);
			_gridDebug.Path = (_gridDebug.Reachable ? _grid.FindPath(player.Pos, worldPoint, player.Radius).Select(ToVec).ToArray() : Array.Empty<Vector2>());
			_gridDebug.QueueRedraw();
		}
	}

	private void Float(Vector2 at, string text, Color col, bool big)
	{
		try
		{
			Label label = new Label
			{
				Text = text,
				Position = at + new Vector2(-24f, -60f),
				Size = new Vector2(48f, 18f),
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore
			};
			label.AddThemeFontSizeOverride("font_size", big ? 22 : 15);
			label.AddThemeColorOverride("font_color", col);
			label.PivotOffset = new Vector2(24f, 9f);
			_ui?.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			Tween? tween = label.CreateTween();
			if (tween != null)
			{
				tween.SetParallel();
				tween.TweenProperty(label, "position:y", label.Position.Y - 30f, 0.6);
				tween.TweenProperty(label, "modulate:a", 0.0, 0.6);
				if (big)
				{
					label.Scale = new Vector2(1.6f, 1.6f);
					tween.TweenProperty(label, "scale", Vector2.One, 0.26).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				}
				tween.Chain().TweenCallback(Callable.From(label.QueueFree));
			}
			else
			{
				label.QueueFree();
			}
		}
		catch
		{
		}
	}

	private void FloatChatBubble(Vector2 at, string speaker, string message)
	{
		if (_playerChatBubble == null || !IsInstanceValid(_playerChatBubble))
		{
			_playerChatBubble = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0f, 24f),
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = 2600
			};
			var style = new StyleBoxFlat
			{
				BgColor = Color.FromHtml("#161b22f0"),
				BorderColor = Color.FromHtml("#ffd76a"),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 4,
				CornerRadiusBottomRight = 4,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				ContentMarginLeft = 10,
				ContentMarginRight = 10,
				ContentMarginTop = 3,
				ContentMarginBottom = 3
			};
			_playerChatBubble.AddThemeStyleboxOverride("panel", style);

			_playerChatBubbleLabel = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Off,
				MouseFilter = MouseFilterEnum.Ignore
			};
			_playerChatBubbleLabel.AddThemeFontSizeOverride("font_size", 12);
			_playerChatBubbleLabel.AddThemeColorOverride("font_color", Colors.White);
			_playerChatBubbleLabel.AddThemeColorOverride("font_outline_color", Color.FromHtml("#12100c"));
			_playerChatBubbleLabel.AddThemeConstantOverride("outline_size", 2);
			_playerChatBubble.AddChild(_playerChatBubbleLabel);

			_ui.AddChild(_playerChatBubble, forceReadableName: false, InternalMode.Disabled);
		}

		string display = string.IsNullOrEmpty(speaker) ? message : $"【{speaker}】{message}";
		if (_playerChatBubbleLabel != null)
		{
			_playerChatBubbleLabel.Text = display;
		}

		_playerChatBubble.ResetSize();
		_playerChatBubbleTimer = 3.5f;
		_playerChatBubble.Modulate = new Color(1f, 1f, 1f, 1f);
		_playerChatBubble.Visible = true;
		_playerChatBubble.Position = PlayerPos() + new Vector2(-_playerChatBubble.Size.X * 0.5f, -85f);
	}

	private void FloatExp(Vector2 at, double exp)
	{
		int expVal = (int)Math.Round(exp);
		if (expVal <= 0) return;

		Label label = new Label
		{
			Text = $"Exp{expVal}",
			Position = at + new Vector2(-28f, -68f),
			Size = new Vector2(60f, 22f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 1950
		};
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#4ade80"));
		label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#052e16"));
		label.AddThemeConstantOverride("outline_size", 3);
		label.PivotOffset = new Vector2(30f, 11f);

		_ui.AddChild(label, forceReadableName: false, InternalMode.Disabled);

		Tween tween = label.CreateTween();
		tween.SetParallel();
		label.Scale = new Vector2(1.25f, 1.25f);
		tween.TweenProperty(label, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(label, "position:y", label.Position.Y - 28f, 0.85);
		tween.TweenProperty(label, "modulate:a", 0.0, 0.85).SetDelay(0.15);
		tween.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	private void FloatGold(Vector2 at, long gold)
	{
		if (gold <= 0) return;

		HBoxContainer box = new HBoxContainer
		{
			Position = at + new Vector2(14f, -32f),
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 1950
		};
		box.AddThemeConstantOverride("separation", 3);

		Texture2D? coinTex = PartyHud.GetCoinTexture();
		if (coinTex != null)
		{
			TextureRect icon = new TextureRect
			{
				Texture = coinTex,
				CustomMinimumSize = new Vector2(24f, 24f),
				Size = new Vector2(24f, 24f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			};
			box.AddChild(icon);
		}

		Label label = new Label
		{
			Text = $"{gold}",
			MouseFilter = MouseFilterEnum.Ignore,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 20);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#fde047"));
		label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#451a03"));
		label.AddThemeConstantOverride("outline_size", 3);
		box.AddChild(label);

		_ui.AddChild(box, forceReadableName: false, InternalMode.Disabled);

		Tween tween = box.CreateTween();
		tween.SetParallel();
		box.Scale = new Vector2(1.25f, 1.25f);
		box.PivotOffset = new Vector2(15f, 12f);
		tween.TweenProperty(box, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(box, "position:y", box.Position.Y - 24f, 0.85);
		tween.TweenProperty(box, "modulate:a", 0.0, 0.85).SetDelay(0.15);
		tween.Chain().TweenCallback(Callable.From(box.QueueFree));
	}

	private void FloatExpAndGold(Vector2 at, double exp, long gold)
	{
		if (exp > 0) FloatExp(at, exp);
		if (gold > 0) FloatGold(at, gold);
	}

	private void Burst(Vector2 at, Color col)
	{
		if (ClientPreferences.EffectsEnabled)
		{
			for (int i = 0; i < 5; i++)
			{
				ColorRect colorRect = new ColorRect
				{
					Color = col,
					Size = new Vector2(5f, 5f),
					Position = at,
					MouseFilter = MouseFilterEnum.Ignore
				};
				_ui.AddChild(colorRect, forceReadableName: false, InternalMode.Disabled);
				double num = _rng.NextDouble() * (Math.PI * 2.0);
				double num2 = 14.0 + _rng.NextDouble() * 18.0;
				Vector2 vector = at + new Vector2((float)(Math.Cos(num) * num2), (float)(Math.Sin(num) * num2));
				Tween tween = colorRect.CreateTween();
				tween.SetParallel();
				tween.TweenProperty(colorRect, "position", vector, 0.28);
				tween.TweenProperty(colorRect, "modulate:a", 0.0, 0.28);
				tween.Chain().TweenCallback(Callable.From(colorRect.QueueFree));
			}
		}
	}

	private static bool IsTripleArrowSkill(string? skillId)
	{
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return false;
		}
		return string.Equals(skillId, "sk_elf_triple", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(skillId, "三重矢", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(SkillInfo.Name(skillId), "三重矢", StringComparison.OrdinalIgnoreCase);
	}

	private void PlayCastAnim(ArpgActor view, Combatant src, Combatant? tgt, string? skillId, bool playBodyAnimation = true)
	{
		double castCd = src.CastCd;
		if (UsesMonsterPresentation(src) && !UsesPlayerAudio(src))
		{
			var (skillName, prefer) = MobCastPresentation(skillId);
			if (playBodyAnimation)
			{
				view.PlayCast(castCd, prefer);
			}
			if (tgt != null)
			{
				_spellFx.Play(skillName, src, tgt, skillId);
			}
			else
			{
				_spellFx.Play(skillName, src, src, skillId);
			}
			GameAudio.Instance?.PlayMobSkill(src.Disp);
			return;
		}
		if (src.Kind == CombatantKind.Summon)
		{
			Vector2 at = ((tgt != null) ? ToVec(tgt.Pos) : ToVec(src.Pos));
			Burst(at, SummonProcColor(skillId));
			return;
		}
		if (playBodyAnimation)
		{
			if (IsTripleArrowSkill(skillId))
			{
				if (tgt != null)
				{
					view.Face((float)(tgt.Pos.X - src.Pos.X), (float)(tgt.Pos.Y - src.Pos.Y));
				}
				bool rangedAttacker = src.AttackRange > 48.0;
				bool rangedShot = tgt != null && CombatRangeRules.DiamondDistance(src.Pos, tgt.Pos) > 48.0;
				view.PlayAttack(_rng, rangedAttacker, rangedShot, _engine.Engine.AttackCycleSeconds(src), _engine.Engine.AttackSpeedRatio(src));
			}
			else
			{
				view.PlayOneShot("skill", castCd);
			}
		}
		if (skillId != null && tgt != null && !IsTripleArrowSkill(skillId))
		{
			_spellFx.Play(SkillInfo.Name(skillId), src, tgt, skillId);
		}
		if (skillId != null)
		{
			if (IsTripleArrowSkill(skillId))
			{
				_spellFx.PlaySelf("三重矢", src);
			}
			else
			{
				_spellFx.PlaySelf(SkillInfo.Name(skillId), tgt ?? src);
			}
		}
		if (IsTripleArrowSkill(skillId))
		{
			PlayAttackSfx(src);
		}
		else
		{
			GameAudio instance = GameAudio.Instance;
			if (skillId == null || instance == null || !instance.PlaySkillCast(skillId, tgt?.Element))
			{
				instance?.PlayEvent("magic");
			}
		}
	}

	private static bool UsesPlayerAudio(Combatant actor)
	{
		return UsesCharacterPresentation(actor);
	}

	private static void PlayAttackSfx(Combatant src)
	{
		GameAudio instance = GameAudio.Instance;
		if (instance != null)
		{
			if (UsesPlayerAudio(src))
			{
				instance.PlayWeaponAttack(WeaponCombatProfile.ResolveFamily(src.MainWeaponId, GameDataProvider.Shared));
			}
			else if (UsesMonsterPresentation(src) && !UsesPlayerAudio(src))
			{
				instance.PlayMobAttack(src.Avatar, src.Disp);
			}
			else if (src.Kind == CombatantKind.Summon)
			{
				instance.PlayMobAttack(src.Avatar, src.Disp);
			}
			else
			{
				instance.PlayWeaponAttack(WeaponCombatProfile.ResolveFamily(src.MainWeaponId, GameDataProvider.Shared));
			}
		}
	}

	private static Color SummonProcColor(string? skillId)
	{
		if (skillId == null)
		{
			return BoltColor;
		}
		if (skillId.Contains("poison"))
		{
			return Color.FromHtml("#7fd06a".AsSpan());
		}
		if (skillId.EndsWith("fire"))
		{
			return Color.FromHtml("#ff7043".AsSpan());
		}
		if (skillId.EndsWith("water"))
		{
			return Color.FromHtml("#4fc3f7".AsSpan());
		}
		if (skillId.EndsWith("wind"))
		{
			return Color.FromHtml("#9ccc65".AsSpan());
		}
		if (skillId.EndsWith("earth"))
		{
			return Color.FromHtml("#c9a06a".AsSpan());
		}
		return BoltColor;
	}

	private static (string? Name, string[] Anims) MobCastPresentation(string? skillId)
	{
		string[] item = new string[4] { "breath", "cast", "skill", "skill2" };
		string[] item2 = new string[3] { "cast", "skill", "skill2" };
		string[] item3 = new string[3] { "cast", "skill2", "skill" };
		if (string.IsNullOrEmpty(skillId))
		{
			return (Name: null, Anims: item3);
		}
		int num = skillId.IndexOf(':');
		string key = ((num > 0) ? skillId.Substring(0, num) : skillId);
		string propertyName = ((num > 0) ? skillId.Substring(num + 1) : "mag");
		JsonObject obj = GameDataProvider.Shared.Mob(key)?[propertyName] as JsonObject;
		string value;
		string item4 = ((obj?["skn"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? value : null);
		string value2;
		string text = ((obj?["type"] is JsonValue jsonValue2 && jsonValue2.TryGetValue<string>(out value2)) ? value2 : "");
		int value3;
		int num2 = ((obj?["actId"] is JsonValue jsonValue3 && jsonValue3.TryGetValue<int>(out value3)) ? value3 : 0);
		if (text == "extra_attack" && num2 > 0)
		{
			return (Name: item4, Anims: new string[4]
			{
				$"act{num2}",
				"attack",
				"skill2",
				"skill"
			});
		}
		if (text == "breath")
		{
			return (Name: item4, Anims: item);
		}
		if (text == "poison")
		{
			return (Name: item4, Anims: item2);
		}
		return (Name: item4, Anims: item3);
	}

	private void BuildHud(Action onExit)
	{
		Control control = new Control
		{
			Position = new Vector2(129f, RnChatTop),
			Size = new Vector2(RnChatW, _viewH - RnChatTop),
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_hud.AddChild(control, forceReadableName: false, InternalMode.Disabled);
		float rnChatW = RnChatW;
		ChatSlice(control, "rn_chat_window", 8f, 10f, rnChatW - 17f, 226f, 20, 20);
		ChatSlice(control, "rn_chat_born", 0f, 0f, rnChatW, 240f, 30, 167);
		Panel("rn_left_panel", 0f, RnLeftY, 138f, 127f);
		Panel("rn_minimap_base", RnMiniX + 12f, RnPanelY + 29f, 135f, 101f);
		BuildMiniMap(new Rect2(RnMiniX + 12f, RnPanelY + 29f, 135f, 101f));
		Panel("rn_minimap_frame", RnMiniX + 4f, RnPanelY, 144f, 134f);
		Panel("rn_macro_panel", RnMacroX, RnPanelY, 167f, 134f);
		float value = Mathf.Round(129f + (RnChatW - 334f) / 2f);
		value = Mathf.Clamp(value, 129f, Mathf.Max(129f, 129f + RnChatW - 334f));
		float num = Mathf.Clamp(RnChatTop - 34f + 12f, 0f, Mathf.Max(0f, RnChatTop));
		_draggableHpMpBar = new DraggableHpMpBar
		{
			Position = new Vector2(value, num)
		};
		_hpFill = _draggableHpMpBar.HpFill;
		_hpTxt = _draggableHpMpBar.HpTxt;
		_mpFill = _draggableHpMpBar.MpFill;
		_mpTxt = _draggableHpMpBar.MpTxt;
		_hpFillRight = 151f;
		_hud.AddChild(_draggableHpMpBar, forceReadableName: false, InternalMode.Disabled);
		_expGauge = BuildGauge(new Vector2(25f, RnLeftY + 26f), ExpBarSize, null, "res://assets/ui/rn_level_bar.png");
		_metaLv = SlotNumXml(43f, 29f, 76f, HorizontalAlignment.Left);
		_metaExp = SlotNumXml(78f, 29f, 122f, HorizontalAlignment.Left);
		Label[] array = new Label[2] { _metaLv, _metaExp };
		foreach (Label obj in array)
		{
			obj.AddThemeColorOverride("font_outline_color", Color.FromHtml("#1a0d06".AsSpan()));
			obj.AddThemeConstantOverride("outline_size", 2);
		}
		_adenaIcon = LeftHudIcon("res://assets/ui/rn_adena_btn.png", 11f, 44f, 18f);
		_adenaValue = SlotNumXml(29f, 48f, 115f, HorizontalAlignment.Right);
		_metaAc = SlotNumXml(41f, 69f, 70f, HorizontalAlignment.Left);
		_alignValue = SlotNumXml(41f, 89f, 70f, HorizontalAlignment.Left);
		_alignIcon = LeftHudIcon(null, 11f, 85f, 18f);
		Vector2 pos = new Vector2(24f, RnLeftY + 107f);
		_weightGauge = BuildGauge(pos, WeightBarSize, null, "res://assets/ui/rn_gauge_low.png");
		_weightPct = SlotNumXml(41f, 109f, 62f, HorizontalAlignment.Left);
		Vector2 pos2 = new Vector2(87f, RnLeftY + 107f);
		_satietyGauge = BuildGauge(pos2, WeightBarSize, null, "res://assets/ui/rn_gauge_low.png");
		_satietyPct = SlotNumXml(105f, 109f, 122f, HorizontalAlignment.Left);
		_weightWarn = LeftHudIcon("res://assets/ui/rn_carry_warn.png", 11f, 105f, 18f);
		_weightWarn.Visible = false;
		_satietyWarn = LeftHudIcon("res://assets/ui/rn_food_warn.png", 71f, 105f, 18f);
		_satietyWarn.Visible = false;
		_metaMr = SlotNumXml(105f, 69f, 129f, HorizontalAlignment.Left);
		_metaMagicDmg = SlotNumXml(105f, 89f, 129f, HorizontalAlignment.Left);
		HBoxContainer logTabs = new HBoxContainer
		{
			Position = new Vector2(143f, RnChatTop + 12f),
			Size = new Vector2(RnChatW - 32f, 18f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		logTabs.AddThemeConstantOverride("separation", 6);

		_tabBtnAll = CreateLogTabButton("全部", ChatLogTab.All);
		_tabBtnChat = CreateLogTabButton("隊伍聊天", ChatLogTab.Chat);
		_tabBtnDrops = CreateLogTabButton("掉落/經驗", ChatLogTab.Drops);
		logTabs.AddChild(_tabBtnAll);
		logTabs.AddChild(_tabBtnChat);
		logTabs.AddChild(_tabBtnDrops);
		_hud.AddChild(logTabs, forceReadableName: false, InternalMode.Disabled);

		_slabLog = new RichTextLabel
		{
			Position = new Vector2(143f, RnChatTop + 32f),
			Size = new Vector2(RnChatW - 32f, 80f),
			BbcodeEnabled = true,
			ScrollActive = false,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_slabLog.AddThemeFontSizeOverride("normal_font_size", 11);
		_slabLog.AddThemeConstantOverride("line_separation", 2);
		_hud.AddChild(_slabLog, forceReadableName: false, InternalMode.Disabled);

		float chatBarW = Math.Min(300f, RnChatW - 32f);
		_draggableChatBar = new DraggableChatBar(chatBarW, OnChatInputSubmitted)
		{
			Position = new Vector2(value + (334f - chatBarW) * 0.5f, num - 28f)
		};
		_chatInput = _draggableChatBar.ChatInput;
		_hud.AddChild(_draggableChatBar, forceReadableName: false, InternalMode.Disabled);

		if (_draggableHpMpBar != null)
		{
			_draggableHpMpBar.OnPositionChanged += delegate(Vector2 barPos)
			{
				if (_draggableChatBar != null)
				{
					_draggableChatBar.Position = new Vector2(barPos.X + (334f - _draggableChatBar.Size.X) * 0.5f, barPos.Y - 28f);
				}
			};
		}

		_slabInfo = MakeLabel(new Vector2(143f, _viewH - 18f), Color.FromHtml("#c8b98a".AsSpan()), 11);
		_slabWeight = MakeLabel(new Vector2(143f, _viewH - 18f), Color.FromHtml("#c8b98a".AsSpan()), 11);
		_slabWeight.Size = new Vector2(RnChatW - 32f, 16f);
		_slabWeight.HorizontalAlignment = HorizontalAlignment.Right;
		BuildQuickBar();
		BuildCombatToggle();
		BuildBottomMacroBar();
		BuildDraggableUtilityBar();
		BuildTeleportMemoryButton();
		BuildPauseMenu();
		if (_session.ActiveDollRemainingSeconds > 0.0 && !string.IsNullOrEmpty(_session.ActiveDollItemKey))
		{
			_engine.Engine.RestoreActiveMagicDoll(_engine.Player, _session.ActiveDollItemKey, _session.ActiveDollItemUid, _session.ActiveDollRemainingSeconds);
		}
		_areaName = MakeLabel(new Vector2(16f, RnLeftY - 19f), Color.FromHtml("#6fa8dc".AsSpan()), 14);
		_areaName.AddThemeColorOverride("font_outline_color", Color.FromHtml("#101c2e".AsSpan()));
		_areaName.AddThemeConstantOverride("outline_size", 3);
		_areaNameText = "";
		SetAreaName((_mapName.Length > 0) ? _mapName : _mapKey);
		RefreshAreaNameColor();
		_buffIcons = new HBoxContainer
		{
			Position = new Vector2(View.X - 520f, 6f),
			Size = new Vector2(508f, 48f),
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_buffIcons.AddThemeConstantOverride("separation", 4);
		_hud.AddChild(_buffIcons, forceReadableName: false, InternalMode.Disabled);
	}

	private void OnChatInputSubmitted(string text)
	{
		string msg = text?.Trim() ?? "";
		if (!string.IsNullOrEmpty(msg))
		{
			if (msg.StartsWith("/") || msg.StartsWith("."))
			{
				string[] parts = msg.TrimStart('/', '.').Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 1)
				{
					string cmd = parts[0].ToLowerInvariant();
					if (cmd == "mobmove" || cmd == "movemob" || cmd == "mm" || cmd == "monmove")
					{
						bool all = parts.Length >= 4 && parts[1].Equals("all", StringComparison.OrdinalIgnoreCase);
						int xIdx = all ? 2 : 1;
						int yIdx = all ? 3 : 2;
						if (parts.Length > yIdx && int.TryParse(parts[xIdx], out int targetCellX) && int.TryParse(parts[yIdx], out int targetCellY))
						{
							(double px, double py) = _topology != null ? _topology.DisplayPixelCenter(targetCellX, targetCellY) : (targetCellX * 48.0 + 24.0, targetCellY * 48.0 + 24.0);
							var dest = new WorldPoint(px, py);
							if (all)
							{
								int count = 0;
								foreach (var c in _engine.Combatants)
								{
									if (c.Kind == CombatantKind.Mob && c.IsAlive)
									{
										c.MarchTarget = dest;
										_engine.Engine.ResetIdleWander(c);
										count++;
									}
								}
								SlabLog($"[color=#00fa9a][GM指揮] 已操控全地圖 {count} 隻怪物往座標 ({targetCellX}, {targetCellY}) 前進行軍！[/color]");
							}
							else
							{
								Combatant? targetMob = _engine.Engine.GetManualTarget(_engine.Player);
								if (targetMob == null || targetMob.Kind != CombatantKind.Mob || !targetMob.IsAlive)
								{
									targetMob = _engine.Combatants
										.Where(c => c.Kind == CombatantKind.Mob && c.IsAlive)
										.OrderBy(c => c.Pos.DistanceTo(_engine.Player.Pos))
										.FirstOrDefault();
								}

								if (targetMob != null)
								{
									targetMob.MarchTarget = dest;
									_engine.Engine.ResetIdleWander(targetMob);
									SlabLog($"[color=#00fa9a][GM指揮] 已操控怪物【{targetMob.Disp}】往座標 ({targetCellX}, {targetCellY}) 前進行軍！[/color]");
								}
								else
								{
									SlabLog("[color=#e6a23c][GM指揮] 視野內沒有可操控的怪物！[/color]");
								}
							}
							if (_chatInput != null) { _chatInput.Text = ""; _chatInput.ReleaseFocus(); }
							return;
						}
						else
						{
							SlabLog("[color=#e6a23c]指令格式: /mobmove <X> <Y> 或 /mobmove all <X> <Y>[/color]");
							if (_chatInput != null) { _chatInput.Text = ""; _chatInput.ReleaseFocus(); }
							return;
						}
					}
					else if (cmd == "mobstop" || cmd == "stopmob")
					{
						int count = 0;
						foreach (var c in _engine.Combatants)
						{
							if (c.Kind == CombatantKind.Mob && c.IsAlive && c.MarchTarget.HasValue)
							{
								c.MarchTarget = null;
								_engine.Engine.ResetIdleWander(c);
								count++;
							}
						}
						SlabLog($"[color=#00fa9a][GM指揮] 已停止全部 {count} 隻怪物的行軍前進！[/color]");
						if (_chatInput != null) { _chatInput.Text = ""; _chatInput.ReleaseFocus(); }
						return;
					}
				}
			}

			string senderName = _engine?.Player?.Disp ?? "冒險者";
			if (NetworkManager.Instance.IsConnected)
			{
				NetworkManager.Instance.SendChat(msg, senderName, "#ffd76a");
			}
			SlabLog($"[color=#66d9ef][隊伍][/color] [color=#ffd76a]{senderName}:[/color] {msg}");
			FloatChatBubble(PlayerPos(), senderName, msg);
		}
		if (_chatInput != null)
		{
			_chatInput.Text = "";
			_chatInput.ReleaseFocus();
		}
	}

	private void RefreshStatusIcons(Combatant player)
	{
		var activeDoll = _engine.Engine.GetActiveDollInfo(player);
		List<StatusIcons.Row> list = StatusIcons.Rows(player, activeDoll);
		string text = string.Join('|', list.ConvertAll((StatusIcons.Row r) => r.Icon));
		if (text == _buffIconSig)
		{
			int num = 0;
			{
				foreach (Node child in _buffIcons.GetChildren())
				{
					if (child is Control control && num < list.Count)
					{
						control.TooltipText = StatusIconTooltip(list[num]);
					}
					num++;
				}
				return;
			}
		}
		_buffIconSig = text;
		foreach (Node child2 in _buffIcons.GetChildren())
		{
			child2.QueueFree();
		}
		foreach (StatusIcons.Row item in list)
		{
			Texture2D texture2D = StatusIcons.Texture(item.Icon);
			if (texture2D != null)
			{
				bool isDoll = (activeDoll.HasValue && string.Equals(item.Icon, activeDoll.Value.Definition.Name, StringComparison.Ordinal))
				              || item.Icon.Contains("娃娃");
				Vector2 iconSize = isDoll ? new Vector2(40f, 44f) : new Vector2(28f, 28f);
				TextureRect node = new TextureRect
				{
					Texture = texture2D,
					CustomMinimumSize = iconSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					TooltipText = StatusIconTooltip(item),
					Modulate = item.Icon.Contains("娃娃") || item.Icon.Contains("守護者") ? Colors.White : new Color(1f, 1f, 1f, 0.55f),
					MouseFilter = MouseFilterEnum.Stop
				};
				_buffIcons.AddChild(node, forceReadableName: false, InternalMode.Disabled);
			}
		}
	}

	private void SyncActiveDollToSession()
	{
		var dollInfo = _engine?.Engine?.GetActiveDollInfo(_engine.Player);
		if (dollInfo.HasValue && dollInfo.Value.RemainingSeconds > 0)
		{
			_session.ActiveDollItemKey = dollInfo.Value.Definition.ItemKey;
			_session.ActiveDollRemainingSeconds = dollInfo.Value.RemainingSeconds;
		}
		else
		{
			_session.ActiveDollItemKey = "";
			_session.ActiveDollItemUid = "";
			_session.ActiveDollRemainingSeconds = 0.0;
		}
	}

	private static string StatusIconTooltip(StatusIcons.Row row)
	{
		if (!(row.Seconds > 0.0))
		{
			return row.Label;
		}
		string timeStr;
		if (row.Seconds >= 60.0)
		{
			int mins = (int)(row.Seconds / 60.0);
			int secs = (int)(row.Seconds % 60.0);
			timeStr = $"{mins} 分 {secs:00} 秒";
		}
		else
		{
			timeStr = $"{Math.Ceiling(row.Seconds):0} 秒";
		}
		if (row.Label.Contains('\n'))
		{
			return $"{row.Label}\n剩餘時間：{timeStr}";
		}
		return $"{row.Label}（剩餘 {timeStr}）";
	}

	private (TextureRect Fill, Label Txt) BuildVital(Vector2 pos, float maxWidth, string? emptyPath, string fullPath)
	{
		if (emptyPath != null)
		{
			_hud.AddChild(new TextureRect
			{
				Texture = GD.Load<Texture2D>(emptyPath),
				Position = pos,
				Size = new Vector2(maxWidth, 14f),
				StretchMode = TextureRect.StretchModeEnum.Scale,
				TextureFilter = TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore
			}, forceReadableName: false, InternalMode.Disabled);
		}
		Texture2D texture2D = GD.Load<Texture2D>(fullPath);
		TextureRect textureRect = new TextureRect
		{
			Texture = new AtlasTexture
			{
				Atlas = texture2D,
				Region = new Rect2(0f, 0f, texture2D.GetWidth(), texture2D.GetHeight())
			},
			Position = pos,
			Size = new Vector2(maxWidth, 14f),
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_hud.AddChild(textureRect, forceReadableName: false, InternalMode.Disabled);
		Label label = new Label
		{
			Position = pos - new Vector2(0f, 2f),
			Size = new Vector2(maxWidth, 14f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#1a0d06".AsSpan()));
		label.AddThemeConstantOverride("outline_size", 1);
		_hud.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		return (Fill: textureRect, Txt: label);
	}

	private static void SetVitalCrop(TextureRect fill, float maxWidth, float ratio, bool anchorRight, float rightEdge)
	{
		float num = maxWidth * ratio;
		fill.Visible = num >= 1f;
		if (fill.Visible)
		{
			AtlasTexture obj = (AtlasTexture)fill.Texture;
			float num2 = obj.Atlas.GetWidth();
			float height = obj.Atlas.GetHeight();
			float num3 = num2 * ratio;
			obj.Region = new Rect2(anchorRight ? (num2 - num3) : 0f, 0f, num3, height);
			fill.Size = new Vector2(num, 14f);
			if (anchorRight)
			{
				fill.Position = new Vector2(rightEdge - num, fill.Position.Y);
			}
		}
	}

	private Label BarMeta(float y)
	{
		return MakeLabel(new Vector2(48f, y), Color.FromHtml("#e8d9a8".AsSpan()), 14);
	}

	private static (double Ratio, string Text) ExperienceProgress(Combatant player)
	{
		if (player.Level >= 99)
		{
			return (Ratio: 1.0, Text: "MAX");
		}
		double num = ProgressionRules.ExperienceProgressRatio(GameDataProvider.Shared, player.Level, player.Experience);
		return (Ratio: num, Text: $"{num * 100.0:0.0000}%");
	}

	private Label SlotNumXml(float anchorX, float anchorY, float right, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		Label label = SlotNum(0f + anchorX, RnLeftY + anchorY - 3f, Math.Max(8f, right - anchorX), align);
		label.VerticalAlignment = VerticalAlignment.Top;
		label.ClipText = true;
		return label;
	}

	private Label SlotNum(float x, float y, float w, HorizontalAlignment align)
	{
		Label label = MakeLabel(new Vector2(x, y), Colors.White, 9);
		label.Size = new Vector2(w, 14f);
		label.HorizontalAlignment = align;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#12100c".AsSpan()));
		label.AddThemeConstantOverride("outline_size", 1);
		return label;
	}

	private static void ChatSlice(Control parent, string name, float x, float y, float w, float h, int capL, int capR)
	{
		parent.AddChild(new NinePatchRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/" + name + ".png"),
			Position = new Vector2(x, y),
			Size = new Vector2(w, h),
			PatchMarginLeft = capL,
			PatchMarginRight = capR,
			AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Stretch,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
	}

	private void Panel(string name, float x, float y, float w, float h)
	{
		_hud.AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/" + name + ".png"),
			Position = new Vector2(x, y),
			Size = new Vector2(w, h),
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
	}

	private TextureRect LeftHudIcon(string? texturePath, float x, float y, float size)
	{
		TextureRect textureRect = new TextureRect
		{
			Position = new Vector2(0f + x, RnLeftY + y),
			Size = new Vector2(size, size),
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		};
		if (texturePath != null)
		{
			textureRect.Texture = GD.Load<Texture2D>(texturePath);
		}
		_hud.AddChild(textureRect, forceReadableName: false, InternalMode.Disabled);
		return textureRect;
	}

	private BarGauge BuildGauge(Vector2 pos, Vector2 size, string? bgPath, string fillPath)
	{
		if (bgPath != null)
		{
			_hud.AddChild(new TextureRect
			{
				Texture = GD.Load<Texture2D>(bgPath),
				Position = pos,
				Size = size,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				TextureFilter = TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore
			}, forceReadableName: false, InternalMode.Disabled);
		}
		Texture2D texture2D = GD.Load<Texture2D>(fillPath);
		TextureRect textureRect = new TextureRect
		{
			Texture = new AtlasTexture
			{
				Atlas = texture2D,
				Region = new Rect2(0f, 0f, texture2D.GetWidth(), texture2D.GetHeight())
			},
			Position = pos,
			Size = size,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_hud.AddChild(textureRect, forceReadableName: false, InternalMode.Disabled);
		return new BarGauge(textureRect, size);
	}

	private void RefreshBars(Combatant player)
	{
		_weightGauge.Set(_weight.Percent, ((double)_weight.Percent >= 80.0) ? "res://assets/ui/rn_gauge_high.png" : (((double)_weight.Percent >= 50.0) ? "res://assets/ui/rn_gauge_mid.png" : "res://assets/ui/rn_gauge_low.png"));
		_weightPct.Text = $"{_weight.Percent:0}";
		_weightWarn.Visible = (double)_weight.Percent >= 80.0;
		double num = SatietyRules.Percent(player);
		_satietyGauge.Set(num, (num >= 100.0) ? "res://assets/ui/rn_gauge_mid.png" : ((num <= 20.0) ? "res://assets/ui/rn_gauge_high.png" : "res://assets/ui/rn_gauge_low.png"));
		_satietyPct.Text = $"{num:0}";
		_satietyWarn.Visible = num <= 20.0;
		_adenaValue.Text = $"{player.Gold:n0}";
	}

	private void RefreshDayNight(Combatant player)
	{
		bool isDark = (_lastDark = DayNight.IsDark(Time.GetUnixTimeFromSystem(), _mapName));
		bool hasNightVision = ElfNightVisionRules.HasNightVision(GameDataProvider.Shared, player, _session.ElfNightVisionEnabled);
		double num = DayNight.VisionRadiusPixels(isDark && !hasNightVision, DayNight.IsLit(player, LanternRules.IsLit(player)));
		_nightMask.Visible = double.IsFinite(num);
		_backBuffer.Visible = _nightMask.Visible;
		_visionOccluded = _nightMask.Visible;
		_visionCutoffSq = (_nightMask.Visible ? ((float)Math.Pow(DayNight.VisionCutoffPixels(num), 2.0)) : float.PositiveInfinity);
		_engine.Engine.PlayerVisionLimit = (_nightMask.Visible ? new double?(DayNight.VisionCutoffPixels(num)) : ((double?)null));
		_engine.Engine.PlayerVisionAspectY = 2.0;
		if (!_nightMask.Visible)
		{
			_shadowViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			return;
		}
		ShaderMaterial obj = (ShaderMaterial)_nightMask.Material;
		obj.SetShaderParameter("center_px", _world.Position + _engine.RenderPos(player));
		obj.SetShaderParameter("clear_px", (float)num);
		obj.SetShaderParameter("iso_aspect_y", 2f);
		obj.SetShaderParameter("band_px", (float)DayNight.DarknessBandPixels);
		obj.SetShaderParameter("band_step", 0.4f);
		obj.SetShaderParameter("max_dark", 0.8f);
		obj.SetShaderParameter("shadow_dark", 0.8f);
		obj.SetShaderParameter("shadow_feather_px", (float)DayNight.OcclusionFeatherPixels);
		Vector2 vector = _engine.RenderPos(player);
		Vector2 vector2 = _world.Position + vector;
		if (CollectShadowRects(vector) || vector2 != _lastShadowPlayerScreen || _world.Position != _lastShadowOffset)
		{
			_lastShadowPlayerScreen = vector2;
			_lastShadowOffset = _world.Position;
			_shadowCaster.SetScene(vector2, _shadowRects, _world.Position);
			_shadowViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}
	}

	private void ToggleElfNightVision()
	{
		_session.ElfNightVisionEnabled = !_session.ElfNightVisionEnabled;
		bool isElf = ElfNightVisionRules.IsElf(_engine.Player);
		if (isElf)
		{
			SlabLog(_session.ElfNightVisionEnabled
				? "[color=#86efac]🌙【妖精夜視】夜視功能已開啟（黑夜與洞窟無視遮蔽）[/color]"
				: "[color=#ffd479]🌙【妖精夜視】夜視功能已關閉（恢復正常黑夜視野）[/color]");
		}
		else
		{
			bool hasHelmet = NightVisionHelmetRules.IsActive(GameDataProvider.Shared, _engine.Player);
			SlabLog(_session.ElfNightVisionEnabled
				? (hasHelmet ? "[color=#86efac]🌙【夜視】頭盔夜視功能已開啟[/color]" : "[color=#86efac]🌙【夜視設定】夜視開關已設為開啟（需妖精或配戴夜視頭盔生效）[/color]")
				: "[color=#ffd479]🌙【夜視設定】夜視開關已設為關閉[/color]");
		}
		RefreshDayNight(_engine.Player);
		RefreshUtilityNightVisionBtn();
	}

	private bool CollectShadowRects(Vector2 playerWorld)
	{
		WorldCollisionGrid grid = _grid;
		if (grid != null)
		{
			float num = (float)grid.CellSize;
			int num2 = (int)(((double)playerWorld.X - grid.OriginX) / (double)num);
			int num3 = (int)(((double)playerWorld.Y - grid.OriginY) / (double)num);
			if (num2 == _shadowCellKeyX && num3 == _shadowCellKeyY)
			{
				return false;
			}
			_shadowCellKeyX = num2;
			_shadowCellKeyY = num3;
			_shadowRects.Clear();
			int num4 = Math.Max(0, (int)(((double)(playerWorld.X - 640f) - grid.OriginX) / (double)num));
			int num5 = Math.Min(grid.Columns - 1, (int)(((double)(playerWorld.X + 640f) - grid.OriginX) / (double)num));
			int num6 = Math.Max(0, (int)(((double)(playerWorld.Y - 640f) - grid.OriginY) / (double)num));
			int num7 = Math.Min(grid.Rows - 1, (int)(((double)(playerWorld.Y + 640f) - grid.OriginY) / (double)num));
			if (num5 < num4 || num7 < num6)
			{
				return true;
			}
			bool[,] array = new bool[num5 - num4 + 1, num7 - num6 + 1];
			for (int i = num6; i <= num7; i++)
			{
				for (int j = num4; j <= num5; j++)
				{
					array[j - num4, i - num6] = grid.IsBlocked(new WorldGridCell(j, i));
				}
			}
			MergeBlockedCells(array, (float)(grid.OriginX + (double)num4 * grid.CellSize), (float)(grid.OriginY + (double)num6 * grid.CellSize), num, num, _shadowRects);
			return true;
		}
		MapTopology topology = _topology;
		if (topology == null || !topology.TryLocalCellAtDisplayPixel(playerWorld.X, playerWorld.Y, out var localX, out var localY))
		{
			return false;
		}
		if (localX == _shadowCellKeyX && localY == _shadowCellKeyY)
		{
			return false;
		}
		_shadowCellKeyX = localX;
		_shadowCellKeyY = localY;
		_shadowRects.Clear();
		(double X, double Y) tuple = topology.DisplayPixelCenter(localX, localY);
		double item = tuple.X;
		double item2 = tuple.Y;
		double item3 = topology.DisplayPixelCenter(localX + 1, localY).X;
		double item4 = topology.DisplayPixelCenter(localX, localY + 1).Y;
		float num8 = (float)(item3 - item);
		float num9 = (float)(item4 - item2);
		if (num8 <= 0f || num9 <= 0f)
		{
			return true;
		}
		int num10 = (int)MathF.Ceiling(640f / num8);
		int num11 = (int)MathF.Ceiling(640f / num9);
		bool[,] array2 = new bool[num10 * 2 + 1, num11 * 2 + 1];
		for (int k = -num11; k <= num11; k++)
		{
			for (int l = -num10; l <= num10; l++)
			{
				array2[l + num10, k + num11] = !topology.IsLegalCell(localX + l, localY + k);
			}
		}
		double num12 = item - (double)((float)(num10 + num11) * num8);
		double num13 = item2 + (double)((float)(num10 - num11) * num9);
		MergeBlockedCells(array2, (float)num12 - num8 * 0.5f, (float)num13 - num9 * 0.5f, num8, num9, _shadowRects);
		return true;
	}

	private static void MergeBlockedCells(bool[,] blocked, float originX, float originY, float cellW, float cellH, List<Rect2> output)
	{
		int length = blocked.GetLength(0);
		int length2 = blocked.GetLength(1);
		Dictionary<(int, int), int> dictionary = new Dictionary<(int, int), int>();
		for (int i = 0; i < length2; i++)
		{
			Dictionary<(int, int), int> dictionary2 = new Dictionary<(int, int), int>();
			int j = 0;
			while (j < length)
			{
				if (!blocked[j, i])
				{
					j++;
					continue;
				}
				int num = j;
				for (; j < length && blocked[j, i]; j++)
				{
				}
				(int, int) key = (num, j);
				if (dictionary.TryGetValue(key, out var value))
				{
					Rect2 rect = output[value];
					output[value] = new Rect2(rect.Position, rect.Size + new Vector2(0f, cellH));
					dictionary2[key] = value;
				}
				else
				{
					output.Add(new Rect2(originX + (float)num * cellW, originY + (float)i * cellH, (float)(j - num) * cellW, cellH));
					dictionary2[key] = output.Count - 1;
				}
			}
			dictionary = dictionary2;
		}
	}

	private static (string Icon, Color Color) AlignmentLook(double alignment)
	{
		return CombatCurveMath.GetAlignmentTier(alignment) switch
		{
			AlignmentTier.Justice => (Icon: "res://assets/ui/rn_align_lawful.png", Color: Color.FromHtml("#6ea8ff".AsSpan())), 
			AlignmentTier.Evil => (Icon: "res://assets/ui/rn_align_chaotic.png", Color: Color.FromHtml("#ff6a5a".AsSpan())), 
			_ => (Icon: "res://assets/ui/rn_align_neutral.png", Color: Color.FromHtml("#f0f0f0".AsSpan())), 
		};
	}

	private void RefreshAlignment(double alignment)
	{
		var (text, color) = AlignmentLook(alignment);
		if (_alignIcon.Texture?.ResourcePath != text)
		{
			_alignIcon.Texture = GD.Load<Texture2D>(text);
		}
		_alignValue.Text = $"{(int)alignment}";
		_alignValue.AddThemeColorOverride("font_color", color);
	}

	private void BuildBottomMacroBar()
	{
		// 0: 回家(回捲)
		BarIcon(0, "回家 (回城卷軸)", delegate
		{
			UseReturnScroll();
		});

		// 1: 角色能力值欄
		BarIcon(1, "角色能力值欄 (裝備與屬性)", ToggleClassicEquipment);

		// 2: 魔法
		BarIcon(2, "魔法技能", ToggleClassicSkills);

		// 3: 道具
		BarIcon(3, "道具背包", ToggleBag);

		// 4: 血盟
		BarIcon(4, "血盟與組隊", ToggleClanParty);

		// 5: 地圖(M)
		BarIcon(5, "世界地圖 [M]", ToggleWorldAtlas);

		// 6: 自動練功的設定(打怪練功)
		BarIcon(6, "自動練功設定 (打怪練功/自動喝水/優先目標)", ToggleSettings);

		// 7: 設定(ESC、存檔返回畫面離開等)
		BarIcon(7, "系統設定 (ESC / 存檔 / 離開)", TogglePause);
	}

	private void BarIcon(int i, string tip, Action onPressed)
	{
		Button button = new Button
		{
			Flat = true,
			Position = new Vector2(RnMacroX + 6f + (float)i * 20f, RnPanelY + 104f),
			Size = new Vector2(19f, 26f),
			CustomMinimumSize = new Vector2(19f, 26f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = tip,
			ZIndex = 2050
		};
		button.Pressed += delegate
		{
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.45f);
			onPressed();
		};
		_hud.AddChild(button, forceReadableName: false, InternalMode.Disabled);
	}

	private Button CreateLogTabButton(string title, ChatLogTab tab)
	{
		Button btn = new Button
		{
			Text = title,
			FocusMode = FocusModeEnum.None,
			Flat = true,
			MouseFilter = MouseFilterEnum.Stop
		};
		btn.AddThemeFontSizeOverride("font_size", 10);
		btn.AddThemeColorOverride("font_color", Colors.White);
		btn.AddThemeColorOverride("font_outline_color", Color.FromHtml("#12100c"));
		btn.AddThemeConstantOverride("outline_size", 2);
		btn.Pressed += delegate
		{
			_currentLogTab = tab;
			RefreshSlabLogView();
		};
		return btn;
	}

	private void UpdateTabButtonStyles()
	{
		if (_tabBtnAll != null)
		{
			_tabBtnAll.Modulate = _currentLogTab == ChatLogTab.All ? Color.FromHtml("#ffd76a") : Color.FromHtml("#8b95a6");
		}
		if (_tabBtnChat != null)
		{
			_tabBtnChat.Modulate = _currentLogTab == ChatLogTab.Chat ? Color.FromHtml("#ffd76a") : Color.FromHtml("#8b95a6");
		}
		if (_tabBtnDrops != null)
		{
			_tabBtnDrops.Modulate = _currentLogTab == ChatLogTab.Drops ? Color.FromHtml("#ffd76a") : Color.FromHtml("#8b95a6");
		}
	}

	private void RefreshSlabLogView()
	{
		if (_slabLog == null) return;

		IEnumerable<string> filtered;
		if (_currentLogTab == ChatLogTab.Chat)
		{
			filtered = _allLogEntries.Where(e => e.Category == ChatLogTab.Chat).Select(e => e.Line);
		}
		else if (_currentLogTab == ChatLogTab.Drops)
		{
			filtered = _allLogEntries.Where(e => e.Category == ChatLogTab.Drops).Select(e => e.Line);
		}
		else
		{
			filtered = _allLogEntries.Select(e => e.Line);
		}

		var lines = filtered.TakeLast(5).ToList();
		_slabLog.Text = string.Join("\n", lines);
		UpdateTabButtonStyles();
	}

	private void SlabLog(string bbcode)
	{
		ChatLogTab cat = ChatLogTab.All;
		if (bbcode.Contains("[隊伍]") || bbcode.Contains("💬"))
		{
			cat = ChatLogTab.Chat;
		}
		else if (bbcode.Contains("獲得 經驗值") || bbcode.Contains("獲得 金幣") || bbcode.Contains("獲得 ") || bbcode.Contains("等級提升") || bbcode.Contains("超過持有上限") || bbcode.Contains("無法取得"))
		{
			cat = ChatLogTab.Drops;
		}

		_allLogEntries.Add((cat, bbcode));
		while (_allLogEntries.Count > 200)
		{
			_allLogEntries.RemoveAt(0);
		}
		RefreshSlabLogView();
	}

	private void BuildPauseMenu()
	{
		_pauseMenu = new Control
		{
			Visible = false,
			ZIndex = 2000,
			Position = Vector2.Zero,
			Size = WorldView,
			MouseFilter = MouseFilterEnum.Stop
		};
		Vector2 size = new Vector2(452.5f, 325f);
		Vector2 vector = new Vector2(482.5f, 352.5f);
		Vector2 vector2 = ((WorldView - vector) * 0.5f).Round();
		Vector2 position = vector2 + new Vector2(15f, 15f);
		_pauseMenu.AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/windows/pause.png"),
			Position = position,
			Size = size,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		_pauseMenu.AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/windows/pause_frame.png"),
			Position = vector2,
			Size = vector,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		_pauseBodyX = position.X + 37.5f;
		_pauseBodyW = 370f;
		float num = position.Y + 60f;
		Label label = new Label
		{
			Text = "暫停",
			Position = new Vector2(_pauseBodyX, num + 4f),
			Size = new Vector2(_pauseBodyW, 30f),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 22);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#e8c07a".AsSpan()));
		_pauseMenu.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Label status = new Label
		{
			Position = new Vector2(_pauseBodyX, num + 152f),
			Size = new Vector2(_pauseBodyW, 20f),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		status.AddThemeFontSizeOverride("font_size", 13);
		status.AddThemeColorOverride("font_color", Color.FromHtml("#8b95a6".AsSpan()));
		AddPauseBtn("繼續遊戲", num + 38f, TogglePause);
		AddPauseBtn("存檔", num + 76f, delegate
		{
			bool flag = SaveManager.Save(_session);
			status.Text = (flag ? "✓ 已存檔。" : "⚠ 存檔失敗，請確認磁碟空間。");
			status.AddThemeColorOverride("font_color", Color.FromHtml((flag ? "#8fd18f" : "#e08b8b").AsSpan()));
		});
		((_onQuitToMenu == null) ? null : AddPauseBtn("回到選單", num + 114f, delegate
		{
			_paused = false;
			QuitToCharacterSelect();
		}))?.AddThemeColorOverride("font_color", Color.FromHtml("#d8c79a".AsSpan()));
		_pauseMenu.AddChild(status, forceReadableName: false, InternalMode.Disabled);
		Label label2 = new Label
		{
			Text = "離開狩獵區、換圖與死亡都會自動存檔",
			Position = new Vector2(_pauseBodyX, num + 174f),
			Size = new Vector2(_pauseBodyW, 22f),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label2.AddThemeFontSizeOverride("font_size", 11);
		label2.AddThemeColorOverride("font_color", Color.FromHtml("#6b7686".AsSpan()));
		_pauseMenu.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		_overlayUi.AddChild(_pauseMenu, forceReadableName: false, InternalMode.Disabled);
	}

	private Button AddPauseBtn(string text, float y, Action onPressed)
	{
		Button button = new Button
		{
			Text = text,
			Position = new Vector2(_pauseBodyX + _pauseBodyW * 0.15f, y),
			CustomMinimumSize = new Vector2(_pauseBodyW * 0.7f, 34f)
		};
		button.Pressed += onPressed;
		_pauseMenu.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		return button;
	}

	private bool TryCloseTopWindow()
	{
		if (_townNpcHost != null && _townNpcHost.CloseTopEmbeddedWindow())
		{
			return true;
		}
		if (_itemTargetOverlay != null)
		{
			CloseItemTargetOverlay();
			return true;
		}
		if (CharmTargeting)
		{
			CancelCharmTargeting();
			return true;
		}
		if (ManualSkillTargeting)
		{
			CancelManualSkillTargeting();
			return true;
		}
		if (_worldNpcPanel != null)
		{
			CloseL1jWorldNpcPanel();
			return true;
		}
		if (_consulDialog != null)
		{
			CloseConsulDialog();
			return true;
		}
		if (_flameConsulDialog != null)
		{
			CloseFlameShadowConsulDialog();
			return true;
		}
		if (_duranDialog != null)
		{
			CloseDuranDialog();
			return true;
		}
		if (_summonPicker != null)
		{
			_summonPicker.QueueFree();
			_summonPicker = null;
			return true;
		}
		if (_tpMemoryPanel != null)
		{
			_tpMemoryPanel.QueueFree();
			_tpMemoryPanel = null;
			return true;
		}
		if (_classicRightPanel != null)
		{
			CloseClassicRight();
			return true;
		}
		if (_classicLeftPanel != null)
		{
			CloseClassicLeft();
			return true;
		}
		return false;
	}

	private void TogglePause()
	{
		_paused = !_paused;
		if (_pauseMenu != null)
		{
			_pauseMenu.Visible = _paused;
		}
	}

	private static bool IsFullscreen()
	{
		DisplayServer.WindowMode windowMode = DisplayServer.WindowGetMode();
		if (windowMode != DisplayServer.WindowMode.Fullscreen)
		{
			return windowMode == DisplayServer.WindowMode.ExclusiveFullscreen;
		}
		return true;
	}

	private static string FullscreenLabel()
	{
		if (!IsFullscreen())
		{
			return "全螢幕";
		}
		return "視窗化";
	}

	private void ToggleFullscreen()
	{
		DisplayServer.WindowSetMode((DisplayServer.WindowMode)(IsFullscreen() ? 0 : 3));
	}

	private string?[] QuickBarSkillLayout()
	{
		GameData shared = GameDataProvider.Shared;
		string[] array = new string[QuickBar.TotalSlots];
		for (int i = 0; i < QuickBar.TotalSlots; i++)
		{
			if (string.IsNullOrEmpty(_session.QuickItems[i]))
			{
				string text = _session.QuickSkills[i];
				if (text != null && text.Length != 0 && QuickBarSkillUsable(shared, text))
				{
					array[i] = text;
				}
			}
		}
		return array;
	}

	private bool QuickBarSkillUsable(GameData data, string skillId)
	{
		if (skillId == "sk_teleport")
		{
			return ClassKitRegistry.CanUseSkill(_engine.Player, skillId, data);
		}
		if (SkillInfo.IsCastable(skillId))
		{
			return ClassKitRegistry.CanUseSkill(_engine.Player, skillId, data);
		}
		return false;
	}

	public static bool CanAssignSkillToQuickBar(string skillId)
	{
		if (skillId.Length > 0 && !SkillInfo.IsProcOnly(skillId) && SkillInfo.Type(skillId) != "passive")
		{
			if (!(skillId == "sk_teleport"))
			{
				return SkillInfo.IsCastable(skillId);
			}
			return true;
		}
		return false;
	}

	private int PlayerSkillMpCost(string skillId)
	{
		JsonObject jsonObject = GameDataProvider.Shared.Skill(skillId);
		if (jsonObject == null)
		{
			return SkillInfo.Mp(skillId);
		}
		return RelicConditionalCombatRules.SkillManaCost(GameDataProvider.Shared, _engine.Player, skillId, CombatModifierRules.SkillMpCost(_engine.Player, jsonObject, skillId));
	}

	private void BuildQuickBar()
	{
		GameData shared = GameDataProvider.Shared;
		int num = Math.Clamp(_session.QuickPage, 0, 1);
		_session.QuickPage = num;
		string[] array = QuickBarSkillLayout();
		for (int i = 0; i < 8; i++)
		{
			int num2 = QuickBar.GlobalSlot(num, i);
			string text = _session.QuickItems[num2];
			if (!string.IsNullOrEmpty(text))
			{
				var (text2, stackUid, _) = QuickBar.DecodeAssignment(text);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					BuildQuickItemSlot(i, num2, text, text2, stackUid, shared);
				}
			}
		}
		for (int j = 0; j < 8; j++)
		{
			int num3 = QuickBar.GlobalSlot(num, j);
			string text3 = array[num3];
			if (text3 == null)
			{
				continue;
			}
			bool num4 = text3 == "sk_teleport";
			bool flag = SkillExecutionRules.IsManualOnly(GameDataProvider.Shared, text3);
			int num5 = j;
			int num6 = PlayerSkillMpCost(text3);
			string id = text3;
			bool flag2 = SummonRules.SkillIds.Contains(id);
			Vector2 position = QuickSlotPos(num5);
			Texture2D texture2D = SkillIcons.For(id);
			QuickSlotButton obj = new QuickSlotButton
			{
				Slot = num3,
				OnDropItem = OnQuickDrop,
				DragPayload = "skill:" + id,
				DragLabel = SkillInfo.Name(id),
				OnDragOut = ClearQuickSlot,
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				Text = ((texture2D == null) ? SkillInfo.Name(id) : ""),
				Position = position,
				Size = QuickSlotSize,
				CustomMinimumSize = QuickSlotSize
			};
			string obj2 = $"{SkillInfo.Name(id)}（{SkillInfo.ResourceLabel(id, num6)} · F{num5 + 5}）";
			string obj3 = (flag2 ? "\n點擊選擇召喚形態（熱鍵直接放預設形態）" : "");
			string text4 = SkillInfo.UsageDescription(id);
			obj.TooltipText = obj2 + obj3 + ((text4 != null && text4.Length > 0) ? ("\n" + text4 + "\n按下後以準星點選怪物；不可設定自動施放") : "") + "\n可從技能視窗／背包拖曳替換·拖到空白處取消";
			QuickSlotButton quickSlotButton = obj;
			quickSlotButton.SetQuickBarSkillIcon(texture2D);
			quickSlotButton.AddThemeFontSizeOverride("font_size", 11);
			if (!num4 && !SkillInfo.IsProcOnly(id) && !flag)
			{
				AttachAutoBadge(quickSlotButton, _session.AutoCast.Contains(id), AutoCastHint(id), delegate(bool on)
				{
					SetAutoSkillEnabled(id, on);
				});
			}
			Action action = (quickSlotButton.OnActivate = (num4 ? new Action(CastTeleportSkill) : (flag2 ? ((Action)delegate
			{
				ToggleSummonPicker(id);
			}) : ((Action)delegate
			{
				TryCastPlayerSkill(id);
			}))));
			_quickSlotAction[num5] = (flag2 ? ((Action)delegate
			{
				TryCastPlayerSkill(id);
			}) : action);
			AttachQuickBarPaging(quickSlotButton);
			_hud.AddChild(quickSlotButton, forceReadableName: false, InternalMode.Disabled);
			_skillBtns.Add((id, quickSlotButton, num6));
		}
		for (int num7 = 0; num7 < 8; num7++)
		{
			if (_quickSlotAction[num7] == null)
			{
				BuildQuickEmptyTarget(num7, QuickBar.GlobalSlot(num, num7));
			}
		}
		BuildQuickPageIndicator(num);
		SyncAutoAttackSkill();
		BuildFloatingQuickBar();
	}

	private void BuildQuickPageIndicator(int page)
	{
		int num = (page + 1) % 2;
		Button button = new Button
		{
			Flat = true,
			Position = QuickPageArrowPos,
			Size = QuickPageArrowSize,
			FocusMode = FocusModeEnum.None,
			TooltipText = $"切到快捷欄 第 {num + 1} 頁（F5~F12）" + "\n兩頁在設定中開啟的自動技能與道具都會生效"
		};
		button.Pressed += delegate
		{
			SwitchQuickBarPage(1);
		};
		AttachQuickBarPaging(button);
		_hud.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		_quickEmptyTargets.Add(button);
	}

	private void BuildTeleportMemoryButton()
	{
		Button button = new Button
		{
			Flat = true,
			Position = TeleportMemoryButtonPos,
			Size = TeleportMemoryButtonSize,
			FocusMode = FocusModeEnum.None,
			TooltipText = "記憶座標／指定傳送\n記住目前位置，或傳送到已記住的座標\n（收費與容量照傳送控制戒指／時空行者的規則）"
		};
		button.Pressed += delegate
		{
			OpenTeleportMemoryFromWorldAtlas(focusName: false);
		};
		_hud.AddChild(button, forceReadableName: false, InternalMode.Disabled);
	}

	private void AttachQuickBarPaging(Control control)
	{
		control.GuiInput += delegate(InputEvent @event)
		{
			if (@event is InputEventMouseButton { ButtonIndex: var buttonIndex } inputEventMouseButton)
			{
				switch (buttonIndex)
				{
				case MouseButton.WheelUp:
					if (inputEventMouseButton.Pressed)
					{
						control.AcceptEvent();
						SwitchQuickBarPage(-1);
					}
					break;
				case MouseButton.WheelDown:
					if (inputEventMouseButton.Pressed)
					{
						control.AcceptEvent();
						SwitchQuickBarPage(1);
					}
					break;
				}
			}
		};
	}

	private void SwitchQuickBarPage(int delta)
	{
		int num = ((_session.QuickPage + delta) % 2 + 2) % 2;
		if (num != _session.QuickPage)
		{
			_session.QuickPage = num;
			RebuildQuickBar();
			SlabLog($"[color=#c9b06a]快捷欄 第 {num + 1} 頁[/color]");
		}
	}

	private static bool IsMagicAttackSkill(string skillId)
	{
		JsonObject jsonObject = GameDataProvider.Shared.Skill(skillId);
		if (jsonObject != null && CombatSkill.TryRead(skillId, jsonObject, out CombatSkill skill) && skill != null)
		{
			return skill.IsMagicDamage;
		}
		return false;
	}

	private void SyncAutoAttackSkill()
	{
		_engine.Player.AutoAttackSkillId = (from id in QuickBarSkillLayout()
			where id != null
			select (id)).FirstOrDefault((string id) => _session.AutoCast.Contains(id) && IsMagicAttackSkill(id)) ?? "";
	}

	private Vector2 QuickSlotPos(int slot)
	{
		return new Vector2(RnMacroX + 22f + (float)(slot % 4) * 34f, RnPanelY + 28f + (float)(slot / 4) * 34f);
	}

	private static string AutoCastHint(string id)
	{
		if (SkillInfo.IsCleanse(id))
		{
			return $"我方 {15.0:0} 格內有解得掉的異常狀態才自動施放";
		}
		if (SkillInfo.UsesHealingSlot(id))
		{
			if (L1jSkillTargetRules.RequiresManualCharacterTarget(GameDataProvider.Shared, id))
			{
				return $"自己低於 {70}%，或夥伴低於設定門檻時自動治癒";
			}
			return $"HP 低於 {70}% 才自動施放（免得滿血浪費 MP）";
		}
		JsonObject jsonObject = GameDataProvider.Shared.Skill(id);
		if (jsonObject != null && SummonRules.IsSummonSkill(id, jsonObject))
		{
			return "場上沒有自己的召喚物時才自動施放（自動走預設最高階形態）";
		}
		if (SkillInfo.Type(id) == "buff")
		{
			return L1jSkillTargetRules.RequiresManualCharacterTarget(GameDataProvider.Shared, id) ? "自己或範圍內夥伴缺少這個增益時才自動補放" : "身上沒有這個增益時才自動施放";
		}
		return "冷卻好且 MP 足夠就自動施放";
	}

	private void AutoCastStep()
	{
		if (_dead || !_engine.Player.AutomaticCombatEnabled || !_mapRule.UsableSkill || _session.AutoCast.Count == 0 || _engine.Engine.HasQueuedManualCast(_engine.Player) || CharmTargeting || ManualSkillTargeting)
		{
			return;
		}
		Combatant player = _engine.Player;
		if (!_weight.ActionsAllowed || player.CastCd > 0.0 || !player.CanCast || (player.MaxMp > 0.0 && player.Mp < player.MaxMp * ((double)_session.AutoSkillMpPercent / 100.0)))
		{
			return;
		}
		var allAutoSkills = QuickBarSkillLayout().Where(id => !string.IsNullOrEmpty(id)).Concat(_session.AutoCast).Distinct(StringComparer.Ordinal);
		foreach (string text in allAutoSkills)
		{
			if (text != null && _session.AutoCast.Contains(text) && !(player.Mp < (double)PlayerSkillMpCost(text)) && CanAttemptAutoCast(text, player) && ((ShouldAutoCast(text, player) && _engine.Engine.TryAutoCastSkill(player, text)) || TryAutoSupportMonsterCompanion(text, player)))
			{
				break;
			}
		}
	}

	private bool ShouldAutoCast(string id, Combatant p)
	{
		if (!CanAttemptAutoCast(id, p))
		{
			return false;
		}
		if (SkillInfo.IsCleanse(id))
		{
			return _engine.Engine.HasCleanseTarget(p, id);
		}
		if (SkillInfo.UsesHealingSlot(id))
		{
			return p.Hp < p.MaxHp * 0.7;
		}
		JsonObject jsonObject = GameDataProvider.Shared.Skill(id);
		if (jsonObject != null && SummonRules.IsSummonSkill(id, jsonObject))
		{
			return _engine.Engine.ActiveSummonsOf(p).Count == 0;
		}
		if (SkillInfo.Type(id) == "buff")
		{
			return MonsterCompanionSupportRules.NeedsBuff(p, id);
		}
		return true;
	}

	private bool CanAttemptAutoCast(string id, Combatant p)
	{
		if (id == "sk_teleport")
		{
			return false;
		}
		if (SkillInfo.IsProcOnly(id))
		{
			return false;
		}
		if (SkillExecutionRules.IsManualOnly(GameDataProvider.Shared, id))
		{
			return false;
		}
		if (_session.AutoSkillHpPercent > 0 && SkillInfo.HpCost(id) > 0 && p.MaxHp > 0.0 && p.Hp < p.MaxHp * ((double)_session.AutoSkillHpPercent / 100.0))
		{
			return false;
		}
		return true;
	}

	private bool TryAutoSupportMonsterCompanion(string skillId, Combatant caster)
	{
		GameData data = GameDataProvider.Shared;
		if (!L1jSkillTargetRules.RequiresManualCharacterTarget(data, skillId))
		{
			return false;
		}
		if (SkillInfo.IsCleanse(skillId))
		{
			return false;
		}
		IEnumerable<Combatant> source = _engine.Engine.Combatants.Where((Combatant target) => MonsterCompanionSupportRules.CanReceive(data, skillId, target));
		if (SkillInfo.UsesHealingSlot(skillId))
		{
			source = (from target in source
				where MonsterCompanionSupportRules.NeedsHealing(target, _session.CompanionAutoHealSkillHpPercent)
				orderby target.Hp / target.MaxHp
				select target).ThenBy<Combatant, string>((Combatant target) => target.Key, StringComparer.Ordinal);
		}
		else
		{
			if (!(SkillInfo.Type(skillId) == "buff"))
			{
				return false;
			}
			source = source.Where((Combatant target) => MonsterCompanionSupportRules.NeedsBuff(target, skillId)).OrderBy<Combatant, string>((Combatant target) => target.Key, StringComparer.Ordinal);
		}
		foreach (Combatant item in source)
		{
			if (_engine.Engine.TryAutoCastSkill(caster, skillId, item))
			{
				return true;
			}
		}
		return false;
	}

	private void ToggleSummonPicker(string skillId)
	{
		if (_summonPicker != null)
		{
			_summonPicker.QueueFree();
			_summonPicker = null;
			return;
		}
		IReadOnlyList<SummonFormInfo> readOnlyList = _engine.Engine.AvailableSummonForms(_engine.Player, skillId);
		bool flag = readOnlyList.Any((SummonFormInfo f) => !f.Unlocked && f.LockReason.Contains("召喚控制"));
		var (control, control2) = ClassicMapFrame.Create(new Vector2(24f, 214f), new Vector2(320f, 388f), delegate
		{
			_summonPicker?.QueueFree();
			_summonPicker = null;
		}, 1500);
		control.AddChild(ClassicMapFrame.Title(SkillInfo.Name(skillId) + "：選擇形態"), forceReadableName: false, InternalMode.Disabled);
		Label label = new Label
		{
			Text = (flag ? "無召喚控制裝備：僅能召預設" : "◆＝召喚控制形態"),
			Position = new Vector2(0f, 26f),
			Size = new Vector2(control2.Size.X, 18f)
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", Color.FromHtml((flag ? "#e0b070" : "#a9a497").AsSpan()));
		control2.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Position = new Vector2(0f, 48f),
			Size = new Vector2(control2.Size.X, control2.Size.Y - 48f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(control2.Size.X - 16f, 0f)
		};
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		control2.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		AddSummonFormBtn(vBoxContainer, "★ 預設（最高階）", enabled: true, delegate
		{
			CastSummonForm(skillId, null);
		});
		foreach (SummonFormInfo item in readOnlyList)
		{
			string text = $"Lv{item.RequiredLevel}" + ((item.RequiredCharisma > 0) ? $"/魅{item.RequiredCharisma}" : "");
			string text2 = (item.Unlocked ? (item.Name + "  " + text + (item.NeedsControl ? " ◆" : "")) : $"{item.Name}  {text}（{item.LockReason}）");
			SummonFormInfo form = item;
			AddSummonFormBtn(vBoxContainer, text2, item.Unlocked, delegate
			{
				CastSummonForm(skillId, form.Name);
			});
		}
		AddAboveBarPanel(control);
		_summonPicker = control;
	}

	private static void AddSummonFormBtn(VBoxContainer vbox, string text, bool enabled, Action onPressed)
	{
		Button button = new Button
		{
			Text = text,
			Disabled = !enabled,
			CustomMinimumSize = new Vector2(280f, 30f)
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		button.Alignment = HorizontalAlignment.Left;
		if (enabled)
		{
			button.Pressed += onPressed;
		}
		vbox.AddChild(button, forceReadableName: false, InternalMode.Disabled);
	}

	private void CastSummonForm(string skillId, string? form)
	{
		if (!_mapRule.UsableSkill || !_mapRule.RecallPets)
		{
			SlabLog("[color=#e2938f]此地圖無法召喚[/color]");
		}
		else if (QueuePlayerManualSkill(skillId, null, form))
		{
			_summonPicker?.QueueFree();
			_summonPicker = null;
		}
	}

	private Label MakeLabel(Vector2 pos, Color col, int size)
	{
		Label label = new Label
		{
			Position = pos,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", col);
		_hud.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		return label;
	}

	private void RefreshHud()
	{
		Combatant player = _engine.Player;
		SetVitalCrop(_hpFill, 142f, (float)Mathf.Clamp(player.Hp / Mathf.Max(1.0, player.MaxHp), 0.0, 1.0), anchorRight: true, _hpFillRight);
		_hpTxt.Text = $"{(int)player.Hp} / {(int)player.MaxHp}";
		SetVitalCrop(_mpFill, 142f, (float)Mathf.Clamp(player.Mp / Mathf.Max(1.0, player.MaxMp), 0.0, 1.0), anchorRight: false, 0f);
		_mpTxt.Text = $"{(int)player.Mp} / {(int)player.MaxMp}";
		RefreshStatusIcons(player);
		_metaLv.Text = $"{player.Level}";
		var (num, text) = ExperienceProgress(player);
		_expGauge.Set(num * 100.0, "res://assets/ui/rn_level_bar.png");
		_metaExp.Text = text;
		_metaAc.Text = $"{player.D.ArmorClass:0}";
		_metaMr.Text = $"{player.D.MagicResist:0}";
		_metaMagicDmg.Text = $"{player.D.MagicDamage:0}";
		RefreshAlignment(player.Alignment);
		RefreshBars(player);
		RefreshWeight(player);
		if (_skillBtns.Count > 0)
		{
			foreach (var skillBtn in _skillBtns)
			{
				IconSlotButton item = skillBtn.Btn;
				int item2 = skillBtn.Mp;
				item.SetDimmed(player.Mp < (double)item2);
			}
		}
		foreach (var quickItemBtn in _quickItemBtns)
		{
			int item3 = quickItemBtn.Slot;
			string item4 = quickItemBtn.ItemKey;
			string stackUid = quickItemBtn.StackUid;
			IconSlotButton item5 = quickItemBtn.Btn;
			bool flag = stackUid.Length > 0;
			bool flag2 = flag && player.EquippedItems.Values.Any((ItemStack itemStack) => itemStack.Uid == stackUid);
			long num2 = (flag ? ((player.InventoryStacks.Any((ItemStack itemStack) => itemStack.Uid == stackUid) || flag2) ? 1 : 0) : ItemStackInventory.CountByItemKey(player.InventoryStacks, item4));
			item5.Disabled = num2 == 0;
			item5.Text = "";
			item5.TooltipText = (flag ? $"{ItemDisplayName(item4)}（{(flag2 ? "已穿戴；按下脫下" : "按下穿戴")}·F{item3 + 5}·拖到空白處取消）" : $"{ItemDisplayName(item4)} ×{num2}（F{item3 + 5}·拖到空白處取消）");
		}
	}

	private void WeightStep(double delta)
	{
		_weightCd -= delta;
		if (!(_weightCd > 0.0))
		{
			_weightCd = 0.5;
			if (_engine.Player.Dead || !TowerOfInsolenceCatalog.RecordsReturnPosition(_mapKey))
			{
				_session.LastHuntMap = "";
			}
			else
			{
				_session.LastHuntMap = _mapKey;
				_session.LastHuntX = _engine.Player.Pos.X;
				_session.LastHuntY = _engine.Player.Pos.Y;
			}
			_weight = WeightRules.Evaluate(GameDataProvider.Shared, _engine.Player);
			WeightRules.Publish(_engine.Player, in _weight);
		}
	}

	private void RefreshWeight(Combatant p)
	{
		int percent = _weight.Percent;
		int loadTier = _weight.LoadTier;
		if (loadTier != _shownLoadTier)
		{
			if (_shownLoadTier >= 0 && loadTier > 0)
			{
				SlabLog($"[color={LoadTierColor(loadTier)}]負重 {percent}%{LoadPenaltyText(loadTier)}[/color]");
			}
			else if (_shownLoadTier > 0 && loadTier == 0)
			{
				SlabLog("[color=#8fdd8f]負重恢復正常[/color]");
			}
			_shownLoadTier = loadTier;
		}
	}

	private static string LoadPenaltyText(int tier)
	{
		return tier switch
		{
			1 => "（過重·停止自然回復·命中下降）", 
			2 => "（過重·無法攻擊與施法）", 
			3 => "（超重·無法攻擊與施法）", 
			_ => "", 
		};
	}

	private static string LoadTierColor(int tier)
	{
		return tier switch
		{
			1 => "#e8d06a", 
			2 => "#e8a05a", 
			3 => "#e2706a", 
			_ => "#8fb48a", 
		};
	}

	private void ShowDeath()
	{
		_dead = true;
		Control control = new Control
		{
			Position = Vector2.Zero,
			Size = WorldView,
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 2000
		};
		ColorRect node = new ColorRect
		{
			Position = Vector2.Zero,
			Size = WorldView,
			Color = new Color(0f, 0f, 0f, 0.6f)
		};
		control.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		(Control Root, Control Body) tuple = ClassicMapFrame.Create(DeathPanelOrigin, DeathPanelSize, null, 1);
		Control item = tuple.Root;
		Control item2 = tuple.Body;
		Label label = ClassicMapFrame.Title("你已倒下");
		label.AddThemeColorOverride("font_color", Color.FromHtml("#e8a0a0".AsSpan()));
		item.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		control.AddChild(item, forceReadableName: false, InternalMode.Disabled);
		ReviveOptions.Means means = (_mapRule.Resurrection ? ReviveOptions.Available(GameDataProvider.Shared, _engine.Player) : ReviveOptions.Means.None);
		if (means == ReviveOptions.Means.None)
		{
			Button button = new Button
			{
				Text = "回村",
				Position = new Vector2(item2.Size.X * 0.5f - 80f, 72f),
				CustomMinimumSize = new Vector2(160f, 46f)
			};
			button.Pressed += ReviveAtVillage;
			item2.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		}
		else
		{
			string text = ((means == ReviveOptions.Means.Skill) ? "原地復活（返生術）" : "原地復活（復活卷軸）");
			Button button2 = new Button
			{
				Text = text,
				Position = new Vector2(16f, 72f),
				CustomMinimumSize = new Vector2(196f, 46f)
			};
			button2.Pressed += delegate
			{
				Revive(means);
			};
			item2.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
			Button button3 = new Button
			{
				Text = "回村",
				Position = new Vector2(item2.Size.X - 16f - 140f, 72f),
				CustomMinimumSize = new Vector2(140f, 46f)
			};
			button3.Pressed += ReviveAtVillage;
			item2.AddChild(button3, forceReadableName: false, InternalMode.Disabled);
		}
		_overlayUi.AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_deathPanel = control;
	}

	private void Revive(ReviveOptions.Means means)
	{
		Combatant player = _engine.Player;
		if (_mapRule.Resurrection && ReviveOptions.Consume(GameDataProvider.Shared, player, means) && _engine.Engine.RevivePlayer(player))
		{
			player.Pos = (HasTopology ? _engine.Engine.SnapToExplorationLandingPoint(player.Pos) : new WorldPoint((double)Field.Position.X + (double)Field.Size.X * 0.5, (double)Field.Position.Y + (double)Field.Size.Y * 0.5));
			_deathPanel?.QueueFree();
			_deathPanel = null;
			_dead = false;
			_playerView?.Revive();
			OnPlayerRevived();
		}
	}

	private void ReviveAtVillage()
	{
		L1jGetbackRoute l1jGetbackRoute = ResolveCurrentGetback();
		L1jRestartOutcome l1jRestartOutcome = L1jRestartRules.Apply(_engine.Player, l1jGetbackRoute.Destination.MapId);
		ReviveAlliesBesideLeader();
		if (l1jRestartOutcome.Refilled)
		{
			GameAudio.Instance?.PlaySkillCast("sk_heal_mid");
			_session.PendingRestartFeelGood = true;
		}
		_engine.Player.Buffs.Clear();
		CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
		ApplyGetbackTransfer(l1jGetbackRoute);
	}

	private void FlushPendingRestartMessage()
	{
		if (_session.PendingRestartFeelGood)
		{
			_session.PendingRestartFeelGood = false;
			SlabLog("[color=#8fdd8f]感覺舒服多了。[/color]");
		}
	}

	private void ReviveAlliesBesideLeader()
	{
		if (_session.Party.Members.Count != 0)
		{
			_engine.Engine.ReviveAlliesAtTown();
			Combatant[] array = (from actor in _engine.Combatants
				where actor.Kind == CombatantKind.Ally
				orderby actor.BornSeq
				select actor).ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				array[num].Pos = MercenaryRules.FormationPoint(_engine.Player, num, array.Length);
				_engine.Engine.ClearMoveTarget(array[num]);
			}
		}
	}

	private void ReturnToTown()
	{
		FailCastleWar("返回村莊，攻城失敗。");
		SynchronizeExitState();
		_onExit();
	}

	private Control? _filterListPanel;
	private Action? _refreshFilterPanelGrid;

	private void ToggleFilterListPanel()
	{
		if (_filterListPanel != null && GodotObject.IsInstanceValid(_filterListPanel))
		{
			_filterListPanel.QueueFree();
			_filterListPanel = null;
			_refreshFilterPanelGrid = null;
			return;
		}
		_filterListPanel = BuildFilterListPanel();
		_hud.AddChild(_filterListPanel, forceReadableName: false, InternalMode.Disabled);
	}

	private void AddFilteredItem(string itemKey)
	{
		if (string.IsNullOrWhiteSpace(itemKey)) return;
		if (_session.FilteredItems.Add(itemKey))
		{
			SaveManager.Save(_session);
			string name = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, itemKey, true);
			Float(PlayerPos(), $"〔已過濾〕{name}", Color.FromHtml("#f2c14e"), big: false);
			SlabLog($"[color=#e6c76a]已將 {name} 加入過濾清單！往後獲得將直接掉落於地面。[/color]");
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.48f);
			_refreshFilterPanelGrid?.Invoke();
		}
	}

	private Control BuildFilterListPanel()
	{
		float bagX = _bagPanel != null && GodotObject.IsInstanceValid(_bagPanel) ? _bagPanel.Position.X : (RightAnchor.X - 310f);
		float bagY = _bagPanel != null && GodotObject.IsInstanceValid(_bagPanel) ? _bagPanel.Position.Y : 8f;
		float posX = Mathf.Max(10f, bagX - 250f);
		float posY = bagY + 20f;

		FilterDropPanel root = new FilterDropPanel
		{
			Position = new Vector2(posX, posY),
			CustomMinimumSize = new Vector2(244f, 380f),
			Size = new Vector2(244f, 380f),
			ZIndex = 1960
		};
		root.OnItemDropped = delegate(string key) { AddFilteredItem(key); };

		StyleBoxFlat styleBox = new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.09f, 0.14f, 0.95f),
			BorderColor = Color.FromHtml("#c6b47a"),
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4
		};
		root.AddThemeStyleboxOverride("panel", styleBox);

		VBoxContainer vBox = new VBoxContainer
		{
			OffsetLeft = 8f,
			OffsetTop = 8f,
			OffsetRight = -8f,
			OffsetBottom = -8f
		};
		vBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(vBox);

		HBoxContainer header = new HBoxContainer();
		Label title = new Label
		{
			Text = "【拾取過濾清單】",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		title.AddThemeColorOverride("font_color", Color.FromHtml("#f2c14e"));
		title.AddThemeFontSizeOverride("font_size", 13);
		header.AddChild(title);

		Button closeBtn = new Button
		{
			Text = " ✕ ",
			Flat = true,
			FocusMode = Control.FocusModeEnum.None
		};
		closeBtn.AddThemeColorOverride("font_color", Color.FromHtml("#e2938f"));
		closeBtn.AddThemeFontSizeOverride("font_size", 12);
		closeBtn.Pressed += ToggleFilterListPanel;
		header.AddChild(closeBtn);
		vBox.AddChild(header);

		Label desc = new Label
		{
			Text = "• 拖曳不要的物品進此處加入過濾\n• 獲得同名物品不進背包直接掉地面\n• 點擊下方物品可解除過濾",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		desc.AddThemeColorOverride("font_color", Color.FromHtml("#94a3b8"));
		desc.AddThemeFontSizeOverride("font_size", 10);
		vBox.AddChild(desc);

		HSeparator sep = new HSeparator();
		vBox.AddChild(sep);

		ScrollContainer scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(228f, 210f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		GridContainer grid = new GridContainer
		{
			Columns = 4,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		grid.AddThemeConstantOverride("h_separation", 6);
		grid.AddThemeConstantOverride("v_separation", 6);
		scroll.AddChild(grid);
		vBox.AddChild(scroll);

		Action refreshGrid = delegate
		{
			foreach (Node child in grid.GetChildren())
			{
				grid.RemoveChild(child);
				child.QueueFree();
			}
			if (_session.FilteredItems.Count == 0)
			{
				Label emptyLabel = new Label
				{
					Text = "\n\n  (目前無過濾物品\n   由背包拖曳物品至此)",
					HorizontalAlignment = HorizontalAlignment.Center
				};
				emptyLabel.AddThemeColorOverride("font_color", Color.FromHtml("#64748b"));
				emptyLabel.AddThemeFontSizeOverride("font_size", 11);
				grid.AddChild(emptyLabel);
				return;
			}
			foreach (string filteredKey in _session.FilteredItems.ToList())
			{
				string keyRef = filteredKey;
				string itemName = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, keyRef, true);
				Button slotBtn = new Button
				{
					CustomMinimumSize = new Vector2(50f, 50f),
					TooltipText = $"{itemName}\n(點擊解除過濾)",
					FocusMode = Control.FocusModeEnum.None
				};
				StyleBoxFlat slotStyle = new StyleBoxFlat
				{
					BgColor = Color.FromHtml("#161d2d"),
					BorderColor = Color.FromHtml("#3b4c68"),
					BorderWidthBottom = 1,
					BorderWidthLeft = 1,
					BorderWidthRight = 1,
					BorderWidthTop = 1,
					CornerRadiusBottomLeft = 2,
					CornerRadiusBottomRight = 2,
					CornerRadiusTopLeft = 2,
					CornerRadiusTopRight = 2
				};
				slotBtn.AddThemeStyleboxOverride("normal", slotStyle);

				Texture2D tex = ItemIcons.For(keyRef);
				if (tex != null)
				{
					TextureRect iconRect = new TextureRect
					{
						Texture = tex,
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.Scale,
						TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
						MouseFilter = Control.MouseFilterEnum.Ignore,
						Size = new Vector2(34f, 34f),
						Position = new Vector2(8f, 8f)
					};
					slotBtn.AddChild(iconRect);
				}
				Label delBadge = new Label
				{
					Text = "✕",
					Position = new Vector2(36f, 1f),
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				delBadge.AddThemeColorOverride("font_color", Color.FromHtml("#ef4444"));
				delBadge.AddThemeFontSizeOverride("font_size", 10);
				slotBtn.AddChild(delBadge);

				slotBtn.Pressed += delegate
				{
					_session.FilteredItems.Remove(keyRef);
					SaveManager.Save(_session);
					GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.48f);
					SlabLog($"[color=#7fd0ff]已將 {itemName} 移出過濾清單，往後將正常拾取至背包[/color]");
					_refreshFilterPanelGrid?.Invoke();
				};
				grid.AddChild(slotBtn);
			}
		};

		_refreshFilterPanelGrid = refreshGrid;
		refreshGrid();

		HBoxContainer bottomBar = new HBoxContainer();
		Button clearBtn = new Button
		{
			Text = "清空清單",
			FocusMode = Control.FocusModeEnum.None
		};
		clearBtn.AddThemeFontSizeOverride("font_size", 11);
		clearBtn.AddThemeColorOverride("font_color", Color.FromHtml("#e2938f"));
		clearBtn.Pressed += delegate
		{
			if (_session.FilteredItems.Count > 0)
			{
				_session.FilteredItems.Clear();
				SaveManager.Save(_session);
				GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.48f);
				SlabLog("[color=#7fd0ff]已清空過濾清單[/color]");
				refreshGrid();
			}
		};
		bottomBar.AddChild(clearBtn);
		vBox.AddChild(bottomBar);

		return root;
	}

	private void ToggleBag()
	{
		if (_filterListPanel != null && GodotObject.IsInstanceValid(_filterListPanel))
		{
			_filterListPanel.QueueFree();
			_filterListPanel = null;
			_refreshFilterPanelGrid = null;
		}
		ToggleRightAnchor("bag", BuildBagPanel);
	}

	private Control BuildBagPanel()
	{
		ClassicWebWindows.BagShell shell = ClassicWebWindows.CreateBagShell(RightAnchor, ToggleBag, "拖曳換位");
		shell.Tabs.SelectFirstOccupied(GameDataProvider.Shared, _engine.Player.InventoryStacks.Select((ItemStack stack) => stack.ItemKey));
		shell.Panel.ZIndex = 1950;
		_bagPanel = shell.Panel;

		BagFilterButton filterBtn = new BagFilterButton
		{
			Text = "過濾清單",
			Position = new Vector2(16f, 412f),
			Size = new Vector2(62f, 24f),
			TooltipText = "點擊開啟拾取過濾清單\n亦可將不要的物品直接拖曳至此加入過濾"
		};
		filterBtn.AddThemeFontSizeOverride("font_size", 11);
		filterBtn.AddThemeColorOverride("font_color", Color.FromHtml("#f2c14e"));
		StyleBoxFlat filterBtnStyle = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#161d2d"),
			BorderColor = Color.FromHtml("#c6b47a"),
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3
		};
		filterBtn.AddThemeStyleboxOverride("normal", filterBtnStyle);
		filterBtn.AddThemeStyleboxOverride("hover", filterBtnStyle);
		filterBtn.Pressed += ToggleFilterListPanel;
		filterBtn.OnItemDropped = delegate(string itemKey)
		{
			AddFilteredItem(itemKey);
		};
		shell.Panel.AddChild(filterBtn, forceReadableName: false, InternalMode.Disabled);

		shell.Status.Position = new Vector2(80f, 411f);
		shell.Status.Size = new Vector2(120f, 26f);
		shell.Status.ClipText = false;
		shell.Status.ZIndex = 5;

		_bagRefresh = delegate
		{
			RebuildBagGrid(shell.Grid, shell.Status, shell.Page, shell.Previous, shell.Next, shell.Tabs.Selected);
		};
		shell.Tabs.Changed += delegate
		{
			_bagPage = 0;
			_bagRefresh();
		};
		shell.Previous.Pressed += delegate
		{
			if (_bagPage > 0)
			{
				_bagPage--;
				_bagRefresh();
			}
		};
		shell.Next.Pressed += delegate
		{
			_bagPage++;
			_bagRefresh();
		};
		_bagRefresh();
		return shell.Panel;
	}

	private void RebuildBagGrid(BagGrid grid, Label status, Label pageLabel, Button previous, Button next, ClassicInventoryTab tab)
	{
		foreach (Node child in grid.GetChildren())
		{
			grid.RemoveChild(child);
			child.QueueFree();
		}
		GameData shared = GameDataProvider.Shared;
		List<BagGridEntry> list = new List<BagGridEntry>();
		Combatant player = _engine.Player;
		long num = CombatWallet.Balance(player);
		if (num > 0 && tab == ClassicInventoryTab.Other)
		{
			ItemStack itemStack = CombatWallet.VirtualStack(player);
			list.Add(new BagGridEntry(itemStack.ItemKey, $"金幣 ×{num:N0}", ItemInstanceText.CompactCount(num), null, Draggable: false, Locked: true));
		}
		foreach (ItemStack inventoryStack in player.InventoryStacks)
		{
			if (ClassicInventoryTabRules.Matches(shared, inventoryStack.ItemKey, tab))
			{
				string text = ItemInstanceText.DisplayName(shared, inventoryStack, _session.Pets);
				if (inventoryStack.IsIdentified)
				{
					text += AttributeScrollText.Suffix(inventoryStack);
				}
				if (inventoryStack.IsIdentified && inventoryStack.BrokenBladeStacks > 0)
				{
					text += $"〔壞刀×{inventoryStack.BrokenBladeStacks}〕";
				}
				ItemStack stackRef = inventoryStack;
				list.Add(new BagGridEntry(inventoryStack.ItemKey, text + $" ×{inventoryStack.Quantity:N0}" + ItemInstanceText.DetailTooltip(shared, inventoryStack), ItemInstanceText.StackCorner(inventoryStack), delegate
				{
					ActivateBagItem(stackRef, _bagRefresh, status);
				}, QuickBar.CanAssign(shared, inventoryStack.ItemKey), inventoryStack.Locked, ItemQualityColors.Highlighted(inventoryStack) ? new Color?(ItemQualityColors.Of(inventoryStack)) : ((Color?)null), ItemQualityColors.Framed(inventoryStack), inventoryStack.IsIdentified && inventoryStack.BrokenBladeStacks > 0, inventoryStack.Uid, ItemQualityColors.Framed(inventoryStack) ? new Color?(ItemQualityColors.FrameOf(inventoryStack)) : ((Color?)null), inventoryStack.IsIdentified ? inventoryStack.Blessing : ItemBlessing.Normal));
			}
		}
		int num2 = 24;
		int num3 = Math.Max(1, (list.Count + num2 - 1) / num2);
		_bagPage = Math.Clamp(_bagPage, 0, num3 - 1);
		int num4 = _bagPage * num2;
		for (int num5 = 0; num5 < num2; num5++)
		{
			int num6 = num4 + num5;
			if (num6 >= list.Count)
			{
				InventoryGridSlot emptySlot = new InventoryGridSlot
				{
					MouseFilter = MouseFilterEnum.Stop
				};
				emptySlot.OnDropToEmpty = delegate(string srcItemKey, string srcStackUid)
				{
					if (string.IsNullOrEmpty(srcStackUid)) return;
					int srcIdx = _engine.Player.InventoryStacks.FindIndex(s => s.Uid == srcStackUid);
					if (srcIdx >= 0)
					{
						ItemStack item = _engine.Player.InventoryStacks[srcIdx];
						_engine.Player.InventoryStacks.RemoveAt(srcIdx);
						_engine.Player.InventoryStacks.Add(item);
						GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.48f);
						SaveManager.Save(_session);
						_bagRefresh?.Invoke();
					}
				};
				grid.AddChild(emptySlot, forceReadableName: false, InternalMode.Disabled);
				continue;
			}
			BagGridEntry bagGridEntry = list[num6];
			string targetUid = bagGridEntry.StackUid;
			InventoryGridSlot inventoryGridSlot = new InventoryGridSlot
			{
				ItemKey = bagGridEntry.ItemKey,
				StackUid = bagGridEntry.StackUid,
				Draggable = bagGridEntry.Draggable,
				Locked = bagGridEntry.Locked,
				Quality = bagGridEntry.Quality,
				QualityFrame = bagGridEntry.QualityFrame,
				FrameQuality = bagGridEntry.FrameQuality,
				BlessingState = bagGridEntry.BlessingState,
				BrokenBlade = bagGridEntry.BrokenBlade,
				TooltipText = bagGridEntry.Tooltip,
				OnActivate = bagGridEntry.Activate
			};
			if (!string.IsNullOrEmpty(targetUid))
			{
				inventoryGridSlot.OnDropSwap = delegate(string srcItemKey, string srcStackUid)
				{
					if (string.IsNullOrEmpty(srcStackUid) || string.IsNullOrEmpty(targetUid) || srcStackUid == targetUid) return;
					int srcIdx = _engine.Player.InventoryStacks.FindIndex(s => s.Uid == srcStackUid);
					int tgtIdx = _engine.Player.InventoryStacks.FindIndex(s => s.Uid == targetUid);
					if (srcIdx >= 0 && tgtIdx >= 0 && srcIdx != tgtIdx)
					{
						ItemStack temp = _engine.Player.InventoryStacks[srcIdx];
						_engine.Player.InventoryStacks[srcIdx] = _engine.Player.InventoryStacks[tgtIdx];
						_engine.Player.InventoryStacks[tgtIdx] = temp;
						GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.48f);
						SaveManager.Save(_session);
						_bagRefresh?.Invoke();
					}
				};
			}
			inventoryGridSlot.SetIcon(ItemIcons.For(bagGridEntry.ItemKey));
			inventoryGridSlot.SetCorner(bagGridEntry.Corner);
			if (QuickBar.CanAutoUse(GameDataProvider.Shared, bagGridEntry.ItemKey))
			{
				string autoKey = bagGridEntry.ItemKey;
				AttachAutoBadge(inventoryGridSlot, _session.AutoUseItems.Contains(autoKey), AutoUseHint(GameDataProvider.Shared, autoKey), delegate(bool on)
				{
					if (on)
					{
						_session.AutoUseItems.Add(autoKey);
					}
					else
					{
						_session.AutoUseItems.Remove(autoKey);
					}
					SaveManager.Save(_session);
				});
			}
			grid.AddChild(inventoryGridSlot, forceReadableName: false, InternalMode.Disabled);
		}
		pageLabel.Text = $"{_bagPage + 1} / {num3}";
		previous.Disabled = _bagPage == 0;
		next.Disabled = _bagPage >= num3 - 1;
	}

	private void UseReturnScroll(Label? status = null, string? stackUid = null)
	{
		if (!_mapRule.UsableItem || !_mapRule.Escapable)
		{
			if (status != null)
			{
				SetBagStatus(status, "無法使用：此地圖無法使用返回卷軸", "#e2938f");
			}
			else
			{
				SlabLog("[color=#e2938f]此地圖無法使用返回卷軸[/color]");
			}
			return;
		}
		L1jGetbackRoute l1jGetbackRoute = ResolveCurrentGetback();
		if (!l1jGetbackRoute.Destination.IsRuntimeResolved)
		{
			throw new InvalidDataException($"L1J getback destination {l1jGetbackRoute.Destination.MapId}:{l1jGetbackRoute.Destination.GameX},{l1jGetbackRoute.Destination.GameY} has no runtime map.");
		}
		ItemStack itemStack = (string.IsNullOrWhiteSpace(stackUid) ? ItemActivation.FindFirstInventoryStack(GameDataProvider.Shared, _engine.Player, ItemAction.ReturnScroll) : _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == stackUid && !stack.Locked && ItemActivation.Classify(GameDataProvider.Shared, _engine.Player, stack) == ItemAction.ReturnScroll));
		if (itemStack == null || !ItemStackInventory.TryRemoveByUid(_engine.Player.InventoryStacks, itemStack.Uid, 1L, out ItemStack _))
		{
			if (status != null)
			{
				SetBagStatus(status, "無法使用：沒有返回卷軸（雜貨商有賣）", "#e2938f");
			}
			else
			{
				SlabLog("[color=#e2938f]沒有返回卷軸（雜貨商有賣）[/color]");
			}
		}
		else
		{
			SlabLog("[color=#7fd0ff]使用返回卷軸[/color]");
			ApplyGetbackTransfer(l1jGetbackRoute);
		}
	}

	private void ActivateBagItem(ItemStack stack, Action refreshBag, Label status)
	{
		if (!_mapRule.UsableItem)
		{
			SetBagStatus(status, "無法使用：此地圖禁止使用道具", "#e2938f");
			return;
		}
		switch (ItemActivation.Classify(GameDataProvider.Shared, _engine.Player, stack))
		{
		case ItemAction.PetCollar:
		{
			PetCollarResult petCollarResult = PetCollarRules.Toggle(GameDataProvider.Shared, _session.Pets, _engine.Player, stack.Uid, _engine.Engine.ActiveSummonPetCostOf(_engine.Player));
			if (!petCollarResult.Success)
			{
				SetBagStatus(status, "無法使用：" + ItemActivation.PetCollarFailureText(petCollarResult.Failure), "#e2938f");
				break;
			}
			PetInstance pet = petCollarResult.Pet;
			List<PetInstance> list = _session.Pets.ActiveFor(_engine.Player).ToList();
			int index = Math.Max(0, list.IndexOf(pet));
			_engine.Engine.DeployPet(GameDataProvider.Shared, _engine.Player, pet, index, list.Count);
			SaveManager.Save(_session);
			refreshBag();
			SetBagStatus(status, "✓ 已使用寵物哨子召喚 " + pet.DisplayName, "#8fdd8f");
			break;
		}
		case ItemAction.PetEgg:
		{
			PetAcquisitionResult petAcquisitionResult = PetAcquisitionRules.TryHatchEgg(GameDataProvider.Shared, _session.Pets, _engine.Player, stack.Uid);
			if (!petAcquisitionResult.Success)
			{
				SetBagStatus(status, "無法使用：" + PetAcquisitionText.FailText(petAcquisitionResult.Failure), "#e2938f");
				break;
			}
			SaveManager.Save(_session);
			refreshBag();
			SetBagStatus(status, "✓ 孵化 " + petAcquisitionResult.PetForm + "，已取得專屬項圈", "#8fdd8f");
			break;
		}
		case ItemAction.PetTamingFood:
			BeginPetTamingTargeting(stack.Uid, status);
			break;
		case ItemAction.PetEvolutionFruit:
			BeginPetEvolutionTargeting(stack.Uid, status);
			break;
		case ItemAction.PetWhistle:
		{
			int num = _engine.Engine.CallActivePets(_engine.Player);
			SetBagStatus(status, (num > 0) ? $"✓ 已召回 {num} 隻寵物" : "目前沒有出戰中的寵物", (num > 0) ? "#8fdd8f" : "#8b95a6");
			break;
		}
		case ItemAction.MonsterCard:
		{
			MonsterCardToggleResult result = MonsterCardPartyRules.Toggle(GameDataProvider.Shared, _session.Party, _engine.Player, stack, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _engine.Engine);
			if (!result.Success)
			{
				SetBagStatus(status, MonsterCardPartyRules.FailureText(result), "#e2938f");
				break;
			}
			string text7 = GameDataProvider.Shared.Mob(result.MobKey)?["n"]?.GetValue<string>() ?? result.MobKey;
			SaveManager.Save(_session);
			refreshBag();
			SetBagStatus(status, result.Joined ? ("✓ " + text7 + " 出戰") : ("✓ " + text7 + " 已收回卡片；冷卻 5 分鐘"), "#8fdd8f");
			break;
		}
		case ItemAction.Painwand:
		{
			PainwandUseResult painwandUseResult = PainwandRules.TryUse(GameDataProvider.Shared, _engine.Player, stack.Uid, _mapRule.Painwand, new SeededCombatRandom((int)Time.GetTicksUsec()));
			if (!painwandUseResult.Success)
			{
				SetBagStatus(status, "無法使用：" + ItemActivation.PainwandFailureText(painwandUseResult.Failure), "#e2938f");
				break;
			}
			Combatant combatant = _engine.SpawnPainwandMob(painwandUseResult.MobKey);
			SaveManager.Save(_session);
			refreshBag();
			SetBagStatus(status, $"✓ 召喚 {combatant.Disp}（60 秒）・剩餘 {painwandUseResult.RemainingCharges} 次", "#8fdd8f");
			break;
		}
		case ItemAction.Elixir:
			var (flag12, text14) = ItemActivation.UseElixir(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flag12 ? ("✓ " + text14) : ("無法使用：" + text14), flag12 ? "#8fdd8f" : "#e2938f");
			if (flag12)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		case ItemAction.Consumable:
			DoUse(stack.Uid, refreshBag, status);
			break;
		case ItemAction.UncurseScroll:
			var (flag8, text8) = ItemActivation.UseUncurseScroll(_engine.Player, stack);
			if (flag8)
			{
				CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
			}
			SetBagStatus(status, flag8 ? ("✓ " + text8) : ("無法使用：" + text8), flag8 ? "#8fdd8f" : "#e2938f");
			if (flag8)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		case ItemAction.IdentifyScroll:
			OpenHuntIdentifyScrollPanel(stack.Uid);
			break;
		case ItemAction.SoulOrbContainer:
			var (flag13, text15) = ItemActivation.UseSoulOrbContainer(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flag13 ? ("✓ " + text15) : ("無法使用：" + text15), flag13 ? "#8fdd8f" : "#e2938f");
			if (flag13)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.RoiBag:
			var (flag9, text9) = ItemActivation.UseRoiBag(GameDataProvider.Shared, _engine.Player, stack, _potionRng);
			SetBagStatus(status, flag9 ? ("✓ " + text9) : ("無法開啟：" + text9), flag9 ? "#8fdd8f" : "#e2938f");
			if (flag9)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		case ItemAction.IvoryQuiver:
			var (flag15, text16) = ItemActivation.UseIvoryQuiver(GameDataProvider.Shared, _engine.Player, stack, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			SetBagStatus(status, flag15 ? ("✓ " + text16) : ("無法使用：" + text16), flag15 ? "#8fdd8f" : "#e2938f");
			if (flag15)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		case ItemAction.Cooking:
			var (flag3, text3) = ItemActivation.UseCooking(GameDataProvider.Shared, _engine.Player, stack);
			if (flag3)
			{
				CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
			}
			SetBagStatus(status, flag3 ? ("✓ " + text3) : ("無法食用：" + text3), flag3 ? "#8fdd8f" : "#e2938f");
			if (flag3)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		case ItemAction.ReturnScroll:
			UseReturnScroll(status, stack.Uid);
			break;
		case ItemAction.TeleportScroll:
			UseTeleportScroll(status, stack);
			break;
		case ItemAction.PolymorphScroll:
			UsePolymorphScroll(stack, refreshBag, status);
			break;
		case ItemAction.OrcEmissaryPolymorph:
			var (flag11, text13) = ItemActivation.UseOrcEmissaryPolymorph(GameDataProvider.Shared, _engine.Player, stack.Uid);
			SetBagStatus(status, flag11 ? ("✓ " + text13) : ("無法使用：" + text13), flag11 ? "#8fdd8f" : "#e2938f");
			if (flag11)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
				SendLocalAction("morph", _engine.Player.PolymorphForm ?? "");
			}
			break;
		case ItemAction.DarkEntBark:
			BeginDarkEntBarkTargeting(stack.Uid, status);
			break;
		case ItemAction.MainTargetItem:
			OpenHuntMainTargetItemPanel(stack.Uid);
			break;
		case ItemAction.Equip:
			DoEquip(stack.Uid, refreshBag, status);
			break;
		case ItemAction.MainLight:
			var (flag4, text4) = ItemActivation.UseMainLight(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flag4 ? ("✓ " + text4) : ("無法使用：" + text4), flag4 ? "#8fdd8f" : "#e2938f");
			if (flag4)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.MainWand:
		{
			bool flag14 = _engine.Engine.TryUseL1jLightningWand(_engine.Player, stack.Uid);
			SetBagStatus(status, flag14 ? "✓ 使用閃電魔杖" : "無法使用：附近沒有目標", flag14 ? "#8fdd8f" : "#e2938f");
			if (flag14)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		}
		case ItemAction.LampOil:
			var (flag10, text10) = ItemActivation.UseLampOil(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flag10 ? ("✓ " + text10) : ("無法使用：" + text10), flag10 ? "#8fdd8f" : "#e2938f");
			if (flag10)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.LearnSkill:
			var (flag6, text6) = ItemActivation.UseSkillBook(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flag6 ? ("✓ " + text6) : ("無法學習：" + text6), flag6 ? "#8fdd8f" : "#e2938f");
			if (flag6)
			{
				SaveManager.Save(_session);
				refreshBag();
				RebuildQuickBar();
			}
			break;
		case ItemAction.Resolvent:
			OpenHuntResolventPanel(stack.Uid);
			break;
		case ItemAction.AttributeScroll:
			OpenHuntAttributeScrollPanel(stack.Uid);
			break;
		case ItemAction.EnchantWeapon:
			OpenHuntEnchantScrollPanel(stack.Uid, isWeapon: true);
			break;
		case ItemAction.EnchantArmor:
			OpenHuntEnchantScrollPanel(stack.Uid, isWeapon: false);
			break;
		case ItemAction.SealScroll:
			OpenHuntSealScrollPanel(stack.Uid, sealing: true);
			break;
		case ItemAction.UnsealScroll:
			OpenHuntSealScrollPanel(stack.Uid, sealing: false);
			break;
		case ItemAction.RespecCandle:
			var (flag2, text2) = ItemActivation.UseRespecCandle(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flag2 ? ("✓ " + text2) : ("無法使用：" + text2), flag2 ? "#8fdd8f" : "#e2938f");
			if (flag2)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		case ItemAction.MagicDollContainer:
		{
			string text11 = MagicDollRules.RollBagReward(GameDataProvider.Shared, new SeededCombatRandom((int)Time.GetTicksUsec()));
			if (!CombatInventory.TryRemove(_engine.Player, stack.ItemKey, 1L))
			{
				SetBagStatus(status, "袋子不見了？", "#c9b06a");
				break;
			}
			CombatInventory.Add(_engine.Player, text11, 1L);
			SaveManager.Save(_session);
			string text12 = GameDataProvider.Shared.Item(text11)?["n"]?.GetValue<string>() ?? text11;
			SetBagStatus(status, "✓ 從袋子裡出現了「" + text12 + "」", "#9be89b");
			refreshBag();
			break;
		}
		case ItemAction.MagicDollSummon:
		{
			bool flag7 = (object)_engine.Engine.ActiveMagicDollOf(_engine.Player) != null;
			if (_engine.Engine.TryUseMagicDoll(_engine.Player, stack.Uid))
			{
				SyncActiveDollToSession();
				SaveManager.Save(_session);
				SetBagStatus(status, flag7 ? "✓ 魔法娃娃回到了它的物品裡" : "✓ 魔法娃娃出現了（30 分鐘）", "#9be89b");
				refreshBag();
				RefreshHud();
			}
			else
			{
				SetBagStatus(status, flag7 ? "已經有一隻魔法娃娃在場（一次最多一隻）" : $"需要 魔法結晶體 ×{50}", "#c9b06a");
			}
			break;
		}
		case ItemAction.PurifyStone:
			var (flag5, text5) = ItemActivation.UsePurifyStone(GameDataProvider.Shared, _engine.Player, stack, new SeededCombatRandom((int)Time.GetTicksUsec()));
			SetBagStatus(status, flag5 ? ("✓ " + text5) : text5, flag5 ? "#9be89b" : "#c9b06a");
			if (flag5)
			{
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		case ItemAction.ReviveScroll:
			BeginReviveTargeting(status);
			break;
		case ItemAction.PrideTravel:
			UsePrideTravel(stack, status);
			break;
		case ItemAction.PrideUnseal:
			var (flag, text) = ItemActivation.UsePrideUnseal(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flag ? ("✓ " + text) : ("無法使用：" + text), flag ? "#8fdd8f" : "#e2938f");
			if (flag)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.MagicScroll:
		{
			var info = ItemActivation.ResolveMagicScrollInfo(GameDataProvider.Shared, stack);
			if (info.IsAttack)
			{
				if (!_mapRule.UsableSkill || !_mapRule.UsableItem)
				{
					SetBagStatus(status, "無法使用：此地圖無法使用卷軸攻擊", "#e2938f");
					break;
				}
				if (IsMagicScrollOnCooldown)
				{
					SetBagStatus(status, $"無法使用：魔法卷軸冷卻中（剩餘 {MagicScrollCooldownRemaining:0.0} 秒）", "#e2938f");
					break;
				}
				Combatant? currentTarget = _engine.Engine.GetManualTarget(_engine.Player);
				if (currentTarget != null && currentTarget.IsAlive && _engine.Engine.Combatants.Contains(currentTarget))
				{
					if (QueuePlayerManualSkill(info.SkillKey, currentTarget, isScroll: true))
					{
						_lastMagicScrollAt = _potionClock;
						CombatInventory.TryRemove(_engine.Player, stack.ItemKey, 1L);
						SaveManager.Save(_session);
						refreshBag();
						RefreshHud();
						SetBagStatus(status, $"✓ 施放 {info.SpellName}！", "#8fdd8f");
					}
					else
					{
						SetBagStatus(status, $"無法使用：{info.SpellName} 目前無法施放", "#e2938f");
					}
				}
				else
				{
					BeginManualSkillTargeting(info.SkillKey, stack);
					SetBagStatus(status, $"{info.SpellName}：請點選畫面上的怪物目標", "#7fd0ff");
				}
				break;
			}
			if (IsMagicScrollOnCooldown)
			{
				SetBagStatus(status, $"無法使用：魔法卷軸冷卻中（剩餘 {MagicScrollCooldownRemaining:0.0} 秒）", "#e2938f");
				break;
			}
			var (flagScroll, textScroll) = ItemActivation.UseMagicScroll(GameDataProvider.Shared, _engine.Player, stack);
			SetBagStatus(status, flagScroll ? ("✓ " + textScroll) : ("無法使用：" + textScroll), flagScroll ? "#8fdd8f" : "#e2938f");
			if (flagScroll)
			{
				_lastMagicScrollAt = _potionClock;
				if (!string.IsNullOrEmpty(info.SkillKey))
				{
					GameAudio.Instance?.PlaySkillCast(info.SkillKey);
					_spellFx.Play(info.SpellName, _engine.Player, _engine.Player, info.SkillKey);
				}
				SaveManager.Save(_session);
				refreshBag();
				RefreshHud();
			}
			break;
		}
		default:
			SetBagStatus(status, "這個物品沒有可以直接使用的效果", "#8b95a6");
			break;
		}
	}

	private void UsePrideTravel(ItemStack stack, Label status)
	{
		if (!TowerOfInsolenceCatalog.TryResolveTravelItem(GameDataProvider.Shared, stack.ItemKey, out var travel))
		{
			SetBagStatus(status, "這個物品沒有可以直接使用的效果", "#8b95a6");
			return;
		}
		if (!_mapRule.Escapable)
		{
			SetBagStatus(status, "無法使用：此地圖無法傳送離開", "#e2938f");
			return;
		}
		if (travel.Kind == TowerTravelItemKind.TeleportScroll && !CombatInventory.TryRemove(_engine.Player, stack.ItemKey, 1L))
		{
			SetBagStatus(status, "無法使用：背包中已沒有這張傳送卷軸", "#e2938f");
			return;
		}
		SlabLog($"[color=#7fd0ff]傳送至 傲慢之塔{travel.FloorNumber}F[/color]");
		_session.SuppressPetDeploymentOnce = false;
		_session.HuntMap = travel.DestinationMapKey;
		_session.PendingMapEntryLandmark = travel.ArrivalLandmarkId;
		SaveManager.Save(_session);
		_pendingScreenTransition = ChangeHuntMap;
	}

	private void BeginReviveTargeting(Label status)
	{
		CancelCharmTargeting(silent: true);
		CancelManualSkillTargeting(silent: true);
		if (!_mapRule.Resurrection)
		{
			SetBagStatus(status, "此地圖無法復活", "#e2938f");
			return;
		}
		if (!_engine.Combatants.Any(delegate(Combatant actor)
		{
			bool flag = actor.Dead;
			if (flag)
			{
				CombatantKind kind = actor.Kind;
				bool flag2 = (uint)(kind - 2) <= 1u;
				flag = flag2;
			}
			return flag;
		}))
		{
			SetBagStatus(status, "目前沒有倒下的傭兵或夥伴", "#8b95a6");
			return;
		}
		_reviveTargeting = true;
		SetBagStatus(status, "點選要復活的目標（點空地取消·不消耗卷軸）", "#7fd0ff");
		SlabLog("[color=#7fd0ff]復活卷軸：點選死亡的傭兵或夥伴（點空地取消）[/color]");
	}

	private bool HandleReviveTargetClick(Vector2 world)
	{
		if (!_reviveTargeting)
		{
			return false;
		}
		_reviveTargeting = false;
		Combatant combatant = null;
		float num = 56f;
		foreach (Combatant combatant2 in _engine.Combatants)
		{
			bool flag = !combatant2.Dead;
			if (!flag)
			{
				CombatantKind kind = combatant2.Kind;
				bool flag2 = (uint)(kind - 2) <= 1u;
				flag = !flag2;
			}
			if (!flag)
			{
				float num2 = world.DistanceTo(ToVec(combatant2.Pos));
				if (!(num2 >= num))
				{
					num = num2;
					combatant = combatant2;
				}
			}
		}
		if (combatant == null)
		{
			SlabLog("[color=#8b95a6]復活卷軸：已取消（未消耗）[/color]");
			return true;
		}
		if (!((combatant.Kind == CombatantKind.Ally) ? _engine.Engine.TryReviveAllyWithScroll(combatant) : _engine.Engine.TryRevivePetWithScroll(combatant)))
		{
			SlabLog("[color=#e2938f]復活失敗——背包裡沒有復活卷軸[/color]");
			return true;
		}
		Float(ToVec(combatant.Pos), "復活", Color.FromHtml("#8fdd8f".AsSpan()), big: true);
		SlabLog("[color=#8fdd8f]" + combatant.Disp + " 復活了（消耗 復活卷軸 ×1）[/color]");
		_bagRefresh?.Invoke();
		return true;
	}

	private void DoEquip(string uid, Action refreshBag, Label status)
	{
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == uid);
		string itemKey = itemStack?.ItemKey ?? "";
		EquipmentEligibilityResult equipmentEligibilityResult = ((itemStack == null) ? EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.MissingItemDefinition, "") : EquipmentRules.Evaluate(GameDataProvider.Shared, _engine.Player, itemStack));
		var (flag, text) = ItemActivation.Equip(GameDataProvider.Shared, _engine.Player, uid, _potionRng);
		SetBagStatus(status, flag ? ("✓ " + text) : text, flag ? "#8fdd8f" : "#e2938f");
		if (flag)
		{
			if (_engine.Player.EquippedItems.TryGetValue(equipmentEligibilityResult.Slot, out ItemStack value))
			{
				QuickBar.RemapEquipmentAssignment(_session.QuickItems, uid, value.Uid, itemKey);
			}
			GameAudio.Instance?.PlayEquipment(itemKey, _engine.Player.ClassId);
			SaveManager.Save(_session);
			refreshBag();
			RebuildQuickBar();
			SyncEquipmentAndBagUi();
		}
	}

	private void DoUnequip(string slot, Action refreshBag, Label status)
	{
		_engine.Player.EquippedItems.TryGetValue(slot, out ItemStack value);
		ItemStack before = value?.Copy();
		var (flag, text) = ItemActivation.Unequip(GameDataProvider.Shared, _engine.Player, slot, _potionRng);
		SetBagStatus(status, flag ? ("✓ " + text) : text, flag ? "#8fdd8f" : "#e2938f");
		if (!flag)
		{
			return;
		}
		if (before != null)
		{
			ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == before.Uid) ?? _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => ItemStackInventory.CanStack(item, before));
			if (itemStack != null)
			{
				QuickBar.RemapEquipmentAssignment(_session.QuickItems, before.Uid, itemStack.Uid, before.ItemKey);
			}
		}
		SaveManager.Save(_session);
		refreshBag();
		RebuildQuickBar();
		SyncEquipmentAndBagUi();
	}

	private void DoUse(string uid, Action refreshBag, Label status)
	{
		string itemKey = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack s) => s.Uid == uid)?.ItemKey ?? "";
		var (flag, text) = ItemActivation.UseConsumable(GameDataProvider.Shared, _engine.Player, uid, _potionRng);
		if (!flag)
		{
			SetBagStatus(status, "無法使用：" + text, "#e2938f");
			return;
		}
		PlayPotionFlash(itemKey);
		SetBagStatus(status, "✓ " + text, "#8fdd8f");
		RefreshHud();
		refreshBag();
	}

	private void PlayPotionFlash(string itemKey)
	{
		PotionFlashFx.TryPlay(_world, itemKey, () => _engine.RenderPos(_engine.Player));
	}

	private void SetBagStatus(Label l, string text, string hex)
	{
		l.Text = text;
		l.TooltipText = text;
		l.AddThemeColorOverride("font_color", Color.FromHtml(hex.AsSpan()));
		if (!string.IsNullOrWhiteSpace(text))
		{
			SlabLog($"[color={hex}]{text}[/color]");
			if (hex == "#e2938f" || text.StartsWith("無法") || text.StartsWith("找不到") || text.StartsWith("需要") || text.StartsWith("此地圖") || text.Contains("失敗") || text.Contains("禁止"))
			{
				try
				{
					Float(PlayerPos(), text, Color.FromHtml(hex.AsSpan()), big: false);
				}
				catch
				{
				}
			}
		}
	}

	private static Label MakeRow(string text, string hex)
	{
		Label label = new Label();
		label.Text = text;
		label.AddThemeColorOverride("font_color", Color.FromHtml(hex.AsSpan()));
		return label;
	}

	private void BeginDarkEntBarkTargeting(string barkUid, Label status)
	{
		CancelCharmTargeting(silent: true);
		CancelManualSkillTargeting(silent: true);
		_darkEntBarkUid = barkUid;
		SetBagStatus(status, "點選自己、角色或怪物施放隨機變形（點空地／右鍵取消）", "#7fd0ff");
		SlabLog("[color=#7fd0ff]黑暗安特的樹皮：點選自己、角色或怪物施放隨機變形（點空地／右鍵取消）[/color]");
	}

	private void CancelDarkEntBarkTargeting()
	{
		_darkEntBarkUid = "";
		SlabLog("[color=#8b95a6]黑暗安特的樹皮：已取消（未消耗）[/color]");
	}

	private bool HandleDarkEntBarkTargetClick(Vector2 world)
	{
		if (!DarkEntBarkTargeting)
		{
			return false;
		}
		string darkEntBarkUid = _darkEntBarkUid;
		_darkEntBarkUid = "";
		Combatant combatant = null;
		float num = 56f;
		foreach (Combatant combatant2 in _engine.Combatants)
		{
			bool flag = !combatant2.IsAlive;
			if (!flag)
			{
				CombatantKind kind = combatant2.Kind;
				bool flag2 = (uint)kind <= 2u;
				flag = !flag2;
			}
			if (!flag)
			{
				float num2 = world.DistanceTo(ToVec(combatant2.Pos));
				if (!(num2 >= num))
				{
					num = num2;
					combatant = combatant2;
				}
			}
		}
		if (combatant == null)
		{
			SlabLog("[color=#8b95a6]黑暗安特的樹皮：已取消（未選目標、未消耗）[/color]");
			return true;
		}
		DarkEntBarkResult darkEntBarkResult = L1jTargetItemUseRules.TryUseDarkEntBark(GameDataProvider.Shared, _engine.Player, combatant, darkEntBarkUid, _potionRng);
		if (!darkEntBarkResult.Attempted)
		{
			SlabLog("[color=#e2938f]無法使用：" + ItemActivation.DarkEntBarkFailureText(darkEntBarkResult.Failure) + "[/color]");
			return true;
		}
		SaveManager.Save(_session);
		_bagRefresh?.Invoke();
		RefreshHud();
		if (darkEntBarkResult.Transformed)
		{
			SlabLog($"[color=#8fdd8f]{combatant.Disp} 變形為 {darkEntBarkResult.FormName}（樹皮已消耗）[/color]");
		}
		else
		{
			SlabLog("[color=#e2938f]" + combatant.Disp + " 抵抗或不符合變形條件（樹皮已消耗）[/color]");
		}
		return true;
	}

	private void OpenConsulDialog()
	{
		CloseConsulDialog();
		string[] lines = new string[3] { "……能走到這座大廳，你身上已經有炎魔大人的氣息了。", "這座大廳之外的四個領域，不過是通往謁見所的門檻罷了。", "既然你走到了這裡，我便為你開啟通往炎魔謁見所的道路。" };
		ClassicNpcDialogHandle classicNpcDialogHandle = ClassicNpcDialogWindow.Create(WorldView, "執政官：", lines, CloseConsulDialog, 1950);
		classicNpcDialogHandle.AddOption("前往炎魔謁見所", delegate
		{
			CloseConsulDialog();
			SlabLog("[color=#ff9a5a]執政官撕開空間——炎魔謁見所的熱風撲面而來……[/color]");
			_session.TownKey = "town_flame_audience";
			_pendingScreenTransition = ReturnToTown;
		});
		AddAboveBarPanel(classicNpcDialogHandle.Root);
		_consulDialog = classicNpcDialogHandle.Root;
	}

	private void CloseConsulDialog()
	{
		if (_consulDialog != null && GodotObject.IsInstanceValid(_consulDialog))
		{
			_consulDialog.QueueFree();
		}
		_consulDialog = null;
	}

	private void LoadDoors(MapTopology topology)
	{
		_pendingDoors.Clear();
		_doorSprites = null;
		string path = DataFileSystem.Combine(DataFileSystem.FullPath("res://data"), "l1j-doors.json");
		if (!DataFileSystem.Exists(path))
		{
			return;
		}
		JsonObject jsonObject;
		try
		{
			jsonObject = JsonNode.Parse(DataFileSystem.ReadAllText(path))?.AsObject();
		}
		catch (JsonException ex)
		{
			GD.PushWarning("[Doors] l1j-doors.json 壞了：" + ex.Message);
			return;
		}
		if (!(jsonObject?["doors"] is JsonArray jsonArray))
		{
			return;
		}
		_doorSprites = jsonObject["sprites"] as JsonObject;
		foreach (JsonNode item3 in jsonArray)
		{
			if (item3 is JsonObject jsonObject2 && !(jsonObject2["map"]?.GetValue<string>() != _mapKey) && jsonObject2["cell"] is JsonArray { Count: 2 } jsonArray2)
			{
				int value = jsonArray2[0].GetValue<int>();
				int value2 = jsonArray2[1].GetValue<int>();
				if (topology.ContainsLocalCell(value, value2))
				{
					var (item, item2) = topology.DisplayPixelCenter(value, value2);
					_pendingDoors.Add((item, item2, jsonObject2));
				}
			}
		}
	}

	private void DoorStep(double delta)
	{
		if (_pendingDoors.Count == 0 || _engine == null)
		{
			return;
		}
		_doorMaterialiseCd -= delta;
		if (_doorMaterialiseCd > 0.0)
		{
			return;
		}
		_doorMaterialiseCd = 0.25;
		WorldPoint pos = _engine.Player.Pos;
		double num = 1960000.0;
		for (int num2 = _pendingDoors.Count - 1; num2 >= 0; num2--)
		{
			(double X, double Y, JsonObject Door) tuple = _pendingDoors[num2];
			double item = tuple.X;
			double item2 = tuple.Y;
			JsonObject item3 = tuple.Door;
			double num3 = item - pos.X;
			double num4 = item2 - pos.Y;
			if (!(num3 * num3 + num4 * num4 > num))
			{
				_pendingDoors.RemoveAt(num2);
				BuildDoorVisual(item, item2, item3);
			}
		}
	}

	private void BuildDoorVisual(double worldX, double worldY, JsonObject door)
	{
		int num = door["gfx"]?.GetValue<int>() ?? 0;
		if (num <= 0 || !(_doorSprites?[num.ToString()] is JsonObject jsonObject))
		{
			return;
		}
		string name = $"door_{num}";
		if (!_atlas.HasAtlas("anim", name))
		{
			return;
		}
		SpriteFrames spriteFrames = _atlas.BuildFrames("anim", name);
		if (spriteFrames == null || !spriteFrames.HasAnimation("frame"))
		{
			return;
		}
		int frameCount = spriteFrames.GetFrameCount("frame");
		int num2 = jsonObject["openFrame"]?.GetValue<int>() ?? 0;
		if (num2 >= frameCount)
		{
			num2 = 0;
		}
		CastleWarDefinition castleWarDefinition = null;
		int cellX = door["cell"]?[0]?.GetValue<int>() ?? (-1);
		int cellY = door["cell"]?[1]?.GetValue<int>() ?? (-1);
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		if (active != null)
		{
			CastleWarDefinition castleWarDefinition2 = CastleWarRules.Find(active.CastleId);
			if ((object)castleWarDefinition2 != null && castleWarDefinition2.Contains(_mapKey, cellX, cellY))
			{
				castleWarDefinition = castleWarDefinition2;
			}
		}
		int num3 = door["hp"]?.GetValue<int>() ?? 0;
		int idx = (((object)castleWarDefinition == null || num3 <= 0) ? num2 : ((num2 == 0) ? Math.Max(0, frameCount - 1) : 0));
		string text = CastleWarRules.DoorKey(door["id"]?.GetValue<int>() ?? 0, cellX, cellY);
		bool flag = (object)castleWarDefinition != null && num3 > 0 && CastleWarStore.Book.IsDestroyed(text);
		Texture2D frameTexture = spriteFrames.GetFrameTexture("frame", idx);
		if (frameTexture == null)
		{
			return;
		}
		float num4 = (float)(jsonObject["latticeOrigin"]?["x"]?.GetValue<double>()).GetValueOrDefault();
		float num5 = (float)(jsonObject["latticeOrigin"]?["y"]?.GetValue<double>()).GetValueOrDefault();
		Vector2 position = new Vector2((float)worldX, (float)worldY);
		Sprite2D sprite2D = new Sprite2D
		{
			Texture = frameTexture,
			Centered = false,
			Offset = new Vector2(0f - num4, 0f - num5),
			Position = position,
			TextureFilter = TextureFilterEnum.Nearest,
			ZIndex = Depth.Of(position.Y)
		};
		_arena.AddChild(sprite2D, forceReadableName: false, InternalMode.Disabled);
		if ((object)castleWarDefinition != null && num3 > 0)
		{
			Texture2D frameTexture2 = spriteFrames.GetFrameTexture("frame", num2);
			if (frameTexture2 != null)
			{
				_castleWarDoorRepairs.Add((sprite2D, frameTexture2));
			}
			if (flag)
			{
				sprite2D.Visible = false;
				return;
			}
			Combatant combatant = CastleWarRules.CreateStructure(text, castleWarDefinition.Name + "城門", $"door:{num}", CastleWarObjectKind.Gate, castleWarDefinition.Id, num3, new WorldPoint(worldX, worldY), ++_worldNpcBornSeq);
			combatant.Hp = CastleWarStore.Book.RestoreHealth(text, combatant.MaxHp);
			_engine.Engine.Add(combatant);
			_castleWarDoorVisuals[combatant] = sprite2D;
		}
	}

	private void LoadL1jDungeonRandom(MapTopology topology)
	{
		_l1jDungeonRandom = L1jDungeonRandomCatalog.Load(GameDataProvider.Shared);
		_lastDungeonRandomPlayerCell = (topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY) ? new(int, int)?((localX, localY)) : (((int, int)?)null));
	}

	private bool L1jDungeonRandomStep()
	{
		if (_l1jDungeonRandom != null && _topology != null && _pendingScreenTransition == null && !_engine.Player.Dead && _topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY))
		{
			(int, int)? lastDungeonRandomPlayerCell = _lastDungeonRandomPlayerCell;
			int num = localX;
			int num2 = localY;
			bool hasValue = lastDungeonRandomPlayerCell.HasValue;
			if (!hasValue)
			{
				goto IL_00bc;
			}
			if (hasValue)
			{
				(int, int) valueOrDefault = lastDungeonRandomPlayerCell.GetValueOrDefault();
				if (valueOrDefault.Item1 != num || valueOrDefault.Item2 != num2)
				{
					goto IL_00bc;
				}
			}
		}
		return false;
		IL_00bc:
		_lastDungeonRandomPlayerCell = (localX, localY);
		var (gameX, gameY) = _topology.ToGameCell(localX, localY);
		if (!_l1jDungeonRandom.TryChoose(_mapRule.MapId, gameX, gameY, _rng.Next(5), out var destination, out var heading))
		{
			return false;
		}
		string text = ResolveDungeonRandomMapKey(destination);
		MapTopology mapTopology = ((text == null) ? null : TryLoadTopology(text));
		if (mapTopology == null || !mapTopology.ContainsGameCell(destination.GameX, destination.GameY))
		{
			SlabLog("[color=#e2938f]隨機地城傳送失敗 — 原版目的地地圖尚未建立[/color]");
			return false;
		}
		var (localX2, localY2) = mapTopology.ToLocalCell(destination.GameX, destination.GameY);
		var (num3, num4) = mapTopology.DisplayPixelCenter(localX2, localY2);
		_engine.Engine.ApplyBuff(_engine.Player, "sk_abs_barrier", _l1jDungeonRandom.BarrierDurationSeconds);
		_engine.Player.Facing8 = heading;
		_engine.StopPlayer();
		if (string.Equals(text, _mapKey, StringComparison.Ordinal))
		{
			RelocatePlayerGroup(new WorldPoint(num3, num4));
		}
		else
		{
			_session.HuntMap = text;
			_session.PendingHuntSpawn = (num3, num4);
			SaveManager.Save(_session);
			_pendingScreenTransition = ChangeHuntMap;
		}
		return true;
	}

	private string? ResolveDungeonRandomMapKey(L1jDungeonRandomCell destination)
	{
		if (destination.MapId == _mapRule.MapId && _topology != null && _topology.ContainsGameCell(destination.GameX, destination.GameY))
		{
			return _mapKey;
		}
		if (!L1jMapRuleCatalog.Load(GameDataProvider.Shared).TryForMapId(destination.MapId, out L1jMapRule rule) || (object)rule == null)
		{
			return null;
		}
		foreach (string mapKey in rule.MapKeys)
		{
			MapTopology? mapTopology = TryLoadTopology(mapKey);
			if (mapTopology != null && mapTopology.ContainsGameCell(destination.GameX, destination.GameY))
			{
				return mapKey;
			}
		}
		return null;
	}

	private static MapTopology? TryLoadTopology(string mapKey)
	{
		if (string.IsNullOrWhiteSpace(mapKey))
		{
			return null;
		}
		GodotDataFiles.EnsureInstalled();
		string text = "res://assets/maps/" + mapKey;
		if (!DataFileSystem.Exists(text + "/map.json") && !Godot.FileAccess.FileExists(text + "/map.json"))
		{
			return null;
		}
		try
		{
			MapTopology mapTopology = MapTopology.Load(text);
			if (!string.Equals(mapTopology.MapKey, mapKey, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException($"地圖清單 key '{mapTopology.MapKey}' 與資產資料夾 '{mapKey}' 不一致。");
			}
			return mapTopology;
		}
		catch (Exception ex)
		{
			GD.PushError("[Exploration] 地形載入失敗（" + mapKey + "）→ 退回固定場地：" + ex.Message);
			return null;
		}
	}

	private static Rect2 TopologyField(MapTopology topology)
	{
		return new Rect2(0f, 0f, (float)((double)topology.FullNativeWidth * topology.DisplayScale), (float)((double)topology.FullNativeHeight * topology.DisplayScale));
	}

	private void BuildPageLayer()
	{
		MapTopology topology = _topology;
		if (topology != null)
		{
			BuildPreviewUnderlay(topology);
			_pageLayer = new Node2D
			{
				ZIndex = 0
			};
			_world.AddChild(_pageLayer, forceReadableName: false, InternalMode.Disabled);
			_forePageLayer = new Node2D
			{
				ZIndex = 1500,
				ZAsRelative = false
			};
			_world.AddChild(_forePageLayer, forceReadableName: false, InternalMode.Disabled);
			BuildPropsLayer(topology);
			_pageStream = new MapPageStreamingSession(topology);
			_villageCells = ExplorationSpawnSession.BuildVillageCells(topology);
			IReadOnlyList<MapSpawnPoint> fixedSpawnPoints2;
			if (!MapSpawnCatalog.TryGetFixedSpawnPoints(GameDataProvider.Shared, IntegratedSpawnMapKey, out IReadOnlyList<MapSpawnPoint> fixedSpawnPoints))
			{
				IReadOnlyList<MapSpawnPoint> readOnlyList = Array.Empty<MapSpawnPoint>();
				fixedSpawnPoints2 = readOnlyList;
			}
			else
			{
				fixedSpawnPoints2 = fixedSpawnPoints;
			}
			_fixedSpawnPoints = fixedSpawnPoints2;
			_spawnSession = new ExplorationSpawnSession(topology, _hostileRng, _villageCells, (MapSpawnCell from, MapSpawnCell to) => _engine.Engine.AreExplorationCellsConnected(from, to), _fixedSpawnPoints);
			_mapEntryTimeSeconds = _engine?.Engine != null ? _engine.Engine.CurrentTimeSeconds : 0.0;
		}
	}

	private static bool MobTemplateIsBoss(string mobKey)
	{
		JsonObject jsonObject = GameDataProvider.Shared.Mob(mobKey);
		if (jsonObject != null)
		{
			return CombatSkill.ReadSystemBossFlag(jsonObject);
		}
		return false;
	}

	private bool BossMobAlive()
	{
		foreach (Combatant combatant in _engine.Combatants)
		{
			if (combatant.Kind == CombatantKind.Mob && combatant.IsAlive && combatant.IsBoss)
			{
				return true;
			}
		}
		return false;
	}

	private void BuildPreviewUnderlay(MapTopology topology)
	{
		if (string.IsNullOrWhiteSpace(topology.PreviewFile))
		{
			return;
		}
		Texture2D? texture2D = GodotDataFiles.TryLoadTexture($"res://{"assets/maps"}/{_mapKey}/{topology.PreviewFile}");
		if (texture2D != null)
		{
			double num = (double)topology.FullNativeWidth * topology.DisplayScale;
			double num2 = (double)topology.FullNativeHeight * topology.DisplayScale;
			if (texture2D.GetWidth() > 0 && texture2D.GetHeight() > 0)
			{
				Sprite2D node = new Sprite2D
				{
					Texture = texture2D,
					Centered = false,
					ZIndex = 0,
					Scale = new Vector2((float)(num / (double)texture2D.GetWidth()), (float)(num2 / (double)texture2D.GetHeight()))
				};
				_world.AddChild(node, forceReadableName: false, InternalMode.Disabled);
			}
		}
	}

	private void BuildPropsLayer(MapTopology topology)
	{
		string propsPath = $"res://assets/maps/{_mapKey}/props.json";
		if (!DataFileSystem.Exists(propsPath))
		{
			return;
		}
		try
		{
			string json = DataFileSystem.ReadAllText(propsPath);
			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement root = doc.RootElement;
			JsonElement arr = root.ValueKind == JsonValueKind.Array ? root : (root.TryGetProperty("props", out JsonElement p) ? p : default);
			if (arr.ValueKind != JsonValueKind.Array)
			{
				return;
			}
			float scale = (float)topology.DisplayScale;
			foreach (JsonElement item in arr.EnumerateArray())
			{
				if (!item.TryGetProperty("file", out JsonElement fElem) || fElem.ValueKind != JsonValueKind.String)
				{
					continue;
				}
				string fileName = fElem.GetString() ?? "";
				if (string.IsNullOrEmpty(fileName)) continue;

				string imgPath = $"res://assets/maps/{_mapKey}/{fileName}";
				Texture2D? tex = GodotDataFiles.TryLoadTexture(imgPath);
				if (tex == null) continue;

				double x = item.TryGetProperty("x", out JsonElement xElem) ? xElem.GetDouble() : 0.0;
				double y = item.TryGetProperty("y", out JsonElement yElem) ? yElem.GetDouble() : 0.0;
				double footY = item.TryGetProperty("footY", out JsonElement fyElem) ? fyElem.GetDouble() : y;
				double itemScale = item.TryGetProperty("scale", out JsonElement sElem) ? sElem.GetDouble() : 1.0;
				double ox = item.TryGetProperty("originX", out JsonElement oxElem) ? oxElem.GetDouble() : 0.5;
				double oy = item.TryGetProperty("originY", out JsonElement oyElem) ? oyElem.GetDouble() : 1.0;

				Sprite2D propSprite = new Sprite2D
				{
					Texture = tex,
					Centered = false,
					Offset = new Vector2((float)(-tex.GetWidth() * ox), (float)(-tex.GetHeight() * oy)),
					Position = new Vector2((float)(x * scale), (float)(y * scale)),
					Scale = new Vector2((float)(scale * itemScale), (float)(scale * itemScale)),
					ZIndex = Depth.Of(footY * scale),
					ZAsRelative = false,
					TextureFilter = TextureFilterEnum.Linear
				};
				_world.AddChild(propSprite, forceReadableName: false, InternalMode.Disabled);
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning("[Exploration] props.json 載入失敗：" + ex.Message);
		}
	}

	private void ExplorationSpawnStep()
	{
		if (NetworkManager.Instance.IsConnected && !NetworkManager.Instance.IsHost)
		{
			return;
		}
		if (_spawnSession == null)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		Combatant player = _engine.Player;
		if (!topology.TryLocalCellAtDisplayPixel(player.Pos.X, player.Pos.Y, out var localX, out var localY) || !topology.IsLegalCell(localX, localY))
		{
			return;
		}
		MapSpawnCell mapSpawnCell = new MapSpawnCell(localX, localY);

		double currentTime = _engine.Engine.CurrentTimeSeconds;
		bool isInitial = !_lastPlayerCell.HasValue;

		if (isInitial)
		{
			int initialDelay = MapSpawnCatalog.GetInitialSpawnDelay(GameDataProvider.Shared, IntegratedSpawnMapKey);
			if (initialDelay > 0 && currentTime - _mapEntryTimeSeconds < (double)initialDelay)
			{
				return; // 等待進圖出怪秒數到達後才開始生成怪物
			}
		}

		bool cellChanged = _lastPlayerCell != mapSpawnCell;
		if (!isInitial && !cellChanged && currentTime - _lastSpawnCheckTime < 0.5)
		{
			return;
		}
		_lastSpawnCheckTime = currentTime;
		_lastPlayerCell = mapSpawnCell;

		PerfProbeMark(12);
		ReconcileFixedSpawnSlots();

		bool isCustomMap = topology.IsCustom ||
		                   topology.MapKey.StartsWith("custom_map", StringComparison.OrdinalIgnoreCase) ||
		                   topology.MapKey.StartsWith("l1j_map_", StringComparison.OrdinalIgnoreCase) ||
		                   (topology.WidthCells <= 256 && topology.HeightCells <= 256);

		int? triggerRange = MapSpawnCatalog.GetSpawnTriggerRange(GameDataProvider.Shared, IntegratedSpawnMapKey);
		if (!triggerRange.HasValue && topology.SpawnTriggerRange.HasValue)
		{
			triggerRange = topology.SpawnTriggerRange;
		}
		if (!triggerRange.HasValue && isCustomMap)
		{
			triggerRange = 35;
		}

		bool shouldRetire = !isCustomMap && (!triggerRange.HasValue || triggerRange.Value > 0);
		if (shouldRetire)
		{
			foreach (Combatant item3 in _engine.Engine.RetireDistantExplorationMobs(player))
			{
				ReleaseFixedSpawnSlot(item3, killed: false);
				DetachRetiredL1jMobGroup(item3);
			}
		}

		PerfProbeMark(13);
		PerfProbeMark(14);

		int spawnedThisPass = 0;
		int maxPerPass = (isInitial || isCustomMap || (triggerRange.HasValue && triggerRange.Value == 0)) ? 100 : 10;
		while (spawnedThisPass < maxPerPass)
		{
			ExplorationSpawnPlan? explorationSpawnPlan = _spawnSession.PlanStep(
				mapSpawnCell, 
				_engine.Engine.LivingNormalMobCount, 
				BossMobAlive(), 
				currentTime, 
				ignoreDistance: isInitial && (!triggerRange.HasValue || triggerRange.Value <= 0),
				triggerDistance: triggerRange);

			if (!explorationSpawnPlan.HasValue)
			{
				break;
			}

			ExplorationSpawnPlan valueOrDefault = explorationSpawnPlan.GetValueOrDefault();
			(double X, double Y) tuple = topology.DisplayPixelCenter(valueOrDefault.Cell.X, valueOrDefault.Cell.Y);
			double item = tuple.X;
			double item2 = tuple.Y;
			double perfProbeNowMs = PerfProbeNowMs;
			Combatant combatant = _engine.SpawnMob(valueOrDefault.MobKey, new WorldPoint(item, item2));
			NoteHostSpawnedMob(combatant, valueOrDefault.MobKey);
			_perfProbeSpawnAccumMs += PerfProbeNowMs - perfProbeNowMs;
			_spawnSession.NoteSpawnPlaced(valueOrDefault);
			_fixedSpawnSlots[combatant] = valueOrDefault.SlotKey;
			combatant.Facing8 = (int)Math.Floor(_hostileRng.NextDouble() * 8.0) & 7;
			if (valueOrDefault.TargetCell.HasValue)
			{
				var tCell = valueOrDefault.TargetCell.Value;
				(double tx, double ty) = topology.DisplayPixelCenter(tCell.X, tCell.Y);
				combatant.MarchTarget = new WorldPoint(tx, ty);
			}
			double perfProbeNowMs2 = PerfProbeNowMs;
			TrySpawnL1jMobGroupForLeader(combatant, valueOrDefault.MobKey, topology);
			_perfProbeGroupAccumMs += PerfProbeNowMs - perfProbeNowMs2;

			spawnedThisPass++;
		}

		PerfProbeMark(15);
		_perfProbePlanCount = spawnedThisPass;
	}

	private void ReconcileFixedSpawnSlots()
	{
		if (_spawnSession == null || _fixedSpawnSlots.Count == 0)
		{
			return;
		}
		KeyValuePair<Combatant, string>[] array = _fixedSpawnSlots.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<Combatant, string> keyValuePair = array[i];
			var (combatant2, slotKey) = keyValuePair;
			if (!_engine.Combatants.Contains(combatant2) || !combatant2.IsAlive)
			{
				_spawnSession.NoteSpawnReleased(slotKey, _engine.Engine.CurrentTimeSeconds, combatant2.Dead || combatant2.Hp <= 0.0);
				_fixedSpawnSlots.Remove(combatant2);
			}
		}
	}

	private void ReleaseFixedSpawnSlot(Combatant? mob, bool killed)
	{
		if (mob != null && _spawnSession != null && _fixedSpawnSlots.Remove(mob, out string value))
		{
			_spawnSession.NoteSpawnReleased(value, _engine.Engine.CurrentTimeSeconds, killed);
		}
	}

	private void StreamPages()
	{
		if (_pageStream == null)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null || _pageLayer == null)
		{
			return;
		}
		CollectStreamedPages(topology);
		Vector2 lastStreamCenter = new Vector2(0f - _world.Position.X + WorldView.X * 0.5f, 0f - _world.Position.Y + WorldView.Y * 0.5f);
		float num = (float)((double)topology.PageWidth * topology.DisplayScale * 0.5);
		if (!float.IsNaN(_lastStreamCenter.X) && lastStreamCenter.DistanceTo(_lastStreamCenter) < num)
		{
			return;
		}
		_lastStreamCenter = lastStreamCenter;
		MapPageStreamingDelta mapPageStreamingDelta = _pageStream.Update(lastStreamCenter.X, lastStreamCenter.Y, WorldView.X, WorldView.Y);
		foreach (MapPage item in mapPageStreamingDelta.ToUnload)
		{
			MapPageCoordinate mapPageCoordinate = new MapPageCoordinate(item.X, item.Y);
			_wantedPages.Remove(mapPageCoordinate);
			if (_pageNodes.Remove(mapPageCoordinate, out Sprite2D value))
			{
				value.QueueFree();
			}
			if (_forePageNodes.Remove(mapPageCoordinate, out (Sprite2D, MapPage) value2))
			{
				value2.Item1.QueueFree();
			}
		}
		foreach (MapPage item2 in mapPageStreamingDelta.ToLoad)
		{
			MapPageCoordinate mapPageCoordinate2 = new MapPageCoordinate(item2.X, item2.Y);
			_wantedPages.Add(mapPageCoordinate2);
			if (!_pageNodes.ContainsKey(mapPageCoordinate2) && !_pendingPages.ContainsKey(mapPageCoordinate2))
			{
				string pagePath = PagePath(item2);
				Texture2D? directTex = null;
				if (pagePath.Contains("/custom_map_") || !ResourceLoader.Exists(pagePath))
				{
					directTex = GodotDataFiles.TryLoadTexture(pagePath);
				}
				if (directTex != null)
				{
					AttachPage(mapPageCoordinate2, item2, directTex, topology);
				}
				else
				{
					Error error = ResourceLoader.LoadThreadedRequest(pagePath, "Texture2D", useSubThreads: true, ResourceLoader.CacheMode.Reuse);
					if (error != Error.Ok)
					{
						Texture2D? diskTex = GodotDataFiles.TryLoadTexture(pagePath);
						if (diskTex != null)
						{
							AttachPage(mapPageCoordinate2, item2, diskTex, topology);
						}
						else
						{
							GD.PushWarning($"[Exploration] 分頁排載失敗：{item2.File}（{error}·該區塊會是空的）");
						}
					}
					else
					{
						_pendingPages[mapPageCoordinate2] = item2;
					}
				}
			}
			if (item2.Foreground != null && !_forePageNodes.ContainsKey(mapPageCoordinate2) && !_pendingForePages.ContainsKey(mapPageCoordinate2))
			{
				Error error2 = ResourceLoader.LoadThreadedRequest(ForePath(item2), "Texture2D", useSubThreads: true, ResourceLoader.CacheMode.Reuse);
				bool hasMask = !string.IsNullOrEmpty(item2.ForegroundMask);
				Error error3 = hasMask ? ResourceLoader.LoadThreadedRequest(MaskPath(item2), "Texture2D", useSubThreads: true, ResourceLoader.CacheMode.Reuse) : Error.Ok;
				if (error2 != Error.Ok && GodotDataFiles.TryLoadTexture(ForePath(item2)) == null)
				{
					GD.PushWarning($"[Exploration] 前景頁排載失敗：{item2.Foreground}（{error2}/{error3}）");
				}
				else
				{
					_pendingForePages[mapPageCoordinate2] = item2;
				}
			}
		}
	}

	private string PagePath(MapPage page)
	{
		return $"res://{"assets/maps"}/{_mapKey}/{page.File}";
	}

	private string ForePath(MapPage page)
	{
		return $"res://{"assets/maps"}/{_mapKey}/{page.Foreground}";
	}

	private string MaskPath(MapPage page)
	{
		return $"res://{"assets/maps"}/{_mapKey}/{page.ForegroundMask}";
	}

	private void CollectStreamedPages(MapTopology topology)
	{
		MapPageCoordinate key;
		MapPage value;
		if (_pendingPages.Count > 0)
		{
			_arrivedPages.Clear();
			foreach (KeyValuePair<MapPageCoordinate, MapPage> pendingPage in _pendingPages)
			{
				pendingPage.Deconstruct(out key, out value);
				MapPageCoordinate mapPageCoordinate = key;
				MapPage page = value;
				string path = PagePath(page);
				switch (ResourceLoader.LoadThreadedGetStatus(path))
				{
				case ResourceLoader.ThreadLoadStatus.Loaded:
					_arrivedPages.Add(mapPageCoordinate);
					if (_wantedPages.Contains(mapPageCoordinate) && !_pageNodes.ContainsKey(mapPageCoordinate))
					{
						Texture2D? resTex = (ResourceLoader.LoadThreadedGet(path) as Texture2D) ?? GodotDataFiles.TryLoadTexture(path);
						AttachPage(mapPageCoordinate, page, resTex, topology);
					}
					else
					{
						ResourceLoader.LoadThreadedGet(path);
					}
					break;
				default:
					_arrivedPages.Add(mapPageCoordinate);
					Texture2D? fallbackTex = GodotDataFiles.TryLoadTexture(path);
					if (fallbackTex != null && _wantedPages.Contains(mapPageCoordinate) && !_pageNodes.ContainsKey(mapPageCoordinate))
					{
						AttachPage(mapPageCoordinate, page, fallbackTex, topology);
					}
					else
					{
						GD.PushWarning("[Exploration] 分頁載不到：" + page.File + "（該區塊會是空的）");
					}
					break;
				case ResourceLoader.ThreadLoadStatus.InProgress:
					break;
				}
			}
			foreach (MapPageCoordinate arrivedPage in _arrivedPages)
			{
				_pendingPages.Remove(arrivedPage);
			}
		}
		if (_pendingForePages.Count == 0)
		{
			return;
		}
		_arrivedPages.Clear();
		foreach (KeyValuePair<MapPageCoordinate, MapPage> pendingForePage in _pendingForePages)
		{
			pendingForePage.Deconstruct(out key, out value);
			MapPageCoordinate mapPageCoordinate2 = key;
			MapPage page2 = value;
			string path2 = ForePath(page2);
			string path3 = MaskPath(page2);
			ResourceLoader.ThreadLoadStatus threadLoadStatus = ResourceLoader.LoadThreadedGetStatus(path2);
			bool hasMask = !string.IsNullOrEmpty(page2.ForegroundMask);
			ResourceLoader.ThreadLoadStatus threadLoadStatus2 = hasMask ? ResourceLoader.LoadThreadedGetStatus(path3) : ResourceLoader.ThreadLoadStatus.Loaded;
			if (threadLoadStatus == ResourceLoader.ThreadLoadStatus.InProgress || (hasMask && threadLoadStatus2 == ResourceLoader.ThreadLoadStatus.InProgress))
			{
				continue;
			}
			_arrivedPages.Add(mapPageCoordinate2);
			Texture2D? texture = (threadLoadStatus == ResourceLoader.ThreadLoadStatus.Loaded ? ResourceLoader.LoadThreadedGet(path2) as Texture2D : null) ?? GodotDataFiles.TryLoadTexture(path2);
			Texture2D? maskTexture = hasMask ? ((threadLoadStatus2 == ResourceLoader.ThreadLoadStatus.Loaded ? ResourceLoader.LoadThreadedGet(path3) as Texture2D : null) ?? GodotDataFiles.TryLoadTexture(path3)) : null;
			if (texture != null)
			{
				if (_wantedPages.Contains(mapPageCoordinate2) && !_forePageNodes.ContainsKey(mapPageCoordinate2))
				{
					AttachForePage(mapPageCoordinate2, page2, texture, maskTexture, topology);
				}
				continue;
			}
			if (threadLoadStatus == ResourceLoader.ThreadLoadStatus.Loaded)
			{
				ResourceLoader.LoadThreadedGet(path2);
			}
			if (hasMask && threadLoadStatus2 == ResourceLoader.ThreadLoadStatus.Loaded)
			{
				ResourceLoader.LoadThreadedGet(path3);
			}
			GD.PushWarning("[Exploration] 前景頁載不到：" + page2.Foreground);
		}
		foreach (MapPageCoordinate arrivedPage2 in _arrivedPages)
		{
			_pendingForePages.Remove(arrivedPage2);
		}
	}

	private void AttachPage(MapPageCoordinate key, MapPage page, Texture2D? texture, MapTopology topology)
	{
		if (texture == null)
		{
			GD.PushWarning("[Exploration] 分頁載不到：" + page.File + "（該區塊會是空的）");
			return;
		}
		float num = (float)topology.DisplayScale;
		Sprite2D sprite2D = new Sprite2D
		{
			Texture = texture,
			Centered = false,
			Position = new Vector2((float)page.PixelX * num, (float)page.PixelY * num),
			Scale = new Vector2(num, num),
			TextureFilter = TextureFilterEnum.Linear
		};
		_pageLayer.AddChild(sprite2D, forceReadableName: false, InternalMode.Disabled);
		_pageNodes[key] = sprite2D;
	}

	private void AttachForePage(MapPageCoordinate key, MapPage page, Texture2D? texture, Texture2D? maskTexture, MapTopology topology)
	{
		if (texture == null)
		{
			GD.PushWarning("[Exploration] 前景頁載不到：" + page.Foreground);
			return;
		}
		ShaderMaterial? shaderMaterial = null;
		if (maskTexture != null)
		{
			if (_occlusionFadeShader == null)
			{
				_occlusionFadeShader = GD.Load<Shader>("res://assets/ui/occlusion_fade.gdshader");
			}
			shaderMaterial = new ShaderMaterial
			{
				Shader = _occlusionFadeShader
			};
			shaderMaterial.SetShaderParameter("group_mask", maskTexture);
			shaderMaterial.SetShaderParameter("active_count", 0);
		}
		float num = (float)topology.DisplayScale;
		Sprite2D sprite2D = new Sprite2D
		{
			Texture = texture,
			Centered = false,
			Position = new Vector2((float)page.PixelX * num, (float)page.PixelY * num),
			Scale = new Vector2(num, num),
			TextureFilter = TextureFilterEnum.Linear,
			Material = shaderMaterial
		};
		_forePageLayer.AddChild(sprite2D, forceReadableName: false, InternalMode.Disabled);
		_forePageNodes[key] = (sprite2D, page);
		_forePagesDirty = true;
	}

	private void UpdateOcclusionFade(double delta)
	{
		if (_forePageNodes.Count == 0)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		_occlusionProbe.Clear();
		Combatant player = _engine.Player;
		AppendActorOcclusionGroups(topology, player);
		_playerOcclusionProbe.Clear();
		for (int i = 0; i < _occlusionProbe.Count && i < 16; i++)
		{
			_playerOcclusionProbe.Add(_occlusionProbe[i]);
		}
		foreach (Combatant combatant in _engine.Combatants)
		{
			if (combatant != player && MonsterCompanionRules.TriggersForegroundOcclusionFade(combatant))
			{
				AppendActorOcclusionGroups(topology, combatant);
			}
		}
		_occlusionProbe.Sort();
		int num = 17;
		foreach (int item2 in _occlusionProbe)
		{
			num = num * 31 + item2;
		}
		if (num == _occlusionSignature && !_forePagesDirty)
		{
			return;
		}
		_occlusionSignature = num;
		_forePagesDirty = false;
		Span<int> span = stackalloc int[16];
		foreach (KeyValuePair<MapPageCoordinate, (Sprite2D, MapPage)> forePageNode in _forePageNodes)
		{
			forePageNode.Deconstruct(out var _, out var value);
			var (sprite2D, mapPage) = value;
			if (!(sprite2D.Material is ShaderMaterial shaderMaterial))
			{
				continue;
			}
			IReadOnlyList<int> foregroundGroups = mapPage.ForegroundGroups;
			if (foregroundGroups == null)
			{
				continue;
			}
			int num2 = 0;
			if (_playerOcclusionProbe.Count > 0)
			{
				for (int j = 0; j < foregroundGroups.Count; j++)
				{
					if (num2 >= 16)
					{
						break;
					}
					if (_playerOcclusionProbe.Contains(foregroundGroups[j]))
					{
						span[num2++] = j + 1;
					}
				}
			}
			if (_occlusionProbe.Count > _playerOcclusionProbe.Count)
			{
				for (int k = 0; k < foregroundGroups.Count; k++)
				{
					if (num2 >= 16)
					{
						break;
					}
					int item = foregroundGroups[k];
					if (_occlusionProbe.Contains(item) && !_playerOcclusionProbe.Contains(item))
					{
						span[num2++] = k + 1;
					}
				}
			}
			shaderMaterial.SetShaderParameter("active_count", num2);
			if (num2 > 0)
			{
				int[] array = new int[16];
				span.Slice(0, num2).CopyTo(array);
				shaderMaterial.SetShaderParameter("active_ids", array);
			}
		}
	}

	private void AppendActorOcclusionGroups(MapTopology topology, Combatant actor)
	{
		_actorOcclusionProbe.Clear();
		if (topology.HasOcclusion && topology.TryLocalCellAtDisplayPixel(actor.Pos.X, actor.Pos.Y, out var localX, out var localY))
		{
			topology.GetOcclusionGroupsAt(localX, localY, _actorOcclusionProbe);
			foreach (int item in _actorOcclusionProbe)
			{
				if (!_occlusionProbe.Contains(item))
				{
					_occlusionProbe.Add(item);
				}
			}
		}
		if (!_worldNpcOccludersByXBucket.TryGetValue(WorldNpcOcclusionBucket((float)actor.Pos.X), out List<MapOcclusionGroup> value))
		{
			return;
		}
		foreach (MapOcclusionGroup item2 in value)
		{
			MapOcclusionGroup group = item2;
			if (!_occlusionProbe.Contains(group.Id) && MapOcclusionDepthRules.ActorOverlapsVisual(in group, topology.DisplayScale, actor.Pos.X, actor.Pos.Y, 24.0, 54.0))
			{
				_occlusionProbe.Add(group.Id);
			}
		}
	}

	private bool HandleMonsterClick(Vector2 world)
	{
		Combatant? target = PickWorldCharacterTarget(world, (Combatant actor) => actor.IsAlive && actor.Kind == CombatantKind.Mob);
		if (target == null)
		{
			return false;
		}

		// Reveal combat name & HP bar above the clicked monster
		if (_views.TryGetValue(target, out var mobView))
		{
			mobView.RevealCombatName(10.0);
		}

		// Lock this monster as the player's manual target
		StopAutoFollow(userCancelled: true);
		_engine.Engine.SetManualTarget(_engine.Player, target);

		// If already within attack range, stop and strike immediately
		if (CombatEngine.IsWithinRange(_engine.Player, target, _engine.Player.AttackRange))
		{
			_engine.StopPlayer();
		}
		else
		{
			// Walk over towards the monster!
			_engine.Engine.SetMoveTarget(_engine.Player, target.Pos, isManual: false);
			_playerView?.InterruptOneShot();
		}
		return true;
	}

	public override void _ExitTree()
	{
		CleanupMultiplayer();
		CancelCharmTargeting(silent: true);
		CancelManualSkillTargeting(silent: true);
		GameAudio.Instance?.ClearEnvironment();
		MapPageCoordinate key;
		MapPage value;
		foreach (KeyValuePair<MapPageCoordinate, MapPage> pendingPage in _pendingPages)
		{
			pendingPage.Deconstruct(out key, out value);
			MapPage page = value;
			ResourceLoader.LoadThreadedGet(PagePath(page));
		}
		foreach (KeyValuePair<MapPageCoordinate, MapPage> pendingForePage in _pendingForePages)
		{
			pendingForePage.Deconstruct(out key, out value);
			MapPage page2 = value;
			ResourceLoader.LoadThreadedGet(ForePath(page2));
			ResourceLoader.LoadThreadedGet(MaskPath(page2));
		}
		_pendingPages.Clear();
		_pendingForePages.Clear();
		_wantedPages.Clear();
	}

	private void BuildFlameShadowConsul()
	{
		_flameConsulPos = null;
		if (!string.Equals(_mapKey, "shadow_temple", StringComparison.Ordinal))
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		MapLandmark mapLandmark = topology.Landmarks.FirstOrDefault((MapLandmark candidate) => string.Equals(candidate.Id, "shadow_temple_1f_flame_consul", StringComparison.Ordinal));
		if (string.IsNullOrEmpty(mapLandmark.Id))
		{
			GD.PushWarning("[FlameShadowLab] 暗影神殿外圍缺少火焰之影的執行官 landmark。");
			return;
		}
		(double X, double Y) tuple = topology.DisplayPixelCenter(mapLandmark.LocalX, mapLandmark.LocalY);
		double item = tuple.X;
		double item2 = tuple.Y;
		Vector2 vector = new Vector2((float)item, (float)item2);
		_flameConsulPos = vector;
		Node2D node2D = new Node2D
		{
			Position = vector,
			ZIndex = ResolveWorldNpcDepth(vector, 54f)
		};
		_arena.AddChild(node2D, forceReadableName: false, InternalMode.Disabled);
		if (_reportedMissingNpcArt.Add("npc_flame_lab_consul"))
		{
			GD.PushWarning("[NPC立繪] 火焰之影的執行官（暗影神殿外圍）沒有 main 落位——只顯示名牌（禁止拿別的 NPC 立繪補洞）。");
		}
		Label label = new Label
		{
			Text = "火焰之影的執行官",
			Position = new Vector2(-90f, -94f),
			Size = new Vector2(180f, 22f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ClassicWorldText.Apply(label, Color.FromHtml("#f0df72".AsSpan()), 13);
		node2D.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Label label2 = new Label
		{
			Text = "[火焰之影實驗室]",
			Position = new Vector2(-90f, -73f),
			Size = new Vector2(180f, 18f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ClassicWorldText.Apply(label2, Color.FromHtml("#ffb08a".AsSpan()), 11);
		node2D.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
	}

	private bool HandleFlameShadowConsulClick(Vector2 world)
	{
		Vector2? flameConsulPos = _flameConsulPos;
		if (flameConsulPos.HasValue)
		{
			Vector2 valueOrDefault = flameConsulPos.GetValueOrDefault();
			if (world.DistanceSquaredTo(valueOrDefault) > 3136f)
			{
				return false;
			}
			if (PlayerPos().DistanceSquaredTo(valueOrDefault) > 9216f)
			{
				_engine.SetPlayerMoveTarget(valueOrDefault);
				return true;
			}
			_engine.StopPlayer();
			OpenFlameShadowConsulDialog();
			return true;
		}
		return false;
	}

	private bool HandleWorldNpcClick(Vector2 world)
	{
		if (_worldNpcPanel != null || _liveWorldNpcs.Count == 0)
		{
			return false;
		}
		L1jNpcSpawn? closestNpc = null;

		foreach (var live in _liveWorldNpcs)
		{
			L1jNpcSpawn npc = live.Npc;
			if (!WorldNpcIsInteractive(npc))
			{
				continue;
			}
			Vector2 npcPos = new Vector2((float)live.X, (float)live.Y);
			Rect2 npcRect;
			if (L1jNpcSpriteRenderer.TryMeasureVisualBounds(GameDataProvider.Shared, npc.Gfx, npc.Heading, out Rect2 vBounds) && vBounds.Size.X > 0 && vBounds.Size.Y > 0)
			{
				npcRect = new Rect2(npcPos + vBounds.Position, vBounds.Size);
			}
			else
			{
				npcRect = new Rect2(npcPos.X - 20f, npcPos.Y - 55f, 40f, 55f);
			}
			if (npcRect.HasPoint(world))
			{
				closestNpc = npc;
				break;
			}
		}

		if (closestNpc != null)
		{
			_engine.StopPlayer();
			OpenL1jWorldNpcPanel(closestNpc);
			return true;
		}
		return false;
	}

	private void OpenFlameShadowConsulDialog()
	{
		CloseFlameShadowConsulDialog();
		string[] lines = new string[3] { "神殿的下層，不是所有人都走得進去的。", "火焰之影大人的實驗室不在這座神殿裡——它在別處，只有裂隙通得過去。", "你要看看嗎？那裡的人會替你把素材鍛成該有的樣子。" };
		ClassicNpcDialogHandle classicNpcDialogHandle = ClassicNpcDialogWindow.Create(WorldView, "火焰之影的執行官：", lines, CloseFlameShadowConsulDialog, 1950);
		classicNpcDialogHandle.AddOption("前往火焰之影實驗室", delegate
		{
			CloseFlameShadowConsulDialog();
			SlabLog("[color=#ff9a5a]執行官撕開裂隙——爐火的熱氣自另一端湧出……[/color]");
			_session.TownKey = "town_flame_lab";
			_pendingScreenTransition = ReturnToTown;
		});
		AddAboveBarPanel(classicNpcDialogHandle.Root);
		_flameConsulDialog = classicNpcDialogHandle.Root;
	}

	private void CloseFlameShadowConsulDialog()
	{
		if (_flameConsulDialog != null && GodotObject.IsInstanceValid(_flameConsulDialog))
		{
			_flameConsulDialog.QueueFree();
		}
		_flameConsulDialog = null;
	}

	private void BuildGates()
	{
		_gates.Clear();
		List<GateVisualLayout.Candidate> list = new List<GateVisualLayout.Candidate>();
		foreach (MapLinks.Gate item in MapLinks.For(_mapKey))
		{
			Vector2 vector = ResolveGatePosition(item);
			_gates.Add((item, vector));
			if (GateVisualLayout.ShouldRender(_mapKey, item))
			{
				list.Add(new GateVisualLayout.Candidate(item, vector));
			}
		}
		foreach (GateVisualLayout.Candidate item2 in GateVisualLayout.Consolidate(list))
		{
			BuildGateLabel(item2.Gate, item2.Position);
			if (!_gates.Any(((MapLinks.Gate Gate, Vector2 Pos) entry) => entry.Pos == item2.Position))
			{
				_gates.Add((item2.Gate, item2.Position));
			}
		}
		Vector2 player = PlayerPos();
		_gatesArmed = !_gates.Any(((MapLinks.Gate Gate, Vector2 Pos) entry) =>
		{
			float r = Math.Max(entry.Gate.TriggerRadius, 36f);
			return player.DistanceSquaredTo(entry.Pos) <= r * r;
		});
	}

	private Vector2 ResolveGatePosition(MapLinks.Gate gate)
	{
		MapTopology topology = _topology;
		if (topology != null)
		{
			(int, int)? sourceGameCell = gate.SourceGameCell;
			if (sourceGameCell.HasValue)
			{
				var (num, num2) = sourceGameCell.GetValueOrDefault();
				var (num3, num4) = topology.ToLocalCell(num, num2);
				if (topology.ContainsLocalCell(num3, num4))
				{
					if (!topology.IsLegalCell(num3, num4))
					{
						if (GatePositionRules.TryFindNearbyLegalCell(topology, num3, num4, 16, out var cellX, out var cellY))
						{
							int num5 = cellX;
							num4 = cellY;
							num3 = num5;
						}
						else
						{
							GD.PushWarning($"[MapGate] Source cell '{num},{num2}' on '{_mapKey}' has no legal cell within {16}; " + "keeping the authored cell.");
						}
					}
					var (num6, num7) = topology.DisplayPixelCenter(num3, num4);
					return new Vector2((float)num6, (float)num7);
				}
				GD.PushWarning($"[MapGate] Source cell '{num},{num2}' is outside '{_mapKey}'.");
			}
		}
		MapTopology topology2 = _topology;
		if (topology2 != null && !string.IsNullOrWhiteSpace(gate.SourceLandmarkId))
		{
			MapLandmark mapLandmark = topology2.Landmarks.FirstOrDefault((MapLandmark candidate) => string.Equals(candidate.Id, gate.SourceLandmarkId, StringComparison.Ordinal));
			if (!string.IsNullOrEmpty(mapLandmark.Id))
			{
				var (num8, num9) = topology2.DisplayPixelCenter(mapLandmark.LocalX, mapLandmark.LocalY);
				return new Vector2((float)num8, (float)num9);
			}
			GD.PushWarning($"[MapGate] Missing landmark '{gate.SourceLandmarkId}' on '{_mapKey}'.");
		}
		return MapLinks.GateWorldPosition(gate.Side, Field, _grid);
	}

	private void PrepareMapPortalEntry()
	{
		if (_session.PendingHuntSpawn.HasValue)
		{
			return;
		}
		string landmarkId = _session.PendingMapEntryLandmark;
		if (string.IsNullOrWhiteSpace(landmarkId))
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		_session.PendingMapEntryLandmark = null;
		MapLandmark mapLandmark = topology.Landmarks.FirstOrDefault((MapLandmark candidate) => string.Equals(candidate.Id, landmarkId, StringComparison.Ordinal));
		if (string.IsNullOrEmpty(mapLandmark.Id))
		{
			GD.PushWarning($"[MapGate] Invalid entry landmark '{landmarkId}' on '{_mapKey}'.");
			return;
		}
		int localX = mapLandmark.LocalX;
		int cellY = mapLandmark.LocalY;
		int cellX = localX;
		if (!topology.IsLegalCell(cellX, cellY) && !GatePositionRules.TryFindNearbyLegalCell(topology, mapLandmark.LocalX, mapLandmark.LocalY, 16, out cellX, out cellY))
		{
			GD.PushWarning($"[MapGate] Entry landmark '{landmarkId}' on '{_mapKey}' has no legal cell within {16}.");
		}
		else
		{
			var (x, y) = topology.DisplayPixelCenter(cellX, cellY);
			_engine.Player.Pos = new WorldPoint(x, y);
			_engine.StopPlayer();
		}
	}

	private static SpriteFrames? _portalCircleFrames;
	private static CanvasItemMaterial? _portalAdditiveMaterial;

	private static SpriteFrames GetPortalCircleFrames()
	{
		if (_portalCircleFrames != null)
		{
			return _portalCircleFrames;
		}
		SpriteFrames frames = new SpriteFrames();
		frames.SetAnimationSpeed("default", 14.0);
		frames.SetAnimationLoopMode("default", SpriteFrames.LoopMode.Linear);
		for (int i = 0; i < 16; i++)
		{
			string path = $"res://assets/props/portal/magic_circle_{i}.png";
			Texture2D? tex = GodotDataFiles.TryLoadTexture(path);
			if (tex != null)
			{
				frames.AddFrame("default", tex);
			}
		}
		_portalCircleFrames = frames;
		return frames;
	}

	private void BuildGateLabel(MapLinks.Gate gate, Vector2 pos)
	{
		Color color = (gate.ToTown ? Color.FromHtml("#8fdd8f".AsSpan()) : Color.FromHtml("#7fd0ff".AsSpan()));
		Label label = new Label
		{
			Text = (gate.ToTown ? "\ud83c\udfe0 " : "▶ ") + gate.TargetName,
			Position = pos + new Vector2(-90f, -75f),
			CustomMinimumSize = new Vector2(180f, 0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			ZIndex = Depth.Of(pos.Y, 20),
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		label.AddThemeFontSizeOverride("font_size", 14);
		label.GuiInput += (@event) =>
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				_engine.Engine.SetManualTarget(_engine.Player, null);
				_engine.SetPlayerPathTarget(pos);
				_playerView?.InterruptOneShot();
			}
		};
		_world.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		BuildGatePortalSprite(gate, pos);
	}

	private void BuildGatePortalSprite(MapLinks.Gate gate, Vector2 pos)
	{
		SpriteFrames frames = GetPortalCircleFrames();
		if (frames.GetFrameCount("default") == 0)
		{
			return;
		}
		if (_portalAdditiveMaterial == null)
		{
			_portalAdditiveMaterial = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
		}
		AnimatedSprite2D animatedSprite2D = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Animation = "default",
			Centered = true,
			Scale = new Vector2(0.65f, 0.65f),
			Position = pos + new Vector2(0f, -39f),
			ZIndex = Depth.Of(pos.Y, -1),
			Material = _portalAdditiveMaterial
		};
		animatedSprite2D.Play();
		_world.AddChild(animatedSprite2D, forceReadableName: false, InternalMode.Disabled);
	}

	private void CheckGates()
	{
		if (_gates.Count == 0)
		{
			return;
		}
		Vector2 player = PlayerPos();
		if (_dead || _pendingScreenTransition != null)
		{
			return;
		}
		if (!_gatesArmed)
		{
			_gatesArmed = _gates.All(((MapLinks.Gate Gate, Vector2 Pos) entry) =>
			{
				float r = Math.Max(entry.Gate.TriggerRadius, 36f);
				return player.DistanceSquaredTo(entry.Pos) > r * r;
			});
			return;
		}
		foreach (var (gate, to) in _gates)
		{
			float r2 = Math.Max(gate.TriggerRadius, 36f);
			if (!(player.DistanceSquaredTo(to) > r2 * r2))
			{
				if (!IsGateSealed(gate))
				{
					EnterGate(gate);
				}
				break;
			}
		}
	}

	private bool IsGateSealed(MapLinks.Gate gate)
	{
		return false;
	}

	private void EnterGate(MapLinks.Gate gate)
	{
		if (gate.ToTown)
		{
			_session.TownKey = gate.TargetKey;
			SlabLog("[color=#8fdd8f]進入 " + gate.TargetName + "[/color]");
			_pendingScreenTransition = ReturnToTown;
			return;
		}
		_session.HuntMap = gate.TargetKey;
		_session.PendingMapEntryLandmark = gate.DestinationLandmarkId;
		_session.PendingHuntSpawn = null;
		(int, int)? destinationGameCell = gate.DestinationGameCell;
		if (destinationGameCell.HasValue)
		{
			(int, int) valueOrDefault = destinationGameCell.GetValueOrDefault();
			int item = valueOrDefault.Item1;
			int item2 = valueOrDefault.Item2;
			MapTopology mapTopology = TryLoadTopology(gate.TargetKey);
			if (mapTopology != null)
			{
				var (cellX, cellY) = mapTopology.ToLocalCell(item, item2);
				if (mapTopology.ContainsLocalCell(cellX, cellY))
				{
					if (!mapTopology.IsLegalCell(cellX, cellY))
					{
						GatePositionRules.TryFindNearbyLegalCell(mapTopology, cellX, cellY, 16, out cellX, out cellY);
					}
					var (item3, item4) = mapTopology.DisplayPixelCenter(cellX, cellY);
					_session.PendingHuntSpawn = (item3, item4);
				}
			}
		}
		SaveManager.Save(_session);
		SlabLog("[color=#7fd0ff]前往 " + gate.TargetName + "[/color]");
		_pendingScreenTransition = ChangeHuntMap;
	}

	private L1jGetbackRoute ResolveCurrentGetback()
	{
		var (gameX, gameY) = CurrentGameCell();
		return L1jGetbackCatalog.Load(GameDataProvider.Shared).Resolve(_mapKey, gameX, gameY, _engine.Player.ClassId, _rng.Next(3), _rng.Next());
	}

	private void ApplyGetbackTransfer(L1jGetbackRoute route)
	{
		ArgumentNullException.ThrowIfNull(route, "route");
		L1jGetbackDestination destination = route.Destination;
		if (!destination.IsRuntimeResolved)
		{
			throw new InvalidDataException($"L1J getback destination {destination.MapId}:{destination.GameX},{destination.GameY} has no runtime map.");
		}
		SynchronizeExitState();
		_session.SuppressPetDeploymentOnce = false;
		_session.HuntMap = destination.MapKey;
		_session.PendingHuntSpawn = (destination.DisplayX.Value, destination.DisplayY.Value);
		_session.LastHuntMap = "";
		if (!string.IsNullOrWhiteSpace(destination.TownKey))
		{
			_session.TownKey = destination.TownKey;
		}
		_onChangeMap();
	}

	private void SynchronizeExitState()
	{
		if (_session.Party.Members.Count > 0)
		{
			_engine.Engine.RefreshLivingAlliesAtTown();
			_session.Party.Synchronize(_engine.Combatants);
		}
		if (_session.Pets.Pets.Count > 0)
		{
			_session.Pets.Synchronize(_engine.Engine);
		}
		SyncAllOfflineCompanions();
	}

	private GroundDrop? SpawnGroundDrop(CombatEvent e, Vector2? overrideAt = null, ItemStack? sourceStack = null, string? dropId = null)
	{
		if (_groundDropLayer == null || e.ItemKey == null || e.IntArg <= 0)
		{
			return null;
		}
		Vector2 position = overrideAt ?? ((e.X != 0.0 || e.Y != 0.0) ? new Vector2((float)e.X, (float)e.Y) : ((e.Source != null) ? ToVec(e.Source.Pos) : PlayerPos()));
		position = new Vector2(Mathf.Clamp(position.X, Field.Position.X, Field.End.X), Mathf.Clamp(position.Y, Field.Position.Y, Field.End.Y));
		float y = position.Y;
		Node2D node2D = new Node2D
		{
			Position = position,
			ZIndex = Math.Max(0, Depth.Of(y, -1))
		};
		ItemStack itemStack = sourceStack?.Copy("ground-preview", e.IntArg) ?? new ItemStack("ground-preview", e.ItemKey, e.IntArg)
		{
			Blessing = e.ItemBlessing,
			Enhancement = e.ItemEnhancement,
			IsIdentified = e.ItemIdentified,
			ItemLevel = e.ItemLevel,
			Affixes = e.ItemAffixes.ToArray()
		};
		Texture2D texture2D = ItemIcons.For(e.ItemKey, forGround: true);
		Vector2 vector = texture2D?.GetSize() ?? Vector2.One;
		vector = new Vector2(Mathf.Max(1f, vector.X), Mathf.Max(1f, vector.Y));
		Vector2 vector2 = new Vector2(Mathf.Max(26f, vector.X), Mathf.Max(26f, vector.Y));
		Color? color = GroundDropQualityColor(itemStack);
		if (color.HasValue)
		{
			Color valueOrDefault = color.GetValueOrDefault();
			TextureRect node = new TextureRect
			{
				Texture = GroundDropGlowTexture(valueOrDefault),
				CustomMinimumSize = new Vector2(52f, 30f),
				Size = new Vector2(52f, 30f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				TextureFilter = TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = new Vector2(-26f, -15f),
				ZIndex = -1
			};
			ColorRect node2 = new ColorRect
			{
				Color = Colors.White,
				CustomMinimumSize = new Vector2(28f, 104f),
				Size = new Vector2(28f, 104f),
				MouseFilter = MouseFilterEnum.Ignore,
				Position = new Vector2(-14f, -104f + vector.Y * 0.5f),
				ZIndex = -1,
				Material = GroundDropPillarMaterial(valueOrDefault)
			};
			node2D.AddChild(node, forceReadableName: false, InternalMode.Disabled);
			node2D.AddChild(node2, forceReadableName: false, InternalMode.Disabled);
		}
		GroundDropClickTarget groundDropClickTarget = new GroundDropClickTarget
		{
			CustomMinimumSize = vector2,
			Size = vector2,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, itemStack) + ItemInstanceText.DetailTooltip(GameDataProvider.Shared, itemStack),
			Position = -vector2 * 0.5f
		};
		if (texture2D != null)
		{
			groundDropClickTarget.AddChild(new TextureRect
			{
				Texture = texture2D,
				Size = vector,
				Position = (vector2 - vector) * 0.5f,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				TextureFilter = TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore
			}, forceReadableName: false, InternalMode.Disabled);
		}
		Label label = null;
		if (e.IntArg > 1)
		{
			label = new Label
			{
				Text = $"×{e.IntArg}",
				MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				LabelSettings = new LabelSettings
				{
					FontSize = 10,
					FontColor = Colors.White,
					ShadowColor = new Color(0f, 0f, 0f, 0.85f),
					ShadowOffset = new Vector2(1f, 1f),
					ShadowSize = 1
				},
				Position = new Vector2((0f - vector2.X) * 0.5f, (0f - vector2.Y) * 0.5f),
				Size = vector2
			};
		}
		node2D.AddChild(groundDropClickTarget, forceReadableName: false, InternalMode.Disabled);
		if (label != null)
		{
			node2D.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		}
		GroundDrop groundDrop = new GroundDrop(node2D, itemStack.Copy(sourceStack?.Uid ?? "ground-item", e.IntArg), 180f, label)
		{
			DropId = dropId ?? Guid.NewGuid().ToString("N")
		};
		groundDropClickTarget.OnPressed = delegate
		{
			RequestGroundDropPickup(groundDrop);
		};
		_groundDropLayer.AddChild(node2D, forceReadableName: false, InternalMode.Disabled);
		_groundDrops.Add(groundDrop);
		return groundDrop;
	}

	private void RequestGroundDropPickup(GroundDrop drop)
	{
		if (_paused || !GodotObject.IsInstanceValid(drop.Node) || !_groundDrops.Contains(drop))
		{
			return;
		}
		foreach (GroundDrop groundDrop in _groundDrops)
		{
			if (groundDrop != drop)
			{
				groundDrop.PickupRequested = false;
				groundDrop.PickupArrivalStopped = false;
				groundDrop.PickupAnimationStarted = false;
				groundDrop.PickupAnimationRemaining = 0.0;
			}
		}
		drop.PickupRequested = true;
		drop.PickupArrivalStopped = false;
		drop.PickupAnimationStarted = false;
		drop.PickupAnimationRemaining = 0.0;
		Vector2 position = drop.Node.Position;
		if (PlayerPos().DistanceTo(position) > 18f)
		{
			_engine.SetPlayerPathTarget(position);
			_playerView?.InterruptOneShot();
		}
	}

	private Control? _dropQuantityPrompt;

	private void CloseDropQuantityPrompt()
	{
		if (_dropQuantityPrompt != null && GodotObject.IsInstanceValid(_dropQuantityPrompt))
		{
			_dropQuantityPrompt.QueueFree();
		}
		_dropQuantityPrompt = null;
	}

	private void OpenDropQuantityPrompt(ItemStack stack, string itemKey, string stackUid, Vector2 dropAt)
	{
		CloseDropQuantityPrompt();

		Control blocker = new Control
		{
			Position = Vector2.Zero,
			Size = new Vector2(_viewW, _viewH),
			MouseFilter = Control.MouseFilterEnum.Stop,
			ZIndex = 2500
		};

		Panel box = new Panel
		{
			Size = new Vector2(340f, 130f),
			Position = ((new Vector2(_viewW, _viewH) - new Vector2(340f, 130f)) * 0.5f).Round(),
			MouseFilter = Control.MouseFilterEnum.Stop
		};

		StyleBoxFlat boxStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.06f, 0.08f, 0.98f),
			BorderColor = new Color(0.78f, 0.68f, 0.42f, 0.9f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4
		};
		box.AddThemeStyleboxOverride("panel", boxStyle);
		blocker.AddChild(box, forceReadableName: false, InternalMode.Disabled);

		long maxQuantity = stack.Quantity;
		long chosenQuantity = maxQuantity;

		string displayName = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, stack);
		Label titleLabel = new Label
		{
			Text = $"丟棄「{displayName}」（現有 {maxQuantity:N0}）",
			Position = new Vector2(12f, 8f),
			Size = new Vector2(316f, 20f)
		};
		titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f));
		titleLabel.AddThemeFontSizeOverride("font_size", 12);
		titleLabel.ClipText = true;
		box.AddChild(titleLabel, forceReadableName: false, InternalMode.Disabled);

		LineEdit input = new LineEdit
		{
			Text = chosenQuantity.ToString(),
			Position = new Vector2(44f, 38f),
			Size = new Vector2(108f, 24f),
			Alignment = HorizontalAlignment.Center,
			TooltipText = $"請輸入丟棄數量 (1 ~ {maxQuantity})"
		};
		input.AddThemeFontSizeOverride("font_size", 12);
		StyleBoxFlat inputStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.02f, 0.04f, 0.95f),
			BorderColor = new Color(0.45f, 0.42f, 0.35f, 0.8f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2
		};
		input.AddThemeStyleboxOverride("normal", inputStyle);
		input.AddThemeStyleboxOverride("focus", inputStyle);
		box.AddChild(input, forceReadableName: false, InternalMode.Disabled);

		void SyncText()
		{
			input.Text = chosenQuantity.ToString();
		}

		void Adjust(long delta)
		{
			chosenQuantity = Math.Clamp(chosenQuantity + delta, 1L, maxQuantity);
			SyncText();
		}

		input.TextChanged += delegate(string newText)
		{
			if (long.TryParse(newText.Trim(), out long val))
			{
				chosenQuantity = Math.Clamp(val, 1L, maxQuantity);
			}
		};

		input.TextSubmitted += delegate
		{
			SyncText();
		};

		Button Mini(string text, float x, float y, float w, Action pressed)
		{
			Button btn = new Button
			{
				Text = text,
				Position = new Vector2(x, y),
				Size = new Vector2(w, 24f),
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			btn.AddThemeFontSizeOverride("font_size", 11);
			btn.Pressed += pressed;
			box.AddChild(btn, forceReadableName: false, InternalMode.Disabled);
			return btn;
		}

		Mini("−", 12f, 38f, 26f, delegate { Adjust(-1L); });
		Mini("＋", 158f, 38f, 26f, delegate { Adjust(1L); });
		Mini("一半", 192f, 38f, 64f, delegate
		{
			chosenQuantity = Math.Max(1L, maxQuantity / 2);
			SyncText();
		});
		Mini("全部", 262f, 38f, 66f, delegate
		{
			chosenQuantity = maxQuantity;
			SyncText();
		});

		TextureButton btnConfirm = ClassicArtButtons.Confirm(delegate
		{
			if (long.TryParse(input.Text.Trim(), out long directVal))
			{
				chosenQuantity = Math.Clamp(directVal, 1L, maxQuantity);
			}
			long finalQty = chosenQuantity;
			CloseDropQuantityPrompt();
			if (!TryDropBagItemToGround(itemKey, stackUid, dropAt, finalQty))
			{
				SlabLog($"[color=#e2938f]無法將 {displayName} 放到地上[/color]");
			}
		}, "確認丟棄");
		btnConfirm.Position = new Vector2(12f, 78f);
		box.AddChild(btnConfirm, forceReadableName: false, InternalMode.Disabled);

		TextureButton btnCancel = ClassicArtButtons.Cancel(delegate
		{
			CloseDropQuantityPrompt();
		}, "取消");
		btnCancel.Position = new Vector2(340f - ClassicArtButtons.CancelSize.X - 12f, 78f);
		box.AddChild(btnCancel, forceReadableName: false, InternalMode.Disabled);

		_dropQuantityPrompt = blocker;
		_overlayUi.AddChild(blocker, forceReadableName: false, InternalMode.Disabled);
	}

	private void OnBagItemDrop(Vector2 atPosition, string payload)
	{
		if (string.IsNullOrWhiteSpace(payload))
		{
			return;
		}
		if (_townNpcHost != null && _townNpcHost.HasActiveOverlay)
		{
			return;
		}
		(string ItemKey, string StackUid, bool HasStackUid) tuple = ItemDragPayload.Decode(payload);
		var (text, stackUid, _) = tuple;
		if (!tuple.HasStackUid)
		{
			text = payload;
		}
		if (!text.StartsWith("skill:", StringComparison.Ordinal))
		{
			Vector2 dropAt = ResolveBagDropPosition(atPosition);
			ItemStack stack = _engine.Player.InventoryStacks.FirstOrDefault(s => (!string.IsNullOrEmpty(stackUid) && s.Uid == stackUid) || s.ItemKey == text);
			if (stack == null)
			{
				return;
			}
			if (stack.Locked)
			{
				SlabLog("[color=#e2938f]已鎖定的物品無法丟棄[/color]");
				return;
			}
			if (stack.Quantity > 1)
			{
				OpenDropQuantityPrompt(stack, text, stackUid, dropAt);
			}
			else
			{
				if (!TryDropBagItemToGround(text, stackUid, dropAt, 1L))
				{
					SlabLog("[color=#e2938f]無法將 " + text + " 放到地上[/color]");
				}
			}
		}
	}

	private bool TryDropBagItemToGround(string itemKey, string stackUid, Vector2 dropAt, long quantity = 1L)
	{
		if (string.IsNullOrWhiteSpace(itemKey) || _engine == null || _engine.Player == null)
		{
			return false;
		}
		if (!TryTakeBagStack(itemKey, stackUid, quantity, out ItemStack taken))
		{
			return false;
		}
		taken = taken.Copy(CombatInventory.NextUid(_engine.Player));
		MonsterCardPartyRules.RecallBeforeCardLeavesInventory(GameDataProvider.Shared, _session.Party, taken, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _engine.Engine);
		int intArg = (int)((taken.Quantity > int.MaxValue) ? int.MaxValue : taken.Quantity);
		string dropId = Guid.NewGuid().ToString("N");
		SpawnGroundDrop(new CombatEvent(CombatEventKind.Drop, null, null, 0.0, crit: false, DamageType.Melee, null, null, null, null, taken.ItemKey, intArg, taken.Blessing, taken.Enhancement, taken.IsIdentified, taken.ItemLevel, taken.Affixes), dropAt, taken, dropId);
		CombatInventory.SyncLegacyView(_engine.Player);
		SaveManager.Save(_session);
		_bagRefresh?.Invoke();
		string text = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, taken);
		string countStr = taken.Quantity > 1 ? $" ×{taken.Quantity:N0}" : "";
		SlabLog($"[color=#8fdd8f]已將「{text}{countStr}」丟到地上[/color]");

		if (NetworkManager.Instance.IsConnected)
		{
			string affixesJson = (taken.Affixes != null && taken.Affixes.Count > 0) ? JsonSerializer.Serialize(taken.Affixes) : "";
			NetworkManager.Instance.SendItemDrop(new ItemDropPacket
			{
				DropId = dropId,
				ItemKey = taken.ItemKey,
				Quantity = taken.Quantity,
				X = dropAt.X,
				Y = dropAt.Y,
				MapKey = _mapKey,
				Blessing = (int)taken.Blessing,
				Enhancement = taken.Enhancement,
				IsIdentified = taken.IsIdentified,
				ItemLevel = taken.ItemLevel,
				AffixesJson = affixesJson,
				DropperId = NetworkManager.Instance.LocalPlayerId,
				DropperName = _engine.Player.Disp
			});
		}
		return true;
	}

	private bool TryReceiveCombatDrop(CombatEvent drop, out long received)
	{
		received = 0L;
		if (_engine == null || _engine.Player == null || string.IsNullOrWhiteSpace(drop.ItemKey) || drop.IntArg <= 0)
		{
			return false;
		}
		JsonObject jsonObject = GameDataProvider.Shared.Item(drop.ItemKey);
		long num = ((jsonObject == null) ? 0 : ((long)Math.Floor(ReadDouble(jsonObject, "maxHold"))));
		long num2 = CombatInventory.Count(_engine.Player, drop.ItemKey);
		long val = ((num <= 0) ? long.MaxValue : (num - num2));
		long num3 = Math.Min(drop.IntArg, Math.Min(val, 2147483647L));
		if (num3 <= 0)
		{
			return false;
		}
		ItemStack incoming = new ItemStack(CombatInventory.NextUid(_engine.Player), drop.ItemKey, num3)
		{
			Blessing = drop.ItemBlessing,
			Enhancement = drop.ItemEnhancement,
			IsIdentified = drop.ItemIdentified || drop.ItemKey == ContentAdditions.GuardianSoulKey,
			ItemLevel = drop.ItemLevel,
			Affixes = drop.ItemAffixes.ToArray()
		};
		try
		{
			CombatInventory.Add(GameDataProvider.Shared, _engine.Player, incoming);
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		received = num3;
		return true;
	}

	private bool TryTakeBagStack(string itemKey, string stackUid, long quantity, out ItemStack taken)
	{
		List<ItemStack> inventoryStacks = _engine.Player.InventoryStacks;
		long takeCount = Math.Max(1L, quantity);
		if (!string.IsNullOrWhiteSpace(stackUid) && ItemStackInventory.TryRemoveByUid(inventoryStacks, stackUid, takeCount, out ItemStack removed))
		{
			taken = removed;
			return true;
		}
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			taken = null;
			return false;
		}
		for (int i = 0; i < inventoryStacks.Count; i++)
		{
			ItemStack itemStack = inventoryStacks[i];
			if (!(itemStack.ItemKey != itemKey) && !itemStack.Locked && ItemStackInventory.TryRemoveByUid(inventoryStacks, itemStack.Uid, takeCount, out ItemStack removed2))
			{
				taken = removed2;
				return true;
			}
		}
		taken = null;
		return false;
	}

	private Vector2 ResolveBagDropPosition(Vector2 dragDropAt)
	{
		if (_engine == null || _engine.Player == null)
		{
			return dragDropAt;
		}
		Vector2 vector = PlayerPos();
		int num = ResolveBagDropHeading(vector, dragDropAt);
		if (num < 0)
		{
			num = _engine.Player.Facing8;
			if (num < 0 || num >= DragDropHeadingSteps.Length)
			{
				return vector;
			}
		}
		MapTopology topology = _topology;
		if (topology == null || !topology.TryLocalCellAtDisplayPixel(vector.X, vector.Y, out var localX, out var localY))
		{
			Vector2 vector2 = vector + DragDropFacingOffsets[num];
			return new Vector2(Mathf.Clamp(vector2.X, Field.Position.X, Field.End.X), Mathf.Clamp(vector2.Y, Field.Position.Y, Field.End.Y));
		}
		int num2 = localX + L1jTileRules.HeadingDx[num];
		int num3 = localY + L1jTileRules.HeadingDy[num];
		if (!topology.ContainsLocalCell(num2, num3) || !topology.IsLegalCell(num2, num3))
		{
			int num4 = localX;
			num3 = localY;
			num2 = num4;
		}
		var (num5, num6) = topology.DisplayPixelCenter(num2, num3);
		return new Vector2((float)num5, (float)num6);
	}

	private int ResolveBagDropHeading(Vector2 playerPos, Vector2 dragDropAt)
	{
		MapTopology topology = _topology;
		if (topology != null && topology.TryLocalCellAtDisplayPixel(dragDropAt.X, dragDropAt.Y, out var localX, out var localY) && topology.TryLocalCellAtDisplayPixel(playerPos.X, playerPos.Y, out var localX2, out var localY2))
		{
			return L1jTileRules.HeadingFor(Math.Sign(localX - localX2), Math.Sign(localY - localY2));
		}
		Vector2 vector = dragDropAt - playerPos;
		if (vector.Length() <= 0f)
		{
			return -1;
		}
		vector = vector.Normalized();
		float num = float.NegativeInfinity;
		int result = -1;
		for (int i = 0; i < DragDropHeadingSteps.Length; i++)
		{
			float num2 = vector.Dot(DragDropHeadingSteps[i]);
			if (!(num2 <= num))
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	private bool GroundDropStep(double delta)
	{
		if (_groundDrops.Count == 0)
		{
			return false;
		}
		Vector2 vector = PlayerPos();
		bool result = false;
		float num = 18f;
		for (int num2 = _groundDrops.Count - 1; num2 >= 0; num2--)
		{
			GroundDrop groundDrop = _groundDrops[num2];
			if (!GodotObject.IsInstanceValid(groundDrop.Node))
			{
				_groundDrops.RemoveAt(num2);
				continue;
			}
			groundDrop.LifeSeconds -= (float)delta;
			if (groundDrop.LifeSeconds <= 0f)
			{
				groundDrop.Node.QueueFree();
				_groundDrops.RemoveAt(num2);
				continue;
			}
			if (!groundDrop.PickupRequested)
			{
				continue;
			}
			if (groundDrop.Node.Position.DistanceTo(vector) > num)
			{
				groundDrop.PickupArrivalStopped = false;
				groundDrop.PickupAnimationStarted = false;
				groundDrop.PickupAnimationRemaining = 0.0;
				continue;
			}
			bool flag = _engine.RenderWalk(_engine.Player).Stepping || Math.Abs(_engine.Player.VelX) > 0.0001 || Math.Abs(_engine.Player.VelY) > 0.0001;
			if (!groundDrop.PickupAnimationStarted)
			{
				if (!groundDrop.PickupArrivalStopped)
				{
					_engine.StopPlayer();
					groundDrop.PickupArrivalStopped = true;
					continue;
				}
				if (flag)
				{
					continue;
				}
				Vector2 vector2 = groundDrop.Node.Position - vector;
				if (vector2.LengthSquared() > 0.0001f)
				{
					_playerView?.Face(vector2.X, vector2.Y);
				}
				_playerView?.PlayOneShot("get");
				double num3 = _playerView?.LastOneShotSeconds ?? 0.0;
				if (num3 > 0.0)
				{
					groundDrop.PickupAnimationStarted = true;
					groundDrop.PickupAnimationRemaining = num3;
					_engine.Engine.LockAction(_engine.Player, num3);
					continue;
				}
				goto IL_02b8;
			}
			if (!flag)
			{
				WorldPoint? moveTarget = _engine.Player.MoveTarget;
				if (!moveTarget.HasValue)
				{
					groundDrop.PickupAnimationRemaining = Math.Max(0.0, groundDrop.PickupAnimationRemaining - delta);
					if (groundDrop.PickupAnimationRemaining > 0.0)
					{
						continue;
					}
					goto IL_02b8;
				}
			}
			groundDrop.PickupArrivalStopped = false;
			groundDrop.PickupAnimationStarted = false;
			groundDrop.PickupAnimationRemaining = 0.0;
			continue;
			IL_02b8:
			groundDrop.PickupAnimationStarted = false;
			groundDrop.PickupAnimationRemaining = 0.0;
			int oldQuantity = groundDrop.Quantity;
			if (TryPickupGroundDrop(groundDrop))
			{
				result = true;
				SaveManager.Save(_session);
				int pickedAmount = oldQuantity - groundDrop.Quantity;
				if (NetworkManager.Instance.IsConnected && !string.IsNullOrEmpty(groundDrop.DropId))
				{
					NetworkManager.Instance.SendItemPickup(new ItemPickupPacket
					{
						DropId = groundDrop.DropId,
						PickerId = NetworkManager.Instance.LocalPlayerId,
						PickerName = _engine.Player.Disp,
						ItemKey = groundDrop.ItemKey,
						Quantity = pickedAmount,
						Remaining = groundDrop.Quantity
					});
				}
				if (groundDrop.Quantity > 0)
				{
					if (groundDrop.CountLabel != null)
					{
						groundDrop.CountLabel.Text = $"×{groundDrop.Quantity}";
					}
					groundDrop.PickupRequested = false;
					groundDrop.PickupArrivalStopped = false;
					SlabLog("[color=#e2938f]背包持有上限不足，剩餘物品留在地上[/color]");
				}
				else
				{
					groundDrop.Node.QueueFree();
					_groundDrops.RemoveAt(num2);
				}
			}
			else
			{
				groundDrop.PickupRequested = false;
				groundDrop.PickupArrivalStopped = false;
				groundDrop.PickupAnimationStarted = false;
				groundDrop.PickupAnimationRemaining = 0.0;
				_engine.StopPlayer();
				SlabLog("[color=#e2938f]目前無法撿取這件物品，物品仍留在地上[/color]");
			}
		}
		return result;
	}

	private bool TryPickupGroundDrop(GroundDrop drop)
	{
		if (_session.FilteredItems.Contains(drop.ItemKey))
		{
			return false;
		}
		JsonObject jsonObject = GameDataProvider.Shared.Item(drop.ItemKey);
		long num = ((jsonObject == null) ? 0 : ((long)Math.Floor(ReadDouble(jsonObject, "maxHold"))));
		long num2 = CombatInventory.Count(_engine.Player, drop.ItemKey);
		long num3 = ((num <= 0) ? long.MaxValue : (num - num2));
		if (num3 <= 0)
		{
			return false;
		}
		long num4 = Math.Min(drop.Quantity, (int)Math.Min(num3, 2147483647L));
		if (num4 <= 0)
		{
			return false;
		}
		ItemStack incoming = drop.Stack.Copy(CombatInventory.NextUid(_engine.Player), num4);
		try
		{
			CombatInventory.Add(GameDataProvider.Shared, _engine.Player, incoming);
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		drop.Stack.Quantity -= num4;
		drop.Quantity -= (int)num4;
		string value = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, drop.ItemKey, drop.IsIdentified);
		SlabLog($"[color=#8fdd8f]撿到 {value} ×{num4}[/color]");
		if (drop.ItemKey == ContentAdditions.GuardianSoulKey)
		{
			ActivateGuardianSoul();
		}
		return true;
	}

	private static Color? GroundDropQualityColor(ItemStack item)
	{
		if (!item.IsIdentified)
		{
			return null;
		}
		if (!IsGroundDropEquipment(item.ItemKey))
		{
			return null;
		}
		return EquipmentAffixRules.Quality(item).Count switch
		{
			1 => GroundDropGreen, 
			2 => GroundDropBlue, 
			3 => GroundDropPurple, 
			4 => GroundDropOrange, 
			_ => null, 
		};
	}

	private static bool IsGroundDropEquipment(string itemKey)
	{
		switch (GameDataProvider.Shared.Item(itemKey)?["type"]?.GetValue<string>())
		{
		case "wpn":
		case "arm":
		case "acc":
			return true;
		default:
			return false;
		}
	}

	private static ShaderMaterial GroundDropPillarMaterial(Color color)
	{
		if (_groundDropPillarShader == null)
		{
			_groundDropPillarShader = GD.Load<Shader>("res://assets/ui/ground_loot_pillar.gdshader");
		}
		ShaderMaterial shaderMaterial = new ShaderMaterial();
		shaderMaterial.Shader = _groundDropPillarShader;
		shaderMaterial.SetShaderParameter("pillar_color", color);
		return shaderMaterial;
	}

	private static Texture2D GroundDropGlowTexture(Color color)
	{
		string key = color.ToHtml();
		if (GroundDropGlowTextures.TryGetValue(key, out Texture2D value) && value != null)
		{
			return value;
		}
		int num = 52;
		int num2 = 30;
		Image image = Image.CreateEmpty(num, num2, useMipmaps: false, Image.Format.Rgba8);
		float num3 = (float)(num - 1) * 0.5f;
		float num4 = (float)(num2 - 1) * 0.5f;
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				float num5 = ((float)j - num3) / Math.Max(1f, num3);
				float num6 = ((float)i - num4) / Math.Max(1f, num4);
				float num7 = MathF.Sqrt(num5 * num5 + num6 * num6);
				float num8 = Math.Clamp(1f - num7, 0f, 1f);
				num8 *= num8 * 0.9f;
				image.SetPixel(j, i, new Color(color.R, color.G, color.B, num8));
			}
		}
		Texture2D texture2D = ImageTexture.CreateFromImage(image);
		GroundDropGlowTextures[key] = texture2D;
		return texture2D;
	}

	private static double ReadDouble(JsonObject definition, string key)
	{
		if (!(definition[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0.0;
		}
		return value;
	}

	private void BuildHarborFerryShip()
	{
		_ferryShip = null;
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		(string, string, int, int)[] shipBerths = HarborFerryCatalog.ShipBerths;
		for (int i = 0; i < shipBerths.Length; i++)
		{
			var (a, b, num, num2) = shipBerths[i];
			if (!string.Equals(a, _mapKey, StringComparison.Ordinal))
			{
				continue;
			}
			MapLandmark mapLandmark = default(MapLandmark);
			foreach (MapLandmark landmark in topology.Landmarks)
			{
				if (string.Equals(landmark.Id, b, StringComparison.Ordinal))
				{
					mapLandmark = landmark;
					break;
				}
			}
			if (!string.IsNullOrEmpty(mapLandmark.Id))
			{
				(double X, double Y) tuple2 = topology.DisplayPixelCenter(mapLandmark.LocalX + num, mapLandmark.LocalY + num2);
				double item = tuple2.X;
				double item2 = tuple2.Y;
				_ferryShip = BuildFerryShipSprite(new Vector2((float)item, (float)item2));
				break;
			}
		}
	}

	private Node2D? BuildFerryShipSprite(Vector2 pos)
	{
		if (!ResourceLoader.Exists("res://assets/props/ferry/ferry_ship.png"))
		{
			GD.PushWarning("[Harbor] 渡輪立繪載入失敗（assets/props/ferry/ferry_ship.png）。");
			return null;
		}
		Texture2D texture2D = GD.Load<Texture2D>("res://assets/props/ferry/ferry_ship.png");
		Sprite2D sprite2D = new Sprite2D
		{
			Texture = texture2D,
			Centered = true,
			Scale = new Vector2(1.3333334f, 1.3333334f),
			Position = pos + new Vector2(0f, (float)(-texture2D.GetHeight()) * 1.3333334f * 0.5f),
			ZIndex = Depth.Of(pos.Y, -1)
		};
		_world.AddChild(sprite2D, forceReadableName: false, InternalMode.Disabled);
		return sprite2D;
	}

	private void CheckHiddenValleyLevelExit()
	{
		if (_pendingScreenTransition == null && L1jHiddenValleyCatalog.TriggersLevelExit(_mapKey, _engine.Player.Level))
		{
			if (!TryResolveL1jTeleportTarget(4, 33088, 33392, out string mapKey, out double worldX, out double worldY))
			{
				SlabLog("[color=#e2938f]離開隱藏之谷失敗 — 原版目的地地圖尚未建立[/color]");
				return;
			}
			_engine.Player.Facing8 = 4;
			_engine.StopPlayer();
			_session.SuppressPetDeploymentOnce = false;
			_session.HuntMap = mapKey;
			_session.PendingHuntSpawn = (worldX, worldY);
			_session.LastHuntMap = "";
			SaveManager.Save(_session);
			SlabLog("[color=#7fd0ff]離開隱藏之谷 — 傳送至騎士村[/color]");
			_pendingScreenTransition = ChangeHuntMap;
		}
	}

	private static Color HostilePlayerNameColor(Combatant actor)
	{
		return CombatCurveMath.GetAlignmentTier(actor.Alignment) switch
		{
			AlignmentTier.Justice => Color.FromHtml("#6ea8ff".AsSpan()), 
			AlignmentTier.Evil => Color.FromHtml("#ff6a5a".AsSpan()), 
			_ => Colors.White, 
		};
	}

	private void SyncPvpFlag()
	{
		_engine.Engine.PlayerPvpEnabled = _session.PvpEnabled;
		RefreshWantedFlags();
		RefreshAreaNameColor();
	}

	private void RefreshWantedFlags()
	{
		long nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		_engine.Player.WantedByGuards = HostilePlayerRules.IsWantedNow(_session.LastPlayerKillUnixSeconds, nowUnixSeconds);
		_engine.Player.WantedForElfGuardians = HostilePlayerRules.IsWantedNow(_session.LastElfKillUnixSeconds, nowUnixSeconds);
	}

	private void RefreshAreaNameColor()
	{
		if (_areaName != null)
		{
			_areaName.AddThemeColorOverride("font_color", Color.FromHtml((_session.PvpEnabled ? "#ff5a4a" : "#6fa8dc").AsSpan()));
		}
	}

	private void SettleHostilePlayerKill(Combatant victim, Combatant? killer)
	{
		bool flag;
		if (killer != null)
		{
			CombatantKind kind = killer.Kind;
			if (kind == CombatantKind.Player || (uint)(kind - 2) <= 2u)
			{
				flag = true;
				goto IL_0019;
			}
		}
		flag = false;
		goto IL_0019;
		IL_0019:
		if (!flag)
		{
			return;
		}
		double alignment = _engine.Player.Alignment;
		double num = HostilePlayerRules.KillAlignmentPenalty(victim.Alignment);
		if (num != 0.0)
		{
			AlignmentRules.Change(_engine.Player, num);
			foreach (Combatant combatant in _engine.Combatants)
			{
				if (combatant.Kind == CombatantKind.Ally)
				{
					AlignmentRules.Change(combatant, num);
				}
			}
			SlabLog($"[color=#e2938f]你擊殺了{((num <= -10000.0) ? "正義" : "中立")}玩家「{victim.Disp}」，性向值 {num:+0;−0}。[/color]");
		}
		else
		{
			SlabLog("[color=#8fdd8f]你擊敗了邪惡玩家「" + victim.Disp + "」。[/color]");
		}
		if (HostilePlayerRules.KillMarksKillerWanted(victim.Alignment))
		{
			if (alignment >= 32767.0)
			{
				_session.LastPlayerKillUnixSeconds = 0L;
			}
			else
			{
				_session.LastPlayerKillUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				SlabLog("[color=#ff9a8a]你因殺害白名／藍名玩家 NPC 被通緝了——警衛會追殺你，死亡制裁一次後解除。[/color]");
			}
			if (_engine.Player.ClassId == "elf" && victim.ClassId == "elf")
			{
				_session.LastElfKillUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				SlabLog("[color=#ff9a8a]妖精一族不會饒恕弒親者——森林的守護者已將你除名。[/color]");
			}
			RefreshWantedFlags();
		}
		SaveManager.Save(_session);
	}

	private void SettleGuardExecution(Combatant victim, Combatant? killer)
	{
		if (victim == _engine.Player && HostilePlayerRules.IsGuardExecutioner(killer) && (_engine.Player.WantedByGuards || _engine.Player.WantedForElfGuardians))
		{
			_session.LastPlayerKillUnixSeconds = 0L;
			_session.LastElfKillUnixSeconds = 0L;
			RefreshWantedFlags();
			_engine.Engine.ClearGuardHostilityToward(_engine.Player);
			SaveManager.Save(_session);
			SlabLog("[color=#9fd7a0]警衛已完成一次死亡制裁，通緝解除；再次殺害白名／藍名玩家 NPC 才會重新通緝。[/color]");
		}
	}

	private void ApplyEvilDeathLoss()
	{
		if (_mapRule.Penalty)
		{
			EvilDeathLossRules.Loss loss = EvilDeathLossRules.ApplyLoss(GameDataProvider.Shared, _engine.Player, _hostileRng);
			if ((object)loss != null)
			{
				SaveManager.Save(_session);
				SlabLog("[color=#e2938f]邪惡的代價：「" + loss.DisplayName + "」化為烏有消失了。[/color]");
				_bagRefresh?.Invoke();
			}
		}
	}

	private void TogglePvpFromHotkey()
	{
		SetPvpEnabled(!_session.PvpEnabled);
	}

	private void SetPvpEnabled(bool on)
	{
		if (_session.PvpEnabled != on)
		{
			_session.PvpEnabled = on;
			SyncPvpFlag();
			SaveManager.Save(_session);
			SlabLog(on ? "[color=#ff9a8a]已開啟 PVP——你現在可以攻擊白名／藍名玩家，他們也會還手。[/color]" : "[color=#9fd7a0]已關閉 PVP——白名／藍名玩家不會再與你交戰。[/color]");
		}
	}

	private bool HandlePanelHotkey(InputEventKey key)
	{
		if (!key.Pressed || key.Echo)
		{
			return false;
		}
		if (_paused)
		{
			return false;
		}
		if (IsTextInputFocused())
		{
			return false;
		}
		Key key2 = ((key.Keycode != Key.None) ? key.Keycode : key.PhysicalKeycode);
		if (key.CtrlPressed)
		{
			switch (key2)
			{
			default:
			{
				Key num = key2 - 80;
				if ((ulong)num <= 6uL)
				{
					switch ((int)num)
					{
					case 3:
						ToggleClassicSkills();
						return true;
					case 1:
						QuitToCharacterSelect();
						return true;
					case 6:
						ToggleSettings();
						return true;
					case 0:
						TogglePvpFromHotkey();
						return true;
					}
				}
				return false;
			}
			case Key.A:
				ToggleClassicEquipment();
				return true;
			case Key.B:
				ToggleCollections();
				return true;
			}
		}
		if (key2 == Key.Tab && !key.ShiftPressed && !key.AltPressed)
		{
			ToggleBag();
			return true;
		}
		if (key2 == Key.M && !key.ShiftPressed && !key.AltPressed)
		{
			ToggleWorldAtlas();
			return true;
		}
		return false;
	}

	private bool IsTextInputFocused()
	{
		Control control = GetViewport().GuiGetFocusOwner();
		if (control is LineEdit || control is TextEdit)
		{
			return true;
		}
		return false;
	}

	private void QuitToCharacterSelect()
	{
		if (_onQuitToMenu != null)
		{
			if (_session.Party.Members.Count > 0)
			{
				_session.Party.Synchronize(_engine.Combatants);
			}
			if (_session.Pets.Pets.Count > 0)
			{
				_session.Pets.Synchronize(_engine.Engine);
			}
			SyncActiveDollToSession();
			_weightCd = 0.0;
			WeightStep(0.0);
			SyncAllOfflineCompanions();
			SaveManager.Save(_session);
			_onQuitToMenu();
		}
	}

	private VBoxContainer CreateHuntIdentifyFrame(string title, Vector2 size)
	{
		CloseItemTargetOverlay();
		(Control Root, Control Body) tuple = ClassicMapFrame.Create(new Vector2((_viewW - size.X) / 2f, 24f), size, CloseItemTargetOverlay, 2200);
		Control item = tuple.Root;
		Control item2 = tuple.Body;
		_itemTargetOverlay = item;
		item.AddChild(ClassicMapFrame.Title(title), forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Position = new Vector2(8f, 28f),
			Size = new Vector2(Mathf.Max(0f, item2.Size.X - 16f), Mathf.Max(0f, item2.Size.Y - 36f))
		};
		vBoxContainer.AddThemeConstantOverride("separation", 8);
		item2.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		AddAboveBarPanel(item);
		return vBoxContainer;
	}

	private static HBoxContainer HuntIdentifyTargetRow(ItemStack stack, string displayName, Action choose)
	{
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.CustomMinimumSize = new Vector2(0f, 38f);
		hBoxContainer.AddThemeConstantOverride("separation", 8);
		hBoxContainer.AddChild(ItemIcons.Slot(stack.ItemKey), forceReadableName: false, InternalMode.Disabled);
		Label label = ItemPanelLabel(displayName, "#c9d1de", 14, 34f);
		label.CustomMinimumSize = new Vector2(400f, 34f);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = "鑑定",
			CustomMinimumSize = new Vector2(72f, 32f)
		};
		button.Pressed += choose;
		hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		return hBoxContainer;
	}

	private void OpenHuntIdentifyScrollPanel(string scrollUid, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		if (itemStack == null || !L1jIdentifyRules.IsScroll(shared, itemStack.ItemKey))
		{
			return;
		}
		VBoxContainer vBoxContainer = CreateHuntIdentifyFrame("鑑定卷軸", new Vector2(640f, 500f));
		IReadOnlyList<ItemStack> readOnlyList = L1jIdentifyRules.EligibleTargets(_engine.Player, scrollUid);
		int unappraisedCount = readOnlyList.Count((ItemStack s) => !s.IsIdentified);
		int scrollTotal = _engine.Player.InventoryStacks.Where((ItemStack s) => L1jIdentifyRules.IsScroll(shared, s.ItemKey) && !s.Locked).Sum((ItemStack s) => (int)s.Quantity);
		HBoxContainer headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 10);
		Label descLabel = ItemPanelLabel("選擇要鑑定的物品。選擇後會顯示能力並消耗一張卷軸；已鑑定物品仍可再次查看。", "#c9d1de", 14, 44f);
		descLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		headerRow.AddChild(descLabel, forceReadableName: false, InternalMode.Disabled);
		Button btnAll = ClassicArtButtons.CreateIdentifyAllButton(unappraisedCount, scrollTotal, delegate
		{
			IdentifyAllHuntItems(scrollUid);
		});
		headerRow.AddChild(btnAll, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(headerRow, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(ItemPanelLabel(message, "#e2938f", 14, 36f), forceReadableName: false, InternalMode.Disabled);
		}
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(ItemPanelLabel("沒有可以鑑定的其他物品。", "#8b95a6", 14, 40f), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 290f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			string text = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, captured);
			vBoxContainer2.AddChild(HuntIdentifyTargetRow(captured, HuntEquippedMark(captured) + text, delegate
			{
				IdentifyHuntItem(scrollUid, captured.Uid);
			}), forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void IdentifyAllHuntItems(string scrollUid)
	{
		GameData shared = GameDataProvider.Shared;
		L1jIdentifyRules.L1jIdentifyAllResult res = L1jIdentifyRules.TryIdentifyAll(shared, _engine.Player, scrollUid);
		if (res.IdentifiedCount > 0)
		{
			SaveManager.Save(_session);
			_bagRefresh?.Invoke();
			string summary = $"✓ 成功鑑定 {res.IdentifiedCount} 件物品（消耗 {res.ScrollsConsumed} 張卷軸）";
			if (res.RemainingUnidentified > 0)
			{
				summary += $"，卷軸已用盡（尚有 {res.RemainingUnidentified} 件未鑑定）";
			}
			SlabLog($"[color=#8fdd8f]{summary}[/color]");
			OpenHuntIdentifyScrollPanel(scrollUid, summary);
		}
		else
		{
			OpenHuntIdentifyScrollPanel(scrollUid, "沒有可以鑑定的未鑑定物品或卷軸不足。");
		}
	}

	private void IdentifyHuntItem(string scrollUid, string targetUid)
	{
		GameData shared = GameDataProvider.Shared;
		L1jIdentifyResult result = L1jIdentifyRules.TryIdentify(shared, _engine.Player, scrollUid, targetUid);
		if (!result.Attempted)
		{
			OpenHuntIdentifyScrollPanel(scrollUid, IdentifyScrollText.Failure(result.Failure));
			return;
		}
		SaveManager.Save(_session);
		_bagRefresh?.Invoke();
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == result.TargetUid) ?? _engine.Player.EquippedItems.Values.FirstOrDefault((ItemStack stack) => stack.Uid == result.TargetUid);
		string text = ((itemStack == null) ? "鑑定完成，但物品已不在身上。" : IdentifyScrollText.Describe(shared, itemStack, result.NewlyIdentified));
		SlabLog("[color=#8fdd8f]" + text.Replace("\n", "\u3000") + "[/color]");
		VBoxContainer vBoxContainer = CreateHuntIdentifyFrame("鑑定結果", new Vector2(600f, 400f));
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", 10);
		if (itemStack != null)
		{
			hBoxContainer.AddChild(ItemIcons.Slot(itemStack.ItemKey), forceReadableName: false, InternalMode.Disabled);
		}
		Label label = ItemPanelLabel(text, "#8fdd8f", 14, 270f);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		bool flag = _engine.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == scrollUid);
		vBoxContainer.AddChild(ClassicArtButtons.Confirm(flag ? ((Action)delegate
		{
			OpenHuntIdentifyScrollPanel(scrollUid);
		}) : new Action(CloseItemTargetOverlay), flag ? "繼續鑑定" : "關閉"), forceReadableName: false, InternalMode.Disabled);
	}

	private void PrepareIntegratedTownEntry()
	{
		IntegratedTownDefinition integratedTownDefinition = IntegratedTownCatalog.FindByTown(_session.TownKey);
		if ((object)integratedTownDefinition == null)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology != null && string.Equals(integratedTownDefinition.MapKey, _mapKey, StringComparison.Ordinal) && string.Equals(_session.TownKey, integratedTownDefinition.TownKey, StringComparison.Ordinal))
		{
			EnsureIntegratedTownEntryCache(topology);
			if (!_integratedTownEntryCells.TryGetValue(integratedTownDefinition.TownKey, out (int, int) value))
			{
				throw new InvalidDataException("Integrated town '" + integratedTownDefinition.TownKey + "' has no cached entry cell.");
			}
			var (localX, localY) = value;
			var (x, y) = topology.DisplayPixelCenter(localX, localY);
			_engine.Player.Pos = new WorldPoint(x, y);
			_engine.StopPlayer();
		}
	}

	private void EnsureNpcPanelHost()
	{
		if (_townNpcHost == null)
		{
			_townNpcHost = new TownScreen();
			_townNpcHost.EmbeddedNpcHost = true;
			_overlayUi.AddChild(_townNpcHost, forceReadableName: false, InternalMode.Disabled);
			_townNpcHost.LiveEngine = _engine.Engine;
			_townNpcHost.InitEmbeddedNpcHost(_atlas, _session, WorldView, delegate
			{
				_pendingScreenTransition = _onChangeMap;
			}, delegate
			{
				_pendingScreenTransition = _onExit;
			}, _onExit);
		}
	}

	private static (int X, int Y) TownEntryCell(MapTopology topology, IntegratedTownDefinition definition)
	{
		(int, int)? entryGameCell = definition.EntryGameCell;
		if (entryGameCell.HasValue)
		{
			var (num, num2) = entryGameCell.GetValueOrDefault();
			var (num3, num4) = topology.ToLocalCell(num, num2);
			if (!topology.IsWalkableCell(num3, num4))
			{
				throw new InvalidDataException($"Integrated town '{definition.TownKey}' entry cell ({num},{num2}) is not walkable.");
			}
			return (X: num3, Y: num4);
		}
		if (definition.EntryLandmarkId == null)
		{
			return TownWorldLayout.SafeZoneCenter(topology);
		}
		MapLandmark mapLandmark = topology.Landmarks.FirstOrDefault((MapLandmark candidate) => string.Equals(candidate.Id, definition.EntryLandmarkId, StringComparison.Ordinal));
		if (string.IsNullOrEmpty(mapLandmark.Id) || !topology.IsWalkableCell(mapLandmark.LocalX, mapLandmark.LocalY))
		{
			throw new InvalidDataException($"Integrated town '{definition.TownKey}' has no walkable entry landmark '{definition.EntryLandmarkId}'.");
		}
		return TownWorldLayout.SafeZoneCenterNear(topology, mapLandmark.LocalX, mapLandmark.LocalY);
	}

	private void EnsureIntegratedTownEntryCache(MapTopology topology)
	{
		if (_integratedTownEntryCacheTopology == topology)
		{
			return;
		}
		_integratedTownEntryCacheTopology = topology;
		_integratedTownEntryCells.Clear();
		foreach (IntegratedTownDefinition integratedTown in IntegratedTowns)
		{
			_integratedTownEntryCells[integratedTown.TownKey] = TownEntryCell(topology, integratedTown);
		}
		_integratedTownLocation = null;
		_integratedTownLocationSafe = null;
		_integratedTownLocationHasCell = false;
	}

	private IntegratedTownDefinition? ResolveIntegratedTownLocation(MapTopology topology, bool safe)
	{
		EnsureIntegratedTownEntryCache(topology);
		if (!safe)
		{
			_integratedTownLocationSafe = false;
			_integratedTownLocation = CurrentIntegratedTown;
			_integratedTownLocationHasCell = false;
			return _integratedTownLocation;
		}
		if (!topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY))
		{
			return CurrentIntegratedTown;
		}
		if (_integratedTownLocationSafe == true && _integratedTownLocationHasCell && _integratedTownLocationCellX == localX && _integratedTownLocationCellY == localY)
		{
			return _integratedTownLocation;
		}
		IntegratedTownDefinition integratedTownDefinition = null;
		int num = int.MaxValue;
		foreach (IntegratedTownDefinition integratedTown in IntegratedTowns)
		{
			if (_integratedTownEntryCells.TryGetValue(integratedTown.TownKey, out (int, int) value))
			{
				(int, int) tuple = value;
				int item = tuple.Item1;
				int item2 = tuple.Item2;
				int num2 = Math.Max(Math.Abs(item - localX), Math.Abs(item2 - localY));
				if (num2 < num)
				{
					integratedTownDefinition = integratedTown;
					num = num2;
				}
			}
		}
		_integratedTownLocationSafe = true;
		_integratedTownLocationHasCell = true;
		_integratedTownLocationCellX = localX;
		_integratedTownLocationCellY = localY;
		_integratedTownLocation = integratedTownDefinition ?? CurrentIntegratedTown;
		return _integratedTownLocation;
	}

	private void SetAreaName(string text)
	{
		if (_areaName != null && !string.Equals(_areaNameText, text, StringComparison.Ordinal))
		{
			_areaNameText = text;
			_areaName.Text = text;
		}
	}

	private void IntegratedTownStep(double delta)
	{
		MapTopology topology = _topology;
		if (topology == null || IntegratedTowns.Count == 0)
		{
			return;
		}
		_integratedTownCd -= delta;
		if (!(_integratedTownCd > 0.0))
		{
			_integratedTownCd = 0.1;
			bool flag = _engine.Engine.IsSafeZone(_engine.Player.Pos) || !topology.HasSafeZone;
			PerfProbeMark(19);
			IntegratedTownDefinition integratedTownDefinition = ResolveIntegratedTownLocation(topology, flag);
			PerfProbeMark(20);
			if (flag && (object)integratedTownDefinition != null)
			{
				ActivateIntegratedTown(integratedTownDefinition);
			}
			if ((object)integratedTownDefinition != null)
			{
				SetAreaName(flag ? integratedTownDefinition.SafeAreaName : integratedTownDefinition.HuntingAreaName);
			}
			PerfProbeMark(21);
			RefreshWorldAtlasLocation(flag && (object)integratedTownDefinition != null);
			PerfProbeMark(22);
		}
	}

	private void ActivateIntegratedTown(IntegratedTownDefinition definition)
	{
		_townNpcHost?.SetEmbeddedTownContext(definition.TownKey);
		if (!string.Equals(_session.TownKey, definition.TownKey, StringComparison.Ordinal))
		{
			_session.TownKey = definition.TownKey;
			SaveManager.Save(_session);
		}
	}

	private bool SyncIntegratedTownMusic(ref bool force)
	{
		MapTopology topology = _topology;
		if (topology == null || IntegratedTowns.Count == 0)
		{
			return false;
		}
		bool flag = _engine.Engine.IsSafeZone(_engine.Player.Pos) || !topology.HasSafeZone;
		if (_integratedTownSafeState != flag)
		{
			_integratedTownSafeState = flag;
			force = true;
		}
		if (!flag)
		{
			return false;
		}
		IntegratedTownDefinition integratedTownDefinition = ResolveIntegratedTownLocation(topology, safe: true);
		if ((object)integratedTownDefinition == null)
		{
			return false;
		}
		if (force)
		{
			GameAudio instance = GameAudio.Instance;
			if (instance != null)
			{
				(int GameX, int GameY) tuple = CurrentGameCell();
				int item = tuple.GameX;
				int item2 = tuple.GameY;
				string scene = MapMusicCatalog.ResolveZone(GameDataProvider.Shared, _mapKey, item, item2) ?? MapMusicCatalog.ResolveMapTrack(GameDataProvider.Shared, _mapKey) ?? instance.TownScene(integratedTownDefinition.TownKey);
				instance.PlayScene(scene);
			}
		}
		return true;
	}

	private bool TryResolveL1jTeleportTarget(int mapId, int gameX, int gameY, out string mapKey, out double worldX, out double worldY)
	{
		mapKey = "";
		worldX = 0.0;
		worldY = 0.0;
		L1jMapRuleCatalog l1jMapRuleCatalog = L1jMapRuleCatalog.Load(GameDataProvider.Shared);
		List<string> candidateKeys = new List<string>();

		if (l1jMapRuleCatalog.TryForMapId(mapId, out L1jMapRule rule) && (object)rule != null)
		{
			candidateKeys.AddRange(l1jMapRuleCatalog.RuntimeTargetKeys(mapId));
		}
		candidateKeys.Add($"l1j_map_{mapId}");
		try
		{
			string cPath = "res://data/l1j-classic-maps.json";
			if (DataFileSystem.Exists(cPath))
			{
				var classicDoc = JsonNode.Parse(DataFileSystem.ReadAllText(cPath))?.AsObject();
				if (classicDoc?["maps"] is JsonArray cMaps)
				{
					foreach (var cm in cMaps)
					{
						if (cm?["mapId"]?.GetValue<int>() == mapId && cm?["mapKey"]?.GetValue<string>() is string mk && !string.IsNullOrEmpty(mk))
						{
							candidateKeys.Add(mk);
						}
					}
				}
			}
		}
		catch { }

		try
		{
			string mapsRoot = GodotDataFiles.TryResolveDiskPath("res://assets/maps") ?? "";
			if (!string.IsNullOrEmpty(mapsRoot) && Directory.Exists(mapsRoot))
			{
				foreach (var dir in Directory.GetDirectories(mapsRoot))
				{
					string mjPath = Path.Combine(dir, "map.json");
					if (File.Exists(mjPath))
					{
						try
						{
							var mj = JsonNode.Parse(File.ReadAllText(mjPath))?.AsObject();
							if (mj?["mapId"]?.GetValue<int>() == mapId)
							{
								candidateKeys.Add(Path.GetFileName(dir));
								if (mj?["mapKey"]?.GetValue<string>() is string extraKey && !string.IsNullOrEmpty(extraKey))
								{
									candidateKeys.Add(extraKey);
								}
							}
						}
						catch { }
					}
				}
			}
		}
		catch { }

		string text = "";
		MapTopology mapTopology = null;
		long num = long.MaxValue;
		foreach (string item3 in candidateKeys.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			MapTopology mapTopology2 = (string.Equals(item3, _mapKey, StringComparison.Ordinal) ? _topology : TryLoadTopology(item3));
			if (mapTopology2 != null)
			{
				bool cellMatched = mapTopology2.ContainsGameCell(gameX, gameY) || mapTopology2.ContainsLocalCell(gameX, gameY);
				if (cellMatched)
				{
					long num2 = (long)mapTopology2.WidthCells * (long)mapTopology2.HeightCells;
					if (num2 < num)
					{
						text = item3;
						mapTopology = mapTopology2;
						num = num2;
					}
				}
				else if (mapTopology == null)
				{
					text = item3;
					mapTopology = mapTopology2;
				}
			}
		}
		if (mapTopology == null)
		{
			return false;
		}
		int localX;
		int localY;
		if (mapTopology.ContainsGameCell(gameX, gameY) && mapTopology.IsWalkableCell(gameX - mapTopology.GameOriginX, gameY - mapTopology.GameOriginY))
		{
			localX = gameX - mapTopology.GameOriginX;
			localY = gameY - mapTopology.GameOriginY;
		}
		else if (mapTopology.ContainsLocalCell(gameX, gameY) && mapTopology.IsWalkableCell(gameX, gameY))
		{
			localX = gameX;
			localY = gameY;
		}
		else if (mapTopology.ContainsLocalCell(gameX, gameY))
		{
			localX = gameX;
			localY = gameY;
		}
		else if (mapTopology.ContainsGameCell(gameX, gameY))
		{
			localX = gameX - mapTopology.GameOriginX;
			localY = gameY - mapTopology.GameOriginY;
		}
		else
		{
			var (safeX, safeY) = mapTopology.FindDefaultSpawnLocalCell();
			localX = safeX;
			localY = safeY;
		}
		(double, double) tuple2 = mapTopology.DisplayPixelCenter(localX, localY);
		worldX = tuple2.Item1;
		worldY = tuple2.Item2;
		mapKey = text;
		return true;
	}

	private void BuildL1jWorldNpcs(MapTopology topology)
	{
		_pendingWorldNpcs.Clear();
		_liveWorldNpcs.Clear();
		_worldNpcCombatVisuals.Clear();
		_worldNpcStaticVisuals.Clear();
		_opaqueWorldObjectVisualsByXBucket.Clear();
		ClearRoiEscortVisual();
		_worldNpcNameplateOffsets.Clear();
		_worldNpcBornSeq = 0;
		BuildWorldNpcOccluderIndex(topology);
		if (!OwnsL1jWorldNpcs)
		{
			return;
		}
		IGameData data = GameDataProvider.Shared;
		foreach (L1jNpcSpawn item3 in L1jWorldNpcCatalog.SpawnsOn(data, _mapKey))
		{
			if (!topology.ContainsLocalCell(item3.CellX, item3.CellY))
			{
				GD.PushWarning($"[世界NPC] {item3.Name}（npcid {item3.NpcId}）的原版格 ({item3.CellX},{item3.CellY}) 不在 {_mapKey} 網格內——不生成，也不另找落點。");
				continue;
			}
			var (item, item2) = topology.DisplayPixelCenter(item3.CellX, item3.CellY);
			if (IsActiveCastleSpawn(item3, out CastleWarDefinition castle))
			{
				CastleWarObjectKind kind = ((!CastleWarRules.IsTower(item3)) ? CastleWarObjectKind.Defender : CastleWarRules.TowerKind(castle, item3));
				if (CastleWarStore.Book.IsDestroyed(CastleWarRules.NpcKey(kind, item3)))
				{
					continue;
				}
			}
			_pendingWorldNpcs.Add((item, item2, item3));
		}
		if (_pendingWorldNpcs.Count != 0)
		{
			if (_pendingWorldNpcs.Any<(double, double, L1jNpcSpawn)>(((double X, double Y, L1jNpcSpawn Npc) entry) => L1jWorldNpcCatalog.HasShopOffers(data, entry.Npc.NpcId)))
			{
				EnsureNpcPanelHost();
			}
			MaterialiseNearbyWorldNpcs();
		}
	}

	private void L1jWorldNpcStep(double delta)
	{
		if (_engine == null || !OwnsL1jWorldNpcs)
		{
			return;
		}
		RoiEscortStep(delta);
		_worldNpcCd -= delta;
		if (_worldNpcCd > 0.0)
		{
			return;
		}
		_worldNpcCd = 0.1;
		RefreshWantedFlags();
		DematerialiseDistantWorldNpcs();
		RestoreFallenWorldNpcs();
		MaterialiseNearbyWorldNpcs();
		KeyValuePair<Combatant, L1jNpcCombatSpritePlayer>[] array = _worldNpcCombatVisuals.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<Combatant, L1jNpcCombatSpritePlayer> keyValuePair = array[i];
			var (combatant2, l1jNpcCombatSpritePlayer2) = keyValuePair;
			if (!_engine.Engine.Combatants.Contains(combatant2) && (!combatant2.Dead || l1jNpcCombatSpritePlayer2.DeathFinished))
			{
				l1jNpcCombatSpritePlayer2.Root.QueueFree();
				_worldNpcCombatVisuals.Remove(combatant2);
				continue;
			}
			Vector2 vector = new Vector2((float)combatant2.Pos.X, (float)combatant2.Pos.Y);
			l1jNpcCombatSpritePlayer2.Root.Position = vector;
			l1jNpcCombatSpritePlayer2.Root.ZIndex = Math.Max(ResolveWorldNpcDepth(vector, l1jNpcCombatSpritePlayer2.ContentHeight), ResolveOpaqueWorldObjectActorDepthFloor(combatant2.Pos));
			if (combatant2.IsAlive)
			{
				l1jNpcCombatSpritePlayer2.DriveLoop(_engine.RenderWalk(combatant2).Stepping, combatant2.Facing8);
			}
		}
	}

	private void DematerialiseDistantWorldNpcs()
	{
		if (_engine == null || _liveWorldNpcs.Count == 0)
		{
			return;
		}
		WorldPoint pos = _engine.Player.Pos;
		double num = 4000000.0;
		foreach (var (num2, num3, _, combatant) in _liveWorldNpcs)
		{
			if (combatant != null && !combatant.Dead)
			{
				double num4 = num2 - pos.X;
				double num5 = num3 - pos.Y;
				if (!(num4 * num4 + num5 * num5 <= num) && !_engine.Engine.IsExplorationMobEngaged(combatant))
				{
					_engine.Engine.Remove(combatant);
				}
			}
		}
	}

	private void RestoreFallenWorldNpcs()
	{
		if (_engine == null || _liveWorldNpcs.Count == 0)
		{
			return;
		}
		for (int num = _liveWorldNpcs.Count - 1; num >= 0; num--)
		{
			var (item, item2, item3, combatant) = _liveWorldNpcs[num];
			if (combatant != null && !_engine.Engine.Combatants.Contains(combatant) && (!_worldNpcCombatVisuals.TryGetValue(combatant, out L1jNpcCombatSpritePlayer value) || !combatant.Dead || value.DeathFinished))
			{
				_liveWorldNpcs.RemoveAt(num);
				if (combatant.CastleWarObjectKey.Length <= 0 || !CastleWarStore.Book.IsDestroyed(combatant.CastleWarObjectKey))
				{
					_pendingWorldNpcs.Add((item, item2, item3));
				}
			}
		}
	}

	private void MaterialiseNearbyWorldNpcs()
	{
		if (_pendingWorldNpcs.Count == 0 || _engine == null)
		{
			return;
		}
		WorldPoint pos = _engine.Player.Pos;
		double num = 1960000.0;
		EnsureNearbyWorldNpcNameplateOffsets(pos, num);
		bool flag = false;
		for (int num2 = _pendingWorldNpcs.Count - 1; num2 >= 0; num2--)
		{
			(double X, double Y, L1jNpcSpawn Npc) tuple = _pendingWorldNpcs[num2];
			double item = tuple.X;
			double item2 = tuple.Y;
			L1jNpcSpawn item3 = tuple.Npc;
			double num3 = item - pos.X;
			double num4 = item2 - pos.Y;
			if (num3 * num3 + num4 * num4 > num)
			{
				continue;
			}
			_pendingWorldNpcs.RemoveAt(num2);
			Combatant combatant = null;
			CastleWarDefinition castle = null;
			bool flag2 = IsActiveCastleSpawn(item3, out castle);
			if (flag2 && CastleWarRules.IsTower(item3))
			{
				CastleWarObjectKind kind = CastleWarRules.TowerKind(castle, item3);
				string text = CastleWarRules.NpcKey(kind, item3);
				if (CastleWarStore.Book.IsDestroyed(text))
				{
					continue;
				}
				int bornSeq = ++_worldNpcBornSeq;
				combatant = CastleWarRules.CreateStructure(text, item3.Name, $"gfx:{item3.Gfx}", kind, castle.Id, item3.Hp, new WorldPoint(item, item2), bornSeq);
				ConfigureCastleWarActor(combatant, item3, castle);
				_engine.Engine.Add(combatant);
				BuildL1jWorldNpcVisual(item, item2, item3, combat: true, out L1jNpcCombatSpritePlayer combatVisual);
				if (combatVisual != null)
				{
					_worldNpcCombatVisuals[combatant] = combatVisual;
				}
			}
			else if (L1jWorldNpcCombatRules.IsCombatNpc(item3) && L1jWorldNpcCombatRules.SpawnsAsCombatant(item3, _engine.Player.Level))
			{
				int num5 = ++_worldNpcBornSeq;
				combatant = L1jWorldNpcCombatRules.Create(GameDataProvider.Shared, item3, $"world-npc:{_mapKey}:{item3.NpcId}:{item3.CellX}:{item3.CellY}:{num5}", num5, new WorldPoint(item, item2));
				if (flag2)
				{
					ConfigureCastleWarActor(combatant, item3, castle);
				}
				_engine.Engine.Add(combatant);
				if (ResolveMobAtlas(_atlas, combatant.Avatar).Length == 0)
				{
					BuildL1jWorldNpcVisual(item, item2, item3, combat: true, out L1jNpcCombatSpritePlayer combatVisual2);
					if (combatVisual2 != null)
					{
						_worldNpcCombatVisuals[combatant] = combatVisual2;
					}
				}
			}
			else
			{
				L1jNpcCombatSpritePlayer combatVisual3;
				WorldNpcStaticPresentation worldNpcStaticPresentation = BuildWorldNpcStaticPresentation(BuildL1jWorldNpcVisual(item, item2, item3, combat: false, out combatVisual3), item3);
				_worldNpcStaticVisuals[(item3.NpcId, item3.CellX, item3.CellY)] = worldNpcStaticPresentation;
				IndexOpaqueWorldObjectVisual(worldNpcStaticPresentation);
			}
			_liveWorldNpcs.Add((item, item2, item3, combatant));
			flag = true;
		}
		if (flag)
		{
			RefreshWorldNpcStaticSolids();
		}
	}

	private void RefreshWorldNpcStaticSolids()
	{
		if (_engine == null)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		HashSet<(int, int)> hashSet = new HashSet<(int, int)>();
		foreach (var liveWorldNpc in _liveWorldNpcs)
		{
			L1jNpcSpawn item = liveWorldNpc.Npc;
			if (liveWorldNpc.Actor != null)
			{
				continue;
			}
			(int, int, int) key = (item.NpcId, item.CellX, item.CellY);
			if (_worldNpcStaticVisuals.TryGetValue(key, out var value) && !value.Root.Visible)
			{
				continue;
			}
			hashSet.Add((item.CellX, item.CellY));
			if (!_worldNpcStaticVisuals.TryGetValue(key, out value))
			{
				continue;
			}
			L1jWorldObjectVisualBounds bounds = CoreVisualBounds(value.LocalVisualBounds);
			if (!L1jWorldObjectPresentationRules.UsesExpandedSolidFootprint(item.Impl, value.StaticSprite, bounds))
			{
				continue;
			}
			int num = L1jWorldObjectPresentationRules.CandidateCellRadius(bounds);
			(double X, double Y) tuple = topology.DisplayPixelCenter(item.CellX, item.CellY);
			double item2 = tuple.X;
			double item3 = tuple.Y;
			for (int i = item.CellY - num; i <= item.CellY + num; i++)
			{
				for (int j = item.CellX - num; j <= item.CellX + num; j++)
				{
					if (topology.ContainsLocalCell(j, i))
					{
						var (num2, num3) = topology.DisplayPixelCenter(j, i);
						if (L1jWorldObjectPresentationRules.ContainsSolidPoint(bounds, num2 - item2, num3 - item3))
						{
							hashSet.Add((j, i));
						}
					}
				}
			}
		}
		_engine.Engine.SetStaticSolidBodies(hashSet.Select<(int, int), WorldPoint>(delegate((int X, int Y) cell)
		{
			var (x, y) = topology.DisplayPixelCenter(cell.X, cell.Y);
			return new WorldPoint(x, y);
		}));
	}

	private static L1jWorldObjectVisualBounds CoreVisualBounds(Rect2 bounds)
	{
		return new L1jWorldObjectVisualBounds(bounds.Position.X, bounds.Position.Y, bounds.End.X, bounds.End.Y);
	}

	private static WorldNpcStaticPresentation BuildWorldNpcStaticPresentation(Node2D root, L1jNpcSpawn npc)
	{
		L1jGfxSprite sprite;
		bool staticSprite = L1jNpcSpriteCatalog.TryGet(GameDataProvider.Shared, npc.Gfx, out sprite) && sprite.Static;
		Rect2 bounds;
		Rect2 localVisualBounds = (L1jNpcSpriteRenderer.TryMeasureVisualBounds(GameDataProvider.Shared, npc.Gfx, npc.Heading, out bounds) ? bounds : default(Rect2));
		return new WorldNpcStaticPresentation(root, localVisualBounds, staticSprite);
	}

	private void IndexOpaqueWorldObjectVisual(WorldNpcStaticPresentation presentation)
	{
		Rect2 localVisualBounds = presentation.LocalVisualBounds;
		if (localVisualBounds.Size.X <= 0f || localVisualBounds.Size.Y <= 0f)
		{
			return;
		}
		float num = presentation.Root.Position.X + localVisualBounds.Position.X;
		float worldX = num + localVisualBounds.Size.X;
		int num2 = WorldNpcOcclusionBucket(num);
		int num3 = WorldNpcOcclusionBucket(worldX);
		for (int i = num2; i <= num3; i++)
		{
			if (!_opaqueWorldObjectVisualsByXBucket.TryGetValue(i, out List<WorldNpcStaticPresentation> value))
			{
				value = new List<WorldNpcStaticPresentation>();
				_opaqueWorldObjectVisualsByXBucket[i] = value;
			}
			value.Add(presentation);
		}
	}

	private int ResolveOpaqueWorldObjectActorDepthFloor(WorldPoint position)
	{
		Rect2 b = new Rect2(new Vector2((float)position.X - 24f, (float)position.Y - 96f), new Vector2(48f, 100f));
		int num = int.MinValue;
		int num2 = WorldNpcOcclusionBucket(b.Position.X);
		int num3 = WorldNpcOcclusionBucket(b.End.X);
		for (int i = num2; i <= num3; i++)
		{
			if (!_opaqueWorldObjectVisualsByXBucket.TryGetValue(i, out List<WorldNpcStaticPresentation> value))
			{
				continue;
			}
			foreach (WorldNpcStaticPresentation item in value)
			{
				Node2D root = item.Root;
				if (root.Visible && !(root.Modulate.A < 0.999f) && !(item.LocalVisualBounds.Size.X <= 0f) && !(item.LocalVisualBounds.Size.Y <= 0f))
				{
					Rect2 rect = new Rect2(root.Position + item.LocalVisualBounds.Position, item.LocalVisualBounds.Size);
					if (rect.Intersects(b))
					{
						num = Math.Max(num, root.ZIndex + 1);
					}
				}
			}
		}
		return num;
	}

	private void PlayL1jWorldNpcAttack(Combatant? actor)
	{
		if (actor != null && _worldNpcCombatVisuals.TryGetValue(actor, out L1jNpcCombatSpritePlayer value) && value.PlayAttack(actor.Facing8, _engine.Engine.AttackCycleSeconds(actor), _engine.Engine.AttackSpeedRatio(actor)))
		{
			_engine.Engine.LockAction(actor, value.LastOneShotSeconds);
		}
	}

	private void PlayL1jWorldNpcDamage(Combatant? actor)
	{
		if (actor != null && _worldNpcCombatVisuals.TryGetValue(actor, out L1jNpcCombatSpritePlayer value))
		{
			value.PlayDamage(actor.Facing8);
		}
	}

	private void PlayL1jWorldNpcDeath(Combatant? actor)
	{
		if (actor != null && _worldNpcCombatVisuals.TryGetValue(actor, out L1jNpcCombatSpritePlayer value) && !value.PlayDeath(actor.Facing8))
		{
			value.Root.QueueFree();
			_worldNpcCombatVisuals.Remove(actor);
		}
	}

	private static (string Name, string Title) SplitL1jNpcName(string raw)
	{
		int num = raw.IndexOf('^');
		if (num >= 0)
		{
			return (Name: raw.Substring(num + 1), Title: raw.Substring(0, num));
		}
		return (Name: raw, Title: "");
	}

	private static (string Name, string Title) L1jNpcDisplayName(L1jNpcSpawn npc)
	{
		return SplitL1jNpcName(npc.Name);
	}

	private static float WorldNpcNameplateWidth(string name, string title)
	{
		int val = name.Length * 14 + 18;
		int val2 = ((title.Length != 0) ? ((title.Length + 2) * 12 + 16) : 0);
		return Math.Clamp(Math.Max(val, val2), 72, 180);
	}

	private (float Top, float CenterX) WorldNpcNameplateAnchor(L1jNpcSpawn npc)
	{
		float contentHeight;
		float centerX;
		bool flag = L1jNpcSpriteRenderer.TryMeasureNameplateAnchor(GameDataProvider.Shared, npc.Gfx, npc.Heading, out contentHeight, out centerX);
		return (Top: 0f - (flag ? contentHeight : 54f) - 40f, CenterX: flag ? centerX : 0f);
	}

	private Rect2 WorldNpcNameplateRect(double worldX, double worldY, L1jNpcSpawn npc, float offset)
	{
		(string Name, string Title) tuple = L1jNpcDisplayName(npc);
		string item = tuple.Name;
		string item2 = tuple.Title;
		float num = WorldNpcNameplateWidth(item, item2);
		float height = ((item2.Length == 0) ? 22 : 40);
		var (num2, num3) = WorldNpcNameplateAnchor(npc);
		return new Rect2((float)worldX + num3 - num / 2f, (float)worldY + num2 + offset, num, height);
	}

	private void EnsureNearbyWorldNpcNameplateOffsets(WorldPoint player, double buildSquared)
	{
		(double X, double Y, L1jNpcSpawn Npc)[] array = (from entry in _pendingWorldNpcs
			where (entry.X - player.X) * (entry.X - player.X) + (entry.Y - player.Y) * (entry.Y - player.Y) <= buildSquared
			orderby entry.Y, entry.X, entry.Npc.NpcId
			select entry).ToArray();
		if (array.Length == 0)
		{
			return;
		}
		List<Rect2> list = new List<Rect2>();
		foreach (var (num, num2, l1jNpcSpawn, _) in _liveWorldNpcs)
		{
			if (ShowsWorldNpcNameplate(l1jNpcSpawn))
			{
				double num3 = num - player.X;
				double num4 = num2 - player.Y;
				if (!(num3 * num3 + num4 * num4 > buildSquared))
				{
					list.Add(WorldNpcNameplateRect(num, num2, l1jNpcSpawn, CollectionExtensions.GetValueOrDefault(key: (l1jNpcSpawn.NpcId, l1jNpcSpawn.CellX, l1jNpcSpawn.CellY), dictionary: _worldNpcNameplateOffsets)));
				}
			}
		}
		(double, double, L1jNpcSpawn)[] array2 = array;
		for (int num5 = 0; num5 < array2.Length; num5++)
		{
			(double, double, L1jNpcSpawn) tuple2 = array2[num5];
			double item = tuple2.Item1;
			double item2 = tuple2.Item2;
			L1jNpcSpawn item3 = tuple2.Item3;
			if (!ShowsWorldNpcNameplate(item3))
			{
				continue;
			}
			(int, int, int) key = (item3.NpcId, item3.CellX, item3.CellY);
			if (_worldNpcNameplateOffsets.TryGetValue(key, out var value))
			{
				list.Add(WorldNpcNameplateRect(item, item2, item3, value));
				continue;
			}
			float num6 = 0f;
			Rect2 candidate = WorldNpcNameplateRect(item, item2, item3, num6);
			while (list.Any((Rect2 other) => other.Grow(2f).Intersects(candidate)) && num6 > -120f)
			{
				num6 -= 20f;
				candidate = WorldNpcNameplateRect(item, item2, item3, num6);
			}
			_worldNpcNameplateOffsets[key] = num6;
			list.Add(candidate);
		}
	}

	private Node2D BuildL1jWorldNpcVisual(double worldX, double worldY, L1jNpcSpawn npc, bool combat, out L1jNpcCombatSpritePlayer? combatVisual)
	{
		combatVisual = null;
		Vector2 vector = new Vector2((float)worldX, (float)worldY);
		Node2D node2D = new Node2D
		{
			Position = vector,
			ZIndex = Depth.Of(vector.Y),
			ZAsRelative = false
		};
		_arena.AddChild(node2D, forceReadableName: false, InternalMode.Disabled);
		bool flag;
		float contentHeight;
		if (combat)
		{
			flag = L1jNpcSpriteRenderer.TryAddCombatSprite(node2D, GameDataProvider.Shared, npc.Gfx, npc.Heading, out combatVisual, out contentHeight);
			if (!flag)
			{
				flag = L1jNpcSpriteRenderer.TryAddSprite(node2D, GameDataProvider.Shared, npc.Gfx, npc.Heading, out contentHeight);
			}
		}
		else
		{
			flag = L1jNpcSpriteRenderer.TryAddSprite(node2D, GameDataProvider.Shared, npc.Gfx, npc.Heading, out contentHeight);
		}
		if (!flag && _reportedMissingNpcArt.Add($"l1j:{npc.NpcId}"))
		{
			GD.PushWarning($"[NPC立繪] {npc.Name}（npcid {npc.NpcId}·gfx {npc.Gfx}）缺立繪" + "——只顯示名牌（禁止拿別的 NPC 立繪補洞）。");
		}
		if (flag && L1jNpcSpriteCatalog.TryGet(GameDataProvider.Shared, npc.Gfx, out L1jGfxSprite sprite) && sprite.Static && L1jNpcSpriteRenderer.TryMeasureVisualBounds(GameDataProvider.Shared, npc.Gfx, npc.Heading, out var bounds))
		{
			double worldY2 = L1jWorldObjectPresentationRules.DepthAnchorY(vector.Y, staticSprite: true, CoreVisualBounds(bounds));
			node2D.ZIndex = Depth.Of(worldY2);
		}
		else
		{
			node2D.ZIndex = ResolveWorldNpcDepth(vector, flag ? Math.Max(54f, contentHeight) : 54f);
		}
		bool flag2 = WorldNpcIsInteractive(npc);
		if (flag2)
		{
			L1jNpcSpawn clicked = npc;
			Vector2 clickPos = new Vector2(-20f, -55f);
			Vector2 clickSize = new Vector2(40f, 55f);
			if (L1jNpcSpriteRenderer.TryMeasureVisualBounds(GameDataProvider.Shared, npc.Gfx, npc.Heading, out Rect2 vBounds) && vBounds.Size.X > 0 && vBounds.Size.Y > 0)
			{
				clickPos = vBounds.Position;
				clickSize = vBounds.Size;
			}
			Control clickArea = new Control
			{
				Position = clickPos,
				Size = clickSize,
				MouseFilter = MouseFilterEnum.Stop
			};
			clickArea.GuiInput += delegate(InputEvent @event)
			{
				if (@event is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed)
				{
					clickArea.AcceptEvent();
					if (_worldNpcPanel == null)
					{
						_engine.StopPlayer();
						OpenL1jWorldNpcPanel(clicked);
					}
				}
			};
			node2D.AddChild(clickArea, forceReadableName: false, InternalMode.Disabled);
		}
		if (!L1jWorldNpcCombatRules.ShowsPersistentNameplate(npc, flag2))
		{
			return node2D;
		}
		(string Name, string Title) tuple = L1jNpcDisplayName(npc);
		string item = tuple.Name;
		string item2 = tuple.Title;
		float num = WorldNpcNameplateWidth(item, item2);
		float valueOrDefault = _worldNpcNameplateOffsets.GetValueOrDefault((npc.NpcId, npc.CellX, npc.CellY));
		(float Top, float CenterX) tuple2 = WorldNpcNameplateAnchor(npc);
		float item3 = tuple2.Top;
		float item4 = tuple2.CenterX;
		float plateHeight = string.IsNullOrEmpty(item2) ? 22f : 38f;
		string plateText = string.IsNullOrEmpty(item2) ? item : $"{item2}\n{item}";
		Label plate = new Label
		{
			Text = plateText,
			Position = new Vector2(item4 - num / 2f, item3 + valueOrDefault),
			Size = new Vector2(num, plateHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = (MouseFilterEnum)(flag2 ? 0 : 2)
		};
		if (flag2)
		{
			L1jNpcSpawn clicked = npc;
			plate.GuiInput += delegate(InputEvent @event)
			{
				if (@event is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed)
				{
					plate.AcceptEvent();
					if (_worldNpcPanel == null)
					{
						_engine.StopPlayer();
						OpenL1jWorldNpcPanel(clicked);
					}
				}
			};
		}
		ClassicWorldText.Apply(plate, flag2 ? Color.FromHtml("#f0cf73".AsSpan()) : Color.FromHtml("#d5dbe5".AsSpan()), 13);
		node2D.AddChild(plate, forceReadableName: false, InternalMode.Disabled);

		if (npc.NpcId == 888001)
		{
			L1jNpcSpawn clickedNpc = npc;
			Control shopIconBox = new Control
			{
				Position = new Vector2(item4 - 13f, item3 + valueOrDefault - (string.IsNullOrEmpty(item2) ? 28f : 36f)),
				Size = new Vector2(26f, 26f),
				MouseFilter = MouseFilterEnum.Stop
			};
			shopIconBox.GuiInput += delegate(InputEvent @event)
			{
				if (@event is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed)
				{
					shopIconBox.AcceptEvent();
					if (_worldNpcPanel == null)
					{
						_engine.StopPlayer();
						OpenL1jWorldNpcPanel(clickedNpc);
					}
				}
			};
			Panel shopIconBg = new Panel
			{
				Position = Vector2.Zero,
				Size = new Vector2(26f, 26f),
				MouseFilter = MouseFilterEnum.Ignore
			};
			shopIconBg.AddThemeStyleboxOverride("panel", new StyleBoxFlat
			{
				BgColor = new Color(0.48f, 0.22f, 0.15f, 0.95f),
				BorderColor = new Color(0.96f, 0.94f, 0.90f, 0.95f),
				BorderWidthLeft = 1,
				BorderWidthTop = 1,
				BorderWidthRight = 1,
				BorderWidthBottom = 1,
				CornerRadiusBottomLeft = 4,
				CornerRadiusBottomRight = 4,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4
			});
			shopIconBox.AddChild(shopIconBg);
			Texture2D bagTexture = ItemIcons.For("doll_bag") ?? ItemIcons.For("l1j_item_40568");
			if (bagTexture != null)
			{
				shopIconBox.AddChild(new TextureRect
				{
					Texture = bagTexture,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					Position = new Vector2(2f, 2f),
					Size = new Vector2(22f, 22f),
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					TextureFilter = TextureFilterEnum.Nearest,
					MouseFilter = MouseFilterEnum.Ignore
				});
			}
			else
			{
				Label bagEmoji = new Label
				{
					Text = "💰",
					Position = new Vector2(0f, -1f),
					Size = new Vector2(26f, 26f),
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					MouseFilter = MouseFilterEnum.Ignore
				};
				bagEmoji.AddThemeFontSizeOverride("font_size", 14);
				shopIconBox.AddChild(bagEmoji);
			}
			node2D.AddChild(shopIconBox, forceReadableName: false, InternalMode.Disabled);
		}

		if (item2.Length == 0)
		{
			return node2D;
		}
		Label label = new Label
		{
			Text = "[" + item2 + "]",
			Position = new Vector2(item4 - num / 2f, item3 + 21f + valueOrDefault),
			Size = new Vector2(num, 18f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		ClassicWorldText.Apply(label, Color.FromHtml("#b8dce5".AsSpan()), 11);
		node2D.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		return node2D;
	}

	private void BuildWorldNpcOccluderIndex(MapTopology topology)
	{
		_worldNpcOccludersByXBucket.Clear();
		float num = (float)topology.DisplayScale;
		foreach (MapOcclusionGroup occlusionGroup in topology.OcclusionGroups)
		{
			float worldX = (float)occlusionGroup.PixelX * num - 48f;
			float worldX2 = (float)(occlusionGroup.PixelX + occlusionGroup.PixelWidth) * num + 48f;
			int num2 = WorldNpcOcclusionBucket(worldX);
			int num3 = WorldNpcOcclusionBucket(worldX2);
			for (int i = num2; i <= num3; i++)
			{
				if (!_worldNpcOccludersByXBucket.TryGetValue(i, out List<MapOcclusionGroup> value))
				{
					value = new List<MapOcclusionGroup>();
					_worldNpcOccludersByXBucket[i] = value;
				}
				value.Add(occlusionGroup);
			}
		}
	}

	private static int WorldNpcOcclusionBucket(float worldX)
	{
		return (int)MathF.Floor(worldX / 512f);
	}

	private int ResolveWorldNpcDepth(Vector2 foot, float visibleHeight)
	{
		int num = Depth.Of(foot.Y);
		MapTopology topology = _topology;
		if (topology == null || !_worldNpcOccludersByXBucket.TryGetValue(WorldNpcOcclusionBucket(foot.X), out List<MapOcclusionGroup> value))
		{
			return num;
		}
		if (!value.Any((MapOcclusionGroup group) => MapOcclusionDepthRules.ActorCrossesBaseFromBelow(in group, topology.DisplayScale, foot.X, foot.Y, 48.0, Math.Max(54f, visibleHeight))))
		{
			return num;
		}
		int num2 = Math.Clamp(num, 0, 1400);
		return 1501 + num2 * 299 / 1400;
	}

	private void OpenL1jWorldNpcPanel(L1jNpcSpawn npc, string? requestedHtmlId = null)
	{
		if (_worldNpcPanel != null)
		{
			return;
		}
		IGameData data = GameDataProvider.Shared;
		bool flag = L1jWorldNpcCatalog.HasShopOffers(data, npc.NpcId);
		bool flag2 = L1jWorldNpcCatalog.IsWarehouseKeeper(npc);
		bool flag3 = L1jWorldNpcCatalog.IsHousekeeper(npc);
		bool flag4 = L1jWorldNpcCatalog.IsClanExecutor(npc);
		CastleWarDefinition castleWarDefinition = CastleWarForNpc(npc);
		L1jUbArena arena;
		bool flag5 = L1jUltimateBattleCatalog.Load(data).TryResolveManager(npc.NpcId, out arena);
		if (!CanInteractWith(npc) && !flag5)
		{
			return;
		}
		NpcDialogCatalog.Reload();
		(data as GameData)?.InvalidateTable("NPC_ACTIONS");
		NpcActionCatalog.InvalidateCache();
		(string, string) tuple = L1jNpcDisplayName(npc);
		string name = tuple.Item1;
		string item = tuple.Item2;
		string text = requestedHtmlId;
		if (text == null)
		{
			text = L1jJavaNpcInteractionRules.InitialHtmlId(data, npc.NpcId, _engine.Player) ?? NpcDialogCatalog.DefaultHtmlId(npc.NpcId, _engine.Player.Alignment);
		}
		if (text != null && !NpcDialogCatalog.HasHtmlDialog(text))
		{
			text = null;
		}
		IReadOnlyList<NpcDialogAction> readOnlyList = ((text != null) ? NpcDialogCatalog.ActionsByHtml(text) : NpcDialogCatalog.Actions(npc.NpcId));
		IReadOnlyList<NpcActionDefinition> readOnlyList2 = (from action in (from action in L1jWorldNpcCatalog.ActionsFor(data, npc.NpcId, _engine.Player)
				where npc.NpcId != 80153 || _engine.Player.Level < 13
				where action.Effects.Count > 0 || action.Outputs.Count > 0 || action.Succeed.Count > 0
				select action).ToArray().Concat(from option in readOnlyList
				select NpcActionCatalog.Find(data, npc.NpcId, option.Action, _engine.Player) into action
				where (object)action != null
				select (action))
			where action.Effects.Count > 0 || action.Outputs.Count > 0 || action.Succeed.Count > 0
			group action by (Seq: action.Seq, Name: action.Name) into @group
			select @group.First()).ToArray();
		IReadOnlyList<NpcActionDefinition> craftActions = readOnlyList2.Where((NpcActionDefinition action) => string.Equals(action.Kind, "MakeItem", StringComparison.Ordinal)).ToArray();
		string speaker = ((text != null) ? NpcDialogCatalog.SpeakerLineByHtml(text, name) : (NpcDialogCatalog.HasOriginalDialog(npc.NpcId) ? NpcDialogCatalog.SpeakerLine(npc.NpcId, name) : ((item.Length > 0) ? (name + "\u3000[" + item + "]") : name)));
		IReadOnlyList<string> mainLines = ((text != null) ? NpcDialogCatalog.LinesByHtml(text) : NpcDialogCatalog.Lines(npc.NpcId));
		ClassicNpcDialogHandle classicNpcDialogHandle = ClassicNpcDialogWindow.Create(WorldView, speaker, (npc.NpcId == 80153) ? MainTutorDialogLines(_engine.Player) : FusedDomanDialogLines(npc.NpcId, _engine.Player, mainLines), CloseL1jWorldNpcPanel, 1950);
		Control panel = classicNpcDialogHandle.Root;
		Label status = classicNpcDialogHandle.Status;
		if ((object)castleWarDefinition != null)
		{
			AddCastleWarDialogOptions(castleWarDefinition, classicNpcDialogHandle);
		}
		if (flag5 && (object)arena != null)
		{
			classicNpcDialogHandle.AddOption("參加無限大賽", delegate
			{
				CloseL1jWorldNpcPanel();
				OpenArenaManagerPanel(npc, arena);
			});
		}
		if (npc.NpcId == 888001)
		{
			classicNpcDialogHandle.AddOption("【戰士戰技印記】(戰士專屬 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888018, "福利商人 · 戰士戰技印記 (1元)");
			});
			classicNpcDialogHandle.AddOption("【騎士技術書籍】(騎士專屬 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888012, "福利商人 · 騎士技術書籍 (1元)");
			});
			classicNpcDialogHandle.AddOption("【黑妖精靈水晶】(黑妖專屬 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888015, "福利商人 · 黑暗妖精精靈水晶 (1元)");
			});
			classicNpcDialogHandle.AddOption("【幻術師記憶水晶】(幻術專屬 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888017, "福利商人 · 幻術師記憶水晶 (1元)");
			});
			classicNpcDialogHandle.AddOption("【王族魔法書籍】(王族專屬 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888011, "福利商人 · 王族技能書籍 (1元)");
			});
			classicNpcDialogHandle.AddOption("【妖精精靈水晶】(妖精專屬 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888013, "福利商人 · 妖精精靈水晶 (1元)");
			});
			classicNpcDialogHandle.AddOption("【龍騎士龍之書板】(龍騎專屬 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888016, "福利商人 · 龍騎士龍之書板 (1元)");
			});
			classicNpcDialogHandle.AddOption("【法師魔法書籍】(一般魔法 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888014, "福利商人 · 法師魔法書籍 (1元)");
			});
			classicNpcDialogHandle.AddOption("【全部技能書籍】(全職業法術/水晶/書板/印記 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888002, "福利商人 · 技能書籍 (1元)");
			});
			classicNpcDialogHandle.AddOption("【全部武器】(各式神兵 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888003, "福利商人 · 全部武器 (1元)");
			});
			classicNpcDialogHandle.AddOption("【全部防具裝備】(防具/飾品 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888004, "福利商人 · 全部防具 (1元)");
			});
			classicNpcDialogHandle.AddOption("【全部卷軸】(武防/傳送/變形 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888005, "福利商人 · 全部卷軸 (1元)");
			});
			classicNpcDialogHandle.AddOption("【消耗品專區】(紅綠勇慎/藥水/料理 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888006, "福利商人 · 消耗品專區 (1元)");
			});
			classicNpcDialogHandle.AddOption("【常用道具】(魔杖/燈籠/娃娃/雜貨 1元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888007, "福利商人 · 常用道具 (1元)");
			});
			classicNpcDialogHandle.AddOption("【全部商品】(全部 1 元)", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(888001, "福利商人 · 全部商品 (1元)");
			});

			// Dynamically load any extra categories created in Admin Tool (e.g. 888008 三段加速 or custom shop categories)
			foreach (var kvp in L1jShopCatalog.Shops(data))
			{
				int sId = kvp.Key;
				if ((sId >= 888001 && sId <= 888007) || (sId >= 888011 && sId <= 888018)) continue;
				var sDef = kvp.Value;
				if (sDef.NpcId == 888001 || sId.ToString().StartsWith("88800"))
				{
					int targetShopId = sId;
					string sTitle = string.IsNullOrWhiteSpace(sDef.Name) ? $"自訂商店 #{sId}" : sDef.Name;
					classicNpcDialogHandle.AddOption($"【{sTitle}】", delegate
					{
						CloseL1jWorldNpcPanel();
						EnsureNpcPanelHost();
						_townNpcHost?.OpenL1jShop(targetShopId, sTitle);
					});
				}
			}
		}
		else if (flag)
		{
			classicNpcDialogHandle.AddOption("商店", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(npc.NpcId, name);
			});
		}
		if (flag2 && L1jWorldNpcCatalog.CanUseWarehouse(npc, _engine.Player))
		{
			string text2 = ((npc.NpcId == 60028) ? "妖精倉庫" : "個人倉庫");
			classicNpcDialogHandle.AddOption(text2, delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jWarehouse(name);
			});
			if (ClanStore.Book.Exists)
			{
				classicNpcDialogHandle.AddOption("血盟倉庫", delegate
				{
					CloseL1jWorldNpcPanel();
					EnsureNpcPanelHost();
					_townNpcHost?.OpenL1jClanWarehouse(name);
				});
			}
		}
		if (flag3)
		{
			classicNpcDialogHandle.AddOption("盟屋", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jClanHousekeeper(npc.NpcId, name);
			});
		}
		if (flag4)
		{
			classicNpcDialogHandle.AddOption("血盟", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jClanPanel(name);
			});
		}
		if (L1jNpcSkillLearningRules.IsMainMagicInstructor(npc.NpcId))
		{
			classicNpcDialogHandle.AddOption("商店", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jShop(npc.NpcId, name);
			});
		}
		if (L1jWorldNpcCatalog.IsMainPetKeeper(npc.NpcId))
		{
			classicNpcDialogHandle.AddOption("寵物保管／領取", delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jPetStore(name);
			});
		}
		if (L1jWorldNpcCatalog.TryPortServiceKey(npc.NpcId, out string portKey))
		{
			if (HasPortExchangeOptions(portKey))
			{
				classicNpcDialogHandle.AddOption("兌換", delegate
				{
					CloseL1jWorldNpcPanel();
					EnsureNpcPanelHost();
					_townNpcHost?.OpenL1jExchange(portKey, name);
				});
			}
			if (string.Equals(portKey, "npc_elion", StringComparison.Ordinal) && ElfElementRules.IsElf(_engine.Player))
			{
				classicNpcDialogHandle.AddOption("元素契約", delegate
				{
					CloseL1jWorldNpcPanel();
					EnsureNpcPanelHost();
					_townNpcHost?.OpenL1jElfElement(name);
				});
			}
			(string, string, Action)[] array = new(string, string, Action)[5]
			{
				("npc_dufa", "\ud83d\uddfa 海賊島", delegate
				{
					_townNpcHost?.OpenL1jDufaDialog(name);
				}),
				("npc_duran", "⛴ 前往海賊島", OpenDuranDialog),
				("npc_desire_cave_consul", "\ud83d\udd25 前往炎魔謁見所", OpenConsulDialog),
				("npc_flame_lab_consul", "\ud83d\udeaa 離開實驗室", delegate
				{
					_townNpcHost?.OpenL1jFlameLabConsul(name);
				}),
				("npc_zeus_golem", "\ud83d\udd28 製作", delegate
				{
					_townNpcHost?.OpenL1jZeusGolem(name);
				})
			};
			for (int num = 0; num < array.Length; num++)
			{
				(string, string, Action) tuple2 = array[num];
				if (string.Equals(portKey, tuple2.Item1, StringComparison.Ordinal))
				{
					Action open = tuple2.Item3;
					classicNpcDialogHandle.AddOption(tuple2.Item2, delegate
					{
						CloseL1jWorldNpcPanel();
						EnsureNpcPanelHost();
						open();
					});
				}
			}
			HarborFerryRoute harborFerryRoute = HarborFerryCatalog.FindByNpc(portKey);
			if ((object)harborFerryRoute != null)
			{
				classicNpcDialogHandle.AddOption("搭船前往" + harborFerryRoute.DestinationName, delegate
				{
					CloseL1jWorldNpcPanel();
					EnsureNpcPanelHost();
					_townNpcHost?.OpenL1jHarborFerry(portKey, name);
				});
			}
			if (string.Equals(portKey, "main_forgotten_island_ticket", StringComparison.Ordinal))
			{
				classicNpcDialogHandle.AddOption("驗票前往遺忘之島", delegate
				{
					CloseL1jWorldNpcPanel();
					EnsureNpcPanelHost();
					_townNpcHost?.OpenL1jForgottenIslandTravel(name);
				});
			}
		}
		string text3 = NpcDialogCatalog.ActionLabel(npc.NpcId, "fixFree");
		if (npc.NpcId == 80158 && text3 != null)
		{
			classicNpcDialogHandle.AddOption(text3, delegate
			{
				RepairMainBlacksmithWeapon(status);
			});
		}
		foreach (TrialDirectCompletionRules.Option item2 in TrialDirectCompletionRules.AvailableFor(npc.NpcId, _engine.Player))
		{
			TrialDirectCompletionRules.Option captured = item2;
			classicNpcDialogHandle.AddOption(captured.Label, delegate
			{
				RunTrialDirectCompletion(npc, captured, status);
			});
		}
		if (craftActions.Count > 0)
		{
			string text4 = ((craftActions.Count == 1 && text != null) ? (NpcDialogCatalog.ActionLabelByHtml(text, craftActions[0].Name) ?? "製作") : $"製作清單（{craftActions.Count} 項）");
			classicNpcDialogHandle.AddOption(text4, delegate
			{
				CloseL1jWorldNpcPanel();
				EnsureNpcPanelHost();
				_townNpcHost?.OpenL1jCrafting(name, craftActions);
			});
		}
		foreach (NpcActionDefinition item2 in readOnlyList2.Where((NpcActionDefinition action) => !string.Equals(action.Kind, "MakeItem", StringComparison.Ordinal)))
		{
			NpcActionDefinition captured = item2;
			NpcActionEffect teleport = L1jWorldNpcCatalog.TeleportOf(item2);
			object obj = L1jWorldNpcCatalog.TutorDestinationLabel(item2);
			string text5 = ((text != null) ? NpcDialogCatalog.ActionLabelByHtml(text, item2.Name) : NpcDialogCatalog.ActionLabel(npc.NpcId, item2.Name));
			if (text5 == null && readOnlyList2.Count == 1 && readOnlyList.Count == 1 && !L1jDungeonExitReroutes.OverridesHtmlLabel(item2) && string.Equals(readOnlyList[0].Action, "teleportURL", StringComparison.OrdinalIgnoreCase))
			{
				text5 = readOnlyList[0].Label;
			}
			if (obj == null)
			{
				obj = text5 ?? (((object)teleport != null) ? L1jTeleportButtonText(teleport) : ("\ud83d\udcac " + item2.Name));
			}
			string text6 = (string)obj;
			if ((object)teleport != null)
			{
				classicNpcDialogHandle.AddOption(text6, delegate
				{
					TryL1jWorldNpcTeleport(captured, teleport, status);
				});
			}
			else
			{
				classicNpcDialogHandle.AddOption(text6, delegate
				{
					RunL1jWorldNpcAction(npc, captured, status);
				});
			}
		}
		if (text != null)
		{
			foreach (NpcDialogLink item3 in NpcDialogCatalog.LinksByHtml(text))
			{
				NpcDialogLink captured2 = item3;
				classicNpcDialogHandle.AddOption(captured2.Label, delegate
				{
					ReopenL1jWorldNpcPanel(npc, captured2.HtmlId);
				});
			}
			foreach (NpcDialogAction item4 in readOnlyList)
			{
				if (L1jJavaNpcInteractionRules.Handles(npc.NpcId, item4.Action))
				{
					NpcDialogAction captured3 = item4;
					classicNpcDialogHandle.AddOption(captured3.Label, delegate
					{
						RunL1jJavaNpcAction(npc, captured3.Action, status);
					});
				}
			}
		}
		classicNpcDialogHandle.AttachStatus();
		AddAboveBarPanel(panel);
		_worldNpcPanel = panel;
		panel.TreeExiting += delegate
		{
			if (_worldNpcPanel == panel)
			{
				_worldNpcPanel = null;
			}
		};
	}

	private static IReadOnlyList<string> MainTutorDialogLines(Combatant player)
	{
		if (player.Level >= 5)
		{
			if (player.Level >= 13)
			{
				return new string[2] { "看到出類拔萃的你，真是讓人驕傲啊！", "希望你能在亞丁的世界裡更加成長。" };
			}
			return new string[2] { "歡迎光臨。", "讓我告訴你現在能做的事情，可以移動到指定地點。" };
		}
		return new string[3] { "經由修練場的木人可以順利升級到 5 等級。", "想要到木人修練場，只要跟著地上的箭頭走就能找到。", "現在去找「修練場管理員」能得到幫助。" };
	}

	private static IReadOnlyList<string> FusedDomanDialogLines(int npcId, Combatant player, IReadOnlyList<string> mainLines)
	{
		if (npcId == 888001)
		{
			return new string[]
			{
				"歡迎來到奇岩城鎮的一元福利專門店！",
				"這裡販售全職業技能書（精靈水晶、魔法書、技能板、各式水晶）、全套神兵武器、全防具飾品裝備，以及各式強化、傳送、變形卷軸，通通只要 1 金幣！",
				"我也提供道具回收買賣服務，有任何需要請隨時點選下方分類選購！"
			};
		}
		if (npcId != 71198 || !string.Equals(player.ClassId, "warrior", StringComparison.Ordinal))
		{
			return mainLines;
		}
		return mainLines.Concat(new string[1] { "【戰士試煉】除毒蛇傭兵團考驗外，我也主持戰士 15／30／45／50 級試煉；達到等級後可在下方接取。" }).ToArray();
	}

	private void RepairMainBlacksmithWeapon(Label status)
	{
		int num = WeaponDurabilityRules.RepairEquippedWeaponFully(_engine.Player);
		if (num <= 0)
		{
			status.Text = "目前裝備的武器沒有損壞。";
			return;
		}
		status.Text = $"已免費修復目前裝備的武器（耐久恢復 {num}）。";
		_bagRefresh?.Invoke();
		SaveManager.Save(_session);
	}

	private string L1jTeleportButtonText(NpcActionEffect effect)
	{
		string mapKey;
		double worldX;
		double worldY;
		string text = (TryResolveL1jTeleportTarget(effect.MapId, effect.X, effect.Y, out mapKey, out worldX, out worldY) ? MapLinks.DisplayName(GameDataProvider.Shared, mapKey) : $"map {effect.MapId}（尚未建立）");
		if (effect.Price <= 0)
		{
			return "\ud83c\udf00 " + text + "\u3000免費";
		}
		return $"\ud83c\udf00 {text}\u3000{effect.Price:N0} 金幣";
	}

	private void CloseL1jWorldNpcPanel()
	{
		_worldNpcPanel?.QueueFree();
		_worldNpcPanel = null;
		_worldNpcCd = 1.0;
	}

	private void ReopenL1jWorldNpcPanel(L1jNpcSpawn npc, string htmlId)
	{
		CloseL1jWorldNpcPanel();
		OpenL1jWorldNpcPanel(npc, htmlId);
	}

	private void RunTrialDirectCompletion(L1jNpcSpawn npc, TrialDirectCompletionRules.Option option, Label status)
	{
		NpcActionResult npcActionResult = TrialDirectCompletionRules.Execute(GameDataProvider.Shared, _engine.Player, npc.NpcId, option.Id);
		ReportL1jWorldNpcAction(npcActionResult, status);
		if (npcActionResult.Success)
		{
			CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
			SyncEquipmentAndBagUi();
			SaveManager.Save(_session);
		}
	}

	private bool CanInteractWith(L1jNpcSpawn npc)
	{
		if (npc.NpcId == 888001)
		{
			return true;
		}
		if (IsActiveCastleSpawn(npc, out CastleWarDefinition _) && L1jWorldNpcCombatRules.IsCombatNpc(npc))
		{
			return false;
		}
		if ((object)CastleWarForNpc(npc) != null)
		{
			return CastleWarStore.Book.Active == null;
		}
		string text = L1jJavaNpcInteractionRules.InitialHtmlId(GameDataProvider.Shared, npc.NpcId, _engine.Player);
		if ((text == null || !NpcDialogCatalog.HasHtmlDialog(text)) && TrialDirectCompletionRules.AvailableFor(npc.NpcId, _engine.Player).Count <= 0 && NpcDialogCatalog.DefaultHtmlId(npc.NpcId, _engine.Player.Alignment) == null && !NpcDialogCatalog.HasOriginalDialog(npc.NpcId))
		{
			return L1jWorldNpcCatalog.IsInteractive(GameDataProvider.Shared, npc, _engine.Player);
		}
		return true;
	}

	private bool WorldNpcIsInteractive(L1jNpcSpawn npc)
	{
		if (!CanInteractWith(npc))
		{
			return L1jJavaNpcInteractionRules.HasConditionalDialog(npc.NpcId);
		}
		return true;
	}

	private bool ShowsWorldNpcNameplate(L1jNpcSpawn npc)
	{
		return L1jWorldNpcCombatRules.ShowsPersistentNameplate(npc, WorldNpcIsInteractive(npc));
	}

	private static bool HasPortExchangeOptions(string portNpcKey)
	{
		try
		{
			return ExchangeRules.ExchangeOptions(GameDataProvider.Shared, portNpcKey).Count > 0;
		}
		catch
		{
			return false;
		}
	}

	private void RunL1jWorldNpcAction(L1jNpcSpawn npc, NpcActionDefinition action, Label status)
	{
		try
		{
			NpcActionResult npcActionResult = (string.Equals(action.Kind, "MakeItem", StringComparison.Ordinal) ? NpcActionRules.ExecuteMakeItem(GameDataProvider.Shared, _engine.Player, action, 1L, _session.Warehouse) : NpcActionRules.ExecuteAction(_engine.Player, action));
			ReportL1jWorldNpcAction(npcActionResult, status);
			if (npcActionResult.Success)
			{
				CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
				if (action.Effects.Any(e => string.Equals(e.Kind, "buff", StringComparison.OrdinalIgnoreCase)))
				{
					bool heals = action.Effects.Any(e => e.Heal);
					if (heals)
					{
						_engine.Player.Hp = _engine.Player.MaxHp;
						_engine.Player.Mp = _engine.Player.MaxMp;
					}
					GameAudio.Instance?.PlayEvent("heal");
					Float(PlayerPos(), "【媽祖賜福】全狀態加持！", Color.FromHtml("#ffd700"), big: true);
					if (heals)
					{
						Float(PlayerPos() + new Vector2(0f, -24f), $"+{(int)_engine.Player.MaxHp} HP / MP 滿血滿魔", Color.FromHtml("#4ade80"), big: false);
					}
					RefreshHud();
					RefreshPartyHud();
				}
				_bagRefresh?.Invoke();
				SaveManager.Save(_session);
				string text = npcActionResult.HtmlIds.FirstOrDefault(NpcDialogCatalog.HasHtmlDialog);
				if (text != null)
				{
					ReopenL1jWorldNpcPanel(npc, text);
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NpcAction] Error executing {action.Name}: {ex}");
			status.Text = "執行完成。";
		}
	}

	private void RunL1jJavaNpcAction(L1jNpcSpawn npc, string action, Label status)
	{
		L1jJavaNpcActionResult l1jJavaNpcActionResult = L1jJavaNpcInteractionRules.Execute(GameDataProvider.Shared, _engine.Player, npc.NpcId, action);
		if (!l1jJavaNpcActionResult.Handled)
		{
			status.Text = "這個原版動作尚未接入。";
			return;
		}
		status.Text = ((l1jJavaNpcActionResult.Message.Length > 0) ? l1jJavaNpcActionResult.Message : (l1jJavaNpcActionResult.Success ? "完成。" : "無法執行。"));
		status.AddThemeColorOverride("font_color", Color.FromHtml((l1jJavaNpcActionResult.Success ? "#a9a497" : "#e2938f").AsSpan()));
		if (!l1jJavaNpcActionResult.Success)
		{
			if (l1jJavaNpcActionResult.HtmlId != null && NpcDialogCatalog.HasHtmlDialog(l1jJavaNpcActionResult.HtmlId))
			{
				ReopenL1jWorldNpcPanel(npc, l1jJavaNpcActionResult.HtmlId);
			}
			return;
		}
		CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
		_bagRefresh?.Invoke();
		SaveManager.Save(_session);
		if (l1jJavaNpcActionResult.StartRoiEscort)
		{
			StartRoiEscort(npc);
			CloseL1jWorldNpcPanel();
		}
		else if (l1jJavaNpcActionResult.HtmlId != null && NpcDialogCatalog.HasHtmlDialog(l1jJavaNpcActionResult.HtmlId))
		{
			ReopenL1jWorldNpcPanel(npc, l1jJavaNpcActionResult.HtmlId);
		}
	}

	private void ReportL1jWorldNpcAction(NpcActionResult result, Label status)
	{
		List<string> list = new List<string>(result.Lines);
		foreach (string htmlId in result.HtmlIds)
		{
			list.Add("（原版台詞 " + htmlId + "·本作無客戶端文字）");
		}
		status.Text = ((list.Count > 0) ? string.Join("\u3000", list) : (result.Success ? "完成。" : "無法執行。"));
		status.AddThemeColorOverride("font_color", Color.FromHtml((result.Success ? "#a9a497" : "#e2938f").AsSpan()));
		foreach (string line in result.Lines)
		{
			SlabLog("[color=#e6c76a]" + line + "[/color]");
		}
	}

	private void TryL1jWorldNpcTeleport(NpcActionDefinition action, NpcActionEffect effect, Label status)
	{
		if (!TryResolveL1jTeleportTarget(effect.MapId, effect.X, effect.Y, out string mapKey, out double worldX, out double worldY))
		{
			status.Text = "這條路線的原版目的地地圖尚未建立。";
			status.AddThemeColorOverride("font_color", Color.FromHtml("#e2938f".AsSpan()));
			return;
		}
		long num = effect.Price;
		if (num > 0 && CombatWallet.Balance(_engine.Player) < num)
		{
			status.Text = $"金幣不足：需要 {num:N0}，持有 {CombatWallet.Balance(_engine.Player):N0}";
			status.AddThemeColorOverride("font_color", Color.FromHtml("#e2938f".AsSpan()));
			return;
		}
		if (num > 0 && !CombatWallet.TryCharge(_engine.Player, num))
		{
			status.Text = "金幣不足。";
			status.AddThemeColorOverride("font_color", Color.FromHtml("#e2938f".AsSpan()));
			return;
		}
		foreach (string htmlId in NpcActionRules.ExecuteAction(_engine.Player, action).HtmlIds)
		{
			SlabLog("[color=#8fa0b8]（原版台詞 " + htmlId + "）[/color]");
		}
		CloseL1jWorldNpcPanel();
		_engine.Player.Facing8 = effect.Heading;
		_engine.StopPlayer();
		string text = MapLinks.DisplayName(GameDataProvider.Shared, mapKey);
		if (string.Equals(mapKey, _mapKey, StringComparison.Ordinal))
		{
			RelocatePlayerGroup(new WorldPoint(worldX, worldY));
			RefreshHud();
			SlabLog("[color=#7fd0ff]傳送 — 前往「" + text + "」[/color]");
			return;
		}
		_session.SuppressPetDeploymentOnce = false;
		_session.HuntMap = mapKey;
		_session.PendingHuntSpawn = (worldX, worldY);
		_session.LastHuntMap = "";
		SaveManager.Save(_session);
		SlabLog("[color=#7fd0ff]傳送 — 前往「" + text + "」[/color]");
		_pendingScreenTransition = ChangeHuntMap;
	}

	private void OpenHuntMainTargetItemPanel(string sourceUid, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == sourceUid);
		if (itemStack == null || !L1jTargetItemUseRules.IsInventoryTargetItem(shared, itemStack.ItemKey))
		{
			return;
		}
		VBoxContainer vBoxContainer = CreateItemTargetFrame(L1jTargetItemUseText.Title(shared, itemStack), new Vector2(560f, 440f));
		vBoxContainer.AddChild(ItemPanelLabel(L1jTargetItemUseText.Instruction(shared, itemStack), "#c9d1de", 14, 58f), forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(ItemPanelLabel(message, message.StartsWith("無法", StringComparison.Ordinal) ? "#e2938f" : "#8fdd8f", 14, 36f), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<ItemStack> readOnlyList = L1jTargetItemUseRules.EligibleInventoryTargets(shared, _engine.Player, sourceUid);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(ItemPanelLabel("背包裡沒有符合的材料。", "#8b95a6", 14, 40f), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 260f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			HBoxContainer hBoxContainer = new HBoxContainer();
			Label label = ItemPanelLabel(L1jItemIdentityRules.DisplayName(shared, captured), "#c9d1de", 14, 30f);
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			Button button = new Button
			{
				Text = "使用",
				CustomMinimumSize = new Vector2(72f, 30f)
			};
			button.Pressed += delegate
			{
				UseHuntMainTargetItem(sourceUid, captured.Uid);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void UseHuntMainTargetItem(string sourceUid, string targetUid)
	{
		L1jTargetItemUseResult result = L1jTargetItemUseRules.TryUseInventoryTargetItem(GameDataProvider.Shared, _engine.Player, sourceUid, targetUid, _potionRng);
		if (result.Attempted)
		{
			SaveManager.Save(_session);
			_bagRefresh?.Invoke();
		}
		string text = L1jTargetItemUseText.Result(GameDataProvider.Shared, result);
		SlabLog("[color=" + (L1jTargetItemUseText.IsSuccess(result) ? "#8fdd8f" : "#e2938f") + "]" + text + "[/color]");
		if (_engine.Player.InventoryStacks.Any((ItemStack item) => item.Uid == sourceUid))
		{
			OpenHuntMainTargetItemPanel(sourceUid, text);
		}
		else
		{
			CloseItemTargetOverlay();
		}
	}

	private bool TryCastPlayerSkill(string skillId)
	{
		if (!_mapRule.UsableSkill)
		{
			SlabLog("[color=#e2938f]此地圖無法使用技能[/color]");
			return false;
		}
		if (SummonRules.SkillIds.Contains(skillId) && !_mapRule.RecallPets)
		{
			SlabLog("[color=#e2938f]此地圖無法召喚[/color]");
			return false;
		}
		if (skillId == "sk_charm")
		{
			if (!ClassKitRegistry.CanUseSkill(_engine.Player, "sk_charm", GameDataProvider.Shared))
			{
				SlabLog("[color=#e2938f]目前無法使用迷魅術[/color]");
				return false;
			}
			BeginCharmTargeting();
			return true;
		}
		if (!ClassKitRegistry.CanUseSkill(_engine.Player, skillId, GameDataProvider.Shared))
		{
			SlabLog("[color=#e2938f]目前無法使用" + SkillInfo.Name(skillId) + "[/color]");
			return false;
		}
		if (L1jSkillTargetRules.IsAttackSkill(GameDataProvider.Shared, skillId))
		{
			Combatant? currentTarget = _engine.Engine.GetManualTarget(_engine.Player);
			if (currentTarget == null || !currentTarget.IsAlive || !_engine.Engine.Combatants.Contains(currentTarget))
			{
				if (IsTripleArrowSkill(skillId))
				{
					currentTarget = _engine.Engine.SelectNearestEnemy(_engine.Player, _engine.Player.AttackRange, requireLineOfSight: true);
				}
			}
			if (currentTarget != null && currentTarget.IsAlive && _engine.Engine.Combatants.Contains(currentTarget))
			{
				return QueuePlayerManualSkill(skillId, currentTarget);
			}
			if (IsTripleArrowSkill(skillId))
			{
				return QueuePlayerManualSkill(skillId, null);
			}
			BeginManualSkillTargeting(skillId);
			return true;
		}
		if (L1jSkillTargetRules.RequiresManualCharacterTarget(GameDataProvider.Shared, skillId))
		{
			BeginManualSkillTargeting(skillId);
			return true;
		}
		return QueuePlayerManualSkill(skillId);
	}

	private bool QueuePlayerManualSkill(string skillId, Combatant? target = null, string? preferredSummonForm = null, bool isScroll = false)
	{
		ManualCastRequestResult num = _engine.Engine.QueueManualCast(_engine.Player, skillId, target, preferredSummonForm, isScroll);
		if (num == ManualCastRequestResult.Queued)
		{
			SlabLog("[color=#e6c76a]" + SkillInfo.Name(skillId) + "：已排入下一個動作[/color]");
		}
		return num != ManualCastRequestResult.Rejected;
	}

	private bool CanUseMapItem()
	{
		if (_mapRule.UsableItem)
		{
			return true;
		}
		SlabLog("[color=#e2938f]此地圖無法使用道具[/color]");
		return false;
	}

	private void OpenMapSelectionPanel()
	{
		CloseClassicRight();
		ToggleRightAnchor("map-selection", BuildMapSelectionPanel);
	}

	private Control? BuildMapSelectionPanel()
	{
		GameData shared = GameDataProvider.Shared;
		Combatant player = _engine.Player;
		MapAccessState state = MapAccessState.From(player);
		L1jGetbackCatalog getback = L1jGetbackCatalog.Load(shared);
		IReadOnlyList<MapMenuRegion> readOnlyList;
		try
		{
			readOnlyList = MapMenuCatalog.Build(shared);
		}
		catch
		{
			readOnlyList = Array.Empty<MapMenuRegion>();
		}
		if (readOnlyList.Count == 0)
		{
			return null;
		}
		Vector2 size = new Vector2(660f, 390f);
		var (control, control2) = ClassicMapFrame.Create(new Vector2((View.X - size.X) * 0.5f, 8f), size, CloseClassicRight, 1950);
		control.AddChild(ClassicMapFrame.Title("選擇地圖"), forceReadableName: false, InternalMode.Disabled);
		long value = MapSelectionTravelRules.MinimumMMenuTravelPriceAdena(shared);
		_mapSelectionStatus = new Label
		{
			Position = new Vector2(6f, control2.Size.Y - 22f),
			Size = new Vector2(control2.Size.X - 12f, 18f),
			HorizontalAlignment = HorizontalAlignment.Center,
			Text = $"選擇要前往的地圖\u3000·\u3000最低 {value:N0} 金幣",
			MouseFilter = MouseFilterEnum.Ignore
		};
		_mapSelectionStatus.AddThemeFontSizeOverride("font_size", 11);
		_mapSelectionStatus.AddThemeColorOverride("font_color", Color.FromHtml("#c9b892".AsSpan()));
		control2.AddChild(_mapSelectionStatus, forceReadableName: false, InternalMode.Disabled);
		control.TreeExiting += delegate
		{
			_mapSelectionStatus = null;
		};
		TabContainer tabContainer = new TabContainer
		{
			Position = new Vector2(3f, 28f),
			Size = new Vector2(control2.Size.X - 6f, control2.Size.Y - 28f - 24f)
		};
		tabContainer.AddThemeFontSizeOverride("font_size", 11);
		TightenTabStrip(tabContainer);
		control2.AddChild(tabContainer, forceReadableName: false, InternalMode.Disabled);
		long balance = CombatWallet.Balance(player);
		float width = Mathf.Floor((tabContainer.Size.X - 34f) / 3f);
		int num = -1;
		foreach (MapMenuRegion item in readOnlyList)
		{
			TabContainer tabContainer2 = new TabContainer();
			tabContainer2.AddThemeFontSizeOverride("font_size", 11);
			TightenTabStrip(tabContainer2);
			tabContainer.AddChild(tabContainer2, forceReadableName: false, InternalMode.Disabled);
			tabContainer.SetTabTitle(tabContainer.GetTabCount() - 1, item.Name);
			int num2 = -1;
			foreach (MapMenuGroup group in item.Groups)
			{
				ScrollContainer scrollContainer = new ScrollContainer
				{
					HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
				};
				HFlowContainer hFlowContainer = new HFlowContainer();
				hFlowContainer.AddThemeConstantOverride("h_separation", 4);
				hFlowContainer.AddThemeConstantOverride("v_separation", 4);
				scrollContainer.AddChild(hFlowContainer, forceReadableName: false, InternalMode.Disabled);
				tabContainer2.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
				tabContainer2.SetTabTitle(tabContainer2.GetTabCount() - 1, group.Name);
				foreach (MapDestination destination in group.Destinations)
				{
					if (IsCurrentMapDestination(destination))
					{
						num2 = tabContainer2.GetTabCount() - 1;
					}
					hFlowContainer.AddChild(BuildMapSelectionButton(shared, player, state, getback, destination, balance, width), forceReadableName: false, InternalMode.Disabled);
				}
			}
			if (num2 >= 0)
			{
				tabContainer2.CurrentTab = num2;
				num = tabContainer.GetTabCount() - 1;
			}
		}
		if (num >= 0)
		{
			tabContainer.CurrentTab = num;
		}
		if (tabContainer.GetTabCount() != 0)
		{
			return control;
		}
		return null;
	}

	private static void TightenTabStrip(TabContainer tabs)
	{
		string[] array = new string[4] { "tab_selected", "tab_unselected", "tab_hovered", "tab_disabled" };
		foreach (string text in array)
		{
			StyleBox themeStylebox = tabs.GetThemeStylebox(text);
			if (themeStylebox != null)
			{
				StyleBox styleBox = (StyleBox)themeStylebox.Duplicate();
				styleBox.ContentMarginLeft = 2f;
				styleBox.ContentMarginRight = 2f;
				tabs.AddThemeStyleboxOverride(text, styleBox);
			}
		}
		tabs.GetTabBar().AddThemeConstantOverride("h_separation", 1);
	}

	private bool IsCurrentMapDestination(MapDestination destination)
	{
		bool flag = false;
		string currentIntegratedTownKey = null;
		if (destination.Kind == MapDestinationKind.Town)
		{
			MapTopology topology = _topology;
			if (topology != null)
			{
				flag = _engine.Engine.IsSafeZone(_engine.Player.Pos) || !topology.HasSafeZone;
				if (flag)
				{
					currentIntegratedTownKey = ResolveIntegratedTownLocation(topology, safe: true)?.TownKey;
				}
			}
		}
		return MapSelectionTravelRules.IsCurrentDestination(destination, _mapKey, flag, currentIntegratedTownKey);
	}

	private Button BuildMapSelectionButton(IGameData data, Combatant player, MapAccessState state, L1jGetbackCatalog getback, MapDestination destination, long balance, float width)
	{
		bool isTown = destination.Kind == MapDestinationKind.Town;
		bool flag = string.Equals(MapMenuCatalog.GroupKeyOf(destination), "village", StringComparison.Ordinal);
		bool num = IsCurrentMapDestination(destination);
		MapAccessResult mapAccessResult = MapAccessRules.Evaluate(data, player, state, destination);
		long num2 = MapSelectionTravelRules.MMenuTravelPriceOf(data, getback, destination);
		bool flag2 = balance >= num2;
		string value = (num ? "\ud83d\udccd " : (flag ? "\ud83c\udfe0 " : ((!mapAccessResult.Allowed) ? "\ud83d\udd12 " : (mapAccessResult.ConsumesItem ? "\ud83d\udddd " : ""))));
		string text = $"{value}{destination.Name}（{num2:N0}）";
		Button button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(width, 27f),
			ClipText = true
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		if (num)
		{
			button.AddThemeColorOverride("font_color", Color.FromHtml("#8fdd8f".AsSpan()));
		}
		else if (!mapAccessResult.Allowed || !flag2)
		{
			button.AddThemeColorOverride("font_color", Color.FromHtml("#8d8577".AsSpan()));
		}
		else if (flag)
		{
			button.AddThemeColorOverride("font_color", Color.FromHtml("#7fd0ff".AsSpan()));
		}
		button.TooltipText = ((mapAccessResult.Allowed && mapAccessResult.ConsumesItem) ? (text + "\n進入將消耗鑰匙") : $"{text}\n傳送費用 {num2:N0} 金幣");
		button.Pressed += delegate
		{
			TravelFromMapSelection(destination, isTown);
		};
		return button;
	}

	private void TravelFromMapSelection(MapDestination destination, bool isTown)
	{
		if (_dead || _pendingScreenTransition != null)
		{
			SetMapSelectionStatus("正在切換畫面，請稍候。", "#c9b892");
			return;
		}
		GameData shared = GameDataProvider.Shared;
		Combatant player = _engine.Player;
		bool flag = string.Equals(MapSelectionTravelRules.PhysicalMapKey(destination), _mapKey, StringComparison.Ordinal);
		if (!MapSelectionTravelRules.CanDepart(_mapRule, flag))
		{
			SetMapSelectionStatus(flag ? "此地圖無法傳送" : "此地圖無法使用傳送離開", "#e2938f");
			return;
		}
		MapAccessState state = MapAccessState.From(player);
		if (!MapAccessRules.Evaluate(shared, player, state, destination).Allowed)
		{
			SetMapSelectionStatus("\ud83d\udd12 " + destination.Name + "：條件不足", "#e2938f");
			return;
		}
		(double, double)? pendingHuntSpawn = (isTown ? (((double, double)?)null) : ResolveMapSelectionLanding(shared, destination));
		if (!isTown && !pendingHuntSpawn.HasValue)
		{
			SetMapSelectionStatus("無法前往 " + destination.Name + "：固定落點不可用", "#e2938f");
			return;
		}
		L1jGetbackCatalog getback = L1jGetbackCatalog.Load(shared);
		long num = MapSelectionTravelRules.MMenuTravelPriceOf(shared, getback, destination);
		if (CombatWallet.Balance(player) < num)
		{
			SetMapSelectionStatus($"金幣不足：前往 {destination.Name} 需要 {num:N0}", "#e2938f");
			return;
		}
		if (!MapAccessRules.TryEnter(shared, player, state, destination).Allowed)
		{
			SetMapSelectionStatus("無法前往 " + destination.Name, "#e2938f");
			return;
		}
		if (!CombatWallet.TryCharge(player, num))
		{
			SetMapSelectionStatus("扣款失敗：" + destination.Name, "#e2938f");
			return;
		}
		CloseClassicRight();
		if (isTown)
		{
			_session.TownKey = destination.Key;
			_session.PendingHuntSpawn = null;
			_session.PendingMapEntryLandmark = null;
			_session.LastHuntMap = "";
			SaveManager.Save(_session);
			SlabLog($"[color=#8fdd8f]前往 {destination.Name}（費用 {num:N0} 金幣）[/color]");
			_pendingScreenTransition = ReturnToTown;
		}
		else
		{
			_session.HuntMap = destination.MapKey;
			_session.PendingMapEntryLandmark = null;
			_session.PendingHuntSpawn = pendingHuntSpawn;
			_session.LastHuntMap = "";
			SaveManager.Save(_session);
			SlabLog($"[color=#7fd0ff]前往 {destination.Name}（費用 {num:N0} 金幣）[/color]");
			_pendingScreenTransition = ChangeHuntMap;
		}
	}

	private static (double X, double Y)? ResolveMapSelectionLanding(IGameData data, MapDestination destination)
	{
		MapTopology mapTopology = TryLoadTopology(destination.MapKey);
		if (mapTopology == null)
		{
			return null;
		}
		if (destination.HasFixedLanding)
		{
			if (!mapTopology.ContainsGameCell(destination.LandingGameX.Value, destination.LandingGameY.Value))
			{
				return null;
			}
			var (localX, localY) = mapTopology.ToLocalCell(destination.LandingGameX.Value, destination.LandingGameY.Value);
			if (!mapTopology.IsWalkableCell(localX, localY))
			{
				return null;
			}
			return mapTopology.DisplayPixelCenter(localX, localY);
		}
		L1jMapRule rule;
		int mapId = ((L1jMapRuleCatalog.Load(data).TryForMapKey(destination.MapKey, out rule) && (object)rule != null) ? rule.MapId : 0);
		MapEntryLanding mapEntryLanding = MapSelectionTravelRules.Resolve(data, mapTopology, mapId, L1jGetbackCatalog.Load(data));
		if ((object)mapEntryLanding == null)
		{
			return null;
		}
		var (item, item2) = mapTopology.DisplayPixelCenter(mapEntryLanding.LocalX, mapEntryLanding.LocalY);
		return (item, item2);
	}

	private void SetMapSelectionStatus(string text, string colour)
	{
		if (_mapSelectionStatus != null)
		{
			_mapSelectionStatus.Text = text;
			_mapSelectionStatus.AddThemeColorOverride("font_color", Color.FromHtml(colour.AsSpan()));
		}
	}

	private void ChangeHuntMap()
	{
		CastleWarAttemptSave active = CastleWarStore.Book.Active;
		CastleWarDefinition castleWarDefinition = ((active == null) ? null : CastleWarRules.Find(active.CastleId));
		if ((object)castleWarDefinition != null && !string.Equals(castleWarDefinition.MapKey, _session.HuntMap, StringComparison.Ordinal))
		{
			FailCastleWar("離開攻城地圖，攻城失敗。");
		}
		if (_session.Party.Members.Count > 0)
		{
			_session.Party.Synchronize(_engine.Combatants);
		}
		if (_session.Pets.Pets.Count > 0)
		{
			_session.Pets.Synchronize(_engine.Engine);
		}
		SyncAllOfflineCompanions();
		_onChangeMap();
	}

	private void BuildMiniMap(Rect2 rect)
	{
		_miniMapRect = rect;
		_miniMap = new TextureRect
		{
			Position = rect.Position,
			Size = rect.Size,
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		_hud.AddChild(_miniMap, forceReadableName: false, InternalMode.Disabled);
		_miniMapDotEdge = new ColorRect
		{
			Color = Color.FromHtml("#12100c".AsSpan()),
			Size = new Vector2(5f, 5f),
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		_hud.AddChild(_miniMapDotEdge, forceReadableName: false, InternalMode.Disabled);
		_miniMapDot = new ColorRect
		{
			Color = Color.FromHtml("#ff3b30".AsSpan()),
			Size = new Vector2(3f, 3f),
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		_hud.AddChild(_miniMapDot, forceReadableName: false, InternalMode.Disabled);
	}

	private void RefreshMiniMap()
	{
		if (_miniMap == null || _miniMapDot == null || _miniMapDotEdge == null)
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null || !topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY))
		{
			return;
		}
		var (gameX, gameY) = topology.ToGameCell(localX, localY);
		if (!string.Equals(_miniMapMapKey, _mapKey, StringComparison.Ordinal))
		{
			_miniMapMapKey = _mapKey;
			_miniMapDefinition = null;
			_miniMapAtlas = null;
			_miniMap.Texture = null;
			if (TryGetCurrentWorldAtlasDefinition(out WorldAtlasDefinition definition) && (object)definition != null)
			{
				Texture2D? texture2D = GodotDataFiles.TryLoadTexture(definition.AssetPath);
				if (texture2D != null)
				{
					_miniMapDefinition = definition;
					_miniMapAtlas = new AtlasTexture
					{
						Atlas = texture2D,
						FilterClip = false
					};
					_miniMap.Texture = _miniMapAtlas;
				}
			}
		}
		if ((object)_miniMapDefinition == null || _miniMapAtlas == null || !WorldAtlasCatalog.TryLocate(_miniMapDefinition, gameX, gameY, out var location))
		{
			_miniMap.Visible = false;
			_miniMapDot.Visible = false;
			_miniMapDotEdge.Visible = false;
			foreach (var dot in _miniMapTeammateDots)
			{
				dot.Edge.Visible = false;
				dot.Fill.Visible = false;
			}
			return;
		}
		float num = Mathf.Min(_miniMapRect.Size.X, _miniMapDefinition.PixelWidth);
		float num2 = Mathf.Min(_miniMapRect.Size.Y, _miniMapDefinition.PixelHeight);
		float s = Mathf.Clamp((float)location.PixelX - num / 2f, 0f, (float)_miniMapDefinition.PixelWidth - num);
		float s2 = Mathf.Clamp((float)location.PixelY - num2 / 2f, 0f, (float)_miniMapDefinition.PixelHeight - num2);
		_miniMapAtlas.Region = new Rect2(Mathf.Round(s), Mathf.Round(s2), num, num2);
		Vector2 position = _miniMapRect.Position + new Vector2(Mathf.Round((_miniMapRect.Size.X - num) / 2f), Mathf.Round((_miniMapRect.Size.Y - num2) / 2f));
		_miniMap.Position = position;
		_miniMap.Size = new Vector2(num, num2);
		_miniMap.Visible = true;
		Vector2 vector = new Vector2(position.X + (float)location.PixelX - Mathf.Round(s), position.Y + (float)location.PixelY - Mathf.Round(s2));
		_miniMapDot.Position = new Vector2(Mathf.Round(vector.X - 1.5f), Mathf.Round(vector.Y - 1.5f));
		_miniMapDotEdge.Position = new Vector2(Mathf.Round(vector.X - 2.5f), Mathf.Round(vector.Y - 2.5f));
		_miniMapDot.Visible = true;
		_miniMapDotEdge.Visible = true;

		// Render teammates on MiniMap
		int teammateIndex = 0;
		if (_remotePlayerViews.Count > 0)
		{
			foreach (var remote in _remotePlayerViews.Values)
			{
				Combatant teammate = remote.Actor;
				if (teammate == null || remote.View == null || !remote.View.Visible) continue;

				Vector2 tPos = remote.VisualPos != Vector2.Zero ? remote.VisualPos : ToVec(teammate.Pos);
				if (!topology.TryLocalCellAtDisplayPixel(tPos.X, tPos.Y, out var tLocalX, out var tLocalY))
				{
					continue;
				}
				var (tGameX, tGameY) = topology.ToGameCell(tLocalX, tLocalY);
				if (!WorldAtlasCatalog.TryLocate(_miniMapDefinition, tGameX, tGameY, out var tLocation))
				{
					continue;
				}

				float tPixelX = position.X + (float)tLocation.PixelX - Mathf.Round(s);
				float tPixelY = position.Y + (float)tLocation.PixelY - Mathf.Round(s2);

				float clampedX = Mathf.Clamp(tPixelX, position.X + 2f, position.X + num - 3f);
				float clampedY = Mathf.Clamp(tPixelY, position.Y + 2f, position.Y + num2 - 3f);

				while (_miniMapTeammateDots.Count <= teammateIndex)
				{
					ColorRect edge = new ColorRect
					{
						Color = Color.FromHtml("#12100c"),
						Size = new Vector2(5f, 5f),
						MouseFilter = MouseFilterEnum.Ignore,
						Visible = false
					};
					_hud.AddChild(edge, forceReadableName: false, InternalMode.Disabled);

					ColorRect fill = new ColorRect
					{
						Color = Color.FromHtml("#00e5ff"),
						Size = new Vector2(3f, 3f),
						MouseFilter = MouseFilterEnum.Ignore,
						Visible = false
					};
					_hud.AddChild(fill, forceReadableName: false, InternalMode.Disabled);

					_miniMapTeammateDots.Add((edge, fill));
				}

				var (tEdge, tFill) = _miniMapTeammateDots[teammateIndex];
				tEdge.Position = new Vector2(Mathf.Round(clampedX - 2.5f), Mathf.Round(clampedY - 2.5f));
				tFill.Position = new Vector2(Mathf.Round(clampedX - 1.5f), Mathf.Round(clampedY - 1.5f));
				tFill.Color = teammate.IsAlive ? Color.FromHtml("#00e5ff") : Color.FromHtml("#9ca3af");
				tEdge.Visible = true;
				tFill.Visible = true;
				teammateIndex++;
			}
		}

		for (int i = teammateIndex; i < _miniMapTeammateDots.Count; i++)
		{
			_miniMapTeammateDots[i].Edge.Visible = false;
			_miniMapTeammateDots[i].Fill.Visible = false;
		}
	}

	private string MobAtlasName(string mobKey)
	{
		return ResolveMobAtlas(_atlas, mobKey);
	}

	internal static string ResolveMobAtlas(AtlasBridge atlas, string identity)
	{
		if (identity.Length == 0 || !(GameDataProvider.Shared.Table("L1J_MOB_SPRITES") is JsonObject jsonObject))
		{
			return "";
		}
		string text = "";
		if (identity.StartsWith("gfx:", StringComparison.Ordinal) && int.TryParse(identity.AsSpan("gfx:".Length), out var result) && result > 0)
		{
			text = jsonObject["byGfx"]?[result.ToString()]?["atlas"]?.GetValue<string>() ?? "";
		}
		if (text.Length == 0)
		{
			text = jsonObject["byMobKey"]?[identity]?["atlas"]?.GetValue<string>() ?? jsonObject["byDisplay"]?[identity]?.GetValue<string>() ?? "";
		}
		if (text.Length <= 0 || !atlas.HasAtlas("anim", text))
		{
			return "";
		}
		return text;
	}

	private void TrySpawnL1jMobGroupForLeader(Combatant leader, string mobKey, MapTopology topology)
	{
		if (MobGroupsByLeader.TryGetValue(mobKey, out int[] value))
		{
			int num = ((value.Length != 1) ? Math.Min(value.Length - 1, (int)Math.Floor(_hostileRng.NextDouble() * (double)value.Length)) : 0);
			SpawnL1jMobGroup(leader, value[num], topology);
		}
	}

	private void SpawnL1jMobGroup(Combatant leader, int groupId, MapTopology topology)
	{
		if (groupId <= 0)
		{
			return;
		}
		L1jMobGroupDefinition l1jMobGroupDefinition = MobGroupCatalog.Require(groupId);
		List<Combatant> list = new List<Combatant>();
		foreach (L1jMobGroupMinionDefinition minion in l1jMobGroupDefinition.Minions)
		{
			if (MobTemplateIsBoss(minion.MobKey))
			{
				continue;
			}
			for (int i = 0; i < minion.Count; i++)
			{
				if (_engine.Engine.LivingNormalMobCount >= topology.SpawnSettings.MaximumLivingNormalMobs)
				{
					break;
				}
				WorldPoint? worldPoint = RollL1jMobGroupPoint(topology, leader);
				if (!worldPoint.HasValue)
				{
					break;
				}
				WorldPoint valueOrDefault = worldPoint.GetValueOrDefault();
				Combatant combatant = _engine.SpawnMob(minion.MobKey, valueOrDefault);
				combatant.Facing8 = leader.Facing8;
				NoteHostSpawnedMob(combatant, minion.MobKey);
				list.Add(combatant);
			}
		}
		_l1jMobGroups.Attach(leader, list, l1jMobGroupDefinition.RemoveGroupIfLeaderDies);
	}

	private WorldPoint? RollL1jMobGroupPoint(MapTopology topology, Combatant leader)
	{
		if (!topology.TryLocalCellAtDisplayPixel(leader.Pos.X, leader.Pos.Y, out var localX, out var localY))
		{
			return null;
		}
		for (int i = 0; i < 64; i++)
		{
			int num = Math.Min(16, (int)Math.Floor(_hostileRng.NextDouble() * 17.0)) - 8;
			int num2 = Math.Min(16, (int)Math.Floor(_hostileRng.NextDouble() * 17.0)) - 8;
			if (TryL1jMobGroupCell(topology, localX + num, localY + num2, out var point))
			{
				return point;
			}
		}
		for (int j = -8; j <= 8; j++)
		{
			for (int k = -8; k <= 8; k++)
			{
				if (TryL1jMobGroupCell(topology, localX + k, localY + j, out var point2))
				{
					return point2;
				}
			}
		}
		return null;
	}

	private bool TryL1jMobGroupCell(MapTopology topology, int x, int y, out WorldPoint point)
	{
		point = default(WorldPoint);
		if (!topology.IsWalkableCell(x, y))
		{
			return false;
		}
		if (_villageCells != null && _villageCells.Contains(new MapSpawnCell(x, y)))
		{
			return false;
		}
		Combatant player = _engine.Player;
		if (!topology.TryLocalCellAtDisplayPixel(player.Pos.X, player.Pos.Y, out var localX, out var localY) || !ExplorationSpawnSession.RespectsOrdinaryDensity(new MapSpawnCell(localX, localY), new MapSpawnCell(x, y), _engine.Engine.LivingNormalMobCells()))
		{
			return false;
		}
		(double X, double Y) tuple = topology.DisplayPixelCenter(x, y);
		double item = tuple.X;
		double item2 = tuple.Y;
		WorldPoint worldPoint = new WorldPoint(item, item2);
		if (_engine.Engine.IsGridCellOccupied(worldPoint))
		{
			return false;
		}
		point = worldPoint;
		return true;
	}

	private void RecordL1jMobGroupDeath(Combatant dead)
	{
		_l1jMobGroups.RecordDeath(dead);
	}

	private void DetachRetiredL1jMobGroup(Combatant retired)
	{
		_l1jMobGroups.DetachWholeGroup(retired);
	}

	private void StartL1jNpcChat(Combatant actor, L1jNpcChatTiming timing)
	{
		NpcChatRuntime.Start(actor, timing, _engine.Engine.CurrentTimeSeconds);
	}

	private void L1jNpcChatStep()
	{
		foreach (L1jNpcChatEmission item in NpcChatRuntime.Advance(_engine.Engine.CurrentTimeSeconds, _engine.Combatants))
		{
			ShowL1jNpcChat(item);
		}
	}

	private void ShowL1jNpcChat(L1jNpcChatEmission emission)
	{
		string text = (_l1jNpcChatCatalog ?? (_l1jNpcChatCatalog = L1jNpcChatCatalog.Load(GameDataProvider.Shared))).ResolveText(emission.ChatToken);
		string text2 = emission.Speaker.Disp + ": " + text;
		Label label = new Label
		{
			Text = text2,
			Position = ToVec(emission.Speaker.Pos) + new Vector2(-180f, -92f),
			Size = new Vector2(360f, 24f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 1850
		};
		ClassicWorldText.Apply(label, emission.Shout ? Color.FromHtml("#ffd76a".AsSpan()) : Color.FromHtml("#f1ead6".AsSpan()), emission.Shout ? 15 : 14);
		_ui.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Tween tween = label.CreateTween();
		tween.TweenInterval(2.2);
		tween.TweenProperty(label, "modulate:a", 0.0, 0.6);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private void PerfProbeMark(int index)
	{
		if (!(_perfProbeRemaining <= 0.0) && _perfProbeStarted)
		{
			_perfProbeMarks[index] = _perfProbeWatch.Elapsed.TotalMilliseconds;
		}
	}

	private static double ReadPerfProbeSeconds()
	{
		if (!double.TryParse(System.Environment.GetEnvironmentVariable("IDLE_LINEAGE_PERF_PROBE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !(result > 0.0))
		{
			return 0.0;
		}
		return result;
	}

	private void PerfProbeStep(double delta)
	{
		if (_perfProbeRemaining <= 0.0)
		{
			return;
		}
		_perfProbeWatch.Restart();
		if (!_perfProbeStarted)
		{
			_perfProbeStarted = true;
			EngineAdapter.SpawnCostProbe = delegate(double createMs, double addMs)
			{
				_perfProbeCreateAccumMs += createMs;
				_perfProbeAddAccumMs += addMs;
			};
			_perfProbeRows = new StringBuilder("usec,delta,steps,procMs,posX,posY,renderX,renderY,anchorX,anchorY,progress,stepping,camX,camY,m0,m1,m2,m3,m4,m5,pages,fixedSpawn,town,doors,worldNpc,boss,preRetire,postRetire,postArgs,postPlan,m16,m17,m18,townSafe,townResolve,townActivate,townAtlas,planCount,spawnAccMs,groupAccMs,createMs,addMs,gen0,gen2,mobs\n");
			_perfProbeLastUsec = Time.GetTicksUsec();
			_perfProbeLastStep = _engine.Engine.CurrentStep;
		}
		(_wasdW, _wasdA, _wasdS, _wasdD) = ((int)(_elapsed / 2.5) % 4) switch
		{
			0 => (false, false, false, true), 
			1 => (false, false, true, false), 
			2 => (false, true, false, false), 
			_ => (true, false, false, false), 
		};
		ApplyWasdInput();
	}

	private void PerfProbeFlush(double delta)
	{
		if (_perfProbeRemaining <= 0.0 || _perfProbeRows == null)
		{
			return;
		}
		_perfProbeWatch.Stop();
		ulong ticksUsec = Time.GetTicksUsec();
		long currentStep = _engine.Engine.CurrentStep;
		Combatant player = _engine.Player;
		Vector2 vector = _engine.RenderPos(player);
		(Vector2, float, bool) tuple = _engine.RenderWalk(player);
		_perfProbeRows.Append(ticksUsec - _perfProbeLastUsec).Append(',').Append(delta.ToString("F6", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(currentStep - _perfProbeLastStep)
			.Append(',')
			.Append(_perfProbeWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(player.Pos.X.ToString("F2", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(player.Pos.Y.ToString("F2", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(vector.X.ToString("F2", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(vector.Y.ToString("F2", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(tuple.Item1.X.ToString("F2", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(tuple.Item1.Y.ToString("F2", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(tuple.Item2.ToString("F4", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(tuple.Item3 ? 1 : 0)
			.Append(',')
			.Append(_camOffset.X.ToString("F2", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(_camOffset.Y.ToString("F2", CultureInfo.InvariantCulture));
		for (int i = 0; i < _perfProbeMarks.Length; i++)
		{
			_perfProbeRows.Append(',').Append(_perfProbeMarks[i].ToString("F3", CultureInfo.InvariantCulture));
			_perfProbeMarks[i] = 0.0;
		}
		long num = GC.CollectionCount(0);
		long num2 = GC.CollectionCount(2);
		_perfProbeRows.Append(',').Append(_perfProbePlanCount).Append(',')
			.Append(_perfProbeSpawnAccumMs.ToString("F3", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(_perfProbeGroupAccumMs.ToString("F3", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(_perfProbeCreateAccumMs.ToString("F3", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(_perfProbeAddAccumMs.ToString("F3", CultureInfo.InvariantCulture))
			.Append(',')
			.Append(num - _perfProbeGen0)
			.Append(',')
			.Append(num2 - _perfProbeGen2)
			.Append(',')
			.Append(_engine.Engine.LivingNormalMobCount)
			.Append('\n');
		_perfProbePlanCount = 0;
		_perfProbeSpawnAccumMs = 0.0;
		_perfProbeGroupAccumMs = 0.0;
		_perfProbeCreateAccumMs = 0.0;
		_perfProbeAddAccumMs = 0.0;
		_perfProbeGen0 = num;
		_perfProbeGen2 = num2;
		_perfProbeLastUsec = ticksUsec;
		_perfProbeLastStep = currentStep;
		_perfProbeRemaining -= delta;
		if (!(_perfProbeRemaining > 0.0))
		{
			string text = System.Environment.GetEnvironmentVariable("IDLE_LINEAGE_PERF_PROBE_OUT") ?? ProjectSettings.GlobalizePath("user://perf-probe.csv");
			try
			{
				File.WriteAllText(text, _perfProbeRows.ToString());
				GD.Print("[PerfProbe] wrote " + text);
			}
			catch (Exception ex)
			{
				GD.PushError("[PerfProbe] write failed: " + ex.Message);
			}
			_perfProbeRows = null;
			EngineAdapter.SpawnCostProbe = null;
			GetTree().Quit();
		}
	}

	private void BeginPetTamingTargeting(string itemUid, Label? status = null)
	{
		CancelCharmTargeting(silent: true);
		CancelManualSkillTargeting(silent: true);
		_petEvolutionItemUid = "";
		_petTamingItemUid = itemUid;
		if (status != null)
		{
			SetBagStatus(status, "點選 HP 40% 以下的可捕捉怪物（前置條件失敗不消耗食物）", "#7fd0ff");
		}
		SlabLog("[color=#7fd0ff]點選 HP 40% 以下的可捕捉怪物（前置條件失敗不消耗食物）[/color]");
	}

	private void BeginPetEvolutionTargeting(string itemUid, Label? status = null)
	{
		CancelCharmTargeting(silent: true);
		CancelManualSkillTargeting(silent: true);
		_petTamingItemUid = "";
		_petEvolutionItemUid = itemUid;
		if (status != null)
		{
			SetBagStatus(status, "點選出戰中的寵物給予進化果實（點空地取消）", "#7fd0ff");
		}
		SlabLog("[color=#7fd0ff]點選出戰中的寵物給予進化果實（點空地取消）[/color]");
	}

	private void CancelPetItemTargeting()
	{
		_petTamingItemUid = "";
		_petEvolutionItemUid = "";
		SlabLog("[color=#8b95a6]寵物道具：已取消（未選目標、未消耗）[/color]");
	}

	private bool HandlePetItemTargetClick(Vector2 world)
	{
		if (!PetItemTargeting)
		{
			return false;
		}
		if (_petTamingItemUid.Length > 0)
		{
			return HandleTamingTargetClick(world);
		}
		return HandleEvolutionTargetClick(world);
	}

	private bool HandleTamingTargetClick(Vector2 world)
	{
		string petTamingItemUid = _petTamingItemUid;
		_petTamingItemUid = "";
		Combatant combatant = PickPetItemTarget(world, (Combatant actor) => actor.Kind == CombatantKind.Mob && actor.IsAlive);
		if (combatant == null)
		{
			SlabLog("[color=#8b95a6]捕捉：已取消（未選目標、未消耗）[/color]");
			return true;
		}
		PetAcquisitionResult petAcquisitionResult = _engine.Engine.TryTamePet(_session.Pets, _engine.Player, combatant, petTamingItemUid);
		if (petAcquisitionResult.QuantityConsumed > 0 || petAcquisitionResult.Success)
		{
			SaveManager.Save(_session);
			_bagRefresh?.Invoke();
		}
		if (!petAcquisitionResult.Success)
		{
			SlabLog("[color=#e2938f]捕捉失敗：" + PetAcquisitionText.FailText(petAcquisitionResult.Failure) + ((petAcquisitionResult.QuantityConsumed > 0) ? "（食物已消耗）" : "") + "[/color]");
			return true;
		}
		SlabLog("[color=#8fdd8f]捕捉 " + petAcquisitionResult.Pet.DisplayName + " 成功，取得項圈；雙擊項圈可召喚出戰[/color]");
		return true;
	}

	private bool HandleEvolutionTargetClick(Vector2 world)
	{
		string petEvolutionItemUid = _petEvolutionItemUid;
		_petEvolutionItemUid = "";
		Combatant combatant = PickPetItemTarget(world, (Combatant actor) => actor.Kind == CombatantKind.Pet && actor.IsAlive);
		if (combatant == null)
		{
			SlabLog("[color=#8b95a6]進化：已取消（未選目標、未消耗）[/color]");
			return true;
		}
		PetInstance petInstance = _engine.Engine.PetInstanceOf(combatant);
		if (petInstance == null)
		{
			SlabLog("[color=#e2938f]進化失敗：找不到寵物資料[/color]");
			return true;
		}
		PetEvolutionResult petEvolutionResult = _session.Pets.TryGiveEvolutionItem(GameDataProvider.Shared, _engine.Player, petInstance.Uid, petEvolutionItemUid);
		SaveManager.Save(_session);
		_bagRefresh?.Invoke();
		if (!petEvolutionResult.Success)
		{
			SlabLog("[color=#e2938f]進化失敗：" + PetEvolutionFailureText(petEvolutionResult.Failure) + "（果實已交給目標）[/color]");
			return true;
		}
		PetCollarRules.UpgradeCollar(GameDataProvider.Shared, _engine.Player, petInstance.Uid);
		_engine.Engine.ReloadPet(_engine.Player, petInstance);
		SaveManager.Save(_session);
		SlabLog($"[color=#8fdd8f]{petEvolutionResult.PreviousForm} 進化為 {petEvolutionResult.TargetForm}[/color]");
		return true;
	}

	private Combatant? PickPetItemTarget(Vector2 world, Func<Combatant, bool> predicate)
	{
		Combatant result = null;
		float num = 56f;
		foreach (Combatant combatant in _engine.Combatants)
		{
			if (predicate(combatant))
			{
				float num2 = world.DistanceTo(ToVec(combatant.Pos));
				if (!(num2 >= num))
				{
					num = num2;
					result = combatant;
				}
			}
		}
		return result;
	}

	private static string PetEvolutionFailureText(PetEvolutionFailure failure)
	{
		return failure switch
		{
			PetEvolutionFailure.LevelTooLow => $"寵物必須達到 Lv{30}", 
			PetEvolutionFailure.FinalForm => "這種寵物不能再進化", 
			PetEvolutionFailure.InvalidFruit => "這不是該寵物需要的果實", 
			PetEvolutionFailure.ForeignOwner => "這不是你的寵物", 
			PetEvolutionFailure.MissingFruit => "背包中沒有該果實", 
			_ => "寵物資料異常", 
		};
	}

	private bool HandlePetCommandClick(Vector2 world)
	{
		Combatant combatant = PickPetItemTarget(world, (Combatant actor) => actor.Kind == CombatantKind.Pet && actor.IsAlive);
		if (combatant == null)
		{
			return false;
		}
		PetInstance petInstance = _engine.Engine.PetInstanceOf(combatant);
		if (petInstance == null)
		{
			return false;
		}
		OpenPetCommandPanel(combatant, petInstance);
		return true;
	}

	private void OpenPetCommandPanel(Combatant runtime, PetInstance pet, string message = "")
	{
		CloseL1jWorldNpcPanel();
		Vector2 size = new Vector2(470f, 430f);
		Control panel = new Control
		{
			Position = new Vector2(Mathf.Round((WorldView.X - size.X) * 0.5f), Mathf.Round((WorldView.Y - size.Y) * 0.5f)),
			Size = size,
			ZIndex = 1950,
			MouseFilter = MouseFilterEnum.Stop
		};
		panel.AddChild(new ColorRect
		{
			Color = Color.FromHtml("#141118".AsSpan()),
			Size = size,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Position = new Vector2(12f, 10f),
			Size = new Vector2(size.X - 24f, size.Y - 20f)
		};
		vBoxContainer.AddThemeConstantOverride("separation", 5);
		panel.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		Label label = new Label
		{
			Text = $"{pet.DisplayName}\u3000Lv{pet.Level}\u3000EXP {pet.ExperiencePercent}%\u3000飽食度 {pet.Food}"
		};
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#e6c76a".AsSpan()));
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Label status = new Label
		{
			Text = ((message.Length > 0) ? message : "指令依 main 一次套用到所有出戰寵物；主人等級低於寵物時該寵物會拒絕。"),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		status.AddThemeFontSizeOverride("font_size", 12);
		status.AddThemeColorOverride("font_color", Color.FromHtml("#a9a497".AsSpan()));
		vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
		CenterContainer centerContainer = new CenterContainer
		{
			CustomMinimumSize = new Vector2(0f, 43f),
			MouseFilter = MouseFilterEnum.Pass
		};
		centerContainer.AddChild(new ClassicPetQuickCommandBar(1f, delegate(PetCommandStatus command)
		{
			PetCommandResult petCommandResult = _session.Pets.CommandActivePets(_engine.Player, command);
			SaveManager.Save(_session);
			status.Text = ((petCommandResult.Defied > 0) ? $"已套用 {petCommandResult.Applied} 隻；{petCommandResult.Defied} 隻因等級高於主人而拒絕。" : $"已套用到 {petCommandResult.Applied} 隻出戰寵物。");
		}), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(centerContainer, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		vBoxContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		BuildHuntPetEquipmentRows(vBoxContainer2, runtime, pet);
		Button button = new Button
		{
			Text = "放生（項圈與寵物紀錄會刪除）",
			CustomMinimumSize = new Vector2(0f, 28f)
		};
		button.AddThemeColorOverride("font_color", Color.FromHtml("#e2938f".AsSpan()));
		button.Pressed += delegate
		{
			if (_engine.Engine.LiberatePet(_session.Pets, _engine.Player, pet) == null)
			{
				status.Text = "放生失敗：找不到寵物或其項圈。";
			}
			else
			{
				SaveManager.Save(_session);
				_bagRefresh?.Invoke();
				SlabLog("[color=#e6c76a]" + pet.DisplayName + " 已被放生，成為野生怪物。[/color]");
				CloseL1jWorldNpcPanel();
			}
		};
		vBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		Button button2 = new Button
		{
			Text = "離開",
			CustomMinimumSize = new Vector2(0f, 28f)
		};
		button2.Pressed += CloseL1jWorldNpcPanel;
		vBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		AddAboveBarPanel(panel);
		_worldNpcPanel = panel;
		panel.TreeExiting += delegate
		{
			if (_worldNpcPanel == panel)
			{
				_worldNpcPanel = null;
			}
		};
	}

	private void BuildHuntPetEquipmentRows(VBoxContainer parent, Combatant runtime, PetInstance pet)
	{
		GameData data = GameDataProvider.Shared;
		L1jPetItemCatalog catalog = L1jPetItemCatalog.Load(data);
		(string, string)[] array = new(string, string)[2]
		{
			("petwpn", "牙齒"),
			("petarm", "盔甲")
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, string) tuple = array[i];
			string slot = tuple.Item1;
			string item = tuple.Item2;
			HFlowContainer hFlowContainer = new HFlowContainer();
			hFlowContainer.AddChild(new Label
			{
				Text = item + "："
			}, forceReadableName: false, InternalMode.Disabled);
			if (pet.Equipment.TryGetValue(slot, out ItemStack value))
			{
				Button button = new Button
				{
					Text = "卸下 " + HuntPetItemName(value.ItemKey),
					CustomMinimumSize = new Vector2(130f, 26f)
				};
				button.Pressed += delegate
				{
					PetEquipmentResult petEquipmentResult = PetEquipmentRules.TryUnequip(data, _session.Pets, _engine.Player, pet.Uid, slot);
					if (petEquipmentResult.Success)
					{
						_engine.Engine.RefreshPetProfiles();
						SaveManager.Save(_session);
						OpenPetCommandPanel(runtime, pet, "已卸下 " + HuntPetItemName(petEquipmentResult.ItemKey) + "。 ");
					}
				};
				hFlowContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			}
			foreach (ItemStack item2 in from @group in _engine.Player.InventoryStacks.Where((ItemStack itemStack) => !itemStack.Locked && catalog.TryGet(itemStack.ItemKey, out L1jPetItemDefinition definition) && definition.Slot == slot).GroupBy<ItemStack, string>((ItemStack itemStack) => itemStack.ItemKey, StringComparer.Ordinal)
				select @group.First())
			{
				string itemUid = item2.Uid;
				string itemKey = item2.ItemKey;
				Button button2 = new Button
				{
					Text = "裝備 " + HuntPetItemName(itemKey),
					CustomMinimumSize = new Vector2(130f, 26f)
				};
				button2.Pressed += delegate
				{
					PetEquipmentResult petEquipmentResult = PetEquipmentRules.TryEquip(data, _session.Pets, _engine.Player, pet.Uid, itemUid);
					if (petEquipmentResult.Success)
					{
						_engine.Engine.RefreshPetProfiles();
						SaveManager.Save(_session);
						OpenPetCommandPanel(runtime, pet, "已裝備 " + HuntPetItemName(petEquipmentResult.ItemKey) + "。 ");
					}
				};
				hFlowContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
			}
			parent.AddChild(hFlowContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private static string HuntPetItemName(string itemKey)
	{
		return GameDataProvider.Shared.Item(itemKey)?["n"]?.ToString() ?? itemKey;
	}

	private void OpenDuranDialog()
	{
		CloseDuranDialog();
		string[] lines = new string[3] { "想去海賊島？嘿……你這種眼神我見多了，都是衝著德雷克那批寶物來的。", "醜話說在前頭：島上的蜥蜴人和海賊亡靈可不會跟你講道理，地監更是有去無回的多。", "決定好了就上船吧——這條航路，只有我肯跑。" };
		ClassicNpcDialogHandle classicNpcDialogHandle = ClassicNpcDialogWindow.Create(WorldView, "杜蘭：", lines, CloseDuranDialog, 1950);
		classicNpcDialogHandle.AddOption("前往海賊島", delegate
		{
			CloseDuranDialog();
			SlabLog("[color=#7fd0ff]杜蘭的船駛向海賊島……[/color]");
			_session.HuntMap = "pirate_wild";
			_session.PendingMapEntryLandmark = "pirate_front_port_arrival";
			_pendingScreenTransition = ChangeHuntMap;
		});
		AddAboveBarPanel(classicNpcDialogHandle.Root);
		_duranDialog = classicNpcDialogHandle.Root;
	}

	private void CloseDuranDialog()
	{
		if (_duranDialog != null && GodotObject.IsInstanceValid(_duranDialog))
		{
			_duranDialog.QueueFree();
		}
		_duranDialog = null;
	}

	private void UsePolymorphScroll(ItemStack stack, Action refreshBag, Label status)
	{
		if (PolymorphRules.IsShannaScroll(GameDataProvider.Shared, stack.ItemKey, out int scrollLevel))
		{
			ToggleRightAnchor("polymorph", () => BuildPolymorphPanel(stack.Uid, scrollLevel));
			return;
		}
		if (PolymorphRules.HasControlItem(_engine.Player))
		{
			ToggleRightAnchor("polymorph", () => BuildPolymorphPanel(stack.Uid));
			return;
		}
		var (flag, text) = ItemActivation.UsePolymorph(GameDataProvider.Shared, _engine.Player, stack.Uid, _potionRng);
		SetBagStatus(status, (flag ? "✓ " : "無法使用：") + text, flag ? "#8fdd8f" : "#e2938f");
		if (flag)
		{
			SaveManager.Save(_session);
			SlabLog("[color=#8fdd8f]" + text + "[/color]");
			RefreshHud();
			refreshBag();
			SendLocalAction("morph", _engine.Player.PolymorphForm ?? "");
		}
	}

	private Control BuildPolymorphPanel(string scrollUid, int? scrollMaxLevel = null)
	{
		IReadOnlyList<PolymorphForm> readOnlyList = PolymorphRules.SelectableForms(GameDataProvider.Shared, _engine.Player, scrollMaxLevel);
		float y = BarY - 44f - 12f;
		var (control, control2) = ClassicMapFrame.Create(new Vector2(RightAnchor.X - 91f, 44f), new Vector2(400f, y), CloseClassicRight, 1950);
		string title = scrollMaxLevel.HasValue ? $"夏納變身 · 選擇形態 (Lv.{scrollMaxLevel.Value})" : "變形控制 · 選擇形態";
		control.AddChild(ClassicMapFrame.Title(title), forceReadableName: false, InternalMode.Disabled);
		Label label = new Label
		{
			Text = "消耗 1 張變形卷軸；清單已依等級與武器過濾。",
			Position = new Vector2(0f, 28f),
			Size = new Vector2(control2.Size.X, 22f)
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#a9a497".AsSpan()));
		control2.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Label status = new Label
		{
			Position = new Vector2(0f, control2.Size.Y - 52f),
			Size = new Vector2(control2.Size.X, 24f)
		};
		status.AddThemeFontSizeOverride("font_size", 12);
		status.AddThemeColorOverride("font_color", Color.FromHtml("#a9a497".AsSpan()));
		control2.AddChild(status, forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Position = new Vector2(0f, 52f),
			Size = new Vector2(control2.Size.X, control2.Size.Y - 108f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(control2.Size.X - 16f, 0f)
		};
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		control2.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		foreach (PolymorphForm item in readOnlyList)
		{
			PolymorphForm form = item;
			Button button = new Button
			{
				Text = $"{form.Name}\u3000Lv{form.Level}",
				CustomMinimumSize = new Vector2(vBoxContainer.CustomMinimumSize.X, 30f),
				Alignment = HorizontalAlignment.Left
			};
			button.AddThemeFontSizeOverride("font_size", 12);
			button.Pressed += delegate
			{
				var (flag, text) = ItemActivation.UsePolymorph(GameDataProvider.Shared, _engine.Player, scrollUid, _potionRng, form.Name);
				SetBagStatus(status, (flag ? "✓ " : "無法使用：") + text, flag ? "#8fdd8f" : "#e2938f");
				if (flag)
				{
					SaveManager.Save(_session);
					SlabLog("[color=#8fdd8f]" + text + "[/color]");
					RefreshHud();
					SendLocalAction("morph", _engine.Player.PolymorphForm ?? "");
				}
			};
			vBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		}
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(MakeRow("目前沒有符合等級與武器的變身形態", "#e2938f"), forceReadableName: false, InternalMode.Disabled);
		}
		Button button2 = new Button
		{
			Text = "返回背包",
			Position = new Vector2(0f, control2.Size.Y - 28f),
			Size = new Vector2(140f, 28f)
		};
		button2.Pressed += delegate
		{
			CloseClassicRight();
			ToggleBag();
		};
		control2.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		return control;
	}

	private void BuildQuickItemSlot(int local, int global, string assignment, string itemKey, string stackUid, GameData data)
	{
		Vector2 position = QuickSlotPos(local);
		string text = data.Item(itemKey)?["n"]?.GetValue<string>() ?? itemKey;
		QuickSlotButton quickSlotButton = new QuickSlotButton
		{
			Slot = global,
			OnDropItem = OnQuickDrop,
			Text = "",
			Position = position,
			Size = QuickSlotSize,
			CustomMinimumSize = QuickSlotSize,
			TooltipText = $"{text}（F{local + 5}·可從背包拖曳替換·拖到空白處取消）",
			DragPayload = assignment,
			DragLabel = text,
			OnDragOut = ClearQuickSlot,
			OnActivate = delegate
			{
				UseQuickItem(global, assignment);
			}
		};
		quickSlotButton.SetIcon(ItemIcons.For(itemKey));
		if (QuickBar.CanAutoUse(data, itemKey))
		{
			AttachAutoBadge(quickSlotButton, _session.AutoUseItems.Contains(itemKey), AutoUseHint(data, itemKey), delegate(bool on)
			{
				if (on)
				{
					_session.AutoUseItems.Add(itemKey);
				}
				else
				{
					_session.AutoUseItems.Remove(itemKey);
				}
			});
		}
		AttachQuickBarPaging(quickSlotButton);
		_hud.AddChild(quickSlotButton, forceReadableName: false, InternalMode.Disabled);
		_quickItemBtns.Add((local, assignment, itemKey, stackUid, quickSlotButton));
		_quickSlotAction[local] = delegate
		{
			UseQuickItem(global, assignment);
		};
	}

	public static void AttachAutoBadge(Control slot, bool initiallyOn, string hint, Action<bool> setEnabled)
	{
		Button badge = new Button
		{
			ToggleMode = true,
			ButtonPressed = initiallyOn,
			Text = "自",
			TooltipText = "自動：" + hint + "\n（點一下開啟·再點一下取消）",
			MouseFilter = MouseFilterEnum.Stop
		};
		badge.AddThemeFontSizeOverride("font_size", 10);
		Paint();
		badge.Toggled += delegate(bool on)
		{
			setEnabled(on);
			Paint();
		};
		badge.CustomMinimumSize = new Vector2(12f, 12f);
		slot.AddChild(badge, forceReadableName: false, InternalMode.Disabled);
		badge.SetAnchorsPreset(LayoutPreset.TopRight);
		badge.OffsetLeft = -13f;
		badge.OffsetTop = 1f;
		badge.OffsetRight = -1f;
		badge.OffsetBottom = 13f;
		void Paint()
		{
			Skin(Color.FromHtml((badge.ButtonPressed ? "#e8c07a" : "#6b7686").AsSpan()));
		}
		void Skin(Color line)
		{
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat
			{
				BgColor = new Color(0f, 0f, 0f, 0.55f),
				BorderColor = line
			};
			styleBoxFlat.SetBorderWidthAll(1);
			styleBoxFlat.SetContentMarginAll(0f);
			string[] array = new string[4] { "normal", "hover", "pressed", "focus" };
			foreach (string text in array)
			{
				badge.AddThemeStyleboxOverride(text, styleBoxFlat);
			}
			array = new string[3] { "font_color", "font_hover_color", "font_pressed_color" };
			foreach (string text2 in array)
			{
				badge.AddThemeColorOverride(text2, line);
			}
		}
	}

	private void BuildQuickEmptyTarget(int local, int global)
	{
		QuickSlotTarget quickSlotTarget = new QuickSlotTarget
		{
			Slot = global,
			OnDropItem = OnQuickDrop,
			Position = QuickSlotPos(local),
			Size = QuickSlotSize,
			TooltipText = $"F{local + 5}（從背包拖曳道具、或從技能視窗拖曳技能到這裡）"
		};
		AttachQuickBarPaging(quickSlotTarget);
		_hud.AddChild(quickSlotTarget, forceReadableName: false, InternalMode.Disabled);
		_quickEmptyTargets.Add(quickSlotTarget);
	}

	private static string ItemDisplayName(string itemKey)
	{
		return GameDataProvider.Shared.Item(itemKey)?["n"]?.GetValue<string>() ?? itemKey;
	}

	public static string AutoUseHint(IGameData data, string itemKey, int autoPotionHpPercent = 70)
	{
		if (ConsumableRules.IsHealingPotion(data, itemKey))
		{
			return $"HP 低於 {autoPotionHpPercent}% 才自動使用（每 1 秒一次）";
		}
		JsonObject? obj = data.Item(itemKey);
		string eff = obj?["eff"]?.GetValue<string>() ?? "";
		if (string.IsNullOrEmpty(eff))
		{
			if (L1jConsumableRules.TryRead(data, itemKey, out var spec) && spec.Effect.Length > 0)
			{
				eff = spec.Effect;
			}
		}
		if (string.IsNullOrEmpty(eff) || eff == "heal")
		{
			return $"HP 低於 {autoPotionHpPercent}% 才自動使用（每 1 秒一次）";
		}
		if (eff == "food")
		{
			return "飽食度不足時才自動食用（每 1 秒一次）";
		}
		return "身上沒有這個增益時才自動使用（每 1 秒一次）";
	}

	private string AutoUseHint(GameData data, string itemKey)
	{
		return AutoUseHint(data, itemKey, _session.AutoPotionHpPercent);
	}

	private void UseQuickItem(int globalSlot, string assignment)
	{
		if (!CanUseMapItem())
		{
			return;
		}
		var (itemKey, stackUid, flag) = QuickBar.DecodeAssignment(assignment);
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			SlabLog("[color=#e2938f]快捷欄物品資料損毀[/color]");
			return;
		}
		if (flag)
		{
			string key = _engine.Player.EquippedItems.FirstOrDefault<KeyValuePair<string, ItemStack>>((KeyValuePair<string, ItemStack> pair) => pair.Value.Uid == stackUid).Key;
			if (!string.IsNullOrEmpty(key))
			{
				ToggleQuickEquipment(globalSlot, assignment, key, null);
				return;
			}
		}
		ItemStack itemStack = (flag ? _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == stackUid && item.ItemKey == itemKey) : _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.ItemKey == itemKey && !item.Locked));
		if (itemStack == null)
		{
			SlabLog("[color=#e2938f]背包／裝備中找不到這個物品[/color]");
			return;
		}
		ItemAction itemAction = ItemActivation.Classify(GameDataProvider.Shared, _engine.Player, itemStack);
		if (itemAction == ItemAction.Equip)
		{
			ToggleQuickEquipment(globalSlot, assignment, null, itemStack);
			return;
		}
		if (itemKey == "scroll_teleport" || itemAction == ItemAction.TeleportScroll)
		{
			UseTeleportScroll(null, itemStack);
			return;
		}
		if (itemAction == ItemAction.ReturnScroll)
		{
			UseReturnScroll(null, itemStack.Uid);
			return;
		}
		if (itemAction == ItemAction.Consumable && PotionIdleSeconds < 1.0)
		{
			SlabLog($"[color=#e2938f]藥水冷卻中（最短 {1.0:0} 秒）[/color]");
			return;
		}
		if (itemAction == ItemAction.MagicScroll && IsMagicScrollOnCooldown)
		{
			SlabLog($"[color=#e2938f]魔法卷軸冷卻中（剩餘 {MagicScrollCooldownRemaining:0.0} 秒）[/color]");
			return;
		}
		switch (itemAction)
		{
		case ItemAction.PetTamingFood:
			BeginPetTamingTargeting(itemStack.Uid);
			break;
		case ItemAction.PetEvolutionFruit:
			BeginPetEvolutionTargeting(itemStack.Uid);
			break;
		case ItemAction.Consumable:
			var (flag2, text) = ItemActivation.UseConsumable(GameDataProvider.Shared, _engine.Player, itemStack.Uid, _potionRng);
			if (!flag2)
			{
				SlabLog("[color=#e2938f]無法使用：" + text + "[/color]");
				break;
			}
			_lastPotionAt = _potionClock;
			PlayPotionFlash(itemKey);
			_bagRefresh?.Invoke();
			RefreshHud();
			break;
		default:
		{
			Label label = new Label();
			Action refreshBag = _bagRefresh ?? ((Action)delegate
			{
			});
			ActivateBagItem(itemStack, refreshBag, label);
			if (!string.IsNullOrWhiteSpace(label.Text))
			{
				SlabLog("[color=#c9b06a]" + label.Text + "[/color]");
			}
			break;
		}
		}
	}

	private void ToggleQuickEquipment(int globalSlot, string assignment, string? equippedSlot, ItemStack? inventoryItem)
	{
		(string ItemKey, string StackUid, bool IsInstance) tuple = QuickBar.DecodeAssignment(assignment);
		var (itemKey, text, _) = tuple;
		if (!tuple.IsInstance || string.IsNullOrWhiteSpace(text))
		{
			SlabLog("[color=#e2938f]裝備快捷鍵缺少實例資料，請重新拖曳這件裝備[/color]");
			return;
		}
		string newUid = text;
		bool flag;
		string text2;
		if (!string.IsNullOrEmpty(equippedSlot))
		{
			ItemStack before = _engine.Player.EquippedItems[equippedSlot].Copy();
			(flag, text2) = ItemActivation.Unequip(GameDataProvider.Shared, _engine.Player, equippedSlot, _potionRng);
			if (flag)
			{
				ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == before.Uid) ?? _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => ItemStackInventory.CanStack(item, before));
				if (itemStack != null)
				{
					newUid = itemStack.Uid;
				}
			}
		}
		else
		{
			if (inventoryItem == null)
			{
				SlabLog("[color=#e2938f]背包裡找不到這件裝備[/color]");
				return;
			}
			EquipmentEligibilityResult equipmentEligibilityResult = EquipmentRules.Evaluate(GameDataProvider.Shared, _engine.Player, inventoryItem);
			(flag, text2) = ItemActivation.Equip(GameDataProvider.Shared, _engine.Player, inventoryItem.Uid, _potionRng);
			if (flag && _engine.Player.EquippedItems.TryGetValue(equipmentEligibilityResult.Slot, out ItemStack value))
			{
				newUid = value.Uid;
				GameAudio.Instance?.PlayEquipment(itemKey, _engine.Player.ClassId);
			}
		}
		SlabLog(flag ? ("[color=#8fdd8f]✓ " + text2 + "[/color]") : ("[color=#e2938f]" + text2 + "[/color]"));
		if (flag)
		{
			QuickBar.RemapEquipmentAssignment(_session.QuickItems, text, newUid, itemKey);
			SaveManager.Save(_session);
			SyncEquipmentAndBagUi();
			RebuildQuickBar();
		}
	}

	private static double PotionHealingPower(IGameData data, string itemKey)
	{
		if (L1jConsumableRules.TryRead(data, itemKey, out var spec) && spec.Kind == ConsumableKind.Healing)
		{
			return spec.HealingBase;
		}
		try
		{
			return ConsumableRules.BaseHealingRange(data, itemKey).Maximum;
		}
		catch
		{
			return 0;
		}
	}

	private static double PotionDuration(IGameData data, string itemKey)
	{
		if (L1jConsumableRules.TryRead(data, itemKey, out var spec) && spec.Kind == ConsumableKind.TimedBuff)
		{
			return spec.MaximumDurationSeconds > 0 ? spec.MaximumDurationSeconds : spec.DurationSeconds;
		}
		JsonObject? obj = data.Item(itemKey);
		if (obj != null)
		{
			double dur = CombatSkill.ReadDouble(obj, "dur");
			if (dur > 0) return dur;
		}
		return 0;
	}

	private void AutoUseStep(double delta)
	{
		_potionClock += delta;
		if (_dead || !_mapRule.UsableItem)
		{
			return;
		}
		GameData shared = GameDataProvider.Shared;
		Combatant player = _engine.Player;
		if (!(PotionIdleSeconds < 1.0) && _session.AutoUseItems.Count > 0)
		{
			var candidates = _session.AutoUseItems
				.Where(itemKey => QuickBar.ShouldAutoUse(shared, player, itemKey, (double)_session.AutoPotionHpPercent / 100.0))
				.OrderByDescending(itemKey =>
				{
					JsonObject? obj = shared.Item(itemKey);
					string eff = obj?["eff"]?.GetValue<string>() ?? "";
					bool isHeal = string.IsNullOrEmpty(eff) || eff == "heal";
					return isHeal ? (100000.0 + PotionHealingPower(shared, itemKey)) : PotionDuration(shared, itemKey);
				})
				.ToList();

			foreach (string itemKey in candidates)
			{
				ItemStack itemStack = player.InventoryStacks.FirstOrDefault((ItemStack s) => s.ItemKey == itemKey && !s.Locked && s.Quantity > 0);
				if (itemStack != null && ConsumableRules.TryUse(shared, player, itemStack.Uid, _potionRng, new ConsumableUseContext
				{
					Automatic = true
				}).Success)
				{
					_lastPotionAt = _potionClock;
					PlayPotionFlash(itemKey);
					RefreshHud();
					return;
				}
			}
		}
		if (!(CompanionPotionIdleSeconds < 1.0))
		{
			TryAutoUseMonsterCompanionPotion(shared, player);
		}
	}

	private bool TryAutoUseMonsterCompanionPotion(GameData data, Combatant player)
	{
		double threshold = (double)_session.CompanionAutoPotionHpPercent / 100.0;
		Combatant combatant = (from actor in _engine.Engine.Combatants
			where MonsterCompanionRules.IsCompanion(actor) && actor.IsAlive && actor.MaxHp > 0.0 && actor.Hp < actor.MaxHp * threshold
			orderby actor.Hp / actor.MaxHp
			select actor).ThenBy<Combatant, string>((Combatant actor) => actor.Key, StringComparer.Ordinal).FirstOrDefault();
		if (combatant == null)
		{
			return false;
		}
		string text = (from key in (from stack in player.InventoryStacks
				where stack.Quantity > 0 && !stack.Locked && MonsterCompanionPotionRules.IsCompanionPotion(data, stack.ItemKey)
				select stack.ItemKey).Distinct<string>(StringComparer.Ordinal)
			orderby MonsterCompanionPotionRules.HealingStrength(data, key) descending
			select key).ThenBy<string, string>((string key) => key, StringComparer.Ordinal).FirstOrDefault();
		if (text == null)
		{
			return false;
		}
		if (!_engine.Engine.TryUseMonsterCompanionPotion(player, combatant, text).Success)
		{
			return false;
		}
		_lastCompanionPotionAt = _potionClock;
		RefreshHud();
		return true;
	}

	private void OnQuickDrop(int slot, string payload)
	{
		if (slot < 0 || slot >= QuickBar.TotalSlots)
		{
			return;
		}
		if (payload.StartsWith("skill:", StringComparison.Ordinal))
		{
			OnQuickDropSkill(slot, payload.Substring("skill:".Length));
			return;
		}
		var (itemKey, stackUid, flag) = ItemDragPayload.Decode(payload);
		if (!flag)
		{
			itemKey = payload;
		}
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return;
		}
		if (!QuickBar.CanAssign(GameDataProvider.Shared, itemKey))
		{
			SlabLog(MonsterCompanionPotionRules.IsCompanionPotion(GameDataProvider.Shared, itemKey) ? "[color=#e2938f]夥伴專用藥水由設定自動使用，不放入快捷欄[/color]" : "[color=#e2938f]找不到這個物品的正式定義，無法指派[/color]");
			return;
		}
		ItemStack itemStack = (flag ? _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == stackUid && item.ItemKey == itemKey) : _engine.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.ItemKey == itemKey));
		if (itemStack == null)
		{
			SlabLog("[color=#e2938f]背包裡找不到拖曳的物品實例[/color]");
			return;
		}
		string text = QuickBar.AssignmentFor(ItemActivation.Classify(GameDataProvider.Shared, _engine.Player, itemStack), itemStack);
		int num = Array.IndexOf<string>(_session.QuickItems, text);
		if (num >= 0)
		{
			_session.QuickItems[num] = null;
		}
		string text2 = _session.QuickItems[slot];
		if (!string.IsNullOrEmpty(text2))
		{
			_session.AutoUseItems.Remove(QuickBar.DecodeAssignment(text2).ItemKey);
		}
		ClearQuickSkillSlot(slot);
		_session.QuickItems[slot] = text;
		GameAudio.Instance?.PlayUi("quickAssign", 0.0, 0.52f);
		SlabLog($"[color=#8fdd8f]{ItemDisplayName(itemKey)} → {QuickBar.SlotDescription(slot)}[/color]");
		RebuildQuickBar();
		_bagRefresh?.Invoke();
	}

	private void OnQuickDropSkill(int slot, string skillId)
	{
		if (!CanAssignSkillToQuickBar(skillId) || !QuickBarSkillUsable(GameDataProvider.Shared, skillId))
		{
			SlabLog("[color=#e2938f]這個技能尚未學會、等級不足，或不能主動施放[/color]");
			return;
		}
		int num = Array.IndexOf<string>(_session.QuickSkills, skillId);
		if (num >= 0)
		{
			_session.QuickSkills[num] = null;
		}
		string text = _session.QuickItems[slot];
		if (!string.IsNullOrEmpty(text))
		{
			_session.AutoUseItems.Remove(QuickBar.DecodeAssignment(text).ItemKey);
		}
		_session.QuickItems[slot] = null;
		ClearQuickSkillSlot(slot);
		_session.QuickSkills[slot] = skillId;
		GameAudio.Instance?.PlayUi("quickAssign", 0.0, 0.52f);
		SlabLog($"[color=#8fdd8f]{SkillInfo.Name(skillId)} → {QuickBar.SlotDescription(slot)}[/color]");
		RebuildQuickBar();
		_bagRefresh?.Invoke();
	}

	private void ClearQuickSlot(int slot)
	{
		if (slot < 0 || slot >= QuickBar.TotalSlots)
		{
			return;
		}
		string text = _session.QuickItems[slot];
		string text2 = _session.QuickSkills[slot];
		if (!string.IsNullOrEmpty(text) || !string.IsNullOrEmpty(text2))
		{
			string value = ((!string.IsNullOrEmpty(text)) ? ItemDisplayName(QuickBar.DecodeAssignment(text).ItemKey) : SkillInfo.Name(text2));
			if (!string.IsNullOrEmpty(text))
			{
				_session.QuickItems[slot] = null;
				_session.AutoUseItems.Remove(QuickBar.DecodeAssignment(text).ItemKey);
			}
			ClearQuickSkillSlot(slot);
			GameAudio.Instance?.PlayUi("quickAssign", 0.0, 0.52f);
			SlabLog($"[color=#8b95a6]{value} 已從{QuickBar.SlotDescription(slot)}移除[/color]");
			RebuildQuickBar();
			_bagRefresh?.Invoke();
		}
	}

	private void ClearQuickSkillSlot(int slot)
	{
		string text = _session.QuickSkills[slot];
		if (text != null && text.Length != 0)
		{
			_session.QuickSkills[slot] = null;
			if (Array.IndexOf<string>(_session.QuickSkills, text) < 0)
			{
				_session.AutoCast.Remove(text);
			}
		}
	}

	private void RebuildQuickBar()
	{
		_floatingQuickBarPanel?.QueueFree();
		_floatingQuickBarPanel = null;
		Array.Clear(_floatingQuickSlotAction);
		foreach (var skillBtn in _skillBtns)
		{
			skillBtn.Btn.QueueFree();
		}
		foreach (var quickItemBtn in _quickItemBtns)
		{
			quickItemBtn.Btn.QueueFree();
		}
		foreach (Control quickEmptyTarget in _quickEmptyTargets)
		{
			quickEmptyTarget.QueueFree();
		}
		_skillBtns.Clear();
		_quickItemBtns.Clear();
		_quickEmptyTargets.Clear();
		Array.Clear(_quickSlotAction);
		BuildQuickBar();
		RefreshHud();
	}

	private void CloseItemTargetOverlay()
	{
		_itemTargetOverlay?.QueueFree();
		_itemTargetOverlay = null;
		GameCursorManager.SetDefault();
	}

	private VBoxContainer CreateItemTargetFrame(string title, Vector2 size)
	{
		CloseItemTargetOverlay();
		GameCursorManager.SetScroll();
		(Control Root, Control Body) tuple = ClassicMapFrame.Create(new Vector2((_viewW - size.X) / 2f, 24f), size, CloseItemTargetOverlay, 2200);
		Control item = tuple.Root;
		Control item2 = tuple.Body;
		_itemTargetOverlay = item;
		Label node = ClassicMapFrame.Title(title);
		item.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Position = new Vector2(8f, 34f),
			Size = new Vector2(Mathf.Max(0f, item2.Size.X - 16f), Mathf.Max(0f, item2.Size.Y - 42f))
		};
		item2.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		AddAboveBarPanel(item);
		return vBoxContainer;
	}

	private void OpenHuntResolventPanel(string solventUid, string message = "")
	{
		VBoxContainer vBoxContainer = CreateItemTargetFrame("溶解劑", new Vector2(540f, 430f));
		vBoxContainer.AddChild(ItemPanelLabel("選擇物品。確認後物品與溶解劑都會消失；失敗也不退還。", "#c9d1de", 14, 40f), forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(ItemPanelLabel(message, message.StartsWith("✓") ? "#8fdd8f" : "#e2938f", 14, 36f), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<(ItemStack, L1jResolventDefinition)> readOnlyList = L1jResolventRules.EligibleTargets(GameDataProvider.Shared, _engine.Player);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(ItemPanelLabel("背包內沒有可溶解的未鎖定物品。", "#8b95a6", 14, 32f), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 290f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (var item3 in readOnlyList)
		{
			ItemStack item = item3.Item1;
			L1jResolventDefinition item2 = item3.Item2;
			ItemStack captured = item;
			L1jResolventDefinition capturedDefinition = item2;
			HBoxContainer hBoxContainer = new HBoxContainer();
			string value = (captured.IsIdentified ? ((captured.Enhancement == 0) ? "+0 " : $"{captured.Enhancement:+#;-#} ") : "");
			Label label = ItemPanelLabel($"{value}{L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, captured)} ×{captured.Quantity}  基礎 {capturedDefinition.CrystalCount:N0}", "#c9d1de", 14, 30f);
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			Button button = new Button
			{
				Text = "選擇",
				CustomMinimumSize = new Vector2(72f, 30f)
			};
			button.Pressed += delegate
			{
				OpenHuntResolventConfirmation(solventUid, captured.Uid, capturedDefinition);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void OpenHuntResolventConfirmation(string solventUid, string targetUid, L1jResolventDefinition definition)
	{
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
		if (itemStack == null)
		{
			OpenHuntResolventPanel(solventUid, "物品已不在背包；未消耗任何物品。");
			return;
		}
		VBoxContainer vBoxContainer = CreateItemTargetFrame("確認溶解", new Vector2(500f, 330f));
		string value = ((!itemStack.IsIdentified) ? "" : ((itemStack.Enhancement == 0) ? "+0 " : $"{itemStack.Enhancement:+#;-#} "));
		vBoxContainer.AddChild(ItemPanelLabel($"物品：{value}{L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, itemStack)}\n基礎產量：魔法結晶體 ×{definition.CrystalCount:N0}\n" + "50% 無產物／40% 基礎量／10% 1.5 倍\n\n同意後消耗物品與溶解劑各 1 個，不可復原。", "#e2938f", 15, 170f), forceReadableName: false, InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		hBoxContainer.AddThemeConstantOverride("separation", 28);
		hBoxContainer.AddChild(ClassicArtButtons.Confirm(delegate
		{
			L1jResolventResult l1jResolventResult = L1jResolventRules.TryDissolve(GameDataProvider.Shared, _engine.Player, solventUid, targetUid, confirmed: true, _potionRng);
			if (l1jResolventResult.Attempted)
			{
				SaveManager.Save(_session);
			}
			string text = ((!l1jResolventResult.Attempted) ? TownResolventFailureText(l1jResolventResult.Failure) : ((l1jResolventResult.CrystalCount > 0) ? $"✓ 獲得魔法結晶體 ×{l1jResolventResult.CrystalCount:N0}" : "溶解失敗：物品與溶解劑已消耗，沒有獲得結晶。"));
			SlabLog((l1jResolventResult.CrystalCount > 0) ? ("[color=#8fdd8f]" + text + "[/color]") : ("[color=#e2938f]" + text + "[/color]"));
			if (_engine.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == solventUid && stack.ItemKey == "l1j_item_41245"))
			{
				OpenHuntResolventPanel(solventUid, text);
			}
			else
			{
				CloseItemTargetOverlay();
			}
		}, "同意並溶解"), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(ClassicArtButtons.Cancel(delegate
		{
			OpenHuntResolventPanel(solventUid);
		}, "取消；不消耗物品"), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private static Label ItemPanelLabel(string text, string color, int fontSize, float height)
	{
		Label label = new Label();
		label.Text = text;
		label.CustomMinimumSize = new Vector2(0f, height);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.AddThemeColorOverride("font_color", Color.FromHtml(color.AsSpan()));
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private static string ItemPanelName(string key)
	{
		return GameDataProvider.Shared.Item(key)?["n"]?.GetValue<string>() ?? key;
	}

	private static string TownResolventFailureText(L1jResolventFailure failure)
	{
		return failure switch
		{
			L1jResolventFailure.SolventMissing => "找不到可用的溶解劑；未消耗物品。", 
			L1jResolventFailure.TargetLocked => "物品已鎖定；未消耗物品。", 
			L1jResolventFailure.TargetEquipped => "請先卸下裝備；未消耗物品。", 
			L1jResolventFailure.TargetNotResolvable => "此物品不在原版溶解表；未消耗物品。", 
			_ => "物品狀態已改變；未消耗物品。", 
		};
	}

	private void StartRoiEscort(L1jNpcSpawn source)
	{
		if (_roiEscortActive)
		{
			return;
		}
		if (_roiFollowerRoot != null && GodotObject.IsInstanceValid(_roiFollowerRoot))
		{
			_roiEscortActive = true;
			_engine.Player.Progress.QuestSteps["Roi"] = 1;
			SaveManager.Save(_session);
			return;
		}
		(int, int, int) key = (source.NpcId, source.CellX, source.CellY);
		if (_worldNpcStaticVisuals.TryGetValue(key, out var value))
		{
			value.Root.Visible = false;
		}
		var (num, num2) = (from entry in _liveWorldNpcs
			where entry.Npc.NpcId == source.NpcId && entry.Npc.CellX == source.CellX && entry.Npc.CellY == source.CellY
			select (X: entry.X, Y: entry.Y)).FirstOrDefault((0.0, 0.0));
		if (num == 0.0 && num2 == 0.0)
		{
			MapTopology topology = _topology;
			if (topology != null)
			{
				(num, num2) = topology.DisplayPixelCenter(source.CellX, source.CellY);
			}
		}
		_roiFollowerPosition = new Vector2((float)num, (float)num2);
		_roiFollowerDialogNpc = source with
		{
			Name = "羅伊",
			Gfx = 4310,
			Heading = 6
		};
		_roiFollowerRoot = BuildL1jWorldNpcVisual(num, num2, _roiFollowerDialogNpc, combat: false, out L1jNpcCombatSpritePlayer _);
		_roiEscortActive = true;
		RefreshWorldNpcStaticSolids();
		SlabLog("[color=#e6c76a]羅伊已解除詛咒，請護送他前往巴休。[/color]");
	}

	private void RoiEscortStep(double delta)
	{
		if (!_roiEscortActive || _roiFollowerRoot == null || !GodotObject.IsInstanceValid(_roiFollowerRoot))
		{
			return;
		}
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		if (_engine.Player.Dead || !string.Equals(_mapKey, "demon_temple", StringComparison.Ordinal))
		{
			FailRoiEscort();
		}
		else
		{
			if (!topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY) || !topology.TryLocalCellAtDisplayPixel(_roiFollowerPosition.X, _roiFollowerPosition.Y, out var localX2, out var localY2))
			{
				return;
			}
			int num = Math.Max(Math.Abs(localX - localX2), Math.Abs(localY - localY2));
			if (num > 10)
			{
				FailRoiEscort();
				return;
			}
			if (num > 2)
			{
				Vector2 vector = new Vector2((float)_engine.Player.Pos.X, (float)_engine.Player.Pos.Y) - _roiFollowerPosition;
				float num2 = vector.Length();
				if (num2 > 0.001f)
				{
					float num3 = Math.Min(num2, (float)(96.0 * Math.Max(0.0, delta)));
					_roiFollowerPosition += vector / num2 * num3;
					_roiFollowerRoot.Position = _roiFollowerPosition;
					_roiFollowerRoot.ZIndex = ResolveWorldNpcDepth(_roiFollowerPosition, 54f);
				}
			}
			int num4 = topology.GameOriginX + localX;
			int num5 = topology.GameOriginY + localY;
			bool flag = ((num4 < 32917 || num4 > 32921) ? true : false);
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3 = ((num5 < 32974 || num5 > 32978) ? true : false);
				flag2 = flag3;
			}
			if (!flag2 && topology.TryLocalCellAtDisplayPixel(_roiFollowerPosition.X, _roiFollowerPosition.Y, out localX2, out localY2) && Math.Max(Math.Abs(localX - localX2), Math.Abs(localY - localY2)) < 3)
			{
				CompleteRoiEscort();
			}
		}
	}

	private void FailRoiEscort()
	{
		_roiEscortActive = false;
		_engine.Player.Progress.QuestSteps["Roi"] = 0;
		SaveManager.Save(_session);
		SlabLog("[color=#e2938f]羅伊與你距離過遠，護送失敗；他停留在失敗地點。[/color]");
	}

	private void CompleteRoiEscort()
	{
		string text = L1jJavaNpcInteractionRules.FindItemKey(GameDataProvider.Shared, 41003);
		if (text == null)
		{
			SlabLog("[color=#e2938f]羅伊的袋子資料缺失，護送尚未完成。[/color]");
			return;
		}
		CombatInventory.Add(_engine.Player, text, 1L);
		_engine.Player.Progress.QuestSteps["Roi"] = 0;
		_roiEscortActive = false;
		_roiFollowerRoot?.QueueFree();
		_roiFollowerRoot = null;
		_roiFollowerDialogNpc = null;
		SaveManager.Save(_session);
		_bagRefresh?.Invoke();
		SlabLog("[color=#8fdd8f]已將羅伊護送到巴休身邊，取得「羅伊的袋子」。[/color]");
	}

	private void ClearRoiEscortVisual()
	{
		if (_roiFollowerRoot != null && GodotObject.IsInstanceValid(_roiFollowerRoot))
		{
			_roiFollowerRoot.QueueFree();
		}
		_roiFollowerRoot = null;
		_roiFollowerDialogNpc = null;
		_roiEscortActive = false;
	}

	private void OpenHuntSealScrollPanel(string scrollUid, bool sealing, string message = "")
	{
		GameData data = GameDataProvider.Shared;
		ItemStack itemStack = _engine.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		if (itemStack == null || !(sealing ? L1jSealRules.IsSealScroll(data, itemStack.ItemKey) : L1jSealRules.IsUnsealScroll(data, itemStack.ItemKey)))
		{
			return;
		}
		string text = (sealing ? "封印" : "解除封印");
		VBoxContainer vBoxContainer = CreateItemTargetFrame(text + "卷軸", new Vector2(540f, 430f));
		vBoxContainer.AddChild(ItemPanelLabel(SealScrollText.Intro(sealing), "#c9d1de", 14, 60f), forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(ItemPanelLabel(message, message.StartsWith("✓") ? "#8fdd8f" : "#e2938f", 14, 36f), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<ItemStack> readOnlyList = L1jSealRules.EligibleTargets(data, _engine.Player, sealing);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(ItemPanelLabel(SealScrollText.NoTargets(sealing), "#8b95a6", 14, 44f), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 260f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			HBoxContainer hBoxContainer = new HBoxContainer();
			string text2 = (captured.IsIdentified ? ((captured.Enhancement == 0) ? "+0 " : $"{captured.Enhancement:+#;-#} ") : "");
			Label label = ItemPanelLabel(HuntEquippedMark(captured) + SealScrollText.Prefix(captured) + text2 + L1jItemIdentityRules.DisplayName(data, captured), "#c9d1de", 14, 30f);
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			Button button = new Button
			{
				Text = "選擇",
				CustomMinimumSize = new Vector2(72f, 30f)
			};
			button.Pressed += delegate
			{
				L1jSealResult result = (sealing ? L1jSealRules.TrySeal(data, _engine.Player, scrollUid, captured.Uid, confirmed: true) : L1jSealRules.TryUnseal(data, _engine.Player, scrollUid, captured.Uid, confirmed: true));
				if (result.Attempted)
				{
					SaveManager.Save(_session);
				}
				string text3 = SealScrollText.Outcome(data, result, sealing);
				SlabLog(result.Attempted ? ("[color=#8fdd8f]" + text3 + "[/color]") : ("[color=#e2938f]" + text3 + "[/color]"));
				if (_engine.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == scrollUid))
				{
					OpenHuntSealScrollPanel(scrollUid, sealing, text3);
				}
				else
				{
					CloseItemTargetOverlay();
				}
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void BeginManualSkillTargeting(string skillId, ItemStack? scrollStack = null)
	{
		CancelCharmTargeting(silent: true);
		_petTamingItemUid = "";
		_petEvolutionItemUid = "";
		_darkEntBarkUid = "";
		_reviveTargeting = false;
		_manualSkillTargetId = skillId;
		_manualSkillScrollStack = scrollStack;
		Input.SetDefaultCursorShape(Input.CursorShape.Cross);
		string name = scrollStack != null ? ItemDisplayName(scrollStack.ItemKey) : SkillInfo.Name(skillId);
		SlabLog("[color=#7fd0ff]" + name + "：點選施放目標（空地／右鍵／Esc 取消）[/color]");
	}

	private void CancelManualSkillTargeting(bool silent = false)
	{
		if (ManualSkillTargeting)
		{
			string text = _manualSkillScrollStack != null ? ItemDisplayName(_manualSkillScrollStack.ItemKey) : SkillInfo.Name(_manualSkillTargetId);
			_manualSkillTargetId = "";
			_manualSkillScrollStack = null;
			Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
			if (!silent)
			{
				SlabLog("[color=#8b95a6]" + text + "：已取消（未施放）[/color]");
			}
		}
	}

	private bool HandleManualSkillTargetClick(Vector2 world)
	{
		if (!ManualSkillTargeting)
		{
			return false;
		}
		string manualSkillTargetId = _manualSkillTargetId;
		ItemStack? scrollStack = _manualSkillScrollStack;
		JsonObject source = GameDataProvider.Shared.Skill(manualSkillTargetId);
		bool isAttack = source != null && L1jSkillTargetRules.IsAttackSkill(source);
		bool isSupport = source != null && L1jSkillTargetRules.RequiresManualCharacterTarget(GameDataProvider.Shared, manualSkillTargetId);
		bool isScroll = scrollStack != null;

		if (source == null || (!isAttack && !isSupport && !isScroll))
		{
			CancelManualSkillTargeting(silent: true);
			return true;
		}

		bool allowsDead = L1jSkillTargetRules.AllowsDeadCharacterTarget(source);
		Combatant combatant;
		if (isAttack)
		{
			combatant = PickWorldCharacterTarget(world, (Combatant actor) => actor.IsAlive && (actor.Kind == CombatantKind.Mob || CombatEngine.IsEnemy(_engine.Player, actor)));
		}
		else
		{
			combatant = PickWorldCharacterTarget(world, (Combatant actor) => (actor.IsAlive || allowsDead) && L1jSkillTargetRules.AllowsCharacterTarget(source, actor));
		}

		if (combatant == null)
		{
			CancelManualSkillTargeting(silent: true);
			string name = scrollStack != null ? ItemDisplayName(scrollStack.ItemKey) : SkillInfo.Name(manualSkillTargetId);
			SlabLog("[color=#8b95a6]" + name + "：已取消（未選目標）[/color]");
			return true;
		}

		if (isAttack || combatant.Kind == CombatantKind.Mob)
		{
			if (_views.TryGetValue(combatant, out var mobView))
			{
				mobView.RevealCombatName(10.0);
			}
			if (!isScroll)
			{
				StopAutoFollow(userCancelled: true);
				_engine.Engine.SetManualTarget(_engine.Player, combatant);

				double skillRange = 480.0;
				if (CombatSkill.TryRead(manualSkillTargetId, source, out var cs) && cs != null && cs.CastRange > 0.0)
				{
					skillRange = cs.CastRange;
				}
				if (CombatEngine.IsWithinRange(_engine.Player, combatant, skillRange))
				{
					_engine.StopPlayer();
				}
				else
				{
					_engine.Engine.SetMoveTarget(_engine.Player, combatant.Pos, isManual: false);
					_playerView?.InterruptOneShot();
				}
			}
		}

		if (combatant.IsRemote)
		{
			_manualSkillTargetId = "";
			_manualSkillScrollStack = null;
			Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
			CastHealOnTeammate(combatant, manualSkillTargetId);
			return true;
		}

		if (isScroll)
		{
			if (IsMagicScrollOnCooldown)
			{
				SlabLog($"[color=#e2938f]魔法卷軸冷卻中（剩餘 {MagicScrollCooldownRemaining:0.0} 秒）[/color]");
				return true;
			}
			if (!QueuePlayerManualSkill(manualSkillTargetId, combatant, isScroll: true))
			{
				SlabLog("[color=#e2938f]" + ItemDisplayName(scrollStack!.ItemKey) + "：此目標不符合條件或目前無法施放，準星已保留[/color]");
				return true;
			}
			_lastMagicScrollAt = _potionClock;
			CombatInventory.TryRemove(_engine.Player, scrollStack!.ItemKey, 1L);
			SaveManager.Save(_session);
			_bagRefresh?.Invoke();
		}
		else
		{
			if (!QueuePlayerManualSkill(manualSkillTargetId, combatant))
			{
				SlabLog("[color=#e2938f]" + SkillInfo.Name(manualSkillTargetId) + "：此目標不符合條件或目前無法施放，準星已保留[/color]");
				return true;
			}
		}

		_manualSkillTargetId = "";
		_manualSkillScrollStack = null;
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
		RefreshHud();
		return true;
	}

	private void CastTeleportSkill()
	{
		Combatant player = _engine.Player;
		if (_dead)
		{
			SlabLog("[color=#e2938f]已死亡，無法施放[/color]");
			return;
		}
		if (!_mapRule.UsableSkill)
		{
			ReportTeleportFailure(null, "此地圖無法使用技能");
			return;
		}
		if (!ClassSkillAccessRules.Allows(player, "sk_teleport"))
		{
			SlabLog("[color=#e2938f]你的職業無法使用傳送術[/color]");
			return;
		}
		if (!player.CanCast)
		{
			SlabLog("[color=#e2938f]無法施放（沉默／魔法封印）[/color]");
			return;
		}
		int num = PlayerSkillMpCost("sk_teleport");
		if (player.Mp < (double)num)
		{
			SlabLog($"[color=#e2938f]MP 不足（需 {num}）[/color]");
		}
		else
		{
			DoTeleport(TeleportSource.Skill, num);
		}
	}

	private void UseTeleportScroll(Label? status = null, ItemStack? scrollStack = null)
	{
		if (_dead)
		{
			ReportTeleportFailure(status, "已死亡，無法使用");
			return;
		}
		if (!_mapRule.UsableItem)
		{
			ReportTeleportFailure(status, "此地圖無法使用道具");
			return;
		}
		if (scrollStack == null)
		{
			scrollStack = ItemActivation.FindFirstInventoryStack(GameDataProvider.Shared, _engine.Player, ItemAction.TeleportScroll);
		}
		if (scrollStack == null)
		{
			ReportTeleportFailure(status, "沒有傳送卷軸");
			return;
		}

		var itemDef = GameDataProvider.Shared.Item(scrollStack.ItemKey) as JsonObject;
		string targetMap = itemDef?["teleportMap"]?.GetValue<string>() ?? itemDef?["teleport_map"]?.GetValue<string>() ?? itemDef?["targetMap"]?.GetValue<string>() ?? "";
		int targetX = itemDef?["teleportX"]?.GetValue<int>() ?? itemDef?["teleport_x"]?.GetValue<int>() ?? 0;
		int targetY = itemDef?["teleportY"]?.GetValue<int>() ?? itemDef?["teleport_y"]?.GetValue<int>() ?? 0;
		int l1jId = CombatSkill.ReadInt(itemDef ?? new JsonObject(), "l1jItemId");
		string itemName = itemDef?["n"]?.GetValue<string>() ?? scrollStack.ItemKey;

		if (string.IsNullOrEmpty(targetMap))
		{
			targetMap = ItemActivation.ResolveFallbackTeleportMap(l1jId, scrollStack.ItemKey, itemName);
		}

		if (targetMap == "home")
		{
			UseReturnScroll(status, scrollStack.Uid);
			return;
		}

		if (targetMap == "random" || (string.IsNullOrEmpty(targetMap) && (scrollStack.ItemKey == "scroll_teleport" || l1jId == 40100)))
		{
			if (!TeleportMemoryRules.CanUseTeleportScroll(_mapRule.UsableItem, _mapRule.Teleportable))
			{
				ReportTeleportFailure(status, "此地圖無法傳送");
				return;
			}
			if (!CombatInventory.TryRemove(_engine.Player, scrollStack.ItemKey, 1L))
			{
				ReportTeleportFailure(status, "沒有瞬間移動卷軸");
				return;
			}
			if (TeleportMemoryRules.HasControlRing(_engine.Player))
			{
				OpenTeleportMemoryPanel(TeleportSource.Scroll, 0);
			}
			else
			{
				TeleportWithinMap();
			}
			return;
		}

		// Specific destination map warp:
		if (!_mapRule.Escapable)
		{
			ReportTeleportFailure(status, "無法使用：此地圖無法傳送離開");
			return;
		}
		if (!CombatInventory.TryRemove(_engine.Player, scrollStack.ItemKey, 1L))
		{
			ReportTeleportFailure(status, "無法使用：背包中已沒有這張傳送卷軸");
			return;
		}

		SlabLog($"[color=#7fd0ff]使用【{itemName}】傳送至【{targetMap}】[/color]");
		_session.SuppressPetDeploymentOnce = false;
		_session.HuntMap = targetMap;
		if (targetX > 0 && targetY > 0)
		{
			_session.PendingHuntSpawn = (targetX, targetY);
		}
		else
		{
			_session.PendingHuntSpawn = null;
		}
		_session.PendingMapEntryLandmark = null;
		SaveManager.Save(_session);
		_pendingScreenTransition = ChangeHuntMap;
	}

	private void ReportTeleportFailure(Label? status, string text)
	{
		if (status != null)
		{
			SetBagStatus(status, "無法使用：" + text, "#e2938f");
		}
		else
		{
			SlabLog("[color=#e2938f]" + text + "[/color]");
		}
	}

	private void DoTeleport(TeleportSource source, int mpCost)
	{
		if (!_mapRule.Teleportable)
		{
			ReportTeleportFailure(null, "此地圖無法傳送");
		}
		else if (TeleportMemoryRules.HasControlRing(_engine.Player))
		{
			OpenTeleportMemoryPanel(source, mpCost);
		}
		else if (PayTeleportCost(source, mpCost, null))
		{
			TeleportWithinMap();
		}
	}

	private bool PayTeleportCost(TeleportSource source, int mpCost, Label? status)
	{
		if (source == TeleportSource.Skill)
		{
			if (_engine.Player.Mp < (double)mpCost)
			{
				ReportTeleportFailure(status, $"MP 不足（需 {mpCost}）");
				return false;
			}
			_engine.Player.Mp -= mpCost;
			RefreshHud();
			return true;
		}
		if (!ItemStackInventory.TryRemoveByItemKey(_engine.Player.InventoryStacks, "scroll_teleport", 1L))
		{
			ReportTeleportFailure(status, "沒有瞬間移動卷軸");
			return false;
		}
		return true;
	}

	private void TeleportWithinMap()
	{
		Vector2 vector = Teleportation.RandomPointInMap(Field, _grid, _topology, PlayerPos(), _engine.Player.Radius, _rng);
		RelocatePlayerGroup(new WorldPoint(vector.X, vector.Y));
		SlabLog("[color=#c9a0ff]傳送 — 移動到本地圖的其他位置[/color]");
	}

	private void OpenTeleportMemoryPanel(TeleportSource source, int mpCost)
	{
		if (_tpMemoryPanel != null)
		{
			_tpMemoryPanel.QueueFree();
			_tpMemoryPanel = null;
			return;
		}
		var (panel, control) = ClassicMapFrame.Create(size: new Vector2(640f, 430f), position: new Vector2(Mathf.Round((WorldView.X - 640f) * 0.5f), 30f), onClose: CloseTeleportMemoryPanel, zIndex: 1950);
		panel.AddChild(ClassicMapFrame.Title("傳送控制戒指 · 記憶位置"), forceReadableName: false, InternalMode.Disabled);
		_tpMemoryStatus = new Label
		{
			Position = new Vector2(8f, 30f),
			Size = new Vector2(control.Size.X - 16f, 20f)
		};
		_tpMemoryStatus.AddThemeFontSizeOverride("font_size", 13);
		_tpMemoryStatus.AddThemeColorOverride("font_color", Color.FromHtml("#a9a497".AsSpan()));
		_tpMemoryStatus.Text = ((source == TeleportSource.Skill) ? $"傳送術（MP {mpCost}）；沒有 MP 時改用瞬移卷軸" : "瞬間移動卷軸");
		control.AddChild(_tpMemoryStatus, forceReadableName: false, InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Position = new Vector2(8f, 56f),
			Size = new Vector2(control.Size.X - 16f, 30f)
		};
		hBoxContainer.AddThemeConstantOverride("separation", 6);
		_tpMemoryName = new LineEdit
		{
			PlaceholderText = "位置名稱",
			MaxLength = 12,
			CustomMinimumSize = new Vector2(172f, 28f)
		};
		_tpMemoryName.AddThemeFontSizeOverride("font_size", 13);
		_tpMemoryName.TextSubmitted += delegate
		{
			RememberCurrentSpot();
		};
		hBoxContainer.AddChild(_tpMemoryName, forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = "儲存目前位置",
			CustomMinimumSize = new Vector2(104f, 28f)
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		button.Pressed += RememberCurrentSpot;
		hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		Button button2 = new Button
		{
			Text = "戒指 · 本圖隨機傳送（免費）",
			CustomMinimumSize = new Vector2(236f, 28f)
		};
		button2.AddThemeFontSizeOverride("font_size", 13);
		button2.Pressed += FreeControlRingTeleport;
		hBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		control.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Position = new Vector2(8f, 96f),
			Size = new Vector2(control.Size.X - 16f, control.Size.Y - 30f - 74f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_tpMemoryList = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(control.Size.X - 32f, 0f)
		};
		_tpMemoryList.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(_tpMemoryList, forceReadableName: false, InternalMode.Disabled);
		control.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		control.MouseFilter = MouseFilterEnum.Pass;
		AddAboveBarPanel(panel);
		_tpMemoryPanel = panel;
		panel.TreeExiting += delegate
		{
			if (_tpMemoryPanel == panel)
			{
				_tpMemoryPanel = null;
				_tpMemoryList = null;
				_tpMemoryStatus = null;
				_tpMemoryName = null;
			}
		};
		RebuildTeleportMemoryList();
	}

	private void CloseTeleportMemoryPanel()
	{
		_tpMemoryPanel?.QueueFree();
		_tpMemoryPanel = null;
	}

	private void RebuildTeleportMemoryList()
	{
		VBoxContainer tpMemoryList = _tpMemoryList;
		if (tpMemoryList == null)
		{
			return;
		}
		foreach (Node child in tpMemoryList.GetChildren())
		{
			child.QueueFree();
		}
		Combatant player = _engine.Player;
		int value = TeleportMemoryRules.MemoryCapacity(player);
		Label label = new Label
		{
			Text = $"已儲存位置 {player.Progress.TeleportMemories.Count} / {value}"
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#e6c76a".AsSpan()));
		tpMemoryList.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (player.Progress.TeleportMemories.Count == 0)
		{
			Label label2 = new Label
			{
				Text = "（還沒有記憶任何位置）"
			};
			label2.AddThemeFontSizeOverride("font_size", 12);
			label2.AddThemeColorOverride("font_color", Color.FromHtml("#8b95a6".AsSpan()));
			tpMemoryList.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
			return;
		}
		TeleportMemoryLocation[] array = player.Progress.TeleportMemories.ToArray();
		foreach (TeleportMemoryLocation teleportMemoryLocation in array)
		{
			TeleportMemoryLocation entry = teleportMemoryLocation;
			HBoxContainer hBoxContainer = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(0f, 30f)
			};
			hBoxContainer.AddThemeConstantOverride("separation", 6);
			Label label3 = new Label
			{
				Text = entry.Name + "\u3000—\u3000" + MapDisplayName(entry.MapKey),
				CustomMinimumSize = new Vector2(420f, 28f),
				VerticalAlignment = VerticalAlignment.Center,
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
			};
			label3.AddThemeFontSizeOverride("font_size", 13);
			hBoxContainer.AddChild(label3, forceReadableName: false, InternalMode.Disabled);
			Button button = new Button
			{
				Text = "傳送",
				CustomMinimumSize = new Vector2(66f, 28f)
			};
			button.AddThemeFontSizeOverride("font_size", 13);
			button.Pressed += delegate
			{
				TeleportToMemory(entry);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			Button button2 = new Button
			{
				Text = "刪除",
				CustomMinimumSize = new Vector2(66f, 28f)
			};
			button2.AddThemeFontSizeOverride("font_size", 13);
			button2.Pressed += delegate
			{
				DeleteMemory(entry);
			};
			hBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
			tpMemoryList.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private string MapDisplayName(string mapKey)
	{
		if (string.Equals(mapKey, _mapKey, StringComparison.Ordinal))
		{
			return _mapName;
		}
		return MapLinks.DisplayName(GameDataProvider.Shared, mapKey);
	}

	private void RememberCurrentSpot()
	{
		Label tpMemoryStatus = _tpMemoryStatus;
		if (tpMemoryStatus == null)
		{
			return;
		}
		bool flag = TeleportMemoryRules.CanUseTeleportScroll(_mapRule.UsableItem, _mapRule.Teleportable);
		if (!flag)
		{
			SetBagStatus(tpMemoryStatus, "此地圖無法記憶座標", "#e2938f");
			return;
		}
		Combatant player = _engine.Player;
		WorldPoint pos = player.Pos;
		if (!Teleportation.CanStand(_grid, _topology, new Vector2((float)pos.X, (float)pos.Y), player.Radius))
		{
			SetBagStatus(tpMemoryStatus, "這個位置站不住，換個地方再記", "#e2938f");
			return;
		}
		TeleportMemoryResult teleportMemoryResult = TeleportMemoryRules.TryRemember(player, _tpMemoryName?.Text ?? "", _mapKey, pos, flag);
		if (!teleportMemoryResult.Success)
		{
			SetBagStatus(tpMemoryStatus, TeleportMemoryFailureText(teleportMemoryResult.Failure), "#e2938f");
			return;
		}
		if (_tpMemoryName != null)
		{
			_tpMemoryName.Text = "";
		}
		SaveManager.Save(_session);
		SetBagStatus(tpMemoryStatus, "已記住「" + teleportMemoryResult.Location.Name + "」", "#9fd68c");
		RebuildTeleportMemoryList();
	}

	private void DeleteMemory(TeleportMemoryLocation entry)
	{
		Label tpMemoryStatus = _tpMemoryStatus;
		if (tpMemoryStatus != null)
		{
			TeleportMemoryResult teleportMemoryResult = TeleportMemoryRules.TryDelete(_engine.Player, entry.Id);
			if (!teleportMemoryResult.Success)
			{
				SetBagStatus(tpMemoryStatus, TeleportMemoryFailureText(teleportMemoryResult.Failure), "#e2938f");
				return;
			}
			SaveManager.Save(_session);
			SetBagStatus(tpMemoryStatus, "已刪除「" + entry.Name + "」", "#a9a497");
			RebuildTeleportMemoryList();
		}
	}

	private void TeleportToMemory(TeleportMemoryLocation entry)
	{
		Label tpMemoryStatus = _tpMemoryStatus;
		if (tpMemoryStatus == null)
		{
			return;
		}
		bool flag = string.Equals(entry.MapKey, _mapKey, StringComparison.Ordinal);
		if (flag && !_mapRule.Teleportable)
		{
			SetBagStatus(tpMemoryStatus, "此地圖無法傳送", "#e2938f");
			return;
		}
		if (!flag && !_mapRule.Escapable)
		{
			SetBagStatus(tpMemoryStatus, "此地圖無法使用傳送離開", "#e2938f");
			return;
		}
		MapDestination mapDestination = null;
		if (!flag)
		{
			mapDestination = FindHuntDestination(entry.MapKey);
			if ((object)mapDestination == null)
			{
				SetBagStatus(tpMemoryStatus, "「" + entry.Name + "」所在的地圖已經不存在", "#e2938f");
				return;
			}
			if (!MapAccessRules.TryEnter(GameDataProvider.Shared, _engine.Player, MapAccessState.From(_engine.Player), mapDestination).Allowed)
			{
				SetBagStatus(tpMemoryStatus, "無法前往：" + mapDestination.Name, "#e2938f");
				return;
			}
		}
		TeleportMemoryResult teleportMemoryResult = TeleportMemoryRules.TryPayForRememberedTeleport(_engine.Player, entry, PlayerSkillMpCost("sk_teleport"), _mapRule.UsableSkill, _mapRule.UsableItem);
		if (!teleportMemoryResult.Success)
		{
			SetBagStatus(tpMemoryStatus, TeleportMemoryFailureText(teleportMemoryResult.Failure), "#e2938f");
			return;
		}
		RefreshHud();
		_tpMemoryPanel?.QueueFree();
		_tpMemoryPanel = null;
		if (flag)
		{
			RelocatePlayerGroup(new WorldPoint(entry.WorldX, entry.WorldY));
			SlabLog("[color=#c9a0ff]傳送 — 回到「" + entry.Name + "」[/color]");
			return;
		}
		_session.HuntMap = entry.MapKey;
		_session.SuppressPetDeploymentOnce = false;
		_session.PendingHuntSpawn = (entry.WorldX, entry.WorldY);
		SaveManager.Save(_session);
		SlabLog($"[color=#c9a0ff]傳送 — 前往「{entry.Name}」（{mapDestination.Name}）[/color]");
		_pendingScreenTransition = ChangeHuntMap;
	}

	private void FreeControlRingTeleport()
	{
		Label tpMemoryStatus = _tpMemoryStatus;
		if (tpMemoryStatus != null)
		{
			if (!_mapRule.Teleportable)
			{
				SetBagStatus(tpMemoryStatus, "此地圖無法傳送", "#e2938f");
				return;
			}
			if (!TeleportMemoryRules.HasControlRing(_engine.Player))
			{
				SetBagStatus(tpMemoryStatus, "需要傳送控制戒指", "#e2938f");
				return;
			}
			Vector2 vector = Teleportation.RandomPointInMap(Field, _grid, _topology, PlayerPos(), _engine.Player.Radius, _rng);
			_tpMemoryPanel?.QueueFree();
			_tpMemoryPanel = null;
			RelocatePlayerGroup(new WorldPoint(vector.X, vector.Y));
			SlabLog("[color=#c9a0ff]傳送控制戒指 — 移動到本地圖的其他位置[/color]");
		}
	}

	private void RelocatePlayerGroup(WorldPoint destination)
	{
		_engine.Engine.RelocateTeleportGroup(_engine.Player, destination, _mapRule.TakePets);
		_lastDungeonRandomPlayerCell = (_lastTrapPlayerCell = ((_topology != null && _topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY)) ? new(int, int)?((localX, localY)) : (((int, int)?)null)));
		_lastPlayerCell = null;
		_mapEntryTimeSeconds = _engine.Engine.CurrentTimeSeconds;
		_spawnSession?.ResetAfterTeleport();
		_camOffset = CamTarget();
		_world.Position = _camOffset;
		_playerView?.PlayOneShot("skill");
		GameAudio.Instance?.PlayEvent("levelup");
	}

	private MapDestination? FindHuntDestination(string mapKey)
	{
		foreach (MapRegionDefinition region in WorldMapCatalog.GetRegions(GameDataProvider.Shared))
		{
			foreach (MapDestination destination in region.Destinations)
			{
				if (destination.Kind == MapDestinationKind.Hunt && string.Equals(destination.Key, mapKey, StringComparison.Ordinal))
				{
					return destination;
				}
			}
		}
		return null;
	}

	private static string TeleportMemoryFailureText(TeleportMemoryFailure failure)
	{
		return failure switch
		{
			TeleportMemoryFailure.Dead => "已死亡，無法使用", 
			TeleportMemoryFailure.MemoryUnavailable => "需要傳送控制戒指才能記憶位置", 
			TeleportMemoryFailure.CapacityReached => "記憶位置已滿，先刪一個", 
			TeleportMemoryFailure.InvalidName => "請輸入位置名稱", 
			TeleportMemoryFailure.DuplicateName => "已經有同名的位置了", 
			TeleportMemoryFailure.InvalidMap => "這個位置無法記憶", 
			TeleportMemoryFailure.InvalidPosition => "這個座標無法記憶", 
			TeleportMemoryFailure.UnknownLocation => "找不到這個記憶位置", 
			TeleportMemoryFailure.ControlRingRequired => "需要傳送控制戒指", 
			TeleportMemoryFailure.NoTeleportResource => "沒有 MP 也沒有瞬間移動卷軸", 
			_ => "無法使用", 
		};
	}

	private void LoadL1jTraps(MapTopology topology)
	{
		L1jTrapCatalog catalog = L1jTrapCatalog.Load(GameDataProvider.Shared);
		_l1jTraps = new L1jTrapRuntime(catalog, _mapKey, topology.IsLegalCell, StringComparer.Ordinal.GetHashCode(_mapKey));
		_trapClockMs = 0L;
		_lastTrapPlayerCell = (topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY) ? new(int, int)?((localX, localY)) : (((int, int)?)null));
	}

	private void L1jTrapStep(double delta)
	{
		if (_l1jTraps == null || _topology == null || _pendingScreenTransition != null || _engine.Player.Dead)
		{
			return;
		}
		checked
		{
			_trapClockMs += Math.Max(0L, (long)Math.Round(delta * 1000.0));
			if (!_topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY))
			{
				return;
			}
			(int, int)? lastTrapPlayerCell = _lastTrapPlayerCell;
			int num = localX;
			int num2 = localY;
			bool hasValue = lastTrapPlayerCell.HasValue;
			if (hasValue)
			{
				if (!hasValue)
				{
					return;
				}
				(int, int) valueOrDefault = lastTrapPlayerCell.GetValueOrDefault();
				if (valueOrDefault.Item1 == num && valueOrDefault.Item2 == num2)
				{
					return;
				}
			}
			_lastTrapPlayerCell = (localX, localY);
			foreach (L1jTrapActivation item in _l1jTraps.OnPlayerMoved(localX, localY, _trapClockMs))
			{
				ActivateL1jTrap(item);
				if (_pendingScreenTransition != null)
				{
					break;
				}
			}
		}
	}

	private void ActivateL1jTrap(L1jTrapActivation activation)
	{
		L1jTrapDefinition definition = activation.Definition;
		_engine.Engine.ApplyL1jTrap(_engine.Player, definition);
		switch (definition.Kind)
		{
		case L1jTrapKind.Monster:
			SpawnTrapMonsters(activation, definition);
			break;
		case L1jTrapKind.Teleport:
			TeleportFromTrap(definition);
			break;
		}
	}

	private void DetectL1jTraps(string? skillId)
	{
		bool flag = _l1jTraps == null || _topology == null;
		if (!flag)
		{
			bool flag2 = ((skillId == "sk_reveal" || skillId == "sk_greater_reveal") ? true : false);
			flag = !flag2;
		}
		if (flag || !_topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY))
		{
			return;
		}
		IReadOnlyList<L1jTrapActivation> readOnlyList = _l1jTraps.Detect(localX, localY, 15, _trapClockMs);
		int num = 0;
		foreach (L1jTrapActivation item in readOnlyList)
		{
			if (item.Definition.Detectionable)
			{
				num++;
				ShowDetectedTrap(item);
			}
		}
		if (num > 0)
		{
			SlabLog($"[color=#e6c76a]發現陷阱 ×{num}[/color]");
		}
	}

	private void ShowDetectedTrap(L1jTrapActivation activation)
	{
		if (_topology != null)
		{
			(double X, double Y) tuple = _topology.DisplayPixelCenter(activation.CellX, activation.CellY);
			double item = tuple.X;
			double item2 = tuple.Y;
			Label label = new Label
			{
				Text = "⚠ " + activation.Definition.Note,
				Position = new Vector2((float)item - 70f, (float)item2 - 54f),
				Size = new Vector2(140f, 28f),
				HorizontalAlignment = HorizontalAlignment.Center,
				ZIndex = Depth.Of((float)item2) + 1,
				MouseFilter = MouseFilterEnum.Ignore
			};
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", Color.FromHtml("#ffd76a".AsSpan()));
			label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#25180b".AsSpan()));
			label.AddThemeConstantOverride("outline_size", 3);
			_arena.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			Tween tween = label.CreateTween();
			tween.TweenInterval(0.8);
			tween.TweenProperty(label, "modulate:a", 0.0, 0.35);
			tween.TweenCallback(Callable.From(label.QueueFree));
		}
	}

	private void SpawnTrapMonsters(L1jTrapActivation activation, L1jTrapDefinition trap)
	{
		if (_topology == null || trap.MonsterMobKey == null)
		{
			return;
		}
		List<(int, int)> list = new List<(int, int)>();
		for (int i = -5; i <= 5; i++)
		{
			list.Add((activation.CellX + i, activation.CellY - 5));
			list.Add((activation.CellX + i, activation.CellY + 5));
			if ((i != -5 && i != 5) || 1 == 0)
			{
				list.Add((activation.CellX - 5, activation.CellY + i));
				list.Add((activation.CellX + 5, activation.CellY + i));
			}
		}
		for (int num = list.Count - 1; num > 0; num--)
		{
			int num2 = _rng.Next(num + 1);
			List<(int, int)> list2 = list;
			int index = num;
			List<(int, int)> list3 = list;
			int index2 = num2;
			(int, int) value = list[num2];
			(int, int) value2 = list[num];
			list2[index] = value;
			list3[index2] = value2;
		}
		int num3 = 0;
		foreach (var (localX, localY) in list)
		{
			if (num3 >= trap.MonsterCount)
			{
				break;
			}
			if (_topology.IsLegalCell(localX, localY))
			{
				(double X, double Y) tuple2 = _topology.DisplayPixelCenter(localX, localY);
				double item = tuple2.X;
				double item2 = tuple2.Y;
				WorldPoint worldPoint = new WorldPoint(item, item2);
				if (!_engine.Engine.IsGridCellOccupied(worldPoint))
				{
					_engine.SpawnMob(trap.MonsterMobKey, worldPoint);
					num3++;
				}
			}
		}
	}

	private void TeleportFromTrap(L1jTrapDefinition trap)
	{
		if (trap.TeleportMapKey == null)
		{
			SlabLog("[color=#e2938f]陷阱傳送失敗 — 原版目的地地圖尚未建立[/color]");
			return;
		}
		MapTopology mapTopology = TryLoadTopology(trap.TeleportMapKey);
		if (mapTopology == null || !mapTopology.ContainsLocalCell(trap.TeleportCellX, trap.TeleportCellY))
		{
			SlabLog("[color=#e2938f]陷阱傳送失敗 — 找不到目的地[/color]");
			return;
		}
		var (num, num2) = mapTopology.DisplayPixelCenter(trap.TeleportCellX, trap.TeleportCellY);
		if (string.Equals(trap.TeleportMapKey, _mapKey, StringComparison.Ordinal))
		{
			RelocatePlayerGroup(new WorldPoint(num, num2));
			return;
		}
		_session.HuntMap = trap.TeleportMapKey;
		_session.PendingHuntSpawn = (num, num2);
		SaveManager.Save(_session);
		_pendingScreenTransition = ChangeHuntMap;
	}

	private void OpenArenaManagerPanel(L1jNpcSpawn npc, L1jUbArena arena)
	{
		CloseL1jWorldNpcPanel();
		Vector2 size = new Vector2(480f, 360f);
		Control control = new Control
		{
			Position = new Vector2(Mathf.Round((WorldView.X - size.X) * 0.5f), Mathf.Round((WorldView.Y - size.Y) * 0.5f)),
			Size = size,
			ZIndex = 1950,
			MouseFilter = MouseFilterEnum.Stop
		};
		control.AddChild(new ColorRect
		{
			Color = Color.FromHtml("#141118".AsSpan()),
			Size = size,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Position = new Vector2(12f, 10f),
			Size = new Vector2(size.X - 24f, size.Y - 20f)
		};
		vBoxContainer.AddThemeConstantOverride("separation", 5);
		control.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(ArenaLabel(arena.Name, "#e6c76a", 16), forceReadableName: false, InternalMode.Disabled);
		int minuteOfDay = ArenaMinuteOfDay();
		int num = L1jUltimateBattleRules.MinutesUntilStart(arena, minuteOfDay);
		vBoxContainer.AddChild(ArenaLabel($"入場等級 {arena.MinLevel}~{arena.MaxLevel}\u3000四回合\u3000每日 {arena.OpenTimes.Count} 場", "#c9d1de", 13), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(ArenaLabel((num <= 0) ? "下一場：即將開始" : $"下一場：約 {num} 分鐘後（開賽前 5 分鐘開門）", "#c9d1de", 13), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(ArenaLabel("開放時刻：" + string.Join("、", arena.OpenTimes.Select(FormatOpenTime)), "#8b95a6", 12), forceReadableName: false, InternalMode.Disabled);
		L1jUbEntryFailure l1jUbEntryFailure = L1jUltimateBattleRules.CheckEntry(arena, _engine.Player.Level, _engine.Player.ClassId, _session.Build.Male, minuteOfDay);
		if (l1jUbEntryFailure == L1jUbEntryFailure.None)
		{
			Button button = new Button
			{
				Text = "⚔ 入場",
				CustomMinimumSize = new Vector2(0f, 32f)
			};
			button.AddThemeFontSizeOverride("font_size", 14);
			button.Pressed += delegate
			{
				CloseL1jWorldNpcPanel();
				EnterUltimateBattle(arena);
			};
			vBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		}
		else
		{
			vBoxContainer.AddChild(ArenaLabel(L1jUltimateBattleRules.FailureText(l1jUbEntryFailure), "#e2938f", 13), forceReadableName: false, InternalMode.Disabled);
		}
		Button button2 = new Button
		{
			Text = "關閉",
			CustomMinimumSize = new Vector2(0f, 28f)
		};
		button2.Pressed += CloseL1jWorldNpcPanel;
		vBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		_worldNpcPanel = control;
		AddAboveBarPanel(control);
	}

	private void EnterUltimateBattle(L1jUbArena arena)
	{
		L1jUltimateBattleCatalog catalog = L1jUltimateBattleCatalog.Load(GameDataProvider.Shared);
		int pattern = L1jUltimateBattleRules.PickPattern(arena, _ubRng);
		double countdownSeconds = Math.Max(0.0, (double)L1jUltimateBattleRules.MinutesUntilStart(arena, ArenaMinuteOfDay()) * 60.0);
		_session.UltimateBattle = new L1jUbSession(catalog, arena, pattern, countdownSeconds);
		_session.HuntMap = arena.MapKey;
		SaveManager.Save(_session);
		_pendingScreenTransition = ChangeHuntMap;
	}

	private void UltimateBattleStep(double delta)
	{
		if (!InUltimateBattle)
		{
			return;
		}
		foreach (L1jUbStep item in _session.UltimateBattle.Advance(delta))
		{
			switch (item.Kind)
			{
			case L1jUbStepKind.SpawnGroup:
			{
				L1jUbWaveGroup l1jUbWaveGroup = item.Group;
				if ((object)l1jUbWaveGroup != null)
				{
					for (int i = 0; i < l1jUbWaveGroup.Count; i++)
					{
						_engine.SpawnMobAroundPlayer(l1jUbWaveGroup.MobKey);
					}
					SlabLog($"[color=#e2938f]第 {item.Round} 回合：{l1jUbWaveGroup.Note} ×{l1jUbWaveGroup.Count}[/color]");
				}
				break;
			}
			case L1jUbStepKind.Supplies:
				GrantUltimateBattleSupplies(item.Round);
				break;
			case L1jUbStepKind.Finished:
				FinishUltimateBattle();
				return;
			}
		}
	}

	private void GrantUltimateBattleSupplies(int round)
	{
		IReadOnlyList<L1jUbSupply> readOnlyList = L1jUltimateBattleCatalog.Load(GameDataProvider.Shared).Supplies(round);
		if (readOnlyList.Count == 0)
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (L1jUbSupply item in readOnlyList)
		{
			if (item.ItemKey.Length == 0)
			{
				CombatWallet.Add(_engine.Player, item.Total);
				list.Add($"金幣 {item.Total:N0}");
			}
			else
			{
				CombatInventory.Add(_engine.Player, item.ItemKey, item.Total);
				list.Add($"{ItemDisplayName(item.ItemKey)} ×{item.Total:N0}");
			}
		}
		SlabLog($"[color=#8fdd8f]第 {round} 回合補給：{string.Join("、", list)}[/color]");
	}

	private void FinishUltimateBattle()
	{
		_session.UltimateBattle = null;
		SlabLog("[color=#ffd76a]★ 無限大賽結束，離開競技場[/color]");
		SaveManager.Save(_session);
		_pendingScreenTransition = ReturnToTown;
	}

	private static int ArenaMinuteOfDay()
	{
		DateTime now = DateTime.Now;
		return now.Hour * 60 + now.Minute;
	}

	private static string FormatOpenTime(int hhmm)
	{
		return $"{hhmm / 100:00}:{hhmm % 100:00}";
	}

	private static Label ArenaLabel(string text, string color, int fontSize)
	{
		Label label = new Label();
		label.Text = text;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.AddThemeColorOverride("font_color", Color.FromHtml(color.AsSpan()));
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private void ToggleWorldAtlas()
	{
		if (!TryGetCurrentWorldAtlasDefinition(out WorldAtlasDefinition _))
		{
			SlabLog("[color=#e8c07a]目前區域尚未配置地圖。[/color]");
		}
		else
		{
			ToggleRightAnchor("world-atlas", BuildWorldAtlasPanel);
		}
	}

	private Control? BuildWorldAtlasPanel()
	{
		if (!TryGetCurrentWorldAtlasDefinition(out WorldAtlasDefinition definition) || (object)definition == null)
		{
			return null;
		}
		_worldAtlasDefinition = definition;
		Vector2 size = new Vector2(920f, 454f);
		(Control, Control) tuple = ClassicMapFrame.Create(new Vector2((View.X - size.X) * 0.5f, 8f), size, CloseClassicRight, 1950);
		Control panel = tuple.Item1;
		Control item = tuple.Item2;
		_worldAtlasPanel = panel;
		panel.TreeExiting += delegate
		{
			ReleaseWorldAtlasPanel(panel);
		};
		panel.AddChild(ClassicMapFrame.Title(definition.Title), forceReadableName: false, InternalMode.Disabled);
		_worldAtlasCaption = new Label
		{
			Position = new Vector2(0f, 30f),
			Size = new Vector2(item.Size.X, 24f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_worldAtlasCaption.AddThemeFontSizeOverride("font_size", 14);
		_worldAtlasCaption.AddThemeColorOverride("font_color", Color.FromHtml("#f1d47a".AsSpan()));
		_worldAtlasCaption.AddThemeColorOverride("font_outline_color", Color.FromHtml("#211406".AsSpan()));
		_worldAtlasCaption.AddThemeConstantOverride("outline_size", 3);
		item.AddChild(_worldAtlasCaption, forceReadableName: false, InternalMode.Disabled);
		float num = item.Size.X - 8f;
		float num2 = item.Size.Y - 58f - 4f - 34f;
		float num3 = Mathf.Min(num / (float)definition.PixelWidth, num2 / (float)definition.PixelHeight);
		Vector2 size2 = new Vector2((float)definition.PixelWidth * num3, (float)definition.PixelHeight * num3);
		Vector2 position = new Vector2((item.Size.X - size2.X) * 0.5f, 58f + (num2 - size2.Y) * 0.5f);
		_worldAtlasImageRect = new Rect2(position, size2);
		TextureRect node = new TextureRect
		{
			Texture = GodotDataFiles.TryLoadTexture(definition.AssetPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Position = position,
			Size = size2,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore
		};
		item.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		AddWorldAtlasDungeonMarkers(item, definition);
		_worldAtlasMarker = BuildWorldAtlasMarker();
		item.AddChild(_worldAtlasMarker, forceReadableName: false, InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Position = new Vector2(0f, item.Size.Y - 34f + 2f),
			Size = new Vector2(item.Size.X, 32f),
			Alignment = BoxContainer.AlignmentMode.Center
		};
		hBoxContainer.AddThemeConstantOverride("separation", 12);
		Button button = new Button
		{
			Text = "儲存座標",
			CustomMinimumSize = new Vector2(130f, 28f)
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		button.Pressed += delegate
		{
			OpenTeleportMemoryFromWorldAtlas(focusName: true);
		};
		hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		Button button2 = new Button
		{
			Text = "指定傳送",
			CustomMinimumSize = new Vector2(130f, 28f)
		};
		button2.AddThemeFontSizeOverride("font_size", 13);
		button2.Pressed += delegate
		{
			OpenTeleportMemoryFromWorldAtlas(focusName: false);
		};
		hBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		Button button3 = new Button
		{
			Text = "選擇地圖",
			CustomMinimumSize = new Vector2(130f, 28f)
		};
		button3.AddThemeFontSizeOverride("font_size", 13);
		button3.Pressed += OpenMapSelectionPanel;
		hBoxContainer.AddChild(button3, forceReadableName: false, InternalMode.Disabled);
		item.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		RefreshWorldAtlasLocation();
		return panel;
	}

	private void OpenTeleportMemoryFromWorldAtlas(bool focusName)
	{
		CloseClassicRight();
		if (_tpMemoryPanel != null)
		{
			_tpMemoryPanel.QueueFree();
			_tpMemoryPanel = null;
		}
		OpenTeleportMemoryPanel(TeleportSource.Skill, PlayerSkillMpCost("sk_teleport"));
		if (focusName)
		{
			_tpMemoryName?.GrabFocus();
		}
	}

	private bool TryGetCurrentWorldAtlasDefinition(out WorldAtlasDefinition? definition)
	{
		definition = null;
		MapTopology topology = _topology;
		if (topology == null || string.IsNullOrWhiteSpace(topology.PreviewFile) || topology.PreviewWidth <= 0 || topology.PreviewHeight <= 0)
		{
			return false;
		}
		string title2;
		string title = ((WorldAtlasCatalog.TryGetTitle(_mapKey, out title2) && !string.IsNullOrWhiteSpace(title2)) ? title2 : MapLinks.DisplayName(GameDataProvider.Shared, _mapKey));
		definition = WorldAtlasCatalog.CreateLocalDefinition(topology, _mapKey, title);
		return true;
	}

	private void AddWorldAtlasDungeonMarkers(Control body, WorldAtlasDefinition definition)
	{
		MapTopology topology = _topology;
		if (topology == null)
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (MapLinks.Gate item in MapLinks.For(_mapKey))
		{
			if (item.ToTown || string.IsNullOrWhiteSpace(item.SourceLandmarkId) || !hashSet.Add(item.SourceLandmarkId))
			{
				continue;
			}
			MapLandmark mapLandmark = default(MapLandmark);
			bool flag = false;
			foreach (MapLandmark landmark in topology.Landmarks)
			{
				if (string.Equals(landmark.Id, item.SourceLandmarkId, StringComparison.Ordinal))
				{
					mapLandmark = landmark;
					flag = true;
					break;
				}
			}
			if (flag && WorldAtlasCatalog.TryLocate(definition, mapLandmark.GameX, mapLandmark.GameY, out var location))
			{
				Control control = BuildWorldAtlasDungeonMarker(item.TargetName);
				control.Position = new Vector2(_worldAtlasImageRect.Position.X + (float)(location.PixelX / (double)definition.PixelWidth) * _worldAtlasImageRect.Size.X, _worldAtlasImageRect.Position.Y + (float)(location.PixelY / (double)definition.PixelHeight) * _worldAtlasImageRect.Size.Y) - control.Size * 0.5f;
				body.AddChild(control, forceReadableName: false, InternalMode.Disabled);
			}
		}
	}

	private static Control BuildWorldAtlasDungeonMarker(string targetName)
	{
		Control control = new Control
		{
			Size = new Vector2(14f, 14f),
			MouseFilter = MouseFilterEnum.Pass,
			TooltipText = "入口：" + targetName
		};
		StyleBoxFlat stylebox = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#2b1a0c".AsSpan()),
			BorderColor = Color.FromHtml("#f1d47a".AsSpan()),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2
		};
		Panel panel = new Panel
		{
			Size = control.Size,
			MouseFilter = MouseFilterEnum.Ignore,
			RotationDegrees = 45f,
			PivotOffset = control.Size * 0.5f
		};
		panel.AddThemeStyleboxOverride("panel", stylebox);
		control.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
		StyleBoxFlat stylebox2 = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#f1d47a".AsSpan())
		};
		Panel panel2 = new Panel
		{
			Position = new Vector2(5f, 5f),
			Size = new Vector2(4f, 4f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel2.AddThemeStyleboxOverride("panel", stylebox2);
		control.AddChild(panel2, forceReadableName: false, InternalMode.Disabled);
		return control;
	}

	private static Control BuildWorldAtlasMarker()
	{
		Control control = new Control
		{
			Size = new Vector2(18f, 18f),
			MouseFilter = MouseFilterEnum.Ignore,
			TooltipText = "玩家目前位置"
		};
		StyleBoxFlat stylebox = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#f6d365".AsSpan()),
			BorderColor = Color.FromHtml("#3b1608".AsSpan()),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 9,
			CornerRadiusTopRight = 9,
			CornerRadiusBottomLeft = 9,
			CornerRadiusBottomRight = 9
		};
		Panel panel = new Panel
		{
			Size = control.Size,
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", stylebox);
		control.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
		StyleBoxFlat stylebox2 = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#e53935".AsSpan()),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4
		};
		Panel panel2 = new Panel
		{
			Position = new Vector2(5f, 5f),
			Size = new Vector2(8f, 8f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel2.AddThemeStyleboxOverride("panel", stylebox2);
		control.AddChild(panel2, forceReadableName: false, InternalMode.Disabled);
		return control;
	}

	private void ReleaseWorldAtlasPanel(Control panel)
	{
		if (_worldAtlasPanel == panel)
		{
			_worldAtlasPanel = null;
			_worldAtlasMarker = null;
			_worldAtlasCaption = null;
			_worldAtlasDefinition = null;
			_worldAtlasImageRect = default(Rect2);
		}
	}

	private void RefreshWorldAtlasLocation(bool keepAreaName = false)
	{
		MapTopology topology = _topology;
		if (topology == null || !topology.TryLocalCellAtDisplayPixel(_engine.Player.Pos.X, _engine.Player.Pos.Y, out var localX, out var localY))
		{
			return;
		}
		(int X, int Y) tuple = topology.ToGameCell(localX, localY);
		int item = tuple.X;
		int item2 = tuple.Y;
		string placeName;
		bool flag = WorldAtlasCatalog.TryResolvePlaceName(_mapKey, item, item2, out placeName);
		IReadOnlyList<WorldAtlasPlaceAnchor> anchors;
		if (!keepAreaName)
		{
			if (flag && placeName.Length > 0)
			{
				SetAreaName(placeName);
			}
			else if (WorldAtlasCatalog.TryGetAnchors(_mapKey, out anchors) && _mapName.Length > 0)
			{
				SetAreaName(_mapName);
			}
		}
		if (_worldAtlasPanel != null && (object)_worldAtlasDefinition != null && string.Equals(_worldAtlasDefinition.MapKey, _mapKey, StringComparison.Ordinal) && WorldAtlasCatalog.TryLocate(_worldAtlasDefinition, item, item2, out var location))
		{
			bool flag2 = WorldAtlasCatalog.TryGetAnchors(_mapKey, out anchors);
			if (!keepAreaName && !flag && !flag2 && location.PlaceName.Length > 0)
			{
				SetAreaName(location.PlaceName);
			}
			string text = (flag ? placeName : (flag2 ? _mapName : location.PlaceName));
			if (_worldAtlasCaption != null && !string.Equals(_worldAtlasCaptionName, text, StringComparison.Ordinal))
			{
				_worldAtlasCaptionName = text;
				_worldAtlasCaption.Text = "目前位置：" + text;
			}
			if (_worldAtlasMarker != null)
			{
				float x = _worldAtlasImageRect.Position.X + (float)(location.PixelX / (double)_worldAtlasDefinition.PixelWidth) * _worldAtlasImageRect.Size.X;
				float y = _worldAtlasImageRect.Position.Y + (float)(location.PixelY / (double)_worldAtlasDefinition.PixelHeight) * _worldAtlasImageRect.Size.Y;
				_worldAtlasMarker.Position = new Vector2(x, y) - _worldAtlasMarker.Size * 0.5f;
				_worldAtlasMarker.TooltipText = "玩家目前位置：" + location.PlaceName;
			}
		}
	}

	private Combatant? PickWorldCharacterTarget(Vector2 world, Func<Combatant, bool> accepts)
	{
		Combatant combatant = null;
		bool flag = false;
		float num = float.MaxValue;
		float num2 = 7225f; // ~85px tolerance radius so clicking near a monster easily targets it
		foreach (Combatant combatant2 in _engine.Combatants)
		{
			if (accepts(combatant2))
			{
				float num3 = world.DistanceSquaredTo(ToVec(combatant2.Pos));
				ArpgActor value;
				bool flag2 = _views.TryGetValue(combatant2, out value) && value.ContainsVisibleBodyPoint(world);
				bool flag3 = num3 < num2;
				if ((flag2 || flag3) && (combatant == null || !(!flag2 || flag) || (flag2 == flag && !(num3 >= num))))
				{
					combatant = combatant2;
					flag = flag2;
					num = num3;
				}
			}
		}
		return combatant;
	}
}
