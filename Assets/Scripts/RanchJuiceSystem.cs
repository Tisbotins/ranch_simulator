using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only "game feel" layer: floating combat text, screen shake,
/// sparkle bursts, and a day/night cycle.
///
/// Nothing here changes gameplay state, so it is safe for every other system to
/// call into it through the static helpers without being wired up first. Every
/// helper is a no-op when the system is absent, which keeps the callers free of
/// null checks.
/// </summary>
[DefaultExecutionOrder(-810)]
[DisallowMultipleComponent]
public class RanchJuiceSystem : MonoBehaviour
{
    private const int MaxPopups = 48;
    private const int MaxSparkles = 120;
    private const float DayLengthSeconds = 300f;

    // Named FloatingText rather than Popup: C# forbids a nested type and a
    // member sharing a name, and the public API is RanchJuiceSystem.Popup(...).
    private sealed class FloatingText
    {
        public Vector3 WorldPosition;
        public string Text;
        public Color Color;
        public float Size;
        public float Life;
        public float MaxLife;
    }

    // Named Mote for the same reason: the public API is
    // RanchJuiceSystem.Sparkle(...).
    private sealed class Mote
    {
        public Transform Transform;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
    }

    private static RanchJuiceSystem active;

    /// <summary>0 = dawn, 0.5 = dusk, wrapping once per in-game day.</summary>
    public float DayFraction { get; private set; }
    public bool IsNight => DayFraction > 0.5f;

    public string TimeOfDayName
    {
        get
        {
            if (DayFraction < 0.08f) return "Dawn";
            if (DayFraction < 0.42f) return "Day";
            if (DayFraction < 0.5f) return "Dusk";
            if (DayFraction < 0.92f) return "Night";
            return "Dawn";
        }
    }

    private RanchGameCore core;
    private readonly List<FloatingText> popups = new List<FloatingText>();
    private readonly List<Mote> sparkles = new List<Mote>();
    private Transform sparkleRoot;

    private Light sunLight;
    private Light fillLight;
    private Camera trackedCamera;
    private float shakeStrength;
    private float shakeRemaining;
    private float shakeDuration;
    private Vector3 appliedShakeOffset;

    private GUIStyle popupStyle;
    private GUIStyle shadowStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        active = this;

        GameObject root = new GameObject("Ranch Juice Effects");
        sparkleRoot = root.transform;

        EnsureSunLight();
        DayFraction = 0.15f;
    }

    private void OnDestroy()
    {
        if (active == this)
            active = null;
    }

    // ---------------------------------------------------------------- statics

    /// <summary>Floating world-space text, e.g. damage or a ranch gain.</summary>
    public static void Popup(Vector3 worldPosition, string text, Color color, float size = 22f)
    {
        if (active == null || string.IsNullOrEmpty(text))
            return;

        active.AddPopup(worldPosition, text, color, size);
    }

    /// <summary>Camera shake. Strength is in world units.</summary>
    public static void Shake(float strength, float duration = 0.25f)
    {
        if (active == null)
            return;

        active.AddShake(strength, duration);
    }

    /// <summary>A short burst of rising, fading motes.</summary>
    public static void Sparkle(Vector3 worldPosition, Color color, int count = 6)
    {
        if (active == null)
            return;

        active.AddSparkles(worldPosition, color, count);
    }

    public static bool IsNightTime => active != null && active.IsNight;

    // ---------------------------------------------------------------- updates

    private void Update()
    {
        if (core == null)
            return;

        float delta = Time.unscaledDeltaTime;

        AdvanceDayCycle(delta);
        UpdatePopups(delta);
        UpdateSparkles(delta);
    }

    private void LateUpdate()
    {
        // Shake is applied after the player controller has positioned the camera,
        // otherwise the controller would overwrite the offset every frame.
        //
        // The previous frame's offset is always subtracted first. The controller
        // stops repositioning the camera while the player is dead, downed, or in
        // a menu, so a purely additive shake would accumulate and permanently
        // drift the camera off the player.
        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        if (appliedShakeOffset != Vector3.zero)
        {
            cam.transform.position -= appliedShakeOffset;
            appliedShakeOffset = Vector3.zero;
        }

        if (shakeRemaining <= 0f)
            return;

        shakeRemaining -= Time.unscaledDeltaTime;
        if (shakeRemaining <= 0f)
            return;

        float falloff = shakeDuration <= 0f
            ? 0f
            : Mathf.Clamp01(shakeRemaining / shakeDuration);

        float amount = shakeStrength * falloff;
        appliedShakeOffset = new Vector3(
            Random.Range(-amount, amount),
            Random.Range(-amount, amount),
            Random.Range(-amount, amount)
        );

        cam.transform.position += appliedShakeOffset;
    }

    private void AdvanceDayCycle(float delta)
    {
        DayFraction += delta / Mathf.Max(1f, DayLengthSeconds);
        if (DayFraction >= 1f)
            DayFraction -= 1f;

        // The Cosmic Journey owns the sky once it starts: each planet sets its
        // own ambient/background, so the day cycle must not fight it.
        if (core.Space != null && core.Space.JourneyUnlocked)
            return;

        EnsureSunLight();

        float sunAngle = DayFraction * 360f - 90f;
        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, 35f, 0f);

            float daylight = Mathf.Clamp01(Mathf.Cos((DayFraction - 0.25f) * Mathf.PI * 2f));
            sunLight.intensity = Mathf.Lerp(0.18f, 1.15f, daylight);
            sunLight.color = Color.Lerp(
                new Color(0.55f, 0.60f, 0.95f),
                new Color(1f, 0.96f, 0.85f),
                daylight
            );

            // Dawn and dusk warm the sky; midday is clear blue, night deep navy.
            Color daySky = new Color(0.42f, 0.66f, 0.92f);
            Color nightSky = new Color(0.03f, 0.04f, 0.11f);
            Color duskSky = new Color(0.85f, 0.45f, 0.28f);

            // Peaks at the horizon crossings (dawn/dusk), zero at noon/midnight.
            float horizon = 1f - Mathf.Abs(daylight * 2f - 1f);
            float goldenHour = Mathf.Pow(Mathf.Clamp01(horizon), 2.5f);

            Color sky = Color.Lerp(nightSky, daySky, daylight);
            sky = Color.Lerp(sky, duskSky, goldenHour * 0.65f);

            Color ground = new Color(0.20f, 0.17f, 0.12f);
            RanchVisuals.ApplyAtmosphere(sky, ground, daylight);

            Camera cam = ResolveCamera();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = sky;
            }
        }
    }

    private void EnsureSunLight()
    {
        if (sunLight != null)
            return;

        // The scene's configured sun, when there is one, costs nothing to read.
        if (RenderSettings.sun != null &&
            RenderSettings.sun.type == LightType.Directional)
        {
            sunLight = RenderSettings.sun;
            return;
        }

        // Otherwise adopt the first active directional light in the scene.
        // Unity 6 deprecates the single-argument FindObjectsByType overload, so
        // the inactive filter is passed explicitly. Exclude matches the old
        // behaviour: an inactive light could not light the ranch anyway.
        Light[] lights = FindObjectsByType<Light>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
            {
                sunLight = lights[i];
                return;
            }
        }

        GameObject sun = new GameObject("Ranch Sun");
        sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.shadows = LightShadows.Soft;
        sunLight.shadowStrength = 0.72f;
        sunLight.shadowBias = 0.03f;
        sunLight.shadowNormalBias = 0.35f;

        // Registering the sun keeps later lookups free and lets Unity's ambient
        // lighting track it.
        RenderSettings.sun = sunLight;

        RanchVisuals.ApplyShadowSettings();
        EnsureFillLight();
    }

    // A dim, shadowless light aimed from the opposite side. Without it the
    // unlit faces of every primitive fall to near-black and the world reads as
    // flat silhouettes.
    private void EnsureFillLight()
    {
        if (fillLight != null)
            return;

        GameObject fill = new GameObject("Ranch Fill Light");
        fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.shadows = LightShadows.None;
        fillLight.intensity = 0.28f;
        fillLight.color = new Color(0.62f, 0.72f, 0.95f);
        fill.transform.rotation = Quaternion.Euler(28f, 215f, 0f);
    }

    private Camera ResolveCamera()
    {
        if (trackedCamera == null)
            trackedCamera = Camera.main;

        return trackedCamera;
    }

    private void AddPopup(Vector3 worldPosition, string text, Color color, float size)
    {
        if (popups.Count >= MaxPopups)
            popups.RemoveAt(0);

        popups.Add(new FloatingText
        {
            WorldPosition = worldPosition + Vector3.up * 1.4f,
            Text = text,
            Color = color,
            Size = size,
            Life = 1.1f,
            MaxLife = 1.1f
        });
    }

    private void UpdatePopups(float delta)
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            popups[i].Life -= delta;
            popups[i].WorldPosition += Vector3.up * delta * 1.15f;

            if (popups[i].Life <= 0f)
                popups.RemoveAt(i);
        }
    }

    private void AddShake(float strength, float duration)
    {
        // Keep the strongest active shake rather than letting them stack into a
        // seizure-inducing amount.
        if (strength <= shakeStrength && shakeRemaining > 0f)
            return;

        shakeStrength = Mathf.Min(strength, 0.6f);
        shakeDuration = Mathf.Max(0.05f, duration);
        shakeRemaining = shakeDuration;
    }

    private void AddSparkles(Vector3 worldPosition, Color color, int count)
    {
        count = Mathf.Clamp(count, 1, 12);

        for (int i = 0; i < count; i++)
        {
            if (sparkles.Count >= MaxSparkles)
                break;

            GameObject mote = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mote.name = "Ranch Mote";
            mote.transform.SetParent(sparkleRoot, false);
            mote.transform.position = worldPosition + Random.insideUnitSphere * 0.35f;
            mote.transform.localScale = Vector3.one * Random.Range(0.06f, 0.14f);

            Collider collider = mote.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = mote.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = GetCachedMaterial(color);

            sparkles.Add(new Mote
            {
                Transform = mote.transform,
                Velocity = new Vector3(
                    Random.Range(-0.9f, 0.9f),
                    Random.Range(1.1f, 2.4f),
                    Random.Range(-0.9f, 0.9f)
                ),
                Life = 0.75f,
                MaxLife = 0.75f
            });
        }
    }

    // RanchWorldBuilder.CreateRuntimeMaterial allocates a fresh Material (and
    // runs Shader.Find) on every call. Sparkles spawn constantly, so they share
    // cached materials instead of leaking one per mote.
    private readonly Dictionary<int, Material> sparkleMaterials =
        new Dictionary<int, Material>();

    private Material GetCachedMaterial(Color color)
    {
        int key = (color.r * 255f).GetHashCode() ^
                  ((color.g * 255f).GetHashCode() << 8) ^
                  ((color.b * 255f).GetHashCode() << 16);

        if (sparkleMaterials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material created = RanchWorldBuilder.CreateRuntimeMaterial(color);
        sparkleMaterials[key] = created;
        return created;
    }

    private void UpdateSparkles(float delta)
    {
        for (int i = sparkles.Count - 1; i >= 0; i--)
        {
            Mote sparkle = sparkles[i];

            if (sparkle.Transform == null)
            {
                sparkles.RemoveAt(i);
                continue;
            }

            sparkle.Life -= delta;
            if (sparkle.Life <= 0f)
            {
                Destroy(sparkle.Transform.gameObject);
                sparkles.RemoveAt(i);
                continue;
            }

            sparkle.Velocity += Vector3.down * 2.2f * delta;
            sparkle.Transform.position += sparkle.Velocity * delta;

            float shrink = Mathf.Clamp01(sparkle.Life / sparkle.MaxLife);
            sparkle.Transform.localScale = Vector3.one * (0.14f * shrink);
        }
    }

    // ------------------------------------------------------------------- draw

    private void OnGUI()
    {
        if (core == null || popups.Count == 0 || core.IsAnyMenuOpen)
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        EnsureStyles();

        for (int i = 0; i < popups.Count; i++)
        {
            FloatingText popup = popups[i];

            Vector3 screen = cam.WorldToScreenPoint(popup.WorldPosition);
            if (screen.z <= 0f)
                continue;

            float fade = Mathf.Clamp01(popup.Life / popup.MaxLife);

            popupStyle.fontSize = Mathf.RoundToInt(popup.Size);
            popupStyle.normal.textColor = new Color(
                popup.Color.r,
                popup.Color.g,
                popup.Color.b,
                fade
            );

            Rect rect = new Rect(screen.x - 110f, Screen.height - screen.y - 20f, 220f, 40f);

            // Cheap drop shadow so text stays readable against bright ground.
            // Styles are reused rather than allocated per popup per OnGUI pass,
            // which would otherwise churn the GC every frame.
            shadowStyle.fontSize = popupStyle.fontSize;
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, fade * 0.6f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), popup.Text, shadowStyle);

            GUI.Label(rect, popup.Text, popupStyle);
        }
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        popupStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        shadowStyle = new GUIStyle(popupStyle);

        stylesReady = true;
    }
}
