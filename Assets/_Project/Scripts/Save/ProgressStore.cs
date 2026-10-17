using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MonsoonRooftops.Save
{
    [Serializable]
    public class LevelProgress
    {
        public string id;
        public int stars;
    }

    [Serializable]
    public class ProgressData
    {
        public List<LevelProgress> levels = new List<LevelProgress>();
    }

    // Best stars per level, saved as JSON in persistentDataPath. Phase 6 folds this into the full SaveService.
    public static class ProgressStore
    {
        private static ProgressData _data;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "progress.json");

        private static ProgressData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(FilePath)) _data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not read progress, starting fresh: " + e.Message);
            }
            _data ??= new ProgressData();
        }

        public static int GetStars(string id)
        {
            foreach (var l in Data.levels)
                if (l.id == id) return l.stars;
            return 0;
        }

        // Keeps the best result.
        public static void Record(string id, int stars)
        {
            var entry = Data.levels.Find(l => l.id == id);
            if (entry == null) Data.levels.Add(entry = new LevelProgress { id = id });
            if (stars <= entry.stars) return;
            entry.stars = stars;
            Write();
        }

        public static void ResetAll()
        {
            _data = new ProgressData();
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        private static void Write()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not save progress: " + e.Message);
            }
        }
    }
}
