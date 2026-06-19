using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RanchGameCore : MonoBehaviour
{
    public static RanchGameCore Instance { get; private set; }

    public RanchInventory Inventory { get; private set; }
    public RanchBottleSystem Bottles { get; private set; }
    public RanchUpgradeSystem Upgrades { get; private set; }
    public RanchTreeSystem Tree { get; private set; }
    public RanchHealthSystem Health { get; private set; }
    public RanchWaveSystem Waves { get; private set; }
    public RanchDrewSystem Drew { get; private set; }
    public RanchShopSystem Shop { get; private set; }
    public RanchCJSystem CJ { get; private set; }

    public RanchPlayerController Player { get; private set; }
    public Transform RanchTreeTransform { get; private set; }

    public int BottlesSold { get; private set; }
    public int CJHeat { get; private set; }
    public bool GameWon { get; private set; }

    public string StatusMessage { get; private set; } = "";
    public float StatusMessageTime { get; private set; }

    public event Action ResourcesChanged;
    public event Action<string> MessageChanged;
    public event Action GameWonEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Initialize(
        RanchInventory inventory,
        RanchBottleSystem bottles,
        RanchUpgradeSystem upgrades,
        RanchTreeSystem tree,
        RanchHealthSystem health,
        RanchWaveSystem waves,
        RanchDrewSystem drew,
        RanchShopSystem shop,
        RanchCJSystem cj)
    {
        Inventory = inventory;
        Bottles = bottles;
        Upgrades = upgrades;
        Tree = tree;
        Health = health;
        Waves = waves;
        Drew = drew;
        Shop = shop;
        CJ = cj;
    }

    public void RegisterWorld(RanchPlayerController player, Transform ranchTree)
    {
        Player = player;
        RanchTreeTransform = ranchTree;
    }

    public void RegisterBottleSale(int bottleCount, int ranchUnitsSold)
    {
        BottlesSold += Mathf.Max(0, bottleCount);
        CJHeat += Mathf.Max(0, ranchUnitsSold);
        CJ.CheckProgress();
        NotifyResourcesChanged();
    }

    public void AddCJHeat(int amount)
    {
        CJHeat += Mathf.Max(0, amount);
        CJ.CheckProgress();
        NotifyResourcesChanged();
    }

    public void NotifyResourcesChanged()
    {
        ResourcesChanged?.Invoke();
    }

    public void ShowMessage(string message, float seconds = 5f)
    {
        StatusMessage = message;
        StatusMessageTime = Mathf.Max(0f, seconds);
        MessageChanged?.Invoke(message);
        Debug.Log(message);
    }

    public void WinGame()
    {
        if (GameWon) return;
        GameWon = true;
        ShowMessage("You defeated CJ, the Ultimate Ranchenator.", 999f);
        GameWonEvent?.Invoke();
        if (Player != null) Player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void Update()
    {
        if (StatusMessageTime > 0f)
            StatusMessageTime -= Time.unscaledDeltaTime;

        if ((GameWon || (Health != null && Health.IsDead)) && Input.GetKeyDown(KeyCode.R))
            RestartScene();
    }
}
