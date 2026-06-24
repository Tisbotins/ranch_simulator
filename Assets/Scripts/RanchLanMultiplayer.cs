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
/// A dependency-free two-player LAN experiment.
///
/// The host remains authoritative for the ranch, waves, enemies, rewards,
/// and save file. The guest can move around, see the host and host enemies,
/// and make basic networked melee attacks against those enemies.
///
/// This is intentionally a learning prototype rather than production
/// matchmaking/netcode. It uses one TCP connection on the local network.
/// </summary>
[DefaultExecutionOrder(-850)]
[DisallowMultipleComponent]
public class RanchLanMultiplayer : MonoBehaviour
{
    public const int DefaultPort = 7777;

    public bool IsConnected => connected;
    public string StatusText => statusText;
    public string LocalAddress => localAddress;
    public int Port => DefaultPort;

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
        public int[] bottleCounts;
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

    private GameObject remotePlayer;
    private Vector3 remotePlayerTargetPosition;
    private Quaternion remotePlayerTargetRotation = Quaternion.identity;
    private bool hasRemotePlayerState;
    private bool clientRestrictionsApplied;

    private float nextPlayerSend;
    private float nextEnemySend;
    private float nextSummarySend;
    private float nextGuestAttack;
    private float lastHostAcceptedAttack;
    private int enemyFrame;

    private float hostRawRanch;
    private float hostTotalRanch;
    private float hostMoney;
    private int hostWave;
    private int hostEnemyCount;

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
        if (core == null)
            return false;

        ShutdownConnection(false);
        RanchGameModeState.SetMode(RanchGameMode.LanHost);

        stopRequested = false;
        connected = false;
        statusText =
            "Hosting on " + localAddress + ":" + DefaultPort +
            " — waiting for one guest";

        listenerThread = new Thread(HostAcceptLoop)
        {
            IsBackground = true,
            Name = "Ranch LAN Host Listener"
        };
        listenerThread.Start();
        return true;
    }

    public bool StartClient(string hostAddress)
    {
        if (core == null)
            return false;

        string cleaned = string.IsNullOrWhiteSpace(hostAddress)
            ? ""
            : hostAddress.Trim();

        if (cleaned.Length == 0)
        {
            statusText = "Enter the host laptop's IPv4 address.";
            return false;
        }

        ShutdownConnection(false);
        RanchGameModeState.SetMode(RanchGameMode.LanClient);

        stopRequested = false;
        connected = false;
        statusText =
            "Connecting to " + cleaned + ":" + DefaultPort + "...";

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
            () => ClientConnectLoop(cleaned)
        )
        {
            IsBackground = true,
            Name = "Ranch LAN Client Connector"
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
            }
            else
            {
                HandleGuestAttackInput(now);
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
        SetEnabled(core.Combat, false);
        SetEnabled(core.Deployables, false);
        SetEnabled(core.Tree, false);
        SetEnabled(core.Bottles, false);
        SetEnabled(core.Upgrades, false);

        core.ShowMessage(
            "Joined as LAN guest. The host owns the ranch, waves, rewards, and save. You can move and help attack enemies.",
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
            area0 = areas != null && areas.Length > 0 && areas[0],
            area1 = areas != null && areas.Length > 1 && areas[1],
            area2 = areas != null && areas.Length > 2 && areas[2],
            area3 = areas != null && areas.Length > 3 && areas[3]
        });
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

        QueuePacket(new LanPacket
        {
            type = "attack",
            heavy = heavy,
            x = player.position.x,
            y = player.position.y,
            z = player.position.z,
            rotationY = player.eulerAngles.y
        });
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
        float now = Time.unscaledTime;
        float requiredDelay = packet.heavy ? 1.0f : 0.42f;

        if (now - lastHostAcceptedAttack < requiredDelay)
            return;

        lastHostAcceptedAttack = now;

        if (core.Waves == null)
            return;

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
            nearest.TakeDamage(damage, false, 0f);
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

    private void EnsureRemotePlayerVisual()
    {
        if (remotePlayer != null)
            return;

        remotePlayer = new GameObject(
            RanchGameModeState.IsLanHost
                ? "LAN Guest Player"
                : "LAN Host Player"
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
            ? "LAN GUEST"
            : "LAN HOST";
        label.fontSize = 52;
        label.characterSize = 0.075f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.AddComponent<RanchBillboard>();

        if (hasRemotePlayerState)
        {
            remotePlayer.transform.position = remotePlayerTargetPosition;
            remotePlayer.transform.rotation = remotePlayerTargetRotation;
        }
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
            "LAN Enemy " + packet.id + " — " + packet.name
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
            ConfigureConnection(accepted);
            statusText = "LAN guest connected";
        }
        catch (Exception exception)
        {
            if (!stopRequested)
                statusText = "Host failed: " + exception.Message;
        }
    }

    private void ClientConnectLoop(string hostAddress)
    {
        try
        {
            TcpClient connectingClient = new TcpClient();
            connectingClient.NoDelay = true;
            connectingClient.Connect(hostAddress, DefaultPort);

            if (stopRequested)
            {
                connectingClient.Close();
                return;
            }

            ConfigureConnection(connectingClient);
            statusText = "Connected to LAN host " + hostAddress;
        }
        catch (Exception exception)
        {
            if (!stopRequested)
                statusText = "Connection failed: " + exception.Message;
        }
    }

    private void ConfigureConnection(TcpClient connection)
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
        }

        connected = true;

        receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "Ranch LAN Receiver"
        };

        sendThread = new Thread(SendLoop)
        {
            IsBackground = true,
            Name = "Ranch LAN Sender"
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

                if (line.Length > 0)
                    incoming.Enqueue(line);
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

        if (!stopRequested)
            statusText = "LAN player disconnected. Press F10 for title screen.";
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

        if (remotePlayer != null)
            Destroy(remotePlayer);
        remotePlayer = null;
        hasRemotePlayerState = false;

        foreach (RemoteEnemyVisual visual in remoteEnemies.Values)
        {
            if (visual.Root != null)
                Destroy(visual.Root);
        }
        remoteEnemies.Clear();

        if (updateStatus)
            statusText = "LAN multiplayer stopped";
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
        float height = RanchGameModeState.IsLanClient ? 190f : 145f;
        Rect panel = new Rect(
            Screen.width - width - 18f,
            18f,
            width,
            height
        );

        GUI.Box(panel, GUIContent.none, panelStyle);

        string heading = RanchGameModeState.IsLanHost
            ? "LAN HOST"
            : "LAN GUEST";

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
                "Address: " + localAddress + ":" + DefaultPort + "\n" +
                "Host owns the ranch and save file.";
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
                "Guest attacks: Left Click/Space, heavy: Q";
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
}
