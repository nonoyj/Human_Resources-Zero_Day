// HR//0D -- the bridge between art/kit/unity_sprites.json and the Unity importer.
//
// Everything here exists because of a specific, shipped bug:
//   * "0 sprites checked" in the editor -- JsonUtility silently returned empty entries because one class
//     had lost its [Serializable]. Nothing in Python or in the stubs could see it.
//   * the game loading bilinear PPU-100 textures -- the importer only touched Assets/Art/Kit, while the
//     game loads Assets/Resources/Art/Kit.
// Both are data-shape bugs, so they are tested as data, headlessly, on every run.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HRZeroDay.Tests
{
    internal static class ManifestBridgeTests
    {
        private const string ManifestPath = "unity/Assets/Resources/unity_sprites.json";

        public static IEnumerable<string> RunAll()
        {
            yield return CheckSpriteEntryIsSerializable();
            yield return CheckFrameNamesMatchThePathBuiltByTheImporter();
            yield return CheckFrameRectsStayInsideTheStrip();
            yield return CheckEveryFrameNameIsUnique();
        }

        /// <summary>
        /// JsonUtility only fills classes marked [Serializable]. Without it the manifest deserializes to 80
        /// entries with every field null -- and the importer reports "0 sprites checked" instead of failing.
        /// </summary>
        private static string CheckSpriteEntryIsSerializable()
        {
            var src = File.ReadAllText("unity/Assets/Scripts/Game/Layout/SpriteManifest.cs");
            foreach (var cls in new[] { "SpriteEntry", "SpriteFrameRect", "SpriteManifest" })
            {
                // the attribute must be the line (possibly with comment between) directly before the class
                var idx = src.IndexOf("class " + cls, StringComparison.Ordinal);
                if (idx < 0) return "FAIL  " + cls + " is missing from SpriteManifest.cs";
                var before = src.Substring(0, idx);
                var tail = before.Length > 400 ? before.Substring(before.Length - 400) : before;
                if (!Regex.IsMatch(tail, @"\[Serializable\][\s\S]*$") ||
                    Regex.IsMatch(tail.Substring(tail.LastIndexOf("[Serializable]", StringComparison.Ordinal) + 14),
                                  @"\bclass\b"))
                    return "FAIL  " + cls + " is not [Serializable] -- JsonUtility will silently empty it";
            }
            return "PASS  SpriteEntry, SpriteFrameRect and SpriteManifest are all [Serializable]";
        }

        /// <summary>
        /// Import Art Kit writes frames/<stem>__<frame>.png and LoadFrames asks for exactly that name.
        /// If the two ever drift, the game throws "frame not found" on a build that looked fine in the editor.
        /// </summary>
        private static string CheckFrameNamesMatchThePathBuiltByTheImporter()
        {
            var manifest = ParseManifest();
            if (manifest == null) return "FAIL  " + ManifestPath + " has no frame lists";

            var builder = File.ReadAllText("unity/Assets/Scripts/Game/Desk/DeskSceneBuilder.cs");
            if (!builder.Contains("frames/") || !builder.Contains("__"))
                return "FAIL  DeskSceneBuilder.LoadFrames no longer builds the frames/<stem>__<frame> path";
            if (!builder.Contains("entry.sprites[i].name"))
                return "FAIL  LoadFrames ignores the manifest frame order";

            // what has to hold is that BOTH sides concatenate stem + "__" + manifest frame name.
            if (!builder.Contains("FileNameNoExt") || !builder.Contains("__"))
                return "FAIL  LoadFrames no longer concatenates FileNameNoExt + \"__\" + frame name";
            if (!File.ReadAllText("unity/Assets/Scripts/Editor/ArtKitImporter.cs")
                     .Contains("stem + \"__\" + f.name"))
                return "FAIL  Import Art Kit writes a different file name than LoadFrames reads";
            var sample = manifest[0];
            return "PASS  frames are written and read as <stem>__<frame> (e.g. " + sample.stem + "__" +
                   sample.frames[0] + ".png)";
        }

        /// <summary>A rect outside the strip means GetPixels throws at import time on the user's machine.</summary>
        private static string CheckFrameRectsStayInsideTheStrip()
        {
            var json = File.ReadAllText(ManifestPath);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var n = 0;
            foreach (var e in doc.RootElement.GetProperty("sprites").EnumerateArray())
            {
                if (!e.TryGetProperty("sprites", out var frames)) continue;
                int w = e.GetProperty("width").GetInt32();
                int h = e.GetProperty("height").GetInt32();
                foreach (var f in frames.EnumerateArray())
                {
                    var r = f.GetProperty("rect").EnumerateArray().Select(v => v.GetInt32()).ToArray();
                    if (r.Length != 4) return "FAIL  a frame rect is not [x,y,w,h]";
                    if (r[0] < 0 || r[1] < 0 || r[2] <= 0 || r[3] <= 0)
                        return "FAIL  degenerate frame rect in " + e.GetProperty("assetPath").GetString();
                    if (r[0] + r[2] > w || r[1] + r[3] > h)
                        return "FAIL  frame rect escapes the sheet in " + e.GetProperty("assetPath").GetString();
                    n++;
                }
            }
            if (n == 0) return "FAIL  the manifest has no frame rects at all";
            return "PASS  " + n + " frame rects all sit inside their strips";
        }

        /// <summary>Two frames with one name = one file silently overwritten = a missing animation.</summary>
        private static string CheckEveryFrameNameIsUnique()
        {
            var manifest = ParseManifest();
            if (manifest == null) return "FAIL  " + ManifestPath + " has no frame lists";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, frames) in manifest)
                foreach (var f in frames)
                    if (!seen.Add(f)) return "FAIL  duplicate frame name: " + f;
            return "PASS  " + seen.Count + " frame names are unique";
        }

        private static List<(string stem, List<string> frames)> ParseManifest()
        {
            if (!File.Exists(ManifestPath)) return null;
            var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ManifestPath));
            var list = new List<(string, List<string>)>();
            foreach (var e in doc.RootElement.GetProperty("sprites").EnumerateArray())
            {
                if (!e.TryGetProperty("sprites", out var frames)) continue;
                var path = e.GetProperty("assetPath").GetString();
                var stem = Path.GetFileNameWithoutExtension(path);
                list.Add((stem, frames.EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList()));
            }
            return list.Count == 0 ? null : list;
        }
    }
}
