using System.Linq;
using StreamOn.Minigames.Runner;
using StreamOn.Minigames.TileArena;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Editor
{
    [InitializeOnLoad]
    public static class BroadcastSystemUiBuilder
    {
        private const string Folder = "Assets/_Project/Prefabs";
        private const string SettlementPath = Folder + "/SharedBroadcastSettlement.prefab";
        private const string TileHudPath = Folder + "/TileBroadcastSessionHUD.prefab";
        private const string ShopPath = Folder + "/RoomEquipmentShop.prefab";
        private const string SettingsPath = "Assets/_Project/Settings/RunnerCampaignSettings.asset";
        private const string RunnerScene = "Assets/Scenes/BroadcastRunner.unity";
        private const string TileScene = "Assets/Scenes/TileArena.unity";
        private const string RoomScene = "Assets/Scenes/StreamerRoom.unity";

        static BroadcastSystemUiBuilder() => EditorApplication.delayCall += Refresh;

        [MenuItem("STREAM ON/Shared UI/Refresh Broadcast Systems")]
        public static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            RunnerCampaignSettings settings = AssetDatabase.LoadAssetAtPath<RunnerCampaignSettings>(SettingsPath);
            if (settings == null) return;
            GameObject settlement = EnsureSettlementPrefab();
            GameObject tileHud = EnsureTileHudPrefab();
            GameObject shop = EnsureShopPrefab(settings);
            PlaceSettlement(RunnerScene, settlement);
            PlaceSettlement(TileScene, settlement);
            UpgradeTile(tileHud, settings);
            UpgradeRoom(shop);
            AssetDatabase.SaveAssets();
        }

        private static GameObject EnsureSettlementPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SettlementPath);
            if (existing != null) return existing;
            TMP_FontAsset font = Font(); Sprite sprite = PanelSprite();
            GameObject root = Panel("Broadcast Settlement Dashboard", new Vector2(680, 530), new Color(.025f, .035f, .06f, .985f), sprite);
            root.AddComponent<CanvasGroup>();
            TMP_Text title = Text("Title", root.transform, "방송 결과", font, 31, new Vector2(610, 52), new Vector2(0, 215));
            TMP_Text game = ResultCard("Game Result", root.transform, "최고 점수", font, new Vector2(0, 130), sprite);
            TMP_Text audience = ResultCard("Audience Result", root.transform, "시청자", font, new Vector2(0, 30), sprite);
            TMP_Text rating = ResultCard("Rating Result", root.transform, "방송 평점", font, new Vector2(0, -70), sprite);
            TMP_Text growth = ResultCard("Growth Result", root.transform, "성장 및 수익", font, new Vector2(0, -170), sprite);
            Button next = Button("Continue Button", root.transform, "다음 날", font, new Vector2(260, 52), new Vector2(0, -236), new Color(.16f, .68f, .58f), sprite, out TMP_Text nextLabel);
            RunnerBroadcastSettlementView view = root.AddComponent<RunnerBroadcastSettlementView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("gameResultText").objectReferenceValue = game;
            serialized.FindProperty("audienceText").objectReferenceValue = audience;
            serialized.FindProperty("ratingText").objectReferenceValue = rating;
            serialized.FindProperty("growthText").objectReferenceValue = growth;
            serialized.FindProperty("continueButton").objectReferenceValue = next;
            serialized.FindProperty("continueLabel").objectReferenceValue = nextLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, SettlementPath);
        }

        private static GameObject EnsureTileHudPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TileHudPath);
            if (existing != null) return existing;
            TMP_FontAsset font = Font(); Sprite sprite = PanelSprite();
            GameObject root = Panel("Tile Broadcast Session HUD", new Vector2(310, 74), new Color(.025f, .035f, .06f, .92f), sprite);
            TMP_Text time = Text("Remaining Time", root.transform, "방송 01:30", font, 20, new Vector2(270, 30), new Vector2(0, 15));
            time.color = new Color(.35f, .95f, .82f);
            TMP_Text attempt = Text("Attempt", root.transform, "도전 1회", font, 15, new Vector2(270, 24), new Vector2(0, -17));
            return Save(root, TileHudPath);
        }

        private static GameObject EnsureShopPrefab(RunnerCampaignSettings settings)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPath);
            if (existing != null) return existing;
            TMP_FontAsset font = Font(); Sprite sprite = PanelSprite();
            GameObject root = new GameObject("Equipment Shop UI", typeof(RectTransform));
            Button open = Button("Open Shop Button", root.transform, "장비 업그레이드", font, new Vector2(220, 48), new Vector2(0, 0), new Color(.16f, .55f, .62f), sprite, out _);
            RectTransform openRect = open.GetComponent<RectTransform>(); openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(1, 1); openRect.anchoredPosition = new Vector2(-28, -28);
            GameObject panel = Panel("Equipment Shop Panel", new Vector2(650, 500), new Color(.025f, .035f, .06f, .985f), sprite);
            panel.transform.SetParent(root.transform, false);
            Text("Title", panel.transform, "방송 장비 업그레이드", font, 30, new Vector2(580, 50), new Vector2(0, 205));
            TMP_Text cash = Text("Cash", panel.transform, "보유금 0원", font, 21, new Vector2(560, 38), new Vector2(0, 160));
            TMP_Text[] labels = new TMP_Text[4]; Button[] buttons = new Button[4];
            string[] names = { "PC", "마이크", "체력 장비", "방 인테리어" };
            for (int i = 0; i < 4; i++)
            {
                float y = 105 - i * 78;
                labels[i] = Text(names[i], panel.transform, names[i] + "  Lv.1", font, 17, new Vector2(390, 60), new Vector2(-80, y));
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                buttons[i] = Button(names[i] + " Upgrade", panel.transform, "업그레이드", font, new Vector2(150, 48), new Vector2(220, y), new Color(.18f, .65f, .54f), sprite, out _);
            }
            TMP_Text notice = Text("Notice", panel.transform, string.Empty, font, 16, new Vector2(560, 30), new Vector2(0, -184));
            notice.color = new Color(1f, .82f, .35f);
            Button close = Button("Close", panel.transform, "닫기", font, new Vector2(160, 46), new Vector2(0, -220), new Color(.25f, .30f, .40f), sprite, out _);
            RunnerEquipmentShopController shop = root.AddComponent<RunnerEquipmentShopController>();
            SerializedObject serialized = new SerializedObject(shop);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("shopPanel").objectReferenceValue = panel;
            serialized.FindProperty("openButton").objectReferenceValue = open;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("cashText").objectReferenceValue = cash;
            serialized.FindProperty("pcButton").objectReferenceValue = buttons[0]; serialized.FindProperty("pcText").objectReferenceValue = labels[0];
            serialized.FindProperty("microphoneButton").objectReferenceValue = buttons[1]; serialized.FindProperty("microphoneText").objectReferenceValue = labels[1];
            serialized.FindProperty("fitnessButton").objectReferenceValue = buttons[2]; serialized.FindProperty("fitnessText").objectReferenceValue = labels[2];
            serialized.FindProperty("interiorButton").objectReferenceValue = buttons[3]; serialized.FindProperty("interiorText").objectReferenceValue = labels[3];
            serialized.FindProperty("noticeText").objectReferenceValue = notice;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, ShopPath);
        }

        private static void PlaceSettlement(string path, GameObject prefab)
        {
            Scene scene = Open(path, out bool opened); if (!scene.IsValid()) return;
            Canvas canvas = Find<Canvas>(scene); RunnerBroadcastSettlementView view = Find<RunnerBroadcastSettlementView>(scene);
            if (canvas != null && view == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                instance.transform.SetParent(canvas.transform, false);
                RectTransform rect = instance.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(-150, 0); rect.SetAsLastSibling();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            SaveClose(scene, opened);
        }

        private static void UpgradeTile(GameObject hudPrefab, RunnerCampaignSettings settings)
        {
            Scene scene = Open(TileScene, out bool opened); if (!scene.IsValid()) return;
            Canvas canvas = Find<Canvas>(scene); TileArenaController game = Find<TileArenaController>(scene);
            if (canvas == null || game == null) { SaveClose(scene, opened); return; }
            TileArenaBroadcastSessionController session = game.GetComponent<TileArenaBroadcastSessionController>();
            if (session == null) session = game.gameObject.AddComponent<TileArenaBroadcastSessionController>();
            Transform hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == "Tile Broadcast Session HUD");
            if (hud == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(hudPrefab, scene) as GameObject; instance.transform.SetParent(canvas.transform, false); hud = instance.transform;
                RectTransform rect = hud.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(-150, -42);
            }
            SerializedObject serialized = new SerializedObject(session);
            serialized.FindProperty("gameController").objectReferenceValue = game;
            serialized.FindProperty("audience").objectReferenceValue = game.GetComponent<TileArenaChatAdapter>();
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("settlementView").objectReferenceValue = Find<RunnerBroadcastSettlementView>(scene);
            serialized.FindProperty("remainingTimeText").objectReferenceValue = hud.Find("Remaining Time")?.GetComponent<TMP_Text>();
            serialized.FindProperty("attemptText").objectReferenceValue = hud.Find("Attempt")?.GetComponent<TMP_Text>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject gameSerialized = new SerializedObject(game);
            gameSerialized.FindProperty("broadcastSession").objectReferenceValue = session;
            gameSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); SaveClose(scene, opened);
        }

        private static void UpgradeRoom(GameObject prefab)
        {
            Scene scene = Open(RoomScene, out bool opened); if (!scene.IsValid()) return;
            Canvas canvas = Find<Canvas>(scene);
            if (canvas != null && Find<RunnerEquipmentShopController>(scene) == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject; instance.transform.SetParent(canvas.transform, false);
                RectTransform rect = instance.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.SetAsLastSibling();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            SaveClose(scene, opened);
        }

        private static TMP_Text ResultCard(string name, Transform parent, string value, TMP_FontAsset font, Vector2 position, Sprite sprite)
        {
            GameObject card = Panel(name, new Vector2(590, 88), new Color(.07f, .09f, .14f, .95f), sprite); card.transform.SetParent(parent, false); card.GetComponent<RectTransform>().anchoredPosition = position;
            return Text("Text", card.transform, value, font, 17, new Vector2(550, 76), Vector2.zero);
        }

        private static GameObject Panel(string name, Vector2 size, Color color, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); obj.GetComponent<RectTransform>().sizeDelta = size;
            Image image = obj.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color; return obj;
        }

        private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float size, Vector2 dimensions, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>(); text.text = value; text.font = font; text.fontSize = size; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.Normal;
            RectTransform rect = text.rectTransform; rect.sizeDelta = dimensions; rect.anchoredPosition = position; return text;
        }

        private static Button Button(string name, Transform parent, string label, TMP_FontAsset font, Vector2 size, Vector2 position, Color color, Sprite sprite, out TMP_Text text)
        {
            GameObject obj = Panel(name, size, color, sprite); obj.transform.SetParent(parent, false); obj.GetComponent<RectTransform>().anchoredPosition = position;
            Button button = obj.AddComponent<Button>(); text = Text("Label", obj.transform, label, font, 17, size - new Vector2(12, 8), Vector2.zero); return button;
        }

        private static GameObject Save(GameObject root, string path) { GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return saved; }
        private static Sprite PanelSprite() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static TMP_FontAsset Font() { string guid = AssetDatabase.FindAssets("Galmuri14 SDF t:TMP_FontAsset").FirstOrDefault(); return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid)); }
        private static Scene Open(string path, out bool opened) { Scene scene = SceneManager.GetSceneByPath(path); opened = !scene.IsValid() || !scene.isLoaded; return opened && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene; }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static void SaveClose(Scene scene, bool opened) { if (scene.IsValid() && scene.isLoaded && scene.isDirty) EditorSceneManager.SaveScene(scene); if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
