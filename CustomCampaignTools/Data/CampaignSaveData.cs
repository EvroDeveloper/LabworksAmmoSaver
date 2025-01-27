using BoneLib;
using Il2CppSLZ.Marrow;
using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Il2CppSystem;
using Newtonsoft.Json;

namespace CustomCampaignTools
{
    public class CampaignSaveData
    {
        public Campaign campaign;

        internal SavePoint LoadedSavePoint;
        internal List<AmmoSave> LoadedAmmoSaves = new List<AmmoSave>();
        internal List<FloatData> LoadedFloatDatas = new List<FloatData>();

        public string SaveFolder { get => $"{MelonUtils.UserDataDirectory}/Campaigns/{campaign.Name}"; }
        public string SavePath { get => $"{SaveFolder}/save.json"; }

        public CampaignSaveData(Campaign c)
        {
            campaign = c;
            LoadFromDisk();
        }

        #region Ammo Methods
        public void SaveAmmoForLevel(string levelBarcode)
        {
            AmmoSave previousAmmoSave = GetPreviousLevelsAmmoSave(levelBarcode);

            if (!DoesSavedAmmoExist(levelBarcode))
            {
                LoadedAmmoSaves.Add(new AmmoSave
                {
                    LevelBarcode = levelBarcode,
                    LightAmmo = AmmoInventory.Instance.GetCartridgeCount("light") - previousAmmoSave.LightAmmo,
                    MediumAmmo = AmmoInventory.Instance.GetCartridgeCount("medium") - previousAmmoSave.MediumAmmo,
                    HeavyAmmo = AmmoInventory.Instance.GetCartridgeCount("heavy") - previousAmmoSave.HeavyAmmo
                });
            } 
            else
            {
                AmmoSave previousHighScore = GetSavedAmmo(levelBarcode);

                for (int i = 0; i < LoadedAmmoSaves.Count; i++)
                {
                    if (LoadedAmmoSaves[i].LevelBarcode == levelBarcode)
                    {
                        LoadedAmmoSaves[i] = new AmmoSave
                        {
                            LevelBarcode = levelBarcode,
                            LightAmmo = Mathf.Max(AmmoInventory.Instance.GetCartridgeCount("light") - previousAmmoSave.LightAmmo, previousHighScore.LightAmmo),
                            MediumAmmo = Mathf.Max(AmmoInventory.Instance.GetCartridgeCount("medium") - previousAmmoSave.MediumAmmo, previousHighScore.MediumAmmo),
                            HeavyAmmo = Mathf.Max(AmmoInventory.Instance.GetCartridgeCount("heavy") - previousAmmoSave.HeavyAmmo, previousHighScore.HeavyAmmo)
                        };
                    }
                }
            }

            campaign.saveData.SaveToDisk();
        }

        public AmmoSave GetPreviousLevelsAmmoSave(string levelBarcode)
        {
            int levelIndex = campaign.GetLevelIndex(levelBarcode);

            AmmoSave previousLevelsAmmoSave = new AmmoSave();

            for (int i = 0; i < levelIndex; i++)
            {
                previousLevelsAmmoSave.LightAmmo += GetSavedAmmo(campaign.GetLevelBarcodeByIndex(i)).LightAmmo;
                previousLevelsAmmoSave.MediumAmmo += GetSavedAmmo(campaign.GetLevelBarcodeByIndex(i)).MediumAmmo;
                previousLevelsAmmoSave.HeavyAmmo += GetSavedAmmo(campaign.GetLevelBarcodeByIndex(i)).HeavyAmmo;
            }

            return previousLevelsAmmoSave;
        }

        public AmmoSave GetSavedAmmo(string levelBarcode)
        {
            return LoadedAmmoSaves.FirstOrDefault(x => x.LevelBarcode == levelBarcode);
        }

        public bool DoesSavedAmmoExist(string levelBarcode)
        {
            if (LoadedAmmoSaves == null)
                return false;

            if (LoadedAmmoSaves.Count == 0)
                return false;

            if (LoadedAmmoSaves.Any(x => x.LevelBarcode == levelBarcode))
                return true;

            return false;
        }

        public void ClearAmmoSave()
        {
            LoadedAmmoSaves.Clear();
            // Fill default ammo saves
            foreach (string barcode in campaign.mainLevels)
            {
                LoadedAmmoSaves.Add(new AmmoSave()
                {
                    LevelBarcode = barcode,
                    LightAmmo = 0,
                    MediumAmmo = 0,
                    HeavyAmmo = 0,
                });
            }
        }
        #endregion

        #region Save Point Methods
        public void ClearSavePoint()
        {
            LoadedSavePoint = new SavePoint();
        }
        #endregion

        #region Float Data
        public void SetValue(string key, float value)
        {
            var register = GetFloatDataEntry(key);
            if (register == null)
            {
                register = new FloatData() { Key = key, Value = value };
                LoadedFloatDatas.Add(register);
            }
            else
            {
                register.Value = value;
            }
        }
        public float GetValue(string key)
        {
            return GetFloatDataEntry(key).Value;
        }
        private FloatData GetFloatDataEntry(string key)
        {
            return LoadedFloatDatas.First(f => f.Key == key);
        }
        #endregion


        #region Saving and Loading
        /// <summary>
        /// Saves the current loaded save data to file.
        /// </summary>
        internal void SaveToDisk()
        {
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);

            SaveData saveData = new SaveData
            {
                SavePoint = LoadedSavePoint,
                AmmoSaves = LoadedAmmoSaves,
                FloatData = LoadedFloatDatas,
            };

            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            string json = JsonConvert.SerializeObject(saveData, settings);

            File.WriteAllText(SavePath, json);
        }

        /// <summary>
        /// Loads the save data from file. This should *probably* only be called at initialization.
        /// </summary>
        internal void LoadFromDisk()
        {
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);

            if (!File.Exists(SavePath))
            {
                ClearAmmoSave();
                return;
            }

            string json = File.ReadAllText(SavePath);

            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            SaveData saveData = JsonConvert.DeserializeObject<SaveData>(json, settings);

            LoadedSavePoint = saveData.SavePoint;
            LoadedAmmoSaves = saveData.AmmoSaves;
            LoadedFloatDatas = saveData.FloatData;
        }
        #endregion

        public class SaveData
        {
            public SavePoint SavePoint { get; set; }
            public List<AmmoSave> AmmoSaves { get; set; }
            public List<FloatData> FloatData { get; set; }
        }

        public struct SavePoint(string levelBarcode, Vector3 position, string backSlotBarcode, string leftSidearmBarcode, string rightSidearmBarcode, string leftShoulderBarcode, string rightShoulderBarcode, List<string> boxContainedBarcodes, Vector3 boxContainerPosition)
        {
            public string LevelBarcode = levelBarcode;
            public float PositionX = position.x;
            public float PositionY = position.y;
            public float PositionZ = position.z;

            public string BackSlotBarcode = backSlotBarcode;
            public string LeftSidearmBarcode = leftSidearmBarcode;
            public string RightSidearmBarcode = rightSidearmBarcode;
            public string LeftShoulderSlotBarcode = leftShoulderBarcode;
            public string RightShoulderSlotBarcode = rightShoulderBarcode;

            public List<string> BoxContainedBarcodes = boxContainedBarcodes;

            public float BoxContainedX = boxContainerPosition.x;
            public float BoxContainedY = boxContainerPosition.y;
            public float BoxContainedZ = boxContainerPosition.z;

            /// <summary>
            /// Returns true if the save point has a level barcode.
            /// </summary>
            /// <param name="out hasSpawnPoint"></param>
            /// <returns></returns>
            public bool IsValid(out bool hasSpawnPoint)
            {
                if (new Vector3(PositionX, PositionY, PositionZ) == Vector3.zero)
                    hasSpawnPoint = false;
                else
                    hasSpawnPoint = true;

                if (LevelBarcode == string.Empty)
                    return false;

                return true;
            }

            public Vector3 GetPosition()
            {
                return new Vector3(PositionX, PositionY, PositionZ);
            }
        }

        public struct AmmoSave
        {
            public string LevelBarcode { get; set; }
            public int LightAmmo { get; set; }
            public int MediumAmmo { get; set; }
            public int HeavyAmmo { get; set; }

            public int GetCombinedTotal()
            {
                return (LightAmmo + MediumAmmo + HeavyAmmo);
            }
        }

        public class FloatData
        {
            public string Key;
            public float Value;
        }
    }
}