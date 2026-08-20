#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StreamOn.EditorTools
{
    [InitializeOnLoad]
    public static class BasementTilePaletteSetup
    {
        private const string SourceFolder = "Assets/Art/Inhumania_Asset/TileSets/basementTileset";
        private const string OutputFolder = "Assets/_Project/Tiles";
        private const string PaletteName = "Runner Basement Palette";
        private const string PalettePath = OutputFolder + "/" + PaletteName + ".prefab";
        private const string SessionKey = "StreamOn.BasementPalette.SpacedV3";
        private const int PaletteColumns = 12;
        private const int CellSpacing = 2;

        static BasementTilePaletteSetup()
        {
            EditorApplication.delayCall += BuildAndSelectOnce;
        }

        [MenuItem("STREAM ON/Tiles/Rebuild Basement Tile Palette")]
        public static void RebuildPalette()
        {
            BuildPalette(true);
            EditorApplication.delayCall += OpenPalette;
        }

        [MenuItem("STREAM ON/Tiles/Fix Tile Palette Grid Visibility")]
        public static void FixGridVisibility()
        {
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo drawGridProperty = typeof(GridPaintingState).GetProperty("drawGridGizmo", staticFlags);
            drawGridProperty?.SetValue(null, true);

            Type clipboardType = typeof(GridPaintPaletteWindow).Assembly
                .GetType("UnityEditor.Tilemaps.GridPaintPaletteClipboard");
            FieldInfo gridColorField = clipboardType?.GetField("k_GridColor", staticFlags);
            object prefColor = gridColorField?.GetValue(null);
            PropertyInfo colorProperty = prefColor?.GetType().GetProperty(
                "Color",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (colorProperty != null)
                colorProperty.SetValue(prefColor, new Color(1f, 1f, 1f, 0.65f));

            foreach (GridPaintPaletteWindow window in Resources.FindObjectsOfTypeAll<GridPaintPaletteWindow>())
                window.Repaint();

            SceneView.RepaintAll();
            Debug.Log("STREAM ON Tile Palette grid forced ON with visible white lines.");
        }

        [MenuItem("STREAM ON/Tiles/Open Basement Tile Palette")]
        public static void OpenPalette()
        {
            if (!SelectPalette())
            {
                BuildPalette(true);
                EditorApplication.delayCall += OpenPalette;
                return;
            }

            Tilemap target = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(tilemap => tilemap.name == "Ground Chunk 1");
            if (target != null)
            {
                Selection.activeGameObject = target.gameObject;
                GridPaintingState.scenePaintTarget = target.gameObject;
                EditorGUIUtility.PingObject(target.gameObject);
            }

            EditorWindow.GetWindow<GridPaintPaletteWindow>("Tile Palette").Show();
        }

        private static void BuildAndSelectOnce()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (!IsPaletteComplete()) BuildPalette(true);
            if (SelectPalette()) SessionState.SetBool(SessionKey, true);
        }

        private static void BuildPalette(bool logResult)
        {
            EnsureFolder(OutputFolder);

            List<TileBase> tiles = LoadSourceTiles();
            if (tiles.Count == 0)
            {
                Debug.LogError($"Basement 타일 에셋을 찾을 수 없습니다: {SourceFolder}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) != null)
                AssetDatabase.DeleteAsset(PalettePath);

            GridPaletteUtility.CreateNewPalette(
                OutputFolder,
                PaletteName,
                GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Manual,
                Vector3.one,
                GridLayout.CellSwizzle.XYZ);

            GameObject paletteRoot = PrefabUtility.LoadPrefabContents(PalettePath);
            try
            {
                Grid grid = paletteRoot.GetComponent<Grid>();
                grid.cellSize = Vector3.one;
                grid.cellGap = Vector3.zero;

                Tilemap tilemap = paletteRoot.GetComponentInChildren<Tilemap>(true);
                TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                tilemap.ClearAllTiles();

                for (int i = 0; i < tiles.Count; i++)
                {
                    // Leave one empty cell between assets. Unity draws its Palette grid
                    // behind opaque sprites, so tightly packed tiles hide every grid line.
                    int x = (i % PaletteColumns) * CellSpacing;
                    int y = -(i / PaletteColumns) * CellSpacing;
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles[i]);
                }

                tilemap.CompressBounds();
                tilemap.RefreshAllTiles();
                PrefabUtility.SaveAsPrefabAsset(paletteRoot, PalettePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(paletteRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SessionState.SetBool(SessionKey, false);

            if (logResult)
                Debug.Log($"STREAM ON Basement Palette rebuilt: {tiles.Count} tiles with visible cell spacing.");
        }

        private static bool IsPaletteComplete()
        {
            GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            if (palette == null) return false;

            Tilemap tilemap = palette.GetComponentInChildren<Tilemap>(true);
            Grid grid = palette.GetComponent<Grid>();
            return tilemap != null
                && grid != null
                && grid.cellSize == Vector3.one
                && tilemap.GetUsedTilesCount() == LoadSourceTiles().Count
                && tilemap.cellBounds.size.x >= PaletteColumns * CellSpacing - 1;
        }

        private static bool SelectPalette()
        {
            GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            if (palette == null || !GridPaintingState.palettes.Contains(palette)) return false;
            GridPaintingState.palette = palette;
            return true;
        }

        private static List<TileBase> LoadSourceTiles()
        {
            return AssetDatabase.FindAssets("t:Tile", new[] { SourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(NaturalTileKey, StringComparer.OrdinalIgnoreCase)
                .Select(AssetDatabase.LoadAssetAtPath<TileBase>)
                .Where(tile => tile != null)
                .ToList();
        }

        private static string NaturalTileKey(string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            int separator = name.LastIndexOf('_');
            if (separator >= 0 && int.TryParse(name.Substring(separator + 1), out int index))
                return name.Substring(0, separator) + "_" + index.ToString("D5");
            return name;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
