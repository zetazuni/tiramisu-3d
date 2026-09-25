using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tiramisu.EditorTools
{
    /// <summary>
    /// The film look (rule 5): high quality shadows, ambient occlusion, HDR sky,
    /// filmic tonemapping, bloom, grading, depth of field and reflection probes.
    /// Called by GreyboxBuilder; every number here is meant to be tuned by eye.
    /// </summary>
    public static class CinematicSetup
    {
        const string SkyHdr = "Assets/Art/Sky/kloofendal_partly_cloudy_2k.hdr";
        const string SkyMat = "Assets/Art/Sky/Sky.mat";
        const string ProfilePath = "Assets/Settings/Cinematic.asset";
        const string LightingPath = "Assets/Settings/Lighting.lighting";

        // ---------- pipeline asset and renderer ----------

        public static void ConfigurePipeline(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", 4);
            Set(so, "m_ShadowDistance", 80f);
            Set(so, "m_ShadowCascadeCount", 4);
            Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", 3);                 // High
            Set(so, "m_AdditionalLightsRenderingMode", 1);     // per pixel
            Set(so, "m_AdditionalLightShadowsSupported", true);
            Set(so, "m_AdditionalLightsShadowmapResolution", 2048);
            Set(so, "m_ReflectionProbeBlending", true);
            Set(so, "m_ReflectionProbeBoxProjection", true);
            Set(so, "m_ColorGradingMode", 1);                  // HDR grading
            Set(so, "m_ColorGradingLutSize", 64);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Forward+ so every room can have its own lights without a per object limit
            var rd = asset.rendererDataList.Length > 0 ? asset.rendererDataList[0] as UniversalRendererData : null;
            if (rd)
            {
                var rso = new SerializedObject(rd);
                Set(rso, "m_RenderingMode", 2);
                rso.ApplyModifiedPropertiesWithoutUndo();
                AddFeature(rd, "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", "Ambient Occlusion");
            }
            EditorUtility.SetDirty(asset);
        }

        static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"Tiramisu: pipeline setting {name} not found, skipped."); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break; // for enums this sets the value, not the list position
                case float f: p.floatValue = f; break;
            }
        }

        static void AddFeature(ScriptableRendererData rd, string typeName, string displayName)
        {
            foreach (var f in rd.rendererFeatures) if (f && f.GetType().FullName == typeName) return;
            System.Type type = null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<ScriptableRendererFeature>())
                if (t.FullName == typeName) { type = t; break; }
            if (type == null) { Debug.LogWarning($"Tiramisu: could not find {typeName}."); return; }

            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = displayName;
            AssetDatabase.AddObjectToAsset(feature, rd);
            rd.rendererFeatures.Add(feature);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(rd);
            var map = so.FindProperty("m_RendererFeatureMap");
            map.InsertArrayElementAtIndex(map.arraySize);
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
            AssetDatabase.SaveAssets();
        }

        // ---------- sky, fog and ambient ----------

        public static void Sky(Light sun)
        {
            var imp = AssetImporter.GetAtPath(SkyHdr) as TextureImporter;
            if (imp && imp.textureShape != TextureImporterShape.TextureCube)
            {
                imp.textureShape = TextureImporterShape.TextureCube;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyHdr);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMat);
            if (!mat)
            {
                mat = new Material(Shader.Find("Skybox/Cubemap"));
                AssetDatabase.CreateAsset(mat, SkyMat);
            }
            mat.SetTexture("_Tex", cube);
            mat.SetFloat("_Exposure", 1.0f);
            mat.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(mat);

            RenderSettings.skybox = mat;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.55f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.80f, 0.85f, 0.91f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 320f;
        }

        // ---------- post processing ----------

        public static Volume PostVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile) AssetDatabase.DeleteAsset(ProfilePath);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.85f));
            bloom.highQualityFiltering.Override(true);

            var grade = Add<ColorAdjustments>(profile);
            grade.postExposure.Override(-0.3f);
            grade.contrast.Override(14f);
            grade.saturation.Override(8f);

            var wb = Add<WhiteBalance>(profile);
            wb.temperature.Override(7f);
            wb.tint.Override(2f);

            var smh = Add<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.06f, 0f));    // cool shadows
            smh.highlights.Override(new Vector4(1.05f, 1.0f, 0.93f, 0f));  // warm highlights

            var vig = Add<Vignette>(profile);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);

            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

            var dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(36f);
            dof.focalLength.Override(95f);
            dof.aperture.Override(5.6f);
            dof.bladeCount.Override(6);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Film look (post processing)");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            return vol;
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        // ---------- room lights and reflection probes ----------

        public static void RoomLightAndProbe(Transform parent, string name, Vector3 floorCentre, Vector2 size, float ceiling, Color warm, bool light)
        {
            if (light)
            {
                var lg = new GameObject($"{name} light");
                lg.transform.SetParent(parent, false);
                lg.transform.position = floorCentre + Vector3.up * (ceiling - 0.4f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = warm;
                l.intensity = 0.6f; // subtle in daylight, the night lighting pass will raise it
                l.range = Mathf.Max(size.x, size.y) * 0.95f;
                l.shadows = LightShadows.None;
            }

            var pg = new GameObject($"{name} reflections");
            pg.transform.SetParent(parent, false);
            pg.transform.position = floorCentre + Vector3.up * (ceiling * 0.5f);
            var probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.size = new Vector3(size.x, ceiling, size.y);
            probe.blendDistance = 0.5f;
            probe.resolution = 256;
            probe.hdr = true;
            probe.importance = 2;
        }

        public static void GardenProbe(Transform parent, Vector3 centre, Vector3 size)
        {
            var pg = new GameObject("Garden reflections");
            pg.transform.SetParent(parent, false);
            pg.transform.position = centre;
            var probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = size;
            probe.resolution = 256;
            probe.hdr = true;
            probe.importance = 1;
        }

        // ---------- baking ----------

        /// <summary>Bakes the ambient light from the sky and every reflection probe. No lightmaps, so it is quick.</summary>
        public static void Bake()
        {
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!ls)
            {
                ls = new LightingSettings { name = "Lighting" };
                AssetDatabase.CreateAsset(ls, LightingPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            Lightmapping.lightingSettings = ls;
            Lightmapping.Bake();
        }
    }
}
