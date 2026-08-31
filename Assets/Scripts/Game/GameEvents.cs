using System;

namespace BlindTiming.Game
{
    public static class GameEvents
    {
        public static event Action<double> CountdownStarted;
        public static event Action<ulong, float> PressAccepted;
        public static event Action<float, RoundMode, ulong[], float[], bool, ulong, bool, int, string> RoundResolved;

        public static event Action OpponentDisconnected;
        public static event Action ConnectionLost;

        public static void RaiseCountdownStarted(double startServerTime) => CountdownStarted?.Invoke(startServerTime);
        public static void RaisePressAccepted(ulong clientId, float pressedAt) => PressAccepted?.Invoke(clientId, pressedAt);

        public static void RaiseRoundResolved(float targetTime, RoundMode mode, ulong[] playerIds,
            float[] pressTimes, bool success, ulong winnerId, bool hasWinner, int pointsAwarded, string outcomeLabel) =>
            RoundResolved?.Invoke(targetTime, mode, playerIds, pressTimes, success, winnerId, hasWinner, pointsAwarded, outcomeLabel);

        public static void RaiseOpponentDisconnected() => OpponentDisconnected?.Invoke();
        public static void RaiseConnectionLost() => ConnectionLost?.Invoke();
    }
}
