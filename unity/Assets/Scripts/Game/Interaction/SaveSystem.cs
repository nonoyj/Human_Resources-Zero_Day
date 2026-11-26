// HR//0D -- save file I/O. BUG prevention rule 1: write tmp -> replace, never overwrite in place.
// Assembly: HRZeroDay.Game
//
// WHERE: Application.persistentDataPath/hr0d_save.json (Android: app-private storage).
// WHY JsonUtility: it is built into Unity, has no package dependency, and SaveData is already
// JsonUtility-shaped (public fields, no dictionaries, no nullable value types -- see SaveData.cs).
// A new package (Newtonsoft) would be one more thing to resolve on a fresh checkout.
//
// The save is written after every committed decision and on app pause/quit, so a background kill
// mid-shift resumes exactly where the player was (docs/02-tech-stack.md).
using System;
using System.IO;
using UnityEngine;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Game.Interaction
{
    public static class SaveSystem
    {
        public const string FileName = "hr0d_save.json";
        private static readonly string TmpName = FileName + ".tmp";
        private static readonly string BakName = FileName + ".bak";

        public static string DirectoryPath
        {
            get { return Application.persistentDataPath; }
        }

        public static string SavePath { get { return Path.Combine(DirectoryPath, FileName); } }

        public static bool HasSave { get { return File.Exists(SavePath); } }

        /// <summary>Writes atomically. Returns false (and logs) instead of throwing: a full disk
        /// must never crash a shift, the run simply continues unsaved.</summary>
        public static bool Write(SaveData data)
        {
            if (data == null) return false;
            try
            {
                data.savedAtUtc = DateTime.UtcNow.ToString("o");
                var json = JsonUtility.ToJson(data, true);
                var dir = DirectoryPath;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var tmp = Path.Combine(dir, TmpName);
                File.WriteAllText(tmp, json);
                // Keep exactly one previous save: a corrupted write must not lose the run.
                if (File.Exists(SavePath)) File.Copy(SavePath, Path.Combine(dir, BakName), true);
                Replace(tmp, SavePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: could not write save (" + e.GetType().Name + "). " +
                                 "The shift continues; progress will not persist.");
                return false;
            }
        }

        /// <summary>Loads the newest readable save. Falls back to the .bak, then to null.
        /// A save from a newer build is refused rather than half-applied.</summary>
        public static SaveData Read()
        {
            var data = TryRead(SavePath);
            if (data == null) data = TryRead(Path.Combine(DirectoryPath, BakName));
            if (data == null) return null;
            if (!data.Migrate())
            {
                Debug.LogWarning("SaveSystem: save is from a newer build; starting a fresh run.");
                return null;
            }
            return data;
        }

        public static void Delete()
        {
            TryDelete(SavePath);
            TryDelete(Path.Combine(DirectoryPath, BakName));
            TryDelete(Path.Combine(DirectoryPath, TmpName));
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || string.IsNullOrEmpty(data.saveId)) return null;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: unreadable save at " + Path.GetFileName(path) +
                                 " (" + e.GetType().Name + "); trying the previous one.");
                return null;
            }
        }

        // File.Replace does not exist on every Android path, so move to a temp name first.
        private static void Replace(string tmp, string target)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (File.Exists(target)) File.Delete(target);
#endif
            File.Move(tmp, target);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { /* best effort: a leftover file is harmless */ }
        }
    }
}
