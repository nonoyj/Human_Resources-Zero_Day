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

        // Callout sits in the top-right of the desk, clear of the face band. NOTE what these are used
        // for in World(): a canvas PIXEL position converted to units directly -- NOT rounded down to
        // whole units, which is what the scene builder used to do for the initial placement and is why
        // the callout appeared in the middle of the room. ShowText repositions it on every show.
        public float calloutX = 268f;
        public float calloutY = 196f;

        /// <summary>
        /// Where the callout will cover. Used by the hit test to decide "you touched the callout, not the
        /// desk" -- the panel is 10 x 3 sprite-units scaled from a 9-slice, so its footprint in canvas px
        /// is derived here once instead of being guessed in the router.
        /// </summary>
        public Rect CalloutCanvasRect()
        {
            const float panelWUnits = 10f, panelHUnits = 3f;    // Panel's localScale in the builder
            float w = panelWUnits * pixelsPerUnit, h = panelHUnits * pixelsPerUnit;
            return new Rect(calloutX - w * 0.5f, calloutY - h * 0.5f, w, h);
        }

        public bool Contains(Vector2 canvasPoint)
        {
            if (callout == null || !callout.gameObject.activeSelf) return false;
            var r = CalloutCanvasRect();
            return canvasPoint.x >= r.xMin && canvasPoint.x <= r.xMax &&
                   canvasPoint.y >= r.yMin && canvasPoint.y <= r.yMax;
        }

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

        /// <summary>True while the callout is on screen -- the HUD and the router both ask.</summary>
        public bool IsShowing { get { return callout != null && callout.gameObject.activeSelf; } }

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
