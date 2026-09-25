using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tiramisu.EditorTools
{
    /// <summary>
    /// Prepares every FBX in Assets/Art/Models: import settings, and each Blender material slot
    /// is swapped (by name) for a shared Unity material in Assets/Art/Materials/Furniture.
    /// Slot names ending in "_main" are the part the colour options will tint later.
    /// Menu: Tiramisu > Import furniture (the greybox builder also runs it).
    /// </summary>
    public static class FurnitureImport
    {
        public const string ModelDir = "Assets/Art/Models";
        const string MatDir = "Assets/Art/Materials/Furniture";

        // Blender slot name -> texture set (null = plain), tint, smoothness range, metallic, neutral
        // (neutral = greyscale detail map so the tint sets the colour; needed for anything the colour options tint).
        // Unknown names get a neutral grey on purpose so they are easy to spot.
        static readonly Dictionary<string, (string tex, Color tint, Vector2 smooth, float metal, bool neutral)> Looks =
            new Dictionary<string, (string, Color, Vector2, float, bool)>
        {
            { "Fabric_main",  ("rough_linen", new Color(0.92f, 0.88f, 0.80f), new Vector2(0f, 0.22f), 0f, true) },
            { "Walnut",       ("american_walnut_veneer", new Color(0.50f, 0.33f, 0.20f), new Vector2(0.45f, 0.78f), 0f, true) },
            { "Marble_main",  ("marble_01", new Color(0.97f, 0.96f, 0.94f), new Vector2(0.82f, 0.97f), 0f, true) },
            { "BlackSteel",   (null, new Color(0.04f, 0.04f, 0.045f), new Vector2(0.78f, 0.78f), 1f, false) },
            { "Rug_main",     ("poly_wool_herringbone", new Color(0.90f, 0.85f, 0.76f), new Vector2(0f, 0.18f), 0f, true) },
            { "RugBorder",    ("rough_linen", new Color(0.50f, 0.40f, 0.31f), new Vector2(0f, 0.2f), 0f, true) },
            { "Cushion_main", ("rough_linen", new Color(0.60f, 0.68f, 0.54f), new Vector2(0f, 0.25f), 0f, true) },
        };

        [MenuItem("Tiramisu/Import furniture")]
        public static void ImportAll()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!imp) continue;

                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importNormals = ModelImporterNormals.Import;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

                foreach (var slot in SlotNames(path))
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), Material(slot));
                imp.SaveAndReimport();
            }
            Debug.Log("Tiramisu: furniture models imported and materials linked.");
        }

        static IEnumerable<string> SlotNames(string path)
        {
            var names = new HashSet<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m) names.Add(m.name);
            // materials already remapped are not sub assets any more, so read them from the renderers too
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m) names.Add(m.name);
            return names;
        }

        public static Material Material(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            if (!Looks.TryGetValue(name, out var l))
                return MaterialLibrary.Plain(path, new Color(0.6f, 0.6f, 0.6f), 0.3f);
            if (l.tex == null)
                return MaterialLibrary.Plain(path, l.tint, l.smooth.y, l.metal);
            return MaterialLibrary.Textured(path, l.tex, MaterialLibrary.Mapping.UV0, l.tint, l.smooth, l.metal, 1f, 1f, l.neutral);
        }
    }
}
