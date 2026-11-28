// HR//0D -- LampView: the desk lamp on the right of the desk, and the light it throws.
// Assembly: HRZeroDay.Game
//
// THE PLAYER'S FOLLOW-UP (v130): "A followup regarding the lamp since I have not tested the game since what
// you did went wrong."
//
// WHAT THIS FIXES
//     The lamp's light has been a rule (LightingTests) and a bake (make_menu_room) for two rounds, and the
//     game's room had no lamp at all -- so there was nothing to test and no way for the player to see whether
//     the fix worked. There is now a lamp SPRITE on the desk's right end (art/kit/desk/lamp_M.png, the menu's
//     own lamp), a lamp LIGHT in LightRig, and a way to switch it: tap it, or press K.
//
// THE SWITCH
//     Off: the sprite loses one step of its own ramp (it is an object, not a glow, and an object that is on
//     is not necessarily lit). On: it gains it back, and the desk under it gains a warm pool within one
//     frame -- both are the same integer step operation the tube uses, so a lamp and a tube cannot look like
//     two different games.
//
// BUG-031 (the player's screenshot, v130.1): "[HR//0D] could not start the game: There is no
//     'SpriteRenderer' attached to the "Lamp" game object, but a script is trying to access it."
//     Why it happened, exactly: THIS component does not need a SpriteRenderer -- it drives the layer that
//     DeskSceneBuilder created for the lamp (L_lamp). But Awake() grabbed one anyway, and when the lamp
//     layer failed to exist the GameObject "Lamp" had no renderer to grab. AddComponent<MonoBehaviour>
//     throws only for a MISSING SCRIPT; a missing SpriteRenderer is a null, and a null dereferenced in
//     Awake is a caught exception in GameBoot -- which is why the desk never appeared at all.
//     The rule this file now follows (orders/09 A1): a view NEVER dereferences a component it did not
//     create. It is handed the renderer, or it does nothing and says so once.
//
// WHY A MONO BEHAVIOUR AND NOT A LIGHT2D
//     Same reason as ScreenLight: a Light2D blends, and this palette does not. See LightRig.cs's header.
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game
{
    public class LampView : MonoBehaviour
    {
        public const string RootName = "Lamp";

        /// <summary>Where the fixture stands, in canvas px (top-down). Mirrors desk_layout.json's lamp layer.</summary>
        public int x = 420, y = 83;
        /// <summary>0..1, ramped. The rig multiplies its lamp source by this.</summary>
        public float level = 1f;

        private SpriteRenderer _sr;      // may be null: the lamp LAYER is the renderer, not this object
        private bool _warned;
        private Color32 _on = new Color32(255, 255, 255, 255);
        private Color32 _off = new Color32(150, 150, 150, 255);

        public bool IsOn { get { return level > 0.5f; } }

        public static LampView Attach(Transform root)
        {
            var existing = root.Find(RootName);
            if (existing != null) return existing.GetComponent<LampView>();
            var go = new GameObject(RootName);
            go.transform.SetParent(root, false);
            return go.AddComponent<LampView>();
        }

        private void Awake()
        {
            // TryGetComponent, not GetComponent: this object is a CONTROLLER that stands next to the lamp
            // layer, and the layer may not exist yet (or at all). Null is a legal state here.
            if (!TryGetComponent(out _sr)) _sr = null;
        }

        /// <summary>The lamp layer's renderer, handed in by DeskSceneBuilder once the layer exists.</summary>
        public void Bind(SpriteRenderer renderer)
        {
            _sr = renderer;
            if (_sr != null) _sr.color = level > 0.5f ? _on : _off;
        }

        public bool HasRenderer { get { return _sr != null; } }

        /// <summary>Called every frame by DeskLightDriver. Cheap: only touches the renderer on a change.</summary>
        public void SetOn(bool on)
        {
            float target = on ? 1f : 0f;
            if (Mathf.Abs(level - target) < 0.001f) return;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * 6f);
            if (_sr != null)
            {
                // an OFF object is drawn one step down its own ramp, never made translucent: alpha stays 255
                _sr.color = level > 0.5f ? _on : _off;
                // ---- AND THE SHADE ITSELF REACTS (v130.5, BUG-040).
                // The player: "make everything that produces light actually make it visible that it produce
                // light". A tint on the whole 56x132 fixture is not visible -- it is mostly POLE, and a pole
                // does not light up. So the fixture shrinks toward its own base as it goes out: 3% is small
                // enough to stay on the pixel grid and big enough that the lamp visibly SETTLES when it is
                // switched. The shade's size is the honest tell, because the shade is what changes temperature.
                float k = 1f - 0.032f * (1f - level);
                _sr.transform.localScale = new Vector3(k, k, 1f);
            }
            else if (!_warned)
            {
                _warned = true;
                Debug.LogWarning("[HR//0D] the lamp is switching, but no renderer is bound to it -- the " +
                                 "light will change and the fixture will not. Run HR//0D > Build Desk Scene.");
            }
        }

        /// <summary>The rig this lamp's state belongs to. The rest of the room's sources come from LightRig.</summary>
        public LightRig Rig(bool tubeOn, float roomLevel)
        {
            return LightRig.ForDesk(tubeOn, IsOn, roomLevel);
        }
    }
}
