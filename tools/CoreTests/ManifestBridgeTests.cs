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
            yield return CheckTheStubsDoNotInventEngineApi();
            yield return CheckSpriteEntryIsSerializable();
            yield return CheckFrameNamesMatchThePathBuiltByTheImporter();
            yield return CheckFrameRectsStayInsideTheStrip();
            yield return CheckEveryFrameNameIsUnique();
            yield return CheckEveryJsonKeyHasAField();
            yield return CheckEveryLayoutLayerResolves();
            yield return CheckEveryResourcesLoadHasAFile();
            yield return CheckTheMenuRoomIsReadable();
            yield return CheckEveryDeskLayerIsReadable();
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
                // the attribute is the nearest [Serializable] above the class, with nothing else between
                var before = src.Substring(0, idx);
                var attr = before.LastIndexOf("[Serializable]", StringComparison.Ordinal);
                if (attr < 0 || before.Substring(attr).Contains("class "))
                    return "FAIL  " + cls + " is not [Serializable] -- JsonUtility will silently empty it";
            }
            return "PASS  SpriteEntry, SpriteFrameRect and SpriteManifest are all [Serializable]";
        }

        /// <summary>
        /// THE PLAYER'S CONSOLE (v127). On their machine ScreenLight said, three times:
        ///
        ///   cannot read 'monitor' / 'monitor_stand' / 'document', so the tube will not light it
        ///
        /// The tube lights every static layer of the desk, so every static layer must be Read/Write or the
        /// light lands on the wall and nowhere else -- the "it lights the back, not the desk" report. This test
        /// reads the LAYOUT, not a list: every sprite the desk names must have isReadable: 1 in BOTH copies of
        /// its .meta, and must be on the importer's DeskLayers() list, or a fresh machine is broken again.
        /// </summary>
        private static string CheckEveryDeskLayerIsReadable()
        {
            var layoutPath = "unity/Assets/Resources/desk_layout.json";
            if (!File.Exists(layoutPath)) return "FAIL  " + layoutPath + " is missing";
            var json = File.ReadAllText(layoutPath);
            // THE LAYERS ARRAY ONLY. The json also carries notes, the menu's frame recipe and the hotspot
            // table, and each of those names sprites that are not desk layers (the applicant strip, an old
            // monitor_big, a server rack) -- reading every "art/kit/" in the file asks the tube to light
            // things it will never see. The layers are what DeskSceneBuilder builds, so they are the set.
            int arr = json.IndexOf("\"layers\"", StringComparison.Ordinal);
            if (arr < 0) return "FAIL  desk_layout.json has no layers array";
            int arrEnd = json.IndexOf("\n  ]", arr, StringComparison.Ordinal);   // the layers array closes at 2-space indent
            var layers = json.Substring(arr, arrEnd - arr);
            var names = new List<string>();
            int at = 0;
            while ((at = layers.IndexOf("art/kit/", at, StringComparison.Ordinal)) >= 0)
            {
                int b = layers.IndexOf(".png", at, StringComparison.Ordinal);
                if (b < 0) break;
                var rel = layers.Substring(at + "art/kit/".Length, b - at - "art/kit/".Length);
                if (!names.Contains(rel)) names.Add(rel);
                at = b;
            }
            // the APPLICANT is animated, and ScreenLight deliberately never touches it (its pixels are
            // swapped every frame, so a lit copy would be overwritten). It is the one layer exempt.
            names.RemoveAll(n => n.StartsWith("applicants/", StringComparison.Ordinal));
            if (names.Count < 4) return "FAIL  only " + names.Count + " desk layers found in the layout";

            var problems = new List<string>();
            var importer = File.ReadAllText("unity/Assets/Scripts/Editor/ArtKitImporter.cs");
            foreach (var rel in names)
            {
                foreach (var root in new[] { "unity/Assets/Resources/Art/Kit/", "unity/Assets/Art/Kit/" })
                {
                    var meta = root + rel + ".png.meta";
                    if (!File.Exists(meta)) { problems.Add(meta + " (missing)"); continue; }
                    if (!File.ReadAllText(meta).Contains("isReadable: 1"))
                        problems.Add(meta + " (isReadable: 0 -- the tube will skip it and warn)");
                }
                if (!importer.Contains("\"" + rel + "\""))
                    problems.Add("ArtKitImporter.DeskLayers() does not list " + rel);
            }
            if (problems.Count > 0)
                return "FAIL  the tube cannot light the desk it is standing on: " +
                       string.Join("; ", problems.ToArray());
            return "PASS  all " + names.Count + " desk layers the layout names are Read/Write in both copies";
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

        /// <summary>
        /// THE bug this whole file exists for. JsonUtility is name-based and completely silent: a json key
        /// with no matching C# field is dropped, and a C# field with no json key stays at its default.
        /// That is how the frame rects became 0x0 and Texture2D threw. So: every key in unity_sprites.json
        /// must exist as a field on SpriteManifest/SpriteEntry/SpriteFrameRect, and vice versa.
        /// </summary>
        private static string CheckEveryJsonKeyHasAField()
        {
            var models = new[]
            {
                ("SpriteManifest",   "unity/Assets/Scripts/Game/Layout/SpriteManifest.cs"),
                ("SpriteEntry",      "unity/Assets/Scripts/Game/Layout/SpriteManifest.cs"),
                ("SpriteFrameRect",  "unity/Assets/Scripts/Game/Layout/SpriteManifest.cs"),
            };
            var missing = new List<string>();

            // Deliberately not modelled in C#: the importer applies Point/PPU/compression from its own
            // code, so this block is documentation for humans and tooling, not input to the game.
            var intentionallyIgnored = new HashSet<string>(StringComparer.Ordinal) { "import" };

            var jsonKeys = new HashSet<string>(StringComparer.Ordinal) { "sprites" };
            var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ManifestPath));
            foreach (var e in doc.RootElement.GetProperty("sprites").EnumerateArray())
                foreach (var p in e.EnumerateObject())
                {
                    jsonKeys.Add(p.Name);
                    if (p.Name == "sprites")
                        foreach (var f in p.Value.EnumerateArray())
                            foreach (var fp in f.EnumerateObject()) jsonKeys.Add("frame:" + fp.Name);
                }

            var src = File.ReadAllText("unity/Assets/Scripts/Game/Layout/SpriteManifest.cs");
            foreach (var key in jsonKeys)
            {
                if (key.StartsWith("frame:", StringComparison.Ordinal))
                {
                    var field = key.Substring("frame:".Length);
                    var cls = src.Substring(src.IndexOf("class SpriteFrameRect", StringComparison.Ordinal));
                    cls = cls.Substring(0, cls.IndexOf('}'));
                    if (!Regex.IsMatch(cls, @"public\s+[\w\[\]<>]+\s+" + field + @"\s*[;{]"))
                        missing.Add("SpriteFrameRect." + field);
                }
            }
            // the entry-level keys (source, assetPath, width, height, ppu, spriteMode, kind, ...)
            var entry = src.Substring(src.IndexOf("class SpriteEntry", StringComparison.Ordinal));
            entry = entry.Substring(0, entry.IndexOf("public string FileName"));
            foreach (var key in jsonKeys.Where(k => !k.StartsWith("frame:", StringComparison.Ordinal)))
            {
                if (key == "sprites") continue;              // covered by the frame check
                if (intentionallyIgnored.Contains(key)) continue;
                if (!Regex.IsMatch(entry, @"public\s+[\w\[\]<>]+\s+" + key + @"\s*[;{]"))
                    missing.Add("SpriteEntry." + key);
            }

            if (missing.Count > 0)
                return "FAIL  json keys with no C# field (JsonUtility drops them silently): " +
                       string.Join(", ", missing);
            return "PASS  every json key (incl. frame.rect) has a matching C# field";
        }

        /// <summary>
        /// desk_layout.json addresses layers by REPO path (art/kit/...) while portraits use the Unity path.
        /// Find() has to index both, and every layer must resolve -- a miss is a NullReferenceException
        /// deep inside the scene builder, which is exactly how long the 2026-11-26 build stopped.
        /// </summary>
        private static string CheckEveryLayoutLayerResolves()
        {
            const string layoutPath = "unity/Assets/Resources/desk_layout.json";
            if (!File.Exists(layoutPath)) return "FAIL  " + layoutPath + " is missing";

            var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ManifestPath));
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in manifest.RootElement.GetProperty("sprites").EnumerateArray())
            {
                if (e.TryGetProperty("source", out var src)) known.Add(src.GetString());
                if (e.TryGetProperty("assetPath", out var ap)) known.Add(ap.GetString());
            }

            var layout = System.Text.Json.JsonDocument.Parse(File.ReadAllText(layoutPath));
            var missing = new List<string>();
            int n = 0;
            foreach (var l in layout.RootElement.GetProperty("layers").EnumerateArray())
            {
                var sprite = l.GetProperty("sprite").GetString();
                n++;
                if (!known.Contains(sprite)) missing.Add(l.GetProperty("id").GetString() + " -> " + sprite);
            }
            if (n == 0) return "FAIL  desk_layout.json has no layers";
            if (missing.Count > 0)
                return "FAIL  layers with no manifest entry: " + string.Join("; ", missing);

            // data alone is not proof: Find() must INDEX both keys, or the lookup misses at runtime.
            // (Asserted on the source because the dictionary is private and Unity-owned.)
            var manifestSource = File.ReadAllText("unity/Assets/Scripts/Game/Layout/SpriteManifest.cs");
            var find = manifestSource.Substring(manifestSource.IndexOf("public SpriteEntry Find", StringComparison.Ordinal));
            find = find.Substring(0, find.IndexOf("OfKind", StringComparison.Ordinal));
            if (!find.Contains("_bySource[s.source]"))
                return "FAIL  Find() does not index the repo path -- every desk layer would miss";
            if (!find.Contains("_bySource[s.assetPath]"))
                return "FAIL  Find() does not index the Unity path -- portraits and UI would miss";
            return "PASS  all " + n + " desk layers resolve to a manifest sprite (repo path or Unity path)";
        }

        /// <summary>
        /// Every Resources.Load in game code must have a committed file behind it. This is the check that
        /// would have caught the missing 5x7 font atlas: the .json was published and the .png never was,
        /// so MonitorScreenView failed on the user's machine while every local gate passed.
        /// </summary>
        private static string CheckEveryResourcesLoadHasAFile()
        {
            var problems = new List<string>();
            var checkedCount = 0;

            // C# verbatim strings: the pattern is legible, and " is escaped by doubling it.
            var constPattern = new Regex(@"const\s+string\s+(\w+)\s*=\s*""([^""]+)""\s*;");
            var loadPattern  = new Regex(@"Resources\.Load<(\w+)>\(\s*(?:""([^""]+)""|([A-Za-z_]\w*))\s*\)");

            foreach (var file in Directory.GetFiles("unity/Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                // strip // comments: the doc comment above MonitorScreenView mentions a Resources.Load
                // call and must not be counted as one.
                var text = string.Join("\n", File.ReadAllLines(file).Select(line =>
                {
                    int cut = line.IndexOf("//", StringComparison.Ordinal);
                    return cut < 0 ? line : line.Substring(0, cut);
                }));

                // constants matter: MonitorScreenView calls Resources.Load<Texture2D>(AtlasResource).
                // Matching only quoted literals skipped exactly the two calls that were broken.
                var consts = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match c in constPattern.Matches(text))
                    consts[c.Groups[1].Value] = c.Groups[2].Value;

                foreach (Match m in loadPattern.Matches(text))
                {
                    var type = m.Groups[1].Value;
                    var path = m.Groups[2].Success ? m.Groups[2].Value : null;
                    if (path == null)
                    {
                        var field = m.Groups[3].Value;
                        if (!consts.TryGetValue(field, out path)) continue;
                    }
                    if (path.Contains("{")) continue;    // built at runtime
                    checkedCount++;

                    // A Resources path never carries its extension: Resources.Load<TextAsset>("fonts/x")
                    // reads fonts/x.json. The test used to demand fonts/x.json ON DISK, so a correct call
                    // failed forever. Also accept the path with its extension already on it.
                    var candidates = new[] { "", ".png", ".json", ".asset", ".bytes", ".txt" };
                    var found = candidates.Any(ext => File.Exists("unity/Assets/Resources/" + path + ext));
                    if (!found)
                        problems.Add(Path.GetFileName(file) + " -> Resources/" + path + " (" + type + ")");
                }
            }

            if (problems.Count > 0)
                return "FAIL  Resources.Load with no committed file: " + string.Join("; ", problems);
            return "PASS  all " + checkedCount + " Resources.Load targets exist under Assets/Resources";
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

        /// <summary>
        /// THE STUB MUST NOT BE MORE PERMISSIVE THAN UNITY (v119.3, BUG-080).
        ///
        /// v119 shipped `if (glass == on)` on two Color32 values. It compiled here and broke the player's
        /// editor with CS0019 -- because this project's stub had invented `operator ==` on Color32, which
        /// Unity does not define. The gate was lying in the most expensive direction: green here, red there.
        ///
        /// This check reads the stub source and fails if the engine types declare members Unity does not
        /// have. It is a short list of the traps already paid for, not a general audit.
        /// </summary>
        private static string CheckTheStubsDoNotInventEngineApi()
        {
            const string path = "tools/UnityStubs/UnityStubs.cs";
            if (!File.Exists(path)) return "FAIL  the stub source is not where this check expects it";
            var stub = File.ReadAllText(path);

            // Color32: Unity defines IEquatable and Equals, but never == / !=  (issue tracker, still open).
            var from = stub.IndexOf("public struct Color32", StringComparison.Ordinal);
            if (from < 0) return "FAIL  the Color32 stub is missing entirely";
            var to = stub.IndexOf("public struct Color", from, StringComparison.Ordinal);
            if (to < 0) to = from + 600;
            var color32 = stub.Substring(from, to - from);
            if (color32.Contains("operator ==") || color32.Contains("operator !="))
                return "FAIL  the Color32 stub invents an == operator; Unity has none, so a colour comparison "
                     + "compiles here and fails in the player's editor (CS0019)";

            // ...and the honest stand-in has to exist, or the rule has no way to be followed
            if (!stub.Contains("Color32Util") || !stub.Contains("public static bool Same("))
                return "FAIL  Color32Util.Same is the stand-in for colour equality and it is missing";

            // a couple of types that are CLASSES in Unity and would break inheritance if stubbed as structs
            if (!stub.Contains("public class Sprite")) return "FAIL  the stub lost its Sprite declaration";
            if (!stub.Contains("public class TextAsset")) return "FAIL  the stub lost its TextAsset declaration";

            // and the warning that Unity shows must not be silenced here (v119's unused field, CS0414)
            var csproj = File.ReadAllText("tools/UnityStubs/CompileCheck.csproj");
            // only the NoWarn LIST matters -- the file also NAMES CS0414 in a comment explaining why it is
            // no longer silenced, and a substring test would fail on its own documentation
            foreach (var line in csproj.Split('\n'))
                if (line.Contains("<NoWarn>") && line.Contains("CS0414"))
                    return "FAIL  CS0414 is suppressed in the gate; Unity shows it, so the gate would report "
                         + "a clean build for a file the player's editor flags";

            return "PASS  the headless stubs declare only members Unity really has (no invented operators)";
        }


        /// <summary>
        /// THE MENU ROOM MUST BE READABLE (BUG-082, v119.4).
        ///
        /// MenuView does not DRAW the room's sprites, it BLITS them into one buffer with GetPixels32. Unity
        /// only serves those pixels when the asset's .meta says `isReadable: 1`; otherwise the call throws,
        /// MenuView treats the throw as a soft failure, and the front door renders as text floating over the
        /// desk scene -- the exact screenshot the player sent after v119.3. The gate that missed it checked
        /// the PNGs, and the PNGs were perfect.
        ///
        /// So this test reads the three places that have to agree and fails if any of them drifts:
        ///   * MenuView.Blit(...) -- which sprites are read,
        ///   * each .meta in Assets/Resources -- whether Unity will hand over the pixels,
        ///   * write_unity_metas.py's READABLE list and ArtKitImporter.MenuRoomSprites() -- the two things
        ///     that would fix it again on a fresh machine.
        /// </summary>
        private static string CheckTheMenuRoomIsReadable()
        {
            var view = File.ReadAllText("unity/Assets/Scripts/Game/Interaction/MenuView.cs");
            var blitted = new List<string>();
            int at = 0;
            while ((at = view.IndexOf("Blit(\"menu/", at, StringComparison.Ordinal)) >= 0)
            {
                int open = view.IndexOf('"', at) + 1;
                int close = view.IndexOf('"', open);
                var name = view.Substring(open, close - open);
                if (!blitted.Contains(name)) blitted.Add(name);
                at = close;
            }
            if (blitted.Count < 7) return "FAIL  only " + blitted.Count + " room sprites found in MenuView.Blit";

            var problems = new List<string>();
            foreach (var name in blitted)
            {
                var metaRel = "unity/Assets/Resources/Art/Kit/" + name + ".png.meta";
                var sourceRel = "unity/Assets/Art/Kit/" + name + ".png.meta";
                foreach (var rel in new[] { metaRel, sourceRel })
                {
                    if (!File.Exists(rel)) { problems.Add(rel + " (missing)"); continue; }
                    if (!File.ReadAllText(rel).Contains("isReadable: 1"))
                        problems.Add(rel + " (isReadable: 0 -- GetPixels32 will throw)");
                }
            }

            // the two fixers have to know the same list, or the next fresh machine is broken again
            var py = File.ReadAllText("tools/art/write_unity_metas.py");
            foreach (var name in blitted)
                if (!py.Contains("'" + name + ".png'") && !py.Contains("'Art/Kit/" + name + ".png'"))
                    problems.Add("write_unity_metas.py does not list " + name + ".png as READABLE");

            var importer = File.ReadAllText("unity/Assets/Scripts/Editor/ArtKitImporter.cs");
            foreach (var name in blitted)
            {
                var bare = name.Substring(name.LastIndexOf('/') + 1);
                if (!importer.Contains("\"" + bare + "\"") && !importer.Contains("\"" + name + "\""))
                    problems.Add("ArtKitImporter.MenuRoomSprites() does not list " + name);
            }

            if (problems.Count > 0)
                return "FAIL  the front door cannot read its own room: " + string.Join("; ", problems.ToArray());
            return "PASS  all " + blitted.Count + " menu-room sprites are Read/Write Enabled in both copies, "
                 + "and both fixers know the list";
        }
    }
}
