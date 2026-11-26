// HR//0D -- world-space implementation of IEvidenceOverlay.
// The editor scene builder creates two children: MagnifierRing (an 9-slice panel sprite) and
// Callout (a panel sprite + two TextMesh labels). BE1/FE only has to wire the references;
// this class never decides what is evidence.
// Owner: Agent (interaction layer).
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DeskEvidenceOverlay : MonoBehaviour, IEvidenceOverlay
    {
        public Transform magnifierRing;
        public Transform callout;
        public TextMesh calloutLabel;
        public TextMesh calloutBody;

        public int referenceWidth = 480;
        public int referenceHeight = 270;
        public float pixelsPerUnit = 16f;
        public float zoom = 2f;

        // Callout sits in the top-right of the desk, clear of the face band.
        public float calloutX = 268f;
        public float calloutY = 196f;

        public void ShowMagnifier(Rect canvasRect, string label, string wording)
        {
            if (magnifierRing != null)
            {
                magnifierRing.gameObject.SetActive(true);
                magnifierRing.position = World(canvasRect.center);
                float w = canvasRect.width * zoom / pixelsPerUnit;
                float h = canvasRect.height * zoom / pixelsPerUnit;
                magnifierRing.localScale = new Vector3(w, h, 1f);
            }
            ShowText(label, wording);
        }

        public void ShowCallout(Rect canvasRect, string label, string wording)
        {
            if (magnifierRing != null) magnifierRing.gameObject.SetActive(false);
            ShowText(label, wording);
        }

        public void Hide()
        {
            if (magnifierRing != null) magnifierRing.gameObject.SetActive(false);
            if (callout != null) callout.gameObject.SetActive(false);
        }

        private void ShowText(string label, string wording)
        {
            if (callout == null) return;
            callout.gameObject.SetActive(true);
            callout.position = World(new Vector2(calloutX, calloutY));
            if (calloutLabel != null) calloutLabel.text = label;
            if (calloutBody != null) calloutBody.text = wording;
        }

        private Vector3 World(Vector2 canvasPoint)
        {
            return new Vector3(canvasPoint.x / pixelsPerUnit, canvasPoint.y / pixelsPerUnit, -1f);
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
