// HR//0D -- one hotspot on the applicant. The editor builder adds this to every HS_* object.
// Owner: Agent (interaction layer).
//
// WHY THIS DOES NOT USE IPointerDownHandler (rewritten 2026-11-26):
//   UnityEngine.EventSystems lives in the uGUI package assembly, and HRZeroDay.Game's asmdef does not
//   reference it, so the whole Game assembly failed to compile in the editor with
//   "CS0234: EventSystems does not exist in the namespace UnityEngine" -- the project dropped into Safe
//   Mode. The stubs had invented that namespace, so the local gate stayed green. Exactly the same class
//   of failure as TextureImporter.spritesheet: never assume an API is in the assembly you are writing in.
//
//   The layer this interacts with is already pure old-InputManager, so the hotspot uses the same thing:
//   `Input.GetMouseButtonDown(0)` also fires for the first touch on Android, which is all a 480x270
//   single-touch game needs. No package reference, no EventSystem object to build, nothing to uninstall.
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

        private Camera _camera;

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

        void Update()
        {
            if (string.IsNullOrEmpty(artifact)) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            float halfW = sizeInUnits.x * 0.5f;
            float halfH = sizeInUnits.y * 0.5f;
            Vector3 p = transform.position;
            if (world.x < p.x - halfW || world.x > p.x + halfW) return;
            if (world.y < p.y - halfH || world.y > p.y + halfH) return;

            OnPointerDown(null);
        }

        private void OnDisable()
        {
            Tapped = null;   // no stale listeners across scene changes (BUG-010 class of failure)
        }
    }
}
