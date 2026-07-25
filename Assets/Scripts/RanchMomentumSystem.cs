using UnityEngine;

/// <summary>
/// Moment-to-moment gameplay mechanics that reward active play:
///
/// * RANCH FEVER — chaining kills builds stacks that boost damage, enemy
///   rewards, and extraction. Stacks decay if you stop fighting and are cut
///   when you take a serious hit, so the fantasy is "stay aggressive".
/// * RANCH SURGE — a periodic timed window where the Ranch Tree gushes. It
///   pulls the player off the combat treadmill and back to the tree.
/// * NIGHT SHIFT — at night enemies pay out more, pairing with the day/night
///   cycle in RanchJuiceSystem so the world's look and its rules agree.
///
/// All state here is intentionally transient (never saved): it is about the
/// current fight, not long-term progress.
/// </summary>
[DefaultExecutionOrder(-805)]
[DisallowMultipleComponent]
public class RanchMomentumSystem : MonoBehaviour
{
    public const int MaxFeverStacks = 20;

    private const float FeverDecaySeconds = 7f;
    private const float FeverPerStack = 0.075f;
    private const float SurgeDuration = 22f;
    private const float SurgeMinInterval = 95f;
    private const float SurgeMaxInterval = 165f;
    private const float SurgeExtractionBonus = 2.5f;
    private const float NightRewardBonus = 0.30f;

    public int FeverStacks { get; private set; }
    public bool SurgeActive { get; private set; }
    public float SurgeRemaining { get; private set; }

    /// <summary>Multiplier applied to weapon damage from Ranch Fever.</summary>
    public float DamageMultiplier => 1f + FeverStacks * FeverPerStack;

    /// <summary>Multiplier applied to money/loot from defeated enemies.</summary>
    public float RewardMultiplier =>
        (1f + FeverStacks * FeverPerStack) *
        (RanchJuiceSystem.IsNightTime ? 1f + NightRewardBonus : 1f);

    /// <summary>Multiplier applied to Ranch Tree extraction.</summary>
    public float ExtractionMultiplier =>
        (1f + FeverStacks * FeverPerStack * 0.5f) *
        (SurgeActive ? SurgeExtractionBonus : 1f);

    public string FeverTitle
    {
        get
        {
            if (FeverStacks >= 16) return "RANCHENATOR";
            if (FeverStacks >= 11) return "UNSTOPPABLE";
            if (FeverStacks >= 7) return "BLAZING";
            if (FeverStacks >= 3) return "HEATING UP";
            return "WARM";
        }
    }

    private RanchGameCore core;
    private float feverTimer;
    private float nextSurgeTime;

    private Texture2D barTexture;
    private Texture2D barBackTexture;
    private GUIStyle labelStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        ScheduleNextSurge();
    }

    private void ScheduleNextSurge()
    {
        nextSurgeTime =
            Time.unscaledTime + Random.Range(SurgeMinInterval, SurgeMaxInterval);
    }

    private void Update()
    {
        if (core == null || core.IsAnyMenuOpen)
            return;

        float delta = Time.deltaTime;

        UpdateFever(delta);
        UpdateSurge(delta);
    }

    private void UpdateFever(float delta)
    {
        if (FeverStacks <= 0)
            return;

        feverTimer -= delta;
        if (feverTimer > 0f)
            return;

        // Bleed a stack at a time rather than dropping to zero, so a short lull
        // does not erase a long chain.
        FeverStacks--;
        feverTimer = FeverDecaySeconds * 0.35f;
    }

    private void UpdateSurge(float delta)
    {
        if (SurgeActive)
        {
            SurgeRemaining -= delta;
            if (SurgeRemaining <= 0f)
            {
                SurgeActive = false;
                SurgeRemaining = 0f;
                ScheduleNextSurge();
                core.ShowMessage("The Ranch Surge fades.", 3f);
            }

            return;
        }

        // Surges only make sense once the player actually has a tree to use and
        // is not in the middle of the final battle.
        if (Time.unscaledTime < nextSurgeTime)
            return;

        if (core.CJ != null && core.CJ.FinalBattleActive)
        {
            ScheduleNextSurge();
            return;
        }

        BeginSurge();
    }

    private void BeginSurge()
    {
        SurgeActive = true;
        SurgeRemaining = SurgeDuration;

        core.ShowMessage(
            "RANCH SURGE! The Ranch Tree is gushing — extract now for " +
            SurgeExtractionBonus.ToString("0.#") + "x Ranch!",
            6f
        );

        if (core.RanchTreeTransform != null)
        {
            RanchJuiceSystem.Sparkle(
                core.RanchTreeTransform.position + Vector3.up * 3f,
                new Color(1f, 0.9f, 0.35f),
                12
            );
        }

        RanchJuiceSystem.Shake(0.12f, 0.4f);
    }

    /// <summary>Called when the player defeats an enemy.</summary>
    public void RegisterKill(Vector3 position, bool boss)
    {
        int gain = boss ? 4 : 1;
        FeverStacks = Mathf.Min(MaxFeverStacks, FeverStacks + gain);
        feverTimer = FeverDecaySeconds;

        if (FeverStacks >= 3)
        {
            RanchJuiceSystem.Popup(
                position,
                FeverTitle + "  x" + DamageMultiplier.ToString("0.00"),
                new Color(1f, 0.62f, 0.18f),
                20f
            );
        }
    }

    /// <summary>Called when the player takes a real hit — momentum breaks.</summary>
    public void RegisterPlayerHit()
    {
        if (FeverStacks <= 0)
            return;

        FeverStacks = Mathf.Max(0, FeverStacks / 2);
        feverTimer = FeverDecaySeconds;
    }

    public void ResetMomentum()
    {
        FeverStacks = 0;
        feverTimer = 0f;
        SurgeActive = false;
        SurgeRemaining = 0f;
        ScheduleNextSurge();
    }

    private void OnGUI()
    {
        if (core == null || core.IsAnyMenuOpen)
            return;

        if (core.Settings != null && !core.Settings.HudVisible)
            return;

        if (FeverStacks <= 0 && !SurgeActive)
            return;

        EnsureStyles();

        float width = 260f;
        float x = (Screen.width - width) * 0.5f;
        float y = 12f;

        if (SurgeActive)
        {
            GUI.Label(
                new Rect(x, y, width, 24f),
                "RANCH SURGE  " + Mathf.CeilToInt(SurgeRemaining) + "s",
                labelStyle
            );
            y += 26f;
        }

        if (FeverStacks > 0)
        {
            GUI.Label(
                new Rect(x, y, width, 24f),
                FeverTitle + "  x" + DamageMultiplier.ToString("0.00"),
                labelStyle
            );

            Rect bar = new Rect(x + 30f, y + 24f, width - 60f, 8f);
            GUI.DrawTexture(bar, barBackTexture);
            GUI.DrawTexture(
                new Rect(
                    bar.x,
                    bar.y,
                    bar.width * Mathf.Clamp01((float)FeverStacks / MaxFeverStacks),
                    bar.height
                ),
                barTexture
            );
        }
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        barTexture = MakeTexture(new Color(1f, 0.55f, 0.15f, 0.95f));
        barBackTexture = MakeTexture(new Color(0f, 0f, 0f, 0.45f));

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle.normal.textColor = new Color(1f, 0.78f, 0.35f);

        stylesReady = true;
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
