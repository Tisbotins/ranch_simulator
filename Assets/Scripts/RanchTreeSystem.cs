using UnityEngine;

public class RanchTreeSystem : MonoBehaviour
{
    private readonly float[] thresholds = { 0f, 200f, 600f, 1500f, 3500f };
    private readonly string[] names =
    {
        "Ranch Sapling", "Young Ranch Tree", "Mature Ranch Tree",
        "Ancient Ranch Tree", "Colossal Ranch Tree"
    };
    private readonly float[] scales = { 0.55f, 0.8f, 1.05f, 1.35f, 1.8f };

    public float LifetimeRanchExtracted { get; private set; }
    public int Stage { get; private set; }
    public string CurrentStageName => names[Stage];
    public float BaseRanchPerSecond = 1.2f;

    private RanchGameCore core;
    private Transform visualRoot;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    public void RegisterTreeVisual(Transform treeVisual)
    {
        visualRoot = treeVisual;
        ApplyVisual();
    }

    public void Extract(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // Ranch Surge and Ranch Fever both feed extraction, so active play pays
        // off at the tree as well as in combat.
        float momentumMultiplier = core != null && core.Momentum != null
            ? core.Momentum.ExtractionMultiplier
            : 1f;

        float stageBonus = 1f + Stage * 0.18f;
        float amount = BaseRanchPerSecond * core.Upgrades.ExtractionMultiplier *
                       core.Shop.ExtractionResearchMultiplier * core.Progression.ProductionMultiplier *
                       momentumMultiplier * stageBonus *
                       // Off-world trees pay far more, and the Quantum
                       // Harvester multiplies everything.
                       (core.Space != null
                           ? core.Space.WorldExtractionMultiplier *
                             core.Space.CosmicExtractionMultiplier
                           : 1f) * deltaTime;

        core.Inventory.AddRawRanch(amount);
        LifetimeRanchExtracted += amount;
        EmitExtractionFeedback(amount, deltaTime);

        int oldStage = Stage;
        Stage = CalculateStage();
        if (Stage != oldStage)
        {
            ApplyVisual();
            core.ShowMessage($"The Ranch Tree evolved into a {CurrentStageName}. Stronger Ranch Raiders can now appear.");
        }
    }

    // Extraction is a held action running every frame, so feedback is batched
    // into readable chunks instead of spamming a popup per frame.
    private float feedbackAccumulator;
    private float feedbackTimer;

    private void EmitExtractionFeedback(float amount, float deltaTime)
    {
        if (visualRoot == null)
            return;

        feedbackAccumulator += amount;
        feedbackTimer += deltaTime;

        if (feedbackTimer < 0.45f)
            return;

        Vector3 position = visualRoot.position + Vector3.up * 2.2f;
        bool surging = core.Momentum != null && core.Momentum.SurgeActive;

        RanchJuiceSystem.Popup(
            position,
            "+" + feedbackAccumulator.ToString("F1") + (surging ? "  SURGE!" : ""),
            surging ? new Color(1f, 0.85f, 0.3f) : new Color(0.75f, 0.95f, 1f),
            surging ? 26f : 20f
        );

        RanchJuiceSystem.Sparkle(
            position,
            surging ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.95f, 1f),
            surging ? 5 : 2
        );

        feedbackAccumulator = 0f;
        feedbackTimer = 0f;
    }

    public float GetNextStageThreshold() => Stage >= thresholds.Length - 1 ? -1f : thresholds[Stage + 1];

    private int CalculateStage()
    {
        int result = 0;
        for (int i = 0; i < thresholds.Length; i++)
            if (LifetimeRanchExtracted >= thresholds[i]) result = i;
        return result;
    }

    public void RestoreState(float lifetimeRanchExtracted)
    {
        LifetimeRanchExtracted = Mathf.Max(0f, lifetimeRanchExtracted);
        Stage = CalculateStage();
        ApplyVisual();
        core.NotifyResourcesChanged();
    }

    private void ApplyVisual()
    {
        if (visualRoot != null) visualRoot.localScale = Vector3.one * scales[Stage];
    }
}
