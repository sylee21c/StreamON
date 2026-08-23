using System;

namespace StreamOn.Minigames.Runner
{
    [Serializable]
    public sealed class BroadcastLeaderboardEntry
    {
        public string playerId;
        public string displayName;
        public int rank = -1;
        public BroadcastGameId gameId;
        public int score;
        public int clearedNight;
        public int followers;
        public string achievedAtUtc;
    }

    [Serializable]
    public sealed class BroadcastLeaderboardMetadata
    {
        public string displayName;
        public int clearedNight;
        public string achievedAtUtc;
    }
}
