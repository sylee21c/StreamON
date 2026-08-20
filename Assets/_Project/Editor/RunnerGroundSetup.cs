#if UNITY_EDITOR
using StreamOn.Minigames.Runner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace StreamOn.EditorTools
{
    [InitializeOnLoad]
    public static class RunnerGroundSetup
    {
        private const string ScenePath = "Assets/Scenes/BroadcastRunner.unity";
        private const string RootName = "Scrolling Ground Tiles";
        private const string EnvironmentName = "Environment";
        private const string StaticGroundName = "Ground";
        private const string ManagerName = "Runner Game Manager";
        private const string PlayerName = "Player";
        private const int GroundLayer = 6;
        private const int ChunkCount = 3;
        private const int ChunkWidth = 16;          // cells per chunk (cell size = 1 world unit)
        private const int FillRowsBelow = 1;        // rows painted below the surface row for visual depth
        private const float GroundTopY = -2.6f;     // world Y of the standing surface — unchanged
        private const float GroundY = GroundTopY - 1f; // chunk root Y; surface row top edge = GroundTopY
        private const float RecycleRightEdgeX = -14f;
        // Bumped when the intended layout changes so old scenes get rebuilt exactly once.
        private const int SetupVersion = 6;

        private const string TileFolder = "Assets/Art/Inhumania_Asset/TileSets/basementTileset";
        // Tile picks — edit these to swap the ground look. Setup only re-runs when SetupVersion bumps.
        private const string SurfaceTileName = "Tile_01_0";
        private const string FillTileName = "Tile_02_0";

        static RunnerGroundSetup()
        {
            // Only run once per domain reload. Do NOT re-trigger on play-mode toggle — that
            // path used to wipe user-painted tiles because the health check treated any
            // extra painting as corruption. Users need to be able to paint decorative tiles
            // in edit mode without setup nuking their work on the next play/stop cycle.
            EditorApplication.delayCall += UpgradeScene;
        }

        [MenuItem("STREAM ON/Rebuild Runner Ground")]
        public static void ForceRebuild()
        {
            EditorPrefs.DeleteKey(VersionKey());
            UpgradeScene();
        }

        private static string VersionKey() => $"StreamOn.RunnerGroundSetup.Version:{ScenePath}";

        public static void UpgradeScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            // Version gate ONLY. Once a user has run setup at the current SetupVersion, do
            // not re-touch the scene — they may have painted extra tiles/scenery on top of
            // the base chunks and we must preserve that. Force a rebuild via the STREAM ON
            // menu or by bumping SetupVersion in code.
            if (EditorPrefs.GetInt(VersionKey(), 0) >= SetupVersion)
                return;

            // Resolve tile assets before touching the scene so we bail cleanly if they're missing.
            if (!TryLoadGroundTiles(out TileBase surfaceTile, out TileBase fillTile))
            {
                Debug.LogWarning($"STREAM ON: ground tile assets not found under {TileFolder}. Skipping ground rebuild.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasAlreadyLoaded)
            {
                if (!System.IO.File.Exists(ScenePath))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject environment = FindRoot(scene, EnvironmentName);
                GameObject managerObject = FindInScene(scene, ManagerName);
                if (environment == null || managerObject == null)
                    return;

                Transform root = FindOrCreateRoot(scene, environment);
                RemoveTilemapArtifacts(root);
                DeleteChildrenNamed(root, "Ground Chunk");

                Transform[] tiles = BuildChunks(root, surfaceTile, fillTile);
                RunnerGroundLooper looper = EnsureLooper(root);
                RunnerGameManager manager = managerObject.GetComponent<RunnerGameManager>();

                SetObject(looper, "gameManager", manager);
                SetArray(looper, "tiles", tiles);
                SetFloat(looper, "tileWidth", ChunkWidth);
                SetFloat(looper, "recycleRightEdgeX", RecycleRightEdgeX);
                SetObject(manager, "groundLooper", looper);

                RemoveStaticGround(environment);
                AlignPlayerToGround(scene);
                EnsureGroundLayerName();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorPrefs.SetInt(VersionKey(), SetupVersion);
                Debug.Log($"STREAM ON: BroadcastRunner ground rebuilt ({ChunkCount} Tilemap chunks, surface={SurfaceTileName}, fill={FillTileName}).");
            }
            finally
            {
                if (!wasAlreadyLoaded && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindOrCreateRoot(Scene scene, GameObject environment)
        {
            Transform existing = environment.transform.Find(RootName);
            GameObject rootObj;
            if (existing != null)
            {
                rootObj = existing.gameObject;
            }
            else
            {
                rootObj = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(rootObj, scene);
                rootObj.transform.SetParent(environment.transform, false);
            }
            rootObj.transform.localPosition = Vector3.zero;
            rootObj.transform.localRotation = Quaternion.identity;
            rootObj.transform.localScale = Vector3.one;
            return rootObj.transform;
        }

        private static void RemoveTilemapArtifacts(Transform root)
        {
            Grid grid = root.GetComponent<Grid>();
            if (grid != null) Object.DestroyImmediate(grid);
        }

        private static void DeleteChildrenNamed(Transform parent, string prefix)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal)
                    || child.GetComponent<Tilemap>() != null
                    || child.GetComponent<TilemapRenderer>() != null)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Transform[] BuildChunks(Transform root, TileBase surfaceTile, TileBase fillTile)
        {
            Material spriteMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            Transform[] tiles = new Transform[ChunkCount];
            int halfWidth = ChunkWidth / 2;
            // Tile bounds in chunk-local space: surface row is [0, 1], each fill row extends 1 unit down.
            float colliderHeight = 1f + FillRowsBelow;
            float colliderOffsetY = (1f - FillRowsBelow) * 0.5f;

            for (int i = 0; i < ChunkCount; i++)
            {
                GameObject chunk = new GameObject($"Ground Chunk {i + 1}");
                chunk.layer = GroundLayer;
                chunk.transform.SetParent(root, false);
                chunk.transform.localPosition = new Vector3((i - 1) * ChunkWidth, GroundY, 0f);
                chunk.transform.localScale = Vector3.one;

                Grid grid = chunk.AddComponent<Grid>();
                grid.cellSize = Vector3.one;
                grid.cellGap = Vector3.zero;
                grid.cellLayout = GridLayout.CellLayout.Rectangle;

                GameObject tilesObj = new GameObject("Tiles");
                tilesObj.transform.SetParent(chunk.transform, false);
                Tilemap tilemap = tilesObj.AddComponent<Tilemap>();
                TilemapRenderer tilemapRenderer = tilesObj.AddComponent<TilemapRenderer>();
                tilemapRenderer.sortingOrder = 1;
                tilemapRenderer.sharedMaterial = spriteMaterial;
                // Individual mode: each tile is a separate sprite that follows the transform
                // every frame. Chunk mode batches into internal meshes with a culling cache
                // that visibly lags when the tilemap moves continuously (the scrolling ground).
                // Individual costs more draw calls, but with ~32 tiles per chunk × 3 chunks it's
                // negligible and the tiles stay in sync with the transform.
                tilemapRenderer.mode = TilemapRenderer.Mode.Individual;

                // Defensive: the GameObject is fresh so ClearAllTiles is a no-op today, but if
                // Unity ever leaves stale internal state on a same-named orphaned Tilemap this
                // prevents a hard-to-diagnose "chunk has wrong content" bug from resurfacing.
                tilemap.ClearAllTiles();
                for (int x = -halfWidth; x < halfWidth; x++)
                {
                    tilemap.SetTile(new Vector3Int(x, 0, 0), surfaceTile);
                    for (int f = 1; f <= FillRowsBelow; f++)
                        tilemap.SetTile(new Vector3Int(x, -f, 0), fillTile);
                }
                tilemap.CompressBounds();
                tilemap.RefreshAllTiles();

                // Single BoxCollider2D covers the full painted extent. Simpler and cheaper than
                // TilemapCollider2D + CompositeCollider2D, and the surface stays perfectly flat.
                BoxCollider2D box = chunk.AddComponent<BoxCollider2D>();
                box.size = new Vector2(ChunkWidth, colliderHeight);
                box.offset = new Vector2(0f, colliderOffsetY);

                // Kinematic RB so per-frame transform moves don't force Physics2D to
                // regenerate static geometry every tick.
                Rigidbody2D rb = chunk.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;

                tiles[i] = chunk.transform;
            }
            return tiles;
        }

        private static bool TryLoadGroundTiles(out TileBase surface, out TileBase fill)
        {
            surface = AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{SurfaceTileName}.asset");
            fill = AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{FillTileName}.asset");
            if (surface != null && fill != null) return true;

            if (!AssetDatabase.IsValidFolder(TileFolder)) return false;
            string[] guids = AssetDatabase.FindAssets("t:Tile", new[] { TileFolder });
            if (guids.Length == 0) return false;
            if (surface == null)
                surface = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (fill == null)
                fill = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guids[guids.Length > 1 ? 1 : 0]));
            return surface != null && fill != null;
        }

        private static RunnerGroundLooper EnsureLooper(Transform root)
        {
            RunnerGroundLooper looper = root.GetComponent<RunnerGroundLooper>();
            if (looper == null) looper = root.gameObject.AddComponent<RunnerGroundLooper>();
            return looper;
        }

        private static void RemoveStaticGround(GameObject environment)
        {
            Transform staticGround = environment.transform.Find(StaticGroundName);
            if (staticGround != null) Object.DestroyImmediate(staticGround.gameObject);
        }

        private static void AlignPlayerToGround(Scene scene)
        {
            GameObject playerObj = FindInScene(scene, PlayerName);
            if (playerObj == null) return;
            BoxCollider2D col = playerObj.GetComponent<BoxCollider2D>();
            if (col == null) return;
            // Bottom of collider in local space: offset.y - size.y/2. Placing transform.y so
            // that world bottom equals GroundTopY keeps the player standing exactly on the surface.
            float scaleY = playerObj.transform.localScale.y;
            float bottomLocal = (col.offset.y - col.size.y * 0.5f) * scaleY;
            Vector3 pos = playerObj.transform.position;
            pos.y = GroundTopY - bottomLocal;
            playerObj.transform.position = pos;
        }

        private static void EnsureGroundLayerName()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || layers.arraySize <= GroundLayer) return;
            SerializedProperty layer = layers.GetArrayElementAtIndex(GroundLayer);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = "Ground";
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == objectName) return root;
            return null;
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindRecursive(root.transform, objectName);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindRecursive(Transform current, string objectName)
        {
            if (current.name == objectName) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform found = FindRecursive(current.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty prop = serialized.FindProperty(propertyName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string propertyName, Transform[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty prop = serialized.FindProperty(propertyName);
            if (prop == null) return;
            prop.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
