using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// A dependency-free two-player LAN / online experiment.
///
/// The host remains authoritative for the ranch, waves, enemies, rewards,
/// and save file. The guest can move around, see the host and host enemies,
/// and make basic networked melee attacks against those enemies.
///
/// This is intentionally a learning prototype rather than production
/// matchmaking/netcode. Direct-online play needs a forwarded host port.
/// Relay play avoids port forwarding because both players connect outward to
/// the same public relay.
/// </summary>
[DefaultExecutionOrder(-850)]
[DisallowMultipleComponent]
public class RanchLanMultiplayer : MonoBehaviour
{
    public const int DefaultPort = 7777;
    public const int DefaultRelayPort = 7778;

    // Each player keeps an independent economy, so the guest persists to its own
    // save file rather than sharing (or overwriting) the host's.
    private const string HostSaveFileName = "RanchSimulatorSave.json";
    private const string GuestSaveFileName = "RanchSimulatorGuestSave.json";

    private enum TransportMode
    {
        Lan,
        DirectOnline,
        Relay
    }

    public bool IsConnected => connected;
    public string StatusText => statusText;
    public string LocalAddress => localAddress;
    public string SessionLabel
    {
        get
        {
            if (transportMode == TransportMode.Relay)
                return "ONLINE RELAY";

            return transportMode == TransportMode.DirectOnline
                ? "ONLINE DIRECT"
                : "LAN";
        }
    }

    public string HostAddressHelp =>
        transportMode == TransportMode.Relay
            ? "Share the relay address and room code."
            : transportMode == TransportMode.DirectOnline
            ? "Share your public IP or DNS name with port " + DefaultPort + "."
            : "Share this local address with another device on your LAN.";
    public int Port => DefaultPort;
    public int RelayPort => DefaultRelayPort;

    private const float PlayerSendInterval = 0.05f;
    private const float EnemySendInterval = 0.10f;
    private const float SummarySendInterval = 0.50f;
    private const float RemoteEnemyDamageRange = 2.15f;
    private const float RemoteEnemyDamageInterval = 0.82f;

    // Co-op revive: how close you must be to a downed teammate and how long you
    // must hold the interact key to bring them back up.
    private const float ReviveRange = 3.0f;
    private const float ReviveHoldSeconds = 1.4f;

    [Serializable]
    private class LanPacket
    {
        public string type;
        public int id;
        public int frame;
        public string name;
        public float x;
        public float y;
        public float z;
        public float rotationY;
        public float health;
        public float maxHealth;
        public float stamina;
        public float maxStamina;
        public int activeSlot;
        public int equippedWeapon;
        public int classType;
        public int combo;
        public int archetype;
        public bool boss;
        public bool heavy;
        public bool dead;
        public bool extracting;
        public float rawRanch;
        public float totalRanch;
        public float money;
        public int wave;
        public int enemyCount;
        public float deltaTime;
        public int stationType;
        public int areaIndex;
        public int bottleTier;
        public bool modifier;
        public int[] bottleCounts;
        public int unlockedBottleTier;
        public int selectedBottleTier;
        public int toolTier;
        public bool area0;
        public bool area1;
        public bool area2;
        public bool area3;
    }

    private sealed class RemoteEnemyVisual
    {
        public GameObject Root;
        public TextMesh Label;
        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public int LastFrame;
    }

    private readonly ConcurrentQueue<string> incoming =
        new ConcurrentQueue<string>();

    private readonly ConcurrentQueue<string> outgoing =
        new ConcurrentQueue<string>();

    private readonly Dictionary<int, RemoteEnemyVisual> remoteEnemies =
        new Dictionary<int, RemoteEnemyVisual>();

    // Unity 6.5 deprecates Object.GetInstanceID(). The host assigns its own
    // stable runtime IDs so each enemy keeps the same network identity
    // throughout the current LAN session.
    private readonly Dictionary<RanchEnemy, int> hostEnemyIds =
        new Dictionary<RanchEnemy, int>();

    private int nextHostEnemyId = 1;

    private readonly object connectionLock = new object();

    private static RanchLanMultiplayer activeInstance;

    private RanchGameCore core;
    private TcpListener listener;
    private TcpClient tcpClient;
    private StreamReader reader;
    private StreamWriter writer;
    private Thread listenerThread;
    private Thread receiveThread;
    private Thread sendThread;

    private volatile bool stopRequested;
    private volatile bool connected;
    private volatile string statusText = "LAN multiplayer inactive";
    private string localAddress = "Unknown";
    private TransportMode transportMode = TransportMode.Lan;

    private GameObject remotePlayer;
    private TextMesh remotePlayerLabel;
    private Transform remoteEquipmentAnchor;
    private Transform remoteExtractor;
    private Transform remoteSword;
    private Transform remoteSpear;
    private Transform remoteBow;
    private Transform remoteTrap;
    private Transform remoteWand;
    private Transform remoteAnimatedEquipment;
    private Vector3 remoteEquipmentBasePosition;
    private Quaternion remoteEquipmentBaseRotation = Quaternion.identity;
    private Vector3 remotePlayerTargetPosition;
    private Quaternion remotePlayerTargetRotation = Quaternion.identity;
    private float remotePlayerHealth;
    private float remotePlayerMaxHealth = 1f;
    private float remotePlayerStamina;
    private float remotePlayerMaxStamina = 1f;
    private int remoteActiveSlot = 1;
    private int remoteEquippedWeapon;
    private int remoteClassType;
    private bool remotePlayerDead;
    private bool remoteExtracting;
    private bool hasRemotePlayerState;
    private bool clientRestrictionsApplied;
    private GameObject remoteAttackArc;
    private float remoteAttackTimer;
    private float remoteAttackDuration;
    private bool remoteAttackHeavy;
    private float remoteEquipmentAnimationTimer;
    private float remoteEquipmentAnimationDuration;
    private bool remoteEquipmentAnimationHeavy;
    private int remoteEquipmentAnimationCombo = 1;

    private float nextPlayerSend;
    private float nextEnemySend;
    private float nextSummarySend;
    private float nextGuestAttack;
    private float reviveHoldTimer;
    private float nextRemoteEnemyDamage;
    private float lastHostAcceptedAttack;
    private int enemyFrame;

    private float hostRawRanch;
    private float hostTotalRanch;
    private float hostMoney;
    private int hostWave;
    private int hostEnemyCount;
    private bool relayPaired;
    private int packetsSent;
    private int packetsReceived;
    private int playerPacketsSent;
    private int playerPacketsReceived;
    private int guestAttacksSent;
    private int guestAttacksReceived;
    private int guestAttackHits;
    private int remoteDamageSent;
    private int remoteDamageReceived;
    private int revivesSent;
    private int revivesReceived;
    private string lastNetworkEvent = "No multiplayer traffic yet";

    private GUIStyle panelStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle warningStyle;
    private Texture2D panelTexture;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        localAddress = FindLocalIPv4Address();
        activeInstance = this;
    }

    // Point the guest at its own save file and load any existing personal
    // progression, so a joining player keeps their ranch across sessions without
    // ever touching the host's save.
    private void ConfigureGuestSaveSlot()
    {
        if (core == null || core.Save == null)
            return;

        core.Save.SaveFileName = GuestSaveFileName;

        if (core.Save.HasSaveFile)
            core.Save.LoadGame(false);
    }

    public static void NotifyLocalAttackAnimation(bool heavy, int combo)
    {
        if (activeInstance != null)
            activeInstance.SendLocalAttackAnimation(heavy, combo);
    }

    private void SendLocalAttackAnimation(bool heavy, int combo)
    {
        if (!connected ||
            !RanchGameModeState.IsLanHost ||
            core == null ||
            core.Equipment == null)
        {
            return;
        }

        QueuePacket(new LanPacket
        {
            type = "attack_anim",
            heavy = heavy,
            combo = combo,
            activeSlot = core.Equipment.ActiveSlot,
            equippedWeapon = (int)core.Equipment.EquippedWeapon,
            classType = core.Classes != null
                ? (int)core.Classes.CurrentClass
                : 0
        });
    }

    public bool StartHost()
    {
        return StartHost(false);
    }

    public bool StartHost(bool onlineDirect)
    {
        if (core == null)
            return false;

        ShutdownConnection(false);
        RanchGameModeState.SetMode(RanchGameMode.LanHost);
        if (core.Save != null)
            core.Save.SaveFileName = HostSaveFileName;
        transportMode = onlineDirect
            ? TransportMode.DirectOnline
            : TransportMode.Lan;

        stopRequested = false;
        connected = false;
        relayPaired = transportMode != TransportMode.Relay;
        statusText =
            (transportMode == TransportMode.DirectOnline
                ? "Direct-online host listening on port "
                : "Hosting on " + localAddress + ":") +
            DefaultPort +
            (transportMode == TransportMode.DirectOnline
                ? " — forward this port to this computer"
                : " — waiting for one guest");

        listenerThread = new Thread(HostAcceptLoop)
        {
            IsBackground = true,
            Name = "Ranch LAN Host Listener"
        };
        listenerThread.Start();
        return true;
    }

    public bool StartRelayHost(string relayAddress, string roomCode)
    {
        return StartRelaySession(relayAddress, roomCode, true);
    }

    public bool StartClient(string hostAddress)
    {
        return StartClient(hostAddress, false);
    }

    public bool StartClient(string hostAddress, bool onlineDirect)
    {
        if (core == null)
            return false;

        string cleaned = string.IsNullOrWhiteSpace(hostAddress)
            ? ""
            : hostAddress.Trim();

        if (cleaned.Length == 0)
        {
            statusText = onlineDirect
                ? "Enter the host's public IP, DNS name, or invite address."
                : "Enter the host laptop's IPv4 address.";
            return false;
        }

        if (!TryParseEndpoint(cleaned, out string address, out int port))
        {
            statusText = "Enter an address like 192.168.1.5 or play.example.com:7777.";
            return false;
        }

        ShutdownConnection(false);
        RanchGameModeState.SetMode(RanchGameMode.LanClient);
        ConfigureGuestSaveSlot();
        transportMode = onlineDirect
            ? TransportMode.DirectOnline
            : TransportMode.Lan;

        stopRequested = false;
        connected = false;
        relayPaired = transportMode != TransportMode.Relay;
        statusText =
            "Connecting to " + address + ":" + port + "...";

        if (core.Player != null)
        {
            Vector3 guestSpawn =
                core.Player.transform.position + Vector3.right * 3f;

            core.Player.Teleport(
                guestSpawn,
                core.Player.transform.eulerAngles.y
            );
        }

        Thread connectThread = new Thread(
            () => ClientConnectLoop(
                address,
                port,
                null,
                "Connected to " + SessionLabel + " host " +
                address + ":" + port
            )
        )
        {
            IsBackground = true,
            Name = "Ranch " + SessionLabel + " Client Connector"
        };
        connectThread.Start();
        return true;
    }

    public bool StartRelayClient(string relayAddress, string roomCode)
    {
        return StartRelaySession(relayAddress, roomCode, false);
    }

    private bool StartRelaySession(
        string relayAddress,
        string roomCode,
        bool host)
    {
        if (core == null)
            return false;

        string cleanedRoom = CleanRoomCode(roomCode);
        if (cleanedRoom.Length == 0)
        {
            statusText = "Enter a relay room code.";
            return false;
        }

        if (!TryParseEndpoint(relayAddress, out string address, out int port))
        {
            statusText = "Enter a relay address like relay.example.com:7778.";
            return false;
        }

        ShutdownConnection(false);
        RanchGameModeState.SetMode(
            host ? RanchGameMode.LanHost : RanchGameMode.LanClient
        );
        if (core.Save != null)
            core.Save.SaveFileName = host ? HostSaveFileName : GuestSaveFileName;
        if (!host)
            ConfigureGuestSaveSlot();
        transportMode = TransportMode.Relay;

        stopRequested = false;
        connected = false;
        relayPaired = false;
        statusText =
            "Connecting to relay " + address + ":" + port +
            " room " + cleanedRoom + "...";

        if (!host && core.Player != null)
        {
            Vector3 guestSpawn =
                core.Player.transform.position + Vector3.right * 3f;

            core.Player.Teleport(
                guestSpawn,
                core.Player.transform.eulerAngles.y
            );
        }

        string role = host ? "host" : "guest";
        string handshake = "__ranch_relay|" + role + "|" + cleanedRoom;
        string connectedText =
            (host
                ? "Relay host ready in room "
                : "Connected to relay room ") +
            cleanedRoom;

        Thread connectThread = new Thread(
            () => ClientConnectLoop(address, port, handshake, connectedText)
        )
        {
            IsBackground = true,
            Name = "Ranch " + SessionLabel + " Connector"
        };
        connectThread.Start();
        return true;
    }

    private void Update()
    {
        if (!RanchGameModeState.IsMultiplayer || core == null)
            return;

        if (RanchGameModeState.IsLanClient && !clientRestrictionsApplied)
            ApplyClientRestrictions();

        ProcessIncomingPackets();
        UpdateRemotePlayerVisual();
        UpdateRemoteEnemyVisuals();
        UpdateRemoteAttackAnimation();

        if (connected)
        {
            float now = Time.unscaledTime;

            if (now >= nextPlayerSend)
            {
                nextPlayerSend = now + PlayerSendInterval;
                SendLocalPlayerState();
            }

            if (RanchGameModeState.IsLanHost)
            {
                if (now >= nextEnemySend)
                {
                    nextEnemySend = now + EnemySendInterval;
                    SendEnemySnapshot();
                }

                if (now >= nextSummarySend)
                {
                    nextSummarySend = now + SummarySendInterval;
                    SendWorldSummary();
                }

                HandleRemoteGuestEnemyDamage(now);
            }
            else
            {
                // The guest plays its own local game for everything except combat
                // against the shared host enemies, which is still proxied so the
                // host applies authoritative damage to its real enemies.
                HandleGuestAttackInput(now);
            }

            // Either player can revive the other when they are downed.
            HandleReviveInput();
        }

        if (Input.GetKeyDown(KeyCode.F10))
            ReturnToTitleScreen();
    }

    private void ApplyClientRestrictions()
    {
        clientRestrictionsApplied = true;

        // The guest runs its OWN full economy locally (ranch, shop, upgrades,
        // classes, laboratory, knowledge, bottles, traps, extraction, and its own
        // save). Only the shared enemy threat is host-authoritative: the guest
        // sees host-spawned enemies as visuals and damages them through networked
        // attack requests, so those systems stay disabled here to avoid the guest
        // spawning a second, separate set of enemies.
        SetEnabled(core.Waves, false);
        SetEnabled(core.Bosses, false);
        SetEnabled(core.CJ, false);

        core.ShowMessage(
            "Joined as " + SessionLabel + " guest. You have your own ranch, shop, upgrades, and save. " +
            "You share the same enemies with the host — fight together, and revive each other when downed.",
            10f
        );
    }

    private static void SetEnabled(Behaviour behaviour, bool value)
    {
        if (behaviour != null)
            behaviour.enabled = value;
    }

    private void SendLocalPlayerState()
    {
        if (core.Player == null)
            return;

        Transform player = core.Player.transform;
        QueuePacket(new LanPacket
        {
            type = "player",
            x = player.position.x,
            y = player.position.y,
            z = player.position.z,
            rotationY = player.eulerAngles.y,
            health = core.Health != null ? core.Health.CurrentHealth : 0f,
            maxHealth = core.Health != null ? core.Health.MaxHealth : 1f,
            stamina = core.Stamina != null ? core.Stamina.CurrentStamina : 0f,
            maxStamina = core.Stamina != null ? core.Stamina.MaximumStamina : 1f,
            activeSlot = core.Equipment != null ? core.Equipment.ActiveSlot : 1,
            equippedWeapon = core.Equipment != null
                ? (int)core.Equipment.EquippedWeapon
                : 0,
            classType = core.Classes != null
                ? (int)core.Classes.CurrentClass
                : 0,
            dead = core.Health != null &&
                   (core.Health.IsDead || core.Health.IsDowned),
            extracting = core.Player != null && core.Player.IsExtracting
        });
    }

    private void SendEnemySnapshot()
    {
        if (core.Waves == null)
            return;

        enemyFrame++;
        List<RanchEnemy> active = core.Waves.ActiveEnemies;

        for (int i = 0; i < active.Count; i++)
        {
            RanchEnemy enemy = active[i];
            if (enemy == null || enemy.Health <= 0f)
                continue;

            Transform enemyTransform = enemy.transform;
            QueuePacket(new LanPacket
            {
                type = "enemy",
                id = GetOrCreateHostEnemyId(enemy),
                frame = enemyFrame,
                name = enemy.EnemyName,
                x = enemyTransform.position.x,
                y = enemyTransform.position.y,
                z = enemyTransform.position.z,
                rotationY = enemyTransform.eulerAngles.y,
                health = enemy.Health,
                maxHealth = enemy.MaxHealth,
                archetype = (int)enemy.Archetype,
                boss = enemy.IsBoss
            });
        }

        QueuePacket(new LanPacket
        {
            type = "enemy_end",
            frame = enemyFrame
        });
    }

    private int GetOrCreateHostEnemyId(RanchEnemy enemy)
    {
        if (enemy == null)
            return 0;

        if (hostEnemyIds.TryGetValue(enemy, out int existingId))
            return existingId;

        int assignedId = nextHostEnemyId++;
        hostEnemyIds.Add(enemy, assignedId);
        return assignedId;
    }

    private void SendWorldSummary()
    {
        bool[] areas = core.Areas != null
            ? core.Areas.GetUnlockStateCopy()
            : null;

        QueuePacket(new LanPacket
        {
            type = "summary",
            rawRanch = core.Inventory != null ? core.Inventory.RawRanch : 0f,
            totalRanch = core.Inventory != null
                ? core.Inventory.TotalRanchCollected
                : 0f,
            money = core.Inventory != null ? core.Inventory.Money : 0f,
            wave = core.Waves != null ? core.Waves.CurrentWave : 0,
            enemyCount = core.Waves != null ? core.Waves.EnemiesRemaining : 0,
            bottleCounts = core.Inventory != null
                ? core.Inventory.GetBottleCountsCopy()
                : null,
            unlockedBottleTier = core.Bottles != null
                ? core.Bottles.UnlockedTier
                : 0,
            selectedBottleTier = core.Bottles != null
                ? core.Bottles.SelectedTier
                : 0,
            toolTier = core.Upgrades != null
                ? core.Upgrades.ToolTier
                : 0,
            area0 = areas != null && areas.Length > 0 && areas[0],
            area1 = areas != null && areas.Length > 1 && areas[1],
            area2 = areas != null && areas.Length > 2 && areas[2],
            area3 = areas != null && areas.Length > 3 && areas[3]
        });
    }

    private void HandleGuestAttackInput(float now)
    {
        if (!connected || core.Player == null || Cursor.visible ||
            (core.Health != null && (core.Health.IsDead || core.Health.IsDowned)) ||
            core.Settings == null || core.Settings.IsOpen)
            return;

        if (now < nextGuestAttack)
            return;

        // Space is the jump key; light attack is left-click only.
        bool light = Input.GetMouseButtonDown(0);

        bool heavy = Input.GetKeyDown(KeyCode.Q);

        if (!light && !heavy)
            return;

        if (core.Equipment != null && !core.Equipment.WeaponSlotActive)
        {
            core.ShowMessage("Select your weapon slot to attack.");
            return;
        }

        float staminaCost = GetGuestAttackStaminaCost(heavy);
        if (core.Stamina != null &&
            staminaCost > 0f &&
            !core.Stamina.TrySpend(staminaCost))
        {
            core.ShowMessage(
                heavy
                    ? "Not enough stamina for a heavy attack."
                    : "Not enough stamina for a light attack."
            );
            return;
        }

        nextGuestAttack = now + (heavy ? 1.1f : 0.55f);
        Transform player = core.Player.transform;

        core.Player.PlayAttackAnimation(
            heavy,
            heavy ? 3 : 1
        );

        QueuePacket(new LanPacket
        {
            type = "attack",
            heavy = heavy,
            combo = heavy ? 3 : 1,
            activeSlot = core.Equipment != null ? core.Equipment.ActiveSlot : 1,
            equippedWeapon = core.Equipment != null
                ? (int)core.Equipment.EquippedWeapon
                : 0,
            classType = core.Classes != null
                ? (int)core.Classes.CurrentClass
                : 0,
            x = player.position.x,
            y = player.position.y,
            z = player.position.z,
            rotationY = player.eulerAngles.y
        });
        guestAttacksSent++;
        lastNetworkEvent =
            "Guest attack sent " + guestAttacksSent +
            (heavy ? " (heavy)" : " (light)");
    }

    private float GetGuestAttackStaminaCost(bool heavy)
    {
        float staminaCost = heavy ? 30f : 10f;

        if (core.Combat != null)
        {
            staminaCost = heavy
                ? core.Combat.HeavyAttackStamina
                : core.Combat.LightAttackStamina;
        }

        if (core.Equipment != null)
        {
            staminaCost *= heavy
                ? core.Equipment.HeavyStaminaMultiplier
                : core.Equipment.LightStaminaMultiplier;
        }

        return staminaCost;
    }

    // Hold the interact key next to a downed teammate to revive them. Works the
    // same whether the local player is the host or the guest.
    private void HandleReviveInput()
    {
        bool localIncapacitated =
            core.Health != null && (core.Health.IsDead || core.Health.IsDowned);

        if (!connected ||
            core.Player == null ||
            remotePlayer == null ||
            !hasRemotePlayerState ||
            !remotePlayerDead ||
            localIncapacitated ||
            (core.Settings != null && core.Settings.IsOpen))
        {
            reviveHoldTimer = 0f;
            return;
        }

        float distance = Vector3.Distance(
            core.Player.transform.position,
            remotePlayer.transform.position
        );

        if (distance > ReviveRange || !Input.GetKey(KeyCode.E))
        {
            reviveHoldTimer = 0f;
            return;
        }

        reviveHoldTimer += Time.unscaledDeltaTime;

        float remaining = Mathf.Max(0f, ReviveHoldSeconds - reviveHoldTimer);
        core.ShowMessage(
            "Reviving teammate... hold E (" + remaining.ToString("0.0") + "s)",
            0.4f
        );

        if (reviveHoldTimer < ReviveHoldSeconds)
            return;

        reviveHoldTimer = 0f;
        QueuePacket(new LanPacket { type = "revive" });
        revivesSent++;
        lastNetworkEvent = "Sent revive to teammate " + revivesSent;
        core.ShowMessage("Teammate revived!", 3f);
    }

    private void ProcessRevive()
    {
        revivesReceived++;

        if (core.Health != null && core.Health.IsDowned)
        {
            core.Health.Revive(0.5f);
            lastNetworkEvent = "Revived by teammate " + revivesReceived;
        }
        else
        {
            lastNetworkEvent = "Revive received but you were not downed";
        }
    }

    private void ProcessIncomingPackets()
    {
        int processed = 0;

        while (processed < 300 && incoming.TryDequeue(out string json))
        {
            processed++;

            LanPacket packet;
            try
            {
                packet = JsonUtility.FromJson<LanPacket>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Ignored malformed LAN packet: " + exception.Message);
                continue;
            }

            if (packet == null || string.IsNullOrEmpty(packet.type))
                continue;

            switch (packet.type)
            {
                case "player":
                    playerPacketsReceived++;
                    remotePlayerTargetPosition =
                        new Vector3(packet.x, packet.y, packet.z);
                    remotePlayerTargetRotation =
                        Quaternion.Euler(0f, packet.rotationY, 0f);
                    remotePlayerHealth = packet.health;
                    remotePlayerMaxHealth = Mathf.Max(1f, packet.maxHealth);
                    remotePlayerStamina = packet.stamina;
                    remotePlayerMaxStamina = Mathf.Max(1f, packet.maxStamina);
                    remoteActiveSlot = Mathf.Clamp(
                        packet.activeSlot,
                        0,
                        RanchEquipmentSystem.SlotCount - 1
                    );
                    remoteEquippedWeapon = Mathf.Clamp(packet.equippedWeapon, 0, 2);
                    remoteClassType = Mathf.Clamp(packet.classType, 0, 3);
                    remotePlayerDead = packet.dead;
                    remoteExtracting = packet.extracting;
                    hasRemotePlayerState = true;
                    EnsureRemotePlayerVisual();
                    UpdateRemotePlayerLabel();
                    RefreshRemoteEquipmentVisuals();
                    break;

                case "attack":
                    if (RanchGameModeState.IsLanHost)
                        ProcessGuestAttack(packet);
                    break;

                case "attack_anim":
                    if (RanchGameModeState.IsLanClient)
                    {
                        ApplyRemoteEquipmentPacket(packet);
                        PlayRemoteAttackAnimation(packet.heavy, packet.combo);
                    }
                    break;

                case "damage":
                    if (RanchGameModeState.IsLanClient)
                        ProcessRemoteDamage(packet);
                    break;

                case "revive":
                    ProcessRevive();
                    break;

                case "enemy":
                    if (RanchGameModeState.IsLanClient)
                        ProcessEnemyPacket(packet);
                    break;

                case "enemy_end":
                    if (RanchGameModeState.IsLanClient)
                        RemoveStaleEnemyVisuals(packet.frame);
                    break;

                case "summary":
                    if (RanchGameModeState.IsLanClient)
                        ProcessSummaryPacket(packet);
                    break;
            }
        }
    }

    private void ProcessGuestAttack(LanPacket packet)
    {
        guestAttacksReceived++;

        if (remotePlayerDead)
        {
            lastNetworkEvent = "Guest attack ignored: guest is defeated";
            return;
        }

        ApplyRemoteEquipmentPacket(packet);
        PlayRemoteAttackAnimation(packet.heavy, packet.combo);

        float now = Time.unscaledTime;
        float requiredDelay = packet.heavy ? 1.0f : 0.42f;

        if (now - lastHostAcceptedAttack < requiredDelay)
        {
            lastNetworkEvent = "Guest attack ignored by cooldown";
            return;
        }

        lastHostAcceptedAttack = now;

        if (core.Waves == null)
        {
            lastNetworkEvent = "Guest attack received but waves are missing";
            return;
        }

        Vector3 attackOrigin = hasRemotePlayerState
            ? remotePlayerTargetPosition
            : new Vector3(packet.x, packet.y, packet.z);

        float range = packet.heavy ? 4.2f : 3.4f;
        float damage = packet.heavy ? 52f : 27f;
        RanchEnemy nearest = null;
        float nearestDistance = range;

        List<RanchEnemy> active = core.Waves.ActiveEnemies;
        for (int i = 0; i < active.Count; i++)
        {
            RanchEnemy enemy = active[i];
            if (enemy == null || enemy.Health <= 0f)
                continue;

            float distance = Vector3.Distance(
                attackOrigin,
                enemy.transform.position
            );

            if (distance <= nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        if (nearest != null)
        {
            guestAttackHits++;
            nearest.TakeDamage(damage, false, 0f);
            lastNetworkEvent =
                "Guest attack hit " + nearest.EnemyName +
                " (" + guestAttackHits + " hits)";
        }
        else
        {
            lastNetworkEvent =
                "Guest attack received but no enemy was in range";
        }
    }

    private void ProcessRemoteDamage(LanPacket packet)
    {
        if (core.Health == null)
            return;

        remoteDamageReceived++;
        string source = string.IsNullOrEmpty(packet.name)
            ? "Remote Ranch Raider"
            : packet.name;

        core.Health.TakeDamage(
            Mathf.Max(0f, packet.health),
            source
        );

        if (core.Player != null)
        {
            Vector3 enemyPosition = new Vector3(packet.x, packet.y, packet.z);
            Vector3 knockbackDirection =
                core.Player.transform.position - enemyPosition;
            core.Player.ApplyKnockback(knockbackDirection, 2.2f);
        }

        lastNetworkEvent =
            "Damage received from host " + remoteDamageReceived + "x";
    }

    private void HandleRemoteGuestEnemyDamage(float now)
    {
        if (!hasRemotePlayerState ||
            remotePlayerDead ||
            core.Waves == null ||
            now < nextRemoteEnemyDamage)
        {
            return;
        }

        List<RanchEnemy> active = core.Waves.ActiveEnemies;
        RanchEnemy nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < active.Count; i++)
        {
            RanchEnemy enemy = active[i];
            if (enemy == null || enemy.Health <= 0f)
                continue;

            float hitRange = enemy.IsBoss
                ? RemoteEnemyDamageRange + 1.25f
                : RemoteEnemyDamageRange;

            float distance = Vector3.Distance(
                remotePlayerTargetPosition,
                enemy.transform.position
            );

            if (distance <= hitRange && distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        if (nearest == null)
            return;

        nextRemoteEnemyDamage = now + RemoteEnemyDamageInterval;
        remoteDamageSent++;
        QueuePacket(new LanPacket
        {
            type = "damage",
            name = nearest.EnemyName,
            health = Mathf.Max(1f, nearest.Damage),
            x = nearest.transform.position.x,
            y = nearest.transform.position.y,
            z = nearest.transform.position.z
        });

        lastNetworkEvent =
            "Enemy hit guest " + remoteDamageSent + "x";
    }

    private void ProcessEnemyPacket(LanPacket packet)
    {
        if (!remoteEnemies.TryGetValue(packet.id, out RemoteEnemyVisual visual))
        {
            visual = CreateRemoteEnemyVisual(packet);
            remoteEnemies.Add(packet.id, visual);
        }

        visual.TargetPosition = new Vector3(packet.x, packet.y, packet.z);
        visual.TargetRotation = Quaternion.Euler(0f, packet.rotationY, 0f);
        visual.LastFrame = packet.frame;

        if (visual.Label != null)
        {
            visual.Label.text =
                packet.name + "\n" +
                Mathf.CeilToInt(Mathf.Max(0f, packet.health)) +
                " / " +
                Mathf.CeilToInt(Mathf.Max(1f, packet.maxHealth));
        }
    }

    private void ProcessSummaryPacket(LanPacket packet)
    {
        // The guest owns its own economy now, so the host summary is used only to
        // show the teammate's progress in the info panel — it must NOT overwrite
        // the guest's wallet, bottles, upgrades, or areas.
        hostRawRanch = packet.rawRanch;
        hostTotalRanch = packet.totalRanch;
        hostMoney = packet.money;
        hostWave = packet.wave;
        hostEnemyCount = packet.enemyCount;
    }

    private void EnsureRemotePlayerVisual()
    {
        if (remotePlayer != null)
            return;

        remotePlayer = new GameObject(
            RanchGameModeState.IsLanHost
                ? SessionLabel + " Guest Player"
                : SessionLabel + " Host Player"
        );

        GameObject prefab =
            Resources.Load<GameObject>("Prefabs/PlayerModel");

        if (prefab != null)
        {
            GameObject model = Instantiate(prefab, remotePlayer.transform);
            model.name = "Remote Player Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            StripRemoteModelComponents(model);
        }
        else
        {
            GameObject capsule =
                GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Remote Player Capsule";
            capsule.transform.SetParent(remotePlayer.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);

            Collider collider = capsule.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = capsule.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial =
                    RanchWorldBuilder.CreateRuntimeMaterial(
                        RanchGameModeState.IsLanHost
                            ? new Color(0.20f, 0.70f, 1f)
                            : new Color(1f, 0.65f, 0.18f)
                    );
            }
        }

        GameObject labelObject = new GameObject("LAN Player Label");
        labelObject.transform.SetParent(remotePlayer.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 2.9f, 0f);

        remotePlayerLabel = labelObject.AddComponent<TextMesh>();
        remotePlayerLabel.fontSize = 52;
        remotePlayerLabel.characterSize = 0.075f;
        remotePlayerLabel.anchor = TextAnchor.MiddleCenter;
        remotePlayerLabel.alignment = TextAlignment.Center;
        remotePlayerLabel.color = Color.white;
        labelObject.AddComponent<RanchBillboard>();
        UpdateRemotePlayerLabel();

        CreateRemoteAttackArc();
        EnsureRemoteEquipmentVisuals();
        RefreshRemoteEquipmentVisuals();

        if (hasRemotePlayerState)
        {
            remotePlayer.transform.position = remotePlayerTargetPosition;
            remotePlayer.transform.rotation = remotePlayerTargetRotation;
        }
    }

    private void UpdateRemotePlayerLabel()
    {
        if (remotePlayerLabel == null)
            return;

        string playerName = RanchGameModeState.IsLanHost
            ? SessionLabel + " GUEST"
            : SessionLabel + " HOST";

        remotePlayerLabel.text =
            playerName + "\n" +
            "HP " +
            Mathf.CeilToInt(Mathf.Max(0f, remotePlayerHealth)) +
            "/" +
            Mathf.CeilToInt(Mathf.Max(1f, remotePlayerMaxHealth)) +
            "  STA " +
            Mathf.CeilToInt(Mathf.Max(0f, remotePlayerStamina)) +
            "/" +
            Mathf.CeilToInt(Mathf.Max(1f, remotePlayerMaxStamina)) +
            (remotePlayerDead ? "\nDOWNED — hold E to revive" : "");
    }

    private void ApplyRemoteEquipmentPacket(LanPacket packet)
    {
        remoteActiveSlot = Mathf.Clamp(
            packet.activeSlot,
            0,
            RanchEquipmentSystem.SlotCount - 1
        );
        remoteEquippedWeapon = Mathf.Clamp(packet.equippedWeapon, 0, 2);
        remoteClassType = Mathf.Clamp(packet.classType, 0, 3);
        remoteExtracting = packet.extracting;

        EnsureRemotePlayerVisual();
        RefreshRemoteEquipmentVisuals();
    }

    private void EnsureRemoteEquipmentVisuals()
    {
        if (remotePlayer == null || remoteEquipmentAnchor != null)
            return;

        remoteEquipmentAnchor =
            FindChildRecursive(remotePlayer.transform, "RightHandAnchor");

        if (remoteEquipmentAnchor == null)
        {
            GameObject anchor = new GameObject("Remote Right Hand Equipment Anchor");
            anchor.transform.SetParent(remotePlayer.transform, false);
            anchor.transform.localPosition = new Vector3(0.72f, 0.05f, 0.05f);
            remoteEquipmentAnchor = anchor.transform;
        }

        remoteSword = CreateRemoteSword(remoteEquipmentAnchor);
        remoteSpear = CreateRemoteSpear(remoteEquipmentAnchor);
        remoteBow = CreateRemoteBow(remoteEquipmentAnchor);
        remoteExtractor = CreateRemoteExtractor(remoteEquipmentAnchor);
        remoteTrap = CreateRemoteTrap(remoteEquipmentAnchor);
        remoteWand = CreateRemoteWand(remoteEquipmentAnchor);
    }

    private void RefreshRemoteEquipmentVisuals()
    {
        EnsureRemoteEquipmentVisuals();

        bool showExtractor = remoteExtracting || remoteActiveSlot == 0;
        bool showWeapon =
            !remoteExtracting &&
            remoteActiveSlot == 1 &&
            remoteClassType != (int)RanchClassType.Summoner;
        bool showTrap = !remoteExtracting && remoteActiveSlot == 2;
        bool showWand =
            !remoteExtracting &&
            remoteActiveSlot == 1 &&
            remoteClassType == (int)RanchClassType.Summoner;

        SetRemoteEquipmentVisible(remoteExtractor, showExtractor);
        SetRemoteEquipmentVisible(
            remoteSword,
            showWeapon && remoteClassType == (int)RanchClassType.Sword
        );
        SetRemoteEquipmentVisible(
            remoteSpear,
            showWeapon && remoteClassType == (int)RanchClassType.Spear
        );
        SetRemoteEquipmentVisible(
            remoteBow,
            showWeapon && remoteClassType == (int)RanchClassType.Ranged
        );
        SetRemoteEquipmentVisible(remoteTrap, showTrap);
        SetRemoteEquipmentVisible(remoteWand, showWand);

        if (remoteAnimatedEquipment != null &&
            !remoteAnimatedEquipment.gameObject.activeInHierarchy)
        {
            ResetRemoteEquipmentAnimation();
        }
    }

    private static void SetRemoteEquipmentVisible(
        Transform target,
        bool visible)
    {
        if (target != null && target.gameObject.activeSelf != visible)
            target.gameObject.SetActive(visible);
    }

    private Transform GetRemoteActiveWeaponVisual()
    {
        if (remoteActiveSlot != 1 ||
            remoteClassType == (int)RanchClassType.Summoner)
        {
            return null;
        }

        switch ((RanchWeaponType)remoteEquippedWeapon)
        {
            case RanchWeaponType.Spear:
                return remoteSpear;
            case RanchWeaponType.Bow:
                return remoteBow;
            default:
                return remoteSword;
        }
    }

    private Transform CreateRemoteSword(Transform anchor)
    {
        GameObject root = CreateRemoteEquipmentRoot(
            anchor,
            "Remote Ranch Defender Sword",
            Vector3.zero,
            Quaternion.Euler(20f, 0f, -25f)
        );
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Sword Blade", new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.2f, 0.08f), new Color(0.90f, 0.90f, 0.85f));
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Sword Handle", new Vector3(0f, -0.15f, 0f), new Vector3(0.18f, 0.35f, 0.14f), new Color(0.35f, 0.20f, 0.09f));
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Sword Guard", new Vector3(0f, 0.05f, 0f), new Vector3(0.55f, 0.08f, 0.12f), new Color(0.95f, 0.75f, 0.25f));
        return root.transform;
    }

    private Transform CreateRemoteSpear(Transform anchor)
    {
        GameObject root = CreateRemoteEquipmentRoot(
            anchor,
            "Remote Ranch Spear",
            new Vector3(0f, 0.15f, 0.1f),
            Quaternion.Euler(75f, 0f, 0f)
        );
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cylinder, "Remote Spear Shaft", new Vector3(0f, 0.8f, 0f), new Vector3(0.07f, 1.4f, 0.07f), new Color(0.35f, 0.20f, 0.09f));
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Spear Head", new Vector3(0f, 2.3f, 0f), new Vector3(0.22f, 0.48f, 0.10f), new Color(0.90f, 0.90f, 0.85f));
        return root.transform;
    }

    private Transform CreateRemoteBow(Transform anchor)
    {
        GameObject root = CreateRemoteEquipmentRoot(
            anchor,
            "Remote Ranch Bow",
            new Vector3(0f, 0.25f, 0.1f),
            Quaternion.Euler(0f, 0f, -10f)
        );
        GameObject upper = CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Bow Upper Limb", new Vector3(0f, 0.75f, 0f), new Vector3(0.10f, 0.75f, 0.10f), new Color(0.35f, 0.20f, 0.09f));
        upper.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        GameObject lower = CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Bow Lower Limb", new Vector3(0f, -0.75f, 0f), new Vector3(0.10f, 0.75f, 0.10f), new Color(0.35f, 0.20f, 0.09f));
        lower.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Bow Grip", Vector3.zero, new Vector3(0.16f, 0.35f, 0.14f), new Color(0.05f, 0.05f, 0.05f));
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Bow String", Vector3.zero, new Vector3(0.025f, 1.55f, 0.025f), new Color(0.90f, 0.90f, 0.85f));
        return root.transform;
    }

    private Transform CreateRemoteExtractor(Transform anchor)
    {
        GameObject root = CreateRemoteEquipmentRoot(
            anchor,
            "Remote Held Ranch Extractor",
            new Vector3(0f, 0.05f, 0.05f),
            Quaternion.Euler(0f, 0f, -8f)
        );
        GameObject body = CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cylinder, "Remote Extractor Body", Vector3.zero, new Vector3(0.23f, 0.55f, 0.23f), new Color(0.55f, 0.25f, 0.80f));
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Extractor Nozzle", new Vector3(0f, 0f, 0.65f), new Vector3(0.14f, 0.14f, 0.8f), new Color(0.05f, 0.05f, 0.05f));
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Extractor Grip", new Vector3(0f, -0.34f, 0.05f), new Vector3(0.16f, 0.48f, 0.16f), new Color(0.35f, 0.20f, 0.09f));
        return root.transform;
    }

    private Transform CreateRemoteTrap(Transform anchor)
    {
        GameObject root = CreateRemoteEquipmentRoot(
            anchor,
            "Remote Held Ranch Trap",
            new Vector3(0f, 0.05f, 0.05f),
            Quaternion.Euler(0f, 0f, -12f)
        );
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cylinder, "Remote Trap Base", Vector3.zero, new Vector3(0.42f, 0.10f, 0.42f), new Color(0.05f, 0.05f, 0.05f));
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * 2f / 4f;
            Vector3 position =
                new Vector3(Mathf.Cos(angle) * 0.30f, 0.16f, Mathf.Sin(angle) * 0.30f);
            GameObject spike = CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Trap Spike " + i, position, new Vector3(0.08f, 0.28f, 0.08f), new Color(0.95f, 0.75f, 0.25f));
            spike.transform.localRotation =
                Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f) *
                Quaternion.Euler(18f, 0f, 18f);
        }

        return root.transform;
    }

    private Transform CreateRemoteWand(Transform anchor)
    {
        GameObject root = CreateRemoteEquipmentRoot(
            anchor,
            "Remote Held Delulu Wand",
            new Vector3(0f, 0.06f, 0.06f),
            Quaternion.Euler(18f, 0f, -18f)
        );
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cylinder, "Remote Wand Shaft", Vector3.zero, new Vector3(0.08f, 0.85f, 0.08f), new Color(0.55f, 0.25f, 0.80f));
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Sphere, "Remote Wand Core", new Vector3(0f, 0.55f, 0f), Vector3.one * 0.28f, new Color(1f, 0.92f, 0.68f));
        CreateRemoteEquipmentPiece(root.transform, PrimitiveType.Cube, "Remote Wand Grip", new Vector3(0f, -0.42f, 0f), new Vector3(0.15f, 0.30f, 0.15f), new Color(0.35f, 0.20f, 0.09f));
        return root.transform;
    }

    private static GameObject CreateRemoteEquipmentRoot(
        Transform anchor,
        string name,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = localRotation;
        return root;
    }

    private static GameObject CreateRemoteEquipmentPiece(
        Transform parent,
        PrimitiveType type,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Color color)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = RanchWorldBuilder.CreateRuntimeMaterial(color);

        Collider collider = piece.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return piece;
    }

    private void CreateRemoteAttackArc()
    {
        if (remotePlayer == null || remoteAttackArc != null)
            return;

        remoteAttackArc = GameObject.CreatePrimitive(PrimitiveType.Cube);
        remoteAttackArc.name = "Remote Attack Swing";
        remoteAttackArc.transform.SetParent(remotePlayer.transform, false);
        remoteAttackArc.transform.localPosition =
            new Vector3(0.45f, 1.25f, 0.95f);
        remoteAttackArc.transform.localRotation =
            Quaternion.Euler(0f, -45f, 22f);
        remoteAttackArc.transform.localScale =
            new Vector3(0.12f, 0.12f, 1.35f);

        Collider collider = remoteAttackArc.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = remoteAttackArc.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial =
                RanchWorldBuilder.CreateRuntimeMaterial(
                    new Color(1f, 0.82f, 0.22f)
                );
        }

        remoteAttackArc.SetActive(false);
    }

    private void PlayRemoteAttackAnimation(bool heavy, int combo)
    {
        EnsureRemotePlayerVisual();
        CreateRemoteAttackArc();
        StartRemoteEquipmentAttackAnimation(heavy, combo);

        remoteAttackHeavy = heavy;
        remoteAttackDuration = heavy ? 0.42f : 0.26f;
        remoteAttackTimer = remoteAttackDuration;

        if (remoteAttackArc != null)
            remoteAttackArc.SetActive(true);
    }

    private void StartRemoteEquipmentAttackAnimation(bool heavy, int combo)
    {
        Transform weapon = GetRemoteActiveWeaponVisual();
        if (weapon == null)
            return;

        if (remoteAnimatedEquipment != null &&
            remoteAnimatedEquipment != weapon)
        {
            ResetRemoteEquipmentAnimation();
        }

        remoteAnimatedEquipment = weapon;
        remoteEquipmentBasePosition = weapon.localPosition;
        remoteEquipmentBaseRotation = weapon.localRotation;
        remoteEquipmentAnimationHeavy = heavy;
        remoteEquipmentAnimationCombo = Mathf.Max(1, combo);
        remoteEquipmentAnimationDuration = heavy ? 0.75f : 0.32f;
        remoteEquipmentAnimationTimer = remoteEquipmentAnimationDuration;
    }

    private void UpdateRemoteAttackAnimation()
    {
        bool animateArc = remoteAttackArc != null && remoteAttackTimer > 0f;
        bool animateEquipment =
            remoteAnimatedEquipment != null &&
            remoteEquipmentAnimationTimer > 0f;

        if (!animateArc && !animateEquipment)
            return;

        if (animateArc)
            UpdateRemoteAttackArc();

        if (animateEquipment)
            UpdateRemoteEquipmentAttackAnimation();
    }

    private void UpdateRemoteAttackArc()
    {
        remoteAttackTimer -= Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.01f, remoteAttackDuration);
        float progress = 1f - Mathf.Clamp01(remoteAttackTimer / duration);
        float angle = Mathf.Lerp(-75f, 85f, progress);
        float reach = remoteAttackHeavy ? 1.75f : 1.35f;

        remoteAttackArc.transform.localPosition =
            new Vector3(0.45f, 1.25f, 0.95f);
        remoteAttackArc.transform.localRotation =
            Quaternion.Euler(0f, angle, remoteAttackHeavy ? 35f : 22f);
        remoteAttackArc.transform.localScale =
            new Vector3(
                remoteAttackHeavy ? 0.18f : 0.12f,
                remoteAttackHeavy ? 0.18f : 0.12f,
                reach
            );

        if (remoteAttackTimer <= 0f)
            remoteAttackArc.SetActive(false);
    }

    private void UpdateRemoteEquipmentAttackAnimation()
    {
        remoteEquipmentAnimationTimer -= Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.01f, remoteEquipmentAnimationDuration);
        float progress =
            1f - Mathf.Clamp01(remoteEquipmentAnimationTimer / duration);
        float pulse = Mathf.Sin(progress * Mathf.PI);

        switch ((RanchWeaponType)remoteEquippedWeapon)
        {
            case RanchWeaponType.Spear:
                remoteAnimatedEquipment.localPosition =
                    remoteEquipmentBasePosition +
                    Vector3.forward *
                    pulse *
                    (remoteEquipmentAnimationHeavy ? 1.2f : 0.7f);
                remoteAnimatedEquipment.localRotation =
                    remoteEquipmentBaseRotation *
                    Quaternion.Euler(-pulse * 12f, 0f, 0f);
                break;

            case RanchWeaponType.Bow:
                remoteAnimatedEquipment.localPosition =
                    remoteEquipmentBasePosition + Vector3.back * pulse * 0.18f;
                remoteAnimatedEquipment.localRotation =
                    remoteEquipmentBaseRotation *
                    Quaternion.Euler(0f, pulse * 8f, 0f);
                break;

            default:
                float angle = remoteEquipmentAnimationHeavy
                    ? 145f
                    : 75f + remoteEquipmentAnimationCombo * 10f;
                remoteAnimatedEquipment.localPosition =
                    remoteEquipmentBasePosition;
                remoteAnimatedEquipment.localRotation =
                    remoteEquipmentBaseRotation *
                    Quaternion.Euler(pulse * angle, 0f, 0f);
                break;
        }

        if (remoteEquipmentAnimationTimer <= 0f)
            ResetRemoteEquipmentAnimation();
    }

    private void ResetRemoteEquipmentAnimation()
    {
        if (remoteAnimatedEquipment != null)
        {
            remoteAnimatedEquipment.localPosition = remoteEquipmentBasePosition;
            remoteAnimatedEquipment.localRotation = remoteEquipmentBaseRotation;
        }

        remoteAnimatedEquipment = null;
        remoteEquipmentAnimationTimer = 0f;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result =
                FindChildRecursive(root.GetChild(i), childName);

            if (result != null)
                return result;
        }

        return null;
    }

    private static void StripRemoteModelComponents(GameObject model)
    {
        Camera[] cameras = model.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            Destroy(cameras[i]);

        AudioListener[] listeners =
            model.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
            Destroy(listeners[i]);

        Light[] lights = model.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            Destroy(lights[i]);

        Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);

        Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
            Destroy(bodies[i]);

        RanchPlayerController[] controllers =
            model.GetComponentsInChildren<RanchPlayerController>(true);
        for (int i = 0; i < controllers.Length; i++)
            Destroy(controllers[i]);
    }

    private RemoteEnemyVisual CreateRemoteEnemyVisual(LanPacket packet)
    {
        PrimitiveType primitive = PrimitiveType.Capsule;
        Vector3 scale = Vector3.one;
        Color color = new Color(0.82f, 0.20f, 0.16f);

        if (packet.archetype == (int)RanchEnemy.EnemyArchetype.RanchBat)
        {
            primitive = PrimitiveType.Sphere;
            scale = new Vector3(1.1f, 0.55f, 1.4f);
            color = new Color(0.58f, 0.24f, 0.82f);
        }
        else if (packet.archetype ==
                 (int)RanchEnemy.EnemyArchetype.RanchRotCrawler)
        {
            primitive = PrimitiveType.Cube;
            scale = new Vector3(1.3f, 0.45f, 1.7f);
            color = new Color(0.30f, 0.66f, 0.18f);
        }

        if (packet.boss)
        {
            scale *= 1.65f;
            color = new Color(0.95f, 0.52f, 0.10f);
        }

        GameObject root = new GameObject(
            SessionLabel + " Enemy " + packet.id + " - " + packet.name
        );

        GameObject body = GameObject.CreatePrimitive(primitive);
        body.name = "Remote Enemy Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = scale;

        Collider collider = body.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = body.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial =
                RanchWorldBuilder.CreateRuntimeMaterial(color);

        GameObject labelObject = new GameObject("Remote Enemy Label");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition =
            new Vector3(0f, packet.boss ? 2.8f : 1.8f, 0f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.fontSize = 42;
        label.characterSize = 0.065f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.AddComponent<RanchBillboard>();

        root.transform.position = new Vector3(packet.x, packet.y, packet.z);
        root.transform.rotation = Quaternion.Euler(0f, packet.rotationY, 0f);

        return new RemoteEnemyVisual
        {
            Root = root,
            Label = label,
            TargetPosition = root.transform.position,
            TargetRotation = root.transform.rotation,
            LastFrame = packet.frame
        };
    }

    private void RemoveStaleEnemyVisuals(int completedFrame)
    {
        List<int> remove = null;

        foreach (KeyValuePair<int, RemoteEnemyVisual> pair in remoteEnemies)
        {
            if (pair.Value.LastFrame == completedFrame)
                continue;

            if (remove == null)
                remove = new List<int>();
            remove.Add(pair.Key);
        }

        if (remove == null)
            return;

        for (int i = 0; i < remove.Count; i++)
        {
            int id = remove[i];
            if (remoteEnemies.TryGetValue(id, out RemoteEnemyVisual visual) &&
                visual.Root != null)
            {
                Destroy(visual.Root);
            }

            remoteEnemies.Remove(id);
        }
    }

    private void UpdateRemotePlayerVisual()
    {
        if (remotePlayer == null || !hasRemotePlayerState)
            return;

        remotePlayer.transform.position = Vector3.Lerp(
            remotePlayer.transform.position,
            remotePlayerTargetPosition,
            14f * Time.unscaledDeltaTime
        );

        remotePlayer.transform.rotation = Quaternion.Slerp(
            remotePlayer.transform.rotation,
            remotePlayerTargetRotation,
            14f * Time.unscaledDeltaTime
        );
    }

    private void UpdateRemoteEnemyVisuals()
    {
        foreach (RemoteEnemyVisual visual in remoteEnemies.Values)
        {
            if (visual.Root == null)
                continue;

            visual.Root.transform.position = Vector3.Lerp(
                visual.Root.transform.position,
                visual.TargetPosition,
                16f * Time.unscaledDeltaTime
            );

            visual.Root.transform.rotation = Quaternion.Slerp(
                visual.Root.transform.rotation,
                visual.TargetRotation,
                16f * Time.unscaledDeltaTime
            );
        }
    }

    private void QueuePacket(LanPacket packet)
    {
        if (!connected || packet == null)
            return;

        packetsSent++;
        if (packet.type == "player")
            playerPacketsSent++;

        outgoing.Enqueue(JsonUtility.ToJson(packet));
    }

    private void HostAcceptLoop()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, DefaultPort);
            listener.Start(1);
            TcpClient accepted = listener.AcceptTcpClient();

            if (stopRequested)
            {
                accepted.Close();
                return;
            }

            listener.Stop();
            ConfigureConnection(accepted, null);
            statusText = SessionLabel + " guest connected";
        }
        catch (Exception exception)
        {
            if (!stopRequested)
                statusText = "Host failed: " + exception.Message;
        }
    }

    private void ClientConnectLoop(
        string hostAddress,
        int port,
        string firstLine,
        string connectedText)
    {
        try
        {
            TcpClient connectingClient = new TcpClient();
            connectingClient.NoDelay = true;
            connectingClient.Connect(hostAddress, port);

            if (stopRequested)
            {
                connectingClient.Close();
                return;
            }

            ConfigureConnection(connectingClient, firstLine);
            statusText = connectedText;
        }
        catch (Exception exception)
        {
            if (!stopRequested)
                statusText = "Connection failed: " + exception.Message;
        }
    }

    private void ConfigureConnection(TcpClient connection, string firstLine)
    {
        lock (connectionLock)
        {
            tcpClient = connection;
            tcpClient.NoDelay = true;

            NetworkStream stream = tcpClient.GetStream();
            reader = new StreamReader(
                stream,
                new UTF8Encoding(false),
                false,
                4096,
                true
            );
            writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                4096,
                true
            )
            {
                AutoFlush = true
            };

            if (!string.IsNullOrEmpty(firstLine))
                writer.WriteLine(firstLine);
        }

        connected = true;

        receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "Ranch " + SessionLabel + " Receiver"
        };

        sendThread = new Thread(SendLoop)
        {
            IsBackground = true,
            Name = "Ranch " + SessionLabel + " Sender"
        };

        receiveThread.Start();
        sendThread.Start();
    }

    private void ReceiveLoop()
    {
        try
        {
            while (!stopRequested)
            {
                string line = reader.ReadLine();
                if (line == null)
                    break;

                if (line.StartsWith(
                    "__ranch_relay|",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ProcessRelayControl(line);
                }
                else if (line.Length > 0)
                {
                    packetsReceived++;
                    incoming.Enqueue(line);
                }
            }
        }
        catch (Exception exception)
        {
            if (!stopRequested)
                statusText = "Connection lost: " + exception.Message;
        }
        finally
        {
            MarkDisconnected();
        }
    }

    private void ProcessRelayControl(string line)
    {
        if (line.IndexOf("|paired", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            relayPaired = true;
            statusText = "Relay peer connected";
            lastNetworkEvent = "Relay paired both players";
        }
        else if (line.IndexOf("|waiting", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            relayPaired = false;
            statusText = "Connected to relay. Waiting for the other player.";
            lastNetworkEvent = "Relay waiting for the other player";
        }
        else if (line.IndexOf("|room_full", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            statusText = "Relay room is full. Pick another room code.";
            lastNetworkEvent = "Relay room was full";
        }
    }

    private void SendLoop()
    {
        try
        {
            while (!stopRequested)
            {
                if (outgoing.TryDequeue(out string line))
                    writer.WriteLine(line);
                else
                    Thread.Sleep(4);
            }
        }
        catch (Exception exception)
        {
            if (!stopRequested)
                statusText = "Connection lost: " + exception.Message;
        }
        finally
        {
            MarkDisconnected();
        }
    }

    private void MarkDisconnected()
    {
        connected = false;
        relayPaired = false;

        if (!stopRequested)
            statusText =
                SessionLabel + " player disconnected. Press F10 for title screen.";
    }

    public void ReturnToTitleScreen()
    {
        ShutdownConnection(true);
        RanchGameModeState.ResetToTitle();

        if (core != null)
            core.RestartScene();
    }

    private void ShutdownConnection(bool updateStatus)
    {
        stopRequested = true;
        connected = false;

        try { listener?.Stop(); } catch { }
        try { reader?.Close(); } catch { }
        try { writer?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }

        listener = null;
        reader = null;
        writer = null;
        tcpClient = null;

        while (incoming.TryDequeue(out _)) { }
        while (outgoing.TryDequeue(out _)) { }
        packetsSent = 0;
        packetsReceived = 0;
        playerPacketsSent = 0;
        playerPacketsReceived = 0;
        guestAttacksSent = 0;
        guestAttacksReceived = 0;
        guestAttackHits = 0;
        remoteDamageSent = 0;
        remoteDamageReceived = 0;
        revivesSent = 0;
        revivesReceived = 0;
        reviveHoldTimer = 0f;
        lastNetworkEvent = "No multiplayer traffic yet";

        if (remotePlayer != null)
            Destroy(remotePlayer);
        remotePlayer = null;
        remotePlayerLabel = null;
        remoteEquipmentAnchor = null;
        remoteExtractor = null;
        remoteSword = null;
        remoteSpear = null;
        remoteBow = null;
        remoteTrap = null;
        remoteWand = null;
        remoteAnimatedEquipment = null;
        remoteAttackArc = null;
        remoteAttackTimer = 0f;
        remoteEquipmentAnimationTimer = 0f;
        remotePlayerHealth = 0f;
        remotePlayerMaxHealth = 1f;
        remotePlayerStamina = 0f;
        remotePlayerMaxStamina = 1f;
        remoteActiveSlot = 1;
        remoteEquippedWeapon = 0;
        remoteClassType = 0;
        remotePlayerDead = false;
        remoteExtracting = false;
        hasRemotePlayerState = false;
        nextRemoteEnemyDamage = 0f;

        if (core != null &&
            core.Equipment != null &&
            RanchGameModeState.IsLanClient)
        {
            core.Equipment.SetExtractionOverride(false);
        }

        foreach (RemoteEnemyVisual visual in remoteEnemies.Values)
        {
            if (visual.Root != null)
                Destroy(visual.Root);
        }
        remoteEnemies.Clear();

        if (updateStatus)
            statusText = SessionLabel + " multiplayer stopped";
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;

        ShutdownConnection(false);
    }

    private void OnApplicationQuit()
    {
        ShutdownConnection(false);
    }

    private void OnGUI()
    {
        if (!RanchGameModeState.IsMultiplayer)
            return;

        EnsureStyles();

        int oldDepth = GUI.depth;
        GUI.depth = -900;

        float width = 390f;
        float height = RanchGameModeState.IsLanClient ? 285f : 270f;
        Rect panel = new Rect(
            Screen.width - width - 18f,
            18f,
            width,
            height
        );

        GUI.Box(panel, GUIContent.none, panelStyle);

        string heading = RanchGameModeState.IsLanHost
            ? SessionLabel + " HOST"
            : SessionLabel + " GUEST";

        GUI.Label(
            new Rect(panel.x + 16f, panel.y + 10f, width - 32f, 28f),
            heading,
            headingStyle
        );

        string body;
        if (RanchGameModeState.IsLanHost)
        {
            body =
                statusText + "\n" +
                "Relay paired: " + (relayPaired ? "yes" : "no") +
                "  Packets in/out: " + packetsReceived + "/" + packetsSent + "\n" +
                "Remote moves: " + playerPacketsReceived +
                "  Guest attacks: " + guestAttacksReceived +
                "  Hits: " + guestAttackHits + "\n" +
                GetRemotePlayerStatusLine() +
                "  Enemy hits: " + remoteDamageSent + "\n" +
                "Revives given/received: " + revivesReceived + "/" + revivesSent + "\n" +
                "Last: " + lastNetworkEvent + "\n" +
                (transportMode == TransportMode.DirectOnline
                    ? "Forward TCP " + DefaultPort + " to this computer."
                    : transportMode == TransportMode.Relay
                    ? "No port forwarding. Relay carries the connection."
                    : "Each player keeps their own ranch and save.");
        }
        else
        {
            body =
                statusText + "\n" +
                "Teammate Ranch: " + hostRawRanch.ToString("F0") +
                "  Total: " + hostTotalRanch.ToString("F0") +
                "  $" + hostMoney.ToString("F0") + "\n" +
                "Teammate Wave: " + hostWave +
                "  Enemies: " + hostEnemyCount + "\n" +
                "Relay paired: " + (relayPaired ? "yes" : "no") +
                "  Packets in/out: " + packetsReceived + "/" + packetsSent + "\n" +
                GetRemotePlayerStatusLine() +
                "  Damage taken: " + remoteDamageReceived + "\n" +
                "Moves sent: " + playerPacketsSent +
                "  Attacks sent: " + guestAttacksSent + "\n" +
                "Revives given/received: " + revivesSent + "/" + revivesReceived + "\n" +
                "You have your own ranch, shop & save. Hold E to revive.";
        }

        GUI.Label(
            new Rect(panel.x + 16f, panel.y + 42f, width - 32f, height - 76f),
            body,
            bodyStyle
        );

        GUI.Label(
            new Rect(panel.x + 16f, panel.y + height - 31f, width - 32f, 22f),
            "F10 — disconnect and return to title",
            warningStyle
        );

        GUI.depth = oldDepth;
    }

    private string GetRemotePlayerStatusLine()
    {
        if (!hasRemotePlayerState)
            return "Remote player: waiting";

        return
            "Remote HP: " +
            Mathf.CeilToInt(Mathf.Max(0f, remotePlayerHealth)) +
            "/" +
            Mathf.CeilToInt(Mathf.Max(1f, remotePlayerMaxHealth)) +
            "  STA: " +
            Mathf.CeilToInt(Mathf.Max(0f, remotePlayerStamina)) +
            "/" +
            Mathf.CeilToInt(Mathf.Max(1f, remotePlayerMaxStamina)) +
            (remotePlayerDead ? "  DOWN" : "");
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        panelTexture = new Texture2D(1, 1);
        panelTexture.SetPixel(
            0,
            0,
            new Color(0.02f, 0.04f, 0.05f, 0.94f)
        );
        panelTexture.Apply();

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;

        headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        headingStyle.normal.textColor =
            new Color(0.35f, 0.95f, 0.70f);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;

        warningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        warningStyle.normal.textColor =
            new Color(0.95f, 0.78f, 0.32f);

        stylesReady = true;
    }

    private static string FindLocalIPv4Address()
    {
        try
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            for (int i = 0; i < host.AddressList.Length; i++)
            {
                IPAddress address = host.AddressList[i];
                if (address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                {
                    return address.ToString();
                }
            }
        }
        catch
        {
            // Fall through to loopback.
        }

        return "127.0.0.1";
    }

    private static bool TryParseEndpoint(
        string value,
        out string address,
        out int port)
    {
        address = "";
        port = DefaultPort;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string cleaned = value.Trim();

        if (cleaned.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(6);

        int separator = cleaned.LastIndexOf(':');
        if (separator > 0 &&
            separator < cleaned.Length - 1 &&
            cleaned.IndexOf(':') == separator)
        {
            string portText = cleaned.Substring(separator + 1);
            if (!int.TryParse(portText, out port) ||
                port <= 0 ||
                port > 65535)
            {
                return false;
            }

            cleaned = cleaned.Substring(0, separator);
        }

        address = cleaned.Trim();
        return address.Length > 0;
    }

    private static string CleanRoomCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string cleaned = value.Trim().ToUpperInvariant();
        StringBuilder builder = new StringBuilder(cleaned.Length);
        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if ((c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '-' ||
                c == '_')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
