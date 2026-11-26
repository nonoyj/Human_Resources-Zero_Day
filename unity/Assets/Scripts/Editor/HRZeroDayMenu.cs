// HR//0D -- editor menu that builds the desk scene from data.
// Assembly: HRZeroDay.Editor
// Owner: Dev A. Menu: HR//0D > Build Desk Scene
//
// It creates (or rewrites) Assets/Scenes/Desk.unity: camera sized for 480x270 at 16 PPU, the desk
// layer stack from desk_layout.json, the applicant with its 4-frame idle, and a Canvas with the
// hotspot collision boxes named after the artifacts so FE can hook input without hunting pixels.
using System.IO;
using HRZeroDay.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HRZeroDay.EditorTools
{
    public static class HRZeroDayMenu
    {
        private const string ScenePath = "Assets/Scenes/Desk.unity";

        [MenuItem("HR//0D/Build Desk Scene", false, 1)]
        public static void BuildDeskScene()
        {
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/desk_layout.json");
            if (layoutAsset == null)
            {
                EditorUtility.DisplayDialog("HR//0D",
                    "Assets/Resources/desk_layout.json is missing.\n\nRun in PowerShell:\n" +
                    "python tools/art/publish_to_unity.py", "OK");
                return;
            }

            var layout = JsonUtility.FromJson<DeskLayout>(layoutAsset.text);
            if (layout == null || layout.layers == null || layout.layers.Count == 0)
            {
                Debug.LogError("[HR//0D] desk_layout.json did not parse.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            DeskSceneBuilder.ConfigureCamera(cam, layout.canvas);

            // desk
            var root = DeskSceneBuilder.Build(layout, null);

            // hotspots: named empty boxes under the applicant, positioned in applicant-local space
            var appTransform = root.Find("L_applicant");
            if (appTransform != null)
            {
                var hotspots = new GameObject("Hotspots").transform;
                hotspots.SetParent(appTransform, false);
                float ppu = layout.canvas.pixelsPerUnit;
                float spriteW = 192f, spriteH = 216f;
                for (int i = 0; i < layout.hotspots.Count; i++)
                {
                    var hp = layout.hotspots[i];
                    var go = new GameObject("HS_" + hp.artifact);
                    go.transform.SetParent(hotspots, false);
                    // applicant-local px (y up, origin bottom-left) -> local units (pivot centre)
                    float x = (hp.rect[0] + hp.rect[2] * 0.5f - spriteW * 0.5f) / ppu;
                    float y = (hp.rect[1] + hp.rect[3] * 0.5f - spriteH * 0.5f) / ppu;
                    go.transform.localPosition = new Vector3(x, y, 0f);
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(hp.rect[2] / ppu, hp.rect[3] / ppu);
                    box.isTrigger = true;
                }
            }

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scenes"));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[HR//0D] built " + ScenePath + " -- " + layout.layers.Count + " layers, " +
                      layout.hotspots.Count + " hotspots. Next: HR//0D > Import Art Kit");
            EditorUtility.DisplayDialog("HR//0D",
                "Desk scene written to Assets/Scenes/Desk.unity\n\nNext steps:\n" +
                "1. HR//0D > Import Art Kit\n2. Open the scene and press Play\n" +
                "3. Check the Console for [HR//0D] import PASS", "OK");
        }

        [MenuItem("HR//0D/Open Desk Scene", false, 2)]
        public static void OpenDeskScene()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
            {
                EditorUtility.DisplayDialog("HR//0D", "No scene yet. Run HR//0D > Build Desk Scene.", "OK");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("HR//0D/Pipeline Self-Check", false, 30)]
        public static void PipelineSelfCheck()
        {
            var problems = 0;
            if (AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/unity_sprites.json") == null)
            { Debug.LogError("[HR//0D] missing Assets/Resources/unity_sprites.json"); problems++; }
            if (AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/desk_layout.json") == null)
            { Debug.LogError("[HR//0D] missing Assets/Resources/desk_layout.json"); problems++; }

            var applicant = AssetDatabase.LoadAssetAtPath<TextureImporter>(
                "Assets/Art/Kit/applicants/app_human_solid_a_192x216.png");
            if (applicant == null) { Debug.LogError("[HR//0D] art not imported at all"); problems++; }
            else
            {
                var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/unity_sprites.json");
                var entry = manifest == null ? null
                    : JsonUtility.FromJson<SpriteManifest>(manifest.text)
                        .Find("Assets/Art/Kit/applicants/app_human_solid_a_192x216.png");
                int frames = 0;
                if (entry != null && entry.sprites != null)
                {
                    for (int i = 0; i < entry.sprites.Count; i++)
                    {
                        var p = "Assets/Resources/Art/Kit/applicants/frames/app_human_solid_a_192x216__" +
                                entry.sprites[i].name + ".png";
                        if (AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null) frames++;
                    }
                }
                if (frames < 4)
                { Debug.LogError("[HR//0D] applicant frames missing (" + frames + "/4) -- run HR//0D > Import Art Kit"); problems++; }
            }

            if (problems == 0)
                Debug.Log("[HR//0D] pipeline self-check PASS -- data files present, applicant strip sliced.");
        }
    }
}
