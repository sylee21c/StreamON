using System;
using UnityEngine;

namespace StreamOn.Core
{
    public sealed class GameSession
    {
        public const int MaxDays = 7;

        public PlayerState Player { get; } = new PlayerState();
        public GamePhase Phase { get; private set; } = GamePhase.Day;
        public BroadcastResult LastBroadcast { get; private set; }

        public void Train()
        {
            RequirePhase(GamePhase.Day);
            Player.GameSkill++;
            Phase = GamePhase.Broadcast;
        }

        public void Rest()
        {
            RequirePhase(GamePhase.Day);
            Player.Mental = Mathf.Min(100f, Player.Mental + 30f);
            Phase = GamePhase.Broadcast;
        }

        public void FinishBroadcast(BroadcastResult result)
        {
            RequirePhase(GamePhase.Broadcast);
            LastBroadcast = result;
            Player.Subscribers = Mathf.Max(0, Player.Subscribers + result.SubscriberDelta);
            Player.Mental = Mathf.Clamp(Player.Mental + result.MentalDelta, 0f, 100f);

            Phase = Player.Subscribers <= 0 || Player.Mental <= 0f
                ? GamePhase.GameOver
                : GamePhase.Settlement;
        }

        public void AdvanceDay()
        {
            RequirePhase(GamePhase.Settlement);
            if (Player.Day >= MaxDays)
            {
                Phase = GamePhase.Clear;
                return;
            }

            Player.Day++;
            Phase = GamePhase.Day;
        }

        private void RequirePhase(GamePhase expected)
        {
            if (Phase != expected)
            {
                throw new InvalidOperationException($"Expected phase {expected}, but current phase is {Phase}.");
            }
        }
    }
}
