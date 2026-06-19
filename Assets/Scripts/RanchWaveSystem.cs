using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RanchWaveSystem : MonoBehaviour
{
    public enum WaveState { WaitingForTree, Intermission, Spawning, Fighting }

    public int CurrentWave { get; private set; }
    public WaveState CurrentState { get; private set; }
    public float FirstWaveDelay = 18f;
    public float IntermissionDuration = 30f;
    public float EnemySpawnDelay = 0.75f;
    public int MaximumEnemiesPerWave = 30;

    public float SecondsUntilNextWave => CurrentState == WaveState.Intermission ? Mathf.Max(0f, timer) : 0f;
    public int EnemiesRemaining => activeEnemies.Count +
        (CurrentState == WaveState.Spawning ? Mathf.Max(0, enemiesInWave - enemiesSpawned) : 0);
    public List<RanchEnemy> ActiveEnemies => activeEnemies;

    private readonly List<RanchEnemy> activeEnemies = new List<RanchEnemy>();
    private RanchGameCore core;
    private Transform tree;
    private Transform container;
    private Transform player;
    private int enemiesInWave;
    private int enemiesSpawned;
    private int serial;
    private float timer;
    private float spawnTimer;
    private int lastAnnounced = -1;
    private string lastReward = "";

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        CurrentState = WaveState.WaitingForTree;
    }

    public void RegisterWorld(Transform ranchTree, Transform enemyContainer, Transform playerTarget)
    {
        tree = ranchTree;
        container = enemyContainer;
        player = playerTarget;
    }

    private void Update()
    {
        if (core == null || core.GameWon || core.Health.IsDead || core.Shop.IsOpen) return;
        activeEnemies.RemoveAll(enemy => enemy == null);

        if (CurrentState == WaveState.WaitingForTree)
        {
            if (core.Tree.Stage >= 1)
            {
                BeginIntermission(FirstWaveDelay);
                core.ShowMessage($"The Ranch Tree attracted Raiders. Wave 1 begins in {Mathf.CeilToInt(FirstWaveDelay)} seconds.");
            }
            return;
        }

        if (CurrentState == WaveState.Intermission) UpdateIntermission();
        else if (CurrentState == WaveState.Spawning) UpdateSpawning();
        else if (CurrentState == WaveState.Fighting && activeEnemies.Count == 0) CompleteWave();
    }

    public void AttackNearestEnemy(float damage, float range, bool showMiss)
    {
        RanchEnemy nearest = null;
        float nearestDistance = range;
        foreach (RanchEnemy enemy in activeEnemies)
        {
            if (enemy == null) continue;
            float distance = enemy.DistanceToPlayer;
            if (distance <= nearestDistance) { nearest = enemy; nearestDistance = distance; }
        }
        if (nearest != null) nearest.TakeDamage(damage);
        else if (showMiss) core.ShowMessage("No Ranch Raider is within sword range.");
    }

    public void DamageNearestFromDefense(float damage, float range)
    {
        RanchEnemy nearest = null;
        float nearestDistance = range;
        foreach (RanchEnemy enemy in activeEnemies)
        {
            if (enemy == null || tree == null) continue;
            float distance = Vector3.Distance(tree.position, enemy.transform.position);
            if (distance <= nearestDistance) { nearest = enemy; nearestDistance = distance; }
        }
        if (nearest != null) nearest.TakeDamage(damage);
    }

    public void NotifyEnemyDefeated(RanchEnemy enemy) => activeEnemies.Remove(enemy);

    public string GetBannerText()
    {
        if (CurrentState == WaveState.WaitingForTree) return "RAIDER WAVES LOCKED";
        if (CurrentState == WaveState.Intermission) return $"WAVE {CurrentWave + 1} IN {Mathf.CeilToInt(SecondsUntilNextWave)}s";
        return $"WAVE {CurrentWave} — {EnemiesRemaining} RAIDERS REMAINING";
    }

    public string GetStatusText()
    {
        StringBuilder text = new StringBuilder();
        if (CurrentState == WaveState.WaitingForTree)
        {
            text.AppendLine("Waves locked\n");
            text.Append("Grow the Ranch Tree into a Young Ranch Tree to begin enemy waves.");
        }
        else if (CurrentState == WaveState.Intermission)
        {
            int next = CurrentWave + 1;
            text.AppendLine($"Next wave: {next}");
            text.AppendLine($"Begins in: {FormatTime(timer)}");
            text.AppendLine($"Expected Raiders: {CalculateWaveSize(next)}");
            text.AppendLine($"Expected types: {GetPreview(next)}");
            if (!string.IsNullOrEmpty(lastReward)) text.AppendLine("\n" + lastReward);
        }
        else
        {
            text.AppendLine($"Wave {CurrentWave} ACTIVE");
            text.AppendLine($"Enemies remaining: {EnemiesRemaining}");
            text.AppendLine($"Spawned: {enemiesSpawned} / {enemiesInWave}");
            text.AppendLine($"Types: {GetPreview(CurrentWave)}");
            if (CurrentWave % 5 == 0) text.Append("\nELITE WAVE");
        }
        return text.ToString();
    }

    private void UpdateIntermission()
    {
        timer -= Time.deltaTime;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timer));
        if (seconds != lastAnnounced)
        {
            lastAnnounced = seconds;
            if (seconds == 10 || (seconds >= 1 && seconds <= 5))
                core.ShowMessage($"Ranch Raider Wave {CurrentWave + 1} begins in {seconds} second{(seconds == 1 ? "" : "s")}.");
        }
        if (timer <= 0f) StartWave();
    }

    private void UpdateSpawning()
    {
        spawnTimer -= Time.deltaTime;
        if (enemiesSpawned < enemiesInWave && spawnTimer <= 0f)
        {
            SpawnEnemy(enemiesSpawned);
            enemiesSpawned++;
            spawnTimer = EnemySpawnDelay;
        }
        if (enemiesSpawned >= enemiesInWave) CurrentState = WaveState.Fighting;
        if (enemiesSpawned >= enemiesInWave && activeEnemies.Count == 0) CompleteWave();
    }

    private void BeginIntermission(float duration)
    {
        CurrentState = WaveState.Intermission;
        timer = Mathf.Max(1f, duration);
        lastAnnounced = -1;
    }

    private void StartWave()
    {
        CurrentWave++;
        enemiesInWave = CalculateWaveSize(CurrentWave);
        enemiesSpawned = 0;
        spawnTimer = 0f;
        CurrentState = WaveState.Spawning;
        core.ShowMessage($"WAVE {CurrentWave} STARTED — {enemiesInWave} Raiders incoming. Types: {GetPreview(CurrentWave)}");
    }

    private void CompleteWave()
    {
        float money = 50f * CurrentWave * (1f + core.Tree.Stage * 0.35f);
        float ranch = 8f * CurrentWave * (1f + core.Tree.Stage * 0.2f);
        core.Inventory.AddMoney(money);
        core.Inventory.AddRawRanch(ranch);
        lastReward = $"Last reward: ${money:F0} and {ranch:F0} raw Ranch.";
        core.ShowMessage($"WAVE {CurrentWave} CLEARED. {lastReward}");
        BeginIntermission(IntermissionDuration);
    }

    private int CalculateWaveSize(int wave)
    {
        int size = 2 + wave + Mathf.Clamp(core.Tree.Stage, 1, 4);
        if (wave % 5 == 0) size += 3;
        return Mathf.Clamp(size, 3, MaximumEnemiesPerWave);
    }

    private void SpawnEnemy(int index)
    {
        if (tree == null || player == null) return;
        serial++;
        int tier = ChooseTier(CurrentWave, index);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(17f, 23f);
        Vector3 position = tree.position + new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);

        GameObject enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyObject.name = $"Wave {CurrentWave} {RanchEnemy.GetEnemyName(tier)} {serial}";
        enemyObject.transform.position = position;
        if (container != null) enemyObject.transform.SetParent(container);
        enemyObject.GetComponent<Renderer>().material = RanchWorldBuilder.CreateRuntimeMaterial(GetTierColor(tier));

        RanchEnemy enemy = enemyObject.AddComponent<RanchEnemy>();
        enemy.Initialize(core, this, player, tier, serial, CurrentWave);
        activeEnemies.Add(enemy);
    }

    private int ChooseTier(int wave, int index)
    {
        int maximum = Mathf.Min(Mathf.Clamp(core.Tree.Stage, 0, 4), Mathf.Clamp((wave + 1) / 3, 0, 4));
        if (wave % 5 == 0 && index == enemiesInWave - 1) return maximum;
        return Random.Range(Mathf.Max(0, maximum - 1), maximum + 1);
    }

    private string GetPreview(int wave)
    {
        int maximum = Mathf.Min(Mathf.Clamp(core.Tree.Stage, 0, 4), Mathf.Clamp((wave + 1) / 3, 0, 4));
        int minimum = Mathf.Max(0, maximum - 1);
        return minimum == maximum ? RanchEnemy.GetEnemyName(maximum) :
            $"{RanchEnemy.GetEnemyName(minimum)} / {RanchEnemy.GetEnemyName(maximum)}";
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static Color GetTierColor(int tier)
    {
        switch (tier)
        {
            case 0: return new Color(0.62f, 0.46f, 0.22f);
            case 1: return new Color(0.72f, 0.30f, 0.18f);
            case 2: return new Color(0.55f, 0.20f, 0.50f);
            case 3: return new Color(0.25f, 0.05f, 0.05f);
            default: return new Color(0.025f, 0.025f, 0.025f);
        }
    }
}
