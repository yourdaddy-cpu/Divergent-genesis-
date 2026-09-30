using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DivergentGenesis.Items;
using DivergentGenesis.Player;
using DivergentGenesis.World;
using DivergentGenesis.Decor;

namespace DivergentGenesis.Save
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int seed;
        public float timeOfDay;

        public float px, py, pz;
        public float yaw, pitch;
        public int firstPerson;

        public float health = 20f;
        public float stamina = 100f;
        public float hunger = 20f;

        public int selectedSlot;
        public int[] slotItems;
        public int[] slotCounts;

        public List<BlockEdits.Entry> edits = new List<BlockEdits.Entry>();
        public List<int> removedPropsCx = new List<int>();
        public List<int> removedPropsCz = new List<int>();
        public List<int> removedPropsLevel = new List<int>();
        public List<int> removedPropsIndex = new List<int>();

        public double savedAtUnix;
    }

    /// <summary>
    /// Whole world = one small JSON file. The 140 km terrain is regenerated from
    /// the seed, so the file only stores the player and their changes.
    /// </summary>
    public static class SaveSystem
    {
        public const string FileName = "divergent-genesis-world.json";

        public static string Path
        {
            get
            {
                try { return System.IO.Path.Combine(Application.persistentDataPath, FileName); }
                catch { return FileName; }
            }
        }

        public static bool Exists()
        {
            try { return File.Exists(Path); } catch { return false; }
        }

        public static void Save(SaveData data)
        {
            try
            {
                data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(data, false);
                File.WriteAllText(Path, json);
                Debug.Log("[DivergentGenesis] saved -> " + Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DivergentGenesis] save failed: " + e.Message);
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                string json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<SaveData>(json);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DivergentGenesis] load failed: " + e.Message);
                return null;
            }
        }

        public static bool Delete()
        {
            try { if (File.Exists(Path)) File.Delete(Path); return true; }
            catch { return false; }
        }
    }
}
