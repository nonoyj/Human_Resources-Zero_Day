// HR//0D -- editor menu that builds the desk scene from data.
// Assembly: HRZeroDay.Editor
// Owner: Dev A. Menu: HR//0D > Build Desk Scene
//
// It creates (or rewrites) Assets/Scenes/Desk.unity: camera sized for 480x270 at 16 PPU, the desk
// layer stack from desk_layout.json, the applicant with its 4-frame idle, and a Canvas with the
// hotspot collision boxes named after the artifacts so FE can hook input without hunting pixels.
using System.IO;
using HRZeroDay.Game;
using HRZeroDay.Game.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HRZeroDay.EditorTools
{
    public static class HRZeroDayMenu
    {
        private const string ScenePath = "Assets/Scenes/Desk.unity";

        [MenuItem("HR//0D/Setup Project (one click)", false, 0)]
        public static void SetupProject()
        {
            S("publishing art to Resources");
            ArtKitPublishMenu.PublishSilent();
            S("configuring sprite import settings and extracting frames");
            ArtKitImporter.ImportSilent();
            S("building and opening the desk scene");
            BuildDeskScene(true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[HR//0D] setup complete. Press Play. Controls: click a hotspot, L letter, " +
                      "1-5 flags/reason, A approve, S reject, SPACE continue, R restart.");
            EditorUtility.DisplayDialog("HR//0D - ready",
                "Project is ready. The Desk scene is open and is in Build Settings.\n\n" +
                "Press Play.\nClick the applicant to inspect; L opens the letter;\n" +
                "A / S stamps; SPACE advances; R restarts after the ending.", "OK");
        }

        private static void S(string message) { Debug.Log("[HR//0D] " + message + "\n"); }

        [MenuItem("HR//0D/Build Desk Scene", false, 1)]
        public static void BuildDeskScene() { BuildDeskScene(false); }

        private static void BuildDeskScene(bool silent)
        {
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/desk_layout.json");
            if (layoutAsset == null)
            {
                EditorUtility.DisplayDialog("HR//0D",
                    "Assets/Resources/desk_layout.json is missing.\n\nRun HR//0D > Setup Project (one click).", "OK");
                return;
            }

            var layout = JsonUtility.FromJson<DeskLayout>(layoutAsset.text);
            if (layout == null || layout.layers == null || layout.layers.Count == 0)
            {
                Debug.LogError("[HR//0D] desk_layout.json did not parse.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // No tag assignment: a project that never opened the Tag Manager can reject "MainCamera"
            // and leave a half-built scene behind. Runtime input looks for Camera.main by component.
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            DeskRuntimeScene.ConfigureCamera(cam, layout.canvas);
            DeskRuntimeScene.BuildGameplay(layout, null);

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scenes"));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();

            Debug.Log("[HR//0D] built " + ScenePath + " -- " + layout.layers.Count + " layers, " +
                      layout.hotspots.Count + " hotspots. Added to Build Settings.");
            if (silent) return;
            EditorUtility.DisplayDialog("HR//0D",
                "Desk scene written to Assets/Scenes/Desk.unity and added to Build Settings.\n\n" +
                "Press Play. For a fresh checkout, use Setup Project (one click) instead.", "OK");
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

            var atlas = AssetImporter.GetAtPath("Assets/Resources/fonts/monitor_font_5x7.png") as TextureImporter;
            if (atlas == null)
            { Debug.LogError("[HR//0D] font atlas not imported -- run HR//0D > Import Art Kit"); problems++; }
            else if (!atlas.isReadable)
            { Debug.LogError("[HR//0D] font atlas is not Read/Write -- the CRT cannot read its glyphs. Run HR//0D > Import Art Kit"); problems++; }

            if (problems == 0)
                Debug.Log("[HR//0D] pipeline self-check PASS -- data files present, applicant strip sliced.");
        }
    }
}
