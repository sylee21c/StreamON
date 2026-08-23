using System.Linq;
using StreamOn.Minigames.Runner;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Editor
{
    /// <summary>
    /// Bakes editable UI objects into the scene in Edit Mode. Nothing here creates UI during play.
    /// </summary>
    [InitializeOnLoad]
    public static class RunnerSceneUiBaker
    {
        private const string RoomScenePath = "Assets/Scenes/StreamerRoom.unity";
        private const string KidPrefabPath = "Assets/KidsCharacterFree/Prefabs/Boy0_Humanoid.prefab";

        static RunnerSceneUiBaker()
        {
            EditorApplication.delayCall += BakeRoomSceneIfNeeded;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += BakeRoomSceneIfNeeded;
            };
        }

        [MenuItem("STREAM ON/UI/Bake or Repair Scene UI")]
        public static void BakeRoomSceneIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Scene scene = SceneManager.GetSceneByPath(RoomScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(RoomScenePath, OpenSceneMode.Additive);

            RunnerRoomController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerRoomController>(true)).FirstOrDefault();
            Canvas canvas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(item => item.name == "Room UI");
            if (controller == null || canvas == null)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true).Select(text => text.font).FirstOrDefault(item => item != null);
            EnsureKidRoomPlayer(controller);

            // Stat Gauges 패널은 StatusUI로 대체되어 제거됨
            Transform legacyStatGauges = canvas.transform.Find("Stat Gauges");
            if (legacyStatGauges != null) Object.DestroyImmediate(legacyStatGauges.gameObject);

            SerializedObject serialized = new SerializedObject(controller);
            CanvasGroup fade = EnsureFadeOverlay(canvas.transform);
            GameObject gameSelection = EnsureGameSelectionPanel(canvas.transform, font, out Button runnerButton, out Button tileArenaButton);
            serialized.FindProperty("transitionFade").objectReferenceValue = fade;
            serialized.FindProperty("gameSelectionPanel").objectReferenceValue = gameSelection;
            serialized.FindProperty("runnerGameButton").objectReferenceValue = runnerButton;
            serialized.FindProperty("tileArenaGameButton").objectReferenceValue = tileArenaButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            TMP_Text status = canvas.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Status");
            if (status != null)
            {
                status.text = "팔로워 0    보유금 0원    방송인 Lv.1    남은 포인트 1";
                status.rectTransform.sizeDelta = new Vector2(760f, 45f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("STREAM ON UI: wit, composure, and control gauges are scene-authored in StreamerRoom.");
        }

        private static void EnsureKidRoomPlayer(RunnerRoomController roomController)
        {
            RunnerRoomPlayerController movement = roomController.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerRoomPlayerController>(true)).FirstOrDefault();
            if (movement == null) return;

            GameObject player = movement.gameObject;
            foreach (MeshRenderer renderer in player.GetComponents<MeshRenderer>()) Object.DestroyImmediate(renderer);
            foreach (MeshFilter filter in player.GetComponents<MeshFilter>()) Object.DestroyImmediate(filter);

            Transform visual = player.transform.Find("Kid Streamer Visual");
            if (visual == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KidPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError("STREAM ON room player: KidsCharacterFree/Boy0_Humanoid prefab was not found.");
                    return;
                }
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, player.transform) as GameObject;
                if (instance == null) return;
                instance.name = "Kid Streamer Visual";
                instance.transform.localPosition = new Vector3(0f, -1f, 0f);
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * 1.7f;
                foreach (CharacterController childController in instance.GetComponents<CharacterController>())
                    Object.DestroyImmediate(childController);
                foreach (MonoBehaviour demoScript in instance.GetComponents<MonoBehaviour>())
                    Object.DestroyImmediate(demoScript);
                visual = instance.transform;
            }

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.applyRootMotion = false;
            SerializedObject movementSerialized = new SerializedObject(movement);
            movementSerialized.FindProperty("characterAnimator").objectReferenceValue = animator;
            movementSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanvasGroup EnsureFadeOverlay(Transform parent)
        {
            Transform existing = parent.Find("Broadcast Fade Overlay");
            GameObject overlay = existing != null ? existing.gameObject
                : new GameObject("Broadcast Fade Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            overlay.transform.SetParent(parent, false);
            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = Color.black;
            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            overlay.transform.SetAsLastSibling();
            return group;
        }

        private static GameObject EnsureGameSelectionPanel(Transform parent, TMP_FontAsset font,
            out Button runnerButton, out Button tileArenaButton)
        {
            Transform existing = parent.Find("Broadcast Game Selection");
            GameObject panel = existing != null ? existing.gameObject
                : new GameObject("Broadcast Game Selection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(720f, 410f);
            panel.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.085f, 0.98f);

            if (panel.transform.Find("Title") == null)
            {
                CreateText(panel.transform, "Title", "오늘 방송할 게임 선택", font, 34f,
                    new Vector2(650f, 60f), new Vector2(0f, 142f));
                CreateText(panel.transform, "Guide", "방송이 끝날 때까지 선택한 게임을 플레이합니다.", font, 19f,
                    new Vector2(650f, 40f), new Vector2(0f, 92f));
                CreateGameButton(panel.transform, "Runner Button", "RUNNER\n점프 · 구르기 · 공격", font,
                    new Vector2(-170f, -25f), new Color(0.14f, 0.67f, 0.60f));
                CreateGameButton(panel.transform, "Tile Arena Button", "TILE ARENA\n이동 · 점프 · 타일 수집", font,
                    new Vector2(170f, -25f), new Color(0.25f, 0.48f, 0.88f));
                CreateText(panel.transform, "Notice", "선택하면 즉시 방송을 시작합니다.", font, 17f,
                    new Vector2(650f, 35f), new Vector2(0f, -155f));
            }

            runnerButton = panel.transform.Find("Runner Button").GetComponent<Button>();
            tileArenaButton = panel.transform.Find("Tile Arena Button").GetComponent<Button>();
            panel.SetActive(false);
            panel.transform.SetAsLastSibling();
            return panel;
        }

        private static Button CreateGameButton(Transform parent, string name, string label, TMP_FontAsset font,
            Vector2 position, Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(290f, 170f);
            rect.anchoredPosition = position;
            buttonObject.GetComponent<Image>().color = color;
            CreateText(buttonObject.transform, "Label", label, font, 23f, new Vector2(270f, 145f), Vector2.zero);
            return buttonObject.GetComponent<Button>();
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, TMP_FontAsset font,
            float fontSize, Vector2 size, Vector2 position)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return text;
        }
    }
}
