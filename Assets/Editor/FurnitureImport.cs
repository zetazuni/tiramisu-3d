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

        // name -> colour, smoothness, metallic. Unknown names get a neutral grey so they are easy to spot.
        static readonly Dictionary<string, (Color c, float smooth, float metal)> Looks = new Dictionary<string, (Color, float, float)>
        {
            { "Fabric_main", (new Color(0.78f, 0.72f, 0.64f), 0.12f, 0f) },
            { "Walnut",      (new Color(0.33f, 0.20f, 0.12f), 0.55f, 0f) },
            { "Marble_main", (new Color(0.94f, 0.93f, 0.91f), 0.9f, 0f) },
            { "BlackSteel",  (new Color(0.06f, 0.06f, 0.065f), 0.7f, 0.9f) },
            { "Rug_main",    (new Color(0.86f, 0.80f, 0.72f), 0.04f, 0f) },
            { "RugBorder",   (new Color(0.62f, 0.50f, 0.40f), 0.04f, 0f) },
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
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            var look = Looks.TryGetValue(name, out var l) ? l : (new Color(0.6f, 0.6f, 0.6f), 0.3f, 0f);
            m.SetColor("_BaseColor", look.Item1);
            m.SetFloat("_Smoothness", look.Item2);
            m.SetFloat("_Metallic", look.Item3);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
