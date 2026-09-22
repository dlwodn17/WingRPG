using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RPG25D.Core.Inventory;
using RPG25D.Core.Gacha;
using RPG25D.Gameplay;

namespace RPG25D.Data
{
    /// <summary>
    /// 영혼 조각 직렬화 저장용 데이터 단위
    /// </summary>
    [Serializable]
    public class ShardSaveEntry
    {
        public string BaseDataID;
        public int Count;

        public ShardSaveEntry() { }
        public ShardSaveEntry(string baseId, int count)
        {
            BaseDataID = baseId;
            Count = count;
        }
    }

    /// <summary>
    /// 유저 전체 영속 데이터 루트 컨테이너
    /// </summary>
    [Serializable]
    public class UserSaveData
    {
        public int SaveVersion = 1;
        public long SaveTimestamp;
        public List<CharacterInstance> OwnedCharacters = new List<CharacterInstance>();
        public List<ShardSaveEntry> Shards = new List<ShardSaveEntry>();
        public string[] PartySlotInstanceIDs = new string[PartyFormationManager.PartySlotCount];
        public List<string> AcquisitionOrder = new List<string>();
    }

    /// <summary>
    /// [요구 산출물 4] 데이터 직렬화 및 영속성 동기화 (UserDataPersistence.cs)
    /// - 보유 캐릭터 인스턴스 목록, 전용 조각 보유량, 현재 파티 편성 슬롯 정보를 JSON 문자열로 직렬화하여 로컬 저장
    /// - PlayerPrefs 및 persistentDataPath 파일 쓰기 지원
    /// - GameManagerData ScriptableObject와의 동기화 브리지 메서드를 제공하여
    ///   씬 전환(가챠 화면 <-> 탐색 화면 <-> 전투 화면) 시 데이터 손실 없이 상태 복원 지원
    /// </summary>
    public static class UserDataPersistence
    {
        public const string DefaultPlayerPrefsKey = "RPG25D_USER_SAVE_DATA";
        public const string DefaultSaveFileName = "rpg25d_savedata.json";

        /// <summary>
        /// 기본 저장 파일 경로 (persistentDataPath 기반)
        /// </summary>
        public static string GetDefaultSaveFilePath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultSaveFileName);
        }

        /// <summary>
        /// 인벤토리, 조각 핸들러, 파티 매니저의 상태를 단일 JSON 문자열로 직렬화합니다.
        /// </summary>
        public static string ToJson(
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager,
            bool prettyPrint = false)
        {
            var saveData = new UserSaveData
            {
                SaveVersion = 1,
                SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // 1. 보유 캐릭터 목록 직렬화
            if (inventory != null)
            {
                saveData.OwnedCharacters.AddRange(inventory.GetAllCharacters());
                saveData.AcquisitionOrder.AddRange(inventory.AcquisitionOrder);
            }

            // 2. 전용 영혼 조각 목록 직렬화
            if (duplicateHandler != null)
            {
                foreach (var kvp in duplicateHandler.CharacterShards)
                {
                    saveData.Shards.Add(new ShardSaveEntry(kvp.Key, kvp.Value));
                }
            }

            // 3. 파티 슬롯 직렬화
            if (partyManager != null)
            {
                for (int i = 0; i < PartyFormationManager.PartySlotCount; i++)
                {
                    saveData.PartySlotInstanceIDs[i] = partyManager.ActivePartySlotInstanceIDs[i];
                }
            }

            return JsonUtility.ToJson(saveData, prettyPrint);
        }

        /// <summary>
        /// JSON 문자열로부터 UserSaveData 객체를 역직렬화합니다.
        /// </summary>
        public static UserSaveData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<UserSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserDataPersistence] JSON 파싱 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 역직렬화된 데이터를 대상 매니저 시스템들에 적용(복원)합니다.
        /// </summary>
        public static void ApplySaveData(
            UserSaveData data,
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager)
        {
            if (data == null)
            {
                Debug.LogWarning("[UserDataPersistence] 복원할 데이터가 null입니다.");
                return;
            }

            // 1. 인벤토리 복원
            if (inventory != null)
            {
                inventory.Clear();
                if (data.OwnedCharacters != null)
                {
                    foreach (var ch in data.OwnedCharacters)
                    {
                        if (ch != null)
                        {
                            inventory.AddCharacter(ch);
                        }
                    }
                }
            }

            // 2. 조각 보관소 복원
            if (duplicateHandler != null)
            {
                duplicateHandler.Clear();
                if (data.Shards != null)
                {
                    foreach (var entry in data.Shards)
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.BaseDataID))
                        {
                            duplicateHandler.AddShards(entry.BaseDataID, entry.Count);
                        }
                    }
                }
            }

            // 3. 파티 편성 슬롯 복원
            if (partyManager != null)
            {
                partyManager.ClearParty();
                if (data.PartySlotInstanceIDs != null)
                {
                    for (int i = 0; i < data.PartySlotInstanceIDs.Length && i < PartyFormationManager.PartySlotCount; i++)
                    {
                        string instId = data.PartySlotInstanceIDs[i];
                        if (!string.IsNullOrEmpty(instId))
                        {
                            if (inventory != null && inventory.HasCharacter(instId))
                            {
                                partyManager.AssignToSlot(i, instId);
                            }
                            else
                            {
                                Debug.LogWarning($"[UserDataPersistence] 파티 슬롯 {i}번의 캐릭터({instId})가 인벤토리에 없어 등록을 건너뜁니다.");
                            }
                        }
                    }
                }
            }

            Debug.Log($"[UserDataPersistence] 🔄 유저 데이터 복원 완료: 캐릭터 {inventory?.TotalCharacterCount ?? 0}명, " +
                      $"샤드 항목 {duplicateHandler?.CharacterShards.Count ?? 0}개, " +
                      $"파티원 {partyManager?.ActiveMemberCount ?? 0}명");
        }

        #region PlayerPrefs 저장/로드

        /// <summary>
        /// PlayerPrefs에 유저 데이터를 저장합니다.
        /// </summary>
        public static bool SaveToPlayerPrefs(
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager,
            string customKey = null)
        {
            try
            {
                string key = !string.IsNullOrEmpty(customKey) ? customKey : DefaultPlayerPrefsKey;
                string json = ToJson(inventory, duplicateHandler, partyManager);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataPersistence] 💾 PlayerPrefs 저장 완료 (Key: {key}, 크기: {json.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserDataPersistence] PlayerPrefs 저장 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// PlayerPrefs로부터 유저 데이터를 로드합니다.
        /// </summary>
        public static bool LoadFromPlayerPrefs(
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager,
            string customKey = null)
        {
            string key = !string.IsNullOrEmpty(customKey) ? customKey : DefaultPlayerPrefsKey;
            if (!PlayerPrefs.HasKey(key))
            {
                Debug.LogWarning($"[UserDataPersistence] PlayerPrefs에 저장된 데이터가 없습니다: {key}");
                return false;
            }

            string json = PlayerPrefs.GetString(key);
            var data = FromJson(json);
            if (data == null) return false;

            ApplySaveData(data, inventory, duplicateHandler, partyManager);
            return true;
        }

        #endregion

        #region 파일 저장/로드

        /// <summary>
        /// 지정된 로컬 파일(JSON)에 유저 데이터를 저장합니다.
        /// </summary>
        public static bool SaveToFile(
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager,
            string customPath = null)
        {
            try
            {
                string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultSaveFilePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = ToJson(inventory, duplicateHandler, partyManager, true);
                File.WriteAllText(path, json);
                Debug.Log($"[UserDataPersistence] 💾 파일 저장 완료: {path} ({json.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserDataPersistence] 파일 저장 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 지정된 로컬 파일(JSON)로부터 유저 데이터를 로드합니다.
        /// </summary>
        public static bool LoadFromFile(
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager,
            string customPath = null)
        {
            string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultSaveFilePath();
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[UserDataPersistence] 세이브 파일을 찾을 수 없습니다: {path}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = FromJson(json);
                if (data == null) return false;

                ApplySaveData(data, inventory, duplicateHandler, partyManager);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserDataPersistence] 파일 로드 실패: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region GameManagerData 브리지 (씬 전환용)

        /// <summary>
        /// [세부 개발 명세 4] GameManagerData와의 브리지 동기화 (저장)
        /// 씬 전환 직전(가챠 화면 ➔ 탐색 화면 ➔ 전투 화면) 현재 인벤토리/파티/조각 상태를 GameManagerData에 즉시 직렬화합니다.
        /// </summary>
        public static void SyncToGameManager(
            GameManagerData gmData,
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager)
        {
            if (gmData == null) return;
            string json = ToJson(inventory, duplicateHandler, partyManager);
            gmData.SerializedUserData = json;
            Debug.Log($"[UserDataPersistence] 🔗 GameManagerData로 유저 데이터 동기화 완료 (JSON 크기: {json.Length} chars)");
        }

        /// <summary>
        /// [세부 개발 명세 4] GameManagerData와의 브리지 동기화 (로드)
        /// 씬 전환 직후 GameManagerData에 저장된 직렬화 문자열을 파싱하여 메모리 상태를 복원합니다.
        /// </summary>
        public static bool SyncFromGameManager(
            GameManagerData gmData,
            CharacterInventoryManager inventory,
            GachaDuplicateHandler duplicateHandler,
            PartyFormationManager partyManager)
        {
            if (gmData == null || string.IsNullOrEmpty(gmData.SerializedUserData))
            {
                return false;
            }

            var data = FromJson(gmData.SerializedUserData);
            if (data == null) return false;

            ApplySaveData(data, inventory, duplicateHandler, partyManager);
            Debug.Log("[UserDataPersistence] 🔗 GameManagerData로부터 유저 데이터 로드/복원 완료");
            return true;
        }

        #endregion
    }
}
