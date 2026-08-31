namespace BlindTiming.Game
{
    public enum RoundMode
    {
        Precision = 0,
        IntervalGuess = 1,
        Cooperative = 2
    }

    public enum GameState
    {
        WaitingReady = 0,
        Countdown = 1,
        Suspense = 2,
        WaitingForInput = 3,
        Results = 4,
        SettingStart = 5,
        SettingEnd = 6
    }
}
