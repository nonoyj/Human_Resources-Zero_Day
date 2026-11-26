// HR//0D -- tints the room by the shift clock. Behaviour layer over Core/Rules/Daylight.cs.
// Assembly: HRZeroDay.Game
//
// WHY A COMPONENT AND NOT A BAKED SPRITE: the shift length is variable (a session can be one applicant or
// ten), so the time of day cannot be painted in. This multiplies a tint over every room sprite each frame
// and, at the three stops, swaps the window/room background for the next painting.
//
// It never touches the monitor glass or the letter: those are the read-critical surfaces, and dimming
// them would break the one thing the game is about. Sorting by layer name keeps that rule explicit.
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game
{
    public class RoomLighting : MonoBehaviour
    {
        public const string RootName = "RoomLighting";

        /// <summary>Layers that must never be dimmed: the CRT and the paperwork.</summary>
        private static readonly string[] NeverTint = { "L_monitor", "MonitorScreen", "LetterRoot" };

        public Sprite[] stages;          // L0 (day), L1 (dusk), L2 (night); optional
        public SpriteRenderer roomLayer; // the L_background renderer to swap
        public float tintSpeed = 2.5f;   // how fast the tint eases when the clock jumps

        private SpriteRenderer[] _renderers;
        private SpriteRenderer[] _targets;
        private Color[] _base;
        private int _stage = -1;
        private float _r = 1f, _g = 1f, _b = 1f;
        private float _wantR = 1f, _wantG = 1f, _wantB = 1f;

        public static RoomLighting Attach(Transform root, Sprite[] stageSprites)
        {
            if (root == null) return null;
            var go = new GameObject(RootName);
            go.transform.SetParent(root, false);
            var lighting = go.AddComponent<RoomLighting>();
            lighting.stages = stageSprites;
            return lighting;
        }

        void Start()
        {
            // FindObjectsOfType is deprecated in 6.3 and Unity logs a CS0618 warning on every compile.
            // FindObjectsByType with SortMode.None is the supported call and is cheaper.
            _renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            var keep = new System.Collections.Generic.List<SpriteRenderer>();
            var baseColors = new System.Collections.Generic.List<Color>();
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (IsProtected(r.name)) continue;
                keep.Add(r);
                baseColors.Add(r.color);
                if (r.name == "L_background") roomLayer = r;
            }
            _targets = keep.ToArray();
            _base = baseColors.ToArray();
        }

        /// <summary>Called every frame by the desk driver with 0..1 shift progress.</summary>
        public void Apply(double progress)
        {
            var stop = Daylight.At(progress);
            _wantR = stop.R; _wantG = stop.G; _wantB = stop.B;

            int stage = RoomStage.SpriteIndex(progress);
            if (stage != _stage)
            {
                _stage = stage;
                if (roomLayer != null && stages != null && stages.Length > stage && stages[stage] != null)
                    roomLayer.sprite = stages[stage];
            }
        }

        void Update()
        {
            float t = Time.deltaTime * tintSpeed;
            _r = Mathf.Lerp(_r, _wantR, t);
            _g = Mathf.Lerp(_g, _wantG, t);
            _b = Mathf.Lerp(_b, _wantB, t);

            if (_targets == null) return;
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null) continue;
                var c = _base[i];
                _targets[i].color = new Color(c.r * _r, c.g * _g, c.b * _b, c.a);
            }
        }

        private static bool IsProtected(string name)
        {
            for (int i = 0; i < NeverTint.Length; i++)
                if (name != null && name.StartsWith(NeverTint[i], System.StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
