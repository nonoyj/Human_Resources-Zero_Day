// HR//0D -- one gameplay builder, used by BOTH the editor menu and Play mode.
// Assembly: HRZeroDay.Game
//
// WHY THIS EXISTS: before this file the desk could only be assembled by HR//0D > Build Desk Scene,
// an editor-only menu action. A fresh checkout had no .unity scene, so pressing Play did nothing and
// the game looked broken even though every rule was implemented. Putting the whole assembly in the
// runtime assembly means GameBoot can build the same scene at Play time, and the editor menu can
// build exactly the same scene for a player build. No behaviour lives here: it places the objects
// that the pure rules in DeskSession/EvidenceLedger already own.
using System;
using HRZeroDay.Core;
using HRZeroDay.Core.Data;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public static class DeskRuntimeScene
    {
        public const float TouchPaddingPx = 6f;

        /// <summary>Builds desk art + every gameplay object the first minute needs.</summary>
        public static Transform BuildGameplay(DeskLayout layout, Transform parent)
        {
            var root = DeskSceneBuilder.Build(layout, parent);
            BuildHotspots(root, layout);
            BuildOverlay(root, layout);
            var stamp = BuildStamp(root);
            BuildLetter(root, layout);

            var decision = root.gameObject.AddComponent<DecisionInputController>();
            var bootstrap = root.gameObject.AddComponent<DeskBootstrap>();
            decision.stamps = stamp;               // the stamp lives in the same root; make the link explicit
            root.gameObject.AddComponent<GameInputRouter>();
            return root;
        }

        public static void ConfigureCamera(Camera cam, CanvasSpec spec)
        {
            DeskSceneBuilder.ConfigureCamera(cam, spec);
        }

        private static void BuildHotspots(Transform root, DeskLayout layout)
        {
            var app = root.Find("L_applicant");
            if (app == null) return;

            var hotspots = new GameObject("Hotspots").transform;
            hotspots.SetParent(app, false);

            float ppu = layout.canvas.pixelsPerUnit;
            const float spriteW = 192f;
            const float spriteH = 216f;
            for (int i = 0; i < layout.hotspots.Count; i++)
            {
                var hp = layout.hotspots[i];
                var go = new GameObject("HS_" + hp.artifact);
                go.transform.SetParent(hotspots, false);
                // applicant-local pixels (y up, origin bottom-left) -> local units, pivot centre.
                float x = (hp.rect[0] + hp.rect[2] * 0.5f - spriteW * 0.5f) / ppu;
                float y = (hp.rect[1] + hp.rect[3] * 0.5f - spriteH * 0.5f) / ppu;
                go.transform.localPosition = new Vector3(x, y, 0f);

                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2((hp.rect[2] + TouchPaddingPx * 2f) / ppu,
                                       (hp.rect[3] + TouchPaddingPx * 2f) / ppu);
                box.isTrigger = true;
                var tap = go.AddComponent<HotspotInteractor>();
                tap.artifact = hp.artifact;
                tap.sizeInUnits = box.size;   // kept in sync with the collider used by GameInputRouter
            }
        }

        private static void BuildOverlay(Transform root, DeskLayout layout)
        {
            float ppu = layout.canvas.pixelsPerUnit;

            var overlayGo = new GameObject("HR_EvidenceOverlay");
            overlayGo.transform.SetParent(root, false);
            var overlay = overlayGo.AddComponent<DeskEvidenceOverlay>();
            overlay.referenceWidth = layout.canvas.width;
            overlay.referenceHeight = layout.canvas.height;
            overlay.pixelsPerUnit = ppu;

            var ring = new GameObject("MagnifierRing");
            ring.transform.SetParent(overlayGo.transform, false);
            var ringSprite = ring.AddComponent<SpriteRenderer>();
            ringSprite.sprite = SpriteOrFallback("art/kit/ui/tooltip_9slice.png");
            ringSprite.sortingOrder = 50;
            overlay.magnifierRing = ring.transform;
            ring.SetActive(false);

            var callout = new GameObject("Callout");
            callout.transform.SetParent(overlayGo.transform, false);
            callout.transform.localPosition = new Vector3(overlay.calloutX / ppu, overlay.calloutY / ppu, 0f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(callout.transform, false);
            var panelSprite = panel.AddComponent<SpriteRenderer>();
            panelSprite.sprite = SpriteOrFallback("art/kit/ui/panel_9slice.png");
            panelSprite.sortingOrder = 45;
            panel.transform.localScale = new Vector3(10f, 3f, 1f);
            overlay.callout = callout.transform;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var label = new GameObject("Label");
            label.transform.SetParent(callout.transform, false);
            var labelMesh = label.AddComponent<TextMesh>();
            labelMesh.font = font;
            labelMesh.characterSize = 0.09f;
            // TextMesh is a Component, not a Renderer: the order goes on the MeshRenderer Unity puts
            // beside it (BUG-042/049). Guarded helper, so a missing renderer cannot throw at Play.
            SetTextMeshSorting(labelMesh, 46);                  // panel sprite is 45
            labelMesh.text = string.Empty;
            overlay.calloutLabel = labelMesh;

            var body = new GameObject("Body");
            body.transform.SetParent(callout.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            var bodyMesh = body.AddComponent<TextMesh>();
            bodyMesh.font = font;
            bodyMesh.characterSize = 0.07f;
            SetTextMeshSorting(bodyMesh, 46);
            bodyMesh.text = string.Empty;
            overlay.calloutBody = bodyMesh;
            callout.SetActive(false);
        }

        private static StampAnimator BuildStamp(Transform root)
        {
            var stamp = new GameObject("Stamp");
            stamp.transform.SetParent(root, false);
            var sprite = stamp.AddComponent<SpriteRenderer>();
            var approved = SpriteOrFallback("art/kit/stamps/stamp_approve.png");
            sprite.sprite = approved;
            sprite.sortingOrder = 60;
            var anim = stamp.AddComponent<StampAnimator>();
            anim.stampTransform = stamp.transform;
            anim.stampRenderer = sprite;
            anim.approveSprite = approved;
            anim.rejectSprite = SpriteOrFallback("art/kit/stamps/stamp_reject.png");
            stamp.SetActive(false);
            return anim;
        }

        private static void BuildLetter(Transform root, DeskLayout layout)
        {
            var go = new GameObject(LetterView.RootName);
            go.transform.SetParent(root, false);

            var paper = go.AddComponent<SpriteRenderer>();
            paper.sprite = SolidSprite();
            paper.color = new Color32(0xf2, 0xe8, 0xd5, 0xff);
            paper.sortingOrder = 190;
            paper.transform.localScale = new Vector3(layout.canvas.width / layout.canvas.pixelsPerUnit,
                                                     layout.canvas.height / layout.canvas.pixelsPerUnit, 1f);
            paper.transform.localPosition = new Vector3(layout.canvas.width / (2f * layout.canvas.pixelsPerUnit),
                                                        layout.canvas.height / (2f * layout.canvas.pixelsPerUnit), -0.5f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<SpriteRenderer>();
            text.sortingOrder = 191;
            var textRect = LetterLayout.TextRect();
            textGo.transform.localPosition = new Vector3(
                (textRect.x + textRect.w * 0.5f) / layout.canvas.pixelsPerUnit,
                (textRect.y + textRect.h * 0.5f) / layout.canvas.pixelsPerUnit, -0.6f);

            var view = go.AddComponent<LetterView>();
            view.root = go;
            view.paper = paper;
            view.textLayer = text;
            go.SetActive(false);
        }

        /// <summary>Art is published by the setup menu; a missing sprite must never stop Play mode.</summary>
        private static Sprite SpriteOrFallback(string resourcePath)
        {
            try
            {
                var entry = SpriteManifest.Load().Find(resourcePath);
                return DeskSceneBuilder.LoadPrimarySprite(entry);
            }
            catch (Exception)
            {
                return SolidSprite();
            }
        }

        /// <summary>
        /// TextMesh in Unity is a Component, NOT a Renderer: `text.sortingOrder` is a compile error
        /// (CS1061) and `text.GetComponent<MeshRenderer>()` is where the order actually lives. Asking a
        /// SpriteRenderer for a MeshRenderer throws at Play, so both mistakes have shipped once each
        /// (BUG-042, BUG-048). This is the only place in the game that touches a TextMesh's order.
        /// </summary>
        public static void SetTextMeshSorting(TextMesh mesh, int order)
        {
            if (mesh == null) return;
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = order;
        }

        private static Sprite SolidSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { new Color(1f, 1f, 1f, 1f) });
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
