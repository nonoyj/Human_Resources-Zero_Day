// HR//0D -- one hotspot on the applicant. The editor builder adds this to every HS_* object.
// Uses EventSystems rather than the legacy Input API so it works with the Input System package
// and with touch. Hit targets are padded to 48 dp in the scene builder (standing rule: touch).
// Owner: Agent (interaction layer).
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HRZeroDay.Game.Interaction
{
    public sealed class HotspotInteractor : MonoBehaviour, IPointerDownHandler
    {
        public string artifact;

        public event Action<string> Tapped;

        public void OnPointerDown(PointerEventData eventData)
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
