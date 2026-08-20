using System;
using StreamOn.Core;
using UnityEngine;

namespace StreamOn.Gameplay
{
    public sealed class BroadcastMiniGame : MonoBehaviour
    {
        public const float Duration = 60f;

        public event Action<float, float, int, int> ProgressChanged;
        public event Action<bool, int, int> AttemptResolved;
        public event Action<BroadcastResult> Finished;

        public bool IsRunning { get; private set; }
        public float TimeRemaining { get; private set; }
        public float Cursor01 { get; private set; }

        private int _hits;
        private int _misses;
        private float _elapsed;
        private int _gameSkill;

        public void Begin(int gameSkill)
        {
            _gameSkill = Mathf.Max(1, gameSkill);
            _hits = 0;
            _misses = 0;
            _elapsed = 0f;
            TimeRemaining = Duration;
            IsRunning = true;
            PushProgress();
        }

        public void AttemptHit()
        {
            if (!IsRunning)
            {
                return;
            }

            float halfWidth = Mathf.Min(0.22f, 0.10f + (_gameSkill - 1) * 0.015f);
            bool succeeded = Mathf.Abs(Cursor01 - 0.5f) <= halfWidth;
            if (succeeded)
            {
                _hits++;
            }
            else
            {
                _misses++;
            }

            AttemptResolved?.Invoke(succeeded, _hits, _misses);
            PushProgress();
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            TimeRemaining = Mathf.Max(0f, Duration - _elapsed);
            Cursor01 = Mathf.PingPong(_elapsed * (0.75f + _gameSkill * 0.03f), 1f);
            PushProgress();

            if (TimeRemaining <= 0f)
            {
                Complete();
            }
        }

        private void Complete()
        {
            IsRunning = false;
            bool succeeded = _hits >= 5 && _hits >= _misses;
            int subscriberDelta = succeeded
                ? 10 + _hits * 3 - _misses
                : -Mathf.Max(5, 5 + _misses * 2 - _hits);
            float mentalDelta = succeeded ? -5f : -20f;
            Finished?.Invoke(new BroadcastResult(succeeded, _hits, _misses, subscriberDelta, mentalDelta));
        }

        private void PushProgress()
        {
            ProgressChanged?.Invoke(TimeRemaining, Cursor01, _hits, _misses);
        }
    }
}
