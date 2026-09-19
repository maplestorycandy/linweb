using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Network;
using IdleLineage.Ui;

namespace IdleLineage.App;

public partial class ArpgEngineScreen
{
    private class RemotePlayerState
    {
        public Combatant Actor { get; set; } = null!;
        public ArpgActor View { get; set; } = null!;
        public Vector2 TargetPos { get; set; }
        public Vector2 VisualPos { get; set; }
        public bool IsMoving { get; set; }
        public float WalkProgress { get; set; }
        public double MoveHoldTimer { get; set; }
        public PanelContainer? ChatBubble { get; set; }
        public Label? ChatBubbleLabel { get; set; }
        public float ChatBubbleTimer { get; set; }
    }

    private class RemoteFollowerState
    {
        public Combatant Actor { get; set; } = null!;
        public ArpgActor View { get; set; } = null!;
        public Vector2 TargetPos { get; set; }
        public Vector2 VisualPos { get; set; }
        public bool IsMoving { get; set; }
        public float WalkProgress { get; set; }
        public string OwnerId { get; set; } = "";
    }

    public class ClientMobState
    {
        public Vector2 VisualPos { get; set; }
        public Vector2 TargetPos { get; set; }
        public int Facing8 { get; set; }
        public bool Stepping { get; set; }
        public float WalkProgress { get; set; }
        public double MoveHoldTimer { get; set; }
    }

    private readonly Dictionary<string, RemotePlayerState> _remotePlayerViews = new();
    private readonly Dictionary<string, RemoteFollowerState> _remoteFollowers = new();
    private readonly Dictionary<string, ClientMobState> _clientMobTargets = new();
    private double _netSyncTimer = 0.0;
    private double _netMobSyncTimer = 0.0;
    private double _followerSyncTimer = 0.0;
    private double _partyRefreshTimer = 0.0;
    private Vector2 _lastSentNetPos;
    private int _lastSentNetFacing = -1;
    private string _lastSentWeaponId = "";
    private string _lastSentPoly = "";
    private bool _lastSentPoison = false;
    private double _lastSentHp = -1.0;
    private double _lastSentMp = -1.0;
    private PartyHud? _partyHud;
    private string _hostPlayerId = "";
    private string _autoFollowPlayerId = "";
    private double _autoFollowTimer = 0.0;
    private readonly Dictionary<string, Combatant> _offlineCompanionActors = new(StringComparer.Ordinal);

    private void InitMultiplayer()
    {
        NetworkManager.Instance.OnRemotePlayerJoined += HandleRemotePlayerJoined;
        NetworkManager.Instance.OnRemotePlayerMoved += HandleRemotePlayerMoved;
        NetworkManager.Instance.OnRemotePlayerEquipped += HandleRemotePlayerEquipped;
        NetworkManager.Instance.OnPlayerHpSynced += HandlePlayerHpSynced;
        NetworkManager.Instance.OnRemotePlayerAction += HandleRemotePlayerAction;
        NetworkManager.Instance.OnChatReceived += HandleRemoteChatReceived;
        NetworkManager.Instance.OnRemotePlayerLeft += HandleRemotePlayerLeft;

        // Mob sync
        NetworkManager.Instance.OnMobSpawned += HandleMobSpawned;
        NetworkManager.Instance.OnMobBatchMoved += HandleMobBatchMoved;
        NetworkManager.Instance.OnMobHitReceived += HandleMobHitReceived;
        NetworkManager.Instance.OnMobHpSynced += HandleMobHpSynced;
        NetworkManager.Instance.OnMobDied += HandleMobDied;

        // Item drop sync
        NetworkManager.Instance.OnItemDropped += HandleRemoteItemDropped;
        NetworkManager.Instance.OnItemPickedUp += HandleRemoteItemPickedUp;

        // Follower sync (pets, summons, magic dolls)
        NetworkManager.Instance.OnFollowerSynced += HandleRemoteFollowerSynced;

        if (_engine?.Engine != null)
        {
            _engine.Engine.DisableMobAi = NetworkManager.Instance.IsConnected && !NetworkManager.Instance.IsHost;
        }

        if (_partyHud == null && _hud != null)
        {
            _partyHud = new PartyHud();
            _partyHud.OnMemberClicked += HandlePartyMemberClicked;
            _partyHud.OnTeleportToMemberClicked += HandlePartyMemberTeleport;
            _partyHud.OnFollowToMemberClicked += HandlePartyMemberFollow;
            _partyHud.OnOfflineClicked += HandlePartyOfflineClicked;
            _partyHud.OnDismissCompanionClicked += HandleDismissCompanionClicked;
            _hud.AddChild(_partyHud, forceReadableName: false, InternalMode.Disabled);
        }

        if (NetworkManager.Instance.IsConnected)
        {
            SendLocalHandshake();
            SlabLog("[color=#86efac]🌐【多人連線已啟用】已同步至多人房間！[/color]");
            RefreshPartyHud();
            SyncGroundDropsToPeers();
        }
        else
        {
            RefreshPartyHud();
        }
    }

    private void CleanupMultiplayer()
    {
        _autoFollowPlayerId = "";
        NetworkManager.Instance.OnRemotePlayerJoined -= HandleRemotePlayerJoined;
        NetworkManager.Instance.OnRemotePlayerMoved -= HandleRemotePlayerMoved;
        NetworkManager.Instance.OnRemotePlayerEquipped -= HandleRemotePlayerEquipped;
        NetworkManager.Instance.OnPlayerHpSynced -= HandlePlayerHpSynced;
        NetworkManager.Instance.OnRemotePlayerAction -= HandleRemotePlayerAction;
        NetworkManager.Instance.OnChatReceived -= HandleRemoteChatReceived;
        NetworkManager.Instance.OnRemotePlayerLeft -= HandleRemotePlayerLeft;

        NetworkManager.Instance.OnMobSpawned -= HandleMobSpawned;
        NetworkManager.Instance.OnMobBatchMoved -= HandleMobBatchMoved;
        NetworkManager.Instance.OnMobHitReceived -= HandleMobHitReceived;
        NetworkManager.Instance.OnMobHpSynced -= HandleMobHpSynced;
        NetworkManager.Instance.OnMobDied -= HandleMobDied;

        NetworkManager.Instance.OnItemDropped -= HandleRemoteItemDropped;
        NetworkManager.Instance.OnItemPickedUp -= HandleRemoteItemPickedUp;
        NetworkManager.Instance.OnFollowerSynced -= HandleRemoteFollowerSynced;

        if (_partyHud != null)
        {
            _partyHud.OnMemberClicked -= HandlePartyMemberClicked;
            _partyHud.OnTeleportToMemberClicked -= HandlePartyMemberTeleport;
            _partyHud.OnFollowToMemberClicked -= HandlePartyMemberFollow;
            _partyHud.OnOfflineClicked -= HandlePartyOfflineClicked;
            _partyHud.OnDismissCompanionClicked -= HandleDismissCompanionClicked;
            try { _partyHud.QueueFree(); } catch { }
            _partyHud = null;
        }

        if (_engine?.Engine != null)
        {
            _engine.Engine.DisableMobAi = false;
        }

        foreach (var state in _remotePlayerViews.Values)
        {
            try { _engine?.Engine?.Remove(state.Actor); } catch { }
            try { state.View.Free(); } catch { }
        }
        _remotePlayerViews.Clear();

        foreach (var fState in _remoteFollowers.Values)
        {
            try { _engine?.Engine?.Remove(fState.Actor); } catch { }
            try { fState.View.Free(); } catch { }
        }
        _remoteFollowers.Clear();

        _clientMobTargets.Clear();
    }

    private void MultiplayerStep(double delta)
    {
        try
        {
            NetworkManager.Instance.Update();

            if (_engine?.Player == null)
            {
                return;
            }

            if (!NetworkManager.Instance.IsConnected)
            {
                // Single-player / offline companion auto-follow logic
                if (!string.IsNullOrEmpty(_autoFollowPlayerId) && !_dead)
                {
                    if (_wasdMoving)
                    {
                        StopAutoFollow(userCancelled: true);
                    }
                    else
                    {
                        _autoFollowTimer -= delta;
                        if (_autoFollowTimer <= 0.0)
                        {
                            _autoFollowTimer = 0.18;
                            if (_offlineCompanionActors.TryGetValue(_autoFollowPlayerId, out var followAlly) && !followAlly.Dead)
                            {
                                Vector2 targetPos = ToVec(followAlly.Pos);
                                Vector2 myPos = PlayerPos();
                                float dist = myPos.DistanceTo(targetPos);
                                if (dist > 65f)
                                {
                                    _engine.SetPlayerMoveTarget(targetPos);
                                }
                                else if (dist <= 48f)
                                {
                                    _engine.StopPlayer();
                                }
                            }
                            else
                            {
                                StopAutoFollow(userCancelled: false);
                                SlabLog("[color=#e2938f]⚠️ 目標隊友不在身邊或已陣亡，自動跟隨已停止。[/color]");
                            }
                        }
                    }
                }

                _partyRefreshTimer += delta;
                if (_partyRefreshTimer >= 0.1)
                {
                    _partyRefreshTimer = 0.0;
                    RefreshPartyHud();
                }
                return;
            }

            if (_engine.Engine != null)
            {
                _engine.Engine.DisableMobAi = !NetworkManager.Instance.IsHost;
            }

            float dt = (float)delta;

            // 0. Auto-Follow Logic
            if (!string.IsNullOrEmpty(_autoFollowPlayerId) && !_dead)
            {
                if (_wasdMoving)
                {
                    StopAutoFollow(userCancelled: true);
                }
                else
                {
                    _autoFollowTimer -= delta;
                    if (_autoFollowTimer <= 0.0)
                    {
                        _autoFollowTimer = 0.18; // Refresh pathfinding ~5.5Hz

                        if (_remotePlayerViews.TryGetValue(_autoFollowPlayerId, out var followState))
                        {
                            Vector2 targetPos = followState.VisualPos != Vector2.Zero ? followState.VisualPos : ToVec(followState.Actor.Pos);
                            Vector2 myPos = PlayerPos();
                            float dist = myPos.DistanceTo(targetPos);

                            if (dist > 65f)
                            {
                                _engine.SetPlayerMoveTarget(targetPos);
                            }
                            else if (dist <= 48f)
                            {
                                _engine.StopPlayer();
                            }
                        }
                        else
                        {
                            StopAutoFollow(userCancelled: false);
                            SlabLog("[color=#e2938f]⚠️ 目標隊友不在當前地圖或已離線，自動跟隨已停止。[/color]");
                        }
                    }
                }
            }

            // 1. Update Remote Player Views & Walking Animation Progress
            foreach (var state in _remotePlayerViews.Values)
            {
                try
                {
                    float dist = state.VisualPos.DistanceTo(state.TargetPos);

                    if (state.MoveHoldTimer > 0)
                    {
                        state.MoveHoldTimer -= delta;
                        if (state.MoveHoldTimer <= 0)
                        {
                            state.IsMoving = false;
                        }
                    }

                    // Teleport / Map change: snap immediately only if really far away (>350px)
                    if (dist > 350f)
                    {
                        state.VisualPos = state.TargetPos;
                    }
                    else if (dist > 1.0f)
                    {
                        // Dynamic velocity catch-up matching player speed to avoid lag and accordion rubberband
                        float baseSpeed = (float)CombatModifierRules.EffectiveMoveSpeed(state.Actor);
                        float moveSpeed = Math.Max(baseSpeed * 1.3f, dist * 12f);
                        Vector2 next = state.VisualPos.MoveToward(state.TargetPos, moveSpeed * dt);
                        float actualDistMoved = state.VisualPos.DistanceTo(next);
                        state.VisualPos = next;

                        if (state.IsMoving || dist > 2.0f)
                        {
                            float advance = actualDistMoved > 0f ? (actualDistMoved / 36.0f) : (dt * 2.2f);
                            state.WalkProgress = (state.WalkProgress + advance) % 1.0f;
                        }
                    }
                    else
                    {
                        state.VisualPos = state.TargetPos;
                        if (state.IsMoving)
                        {
                            state.WalkProgress = (state.WalkProgress + dt * 2.2f) % 1.0f;
                        }
                    }

                    state.Actor.Pos = new WorldPoint(state.VisualPos.X, state.VisualPos.Y);

                    bool moving = state.IsMoving || dist > 2.0f;
                    UpdateView(state.View, state.Actor, (state.VisualPos, state.WalkProgress, moving), dt, false);

                    if (state.ChatBubble != null && state.ChatBubble.Visible)
                    {
                        if (state.ChatBubbleTimer > 0f)
                        {
                            state.ChatBubbleTimer -= dt;
                            Vector2 rPos = state.VisualPos != Vector2.Zero ? state.VisualPos : ToVec(state.Actor.Pos);
                            state.ChatBubble.Position = rPos + new Vector2(-state.ChatBubble.Size.X * 0.5f, -85f);
                            if (state.ChatBubbleTimer <= 0f)
                            {
                                Tween t = state.ChatBubble.CreateTween();
                                t.TweenProperty(state.ChatBubble, "modulate:a", 0f, 1.0);
                                t.TweenCallback(Callable.From(() => state.ChatBubble.Visible = false));
                            }
                        }
                    }
                }
                catch { }
            }

            // 1.5. Update Remote Follower Views (Pets, Summons, Magic Dolls)
            foreach (var fState in _remoteFollowers.Values)
            {
                try
                {
                    float fDist = fState.VisualPos.DistanceTo(fState.TargetPos);
                    if (fDist > 350f)
                    {
                        fState.VisualPos = fState.TargetPos;
                    }
                    else if (fDist > 1.0f)
                    {
                        float baseSpeed = (float)CombatModifierRules.EffectiveMoveSpeed(fState.Actor);
                        float fSpeed = Math.Max(baseSpeed * 1.3f, fDist * 12f);
                        Vector2 next = fState.VisualPos.MoveToward(fState.TargetPos, fSpeed * dt);
                        float actualDistMoved = fState.VisualPos.DistanceTo(next);
                        fState.VisualPos = next;

                        if (fState.IsMoving || fDist > 2.0f)
                        {
                            float advance = actualDistMoved > 0f ? (actualDistMoved / 36.0f) : (dt * 2.2f);
                            fState.WalkProgress = (fState.WalkProgress + advance) % 1.0f;
                        }
                    }
                    else
                    {
                        fState.VisualPos = fState.TargetPos;
                        if (fState.IsMoving)
                        {
                            fState.WalkProgress = (fState.WalkProgress + dt * 2.2f) % 1.0f;
                        }
                    }

                    fState.Actor.Pos = new WorldPoint(fState.VisualPos.X, fState.VisualPos.Y);
                    bool fMoving = fState.IsMoving || fDist > 2.0f;
                    UpdateView(fState.View, fState.Actor, (fState.VisualPos, fState.WalkProgress, fMoving), dt, false);
                }
                catch { }
            }

            // 2. Client-side Smooth Mob Movement & Walking Animation
            if (!NetworkManager.Instance.IsHost && _clientMobTargets.Count > 0)
            {
                foreach (var (mobId, info) in _clientMobTargets)
                {
                    Combatant? mob = _engine.Combatants.FirstOrDefault(c => c.Key == mobId);
                    if (mob != null && !mob.Dead)
                    {
                        if (info.VisualPos == Vector2.Zero)
                        {
                            info.VisualPos = ToVec(mob.Pos);
                        }

                        float dist = info.VisualPos.DistanceTo(info.TargetPos);

                        if (info.MoveHoldTimer > 0)
                        {
                            info.MoveHoldTimer -= delta;
                            if (info.MoveHoldTimer <= 0)
                            {
                                info.Stepping = false;
                            }
                        }

                        float actualDistMoved = 0f;
                        if (dist > 300f)
                        {
                            info.VisualPos = info.TargetPos;
                        }
                        else if (dist > 1.0f)
                        {
                            float baseSpeed = (float)CombatModifierRules.EffectiveMoveSpeed(mob);
                            float moveSpeed = Math.Max(baseSpeed * 1.3f, dist * 12f);
                            Vector2 next = info.VisualPos.MoveToward(info.TargetPos, moveSpeed * dt);
                            actualDistMoved = info.VisualPos.DistanceTo(next);
                            info.VisualPos = next;
                        }
                        else
                        {
                            info.VisualPos = info.TargetPos;
                        }

                        mob.Pos = new WorldPoint(info.VisualPos.X, info.VisualPos.Y);

                        bool mobMoving = info.Stepping || dist > 2.0f;
                        if (mobMoving)
                        {
                            float advance = actualDistMoved > 0f ? (actualDistMoved / 36.0f) : (dt * 2.2f);
                            info.WalkProgress = (info.WalkProgress + advance) % 1.0f;
                        }

                        mob.Facing8 = info.Facing8;
                    }
                }
            }

            // 3. Sync weapon change if local player switched weapon in bag
            string currentWeapon = _engine.Player.MainWeaponId ?? "";
            if (currentWeapon != _lastSentWeaponId)
            {
                _lastSentWeaponId = currentWeapon;
                NetworkManager.Instance.SendEquip(new EquipPacket
                {
                    MainWeaponId = currentWeapon,
                    WeaponPrefix = _build?.WeaponPrefix ?? ""
                });
            }

            // 4. Send Player Move, HP, MP & Polymorph (30Hz)
            _netSyncTimer += delta;
            if (_netSyncTimer >= 0.03)
            {
                _netSyncTimer = 0.0;
                var p = _engine.Player;
                Vector2 currentPos = _engine.RenderPos(p);
                int currentFacing = p.Facing8;
                bool isMoving = _wasdMoving || p.MoveTarget.HasValue || _engine.RenderWalk(p).Stepping || Math.Abs(p.VelX) > 0.1 || Math.Abs(p.VelY) > 0.1;
                bool hpChanged = Math.Abs(p.Hp - _lastSentHp) > 0.01;
                bool mpChanged = Math.Abs(p.Mp - _lastSentMp) > 0.01;
                string currentPoly = p.PolymorphForm ?? "";
                bool polyChanged = !string.Equals(currentPoly, _lastSentPoly, StringComparison.Ordinal);
                bool isPoisoned = L1jPoisonAttackRules.IsPoisoned(p);
                bool poisonChanged = isPoisoned != _lastSentPoison;

                if (currentPos.DistanceSquaredTo(_lastSentNetPos) > 0.01f || currentFacing != _lastSentNetFacing || hpChanged || mpChanged || polyChanged || poisonChanged)
                {
                    _lastSentNetPos = currentPos;
                    _lastSentNetFacing = currentFacing;
                    _lastSentHp = p.Hp;
                    _lastSentMp = p.Mp;
                    _lastSentPoly = currentPoly;
                    _lastSentPoison = isPoisoned;

                    NetworkManager.Instance.SendMove(new MovePacket
                    {
                        X = currentPos.X,
                        Y = currentPos.Y,
                        Facing8 = currentFacing,
                        Stepping = isMoving,
                        Hp = p.Hp,
                        MaxHp = p.MaxHp,
                        Mp = p.Mp,
                        MaxMp = p.MaxMp,
                        MapKey = _mapKey,
                        PolymorphForm = currentPoly,
                        IsPoisoned = isPoisoned
                    });
                    RefreshPartyHud();
                }
            }

            // 4.5. Broadcast Local Followers (Pets, Summons, Magic Dolls) (10Hz)
            _followerSyncTimer += delta;
            if (_followerSyncTimer >= 0.1)
            {
                _followerSyncTimer = 0.0;
                BroadcastLocalFollowers();
            }

            // Periodic Party HUD stats refresh (10Hz)
            _partyRefreshTimer += delta;
            if (_partyRefreshTimer >= 0.1)
            {
                _partyRefreshTimer = 0.0;
                RefreshPartyHud();
            }

            // 5. Host broadcasts living mob positions to clients at 30Hz
            if (NetworkManager.Instance.IsHost)
            {
                _netMobSyncTimer += delta;
                if (_netMobSyncTimer >= 0.033)
                {
                    _netMobSyncTimer = 0.0;
                    BroadcastHostMobs();
                }
            }
        }
        catch { }
    }

    private void BroadcastHostMobs()
    {
        try
        {
            var moves = new List<MobMoveEntry>();
            foreach (Combatant c in _engine.Combatants)
            {
                if (c.Kind == CombatantKind.Mob && !c.Dead)
                {
                    Vector2 rPos = _engine.RenderPos(c);
                    moves.Add(new MobMoveEntry
                    {
                        MobId = c.Key,
                        X = rPos.X,
                        Y = rPos.Y,
                        Facing8 = c.Facing8,
                        Stepping = _engine.Engine.IsStepping(c) || c.MoveTarget.HasValue
                    });
                }
            }
            if (moves.Count > 0)
            {
                NetworkManager.Instance.SendMobBatchMove(new MobBatchMovePacket { Moves = moves });
            }
        }
        catch { }
    }

    public void NoteHostSpawnedMob(Combatant mob, string mobKey)
    {
        if (!NetworkManager.Instance.IsConnected || !NetworkManager.Instance.IsHost) return;
        try
        {
            NetworkManager.Instance.SendMobSpawn(new MobSpawnPacket
            {
                MobId = mob.Key,
                MobKey = mobKey,
                X = mob.Pos.X,
                Y = mob.Pos.Y,
                Hp = mob.Hp,
                MaxHp = mob.MaxHp,
                Facing8 = mob.Facing8,
                MapKey = _mapKey
            });
        }
        catch { }
    }

    public void NoteCombatDamageEvent(Combatant source, Combatant target, double dmg)
    {
        if (!NetworkManager.Instance.IsConnected || target == null) return;
        try
        {
            if (target == _engine.Player)
            {
                NetworkManager.Instance.SendPlayerHpSync(new PlayerHpSyncPacket
                {
                    PlayerId = NetworkManager.Instance.LocalPlayerId,
                    Hp = _engine.Player.Hp,
                    MaxHp = _engine.Player.MaxHp,
                    Mp = _engine.Player.Mp,
                    MaxMp = _engine.Player.MaxMp,
                    DamageTaken = dmg,
                    IsPoisoned = L1jPoisonAttackRules.IsPoisoned(_engine.Player)
                });
            }
            else if (target.IsRemote)
            {
                if (NetworkManager.Instance.IsHost)
                {
                    target.Dead = target.Hp <= 0.0;
                    NetworkManager.Instance.SendPlayerHpSync(new PlayerHpSyncPacket
                    {
                        PlayerId = target.Key,
                        Hp = target.Hp,
                        MaxHp = target.MaxHp,
                        Mp = target.Mp,
                        MaxMp = target.MaxMp,
                        DamageTaken = dmg,
                        IsPoisoned = L1jPoisonAttackRules.IsPoisoned(target)
                    });
                }
            }
            else if (target.Kind == CombatantKind.Mob)
            {
                if (NetworkManager.Instance.IsHost)
                {
                    NetworkManager.Instance.SendMobHpSync(new MobHpSyncPacket
                    {
                        MobId = target.Key,
                        CurrentHp = target.Hp,
                        DamageTaken = dmg,
                        AttackerId = NetworkManager.Instance.LocalPlayerId
                    });
                }
                else if (source == _engine.Player)
                {
                    NetworkManager.Instance.SendMobHit(new MobHitPacket
                    {
                        MobId = target.Key,
                        Damage = dmg
                    });
                }
            }
        }
        catch { }
    }

    public void NoteCombatDeathEvent(Combatant target)
    {
        if (!NetworkManager.Instance.IsConnected || !NetworkManager.Instance.IsHost || target == null || target.Kind != CombatantKind.Mob) return;
        try
        {
            NetworkManager.Instance.SendMobDeath(new MobDeathPacket
            {
                MobId = target.Key,
                KillerId = NetworkManager.Instance.LocalPlayerId,
                Exp = target.LastExpShare,
                Gold = target.LastGoldShare
            });

            if (target.LastExpShare > 0 || target.LastGoldShare > 0)
            {
                foreach (var remote in _remotePlayerViews.Values)
                {
                    if (remote.Actor.IsAlive)
                    {
                        Vector2 remotePos = ToVec(remote.Actor.Pos);
                        if (target.LastExpShare > 0) FloatExp(remotePos, target.LastExpShare);
                        if (target.LastGoldShare > 0) FloatGold(remotePos, target.LastGoldShare);
                    }
                }
            }
        }
        catch { }
    }

    private void SendLocalHandshake(bool isResponse = false)
    {
        if (_engine?.Player == null) return;
        var p = _engine.Player;
        Vector2 pos = PlayerPos();
        string weaponId = string.IsNullOrEmpty(p.MainWeaponId) ? (_build?.WeaponPrefix ?? "sword1") : p.MainWeaponId;
        string avatar = string.IsNullOrEmpty(p.Avatar) ? (_build?.Avatar ?? "男騎士") : p.Avatar;
        string classId = string.IsNullOrEmpty(p.ClassId) ? (_build?.ClassId ?? "knight") : p.ClassId;
        string weaponPrefix = string.IsNullOrEmpty(p.WeaponPrefix) ? (_build?.WeaponPrefix ?? "sword1") : p.WeaponPrefix;

        NetworkManager.Instance.SendHandshake(new HandshakePacket
        {
            Name = p.Disp,
            ClassId = classId,
            Avatar = avatar,
            WeaponPrefix = weaponPrefix,
            MainWeaponId = weaponId,
            Level = p.Level,
            Hp = p.Hp,
            MaxHp = p.MaxHp,
            Mp = p.Mp,
            MaxMp = p.MaxMp,
            IsHost = NetworkManager.Instance.IsHost,
            X = pos.X,
            Y = pos.Y,
            Facing8 = p.Facing8,
            MapKey = _mapKey,
            IsResponse = isResponse,
            PolymorphForm = p.PolymorphForm ?? "",
            IsPoisoned = L1jPoisonAttackRules.IsPoisoned(p)
        });
    }

    private void SendLocalAction(string actionType, string skillId = "", double targetX = 0, double targetY = 0, string morph = "", string playerId = "")
    {
        if (!NetworkManager.Instance.IsConnected) return;
        NetworkManager.Instance.SendAction(new ActionPacket
        {
            PlayerId = !string.IsNullOrEmpty(playerId) ? playerId : NetworkManager.Instance.LocalPlayerId,
            ActionType = actionType,
            SkillId = skillId,
            MorphName = morph,
            TargetX = targetX,
            TargetY = targetY
        });
    }

    private void HandleRemotePlayerJoined(HandshakePacket handshake)
    {
        if (string.IsNullOrEmpty(handshake.PlayerId)) return;

        if (handshake.IsHost)
        {
            _hostPlayerId = handshake.PlayerId;
        }

        if (_remotePlayerViews.TryGetValue(handshake.PlayerId, out var existing))
        {
            try { _engine?.Engine?.Remove(existing.Actor); } catch { }
            try { existing.View.Free(); } catch { }
            _remotePlayerViews.Remove(handshake.PlayerId);
        }

        string avatar = string.IsNullOrEmpty(handshake.Avatar) ? "男騎士" : handshake.Avatar;
        string weapon = string.IsNullOrEmpty(handshake.WeaponPrefix) ? "sword1" : handshake.WeaponPrefix;
        string mainWeapon = string.IsNullOrEmpty(handshake.MainWeaponId) ? weapon : handshake.MainWeaponId;

        var actor = new Combatant
        {
            Kind = CombatantKind.Player,
            IsRemote = true,
            Key = handshake.PlayerId,
            Disp = handshake.Name,
            Level = handshake.Level,
            MaxHp = handshake.MaxHp,
            Hp = handshake.Hp,
            MaxMp = handshake.MaxMp > 0 ? handshake.MaxMp : 100,
            Mp = handshake.Mp > 0 ? handshake.Mp : 100,
            ClassId = handshake.ClassId,
            Avatar = avatar,
            WeaponPrefix = weapon,
            MainWeaponId = mainWeapon,
            Pos = new WorldPoint(handshake.X, handshake.Y),
            Facing8 = handshake.Facing8
        };

        if (!string.IsNullOrEmpty(handshake.PolymorphForm))
        {
            actor.Buffs["poly"] = 1800.0;
            actor.PolymorphForm = handshake.PolymorphForm;
        }

        bool onSameMap = string.IsNullOrEmpty(handshake.MapKey) || string.Equals(handshake.MapKey, _mapKey, StringComparison.Ordinal);

        try
        {
            if (onSameMap)
            {
                _engine?.Engine?.Add(actor);
            }

            ArpgActor view = CreateView(actor);
            view.IsRemote = true;
            view.SetVisible(onSameMap);
            view.SetNameWithoutLevel(actor.Disp, actor.Level);
            view.SetTitle(actor.Title);
            view.SetNameColor(Color.FromHtml("#66d9ef"));
            view.Hp = actor.Hp;
            view.MaxHp = actor.MaxHp;
            view.Mp = actor.Mp;
            view.MaxMp = actor.MaxMp;
            view.Pos = ToVec(actor.Pos);
            view.FaceDirection(handshake.Facing8);

            var (desired, fallback) = CharacterWeaponAnimation.Resolve(actor, GameDataProvider.Shared);
            view.SetWeaponPrefix(desired, fallback);
            if (handshake.IsPoisoned)
            {
                actor.Statuses["poison"] = 100;
            }
            view.SetAbnormalVisual(L1jAbnormalStateRules.Resolve(actor));
            view.Sync(1.0, 0f);

            _remotePlayerViews[handshake.PlayerId] = new RemotePlayerState
            {
                Actor = actor,
                View = view,
                TargetPos = ToVec(actor.Pos),
                VisualPos = ToVec(actor.Pos),
                IsMoving = false,
                WalkProgress = 0f,
                MoveHoldTimer = 0.0
            };
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[Multiplayer] Create view warning: {ex.Message}");
        }

        if (onSameMap)
        {
            SlabLog($"[color=#86efac]⚔️【隊友連線】{handshake.Name}（{handshake.ClassId} Lv.{handshake.Level}）已加入同地圖！[/color]");
        }

        // Mutual handshake: if this handshake is not already a response, send ours back immediately!
        if (!handshake.IsResponse)
        {
            SendLocalHandshake(isResponse: true);
        }

        if (NetworkManager.Instance.IsHost && onSameMap)
        {
            foreach (Combatant c in _engine.Combatants)
            {
                if (c.Kind == CombatantKind.Mob && !c.Dead)
                {
                    string mKey = string.IsNullOrEmpty(c.Avatar) ? "goblin" : c.Avatar;
                    NetworkManager.Instance.SendMobSpawn(new MobSpawnPacket
                    {
                        MobId = c.Key,
                        MobKey = mKey,
                        X = c.Pos.X,
                        Y = c.Pos.Y,
                        Hp = c.Hp,
                        MaxHp = c.MaxHp,
                        Facing8 = c.Facing8,
                        MapKey = _mapKey
                    });
                }
            }
        }

        if (onSameMap)
        {
            SyncGroundDropsToPeers();
        }

        RefreshPartyHud();
    }

    private void HandleRemotePlayerEquipped(EquipPacket equip)
    {
        if (_remotePlayerViews.TryGetValue(equip.PlayerId, out var state))
        {
            state.Actor.MainWeaponId = equip.MainWeaponId;
            var (desired, fallback) = CharacterWeaponAnimation.Resolve(state.Actor, GameDataProvider.Shared);
            state.View.SetWeaponPrefix(desired, fallback);
            state.View.Sync(1.0, 0f);
        }
    }

    private void HandlePlayerHpSynced(PlayerHpSyncPacket hpSync)
    {
        if (hpSync.PlayerId == NetworkManager.Instance.LocalPlayerId || string.IsNullOrEmpty(hpSync.PlayerId))
        {
            if (_engine?.Player != null)
            {
                _engine.Player.Hp = Math.Max(0.0, hpSync.Hp);
                if (hpSync.MaxHp > 0) _engine.Player.MaxHp = hpSync.MaxHp;
                if (hpSync.Mp > 0 || hpSync.MaxMp > 0)
                {
                    _engine.Player.Mp = Math.Max(0.0, hpSync.Mp);
                    if (hpSync.MaxMp > 0) _engine.Player.MaxMp = hpSync.MaxMp;
                }
                if (hpSync.DamageTaken > 0)
                {
                    Float(PlayerPos(), $"{(int)hpSync.DamageTaken}", Color.FromHtml("#ef4444"), big: false);
                    _playerView?.PlayOneShot("hurt");
                    GameAudio.Instance?.PlayPartyHurt(_engine.Player);
                    RefreshHud();
                    SlabLog($"[color=#ff7b72]受到傷害 {(int)hpSync.DamageTaken}！[/color]");
                }
                else if (hpSync.DamageTaken < 0)
                {
                    Float(PlayerPos(), $"+{(int)(-hpSync.DamageTaken)}", Color.FromHtml("#4ade80"), big: false);
                    _playerView?.PlayOneShot("skill");
                    GameAudio.Instance?.PlayEvent("heal");
                    RefreshHud();
                }
                if (_engine.Player.Hp <= 0.0)
                {
                    _engine.Player.Hp = 0; _engine.Player.Dead = true;
                }
            }
            RefreshPartyHud();
            return;
        }

        if (_remotePlayerViews.TryGetValue(hpSync.PlayerId, out var state))
        {
            state.Actor.Hp = hpSync.Hp;
            state.Actor.MaxHp = hpSync.MaxHp;
            if (hpSync.Mp > 0 || hpSync.MaxMp > 0)
            {
                state.Actor.Mp = hpSync.Mp;
                if (hpSync.MaxMp > 0) state.Actor.MaxMp = hpSync.MaxMp;
            }
            state.Actor.Dead = hpSync.Hp <= 0;
            state.View.Hp = hpSync.Hp;
            state.View.MaxHp = hpSync.MaxHp;
            state.View.Mp = hpSync.Mp;
            state.View.MaxMp = hpSync.MaxMp;
            if (!state.Actor.Dead && state.View.Dead)
            {
                state.View.Revive();
            }
            if (hpSync.IsPoisoned)
            {
                state.Actor.Statuses["poison"] = 100;
            }
            else
            {
                state.Actor.Statuses.Remove("poison");
                state.Actor.Statuses.Remove("poisonsilence");
                state.Actor.Statuses.Remove("poisonparalyzing");
                state.Actor.Statuses.Remove("poisonparalyzed");
            }
            state.View.SetAbnormalVisual(L1jAbnormalStateRules.Resolve(state.Actor));
            state.View.Sync(1.0, 0.016f);

            if (hpSync.DamageTaken > 0)
            {
                Float(ToVec(state.Actor.Pos), $"{(int)hpSync.DamageTaken}", Color.FromHtml("#ef4444"), big: false);
                state.View.PlayOneShot("hurt");
            }
            else if (hpSync.DamageTaken < 0)
            {
                Float(ToVec(state.Actor.Pos), $"+{(int)(-hpSync.DamageTaken)}", Color.FromHtml("#4ade80"), big: false);
                state.View.PlayOneShot("skill");
                GameAudio.Instance?.PlayEvent("heal");
            }
            RefreshPartyHud();
        }
    }

    private void HandleRemotePlayerMoved(MovePacket move)
    {
        if (!_remotePlayerViews.TryGetValue(move.PlayerId, out var state))
        {
            if (NetworkManager.Instance.ConnectedPlayers.TryGetValue(move.PlayerId, out var cached))
            {
                HandleRemotePlayerJoined(cached);
                _remotePlayerViews.TryGetValue(move.PlayerId, out state);
            }
            else
            {
                SendLocalHandshake(isResponse: false);
                return;
            }
        }
        if (state == null) return;

        bool onSameMap = string.IsNullOrEmpty(move.MapKey) || string.Equals(move.MapKey, _mapKey, StringComparison.Ordinal);
        state.View.SetVisible(onSameMap);
        if (!onSameMap)
        {
            state.Actor.Hp = move.Hp;
            state.Actor.MaxHp = move.MaxHp;
            state.Actor.Mp = move.Mp;
            state.Actor.MaxMp = move.MaxMp;
            state.Actor.Dead = move.Hp <= 0;
            RefreshPartyHud();
            return;
        }

        if (!_engine.Combatants.Contains(state.Actor))
        {
            try { _engine?.Engine?.Add(state.Actor); } catch { }
        }

        Vector2 netPos = new Vector2((float)move.X, (float)move.Y);
        if (state.VisualPos == Vector2.Zero || state.VisualPos.DistanceTo(netPos) > 350f)
        {
            state.VisualPos = netPos;
        }
        state.TargetPos = netPos;
        state.Actor.Facing8 = move.Facing8;
        state.Actor.Hp = move.Hp;
        state.Actor.MaxHp = move.MaxHp;
        state.Actor.Mp = move.Mp;
        state.Actor.MaxMp = move.MaxMp;
        state.Actor.Dead = move.Hp <= 0;
        state.View.Hp = move.Hp;
        state.View.MaxHp = move.MaxHp;
        if (!state.Actor.Dead && state.View.Dead)
        {
            state.View.Revive();
        }
        state.View.FaceDirection(move.Facing8);
        state.IsMoving = move.Stepping;
        state.MoveHoldTimer = move.Stepping ? 0.5 : 0.15;

        if (!string.Equals(state.Actor.PolymorphForm ?? "", move.PolymorphForm ?? "", StringComparison.Ordinal))
        {
            SetRemotePlayerMorph(state, move.PolymorphForm ?? "");
        }

        if (move.IsPoisoned)
        {
            state.Actor.Statuses["poison"] = 100;
        }
        else
        {
            state.Actor.Statuses.Remove("poison");
            state.Actor.Statuses.Remove("poisonsilence");
            state.Actor.Statuses.Remove("poisonparalyzing");
            state.Actor.Statuses.Remove("poisonparalyzed");
        }
        state.View.SetAbnormalVisual(L1jAbnormalStateRules.Resolve(state.Actor));

        RefreshPartyHud();
    }

    private void HandleRemotePlayerAction(ActionPacket action)
    {
        if (action.ActionType == "mob_attack")
        {
            Combatant? mob = _engine.Combatants.FirstOrDefault(c => c.Key == action.SkillId);
            if (mob != null && _views.TryGetValue(mob, out ArpgActor? mobView))
            {
                Combatant? tgt = !string.IsNullOrEmpty(action.PlayerId) ? _engine.Combatants.FirstOrDefault(c => c.Key == action.PlayerId) : null;
                if (tgt != null)
                {
                    mobView.Face((float)(tgt.Pos.X - mob.Pos.X), (float)(tgt.Pos.Y - mob.Pos.Y));
                }
                bool rangedAttacker = mob.AttackRange > 48.0;
                bool rangedShot = tgt != null && CombatRangeRules.DiamondDistance(mob.Pos, tgt.Pos) > 48.0;
                mobView.PlayAttack(_rng, rangedAttacker: rangedAttacker, rangedShot: rangedShot, cycleSeconds: 0.6, speedRatio: 1.0);
                PlayAttackSfx(mob);
            }
            return;
        }

        if (action.ActionType == "mob_cast")
        {
            Combatant? mob = _engine.Combatants.FirstOrDefault(c => c.Key == action.MorphName);
            Combatant? tgt = !string.IsNullOrEmpty(action.PlayerId) ? _engine.Combatants.FirstOrDefault(c => c.Key == action.PlayerId) : null;
            if (tgt == null && (action.TargetX != 0 || action.TargetY != 0))
            {
                WorldPoint pt = new WorldPoint(action.TargetX, action.TargetY);
                tgt = _engine.Combatants.OrderBy(c => c.Pos.DistanceSquaredTo(pt)).FirstOrDefault();
            }
            if (mob != null && _views.TryGetValue(mob, out ArpgActor? mobView))
            {
                mobView.RevealCombatName();
                if (tgt != null)
                {
                    mobView.Face((float)(tgt.Pos.X - mob.Pos.X), (float)(tgt.Pos.Y - mob.Pos.Y));
                }
                PlayCastAnim(mobView, mob, tgt ?? _engine.Player, action.SkillId, playBodyAnimation: true);
            }
            return;
        }

        if (action.ActionType == "morph")
        {
            if (_remotePlayerViews.TryGetValue(action.PlayerId, out var state))
            {
                SetRemotePlayerMorph(state, action.SkillId ?? action.MorphName ?? "");
            }
            return;
        }

        if (_remotePlayerViews.TryGetValue(action.PlayerId, out var state2))
        {
            if (action.ActionType == "attack")
            {
                state2.View.PlayAttack(_rng, rangedAttacker: false, rangedShot: false, cycleSeconds: 0.6, speedRatio: 1.0);
            }
            else if (action.ActionType == "cast")
            {
                PlayCastAnim(state2.View, state2.Actor, null, action.SkillId, true);
            }
            else if (action.ActionType == "revive")
            {
                state2.Actor.Dead = false;
                state2.View.Revive();
                GameAudio.Instance?.PlaySkillCast("sk_heal_mid");
                RefreshPartyHud();
            }
        }
    }

    private void SetRemotePlayerMorph(RemotePlayerState state, string morphName)
    {
        if (!string.IsNullOrEmpty(morphName))
        {
            state.Actor.Buffs["poly"] = 1800.0;
            state.Actor.PolymorphForm = morphName;
        }
        else
        {
            state.Actor.Buffs.Remove("poly");
            state.Actor.PolymorphForm = "";
        }

        string visualKey = CharacterVisualKey(state.Actor);
        if (string.Equals(state.View.VisualKey, visualKey, StringComparison.Ordinal))
        {
            return;
        }

        Vector2 currentPos = state.VisualPos;
        int facing = state.Actor.Facing8;
        bool isMoving = state.IsMoving;

        try { state.View.Free(); } catch { }

        ArpgActor newView = CreateView(state.Actor);
        newView.IsRemote = true;
        newView.SetVisible(state.Actor.IsAlive);
        newView.SetNameWithoutLevel(state.Actor.Disp, state.Actor.Level);
        newView.SetTitle(state.Actor.Title);
        newView.SetNameColor(Color.FromHtml("#66d9ef"));
        newView.Hp = state.Actor.Hp;
        newView.MaxHp = state.Actor.MaxHp;
        newView.Mp = state.Actor.Mp;
        newView.MaxMp = state.Actor.MaxMp;
        newView.Pos = currentPos;
        newView.FaceDirection(facing);

        var (desired, fallback) = CharacterWeaponAnimation.Resolve(state.Actor, GameDataProvider.Shared);
        newView.SetWeaponPrefix(desired, fallback);
        newView.Sync(1.0, 0f);

        state.View = newView;
        UpdateView(state.View, state.Actor, (currentPos, state.WalkProgress, isMoving), 0.016f, false);
    }

    private void BroadcastLocalFollowers()
    {
        if (!NetworkManager.Instance.IsConnected || _engine?.Combatants == null) return;
        try
        {
            var myFollowers = _engine.Combatants
                .Where(c => !c.IsRemote && (c.Kind == CombatantKind.Pet || c.Kind == CombatantKind.Summon || MonsterCompanionRules.IsCompanion(c)) && !c.Dead)
                .Select(c =>
                {
                    Vector2 rPos = _engine.RenderPos(c);
                    return new FollowerInfo
                    {
                        Id = c.Key,
                        Name = c.Disp,
                        Avatar = c.Avatar,
                        Kind = c.Kind == CombatantKind.Pet ? "pet" : (c.Kind == CombatantKind.Summon ? "summon" : "companion"),
                        X = rPos.X,
                        Y = rPos.Y,
                        Facing8 = c.Facing8,
                        Hp = c.Hp,
                        MaxHp = c.MaxHp,
                        Stepping = _engine.Engine.IsStepping(c) || c.MoveTarget.HasValue
                    };
                }).ToList();

            NetworkManager.Instance.SendFollowerSync(new FollowerSyncPacket
            {
                OwnerId = NetworkManager.Instance.LocalPlayerId,
                MapKey = _mapKey,
                Followers = myFollowers
            });
        }
        catch { }
    }

    private void HandleRemoteFollowerSynced(FollowerSyncPacket sync)
    {
        if (sync.OwnerId == NetworkManager.Instance.LocalPlayerId) return;
        bool onSameMap = string.IsNullOrEmpty(sync.MapKey) || string.Equals(sync.MapKey, _mapKey, StringComparison.Ordinal);

        var currentFollowerIds = new HashSet<string>();
        if (onSameMap)
        {
            foreach (var fInfo in sync.Followers)
            {
                currentFollowerIds.Add(fInfo.Id);
                if (!_remoteFollowers.TryGetValue(fInfo.Id, out var fState))
                {
                    CombatantKind kind = fInfo.Kind == "pet" ? CombatantKind.Pet : (fInfo.Kind == "companion" ? CombatantKind.Ally : CombatantKind.Summon);
                    var actor = new Combatant
                    {
                        Key = fInfo.Id,
                        Disp = fInfo.Name,
                        Avatar = fInfo.Avatar,
                        Kind = kind,
                        Pos = new WorldPoint(fInfo.X, fInfo.Y),
                        Facing8 = fInfo.Facing8,
                        Hp = fInfo.Hp,
                        MaxHp = fInfo.MaxHp,
                        IsRemote = true,
                        Passive = true
                    };
                    if (fInfo.Kind == "companion")
                    {
                        actor.ClassId = "monster";
                    }

                    try { _engine?.Engine?.Add(actor); } catch { }

                    ArpgActor view = CreateView(actor);
                    view.IsRemote = true;
                    view.Pos = new Vector2((float)fInfo.X, (float)fInfo.Y);
                    view.FaceDirection(fInfo.Facing8);

                    fState = new RemoteFollowerState
                    {
                        Actor = actor,
                        View = view,
                        VisualPos = new Vector2((float)fInfo.X, (float)fInfo.Y),
                        TargetPos = new Vector2((float)fInfo.X, (float)fInfo.Y),
                        OwnerId = sync.OwnerId
                    };
                    _remoteFollowers[fInfo.Id] = fState;
                }

                Vector2 netPos = new Vector2((float)fInfo.X, (float)fInfo.Y);
                if (fState.VisualPos == Vector2.Zero || fState.VisualPos.DistanceTo(netPos) > 350f)
                {
                    fState.VisualPos = netPos;
                }
                fState.TargetPos = netPos;
                fState.Actor.Facing8 = fInfo.Facing8;
                fState.Actor.Hp = fInfo.Hp;
                fState.Actor.MaxHp = fInfo.MaxHp;
                fState.IsMoving = fInfo.Stepping;
                fState.View.FaceDirection(fInfo.Facing8);
            }
        }

        var toRemove = _remoteFollowers
            .Where(kvp => kvp.Value.OwnerId == sync.OwnerId && !currentFollowerIds.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            if (_remoteFollowers.Remove(id, out var fState))
            {
                try { _engine?.Engine?.Remove(fState.Actor); } catch { }
                try { fState.View.Free(); } catch { }
            }
        }
    }

    private void HandleRemotePlayerLeft(string playerId)
    {
        if (_remotePlayerViews.Remove(playerId, out var state))
        {
            try { _engine?.Engine?.Remove(state.Actor); } catch { }
            try { state.View.Free(); } catch { }
            SlabLog($"[color=#fca5a5]🚪【隊友離線】{state.Actor.Disp} 已退出遊戲。[/color]");
            RefreshPartyHud();
        }

        var followersToRemove = _remoteFollowers
            .Where(kvp => kvp.Value.OwnerId == playerId)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var id in followersToRemove)
        {
            if (_remoteFollowers.Remove(id, out var fState))
            {
                try { _engine?.Engine?.Remove(fState.Actor); } catch { }
                try { fState.View.Free(); } catch { }
            }
        }
    }

    private void RefreshPartyHud()
    {
        if (_partyHud == null || _engine?.Player == null) return;

        if (!NetworkManager.Instance.IsConnected)
        {
            _partyHud.Visible = true;
            var localList = new List<PartyHud.PartyMemberData>();

            string localId = string.IsNullOrEmpty(NetworkManager.Instance.LocalPlayerId) ? "local_player" : NetworkManager.Instance.LocalPlayerId;

            // Self is #1 (Leader with Crown)
            localList.Add(new PartyHud.PartyMemberData
            {
                PlayerId = localId,
                Name = _engine.Player.Disp,
                ClassId = _engine.Player.ClassId,
                Index = 1,
                IsLeader = true,
                Hp = _engine.Player.Hp,
                MaxHp = _engine.Player.MaxHp,
                Mp = _engine.Player.Mp,
                MaxMp = _engine.Player.MaxMp,
                IsOfflineCompanion = false
            });

            int cIdx = 2;
            foreach (var kvp in _offlineCompanionActors)
            {
                var ally = kvp.Value;
                int slot = 0;
                if (kvp.Key.StartsWith("offline_slot_") && int.TryParse(kvp.Key.Substring("offline_slot_".Length), out int s))
                {
                    slot = s;
                }

                localList.Add(new PartyHud.PartyMemberData
                {
                    PlayerId = kvp.Key,
                    Name = ally.Disp,
                    ClassId = ally.ClassId,
                    Index = cIdx++,
                    IsLeader = false,
                    Hp = ally.Hp,
                    MaxHp = ally.MaxHp,
                    Mp = ally.Mp,
                    MaxMp = ally.MaxMp,
                    IsOfflineCompanion = true,
                    Slot = slot
                });
            }

            _partyHud.FollowingPlayerId = _autoFollowPlayerId;
            _partyHud.UpdateMembers(localList, localId);
            return;
        }

        _partyHud.Visible = true;
        var list = new List<PartyHud.PartyMemberData>();

        if (NetworkManager.Instance.IsHost)
        {
            // Host is always Leader (#1, with Crown)
            list.Add(new PartyHud.PartyMemberData
            {
                PlayerId = NetworkManager.Instance.LocalPlayerId,
                Name = _engine.Player.Disp,
                ClassId = _engine.Player.ClassId,
                Index = 1,
                IsLeader = true,
                Hp = _engine.Player.Hp,
                MaxHp = _engine.Player.MaxHp,
                Mp = _engine.Player.Mp,
                MaxMp = _engine.Player.MaxMp
            });

            int idx = 2;
            foreach (var kvp in _remotePlayerViews)
            {
                var actor = kvp.Value.Actor;
                list.Add(new PartyHud.PartyMemberData
                {
                    PlayerId = kvp.Key,
                    Name = actor.Disp,
                    ClassId = actor.ClassId,
                    Index = idx++,
                    IsLeader = false,
                    Hp = actor.Hp,
                    MaxHp = actor.MaxHp,
                    Mp = actor.Mp,
                    MaxMp = actor.MaxMp
                });
            }
        }
        else
        {
            // Client view: Host is #1 (leader with Crown), Client is #2
            int idx = 1;
            string hostId = _hostPlayerId;
            RemotePlayerState? hostState = null;
            if (!string.IsNullOrEmpty(hostId) && _remotePlayerViews.TryGetValue(hostId, out var hs))
            {
                hostState = hs;
            }
            else
            {
                hostState = _remotePlayerViews.Values.FirstOrDefault(v => v.Actor.IsRemote);
            }

            if (hostState != null)
            {
                list.Add(new PartyHud.PartyMemberData
                {
                    PlayerId = hostState.Actor.Key,
                    Name = hostState.Actor.Disp,
                    ClassId = hostState.Actor.ClassId,
                    Index = idx++,
                    IsLeader = true,
                    Hp = hostState.Actor.Hp,
                    MaxHp = hostState.Actor.MaxHp,
                    Mp = hostState.Actor.Mp,
                    MaxMp = hostState.Actor.MaxMp
                });
            }
            else
            {
                // Placeholder if host handshake is still on the way
                list.Add(new PartyHud.PartyMemberData
                {
                    PlayerId = "host",
                    Name = "房主 (主機)",
                    ClassId = "knight",
                    Index = idx++,
                    IsLeader = true,
                    Hp = 100,
                    MaxHp = 100,
                    Mp = 100,
                    MaxMp = 100
                });
            }

            // Local client player is #2
            list.Add(new PartyHud.PartyMemberData
            {
                PlayerId = NetworkManager.Instance.LocalPlayerId,
                Name = _engine.Player.Disp,
                ClassId = _engine.Player.ClassId,
                Index = idx++,
                IsLeader = false,
                Hp = _engine.Player.Hp,
                MaxHp = _engine.Player.MaxHp,
                Mp = _engine.Player.Mp,
                MaxMp = _engine.Player.MaxMp
            });

            // Any additional peer clients in the room
            foreach (var kvp in _remotePlayerViews)
            {
                if (hostState != null && kvp.Value == hostState) continue;
                var actor = kvp.Value.Actor;
                list.Add(new PartyHud.PartyMemberData
                {
                    PlayerId = kvp.Key,
                    Name = actor.Disp,
                    ClassId = actor.ClassId,
                    Index = idx++,
                    IsLeader = false,
                    Hp = actor.Hp,
                    MaxHp = actor.MaxHp,
                    Mp = actor.Mp,
                    MaxMp = actor.MaxMp
                });
            }
        }

        _partyHud.FollowingPlayerId = _autoFollowPlayerId;
        _partyHud.UpdateMembers(list, NetworkManager.Instance.LocalPlayerId);
    }

    private void HandlePartyMemberTeleport(string targetPlayerId)
    {
        if (_engine?.Player == null) return;
        if (_dead)
        {
            SlabLog("[color=#e2938f]⚠️ 死亡狀態無法進行傳送！[/color]");
            return;
        }

        // Check if target is an offline companion in the scene
        if (_offlineCompanionActors.TryGetValue(targetPlayerId, out var oAlly) && oAlly != null)
        {
            _engine.StopPlayer();
            _wasdMoving = false;

            float offX = (float)(_rng.NextDouble() * 32.0 - 16.0);
            float offY = (float)(_rng.NextDouble() * 32.0 - 16.0);
            Vector2 dest = ToVec(oAlly.Pos) + new Vector2(offX, offY);
            if (_topology != null && _topology.TryLocalCellAtDisplayPixel(dest.X, dest.Y, out var cx, out var cy))
            {
                if (!_topology.IsLegalCell(cx, cy))
                {
                    dest = ToVec(oAlly.Pos);
                }
            }

            RelocatePlayerGroup(new WorldPoint(dest.X, dest.Y));
            _playerView?.PlayOneShot("skill");
            GameAudio.Instance?.PlayEvent("levelup");
            SlabLog($"[color=#c9a0ff]⚡【快速傳送】已瞬移至離線隊友 {oAlly.Disp} 身邊！[/color]");
            return;
        }

        // 1. Try to find remote player in room
        _remotePlayerViews.TryGetValue(targetPlayerId, out var rState);
        NetworkManager.Instance.ConnectedPlayers.TryGetValue(targetPlayerId, out var targetInfo);

        if (rState == null && targetInfo == null)
        {
            SlabLog("[color=#e2938f]⚠️ 找不到該隊友，無法傳送！[/color]");
            return;
        }

        string targetName = rState?.Actor?.Disp ?? targetInfo?.Name ?? "隊友";
        string targetMap = targetInfo?.MapKey ?? "";
        bool onSameMap = string.IsNullOrEmpty(targetMap) || string.Equals(targetMap, _mapKey, StringComparison.Ordinal);

        // Case A: On the same map
        if (onSameMap)
        {
            Vector2 targetPos = rState != null
                ? (rState.VisualPos != Vector2.Zero ? rState.VisualPos : ToVec(rState.Actor.Pos))
                : (targetInfo != null ? new Vector2((float)targetInfo.X, (float)targetInfo.Y) : Vector2.Zero);

            if (targetPos == Vector2.Zero)
            {
                SlabLog("[color=#e2938f]⚠️ 無法取得隊友坐標！[/color]");
                return;
            }

            _engine.StopPlayer();
            _wasdMoving = false;

            // Offset slightly so we don't directly overlap inside the player
            float offX = (float)(_rng.NextDouble() * 32.0 - 16.0);
            float offY = (float)(_rng.NextDouble() * 32.0 - 16.0);
            Vector2 dest = targetPos + new Vector2(offX, offY);
            if (_topology != null && _topology.TryLocalCellAtDisplayPixel(dest.X, dest.Y, out var cx, out var cy))
            {
                if (!_topology.IsLegalCell(cx, cy))
                {
                    dest = targetPos;
                }
            }

            RelocatePlayerGroup(new WorldPoint(dest.X, dest.Y));
            _playerView?.PlayOneShot("skill");
            GameAudio.Instance?.PlayEvent("levelup");
            SlabLog($"[color=#c9a0ff]⚡【快速傳送】已瞬移至隊友 {targetName} 身邊！[/color]");

            // Immediately broadcast new position so peers see us arrive
            Vector2 newPos = PlayerPos();
            _lastSentNetPos = newPos;
            NetworkManager.Instance.SendMove(new MovePacket
            {
                X = newPos.X,
                Y = newPos.Y,
                Facing8 = _engine.Player.Facing8,
                Stepping = false,
                Hp = _engine.Player.Hp,
                MaxHp = _engine.Player.MaxHp,
                Mp = _engine.Player.Mp,
                MaxMp = _engine.Player.MaxMp,
                MapKey = _mapKey,
                PolymorphForm = _engine.Player.PolymorphForm ?? "",
                IsPoisoned = L1jPoisonAttackRules.IsPoisoned(_engine.Player)
            });
        }
        else
        {
            // Case B: On another map -> Cross-map teleport!
            double targetX = targetInfo?.X ?? (rState != null ? rState.Actor.Pos.X : 0);
            double targetY = targetInfo?.Y ?? (rState != null ? rState.Actor.Pos.Y : 0);

            _engine.StopPlayer();
            _wasdMoving = false;

            _session.HuntMap = targetMap;
            _session.PendingMapEntryLandmark = null;
            if (targetX != 0 || targetY != 0)
            {
                _session.PendingHuntSpawn = (targetX, targetY);
            }
            _session.LastHuntMap = "";
            SaveManager.Save(_session);
            SlabLog($"[color=#c9a0ff]⚡【跨圖傳送】正在傳送至隊友 {targetName} 所在的地圖...[/color]");
            _pendingScreenTransition = ChangeHuntMap;
        }
    }

    private void HandlePartyMemberFollow(string targetPlayerId)
    {
        if (_engine?.Player == null) return;
        if (_dead)
        {
            SlabLog("[color=#e2938f]⚠️ 死亡狀態無法自動跟隨！[/color]");
            return;
        }

        if (_autoFollowPlayerId == targetPlayerId)
        {
            StopAutoFollow(userCancelled: true);
            return;
        }

        string name = "隊友";
        if (_offlineCompanionActors.TryGetValue(targetPlayerId, out var oAlly))
        {
            name = oAlly.Disp;
        }
        else if (_remotePlayerViews.TryGetValue(targetPlayerId, out var rState))
        {
            name = rState.Actor.Disp;
        }
        else if (NetworkManager.Instance.ConnectedPlayers.TryGetValue(targetPlayerId, out var pInfo))
        {
            name = pInfo.Name;
        }

        _autoFollowPlayerId = targetPlayerId;
        _autoFollowTimer = 0.0;
        RefreshPartyHud();
        SlabLog($"[color=#86efac]🐾 開始自動跟隨隊友 【{name}】！[/color]");
    }

    public void StopAutoFollow(bool userCancelled = false)
    {
        if (string.IsNullOrEmpty(_autoFollowPlayerId)) return;

        string name = "隊友";
        if (_offlineCompanionActors.TryGetValue(_autoFollowPlayerId, out var oAlly))
        {
            name = oAlly.Disp;
        }
        else if (_remotePlayerViews.TryGetValue(_autoFollowPlayerId, out var rState))
        {
            name = rState.Actor.Disp;
        }
        else if (NetworkManager.Instance.ConnectedPlayers.TryGetValue(_autoFollowPlayerId, out var pInfo))
        {
            name = pInfo.Name;
        }

        _autoFollowPlayerId = "";
        _engine.StopPlayer();
        RefreshPartyHud();

        if (userCancelled)
        {
            SlabLog($"[color=#ffd76a]🛑 已停止跟隨隊友 【{name}】。[/color]");
        }
    }

    private void HandlePartyOfflineClicked()
    {
        if (_engine?.Player == null) return;
        if (_dead)
        {
            SlabLog("[color=#e2938f]⚠️ 死亡狀態無法設定自動離線託管！[/color]");
            return;
        }

        int currentSlot = SaveManager.CurrentSlot;
        Vector2 pPos = PlayerPos();
        string autoCast = (_session?.AutoCast != null) ? string.Join(",", _session.AutoCast) : "";

        LocalOfflinePartyManager.RegisterOffline(currentSlot, _engine.Player, _mapKey, pPos.X, pPos.Y, autoCast);

        SlabLog("[color=#86efac]⚡【角色託管】當前角色已成功登錄為「自動離線在線掛機」！[/color]");
        SlabLog("[color=#93c5fd]💾 角色進度已保存，正在返回選角畫面（可繼續創角或選角，實現本地多開同台同行）...[/color]");

        QuitToCharacterSelect();
    }

    private void HandleDismissCompanionClicked(string targetPlayerId)
    {
        if (_offlineCompanionActors.TryGetValue(targetPlayerId, out var ally))
        {
            int slot = 0;
            if (targetPlayerId.StartsWith("offline_slot_") && int.TryParse(targetPlayerId.Substring("offline_slot_".Length), out int s))
            {
                slot = s;
            }

            if (slot > 0)
            {
                LocalOfflinePartyManager.SyncProgressBackToSlot(slot, ally);
                LocalOfflinePartyManager.Unregister(slot);
            }

            _engine?.Engine?.Remove(ally);
            _offlineCompanionActors.Remove(targetPlayerId);

            if (_autoFollowPlayerId == targetPlayerId)
            {
                StopAutoFollow(userCancelled: true);
            }

            RefreshPartyHud();
            SlabLog($"[color=#93c5fd]💤 離線託管隊友【{ally.Disp}】已收回下線並保存進度。[/color]");
        }
    }

    public void SyncAllOfflineCompanions()
    {
        foreach (var (key, ally) in _offlineCompanionActors)
        {
            if (key.StartsWith("offline_slot_") && int.TryParse(key.Substring("offline_slot_".Length), out int slot))
            {
                LocalOfflinePartyManager.SyncProgressBackToSlot(slot, ally);
            }
        }
    }

    private void DeployOfflineCompanions()
    {
        _offlineCompanionActors.Clear();
        var activeCompanions = LocalOfflinePartyManager.GetActiveCompanions(SaveManager.CurrentSlot);
        if (activeCompanions.Count == 0 || _engine?.Player == null) return;

        int idx = 0;
        foreach (var comp in activeCompanions)
        {
            try
            {
                string playerBlob = SaveManager.GetPlayerBlobForSlot(comp.Slot);
                if (string.IsNullOrWhiteSpace(playerBlob))
                {
                    playerBlob = comp.PlayerBlob;
                }
                Combatant ally = PlayerSave.RestoreAsAlly(GameDataProvider.Shared, playerBlob, restoreResources: true);
                string allyKey = $"offline_slot_{comp.Slot}";
                ally.Key = allyKey;
                ally.Disp = comp.Name;

                // Sync AutoCast settings to companion
                string autoCastRaw = SaveManager.GetAutoCastForSlot(comp.Slot);
                if (string.IsNullOrWhiteSpace(autoCastRaw))
                {
                    autoCastRaw = comp.AutoCast;
                }
                ally.HasCustomAutoCast = true;
                ally.AutoCastSkills.Clear();
                if (!string.IsNullOrWhiteSpace(autoCastRaw))
                {
                    foreach (string s in autoCastRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string trimmed = s.Trim();
                        if (!SkillExecutionRules.IsManualOnly(GameDataProvider.Shared, trimmed))
                        {
                            ally.AutoCastSkills.Add(trimmed);
                        }
                    }
                }

                // Place near player in circular spacing
                float angle = (float)(idx * Math.PI * 2.0 / Math.Max(1, activeCompanions.Count));
                float dist = 48f + (idx * 16f);
                float targetX = (float)_engine.Player.Pos.X + Mathf.Cos(angle) * dist;
                float targetY = (float)_engine.Player.Pos.Y + Mathf.Sin(angle) * dist;

                if (_topology != null && _topology.TryLocalCellAtDisplayPixel(targetX, targetY, out int cx, out int cy) && _topology.IsWalkableCell(cx, cy))
                {
                    ally.Pos = new WorldPoint(targetX, targetY);
                }
                else
                {
                    ally.Pos = new WorldPoint(_engine.Player.Pos.X + 24.0, _engine.Player.Pos.Y + 24.0);
                }

                ally.Facing8 = _engine.Player.Facing8;
                _engine.Engine.Add(ally);
                _offlineCompanionActors[allyKey] = ally;

                idx++;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[DeployOfflineCompanions] 部署第 {comp.Slot} 槽離線隊友失敗: {ex.Message}");
            }
        }

        if (_offlineCompanionActors.Count > 0)
        {
            SlabLog($"[color=#86efac]👥【本地多開】成功加載 {_offlineCompanionActors.Count} 位離線在線託管隊友同行戰鬥！[/color]");
        }
    }

    public void OnPlayerRevived()
    {
        if (!NetworkManager.Instance.IsConnected || _engine?.Player == null) return;

        var player = _engine.Player;
        _lastSentHp = player.Hp;
        _lastSentMp = player.Mp;

        // Send HP sync packet immediately so teammates see alive HP bar
        NetworkManager.Instance.SendPlayerHpSync(new PlayerHpSyncPacket
        {
            PlayerId = NetworkManager.Instance.LocalPlayerId,
            Hp = player.Hp,
            MaxHp = player.MaxHp,
            Mp = player.Mp,
            MaxMp = player.MaxMp,
            IsPoisoned = L1jPoisonAttackRules.IsPoisoned(player)
        });

        // Send Move packet with new alive status and position
        Vector2 newPos = _engine.RenderPos(player);
        _lastSentNetPos = newPos;
        NetworkManager.Instance.SendMove(new MovePacket
        {
            X = newPos.X,
            Y = newPos.Y,
            Facing8 = player.Facing8,
            Stepping = false,
            Hp = player.Hp,
            MaxHp = player.MaxHp,
            Mp = player.Mp,
            MaxMp = player.MaxMp,
            MapKey = _mapKey,
            PolymorphForm = player.PolymorphForm ?? "",
            IsPoisoned = L1jPoisonAttackRules.IsPoisoned(player)
        });

        // Broadcast revive action effect so peers see resurrection and play revive animation!
        NetworkManager.Instance.SendAction(new ActionPacket
        {
            ActionType = "revive",
            PlayerId = NetworkManager.Instance.LocalPlayerId
        });

        RefreshPartyHud();
    }

    private void HandleRemoteChatReceived(ChatPacket chat)
    {
        if (chat == null || string.IsNullOrWhiteSpace(chat.Message)) return;
        if (chat.SenderId == NetworkManager.Instance.LocalPlayerId) return;

        string col = string.IsNullOrEmpty(chat.ColorHex) ? "#ffd76a" : chat.ColorHex;
        SlabLog($"[color=#66d9ef][隊伍][/color] [color={col}]{chat.SenderName}:[/color] {chat.Message}");

        if (_remotePlayerViews.TryGetValue(chat.SenderId, out var remote))
        {
            ShowRemoteChatBubble(remote, chat.SenderName, chat.Message);
        }
    }

    private void ShowRemoteChatBubble(RemotePlayerState remote, string speaker, string message)
    {
        if (remote.ChatBubble == null || !IsInstanceValid(remote.ChatBubble))
        {
            remote.ChatBubble = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0f, 24f),
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 2600
            };
            var style = new StyleBoxFlat
            {
                BgColor = Color.FromHtml("#161b22f0"),
                BorderColor = Color.FromHtml("#66d9ef"),
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
            remote.ChatBubble.AddThemeStyleboxOverride("panel", style);

            remote.ChatBubbleLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
                MouseFilter = MouseFilterEnum.Ignore
            };
            remote.ChatBubbleLabel.AddThemeFontSizeOverride("font_size", 12);
            remote.ChatBubbleLabel.AddThemeColorOverride("font_color", Colors.White);
            remote.ChatBubbleLabel.AddThemeColorOverride("font_outline_color", Color.FromHtml("#12100c"));
            remote.ChatBubbleLabel.AddThemeConstantOverride("outline_size", 2);
            remote.ChatBubble.AddChild(remote.ChatBubbleLabel);

            _ui.AddChild(remote.ChatBubble, forceReadableName: false, InternalMode.Disabled);
        }

        string display = string.IsNullOrEmpty(speaker) ? message : $"【{speaker}】{message}";
        if (remote.ChatBubbleLabel != null)
        {
            remote.ChatBubbleLabel.Text = display;
        }

        remote.ChatBubble.ResetSize();
        remote.ChatBubbleTimer = 3.5f;
        remote.ChatBubble.Modulate = new Color(1f, 1f, 1f, 1f);
        remote.ChatBubble.Visible = true;
        Vector2 rPos = remote.VisualPos != Vector2.Zero ? remote.VisualPos : ToVec(remote.Actor.Pos);
        remote.ChatBubble.Position = rPos + new Vector2(-remote.ChatBubble.Size.X * 0.5f, -85f);
    }

    // --- Monster Synchronization Handlers ---

    private void HandleMobSpawned(MobSpawnPacket spawn)
    {
        if (NetworkManager.Instance.IsHost) return;
        if (spawn.MapKey != _mapKey) return;

        Combatant? existing = _engine.Combatants.FirstOrDefault(c => c.Key == spawn.MobId);
        if (existing == null)
        {
            try
            {
                Combatant mob = _engine.SpawnMob(spawn.MobKey, new WorldPoint(spawn.X, spawn.Y));
                mob.Key = spawn.MobId;
                mob.Hp = spawn.Hp;
                mob.MaxHp = spawn.MaxHp;
                mob.Facing8 = spawn.Facing8;
                _clientMobTargets[spawn.MobId] = new ClientMobState
                {
                    TargetPos = new Vector2((float)spawn.X, (float)spawn.Y),
                    Facing8 = spawn.Facing8,
                    Stepping = false
                };
            }
            catch { }
        }
        else
        {
            existing.Hp = spawn.Hp;
            existing.MaxHp = spawn.MaxHp;
            existing.Pos = new WorldPoint(spawn.X, spawn.Y);
            existing.Facing8 = spawn.Facing8;
            if (!_clientMobTargets.TryGetValue(spawn.MobId, out var state))
            {
                state = new ClientMobState();
                _clientMobTargets[spawn.MobId] = state;
            }
            state.TargetPos = new Vector2((float)spawn.X, (float)spawn.Y);
            state.Facing8 = spawn.Facing8;
            state.Stepping = false;
        }
    }

    private void HandleMobBatchMoved(MobBatchMovePacket batch)
    {
        if (NetworkManager.Instance.IsHost) return;

        foreach (var entry in batch.Moves)
        {
            Vector2 netPos = new Vector2((float)entry.X, (float)entry.Y);
            if (!_clientMobTargets.TryGetValue(entry.MobId, out var state))
            {
                state = new ClientMobState
                {
                    VisualPos = netPos,
                    TargetPos = netPos
                };
                _clientMobTargets[entry.MobId] = state;
            }
            if (state.VisualPos == Vector2.Zero || state.VisualPos.DistanceTo(netPos) > 300f)
            {
                state.VisualPos = netPos;
            }
            state.TargetPos = netPos;
            state.Facing8 = entry.Facing8;
            state.Stepping = entry.Stepping;
            if (entry.Stepping)
            {
                state.MoveHoldTimer = 0.35;
            }
        }
    }

    private void HandleMobHitReceived(MobHitPacket hit)
    {
        if (!NetworkManager.Instance.IsHost) return;

        Combatant? mob = _engine.Combatants.FirstOrDefault(c => c.Key == hit.MobId);
        Combatant? attacker = _engine.Combatants.FirstOrDefault(c => c.Key == hit.AttackerId);

        if (mob != null && !mob.Dead)
        {
            mob.Hp = Math.Max(0.0, mob.Hp - hit.Damage);
            if (attacker != null)
            {
                _engine.Engine.AddHateExternal(mob, attacker, hit.Damage);
            }

            FloatDamage(ToVec(mob.Pos), (int)hit.Damage, false);

            NetworkManager.Instance.SendMobHpSync(new MobHpSyncPacket
            {
                MobId = mob.Key,
                CurrentHp = mob.Hp,
                DamageTaken = hit.Damage,
                AttackerId = hit.AttackerId
            });

            if (mob.Hp <= 0.0)
            {
                _clientMobTargets.Remove(mob.Key);
                _engine.Engine.KillMobExternal(mob, attacker);
            }
        }
    }

    private void HandleMobHpSynced(MobHpSyncPacket hpSync)
    {
        Combatant? mob = _engine.Combatants.FirstOrDefault(c => c.Key == hpSync.MobId);
        if (mob != null)
        {
            mob.Hp = hpSync.CurrentHp;
            FloatDamage(ToVec(mob.Pos), (int)hpSync.DamageTaken, false);
        }
    }

    private void HandleMobDied(MobDeathPacket death)
    {
        _clientMobTargets.Remove(death.MobId);
        Combatant? mob = _engine.Combatants.FirstOrDefault(c => c.Key == death.MobId);
        if (mob != null && !mob.Dead)
        {
            mob.Hp = 0;
            mob.Dead = true;
        }

        // Apply equally divided party rewards to client!
        if (_engine?.Player != null)
        {
            if (death.Exp > 0)
            {
                int oldLevel = _engine.Player.Level;
                int levelsGained = ProgressionRules.ApplyExperience(_engine.Player, death.Exp, GameDataProvider.Shared);
                if (levelsGained > 0)
                {
                    CombatantBuilder.RefreshPlayer(_engine.Player, GameDataProvider.Shared);
                    Float(PlayerPos(), "LEVEL UP!", Color.FromHtml("#ffd76a"), big: true);
                    _playerView?.PlayOneShot("skill");
                    GameAudio.Instance?.PlayEvent("levelup");
                    SlabLog($"[color=#ffd76a]等級提升！Lv {_engine.Player.Level}[/color]");
                }
                FloatExp(PlayerPos(), death.Exp);
                SlabLog($"[color=#4ade80]獲得 經驗值 +{(long)death.Exp:N0}[/color]");
            }
            if (death.Gold > 0)
            {
                CombatWallet.Add(_engine.Player, death.Gold);
                FloatGold(PlayerPos(), death.Gold);
                SlabLog($"[color=#e6c76a]獲得 金幣 ×{death.Gold:N0}[/color]");
                _bagRefresh?.Invoke();
            }

            // Immediately update HUD bars, exp gauge, level, and adena counter
            RefreshHud();
            try { SaveManager.Save(_session); } catch { }

            // Also float numbers over teammates/host if on screen
            if (death.Exp > 0 || death.Gold > 0)
            {
                foreach (var remote in _remotePlayerViews.Values)
                {
                    if (remote.Actor.IsAlive)
                    {
                        Vector2 remotePos = ToVec(remote.Actor.Pos);
                        if (death.Exp > 0) FloatExp(remotePos, death.Exp);
                        if (death.Gold > 0) FloatGold(remotePos, death.Gold);
                    }
                }
            }

            RefreshPartyHud();
        }
    }

    private void HandlePartyMemberClicked(string playerId)
    {
        if (_engine?.Player == null) return;

        Combatant? target = null;
        if (playerId == NetworkManager.Instance.LocalPlayerId)
        {
            target = _engine.Player;
        }
        else if (_remotePlayerViews.TryGetValue(playerId, out var remote))
        {
            target = remote.Actor;
        }
        else if (_offlineCompanionActors.TryGetValue(playerId, out var companion))
        {
            target = companion;
        }

        if (target == null) return;

        if (ManualSkillTargeting)
        {
            string skillId = _manualSkillTargetId;
            _manualSkillTargetId = "";
            _manualSkillScrollStack = null;
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            CastHealOnTeammate(target, skillId);
            return;
        }

        CastHealOnTeammate(target);
    }

    private string? GetBestAvailableHealSkill()
    {
        string[] candidates = { "sk_full_heal", "sk_heal2", "sk_heal_mid", "sk_heal1" };
        foreach (var skillId in candidates)
        {
            if (_engine.Player.GrantedSkills.Contains(skillId) && ClassKitRegistry.CanUseSkill(_engine.Player, skillId, GameDataProvider.Shared))
            {
                return skillId;
            }
        }
        foreach (var skillId in _engine.Player.GrantedSkills)
        {
            var data = GameDataProvider.Shared.Skill(skillId);
            if (data != null)
            {
                var l1j = L1jSkillFields.TryRead(data["l1j"] as JsonObject);
                if (l1j != null && (l1j.IsHeal || string.Equals(l1j.Category, "heal", StringComparison.Ordinal)))
                {
                    return skillId;
                }
            }
        }
        return null;
    }

    private double CalculateHealAmount(Combatant caster, Combatant target, string skillId)
    {
        double intStat = caster.Allocations.GetValueOrDefault("int", 0) + caster.LevelStatBonuses.GetValueOrDefault("int", 0) + caster.ElixirBonuses.GetValueOrDefault("int", 0) + 12;
        double magicBonus = Math.Max(0.0, (intStat - 12) * 2.5);

        switch (skillId)
        {
            case "sk_full_heal":
                return Math.Max(500.0, target.MaxHp - target.Hp);
            case "sk_heal2":
                return 220.0 + magicBonus * 3.0 + _rng.Next(1, 40);
            case "sk_heal_mid":
                return 80.0 + magicBonus * 2.0 + _rng.Next(1, 20);
            case "sk_heal1":
            default:
                return 30.0 + magicBonus * 1.2 + _rng.Next(1, 10);
        }
    }

    private void CastHealOnTeammate(Combatant target, string? skillId = null)
    {
        if (target == null || _engine?.Player == null) return;

        skillId ??= GetBestAvailableHealSkill();
        if (string.IsNullOrEmpty(skillId))
        {
            SlabLog("[color=#e2938f]尚未學習或裝備治癒術[/color]");
            return;
        }

        if (!ClassKitRegistry.CanUseSkill(_engine.Player, skillId, GameDataProvider.Shared))
        {
            SlabLog("[color=#e2938f]目前無法使用" + SkillInfo.Name(skillId) + "[/color]");
            return;
        }

        int mpCost = PlayerSkillMpCost(skillId);
        if (_engine.Player.Mp < (double)mpCost)
        {
            SlabLog($"[color=#e2938f]魔力不足（需要 {mpCost} MP）[/color]");
            return;
        }

        if (target == _engine.Player)
        {
            QueuePlayerManualSkill(skillId, target);
            return;
        }

        // Healing a remote teammate
        double oldHp = target.Hp;
        _engine.Player.Mp = Math.Max(0.0, _engine.Player.Mp - mpCost);
        double healAmount = CalculateHealAmount(_engine.Player, target, skillId);
        target.Hp = Math.Min(target.MaxHp, target.Hp + healAmount);
        double healed = target.Hp - oldHp;

        _playerView?.PlayOneShot("skill");
        GameAudio.Instance?.PlayEvent("heal");

        if (_views.TryGetValue(target, out var targetView))
        {
            targetView.PlayOneShot("skill");
        }

        Vector2 atPos = ToVec(target.Pos);
        Float(atPos, $"+{(int)healed}", Color.FromHtml("#4ade80"), big: false);
        SlabLog($"[color=#4ade80]對 {target.Disp} 施放 {SkillInfo.Name(skillId)}，回復 {(int)healed} HP！[/color]");

        NetworkManager.Instance.SendPlayerHpSync(new PlayerHpSyncPacket
        {
            PlayerId = target.Key,
            Hp = target.Hp,
            MaxHp = target.MaxHp,
            Mp = target.Mp,
            MaxMp = target.MaxMp,
            DamageTaken = -healed,
            IsPoisoned = L1jPoisonAttackRules.IsPoisoned(target)
        });

        SendLocalAction("cast_heal", target.Key, (int)target.Pos.X, (int)target.Pos.Y);
        RefreshHud();
        RefreshPartyHud();
    }

    private void SyncGroundDropsToPeers()
    {
        if (!NetworkManager.Instance.IsConnected || _groundDrops.Count == 0) return;
        foreach (var drop in _groundDrops)
        {
            if (string.IsNullOrEmpty(drop.DropId))
            {
                drop.DropId = Guid.NewGuid().ToString("N");
            }
            string affixesJson = (drop.Affixes != null && drop.Affixes.Count > 0) ? JsonSerializer.Serialize(drop.Affixes) : "";
            NetworkManager.Instance.SendItemDrop(new ItemDropPacket
            {
                DropId = drop.DropId,
                ItemKey = drop.ItemKey,
                Quantity = drop.Quantity,
                X = drop.Node.Position.X,
                Y = drop.Node.Position.Y,
                MapKey = _mapKey,
                Blessing = (int)drop.Blessing,
                Enhancement = drop.Enhancement,
                IsIdentified = drop.IsIdentified,
                ItemLevel = drop.ItemLevel,
                AffixesJson = affixesJson,
                DropperId = NetworkManager.Instance.LocalPlayerId,
                DropperName = _engine?.Player?.Disp ?? ""
            });
        }
    }

    private void HandleRemoteItemDropped(ItemDropPacket drop)
    {
        if (drop == null || string.IsNullOrWhiteSpace(drop.ItemKey)) return;
        if (!string.Equals(drop.MapKey, _mapKey, StringComparison.Ordinal)) return;

        if (drop.DropperId == NetworkManager.Instance.LocalPlayerId) return;
        if (_groundDrops.Any(g => g.DropId == drop.DropId)) return;

        var affixes = new List<EquipmentAffixRoll>();
        if (!string.IsNullOrEmpty(drop.AffixesJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<EquipmentAffixRoll>>(drop.AffixesJson);
                if (list != null) affixes = list;
            }
            catch { }
        }

        var stack = new ItemStack("remote-drop", drop.ItemKey, drop.Quantity)
        {
            Blessing = (ItemBlessing)drop.Blessing,
            Enhancement = drop.Enhancement,
            IsIdentified = drop.IsIdentified,
            ItemLevel = drop.ItemLevel,
            Affixes = affixes
        };

        var combatEvent = new CombatEvent(
            CombatEventKind.Drop,
            null,
            null,
            0.0,
            false,
            DamageType.Melee,
            null,
            null,
            null,
            null,
            drop.ItemKey,
            (int)Math.Min(int.MaxValue, drop.Quantity),
            (ItemBlessing)drop.Blessing,
            drop.Enhancement,
            drop.IsIdentified,
            drop.ItemLevel,
            affixes
        );

        Vector2 dropPos = new Vector2((float)drop.X, (float)drop.Y);
        SpawnGroundDrop(combatEvent, dropPos, stack, drop.DropId);

        string itemName = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, stack);
        string who = string.IsNullOrWhiteSpace(drop.DropperName) ? "隊友" : drop.DropperName;
        SlabLog($"[color=#86efac]📦 {who} 將「{itemName}」丟在地上[/color]");
    }

    private void HandleRemoteItemPickedUp(ItemPickupPacket pickup)
    {
        if (pickup == null || string.IsNullOrWhiteSpace(pickup.DropId)) return;
        if (pickup.PickerId == NetworkManager.Instance.LocalPlayerId) return;

        for (int i = _groundDrops.Count - 1; i >= 0; i--)
        {
            var drop = _groundDrops[i];
            if (drop.DropId == pickup.DropId)
            {
                if (pickup.Remaining <= 0)
                {
                    drop.Node.QueueFree();
                    _groundDrops.RemoveAt(i);
                }
                else
                {
                    drop.Quantity = (int)pickup.Remaining;
                    if (drop.CountLabel != null)
                    {
                        drop.CountLabel.Text = $"×{drop.Quantity}";
                    }
                }
                string who = string.IsNullOrWhiteSpace(pickup.PickerName) ? "隊友" : pickup.PickerName;
                string itemName = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, drop.ItemKey, drop.IsIdentified);
                SlabLog($"[color=#86efac]✋ {who} 撿起了「{itemName}」×{pickup.Quantity}[/color]");
                break;
            }
        }
    }
}
