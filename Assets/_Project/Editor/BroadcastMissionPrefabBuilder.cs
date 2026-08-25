#if UNITY_EDITOR
using System.Linq;
using StreamOn.Minigames.Runner;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.EditorTools
{
    public static class BroadcastMissionPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/SharedMissionEvent.prefab";

        [MenuItem("STREAM ON/Shared UI/Rebuild Mission Event Prefab")]
        public static void Rebuild()
        {
            EnsurePrefab(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static GameObject EnsurePrefab(bool rebuild = false)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && !rebuild) return existing;

            TMP_FontAsset font = FindFont();
            Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            GameObject root = new GameObject("Mission Event", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup), typeof(BroadcastMissionEventController));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 150f);
            Image panel = root.GetComponent<Image>();
            panel.sprite = panelSprite;
            panel.type = Image.Type.Sliced;
            panel.color = new Color(0.035f, 0.045f, 0.075f, 0.97f);
            panel.raycastTarget = false;

            Image accent = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            accent.transform.SetParent(root.transform, false);
            accent.color = new Color32(255, 190, 59, 255);
            accent.raycastTarget = false;
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, .5f);
            accent.rectTransform.sizeDelta = new Vector2(8f, 0f);

            TMP_Text header = Label("Header", root.transform, "보통 돌발 미션", font, 17f,
                new Vector2(420f, 28f), new Vector2(-47f, 48f));
            header.color = new Color32(255, 190, 59, 255);
            TMP_Text mission = Label("Mission", root.transform, "10초 동안 피해받지 않기", font, 24f,
                new Vector2(440f, 40f), new Vector2(-37f, 15f));
            TMP_Text progress = Label("Progress", root.transform, "진행도  0%", font, 17f,
                new Vector2(235f, 30f), new Vector2(-140f, -31f));
            progress.color = new Color32(223, 226, 234, 255);
            TMP_Text reward = Label("Reward", root.transform, "성공 보상  +5,000원", font, 17f,
                new Vector2(250f, 30f), new Vector2(105f, -31f));
            reward.color = new Color32(255, 219, 108, 255);

            Image timerBackground = new GameObject("Timer Ring Background", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            timerBackground.transform.SetParent(root.transform, false);
            timerBackground.sprite = circle;
            timerBackground.color = new Color(0.18f, 0.22f, 0.30f, .9f);
            timerBackground.raycastTarget = false;
            SetRect(timerBackground.rectTransform, new Vector2(54f, 54f), new Vector2(244f, 43f));

            Image timerFill = new GameObject("Timer Ring Fill", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            timerFill.transform.SetParent(root.transform, false);
            timerFill.sprite = circle;
            timerFill.color = new Color32(255, 190, 59, 255);
            timerFill.raycastTarget = false;
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Radial360;
            timerFill.fillOrigin = 2;
            timerFill.fillClockwise = false;
            timerFill.fillAmount = 1f;
            SetRect(timerFill.rectTransform, new Vector2(48f, 48f), new Vector2(244f, 43f));

            BroadcastMissionEventController controller = root.GetComponent<BroadcastMissionEventController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("headerText").objectReferenceValue = header;
            so.FindProperty("missionText").objectReferenceValue = mission;
            so.FindProperty("progressText").objectReferenceValue = progress;
            so.FindProperty("rewardText").objectReferenceValue = reward;
            so.FindProperty("timerBackground").objectReferenceValue = timerBackground;
            so.FindProperty("timerFill").objectReferenceValue = timerFill;
            SerializedProperty rules = so.FindProperty("missions");
            BroadcastMissionRule[] defaults = DefaultRules();
            rules.arraySize = defaults.Length;
            for (int i = 0; i < defaults.Length; i++) WriteRule(rules.GetArrayElementAtIndex(i), defaults[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CanvasGroup>().alpha = 0f;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static BroadcastMissionRule[] DefaultRules() => new[]
        {
            Rule("runner_score_easy", BroadcastMissionGame.Runner, BroadcastMissionDifficulty.Easy, BroadcastMissionType.RunnerNoDamageScore, "피해 없이 150점 획득", 150, 0, 10, 1.5f),
            Rule("runner_time_easy", BroadcastMissionGame.Runner, BroadcastMissionDifficulty.Easy, BroadcastMissionType.RunnerNoDamageTime, "8초 동안 피해받지 않기", 1, 8, 10, 1.5f),
            Rule("runner_obstacle_normal", BroadcastMissionGame.Runner, BroadcastMissionDifficulty.Normal, BroadcastMissionType.RunnerClearObstaclesNoDamage, "피해 없이 장애물 4개 통과", 4, 0, 14, 1.9f),
            Rule("runner_robot_normal", BroadcastMissionGame.Runner, BroadcastMissionDifficulty.Normal, BroadcastMissionType.RunnerAvoidRobots, "로봇 1개를 점프로 넘기", 1, 0, 14, 1.9f),
            Rule("runner_no_attack_hard", BroadcastMissionGame.Runner, BroadcastMissionDifficulty.Hard, BroadcastMissionType.RunnerNoAttackScore, "공격 없이 500점 획득", 500, 0, 20, 2.5f),
            Rule("runner_time_hard", BroadcastMissionGame.Runner, BroadcastMissionDifficulty.Hard, BroadcastMissionType.RunnerNoDamageTime, "25초 동안 피해받지 않기", 1, 25, 20, 2.5f),

            Rule("tile_pickup_easy", BroadcastMissionGame.TileArena, BroadcastMissionDifficulty.Easy, BroadcastMissionType.TileCollectBlueTimed, "10초 안에 파란 타일 3개 획득", 3, 10, 10, 1.5f),
            Rule("tile_safe_easy", BroadcastMissionGame.TileArena, BroadcastMissionDifficulty.Easy, BroadcastMissionType.TileNoDamageTime, "10초 동안 피해받지 않기", 1, 10, 10, 1.5f),
            Rule("tile_pattern_normal", BroadcastMissionGame.TileArena, BroadcastMissionDifficulty.Normal, BroadcastMissionType.TileClearPatternNoDamage, "현재 패턴을 피해 없이 완료", 1, 0, 14, 1.9f),
            Rule("tile_no_jump_normal", BroadcastMissionGame.TileArena, BroadcastMissionDifficulty.Normal, BroadcastMissionType.TileCollectWithoutJump, "점프 없이 파란 타일 4개 획득", 4, 0, 14, 1.9f),
            Rule("tile_two_patterns_hard", BroadcastMissionGame.TileArena, BroadcastMissionDifficulty.Hard, BroadcastMissionType.TileClearPatternsNoDamage, "피해 없이 패턴 2개 완료", 2, 0, 20, 2.5f),
            Rule("tile_pickup_hard", BroadcastMissionGame.TileArena, BroadcastMissionDifficulty.Hard, BroadcastMissionType.TileCollectBlueTimed, "10초 안에 파란 타일 8개 획득", 8, 10, 20, 2.5f),

            PlasticRule("plastic_bed_easy", BroadcastMissionDifficulty.Easy, BroadcastMissionType.PlasticBedNoDamageTime, "침대 10초 동안 지키기", 1, 10, 10, 1.5f, 3, 2),
            PlasticRule("plastic_kill_easy", BroadcastMissionDifficulty.Easy, BroadcastMissionType.PlasticDefeatGhostsTimed, "현재 유령 2마리 처치", 2, 12, 10, 1.5f, 2, 0),
            PlasticRule("plastic_combo_normal", BroadcastMissionDifficulty.Normal, BroadcastMissionType.PlasticCombo, "4콤보 달성", 4, 0, 14, 1.9f, 2, 0),
            PlasticRule("plastic_player_normal", BroadcastMissionDifficulty.Normal, BroadcastMissionType.PlasticPlayerNoDamageTime, "20초 동안 피해받지 않기", 1, 20, 14, 1.9f, 3, 0),
            PlasticRule("plastic_bed_hard", BroadcastMissionDifficulty.Hard, BroadcastMissionType.PlasticBedNoDamageTime, "침대 30초 동안 지키기", 1, 30, 20, 2.5f, 5, 2),
            PlasticRule("plastic_kill_hard", BroadcastMissionDifficulty.Hard, BroadcastMissionType.PlasticDefeatGhostsTimed, "20초 안에 유령 5마리 처치", 5, 20, 20, 2.5f, 5, 0)
        };

        private static BroadcastMissionRule Rule(string id, BroadcastMissionGame game, BroadcastMissionDifficulty difficulty,
            BroadcastMissionType type, string title, float target, float duration, float heat, float donation) => new BroadcastMissionRule
        {
            id = id, game = game, difficulty = difficulty, type = type, title = title,
            target = target, durationSeconds = duration, successHeat = heat, donationMultiplier = donation
        };

        private static BroadcastMissionRule PlasticRule(string id, BroadcastMissionDifficulty difficulty,
            BroadcastMissionType type, string title, float target, float duration, float heat, float donation,
            int ghosts, int damaged) => new BroadcastMissionRule
        {
            id = id, game = BroadcastMissionGame.PlasticKnightmare, difficulty = difficulty, type = type,
            title = title, target = target, durationSeconds = duration, successHeat = heat,
            donationMultiplier = donation, minimumActiveGhosts = ghosts, minimumDamagedFacilities = damaged
        };

        private static void WriteRule(SerializedProperty target, BroadcastMissionRule value)
        {
            target.FindPropertyRelative("id").stringValue = value.id;
            target.FindPropertyRelative("game").enumValueIndex = (int)value.game;
            target.FindPropertyRelative("difficulty").enumValueIndex = (int)value.difficulty;
            target.FindPropertyRelative("type").enumValueIndex = (int)value.type;
            target.FindPropertyRelative("title").stringValue = value.title;
            target.FindPropertyRelative("target").floatValue = value.target;
            target.FindPropertyRelative("durationSeconds").floatValue = value.durationSeconds;
            target.FindPropertyRelative("successHeat").floatValue = value.successHeat;
            target.FindPropertyRelative("donationMultiplier").floatValue = value.donationMultiplier;
            target.FindPropertyRelative("minimumActiveGhosts").intValue = value.minimumActiveGhosts;
            target.FindPropertyRelative("minimumDamagedFacilities").intValue = value.minimumDamagedFacilities;
        }

        private static TMP_Text Label(string name, Transform parent, string text, TMP_FontAsset font,
            float fontSize, Vector2 size, Vector2 position)
        {
            TextMeshProUGUI label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.text = text;
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            SetRect(label.rectTransform, size, position);
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static TMP_FontAsset FindFont()
        {
            string guid = AssetDatabase.FindAssets("Galmuri14 SDF t:TMP_FontAsset").FirstOrDefault();
            return string.IsNullOrEmpty(guid) ? null
                : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
#endif
