using UnityEngine;

public class RanchDrewSystem : MonoBehaviour
{
    public int Level { get; private set; }
    public bool IsHired => Level > 0;

    private RanchGameCore core;
    private GameObject visual;
    private Transform treeTarget;
    private Transform bottleTarget;
    private bool movingToTree = true;
    private float timer;
    private float interval = 6f;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    public void RegisterVisual(GameObject drewVisual, Transform tree, Transform bottleStation)
    {
        visual = drewVisual;
        treeTarget = tree;
        bottleTarget = bottleStation;
        if (visual != null) visual.SetActive(IsHired);
    }

    public float GetUpgradeCost() => !IsHired ? 100f : 100f + Level * Level * 85f;

    public void HireOrUpgrade()
    {
        if (Level >= 10) { core.ShowMessage("Drew is already max level."); return; }
        float cost = GetUpgradeCost();
        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage($"Need ${cost:F0} to {(IsHired ? "upgrade" : "hire")} Drew.");
            return;
        }

        Level++;
        if (Level == 1)
        {
            if (visual != null) visual.SetActive(true);
            core.ShowMessage("Drew hired. He looks confused but enthusiastic.");
        }
        else
        {
            interval = Mathf.Max(1.2f, interval - 0.45f);
            core.ShowMessage($"Drew upgraded to Level {Level}.");
        }
        core.Progression.AddExperience(20f + Level * 6f, "Drew upgrade");
        core.Save.RequestSave();
        core.NotifyResourcesChanged();
    }

    public void RestoreState(int level)
    {
        Level = Mathf.Clamp(level, 0, 10);
        interval = Mathf.Max(1.2f, 6f - Mathf.Max(0, Level - 1) * 0.45f);
        if (visual != null) visual.SetActive(IsHired);
        core.NotifyResourcesChanged();
    }

    private void Update()
    {
        if (!IsHired || core == null || core.GameWon || core.Health.IsDead || core.Shop.IsOpen) return;
        MoveDrew();
        timer += Time.deltaTime;
        if (timer >= interval) { timer = 0f; Work(); }
    }

    private void MoveDrew()
    {
        if (visual == null || treeTarget == null || bottleTarget == null) return;
        Transform target = movingToTree ? treeTarget : bottleTarget;
        Vector3 destination = target.position;
        destination.y = visual.transform.position.y;
        visual.transform.position = Vector3.MoveTowards(visual.transform.position, destination,
            (1.8f + Level * 0.15f) * Time.deltaTime);
        Vector3 direction = destination - visual.transform.position;
        if (direction.sqrMagnitude > 0.01f) visual.transform.rotation = Quaternion.LookRotation(direction);
        if (Vector3.Distance(visual.transform.position, destination) < 0.45f) movingToTree = !movingToTree;
    }

    private void Work()
    {
        float extracted = Level * 0.8f;
        core.Inventory.AddRawRanch(extracted);
        int attempts = Mathf.Max(1, Level / 2);
        int made = 0;
        for (int i = 0; i < attempts; i++)
            if (core.Bottles.TryBottleSelected(false)) made++;
        if (made > 0) core.ShowMessage($"Drew extracted {extracted:F1} Ranch and filled {made} bottle(s).");
    }
}
