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
        public int archetype;
        public bool boss;
        public bool heavy;
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
    private Vector3 remotePlayerTargetPosition;
    private Quaternion remotePlayerTargetRotation = Quaternion.identity;
    private bool hasRemotePlayerState;
    private bool clientRestrictionsApplied;
    private GameObject remoteAttackArc;
    private float remoteAttackTimer;
    private float remoteAttackDuration;
    private bool remoteAttackHeavy;

    private float nextPlayerSend;
    private float nextEnemySend;
    private float nextSummarySend;
    private float nextGuestAttack;
    private float nextHostAttackAnimation;
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
    private int guestExtractsSent;
    private int guestExtractsReceived;
    private int guestExtractsAccepted;
    private int guestInteractionsSent;
    private int guestInteractionsReceived;
    private int guestInteractionsAccepted;
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
                HandleHostAttackAnimationInput(now);

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
            }
            else
            {
                HandleGuestBottleSelectionInput();
                HandleGuestAttackInput(now);
                HandleGuestExtractInput();
                HandleGuestInteractionInput();
            }
        }

        if (Input.GetKeyDown(KeyCode.F10))
            ReturnToTitleScreen();
    }

    private void ApplyClientRestrictions()
    {
        clientRestrictionsApplied = true;

        SetEnabled(core.Waves, false);
        SetEnabled(core.Bosses, false);
        SetEnabled(core.CJ, false);
        SetEnabled(core.Drew, false);
        SetEnabled(core.Shop, false);
        SetEnabled(core.Progression, false);
        SetEnabled(core.Classes, false);
        SetEnabled(core.Laboratory, false);
        SetEnabled(core.Deployables, false);
        SetEnabled(core.Tree, false);
        SetEnabled(core.Bottles, false);
        SetEnabled(core.Upgrades, false);

        core.ShowMessage(
            "Joined as " + SessionLabel + " guest. The host owns the ranch, waves, rewards, and save. You can move and help attack enemies.",
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
            rotationY = player.eulerAngles.y
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

    private void HandleHostAttackAnimationInput(float now)
    {
        if (!connected ||
            now < nextHostAttackAnimation ||
            core == null ||
            core.Player == null ||
            Cursor.visible ||
            core.Settings == null ||
            core.Settings.IsOpen ||
            core.Health == null ||
            core.Health.IsDead ||
            core.GameWon)
        {
            return;
        }

        bool light =
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.Space);

        bool heavy = Input.GetKeyDown(KeyCode.Q);

        if (!light && !heavy)
            return;

        nextHostAttackAnimation = now + (heavy ? 0.45f : 0.25f);
        QueuePacket(new LanPacket
        {
            type = "attack_anim",
            heavy = heavy
        });
    }

    private void HandleGuestBottleSelectionInput()
    {
        if (!connected || core.Bottles == null)
            return;

        int direction = 0;
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            direction = -1;
        else if (Input.GetKeyDown(KeyCode.RightBracket))
            direction = 1;

        if (direction == 0)
            return;

        core.Bottles.CycleSelection(direction);

        QueuePacket(new LanPacket
        {
            type = "select_bottle",
            bottleTier = core.Bottles.SelectedTier
        });

        lastNetworkEvent =
            "Guest selected " +
            core.Bottles.GetTierName(core.Bottles.SelectedTier);
    }

    private void HandleGuestAttackInput(float now)
    {
        if (!connected || core.Player == null || Cursor.visible ||
            core.Settings == null || core.Settings.IsOpen)
            return;

        if (now < nextGuestAttack)
            return;

        bool light =
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.Space);

        bool heavy = Input.GetKeyDown(KeyCode.Q);

        if (!light && !heavy)
            return;

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

    private void HandleGuestExtractInput()
    {
        if (!connected ||
            core.Player == null ||
            core.RanchTreeTransform == null ||
            core.Settings == null ||
            core.Settings.IsOpen)
        {
            return;
        }

        bool nearTree =
            Vector3.Distance(
                core.Player.transform.position,
                core.RanchTreeTransform.position
            ) <= core.Player.InteractionRange + 0.75f;

        if (!nearTree || !Input.GetKey(KeyCode.E))
            return;

        float extractDelta = Mathf.Min(Time.deltaTime, 0.05f);
        if (extractDelta <= 0f)
            return;

        Transform player = core.Player.transform;
        QueuePacket(new LanPacket
        {
            type = "extract",
            deltaTime = extractDelta,
            x = player.position.x,
            y = player.position.y,
            z = player.position.z,
            rotationY = player.eulerAngles.y
        });

        guestExtractsSent++;
        lastNetworkEvent =
            "Guest extract sent " + guestExtractsSent;
    }

    private void HandleGuestInteractionInput()
    {
        if (!connected ||
            core.Player == null ||
            core.Settings == null ||
            core.Settings.IsOpen ||
            !Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

        RanchInteractable interactable =
            FindNearestInteractable(core.Player.transform.position);

        if (interactable == null ||
            interactable is RanchTreeInteractable)
        {
            return;
        }

        bool modifier =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        RanchStation station = interactable as RanchStation;
        if (station != null)
        {
            QueuePacket(new LanPacket
            {
                type = "station",
                stationType = (int)station.StationType,
                modifier = modifier,
                x = core.Player.transform.position.x,
                y = core.Player.transform.position.y,
                z = core.Player.transform.position.z
            });

            guestInteractionsSent++;
            lastNetworkEvent =
                "Guest station request: " + station.StationType;
            return;
        }

        RanchAreaGate areaGate = interactable as RanchAreaGate;
        if (areaGate != null)
        {
            QueuePacket(new LanPacket
            {
                type = "area_gate",
                areaIndex = areaGate.AreaIndex,
                x = core.Player.transform.position.x,
                y = core.Player.transform.position.y,
                z = core.Player.transform.position.z
            });

            guestInteractionsSent++;
            lastNetworkEvent =
                "Guest area gate request: " + areaGate.AreaIndex;
            return;
        }

        lastNetworkEvent =
            "That guest interaction is host-only for now";
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
                    hasRemotePlayerState = true;
                    EnsureRemotePlayerVisual();
                    break;

                case "attack":
                    if (RanchGameModeState.IsLanHost)
                        ProcessGuestAttack(packet);
                    break;

                case "attack_anim":
                    if (RanchGameModeState.IsLanClient)
                        PlayRemoteAttackAnimation(packet.heavy);
                    break;

                case "extract":
                    if (RanchGameModeState.IsLanHost)
                        ProcessGuestExtract(packet);
                    break;

                case "select_bottle":
                    if (RanchGameModeState.IsLanHost)
                        ProcessGuestBottleSelection(packet);
                    break;

                case "station":
                    if (RanchGameModeState.IsLanHost)
                        ProcessGuestStation(packet);
                    break;

                case "area_gate":
                    if (RanchGameModeState.IsLanHost)
                        ProcessGuestAreaGate(packet);
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
        PlayRemoteAttackAnimation(packet.heavy);
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

    private void ProcessGuestExtract(LanPacket packet)
    {
        guestExtractsReceived++;

        if (core.Tree == null || core.RanchTreeTransform == null)
        {
            lastNetworkEvent = "Guest extract received but tree is missing";
            return;
        }

        Vector3 extractOrigin = hasRemotePlayerState
            ? remotePlayerTargetPosition
            : new Vector3(packet.x, packet.y, packet.z);

        float distance = Vector3.Distance(
            extractOrigin,
            core.RanchTreeTransform.position
        );

        if (distance > 4.25f)
        {
            lastNetworkEvent =
                "Guest extract rejected: too far from tree";
            return;
        }

        float extractDelta = Mathf.Clamp(packet.deltaTime, 0f, 0.05f);
        if (extractDelta <= 0f)
            return;

        core.Tree.Extract(extractDelta);
        guestExtractsAccepted++;
        lastNetworkEvent =
            "Guest extracted ranch " + guestExtractsAccepted + "x";
    }

    private void ProcessGuestBottleSelection(LanPacket packet)
    {
        if (core.Bottles == null)
            return;

        int tier = Mathf.Clamp(
            packet.bottleTier,
            0,
            RanchBottleSystem.TierCount - 1
        );

        if (!core.Bottles.IsUnlocked(tier))
        {
            lastNetworkEvent = "Guest selected a locked bottle tier";
            return;
        }

        core.Bottles.SelectTier(tier);
        lastNetworkEvent =
            "Guest selected " + core.Bottles.GetTierName(tier);
    }

    private void ProcessGuestStation(LanPacket packet)
    {
        guestInteractionsReceived++;

        RanchStationType stationType =
            (RanchStationType)Mathf.Clamp(
                packet.stationType,
                0,
                (int)RanchStationType.Shop
            );

        Vector3 origin = hasRemotePlayerState
            ? remotePlayerTargetPosition
            : new Vector3(packet.x, packet.y, packet.z);

        RanchStation station = FindNearestStation(origin, stationType);
        if (station == null)
        {
            lastNetworkEvent =
                "Guest " + stationType + " rejected: too far";
            return;
        }

        switch (stationType)
        {
            case RanchStationType.Bottle:
                if (packet.modifier)
                    core.Bottles.BottleAllSelected();
                else
                    core.Bottles.TryBottleSelected();
                break;

            case RanchStationType.Sell:
                if (packet.modifier)
                    core.Bottles.SellAllSelected();
                else
                    core.Bottles.SellOneSelected();
                break;

            case RanchStationType.BottleUpgrade:
                core.Upgrades.BuyNextBottleTier();
                break;

            case RanchStationType.ToolUpgrade:
                core.Upgrades.BuyNextToolTier();
                break;

            case RanchStationType.Drew:
                core.Drew.HireOrUpgrade();
                break;

            case RanchStationType.CJGate:
                core.CJ.ChallengeCJ();
                break;

            case RanchStationType.Shop:
                core.ShowMessage(
                    "Guest reached the Ranch Empire Shop. Full shop menus are host-only for now.",
                    6f
                );
                break;
        }

        guestInteractionsAccepted++;
        lastNetworkEvent =
            "Guest used " + stationType +
            " (" + guestInteractionsAccepted + " accepted)";
    }

    private void ProcessGuestAreaGate(LanPacket packet)
    {
        guestInteractionsReceived++;

        int areaIndex = Mathf.Clamp(
            packet.areaIndex,
            0,
            RanchAreaSystem.AreaCount - 1
        );

        Vector3 origin = hasRemotePlayerState
            ? remotePlayerTargetPosition
            : new Vector3(packet.x, packet.y, packet.z);

        RanchAreaGate gate = FindNearestAreaGate(origin, areaIndex);
        if (gate == null)
        {
            lastNetworkEvent =
                "Guest area gate rejected: too far";
            return;
        }

        if (core.Areas.TryUnlock(areaIndex))
        {
            guestInteractionsAccepted++;
            lastNetworkEvent =
                "Guest opened area gate " + areaIndex;
        }
        else
        {
            lastNetworkEvent =
                "Guest area gate request denied by requirements";
        }
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
        hostRawRanch = packet.rawRanch;
        hostTotalRanch = packet.totalRanch;
        hostMoney = packet.money;
        hostWave = packet.wave;
        hostEnemyCount = packet.enemyCount;

        if (core.Inventory != null)
        {
            core.Inventory.RestoreState(
                packet.rawRanch,
                packet.totalRanch,
                packet.money,
                packet.bottleCounts
            );
        }

        if (core.Bottles != null)
        {
            core.Bottles.RestoreState(
                packet.unlockedBottleTier,
                packet.selectedBottleTier
            );
        }

        if (core.Upgrades != null)
            core.Upgrades.RestoreState(packet.toolTier);

        if (core.Areas != null)
        {
            core.Areas.RestoreState(new[]
            {
                packet.area0,
                packet.area1,
                packet.area2,
                packet.area3
            });
        }
    }

    private RanchInteractable FindNearestInteractable(Vector3 origin)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, 4.5f);
        RanchInteractable nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            RanchInteractable interactable =
                colliders[i].GetComponentInParent<RanchInteractable>();

            if (interactable == null)
                continue;

            float distance = Vector3.Distance(
                origin,
                interactable.transform.position
            );

            if (distance < nearestDistance)
            {
                nearest = interactable;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private RanchStation FindNearestStation(
        Vector3 origin,
        RanchStationType stationType)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, 5.25f);
        RanchStation nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            RanchStation station =
                colliders[i].GetComponentInParent<RanchStation>();

            if (station == null || station.StationType != stationType)
                continue;

            float distance = Vector3.Distance(
                origin,
                station.transform.position
            );

            if (distance < nearestDistance)
            {
                nearest = station;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private RanchAreaGate FindNearestAreaGate(
        Vector3 origin,
        int areaIndex)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, 7f);
        RanchAreaGate nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            RanchAreaGate gate =
                colliders[i].GetComponentInParent<RanchAreaGate>();

            if (gate == null || gate.AreaIndex != areaIndex)
                continue;

            float distance = Vector3.Distance(
                origin,
                gate.transform.position
            );

            if (distance < nearestDistance)
            {
                nearest = gate;
                nearestDistance = distance;
            }
        }

        return nearest;
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

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = RanchGameModeState.IsLanHost
            ? SessionLabel + " GUEST"
            : SessionLabel + " HOST";
        label.fontSize = 52;
        label.characterSize = 0.075f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.AddComponent<RanchBillboard>();

        CreateRemoteAttackArc();

        if (hasRemotePlayerState)
        {
            remotePlayer.transform.position = remotePlayerTargetPosition;
            remotePlayer.transform.rotation = remotePlayerTargetRotation;
        }
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

    private void PlayRemoteAttackAnimation(bool heavy)
    {
        EnsureRemotePlayerVisual();
        CreateRemoteAttackArc();

        remoteAttackHeavy = heavy;
        remoteAttackDuration = heavy ? 0.42f : 0.26f;
        remoteAttackTimer = remoteAttackDuration;

        if (remoteAttackArc != null)
            remoteAttackArc.SetActive(true);
    }

    private void UpdateRemoteAttackAnimation()
    {
        if (remoteAttackArc == null || remoteAttackTimer <= 0f)
            return;

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
        guestExtractsSent = 0;
        guestExtractsReceived = 0;
        guestExtractsAccepted = 0;
        guestInteractionsSent = 0;
        guestInteractionsReceived = 0;
        guestInteractionsAccepted = 0;
        lastNetworkEvent = "No multiplayer traffic yet";

        if (remotePlayer != null)
            Destroy(remotePlayer);
        remotePlayer = null;
        remoteAttackArc = null;
        remoteAttackTimer = 0f;
        hasRemotePlayerState = false;

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
        float height = RanchGameModeState.IsLanClient ? 265f : 245f;
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
                "Guest extracts: " + guestExtractsReceived +
                "  Accepted: " + guestExtractsAccepted + "\n" +
                "Guest stations: " + guestInteractionsReceived +
                "  Accepted: " + guestInteractionsAccepted + "\n" +
                "Last: " + lastNetworkEvent + "\n" +
                (transportMode == TransportMode.DirectOnline
                    ? "Forward TCP " + DefaultPort + " to this computer."
                    : transportMode == TransportMode.Relay
                    ? "No port forwarding. Relay carries the connection."
                    : "Host owns the ranch and save file.");
        }
        else
        {
            body =
                statusText + "\n" +
                "Host Ranch: " + hostRawRanch.ToString("F0") +
                "  Total: " + hostTotalRanch.ToString("F0") +
                "  $" + hostMoney.ToString("F0") + "\n" +
                "Host Wave: " + hostWave +
                "  Enemies: " + hostEnemyCount + "\n" +
                "Relay paired: " + (relayPaired ? "yes" : "no") +
                "  Packets in/out: " + packetsReceived + "/" + packetsSent + "\n" +
                "Moves sent: " + playerPacketsSent +
                "  Attacks sent: " + guestAttacksSent + "\n" +
                "Extracts sent: " + guestExtractsSent +
                "  Station uses: " + guestInteractionsSent + "\n" +
                "Guest: Hold E at tree. Press E at stations.";
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
