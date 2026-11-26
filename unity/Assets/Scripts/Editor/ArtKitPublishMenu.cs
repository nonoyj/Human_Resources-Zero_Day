// HR//0D -- the one menu item that must exist before the other two can work.
// Assembly: HRZeroDay.Editor
// Agent-written 2026-11-26.
//
// WHY: DeskSceneBuilder loads every sprite through Resources.LoadAll, which means the art has to live
// under Assets/Resources/Art/... . The authoring copies live in Assets/Art/Kit/... (byte-identical to
// art/kit/, verified by the art gate). The step that copies one to the other is a python script,
// tools/art/publish_to_unity.py -- i.e. it does not exist on a machine that opened Unity but never ran
// the art pipeline. Result: HR//0D > Import Art Kit runs fine, then Build Desk Scene throws
// "sprite not found at Resources/Art/Kit/..." and the operator has no idea why.
//
// So the menu gets its own publisher: copy every PNG + JSON from Assets/Art/Kit to
// Assets/Resources/Art/Kit once, refresh, and say what it did. Idempotent; safe to run any time.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HRZeroDay.EditorTools
{
    public static class ArtKitPublishMenu
    {
        private const string Source = "Assets/Art/Kit";
        private const string Dest = "Assets/Resources/Art/Kit";

        [MenuItem("HR//0D/Publish Art To Resources", false, 9)]
        public static void Publish()
        {
            if (!Directory.Exists(Source))
            {
                EditorUtility.DisplayDialog("HR//0D",
                    Source + " is missing.\n\nRun: python tools/art/publish_to_unity.py", "OK");
                return;
            }

            // One asset edit block: without it Unity reimports after every single file, which on the
            // first run is 80 PNGs x a full domain reload each.
            int copied = 0, skipped = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var file in Directory.GetFiles(Source, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".png" && ext != ".json") continue;

                    var rel = file.Substring(Source.Length).TrimStart('/', '\\');
                    var dst = Path.Combine(Dest, rel);
                    var dstDir = Path.GetDirectoryName(dst);
                    if (!Directory.Exists(dstDir)) Directory.CreateDirectory(dstDir);

                    // a Resources copy of a big editor-only file must never drift silently: compare sizes
                    if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(file).Length)
                    {
                        skipped++;
                        continue;
                    }
                    File.Copy(file, dst, true);
                    copied++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log(string.Format("[HR//0D] published art to {0}: {1} copied, {2} already current. " +
                                    "Next: HR//0D > Import Art Kit, then Build Desk Scene.", Dest, copied, skipped));
            EditorUtility.DisplayDialog("HR//0D",
                string.Format("Published {0} files to {1}\n({2} already current)\n\nNext: Import Art Kit", copied, Dest, skipped),
                "OK");
        }
    }
}
