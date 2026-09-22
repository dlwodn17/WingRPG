using UnityEngine;

namespace RPG25D.Data
{
    /// <summary>
    /// [요구 산출물 3] 캐릭터 원본 마스터 데이터 (ScriptableObject)
    /// 가챠 소환 풀 및 캐릭터 기본 스탯의 원본 정보입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterBaseData", menuName = "RPG25D/CharacterBaseData")]
    public class CharacterBaseData : ScriptableObject
    {
        [Header("캐릭터 기본 식별자")]
        [Tooltip("캐릭터 고유 ID (예: HERO_001, MAGE_002)")]
        public string CharacterID = "CHAR_001";

        [Tooltip("캐릭터 이름")]
        public string CharacterName = "이름 없는 영웅";

        [Header("등급 및 기본 스탯")]
        [Tooltip("기본 레어도 (3성, 4성, 5성)")]
        [Range(3, 5)]
        public int BaseRarity = 3;

        [Tooltip("기본 공격력 스탯")]
        public int BaseAttack = 25;

        [Tooltip("기본 체력 스탯")]
        public int BaseHP = 120;

        [Tooltip("기본 속도 스탯")]
        public int BaseSpeed = 10;

        /// <summary>
        /// 런타임 또는 단위 테스트에서 메모리상에 인스턴스를 즉시 생성하는 팩토리 메서드
        /// </summary>
        public static CharacterBaseData Create(string id, string name, int rarity, int baseAttack = 25, int baseHP = 120, int baseSpeed = 10)
        {
            var data = CreateInstance<CharacterBaseData>();
            data.CharacterID = id;
            data.CharacterName = name;
            data.BaseRarity = Mathf.Clamp(rarity, 3, 5);
            data.BaseAttack = baseAttack;
            data.BaseHP = baseHP;
            data.BaseSpeed = baseSpeed;
            return data;
        }
    }
}
