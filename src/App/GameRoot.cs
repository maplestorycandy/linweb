using System;
using System.IO;
using Godot;
using Godot.Collections;
using IdleLineage.Data;
using IdleLineage.Ui;

namespace IdleLineage.App;

public sealed partial class GameRoot : Control
{
	private AtlasBridge _atlas;

	private Control? _current;

	private GameSession? _session;

	private const int AcceptanceSaveSlot = 8;

	private const int MainFontWeight = 700;

	public override void _Ready()
	{
		GameCursorManager.Init();
		PatchPacks.LoadAll();
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		base.MouseFilter = MouseFilterEnum.Ignore;
		ApplyTheme();
		try
		{
			GodotDataFiles.EnsureInstalled();
			MapLinks.ConfigureRastabad("res://data");
			MapLinks.ConfigureClassicMaps("res://data");
		}
		catch (Exception ex)
		{
			GD.PushError("[MapLinks] canonical 地圖拓撲載入失敗：" + ex.Message);
		}
		_atlas = AtlasBridge.Resolve(this);
		GameAudio.Attach(this);
		string environment = OS.GetEnvironment("IDLE_LINEAGE_PREVIEW_LOAD_SLOT");
		if (environment.Length > 0 && int.TryParse(environment, out var result))
		{
			ShowMenu();
			LoadGame(result);
		}
		else if (string.Equals(OS.GetEnvironment("IDLE_LINEAGE_PREVIEW_SCREEN"), "create", StringComparison.OrdinalIgnoreCase))
		{
			ShowCreate();
		}
		else
		{
			ShowMenu();
		}
	}

	public override void _Notification(int what)
	{
		if ((long)what == 1006)
		{
			if (_current is ArpgEngineScreen arpg)
			{
				try
				{
					arpg.SyncAllOfflineCompanions();
				}
				catch { }
			}
			if (_session != null)
			{
				SaveManager.Save(_session);
			}
		}
	}

	private void Show(Control screen)
	{
		_current?.QueueFree();
		_current = screen;
		AddChild(screen, forceReadableName: false, InternalMode.Disabled);
	}

	private void ShowMenu()
	{
		GameAudio.Instance?.PlayScene(MapMusicCatalog.LoginTrack(GameDataProvider.Shared));
		MenuScreen menuScreen = new MenuScreen();
		Show(menuScreen);
		menuScreen.Init(_atlas, NewGameInSlot, LoadGame, (int slot) => SaveManager.DeleteSlot(slot), SaveManager.ExportToClipboard, ImportSave);
	}

	private void NewGameInSlot(int slot)
	{
		SaveManager.CurrentSlot = slot;
		ShowCreate();
	}

	private (bool ok, string msg) ImportSave(int slot)
	{
		return SaveManager.ImportFromClipboard(slot) switch
		{
			SaveManager.ImportOutcome.Success => (ok: true, msg: "✓ 匯入成功！按「繼續遊戲」載入（原存檔已備份）"), 
			SaveManager.ImportOutcome.EmptyClipboard => (ok: false, msg: "✗ 剪貼簿是空的——先在別台匯出進度並複製"), 
			SaveManager.ImportOutcome.BadFormat => (ok: false, msg: "✗ 剪貼簿內容不是有效的存檔碼"), 
			SaveManager.ImportOutcome.InvalidSave => (ok: false, msg: "✗ 存檔碼損毀或無法還原，未匯入"), 
			_ => (ok: false, msg: "✗ 匯入寫入失敗（原存檔未動）"), 
		};
	}

	private void LoadGame(int slot)
	{
		GameSession gameSession = SaveManager.Load(slot);
		if (gameSession != null)
		{
			ApplyRestartGetback(gameSession);
			if (gameSession.LastHuntMap.Length > 0 && GameDataProvider.Shared.Maps[gameSession.LastHuntMap] != null)
			{
				gameSession.HuntMap = gameSession.LastHuntMap;
				gameSession.PendingHuntSpawn = (gameSession.LastHuntX, gameSession.LastHuntY);
				_session = gameSession;
				ClanStore.Sync(gameSession);
				ShowHunt(gameSession);
			}
			else
			{
				ShowTown(gameSession);
			}
		}
	}

	private void ShowCreate()
	{
		CreateScreen createScreen = new CreateScreen();
		Show(createScreen);
		createScreen.Init(_atlas, StartSession, ShowMenu);
		string environment = OS.GetEnvironment("IDLE_LINEAGE_PREVIEW_CREATE_CLASS");
		if (environment.Length != 0)
		{
			SaveManager.CurrentSlot = 8;
			bool male = !string.Equals(OS.GetEnvironment("IDLE_LINEAGE_PREVIEW_CREATE_GENDER"), "female", StringComparison.OrdinalIgnoreCase);
			if (!createScreen.RunPreviewCreation(environment, male, "驗收" + environment))
			{
				GD.PushError("[創角驗收] 沒有這個職業：" + environment);
			}
		}
	}

	private void StartSession(PlayerBuild build)
	{
		MapTopology startTopology = MapTopology.Load("res://assets/maps/l1j_map_2005");
		GameSession session = (_session = GameSession.CreateNewCharacter(build, startTopology));
		SaveManager.Save(session);
		ClanStore.Sync(session);
		ShowHunt(session);
	}

	private void ShowTown(GameSession session)
	{
		_session = session;
		IntegratedTownDefinition integratedTownDefinition = IntegratedTownCatalog.FindByTown(session.TownKey);
		if ((object)integratedTownDefinition != null)
		{
			session.HuntMap = integratedTownDefinition.MapKey;
		}
		session.LastHuntMap = "";
		SaveManager.Save(session);
		ClanStore.Sync(session);
		if ((object)integratedTownDefinition != null)
		{
			ShowHunt(session);
			return;
		}
		GameAudio instance = GameAudio.Instance;
		instance?.PlayScene(instance.TownScene(session.TownKey));
		TownScreen townScreen = new TownScreen();
		Show(townScreen);
		townScreen.Init(_atlas, session, delegate
		{
			ShowHunt(session);
		}, delegate
		{
			ShowTown(session);
		}, ShowMenu);
	}

	private void ShowHunt(GameSession session)
	{
		ArpgEngineScreen arpgEngineScreen = new ArpgEngineScreen();
		Show(arpgEngineScreen);
		arpgEngineScreen.Init(_atlas, session, delegate
		{
			ShowTown(session);
		}, delegate
		{
			ShowHunt(session);
		}, ShowMenu);
	}

	private void ApplyTheme()
	{
		SystemFont systemFont = new SystemFont();
		systemFont.FontNames = new string[2] { "Microsoft JhengHei UI", "Microsoft JhengHei" };
		systemFont.FontWeight = 700;
		systemFont.AllowSystemFallback = true;
		systemFont.Hinting = TextServer.Hinting.Normal;
		systemFont.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
		SystemFont systemFont2 = systemFont;
		Font defaultFont = systemFont2;
		if (ResourceLoader.Exists("res://assets/fonts/NotoSansTC-VF.ttf"))
		{
			FontFile fontFile = GD.Load<FontFile>("res://assets/fonts/NotoSansTC-VF.ttf");
			fontFile.Antialiasing = TextServer.FontAntialiasing.Gray;
			fontFile.Hinting = TextServer.Hinting.Normal;
			fontFile.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
			fontFile.Fallbacks = new Array<Font> { systemFont2 };
			defaultFont = new FontVariation
			{
				BaseFont = fontFile,
				VariationOpentype = new Dictionary { [TextServerManager.GetPrimaryInterface().NameToTag("wght")] = 700f }
			};
		}
		Theme theme = new Theme
		{
			DefaultFont = defaultFont,
			DefaultFontSize = 16
		};
		theme.SetStylebox("panel", "TooltipPanel", new StyleBoxEmpty());
		base.Theme = theme;
	}

	private static void ApplyRestartGetback(GameSession session)
	{
		if (!string.IsNullOrWhiteSpace(session.LastHuntMap) && L1jGetbackCatalog.Load(GameDataProvider.Shared).TryResolveRestart(session.LastHuntMap, out L1jGetbackRestartRow route) && (object)route != null)
		{
			L1jGetbackDestination destination = route.Destination;
			if (!destination.IsRuntimeResolved)
			{
				throw new InvalidDataException($"L1J restart destination {destination.MapId}:{destination.GameX},{destination.GameY} has no runtime map.");
			}
			session.LastHuntMap = destination.MapKey;
			session.LastHuntX = destination.DisplayX.Value;
			session.LastHuntY = destination.DisplayY.Value;
			if (!string.IsNullOrWhiteSpace(destination.TownKey))
			{
				session.TownKey = destination.TownKey;
			}
		}
	}
}
