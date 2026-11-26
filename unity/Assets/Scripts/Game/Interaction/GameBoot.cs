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
                DeskRuntimeScene.ConfigureCamera(cam, layout.canvas);
                DeskRuntimeScene.BuildGameplay(layout, null);
                Debug.Log("[HR//0D] Play mode boot: desk built from Resources/desk_layout.json. " +
                          "Click a hotspot, L reads the letter, A/S stamps, SPACE continues.");
            }
            catch (Exception ex)
            {
                // An actionable error beats a black Game view. The art pipeline is the usual cause.
                Debug.LogError("[HR//0D] could not start the game: " + ex.Message +
                               "\nRun HR//0D > Setup Project (one click), then press Play again.");
            }
        }
    }
}
