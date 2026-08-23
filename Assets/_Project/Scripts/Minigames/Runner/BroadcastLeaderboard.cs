using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

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

    /// <summary>UGS 익명 인증과 네 개의 관리형 리더보드를 연결하는 WebGL 대응 제공자입니다.</summary>
    public sealed class BroadcastLeaderboardProvider : MonoBehaviour
    {
        public RunnerCampaignSettings settings;
        public UgsNotificationPresenter notificationPresenter;
        public string LastError { get; private set; }
        public bool LastOperationSucceeded { get; private set; }

        private Task<bool> _readyTask;
        private string _synchronizedStreamerName;

        private void Awake()
        {
            if (settings != null && settings.useOnlineLeaderboard)
                StartCoroutine(InitializeAndFlush());
        }

        public IEnumerator Submit(BroadcastLeaderboardEntry entry, bool followerBoard, Action<bool> completed = null)
        {
            if (settings == null || !settings.useOnlineLeaderboard || entry == null)
            {
                completed?.Invoke(false);
                yield break;
            }
            Task<bool> task = SubmitAsync(entry, followerBoard);
            yield return new WaitUntil(() => task.IsCompleted);
            completed?.Invoke(task.Status == TaskStatus.RanToCompletion && task.Result);
        }

        public IEnumerator Fetch(BroadcastGameId gameId, bool followerBoard,
            Action<IReadOnlyList<BroadcastLeaderboardEntry>> completed)
        {
            if (settings == null || !settings.useOnlineLeaderboard)
            {
                completed?.Invoke(Array.Empty<BroadcastLeaderboardEntry>());
                yield break;
            }
            Task<IReadOnlyList<BroadcastLeaderboardEntry>> task = FetchAsync(gameId, followerBoard);
            yield return new WaitUntil(() => task.IsCompleted);
            completed?.Invoke(task.Status == TaskStatus.RanToCompletion
                ? task.Result : Array.Empty<BroadcastLeaderboardEntry>());
        }

        public BroadcastLeaderboardEntry LocalEntry(RunnerCampaignSaveData save, BroadcastGameId gameId, bool followerBoard)
        {
            if (save == null) return null;
            return new BroadcastLeaderboardEntry
            {
                playerId = save.playerId,
                displayName = string.IsNullOrWhiteSpace(save.streamerName) ? settings.defaultStreamerName : save.streamerName,
                gameId = gameId,
                score = gameId == BroadcastGameId.Runner ? save.bestRunnerGameScore
                    : gameId == BroadcastGameId.TileArena ? save.bestTileArenaGameScore : save.bestPlasticGameScoreAtNight,
                clearedNight = gameId == BroadcastGameId.PlasticKnightmare ? save.bestPlasticNight : 0,
                followers = save.subscribers,
                achievedAtUtc = save.savedAtUtc
            };
        }

        private IEnumerator InitializeAndFlush()
        {
            Task<bool> ready = EnsureReadyAsync();
            yield return new WaitUntil(() => ready.IsCompleted);
            if (ready.Status != TaskStatus.RanToCompletion || !ready.Result) yield break;

            foreach (BroadcastPendingLeaderboardSubmission pending in BroadcastLeaderboardPendingStore.Snapshot())
            {
                if (pending == null || string.IsNullOrWhiteSpace(pending.boardId)) continue;
                Task<bool> upload = SubmitPendingAsync(pending);
                yield return new WaitUntil(() => upload.IsCompleted);
                if (upload.Status == TaskStatus.RanToCompletion && upload.Result)
                    BroadcastLeaderboardPendingStore.MarkUploaded(pending);
            }
        }

        private async Task<bool> EnsureReadyAsync()
        {
            if (_readyTask == null || (_readyTask.IsCompleted && !_readyTask.Result))
                _readyTask = InitializeServicesAsync();
            return await _readyTask;
        }

        private async Task<bool> InitializeServicesAsync()
        {
            LastError = string.Empty;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    InitializationOptions options = new InitializationOptions();
                    if (!string.IsNullOrWhiteSpace(settings.leaderboardEnvironmentName))
                        options.SetEnvironmentName(settings.leaderboardEnvironmentName.Trim());
                    await UnityServices.InitializeAsync(options);
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await RefreshComplianceNotificationsAsync();
                LastOperationSucceeded = true;
                return true;
            }
            catch (Exception exception)
            {
                LastOperationSucceeded = false;
                LastError = FriendlyError(exception);
                Debug.LogWarning("STREAM ON UGS initialization failed: " + exception.Message);
                return false;
            }
        }

        private async Task RefreshComplianceNotificationsAsync()
        {
            if (notificationPresenter == null) return;
            try
            {
                List<Notification> notifications = await AuthenticationService.Instance.GetNotificationsAsync();
                if (notifications != null && notifications.Count > 0)
                    notificationPresenter.Show(notifications);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("STREAM ON UGS notifications could not be retrieved: " + exception.Message);
            }
        }

        private async Task<bool> SubmitAsync(BroadcastLeaderboardEntry entry, bool followerBoard)
        {
            string boardId = settings.LeaderboardId(entry.gameId, followerBoard);
            BroadcastPendingLeaderboardSubmission pending = new BroadcastPendingLeaderboardSubmission
            {
                boardId = boardId,
                gameId = entry.gameId,
                followerBoard = followerBoard,
                score = followerBoard ? entry.followers : entry.score,
                clearedNight = entry.clearedNight,
                displayName = entry.displayName,
                achievedAtUtc = entry.achievedAtUtc
            };
            bool success = await SubmitPendingAsync(pending);
            if (success) BroadcastLeaderboardPendingStore.MarkUploaded(pending);
            return success;
        }

        private async Task<bool> SubmitPendingAsync(BroadcastPendingLeaderboardSubmission pending)
        {
            if (pending == null || string.IsNullOrWhiteSpace(pending.boardId) || !await EnsureReadyAsync()) return false;
            try
            {
                await SynchronizePlayerNameAsync(pending.displayName);
                BroadcastLeaderboardMetadata metadata = new BroadcastLeaderboardMetadata
                {
                    displayName = pending.displayName,
                    clearedNight = pending.clearedNight,
                    achievedAtUtc = pending.achievedAtUtc
                };
                await LeaderboardsService.Instance.AddPlayerScoreAsync(pending.boardId, Mathf.Max(0, pending.score),
                    new AddPlayerScoreOptions { Metadata = metadata });
                LastOperationSucceeded = true;
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                LastOperationSucceeded = false;
                LastError = FriendlyError(exception);
                Debug.LogWarning($"STREAM ON UGS submit failed ({pending.boardId}): {exception.Message}");
                return false;
            }
        }

        private async Task<IReadOnlyList<BroadcastLeaderboardEntry>> FetchAsync(BroadcastGameId gameId, bool followerBoard)
        {
            if (!await EnsureReadyAsync()) return Array.Empty<BroadcastLeaderboardEntry>();
            string boardId = settings.LeaderboardId(gameId, followerBoard);
            if (string.IsNullOrWhiteSpace(boardId))
            {
                LastOperationSucceeded = false;
                LastError = "리더보드 ID가 비어 있습니다.";
                return Array.Empty<BroadcastLeaderboardEntry>();
            }
            try
            {
                LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(boardId,
                    new GetScoresOptions
                    {
                        Offset = 0,
                        Limit = Mathf.Clamp(settings.leaderboardMaximumRows, 1, 1000),
                        IncludeMetadata = true
                    });
                List<BroadcastLeaderboardEntry> result = new List<BroadcastLeaderboardEntry>();
                if (page?.Results != null)
                {
                    foreach (LeaderboardEntry source in page.Results)
                    {
                        BroadcastLeaderboardMetadata metadata = ParseMetadata(source.Metadata);
                        result.Add(new BroadcastLeaderboardEntry
                        {
                            playerId = source.PlayerId,
                            displayName = !string.IsNullOrWhiteSpace(metadata.displayName) ? metadata.displayName : source.PlayerName,
                            rank = source.Rank,
                            gameId = gameId,
                            score = followerBoard ? 0 : Mathf.Max(0, (int)Math.Round(source.Score)),
                            followers = followerBoard ? Mathf.Max(0, (int)Math.Round(source.Score)) : 0,
                            clearedNight = metadata.clearedNight,
                            achievedAtUtc = !string.IsNullOrWhiteSpace(metadata.achievedAtUtc)
                                ? metadata.achievedAtUtc : source.UpdatedTime.ToString("O")
                        });
                    }
                }
                LastOperationSucceeded = true;
                LastError = string.Empty;
                return result;
            }
            catch (Exception exception)
            {
                LastOperationSucceeded = false;
                LastError = FriendlyError(exception);
                Debug.LogWarning($"STREAM ON UGS fetch failed ({boardId}): {exception.Message}");
                return Array.Empty<BroadcastLeaderboardEntry>();
            }
        }

        private async Task SynchronizePlayerNameAsync(string streamerName)
        {
            string sanitized = SanitizePlayerName(streamerName);
            if (sanitized == _synchronizedStreamerName) return;
            await AuthenticationService.Instance.UpdatePlayerNameAsync(sanitized);
            _synchronizedStreamerName = sanitized;
        }

        private string SanitizePlayerName(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? settings.defaultStreamerName : value.Trim();
            string sanitized = new string(source.Where(character => !char.IsWhiteSpace(character)).Take(16).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "Streamer" : sanitized;
        }

        private static BroadcastLeaderboardMetadata ParseMetadata(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new BroadcastLeaderboardMetadata();
            try { return JsonUtility.FromJson<BroadcastLeaderboardMetadata>(json) ?? new BroadcastLeaderboardMetadata(); }
            catch { return new BroadcastLeaderboardMetadata(); }
        }

        private static string FriendlyError(Exception exception)
        {
            string message = exception?.Message ?? string.Empty;
            if (message.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Unity 프로젝트와 UGS 연결을 확인하세요.";
            if (message.IndexOf("leaderboard", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Unity Dashboard의 리더보드 ID를 확인하세요.";
            return "온라인 연결 실패 · 기록은 재전송 대기 중";
        }
    }

    public sealed class UgsNotificationPresenter : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text messageText;
        public UnityEngine.UI.Button closeButton;

        private void Awake() => closeButton?.onClick.AddListener(Close);

        public void Show(IReadOnlyList<Notification> notifications)
        {
            if (notifications == null || notifications.Count == 0 || panel == null) return;
            if (messageText != null)
            {
                messageText.text = string.Join("\n\n", notifications.Select(notification =>
                    string.IsNullOrWhiteSpace(notification.CaseId)
                        ? notification.Message
                        : $"{notification.Message}\n사례 번호: {notification.CaseId}"));
            }
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        public void Close() => panel?.SetActive(false);
    }

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

    public sealed class StreamerProfilePanel : MonoBehaviour
    {
        public RunnerCampaignSettings settings;
        public TMP_InputField nameInput;
        public UnityEngine.UI.Button saveButton;
        public TMP_Text feedbackText;

        private void Awake() => saveButton?.onClick.AddListener(SaveName);
        private void OnEnable()
        {
            if (settings != null && nameInput != null && RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save))
                nameInput.text = save.streamerName;
        }

        public void SaveName()
        {
            if (settings == null || nameInput == null || !RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save)) return;
            string value = (nameInput.text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) value = settings.defaultStreamerName;
            save.streamerName = value.Substring(0, Mathf.Min(value.Length, 16));
            RunnerCampaignSaveStore.Save(settings, save, true);
            if (feedbackText != null) feedbackText.text = "스트리머 이름 저장 완료";
        }
    }
}
