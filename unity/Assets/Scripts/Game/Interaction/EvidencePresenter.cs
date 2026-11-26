// HR//0D -- turns a ledger observation into a magnifier ring or a face callout.
// The presenter has no UI framework dependency; a view implements IEvidenceOverlay.
// All wording comes from TellCatalog (curated copy) or desk_layout.json labels, never from a
// string literal in this file.
// Owner: Agent (interaction layer).
using UnityEngine;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Game.Interaction
{
    public interface IEvidenceOverlay
    {
        void ShowMagnifier(Rect canvasRect, string label, string wording);
        void ShowCallout(Rect canvasRect, string label, string wording);
        void Hide();
    }

    public sealed class EvidencePresenter
    {
        private readonly DeskSession _session;
        private readonly DeskLayout _layout;
        private readonly IEvidenceOverlay _overlay;
        private readonly string _watchCopy;

        public EvidencePresenter(DeskSession session, DeskLayout layout,
                                 IEvidenceOverlay overlay, string watchCopy)
        {
            _session = session;
            _layout = layout;
            _overlay = overlay;
            _watchCopy = watchCopy;
        }

        public ObservationResult Inspect(string artifact)
        {
            var result = _session.Inspect(artifact);
            var rect = ArtifactRect(artifact);
            if (result.RequiresMoreTime)
            {
                _overlay.ShowCallout(rect, Label(artifact), _watchCopy);
                return result;
            }
            if (result.Style == RevealStyle.MagnifierRing)
            {
                // Body tells: 2x region zoom. The wording sits beside the ring, not over the art.
                _overlay.ShowMagnifier(rect, Label(artifact), result.Wording);
            }
            else
            {
                // Face tells: text callout, so no tell is conveyed by colour alone (I8).
                _overlay.ShowCallout(rect, Label(artifact), result.Wording);
            }
            return result;
        }

        public void Open(RevealSurface surface)
        {
            var results = _session.Open(surface);
            if (results.Count == 0) return;
            var last = results[results.Count - 1];
            var rect = new Rect(14f, 52f, 192f, 108f);   // monitor / document sheet from the layout
            _overlay.ShowCallout(rect, SurfaceLabel(surface), last.Wording);
        }

        public void Hide() { _overlay.Hide(); }

        private string Label(string artifact)
        {
            foreach (var hp in _layout.hotspots)
                if (hp.artifact == artifact) return hp.label;
            return artifact;
        }

        private static string SurfaceLabel(RevealSurface surface)
        {
            // The label is a surface enum name, not gameplay copy. Content files own the prose.
            return surface.ToString();
        }

        private Rect ArtifactRect(string artifact)
        {
            var applicant = _layout.Layer("applicant");
            float ax = applicant.anchor[0];
            float ay = applicant.anchor[1];
            foreach (var hp in _layout.hotspots)
            {
                if (hp.artifact != artifact || hp.rect == null || hp.rect.Length != 4) continue;
                var p = HotspotHitTest.LocalToCanvas(
                    new PixelRect(hp.rect[0], hp.rect[1], hp.rect[2], hp.rect[3]), ax, ay);
                return new Rect(p.X, p.Y, p.Width, p.Height);
            }
            return new Rect(ax, ay, 192f, 216f);
        }
    }
}
