using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central art-direction authority for the whole game.
///
/// The world is built from Unity primitives, so how those primitives are shaded
/// matters far more than their geometry. Previously every surface used the
/// Standard shader with only its colour set, which left the entire game at
/// Unity's default 0.5 smoothness — the flat, plasticky look. This class gives
/// each surface a deliberate finish (matte ground, rough stone, glossy metal,
/// glowing ranch) and sets up fog and gradient ambient light so the world reads
/// with depth instead of as flat blocks.
///
/// It also generates the UI textures (rounded, gradient, bordered panels) so the
/// interface stops being solid single-pixel rectangles.
///
/// Everything is generated at runtime — no imported art assets required.
/// </summary>
public static class RanchVisuals
{
    /// <summary>How a surface responds to light.</summary>
    public enum Surface
    {
        /// <summary>Ground, cloth, dirt — no highlight.</summary>
        Matte,
        /// <summary>Stone, bark, wood — a faint broad highlight.</summary>
        Rough,
        /// <summary>Painted props and structures.</summary>
        Satin,
        /// <summary>Glass, liquid ranch, polished tile.</summary>
        Glossy,
        /// <summary>Blades, armour, machinery.</summary>
        Metal,
        /// <summary>Self-lit: ranch glow, cosmic energy, UI beacons.</summary>
        Emissive,
        /// <summary>Leaves and crops — matte with a touch of scatter.</summary>
        Foliage
    }

    // Standard (built-in RP) and URP/Lit spell these differently; both are set
    // behind HasProperty guards so either pipeline works.
    private const string SmoothnessStandard = "_Glossiness";
    private const string SmoothnessUrp = "_Smoothness";
    private const string MetallicProperty = "_Metallic";
    private const string EmissionProperty = "_EmissionColor";

    private static readonly Dictionary<int, Material> materialCache =
        new Dictionary<int, Material>();

    private static Shader cachedShader;

    // ---------------------------------------------------------------- shading

    private static Shader ResolveShader()
    {
        if (cachedShader != null)
            return cachedShader;

        cachedShader = Shader.Find("Standard");
        if (cachedShader == null)
            cachedShader = Shader.Find("Universal Render Pipeline/Lit");
        if (cachedShader == null)
            cachedShader = Shader.Find("Sprites/Default");

        return cachedShader;
    }

    /// <summary>
    /// Returns a shared material for this colour and finish. Materials are
    /// cached: the world builder creates thousands of primitives, and a fresh
    /// Material (plus Shader.Find) per primitive is both slow and leaky.
    /// </summary>
    public static Material GetMaterial(Color color, Surface surface)
    {
        int key = ColorKey(color) * 31 + (int)surface;

        if (materialCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material material = CreateUnique(color, surface);
        materialCache[key] = material;
        return material;
    }

    /// <summary>
    /// An unshared material the caller is free to mutate. Anything that changes
    /// blend mode, render queue, or keywords after creation MUST use this —
    /// mutating a cached material would corrupt every object sharing it.
    /// </summary>
    public static Material CreateUnique(Color color, Surface surface)
    {
        Material material = new Material(ResolveShader());
        material.color = color;

        float smoothness;
        float metallic = 0f;

        switch (surface)
        {
            case Surface.Matte:
                smoothness = 0.05f;
                break;
            case Surface.Rough:
                smoothness = 0.18f;
                break;
            case Surface.Satin:
                smoothness = 0.38f;
                break;
            case Surface.Glossy:
                smoothness = 0.78f;
                break;
            case Surface.Metal:
                smoothness = 0.62f;
                metallic = 0.85f;
                break;
            case Surface.Emissive:
                smoothness = 0.30f;
                break;
            default: // Foliage
                smoothness = 0.12f;
                break;
        }

        if (material.HasProperty(SmoothnessStandard))
            material.SetFloat(SmoothnessStandard, smoothness);
        if (material.HasProperty(SmoothnessUrp))
            material.SetFloat(SmoothnessUrp, smoothness);
        if (material.HasProperty(MetallicProperty))
            material.SetFloat(MetallicProperty, metallic);

        if (surface == Surface.Emissive && material.HasProperty(EmissionProperty))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionProperty, color * 1.15f);
            material.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        return material;
    }

    /// <summary>
    /// Picks a sensible finish from the object's name so the thousands of
    /// existing CreateRuntimeMaterial calls gain proper shading without every
    /// call site having to be rewritten.
    /// </summary>
    public static Surface InferSurface(Color color)
    {
        // Bright saturated colours read as painted props; dark desaturated ones
        // as ground and stone.
        Color.RGBToHSV(color, out _, out float saturation, out float value);

        if (saturation < 0.18f && value < 0.5f)
            return Surface.Rough;

        if (saturation < 0.12f && value > 0.85f)
            return Surface.Glossy;

        return Surface.Satin;
    }

    private static int ColorKey(Color color)
    {
        int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
        int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
        int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
        int a = Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f);
        return (r << 24) | (g << 16) | (b << 8) | a;
    }

    // ------------------------------------------------------------- atmosphere

    /// <summary>
    /// Fog and gradient ambient light. Fog is what stops a flat primitive world
    /// from looking like floating boxes — distant geometry sinks into the sky
    /// colour and the scene gains depth.
    /// </summary>
    public static void ApplyAtmosphere(Color skyColor, Color groundColor, float daylight)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = skyColor;
        // Tuned for this world's scale: the unlockable areas sit out at
        // x = 35 / 80 / 130, so the fog has to add depth without swallowing
        // them. At 0.0035 a point 100 units away is only ~12% fogged; heavier
        // values (0.01+) grey out the far districts entirely.
        RenderSettings.fogDensity = Mathf.Lerp(0.0065f, 0.0035f, daylight);

        // A three-band ambient gradient (sky / horizon / bounce) is far more
        // natural than a single flat ambient colour.
        RenderSettings.ambientMode =
            UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyColor * Mathf.Lerp(0.55f, 1.15f, daylight);
        RenderSettings.ambientEquatorColor =
            Color.Lerp(skyColor, groundColor, 0.5f) * Mathf.Lerp(0.45f, 0.95f, daylight);
        RenderSettings.ambientGroundColor = groundColor * Mathf.Lerp(0.30f, 0.70f, daylight);
    }

    /// <summary>Shadow quality that suits a small, close-up world.</summary>
    public static void ApplyShadowSettings()
    {
        QualitySettings.shadowDistance = 90f;
        QualitySettings.shadowCascades = 2;
    }

    // ---------------------------------------------------------- UI  textures

    /// <summary>
    /// A rounded, vertically-shaded panel with a soft top highlight and a
    /// darker base, plus a subtle border. Built for IMGUI 9-slice: pair it with
    /// <see cref="PanelBorder"/> so the corners keep their radius when the panel
    /// is stretched to any size.
    /// </summary>
    public static Texture2D CreatePanelTexture(
        Color topColor,
        Color bottomColor,
        Color borderColor,
        int radius = 12)
    {
        radius = Mathf.Clamp(radius, 2, 40);

        // 9-slice: radius corners on each side plus a 2px stretchable middle.
        int size = radius * 2 + 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float verticalT = size <= 1 ? 0f : 1f - (float)y / (size - 1);
            Color fill = Color.Lerp(topColor, bottomColor, verticalT);

            for (int x = 0; x < size; x++)
            {
                float coverage = RoundedCoverage(x, y, size, radius);

                if (coverage <= 0f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                // Edge band becomes the border colour.
                float edge = RoundedCoverage(x, y, size, radius, 1.35f);
                Color pixel = edge < 1f
                    ? Color.Lerp(borderColor, fill, edge)
                    : fill;

                pixel.a *= coverage;
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return texture;
    }

    /// <summary>The 9-slice border to use with <see cref="CreatePanelTexture"/>.</summary>
    public static RectOffset PanelBorder(int radius = 12)
    {
        radius = Mathf.Clamp(radius, 2, 40);
        return new RectOffset(radius, radius, radius, radius);
    }

    // Signed-distance coverage for a rounded rectangle, giving anti-aliased
    // corners instead of the hard stair-stepping of a plain mask.
    private static float RoundedCoverage(
        int x,
        int y,
        int size,
        int radius,
        float inset = 0f)
    {
        float half = size * 0.5f;
        float px = x + 0.5f - half;
        float py = y + 0.5f - half;

        float extent = half - radius;
        float dx = Mathf.Max(Mathf.Abs(px) - extent, 0f);
        float dy = Mathf.Max(Mathf.Abs(py) - extent, 0f);
        float distance = Mathf.Sqrt(dx * dx + dy * dy);

        return Mathf.Clamp01(radius - inset - distance + 0.5f);
    }

    /// <summary>A flat 1x1 texture, for bars and simple fills.</summary>
    public static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// A horizontal gradient fill, used for progress and resource bars so they
    /// read as lit rather than as blocks of colour.
    /// </summary>
    public static Texture2D CreateBarTexture(Color left, Color right)
    {
        const int width = 64;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1);
            // Slight vertical-looking sheen by brightening the middle.
            float sheen = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
            Color color = Color.Lerp(left, right, t) * sheen;
            color.a = Mathf.Lerp(left.a, right.a, t);
            texture.SetPixel(x, 0, color);
        }

        texture.Apply();
        return texture;
    }

    // ------------------------------------------------------------ typography

    // Fonts are created from the operating system rather than shipped as
    // assets, so no font files (or their licences) need to live in the repo.
    // Each role lists fallbacks in priority order — Unity walks the list and
    // uses the first family the machine actually has, so Windows, macOS, and
    // Linux each land on something sensible.

    private static readonly string[] DisplayFamilies =
    {
        // Windows                     macOS                    Linux
        "Impact", "Haettenschweiler", "Arial Black",
        "Franklin Gothic Heavy", "Futura", "Gill Sans",
        "Helvetica Neue", "DejaVu Sans", "Arial", "Helvetica"
    };

    private static readonly string[] BodyFamilies =
    {
        "Segoe UI", "SF Pro Text", "Helvetica Neue", "Avenir Next", "Avenir",
        "Roboto", "Noto Sans", "DejaVu Sans", "Verdana", "Tahoma",
        "Arial", "Helvetica"
    };

    // Monospaced so changing digits keep a constant width. Without this a HUD
    // value ticking 9 -> 10 shifts everything after it and the numbers jitter.
    private static readonly string[] NumericFamilies =
    {
        "Consolas", "SF Mono", "Menlo", "Monaco", "DejaVu Sans Mono",
        "Roboto Mono", "Liberation Mono", "Courier New", "Courier"
    };

    private static string[] installedFamilies;

    private static Font displayFont;
    private static Font bodyFont;
    private static Font numericFont;
    private static bool fontsResolved;

    /// <summary>Heavy poster face for titles and headings.</summary>
    public static Font DisplayFont { get { EnsureFonts(); return displayFont; } }

    /// <summary>Clean humanist face for descriptions and menus.</summary>
    public static Font BodyFont { get { EnsureFonts(); return bodyFont; } }

    /// <summary>Monospaced face for HUD numbers and stat readouts.</summary>
    public static Font NumericFont { get { EnsureFonts(); return numericFont; } }

    private static void EnsureFonts()
    {
        if (fontsResolved)
            return;

        fontsResolved = true;
        displayFont = ResolveFont(DisplayFamilies);
        bodyFont = ResolveFont(BodyFamilies);
        numericFont = ResolveFont(NumericFamilies);

        // Which families won is machine-dependent, so state it once. "built-in"
        // means none of the chain was installed and IMGUI's default is used.
        Debug.Log(
            "Ranch fonts — display: " + DescribeFont(displayFont) +
            ", body: " + DescribeFont(bodyFont) +
            ", numeric: " + DescribeFont(numericFont)
        );
    }

    private static string DescribeFont(Font font)
    {
        return font != null ? font.name : "built-in";
    }

    // Only families the machine actually has are requested.
    // Font.CreateDynamicFontFromOSFont does NOT skip missing families — asking
    // for a font that is not installed logs "Unable to load font face for [X]"
    // and falls back anyway (e.g. Segoe UI is Windows-only, so a macOS build
    // warns on every launch). Checking the installed list first keeps the
    // console clean and makes the fallback chain actually work.
    //
    // A null result is safe and meaningful: IMGUI falls back to the built-in
    // font, so a machine with none of these families still renders correctly.
    private static Font ResolveFont(string[] families)
    {
        if (installedFamilies == null)
        {
            try
            {
                installedFamilies = Font.GetOSInstalledFontNames();
            }
            catch
            {
                // Some platforms disallow font enumeration entirely.
            }

            if (installedFamilies == null)
                installedFamilies = new string[0];
        }

        for (int i = 0; i < families.Length; i++)
        {
            if (!IsInstalled(families[i]))
                continue;

            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(families[i], 16);
                if (font != null)
                    return font;
            }
            catch
            {
                // Try the next family in the chain.
            }
        }

        return null;
    }

    private static bool IsInstalled(string family)
    {
        string wanted = Normalize(family);

        for (int i = 0; i < installedFamilies.Length; i++)
        {
            if (Normalize(installedFamilies[i]) == wanted)
                return true;
        }

        return false;
    }

    // Installed names vary in spacing and case between platforms
    // ("Helvetica Neue" vs "HelveticaNeue"), so compare on a reduced form.
    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == ' ' || c == '-' || c == '_')
                continue;

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Applies the body font as the global IMGUI default. Because no GUIStyle
    /// in the project sets its own font, every style in every menu inherits
    /// this in one step.
    /// </summary>
    public static void ApplyGlobalFont()
    {
        if (GUI.skin != null && BodyFont != null)
            GUI.skin.font = BodyFont;
    }

    /// <summary>Assigns a role font, leaving the style's size and colour alone.</summary>
    public static void UseDisplayFont(GUIStyle style)
    {
        if (style != null && DisplayFont != null)
            style.font = DisplayFont;
    }

    public static void UseNumericFont(GUIStyle style)
    {
        if (style != null && NumericFont != null)
            style.font = NumericFont;
    }

    // ------------------------------------------------------------ UI palette

    public static readonly Color PanelTop = new Color(0.11f, 0.13f, 0.17f, 0.95f);
    public static readonly Color PanelBottom = new Color(0.05f, 0.06f, 0.09f, 0.97f);
    public static readonly Color PanelBorderColor = new Color(0.32f, 0.38f, 0.46f, 0.9f);

    public static readonly Color AccentTop = new Color(0.13f, 0.52f, 0.48f, 0.96f);
    public static readonly Color AccentBottom = new Color(0.06f, 0.30f, 0.30f, 0.98f);
    public static readonly Color AccentBorder = new Color(0.35f, 0.88f, 0.78f, 0.95f);

    public static readonly Color WarnTop = new Color(0.42f, 0.24f, 0.05f, 0.96f);
    public static readonly Color WarnBottom = new Color(0.24f, 0.12f, 0.02f, 0.97f);
    public static readonly Color WarnBorder = new Color(0.95f, 0.68f, 0.28f, 0.9f);

    public static readonly Color DangerTop = new Color(0.45f, 0.08f, 0.09f, 0.96f);
    public static readonly Color DangerBottom = new Color(0.24f, 0.03f, 0.04f, 0.97f);
    public static readonly Color DangerBorder = new Color(0.98f, 0.36f, 0.32f, 0.9f);

    public static readonly Color TextPrimary = new Color(0.96f, 0.97f, 0.99f);
    public static readonly Color TextMuted = new Color(0.66f, 0.71f, 0.78f);
}
