// HR//0D -- one hotspot on the applicant. The editor builder adds this to every HS_* object.
// Owner: Agent (interaction layer).
//
// Pointer input is owned by GameInputRouter (new Input System). This component is deliberately a
// passive target: it exposes one entry point, OnPointerDown, and raises the artifact id to the
// session. Keeping the hit test in one place avoids a double inspect when both mouse and touch fire.
using System;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public sealed class HotspotInteractor : MonoBehaviour
    {
        public string artifact;

        /// <summary>Invisible tap target around the small art hotspot, in world units.</summary>
        public Vector2 sizeInUnits = Vector2.one;

        public event Action<string> Tapped;

        /// <summary>
        /// The single entry point for a tap. GameInputRouter calls it after its own screen-to-world test,
        /// so there is exactly one hit test in the game and it is the one with the collider maths in it.
        /// (This kept the name OnPointerDown after the EventSystem version was removed, so callers written
        /// against that name still work and there is no second code path to keep in sync.)
        /// </summary>
        public void OnPointerDown(object ignored)
        {
            if (string.IsNullOrEmpty(artifact)) return;
            if (Tapped != null) Tapped(artifact);
        }

        private void OnDisable()
        {
            Tapped = null;   // no stale listeners across scene changes (BUG-010 class of failure)
        }
    }
}
