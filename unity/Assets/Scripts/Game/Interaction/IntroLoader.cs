// HR//0D -- IntroLoader: the JSON the content team writes, as a script the game can show.
// Assembly: HRZeroDay.Game  (UnityEngine for Resources only -- no layout, no drawing)
//
// THE FILE (docs/21-content-pack-format.md, section "intro"):
//   Resources/Content/intro/intro_01.json
//   { "id": "intro_01", "version": 1,
//     "beats": [ { "speaker": "DISPATCH", "text": "ONE LINE OF STORY.", "hold": 2.4 } ] }
//
// WHERE IT SHIPS FROM: content/intro/intro_01.json in the repo, published to Resources/Content by the art/
// content pipeline (tools/art/publish_to_unity.py already mirrors content/ -> Resources/Content).
//
// EVERY FAILURE IS THE SAME FAILURE: no intro, and the desk starts. Missing file, unreadable JSON, no beats,
// beats the atlas cannot paint. The Console gets ONE line naming the reason so the content team can see it;
// the player gets the game. That is rule 1 in IntroView and it is enforced here, at the only place the file
// is read.
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public static class IntroLoader
    {
        /// <summary>Resources path without the extension. Resources.Load&lt;TextAsset&gt; resolves ".json".</summary>
        public const string ResourcePath = "Content/intro/intro_01";

        /// <summary>The script, or a script with no beats and a Problem. Never null.</summary>
        public static IntroScript Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
                return IntroScript.None("no intro script at Resources/" + ResourcePath + ".json -- the desk " +
                                      "starts immediately (this is not an error)");

            IntroFile file = null;
            try { file = JsonUtility.FromJson<IntroFile>(asset.text); }
            catch (System.Exception e)
            {
                return IntroScript.None("the intro script is not valid JSON (" + e.GetType().Name + ") -- " +
                                        "the desk starts immediately");
            }
            if (file == null || file.beats == null || file.beats.Count == 0)
                return IntroScript.None("the intro script has no beats -- the desk starts immediately");

            var raw = new List<IntroBeat>();
            foreach (var b in file.beats)
            {
                if (b == null) continue;
                raw.Add(new IntroBeat { Speaker = b.speaker, Text = b.text, Hold = b.hold });
            }

            var script = IntroScriptRules.Validate(file.id, file.version, raw);
            if (!string.IsNullOrEmpty(script.Problem))
            {
                // "usable, with problems: ..." is a warning; "no beats" is the fallback path
                if (script.HasBeats) Debug.LogWarning("[HR//0D] intro: " + script.Problem);
                else Debug.Log("[HR//0D] intro skipped: " + script.Problem);
            }
            return script;
        }

        [System.Serializable]
        private class IntroFile
        {
            public string id;
            public int version;
            public List<Beat> beats;
        }

        [System.Serializable]
        private class Beat
        {
            public string speaker;
            public string text;
            public float hold;
        }
    }
}
