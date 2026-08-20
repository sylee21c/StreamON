using System.Collections.Generic;
using UnityEngine;

namespace StreamOn.Chat
{
    public sealed class LocalChatService
    {
        private static readonly string[] Nicknames =
        {
            "ㅇㅇ", "돌멩이", "방구석프로", "첫방유입", "억까단", "고인물", "눈팅중"
        };

        private static readonly Dictionary<BroadcastChatEvent, string[]> Messages =
            new Dictionary<BroadcastChatEvent, string[]>
            {
                {
                    BroadcastChatEvent.BroadcastStarted,
                    new[] { "방송 켰다 ㅋㅋ", "오늘은 잘하냐", "일단 지켜본다", "드가자~", "첫 판 가보자고" }
                },
                {
                    BroadcastChatEvent.TimingHit,
                    new[] { "오 좀 치는데?", "나이스 ㅋㅋ", "이걸 맞추네", "폼 좋은데", "깔끔했다" }
                },
                {
                    BroadcastChatEvent.TimingMiss,
                    new[] { "방금 뭐함?", "아니 그걸 왜", "손 어디 갔냐", "억까 시작 ㅋㅋ", "이게 안 보여?" }
                },
                {
                    BroadcastChatEvent.HotStreak,
                    new[] { "오늘 폼 미쳤다", "프로 맞네 ㅋㅋ", "연속 성공 뭐냐", "슬슬 떡상각", "이건 클립감이다" }
                },
                {
                    BroadcastChatEvent.BroadcastSuccess,
                    new[] { "오늘 방송 좋았다", "구독 박고 간다", "이 맛에 보지", "다음 방송도 온다" }
                },
                {
                    BroadcastChatEvent.BroadcastFailure,
                    new[] { "오늘 쉽지 않네", "멘탈 잡아라", "다음 판은 잘하자", "방종각 보인다" }
                }
            };

        private string _lastMessage;

        public string CreateMessage(BroadcastChatEvent chatEvent)
        {
            string[] pool = Messages[chatEvent];
            string message = pool[Random.Range(0, pool.Length)];

            if (pool.Length > 1 && message == _lastMessage)
            {
                int currentIndex = System.Array.IndexOf(pool, message);
                message = pool[(currentIndex + 1) % pool.Length];
            }

            _lastMessage = message;
            string nickname = Nicknames[Random.Range(0, Nicknames.Length)];
            return $"<color=#72E0D0>{nickname}</color>  {message}";
        }
    }
}
