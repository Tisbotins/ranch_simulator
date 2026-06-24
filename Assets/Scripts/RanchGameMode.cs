public enum RanchGameMode
{
    TitleScreen,
    SinglePlayer,
    LanHost,
    LanClient
}

/// <summary>
/// Shared runtime flag so existing systems can tell whether the current
/// session is single-player, the LAN host, or the LAN guest.
/// </summary>
public static class RanchGameModeState
{
    public static RanchGameMode Current { get; private set; } =
        RanchGameMode.TitleScreen;

    public static bool IsSinglePlayer =>
        Current == RanchGameMode.SinglePlayer;

    public static bool IsMultiplayer =>
        Current == RanchGameMode.LanHost ||
        Current == RanchGameMode.LanClient;

    public static bool IsLanHost =>
        Current == RanchGameMode.LanHost;

    public static bool IsLanClient =>
        Current == RanchGameMode.LanClient;

    public static void SetMode(RanchGameMode mode)
    {
        Current = mode;
    }

    public static void ResetToTitle()
    {
        Current = RanchGameMode.TitleScreen;
    }
}
