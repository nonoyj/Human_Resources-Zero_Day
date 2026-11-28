// HR//0D -- editor menu that builds the desk scene from data.
// Assembly: HRZeroDay.Editor
// Owner: Dev A. Menu: HR//0D > Build Desk Scene
//
// It creates (or rewrites) Assets/Scenes/Desk.unity: camera sized for 480x270 at 16 PPU, the desk
// layer stack from desk_layout.json, the applicant with its 4-frame idle, and a Canvas with the
// hotspot collision boxes named after the artifacts so FE can hook input without hunting pixels.
using System.Collections.Generic;
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

        // Scene creation and asset import cannot run during Play mode: EditorSceneManager.NewScene
        // throws "This cannot be used during play mode", and asset changes made in Play mode are
        // discarded when it exits. Exit Play mode and ask the user to run the menu again in Edit
        // mode (ExitPlaymode is async, so the setup cannot continue in the same call).
        private static bool BlockIfPlaying(string action)
        {
            if (!EditorApplication.isPlaying) return false;
            EditorApplication.ExitPlaymode();
            Debug.LogError("[HR//0D] Cannot " + action + " during Play mode. Exiting Play mode -- " +
                           "run it again once the editor is back in Edit mode.");
            EditorUtility.DisplayDialog("HR//0D",
                "You are in Play mode.\n\n" + action + " needs Edit mode.\n\n" +
                "Play mode is exiting -- run the menu again once it stops.", "OK");
            return true;
        }

        [MenuItem("HR//0D/Setup Project (one click)", false, 0)]
        public static void SetupProject()
        {
            if (BlockIfPlaying("Setup Project")) return;
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

        // ---------------------------------------------------------------------------------------
        // Play-mode convenience, added 2026-11-25.
        //
        // The player's question: "do I always have to exit unity? It has very long loading times and
        // doing that all the time is tiring." No -- the cycle he actually needs is Play -> Stop -> Play,
        // because edits made in Play mode are DISCARDED by Unity when Play ends. Stopping is what
        // recompiles; the editor itself never has to close.
        //
        // These two menu items remove the last two reasons to leave the editor:
        //   Open Game        open the desk scene (or build it if it is missing) and enter Play mode.
        //   Reload Art Kit   re-run the publish + import steps without quitting, for when the PNGs
        //                    changed under an open editor (Unity does not reload them on focus).
        // ---------------------------------------------------------------------------------------
        [MenuItem("HR//0D/Open Game (play)", false, 3)]
        public static void OpenGame()
        {
            if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
            {
                Debug.Log("[HR//0D] no Desk scene yet -- building it first.");
                BuildDeskScene(true);
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("HR//0D/Reload Art Kit (open editor)", false, 12)]
        public static void ReloadArtKit()
        {
            if (BlockIfPlaying("Reload Art Kit")) return;
            ArtKitPublishMenu.PublishSilent();
            ArtKitImporter.ImportSilent();
            AssetDatabase.Refresh();
            Debug.Log("[HR//0D] art kit republished and reimported. Press Play.");
        }

        [MenuItem("HR//0D/Build Desk Scene", false, 1)]
        public static void BuildDeskScene() { BuildDeskScene(false); }

        private static void BuildDeskScene(bool silent)
        {
            if (BlockIfPlaying("Build Desk Scene")) return;
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
            if (BlockIfPlaying("Open Desk Scene")) return;
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
            {
                EditorUtility.DisplayDialog("HR//0D", "No scene yet. Run HR//0D > Build Desk Scene.", "OK");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// THE APPLICANT PIPELINE, REPAIRED FROM THE EDITOR, WHICH IS WHERE IT BROKE.
        ///
        /// The player's black screen carried: "app_human_solid_a_192x216.png has no frame list in the
        /// manifest". `DeskSceneBuilder.LoadFrames` does not slice a sheet -- it loads four separate FRAME
        /// files under .../applicants/frames/&lt;stem&gt;__&lt;name&gt;.png, and the manifest is what lists them.
        /// So the repair is two idempotent steps, both pure AssetDatabase work with no slicing API:
        ///   1. write any missing 192x216 frame PNG from the strip (4 cells, left to right);
        ///   2. make sure every strip is imported Point / uncompressed / readable.
        /// Slicing is deliberately NOT attempted: TextureImporter.spritesheet was removed in Unity 6, which is
        /// how an earlier stub made this gate green while the editor sliced nothing (see ASSEMBLY-RULES.md).
        /// </summary>
        private const int CELL_W = 192, CELL_H = 216;   // one frame of an applicant strip

        [MenuItem("HR//0D/Fix Applicant Frames (rebuild missing)", false, 11)]
        public static void FixApplicantFrames()
        {
            var manifest = Resources.Load<TextAsset>("unity_sprites");
            int written = 0, touched = 0;
            var root = "Assets/Resources/Art/Kit/applicants";
            if (!Directory.Exists(root))
            {
                EditorUtility.DisplayDialog("HR//0D",
                    "Assets/Resources/Art/Kit/applicants is missing.\n\nRun: python tools/art/publish_to_unity.py",
                    "OK");
                return;
            }
            // The strip exists twice on purpose: Assets/Art/Kit is the source the manifest names, and
            // Assets/Resources/Art/Kit is what the game loads. A strip that imported before its frames were
            // copied is exactly the black-screen state, so BOTH copies are repaired from ONE generator --
            // this file's. No python, no network, no extra menu step for the player to forget.
            var strips = new List<string>();
            strips.AddRange(Directory.GetFiles("Assets/Art/Kit/applicants", "*.png", SearchOption.TopDirectoryOnly));
            strips.AddRange(Directory.GetFiles(root, "*.png", SearchOption.TopDirectoryOnly));
            foreach (var stripPath in strips)
            {
                var stem = Path.GetFileNameWithoutExtension(stripPath);
                var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!srcTex.LoadImage(File.ReadAllBytes(stripPath))) continue;
                if (srcTex.width < CELL_W * 2) { UnityEngine.Object.DestroyImmediate(srcTex); continue; }

                var frameDir = Path.Combine(root, "frames");
                if (!Directory.Exists(frameDir)) Directory.CreateDirectory(frameDir);
                string[] names = ReadFrameNames(manifest, stem, srcTex.width / CELL_W);
                for (int i = 0; i < names.Length; i++)
                {
                    var dst = Path.Combine(frameDir, stem + "__" + names[i] + ".png");
                    if (File.Exists(dst)) continue;
                    var frame = new Texture2D(CELL_W, CELL_H, TextureFormat.RGBA32, false);
                    frame.SetPixels32(srcTex.GetPixels32().Length == 0
                        ? new Color32[CELL_W * CELL_H]
                        : CutCell(srcTex, i));
                    frame.Apply(false);
                    File.WriteAllBytes(dst, frame.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(frame);
                    written++;
                }
                UnityEngine.Object.DestroyImmediate(srcTex);

                var importer = AssetImporter.GetAtPath(stripPath) as TextureImporter;
                if (importer != null)
                {
                    // Same settings the frames get from write_unity_metas.py: Point, uncompressed, 16 px per
                    // unit. NOTE the mode: `spriteMode: 1` (Single) with an EMPTY slice list is the correct
                    // state for every sheet in this project -- the animation frames are separate files, so a
                    // sheet is never sliced (TextureImporter.spritesheet was removed in Unity 6 anyway). A
                    // sheet that some older pipeline left in Multiple is reset to Single here.
                    bool wrong = importer.filterMode != FilterMode.Point ||
                                 importer.textureCompression != TextureImporterCompression.Uncompressed ||
                                 importer.spriteImportMode != SpriteImportMode.Single;
                    if (wrong)
                    {
                        importer.filterMode = FilterMode.Point;
                        importer.isReadable = true;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.mipmapEnabled = false;
                        importer.spritePixelsPerUnit = 16f;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                        touched++;
                    }
                }
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("HR//0D",
                "Wrote " + written + " missing frame(s); fixed import settings on " + touched + " strip(s).\n\n" +
                "Then: HR//0D > Open Game (play)", "OK");
        }

        /// <summary>
        /// The four frame names, in MANIFEST order (Resources/unity_sprites.json). Nothing is guessed: the
        /// manifest is what DeskSceneBuilder.LoadFrames reads, so the repair must write exactly the names it
        /// will look for.
        ///
        /// WHY A SCANNER AND NOT JsonUtility: unity_sprites.json is a BARE ARRAY, and JsonUtility cannot
        /// deserialise a bare array -- that is the same reason the runtime reads it through SpriteManifest.
        /// A 30-line scanner that finds the entry whose assetPath contains the stem and then collects the
        /// "name" values up to the next "assetPath" is enough for a repair path, and it cannot silently
        /// reinterpret the file.
        /// </summary>
        private static string[] ReadFrameNames(TextAsset manifestJson, string stem, int cells)
        {
            var names = new List<string>();
            if (manifestJson != null)
            {
                var text = manifestJson.text;
                int at = text.IndexOf(stem, System.StringComparison.Ordinal);
                while (at >= 0 && names.Count == 0)
                {
                    int next = text.IndexOf("\"assetPath\"", at, System.StringComparison.Ordinal);
                    int endOfEntry = next < 0 ? text.Length : next;
                    int scan = at;
                    while (true)
                    {
                        int nameAt = text.IndexOf("\"name\"", scan, System.StringComparison.Ordinal);
                        if (nameAt < 0 || nameAt > endOfEntry) break;
                        int quote = text.IndexOf('"', nameAt + 6);
                        if (quote < 0) break;
                        int close = text.IndexOf('"', quote + 1);
                        if (close < 0) break;
                        var value = text.Substring(quote + 1, close - quote - 1);
                        if (value.Length > 0 && value.IndexOf(stem, System.StringComparison.Ordinal) >= 0)
                            names.Add(value);
                        scan = close + 1;
                    }
                    if (names.Count == 0) at = text.IndexOf(stem, at + stem.Length, System.StringComparison.Ordinal);
                }
            }
            if (names.Count == 0) names.AddRange(new[] { "neutral", "breathe", "breathe_fidget", "blink" });
            while (names.Count < cells && names.Count > 0) names.Add(names[names.Count - 1] + "_" + names.Count);
            return names.ToArray();
        }

        /// <summary>Cell i of a strip, as a top-down-free colour array for SetPixels32 (Unity is bottom-up).</summary>
        private static Color32[] CutCell(Texture2D strip, int index)
        {
            var all = strip.GetPixels32();
            var cell = new Color32[CELL_W * CELL_H];
            int sw = strip.width;
            for (int y = 0; y < CELL_H; y++)
                for (int x = 0; x < CELL_W; x++)
                    cell[y * CELL_W + x] = all[y * sw + index * CELL_W + x];
            return cell;
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
