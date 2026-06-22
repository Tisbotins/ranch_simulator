using UnityEngine;

public class RanchBossSystem : MonoBehaviour
{
    public bool BossAlive { get; private set; }
    public string CurrentBossName { get; private set; } = "";

    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public bool IsBossWave(int wave)
    {
        return wave > 0 && wave % 5 == 0;
    }

    public void ConfigureBoss(RanchEnemy enemy, int wave)
    {
        if (enemy == null) return;
        CurrentBossName = GetBossName(wave);
        BossAlive = true;

        float cycle = Mathf.Max(1f, wave / 5f);
        enemy.MakeBoss(CurrentBossName, 3.6f + cycle * 0.35f, 1.35f + cycle * 0.07f);
        core.ShowMessage($"BOSS WAVE: {CurrentBossName} has entered the Ranch!", 8f);
    }

    public string GetBossName(int wave)
    {
        // Wave 15 is a dedicated boss round.
        if (wave == 15)
            return "The Ranch Jockey";

        int index = Mathf.Max(0, wave / 5 - 1) % 5;
        switch (index)
        {
            case 0: return "The Bottle Baron";
            case 1: return "The Cream Colossus";
            case 2: return "The Ranch Auditor";
            case 3: return "CJ's Regional Manager";
            default: return "Prototype Ranchenator";
        }
    }

    public void NotifyBossDefeated(RanchEnemy boss, int wave)
    {
        BossAlive = false;
        float money = 350f + wave * 90f;
        float ranch = 75f + wave * 12f;
        core.Inventory.AddMoney(money);
        core.Inventory.AddRawRanch(ranch);
        core.Progression.AddExperience(175f + wave * 15f, "Boss defeated");
        core.AddCJHeat(75 + wave * 4);
        core.ShowMessage($"BOSS DEFEATED: {CurrentBossName}! Bonus: ${money:F0}, {ranch:F0} Ranch, and major XP.", 9f);
        CurrentBossName = "";
        core.Save.RequestSave();
    }

    public void ResetBossState()
    {
        BossAlive = false;
        CurrentBossName = "";
    }
}
