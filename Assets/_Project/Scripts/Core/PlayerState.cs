using System;

namespace StreamOn.Core
{
    [Serializable]
    public sealed class PlayerState
    {
        public int Day = 1;
        public int Subscribers = 100;
        public float Mental = 100f;
        public int GameSkill = 1;
        public int TalkingSkill = 1;
    }
}
