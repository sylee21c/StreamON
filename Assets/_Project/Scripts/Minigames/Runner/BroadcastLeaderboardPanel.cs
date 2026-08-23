using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class BroadcastLeaderboardPanel : MonoBehaviour
    {
        public RunnerCampaignSettings settings;
        public BroadcastLeaderboardProvider provider;
        public BroadcastGameId gameId;
        public bool followerLeaderboard;
        public TMP_Text titleText;
        public TMP_Text[] rowTexts;
        public TMP_Text statusText;
        public UnityEngine.UI.Button submitButton;

        private void Awake() => submitButton?.onClick.AddListener(SubmitMyRecord);
        private void OnEnable() => Refresh();

        public void Refresh()
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save)) return;
            if (provider == null) provider = FindFirstObjectByType<BroadcastLeaderboardProvider>();
            if (titleText != null) titleText.text = followerLeaderboard ? "스트리머 팔로워 순위" : $"{settings.GameRule(gameId).displayName} 리더보드";
            BroadcastLeaderboardEntry local = provider != null ? provider.LocalEntry(save, gameId, followerLeaderboard) : null;
            IReadOnlyList<BroadcastLeaderboardEntry> localEntries = local != null
                ? new[] { local } : Array.Empty<BroadcastLeaderboardEntry>();
            Render(localEntries, true);
            if (provider != null && settings.useOnlineLeaderboard)
                StartCoroutine(provider.Fetch(gameId, followerLeaderboard, entries =>
                {
                    if (provider.LastOperationSucceeded) Render(entries, false);
                    else
                    {
                        Render(localEntries, true);
                        if (statusText != null) statusText.text = provider.LastError;
                    }
                }));
        }

        public void SubmitMyRecord()
        {
            if (provider == null || settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save)) return;
            BroadcastLeaderboardPendingStore.QueueFromSave(settings, save);
            BroadcastLeaderboardEntry entry = provider.LocalEntry(save, gameId, followerLeaderboard);
            StartCoroutine(provider.Submit(entry, followerLeaderboard, success =>
            {
                if (statusText != null) statusText.text = success ? "기록 등록 완료" : "온라인 리더보드 연결 필요";
                if (success) Refresh();
            }));
        }

        private void Render(IEnumerable<BroadcastLeaderboardEntry> entries, bool localOnly)
        {
            List<BroadcastLeaderboardEntry> sorted = entries.Where(entry => entry != null).ToList();
            bool serverRanked = sorted.Any(entry => entry.rank >= 0);
            sorted = serverRanked ? sorted.OrderBy(entry => entry.rank).ToList()
                : followerLeaderboard ? sorted.OrderByDescending(entry => entry.followers).ToList()
                : sorted.OrderByDescending(entry => entry.score).ToList();
            for (int index = 0; rowTexts != null && index < rowTexts.Length; index++)
            {
                TMP_Text row = rowTexts[index];
                if (row == null) continue;
                if (index >= sorted.Count) { row.text = string.Empty; continue; }
                BroadcastLeaderboardEntry entry = sorted[index];
                string value = followerLeaderboard ? $"{entry.followers:N0}명"
                    : gameId == BroadcastGameId.PlasticKnightmare ? $"Night {entry.clearedNight} · {entry.score:N0}" : entry.score.ToString("N0");
                int displayedRank = serverRanked && entry.rank >= 0 ? entry.rank + 1 : index + 1;
                row.text = $"{displayedRank,2}.  {entry.displayName}    {value}";
            }
            if (statusText != null) statusText.text = localOnly && !settings.useOnlineLeaderboard
                ? "로컬 기록 · UGS 연결 전"
                : localOnly ? "온라인 리더보드 연결 중..." : "온라인 리더보드 갱신 완료";
        }
    }
}
