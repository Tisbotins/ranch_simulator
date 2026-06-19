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

        float stageBonus = 1f + Stage * 0.18f;
        float amount = BaseRanchPerSecond * core.Upgrades.ExtractionMultiplier *
                       core.Shop.ExtractionResearchMultiplier * stageBonus * deltaTime;

        core.Inventory.AddRawRanch(amount);
        LifetimeRanchExtracted += amount;

        int oldStage = Stage;
        Stage = CalculateStage();
        if (Stage != oldStage)
        {
            ApplyVisual();
            core.ShowMessage($"The Ranch Tree evolved into a {CurrentStageName}. Stronger Ranch Raiders can now appear.");
        }
    }

    public float GetNextStageThreshold() => Stage >= thresholds.Length - 1 ? -1f : thresholds[Stage + 1];

    private int CalculateStage()
    {
        int result = 0;
        for (int i = 0; i < thresholds.Length; i++)
            if (LifetimeRanchExtracted >= thresholds[i]) result = i;
        return result;
    }

    private void ApplyVisual()
    {
        if (visualRoot != null) visualRoot.localScale = Vector3.one * scales[Stage];
    }
}
