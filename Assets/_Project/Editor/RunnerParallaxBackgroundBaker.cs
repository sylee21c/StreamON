using System.Collections.Generic;
using System.Linq;
using StreamOn.Minigames.Runner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StreamOn.Editor
{
    public static class RunnerParallaxBackgroundBaker
    {
        private const string ScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string RootName = "Parallax Background Themes";

        private static readonly string[] ThemeNames =
        {
            "GreenZone Day", "GreenZone Night", "Ghetto Day", "Ghetto Night",
            "Seaport Day", "Seaport Night", "Desert Day", "Desert Night"
        };

        private static readonly string[] ThemeFolders =
        {
            "GreenzoneBackground/Day", "GreenzoneBackground/Night",
            "GhettoBackground/Day", "GhettoBackground/Night",
            "SeaportBackground/Day", "SeaportBackground/Night",
            "DesertBackground/Day", "DesertBackground/Night"
        };

        private static readonly float[] DefaultParallax = { 0.015f, 0.04f, 0.08f, 0.14f, 0.22f };

        [MenuItem("STREAM ON/Runner/Bake Parallax Backgrounds")]
        public static void BakeIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating) return;

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            if (scene.GetRootGameObjects().Any(root => root.name == RootName))
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            RunnerGameManager gameManager = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunnerGameManager>(true)).FirstOrDefault();
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
            if (gameManager == null || camera == null)
            {
                Debug.LogError("Runner parallax bake failed: game manager or main camera was not found.");
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            List<Sprite> sprites = LoadSprites();
            if (sprites.Count != ThemeNames.Length * 5 || sprites.Any(sprite => sprite == null))
            {
                Debug.LogWarning("Runner parallax sprites are still importing. Bake will retry.");
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                EditorApplication.delayCall += BakeIfMissing;
                return;
            }

            GameObject root = new GameObject(RootName, typeof(RunnerParallaxBackgroundController));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
            RunnerParallaxBackgroundController controller = root.GetComponent<RunnerParallaxBackgroundController>();
            GameObject[] themeRoots = new GameObject[ThemeNames.Length];
            RunnerParallaxLayer[][] themeLayers = new RunnerParallaxLayer[ThemeNames.Length][];

            for (int themeIndex = 0; themeIndex < ThemeNames.Length; themeIndex++)
            {
                GameObject theme = new GameObject(ThemeNames[themeIndex]);
                theme.transform.SetParent(root.transform, false);
                themeRoots[themeIndex] = theme;
                themeLayers[themeIndex] = new RunnerParallaxLayer[5];

                for (int layerIndex = 0; layerIndex < 5; layerIndex++)
                {
                    GameObject layerObject = new GameObject($"Layer {layerIndex + 1}",
                        typeof(SpriteRenderer), typeof(RunnerParallaxLayer));
                    layerObject.transform.SetParent(theme.transform, false);
                    SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
                    renderer.sprite = sprites[themeIndex * 5 + layerIndex];
                    renderer.sortingOrder = -100 + layerIndex;
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.enabled = false;
                    float viewHeight = camera.orthographicSize * 2f;
                    float authoredScale = viewHeight / Mathf.Max(0.01f, renderer.sprite.bounds.size.y) * 1.02f;
                    layerObject.transform.localScale = new Vector3(authoredScale, authoredScale, 1f);

                    SpriteRenderer[] tileRenderers = new SpriteRenderer[3];
                    for (int tileIndex = 0; tileIndex < tileRenderers.Length; tileIndex++)
                    {
                        GameObject tileObject = new GameObject($"Parallax Tile {tileIndex + 1}", typeof(SpriteRenderer));
                        tileObject.transform.SetParent(layerObject.transform, false);
                        tileObject.transform.localPosition = Vector3.right
                            * renderer.sprite.bounds.size.x * (tileIndex - 1);
                        SpriteRenderer tileRenderer = tileObject.GetComponent<SpriteRenderer>();
                        tileRenderer.sprite = renderer.sprite;
                        tileRenderer.sharedMaterial = renderer.sharedMaterial;
                        tileRenderer.sortingLayerID = renderer.sortingLayerID;
                        tileRenderer.sortingOrder = renderer.sortingOrder;
                        tileRenderer.drawMode = SpriteDrawMode.Simple;
                        tileRenderer.flipX = false;
                        tileRenderers[tileIndex] = tileRenderer;
                    }

                    RunnerParallaxLayer layer = layerObject.GetComponent<RunnerParallaxLayer>();
                    SerializedObject serializedLayer = new SerializedObject(layer);
                    serializedLayer.FindProperty("gameManager").objectReferenceValue = gameManager;
                    serializedLayer.FindProperty("targetCamera").objectReferenceValue = camera;
                    serializedLayer.FindProperty("sourceRenderer").objectReferenceValue = renderer;
                    SerializedProperty serializedTiles = serializedLayer.FindProperty("tileRenderers");
                    serializedTiles.arraySize = tileRenderers.Length;
                    for (int tileIndex = 0; tileIndex < tileRenderers.Length; tileIndex++)
                        serializedTiles.GetArrayElementAtIndex(tileIndex).objectReferenceValue = tileRenderers[tileIndex];
                    serializedLayer.FindProperty("parallaxScale").floatValue = DefaultParallax[layerIndex];
                    serializedLayer.ApplyModifiedPropertiesWithoutUndo();
                    themeLayers[themeIndex][layerIndex] = layer;
                }
                theme.SetActive(themeIndex == 0);
            }

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("gameManager").objectReferenceValue = gameManager;
            serializedController.FindProperty("crossFadeSeconds").floatValue = 0.28f;
            SerializedProperty themes = serializedController.FindProperty("themes");
            themes.arraySize = ThemeNames.Length;
            for (int themeIndex = 0; themeIndex < ThemeNames.Length; themeIndex++)
            {
                SerializedProperty theme = themes.GetArrayElementAtIndex(themeIndex);
                theme.FindPropertyRelative("displayName").stringValue = ThemeNames[themeIndex];
                theme.FindPropertyRelative("root").objectReferenceValue = themeRoots[themeIndex];
                SerializedProperty layers = theme.FindPropertyRelative("layers");
                layers.arraySize = 5;
                for (int layerIndex = 0; layerIndex < 5; layerIndex++)
                    layers.GetArrayElementAtIndex(layerIndex).objectReferenceValue = themeLayers[themeIndex][layerIndex];
            }
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            Transform oldSky = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Sky Background");
            if (oldSky != null) oldSky.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("Runner parallax backgrounds baked as editable scene objects.");
        }

        private static List<Sprite> LoadSprites()
        {
            List<Sprite> sprites = new List<Sprite>(ThemeNames.Length * 5);
            foreach (string folder in ThemeFolders)
                for (int layer = 1; layer <= 5; layer++)
                    sprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Backgrounds/{folder}/{layer}.png"));
            return sprites;
        }
    }
}
