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

        private SpriteRenderer _sr;
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
            _sr = GetComponent<SpriteRenderer>();
        }

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
            }
        }

        /// <summary>The rig this lamp's state belongs to. The rest of the room's sources come from LightRig.</summary>
        public LightRig Rig(bool tubeOn, float roomLevel)
        {
            return LightRig.ForDesk(tubeOn, roomLevel);
        }
    }
}
