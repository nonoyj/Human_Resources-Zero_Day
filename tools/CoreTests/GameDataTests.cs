// HR//0D -- tests for the ART -> UNITY data bridge, runnable without Unity.
//
// These catch the failures that would otherwise only show up on a phone:
//   * a sprite moved/renamed in art/kit but not in the manifest
//   * a sheet sliced into the wrong number of frames
//   * a hotspot that points outside the applicant sprite
//   * the published copy in unity/Assets drifting from art/kit
//   * the desk layout breaking the brief (monitor must be on the left half)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Tests
{
    public static class GameDataTests
    {
        private static readonly List<string> _scratch = new List<string>();

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "art", "kit")) &&
                                    Directory.Exists(Path.Combine(dir.FullName, "unity"))))
                dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
            return dir.FullName;
        }

        private static JsonDocument LoadJson(string rel, List<string> results)
        {
            var path = Path.Combine(RepoRoot(), rel);
            if (!File.Exists(path)) { results.Add("FAIL  manifest_missing " + rel); return null; }
            return JsonDocument.Parse(File.ReadAllText(path));
        }

        public static IEnumerable<string> RunAll()
        {
            yield return ManifestCoversEverySprite();
            yield return ApplicantSheetsSliceIntoFourFrames();
            yield return HotspotsInsideApplicant();
            yield return DeskLayoutMatchesBrief();
            yield return PublishedArtIsCurrent();
            yield return EveryArtifactReachesATell();
            yield return DeskFrontLayerSortsAboveApplicant();
        }

        private static string ManifestCoversEverySprite()
        {
            var results = new List<string>();
            using var doc = LoadJson("art/kit/unity_sprites.json", results);
            if (doc == null) return results[0];

            var root = RepoRoot();
            var listed = new HashSet<string>(
                doc.RootElement.GetProperty("sprites").EnumerateArray()
                    .Select(s => s.GetProperty("source").GetString()));

            var onDisk = Directory.GetFiles(Path.Combine(root, "art", "kit"), "*.png", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
                .Where(p => !p.Contains("/reference/"));

            var missing = onDisk.Where(p => !listed.Contains(p)).ToList();
            if (missing.Count > 0)
                return "FAIL  manifest_covers_every_sprite  not listed: " + string.Join(", ", missing.Take(3));

            return "PASS  Manifest_covers_every_sprite (" + listed.Count + " sprites)";
        }

        private static string ApplicantSheetsSliceIntoFourFrames()
        {
            var results = new List<string>();
            using var doc = LoadJson("art/kit/unity_sprites.json", results);
            if (doc == null) return results[0];

            foreach (var s in doc.RootElement.GetProperty("sprites").EnumerateArray())
            {
                if (s.GetProperty("kind").GetString() != "applicant") continue;
                var frames = s.GetProperty("sprites").EnumerateArray().ToList();
                if (frames.Count != 4)
                    return "FAIL  applicant_slices  " + s.GetProperty("source").GetString() +
                           " has " + frames.Count + " frames, expected 4";

                var names = frames.Select(f => f.GetProperty("name").GetString()).ToList();
                foreach (var want in new[] { "_neutral", "_breathe", "_breathe_fidget", "_blink" })
                    if (!names.Any(n => n.EndsWith(want, StringComparison.Ordinal)))
                        return "FAIL  applicant_slices  missing frame " + want;

                foreach (var f in frames)
                {
                    var r = f.GetProperty("rect");
                    if (r[2].GetInt32() != 192 || r[3].GetInt32() != 216)
                        return "FAIL  applicant_slices  wrong cell size in " + names.First();
                }
            }
            return "PASS  Applicant_sheets_slice_into_four_frames";
        }

        private static string HotspotsInsideApplicant()
        {
            var results = new List<string>();
            using var doc = LoadJson("unity/Assets/Content/desk_layout.json", results);
            if (doc == null) return results[0];

            foreach (var h in doc.RootElement.GetProperty("hotspots").EnumerateArray())
            {
                var r = h.GetProperty("rect");
                int x = r[0].GetInt32(), y = r[1].GetInt32(), w = r[2].GetInt32(), hh = r[3].GetInt32();
                var id = h.GetProperty("artifact").GetString();
                if (x < 0 || y < 0 || x + w > 192 || y + hh > 216)
                    return "FAIL  hotspot_bounds  " + id + " rect leaves the 192x216 applicant";
                if (w * hh < 100)
                    return "FAIL  hotspot_bounds  " + id + " is too small to tap (" + w + "x" + hh + ")";
            }
            return "PASS  Hotspots_inside_applicant";
        }

        private static string DeskLayoutMatchesBrief()
        {
            var results = new List<string>();
            using var doc = LoadJson("unity/Assets/Content/desk_layout.json", results);
            if (doc == null) return results[0];

            var canvas = doc.RootElement.GetProperty("canvas");
            int width = canvas.GetProperty("width").GetInt32();
            int height = canvas.GetProperty("height").GetInt32();
            if (width != 480 || height != 270) return "FAIL  desk_brief  canvas is not 480x270";
            if (canvas.GetProperty("pixelsPerUnit").GetInt32() != 16) return "FAIL  desk_brief  PPU is not 16";

            var layers = doc.RootElement.GetProperty("layers").EnumerateArray().ToList();
            var monitor = layers.First(l => l.GetProperty("id").GetString() == "monitor");
            var applicant = layers.First(l => l.GetProperty("id").GetString() == "applicant");
            int monitorX = monitor.GetProperty("anchor")[0].GetInt32();
            int appX = applicant.GetProperty("anchor")[0].GetInt32();

            if (monitorX >= width / 2) return "FAIL  desk_brief  monitor is not on the left half";
            if (appX + 96 <= width / 2) return "FAIL  desk_brief  applicant is not on the right half";

            var anim = applicant.GetProperty("animator");
            if (anim.GetProperty("frames").GetArrayLength() != 4) return "FAIL  desk_brief  applicant is not a 4-frame loop";

            return "PASS  Desk_layout_matches_brief (monitor left, applicant right, 480x270 @ 16 PPU)";
        }

        private static string PublishedArtIsCurrent()
        {
            var root = RepoRoot();
            var published = Path.Combine(root, "unity", "Assets", "Art", "Kit");
            if (!Directory.Exists(published))
                return "FAIL  published_art  unity/Assets/Art/Kit missing -- run tools/art/publish_to_unity.py";

            int checkedCount = 0;
            foreach (var src in Directory.GetFiles(Path.Combine(root, "art", "kit"), "*.png", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(Path.Combine(root, "art", "kit"), src);
                if (rel.StartsWith("reference")) continue;
                var dst = Path.Combine(published, rel);
                if (!File.Exists(dst))
                    return "FAIL  published_art  missing in Unity: " + rel.Replace('\\', '/');
                using var a = File.OpenRead(src);
                using var b = File.OpenRead(dst);
                const int N = 4096;
                var ba = new byte[N];
                var bb = new byte[N];
                int readA, readB;
                while (true)
                {
                    readA = a.Read(ba, 0, N);
                    readB = b.Read(bb, 0, N);
                    if (readA != readB) return "FAIL  published_art  size differs: " + rel.Replace('\\', '/');
                    if (readA == 0) break;
                    for (int i = 0; i < readA; i++)
                        if (ba[i] != bb[i]) return "FAIL  published_art  bytes differ: " + rel.Replace('\\', '/');
                }
                checkedCount++;
            }
            return "PASS  Published_art_is_current (" + checkedCount + " files byte-identical)";
        }

        private static string EveryArtifactReachesATell()
        {
            var results = new List<string>();
            using var doc = LoadJson("unity/Assets/Content/desk_layout.json", results);
            if (doc == null) return results[0];

            var tells = TellCatalog.All.Select(t => t.Id).ToList();
            var mapped = new Dictionary<string, string>
            {
                { "seam", TellCatalog.PortraitBlendSeam },
                { "mirror", TellCatalog.PortraitAsymmetry },
                { "no_blink", TellCatalog.PortraitAsymmetry },
            };

            foreach (var h in doc.RootElement.GetProperty("hotspots").EnumerateArray())
            {
                var id = h.GetProperty("artifact").GetString();
                var tell = mapped.ContainsKey(id) ? mapped[id] : null;
                if (tell != null && !tells.Contains(tell))
                    return "FAIL  artifact_to_tell  " + id + " maps to unknown tell " + tell;
                if (tell == null && !tells.Any(t => t.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    !HasConcept(id))
                    return "FAIL  artifact_to_tell  " + id + " has no Field Manual concept";
            }
            return "PASS  Every_artifact_reaches_a_tell";
        }

        /// <summary>Artifacts that are art-only for now (documented in art/kit/README.md).</summary>
        private static bool HasConcept(string artifact)
        {
            switch (artifact)
            {
                case "ear":
                case "glasses_merge":
                case "skin_band":
                case "hands":
                case "straight_badge":
                case "shadow":
                    return true;
                default:
                    return false;
            }
        }

        // ---- composition L (approved 2026-11-26) -----------------------------------------------
        // The desk must sort ABOVE the applicant so the keyboard and mug read as sitting in front of
        // him, and the applicant must be drawn at the approved 0.96 while still bigger than the screen.
        public static string DeskFrontLayerSortsAboveApplicant()
        {
            using var json = LoadJson("unity/Assets/Content/desk_layout.json", _scratch);
            if (json == null) return "FAIL  desk_layers unreadable";
            int deskOrder = int.MinValue, appOrder = int.MinValue;
            double appScale = 1.0;
            foreach (var layer in json.RootElement.GetProperty("layers").EnumerateArray())
            {
                var id = layer.GetProperty("id").GetString();
                var order = layer.GetProperty("sortingOrder").GetInt32();
                if (id == "desk") deskOrder = order;
                if (id == "applicant")
                {
                    appOrder = order;
                    if (layer.TryGetProperty("scaleFactor", out var sc)) appScale = sc.GetDouble();
                }
            }
            if (deskOrder == int.MinValue) return "FAIL  desk_layers no desk layer";
            if (deskOrder <= appOrder)
                return $"FAIL  desk_layers desk sorts {deskOrder} but applicant {appOrder} -- the keyboard would sit behind him";
            if (appScale <= 0.9 || appScale >= 1.0)
                return $"FAIL  desk_layers applicant scaleFactor {appScale} is not the approved 0.96";
            return "PASS  DeskFrontLayerSortsAboveApplicant (composition L)";
        }
    }
}
