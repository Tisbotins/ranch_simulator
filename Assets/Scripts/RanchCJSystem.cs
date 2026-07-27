using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class RanchCJSystem : MonoBehaviour
{
    public const int RequiredWaves = 30;

    public bool HasWarned { get; private set; }
    public int MilestoneIndex { get; private set; }
    public bool FinalBattleActive { get; private set; }
    public bool FinalBattleCompleted { get; private set; }
    public int CurrentPhase { get; private set; }
    public RanchEnemy FinalBoss { get; private set; }

    public int WarningHeat = 250;
    public float ThreatMultiplier => 1f + MilestoneIndex * 0.08f;

    public bool BattleUnlocked =>
        core != null &&
        core.Waves != null &&
        core.Waves.HighestWaveCleared >= RequiredWaves;

    public int WavesRemaining =>
        core == null || core.Waves == null
            ? RequiredWaves
            : Mathf.Max(0, RequiredWaves - core.Waves.HighestWaveCleared);

    public float FinalBossHealthPercent =>
        FinalBoss == null || FinalBoss.MaxHealth <= 0f
            ? 0f
            : Mathf.Clamp01(FinalBoss.Health / FinalBoss.MaxHealth);

    private readonly int[] milestones = { 250, 500, 900, 1500, 2500, 4000 };

    private RanchGameCore core;
    private Transform gateTransform;
    private TextMesh gateLabel;
    private Transform arenaRoot;
    private Transform playerStart;
    private Transform bossSpawn;
    private float labelRefreshTimer;
    private bool gateReadyAnnounced;
    private Coroutine battleRoutine;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void RegisterGateVisual(Transform gate, TextMesh label)
    {
        gateTransform = gate;
        gateLabel = label;
        RefreshGateLabel();
    }

    public void RegisterArena(Transform arena, Transform playerMarker, Transform bossMarker)
    {
        arenaRoot = arena;
        playerStart = playerMarker;
        bossSpawn = bossMarker;
    }

    private void Update()
    {
        if (core == null)
            return;

        labelRefreshTimer -= Time.unscaledDeltaTime;
        if (labelRefreshTimer <= 0f)
        {
            labelRefreshTimer = 0.25f;
            RefreshGateLabel();
        }

        if (BattleUnlocked && !gateReadyAnnounced && !core.GameWon)
        {
            gateReadyAnnounced = true;
            core.ShowMessage(
                "THIRTY WAVES CLEARED — The CJ Gate is ready. Enter it to begin the final boss battle.",
                10f
            );
        }
    }

    public void CheckProgress()
    {
        if (core == null)
            return;

        int reached = 0;
        for (int i = 0; i < milestones.Length; i++)
        {
            if (core.CJHeat >= milestones[i])
                reached = i + 1;
        }

        while (MilestoneIndex < reached)
        {
            MilestoneIndex++;
            AnnounceMilestone(MilestoneIndex);
        }

        HasWarned = core.CJHeat >= WarningHeat || HasWarned;
        RefreshGateLabel();
    }

    public void NotifyWaveCleared(int wave)
    {
        if (wave < RequiredWaves)
            return;

        gateReadyAnnounced = true;
        RefreshGateLabel();

        core.ShowMessage(
            "CAMPAIGN COMPLETE: Wave 30 cleared. The CJ Gate is glowing — the final battle is ready.",
            12f
        );
    }

    private void AnnounceMilestone(int milestone)
    {
        switch (milestone)
        {
            case 1:
                core.ShowMessage("CJ: Cute little Ranch operation you've got there.", 7f);
                break;
            case 2:
                core.ShowMessage("CJ has authorized stronger Raider equipment.", 7f);
                break;
            case 3:
                core.ShowMessage("CJ has placed your Ranch under corporate surveillance.", 7f);
                break;
            case 4:
                core.ShowMessage("CJ has marked your empire as hostile. The gate still requires 30 cleared waves.", 8f);
                break;
            case 5:
                core.ShowMessage("CJ has declared your Ranch Empire a direct threat.", 7f);
                break;
            case 6:
                core.ShowMessage("MAXIMUM CJ HEAT: The Ranchenator war has begun.", 8f);
                break;
        }
    }

    public string GetHeatStatus()
    {
        if (MilestoneIndex <= 0) return "Ignored";
        if (MilestoneIndex == 1) return "Noticed";
        if (MilestoneIndex == 2) return "Monitored";
        if (MilestoneIndex == 3) return "Targeted";
        if (MilestoneIndex == 4) return "Hostile";
        if (MilestoneIndex == 5) return "Empire Threat";
        return "Ranchenator War";
    }

    public string GetGatePrompt()
    {
        if (core == null)
            return "CJ Gate unavailable";

        if (core.GameWon)
            return "CJ defeated — press R to begin a new game";

        if (FinalBattleCompleted)
            return "CJ defeated — press J for the Ranch Rocket";

        if (FinalBattleActive)
            return "CJ FINAL BATTLE IN PROGRESS";

        if (!BattleUnlocked)
            return $"CJ Gate locked: {core.Waves.HighestWaveCleared}/{RequiredWaves} waves cleared";

        return "Press E: Enter the CJ Arena — FINAL BOSS";
    }

    public string GetGateStatusText()
    {
        if (core == null || core.Waves == null)
            return "Gate unavailable";

        if (core.GameWon)
            return "CJ DEFEATED\n\nPress R to erase the completed save and begin a new game.";

        if (FinalBattleCompleted)
            return "CJ DEFEATED\n\nA cosmic rift opened above the ranch.\nPress J to open the Ranch Rocket console and begin the Cosmic Journey.";

        if (FinalBattleActive)
        {
            if (FinalBoss == null)
                return "FINAL BATTLE STARTING\n\nCJ is entering the arena.";

            return
                $"FINAL BATTLE ACTIVE\nPhase: {CurrentPhase}/3\n" +
                $"CJ Health: {FinalBoss.Health:F0}/{FinalBoss.MaxHealth:F0}\n\n" +
                "Defeat CJ to finish the game.";
        }

        if (!BattleUnlocked)
        {
            return
                $"LOCKED — CLEAR 30 WAVES\nProgress: {core.Waves.HighestWaveCleared}/{RequiredWaves}\n" +
                $"Waves remaining: {WavesRemaining}\n\n" +
                "Wave 30 must be completed before CJ can be challenged.";
        }

        return
            "READY — FINAL BOSS AVAILABLE\nProgress: 30/30 waves\n\n" +
            "Travel to the CJ Gate and press E to enter the boss arena.";
    }

    public string GetGateStatusShort()
    {
        if (core == null || core.Waves == null)
            return "Unavailable";

        if (core.GameWon || FinalBattleCompleted)
            return "CJ defeated";

        if (FinalBattleActive)
            return $"Final battle — Phase {Mathf.Max(1, CurrentPhase)}/3";

        if (BattleUnlocked)
            return "READY — 30/30 waves";

        return $"LOCKED — {core.Waves.HighestWaveCleared}/{RequiredWaves} waves";
    }

    public void ChallengeCJ()
    {
        if (core == null || core.GameWon)
            return;

        if (FinalBattleActive)
        {
            core.ShowMessage("CJ is already fighting you in the arena.");
            return;
        }

        if (!BattleUnlocked)
        {
            core.ShowMessage(
                $"The CJ Gate is locked. Clear {WavesRemaining} more wave{(WavesRemaining == 1 ? "" : "s")} to reach 30.",
                7f
            );
            return;
        }

        if (arenaRoot == null || playerStart == null || bossSpawn == null || core.Player == null)
        {
            core.ShowMessage("The CJ arena was not created correctly. Check the Unity Console.", 8f);
            Debug.LogError("CJ battle could not start because the arena or player registration is missing.");
            return;
        }

        // A confrontation before the fight, but only here at the gate. CJ's
        // taunts during the battle stay as on-screen messages on purpose —
        // dialogue pauses the game, and freezing a boss fight to read a modal
        // box would wreck it.
        if (core.Dialogue != null)
        {
            core.Dialogue.BeginWithChoices(
                "CJ",
                new List<RanchDialogueSystem.Choice>
                {
                    new RanchDialogueSystem.Choice
                    {
                        Text = "Open the gate.",
                        OnChosen = StartFinalBattle
                    },
                    new RanchDialogueSystem.Choice
                    {
                        Text = "Not yet.",
                        OnChosen = null
                    }
                },
                "So the little ranch hand finally walks up to my gate.",
                "Thirty waves. I sent every one of them, and you kept bottling. I'll admit that's more spine than I expected.",
                "Come through and I'll take the whole operation back myself. Last chance to walk away."
            );
            return;
        }

        StartFinalBattle();
    }

    private void StartFinalBattle()
    {
        if (battleRoutine != null)
            StopCoroutine(battleRoutine);

        battleRoutine = StartCoroutine(BeginFinalBattle());
    }

    private IEnumerator BeginFinalBattle()
    {
        FinalBattleActive = true;
        FinalBattleCompleted = false;
        CurrentPhase = 0;
        FinalBoss = null;

        core.Save.SaveGame(false);
        core.Waves.PrepareForFinalBattle();
        core.Player.Teleport(playerStart.position, playerStart.eulerAngles.y);
        core.Health.Heal(core.Health.MaxHealth);
        core.Stamina.RestoreFull();
        RefreshGateLabel();

        core.ShowMessage("CJ: Thirty waves, and you still think this Ranch belongs to you?", 4f);
        yield return new WaitForSeconds(2.4f);

        core.ShowMessage("Drew: This is it. Use everything we learned.", 4f);
        yield return new WaitForSeconds(2.2f);

        core.ShowMessage("FINAL BOSS — CJ, THE ULTIMATE RANCHENATOR", 5f);
        SpawnFinalBoss();
        battleRoutine = null;
    }

    private void SpawnFinalBoss()
    {
        GameObject bossObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bossObject.name = "CJ, the Ultimate Ranchenator";
        bossObject.transform.position = bossSpawn.position;
        bossObject.transform.rotation = bossSpawn.rotation;

        // Use the custom CJ character model if one has been added, exactly like
        // the Player (PlayerModel.prefab) and Drew (DrewModel.prefab). The capsule
        // stays as the invisible gameplay body (collider + RanchEnemy); only its
        // mesh is hidden when a real model is present. With no prefab, CJ falls
        // back to the primitive look below.
        if (!TryLoadCJModel(bossObject))
        {
            bossObject.GetComponent<Renderer>().material =
                RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.92f, 0.66f, 0.08f));
            CreateBossCrown(bossObject.transform);
        }

        RanchEnemy boss = bossObject.AddComponent<RanchEnemy>();
        boss.Initialize(core, core.Waves, core.Player.transform, 4, 9999, RequiredWaves);
        boss.MakeFinalCJ(this);

        FinalBoss = boss;
        CurrentPhase = 1;
        core.Waves.RegisterFinalBattleEnemy(boss);
        RefreshGateLabel();
    }

    // Loads Assets/Resources/Prefabs/CJModel.prefab onto the boss body if it
    // exists. Returns true when a custom model was attached. Mirrors the Drew
    // loader in RanchWorldBuilder: imported cameras/lights/listeners/colliders
    // are stripped so they cannot hijack the game camera, lighting, or hitboxes.
    private bool TryLoadCJModel(GameObject bossObject)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/CJModel");
        if (prefab == null)
        {
            Debug.LogWarning(
                "CJModel.prefab was not found. Expected path: " +
                "Assets/Resources/Prefabs/CJModel.prefab. " +
                "Using the primitive CJ until a model is added."
            );
            return false;
        }

        GameObject model = Instantiate(prefab);
        model.name = "CJ Character Model";
        model.transform.SetParent(bossObject.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
            Destroy(importedCamera);
        foreach (AudioListener importedListener in model.GetComponentsInChildren<AudioListener>(true))
            Destroy(importedListener);
        foreach (Light importedLight in model.GetComponentsInChildren<Light>(true))
            Destroy(importedLight);
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
            Destroy(importedCollider);
        foreach (Transform importedObject in model.GetComponentsInChildren<Transform>(true))
            importedObject.gameObject.tag = "Untagged";

        MeshRenderer capsuleRenderer = bossObject.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null)
            Destroy(capsuleRenderer);
        MeshFilter capsuleFilter = bossObject.GetComponent<MeshFilter>();
        if (capsuleFilter != null)
            Destroy(capsuleFilter);

        Debug.Log("CJ custom model loaded from Resources/Prefabs/CJModel.");
        return true;
    }

    private void CreateBossCrown(Transform parent)
    {
        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        crown.name = "CJ Golden Crown";
        crown.transform.SetParent(parent, false);
        crown.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        crown.transform.localScale = new Vector3(0.72f, 0.14f, 0.72f);
        crown.GetComponent<Renderer>().material =
            RanchWorldBuilder.CreateRuntimeMaterial(new Color(1f, 0.82f, 0.12f));

        Collider collider = crown.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    public void NotifyBossPhaseChanged(int phase)
    {
        if (!FinalBattleActive || phase <= CurrentPhase)
            return;

        CurrentPhase = Mathf.Clamp(phase, 1, 3);

        if (CurrentPhase == 2)
        {
            core.ShowMessage("CJ: Enough. Regional enforcement — enter the arena! PHASE 2", 7f);
            SummonGuards(2);
        }
        else if (CurrentPhase == 3)
        {
            core.ShowMessage("CJ: INITIATING ULTIMATE RANCHENATOR PROTOCOL. FINAL PHASE!", 8f);
            SummonGuards(3);
        }
    }

    public void SummonGuards(int count)
    {
        if (!FinalBattleActive || FinalBoss == null)
            return;

        for (int i = 0; i < count; i++)
            core.Waves.SpawnFinalBattleGuard(FinalBoss.transform.position, i);
    }

    public void NotifyCJDefeated(RanchEnemy boss)
    {
        if (!FinalBattleActive)
            return;

        FinalBoss = null;
        FinalBattleActive = false;
        FinalBattleCompleted = true;
        CurrentPhase = 3;
        core.Waves.EndFinalBattle();
        RefreshGateLabel();

        if (battleRoutine != null)
            StopCoroutine(battleRoutine);

        battleRoutine = StartCoroutine(FinishVictorySequence());
    }

    private IEnumerator FinishVictorySequence()
    {
        core.Inventory.AddMoney(250000f);
        core.Inventory.AddRawRanch(5000f);
        core.Progression.AddExperience(1500f, "Defeated CJ");
        core.ShowMessage("CJ: Impossible... you became the Ultimate Ranchenator.", 5f);
        yield return new WaitForSeconds(2.5f);
        core.WinGame();
        battleRoutine = null;
    }

    public void RestoreState(bool hasWarned, bool ignoredOldBattleUnlocked, int milestoneIndex)
    {
        HasWarned = hasWarned;
        MilestoneIndex = Mathf.Clamp(milestoneIndex, 0, milestones.Length);
        FinalBattleActive = false;
        FinalBattleCompleted = false;
        CurrentPhase = 0;
        FinalBoss = null;
        gateReadyAnnounced = false;
        RefreshGateLabel();
    }

    private void RefreshGateLabel()
    {
        if (gateLabel == null)
            return;

        if (core == null || core.Waves == null)
        {
            gateLabel.text = "CJ GATE\nINITIALIZING";
            return;
        }

        if (core.GameWon || FinalBattleCompleted)
        {
            gateLabel.text = core.GameWon
                ? "CJ DEFEATED\nPRESS R FOR NEW GAME"
                : "CJ DEFEATED\nPRESS J — COSMIC JOURNEY";
            gateLabel.color = Color.green;
        }
        else if (FinalBattleActive)
        {
            gateLabel.text = "CJ FINAL BATTLE\nIN PROGRESS";
            gateLabel.color = Color.red;
        }
        else if (BattleUnlocked)
        {
            gateLabel.text = "CJ GATE READY\n30 / 30 WAVES\nPRESS E";
            gateLabel.color = new Color(1f, 0.75f, 0.1f);
        }
        else
        {
            gateLabel.text =
                $"CJ GATE LOCKED\n{core.Waves.HighestWaveCleared} / {RequiredWaves} WAVES";
            gateLabel.color = Color.white;
        }
    }
}
