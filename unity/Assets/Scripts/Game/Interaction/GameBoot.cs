// HR//0D -- Play mode always starts a game, even from an empty scene.
// Assembly: HRZeroDay.Game
//
// A fresh clone has no .unity scene. Requiring the operator to run an editor menu before they can
// press Play is the single biggest reason "the game does not work in Unity". This hook builds the
// exact same desk the editor menu builds, but only when no DeskBootstrap is already in the loaded
// scene. Build Desk Scene still works: in that case this hook does nothing.
using System;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public static class GameBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (UnityEngine.Object.FindFirstObjectByType<DeskBootstrap>() != null) return;
#if !UNITY_EDITOR
            // A phone has no Editor console, so a message there is a message to nobody. The desk itself
            // already says what to do (HUD line 3), which is where the player will look.
#else

            try
            {
                var layout = DeskLayout.Load();
                var cam = Camera.main;
                if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                {
                    var camGo = new GameObject("Main Camera");
                    cam = camGo.AddComponent<Camera>();
                }
                // ONE DESK, ALWAYS. The player's v130 screenshot showed THREE applicants side by side: a
                // stale DeskRoot (a saved scene, or a Play session that did not reload the domain) was still
                // standing next to the new one. The cleanup lives in DeskSceneBuilder.Build, so the editor
                // menu and Play mode cannot disagree about it -- see the note there.
                DeskRuntimeScene.ConfigureCamera(cam, layout.canvas);
                DeskRuntimeScene.BuildGameplay(layout, null);
                Debug.Log("[HR//0D] desk built: " +
                          "click the applicant to inspect, L letter, A approve, S reject, SPACE next.");
            }
            catch (Exception ex)
            {
                // An actionable error beats a black Game view. The art pipeline is the usual cause.
                // v130.1: the old text here said "Run HR//0D > Setup Project" -- which has never once been
                // the fix, and cost the player 40 minutes at a time (orders/09 G3). What an actionable error
                // actually needs is: what failed, where, and what to send me.
                Debug.LogError("[HR//0D] could not start the game: " + ex.Message +
                               "\n  at " + (ex.StackTrace ?? string.Empty).Split('\n')[0] +
                               "\n\nSend me gate-v130.log. If this says a sprite or a frame is missing, run " +
                               "HR//0D > Fix Applicant Frames, then HR//0D > Open Game (play).");
            }
#endif
        }
    }
}
