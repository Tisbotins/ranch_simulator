using UnityEngine;

/// <summary>
/// Applies a 60 FPS cap automatically whenever the game starts.
/// No scene object or manually attached component is required.
/// </summary>
public static class RanchFrameRateLimiter
{
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyFrameRateLimit()
    {
        // Application.targetFrameRate controls desktop frame pacing only
        // when vertical synchronization is disabled.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
