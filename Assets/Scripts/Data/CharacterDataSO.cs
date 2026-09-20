using UnityEngine;
using RPG25D.Core.Battle;

namespace RPG25D.Data
{
    [CreateAssetMenu(fileName = "CharacterData", menuName = "RPG25D/Character Data")]
    public class CharacterDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string characterName = "Hero";
        public bool isPlayer = true;

        [Header("스탯 설정")]
        public int maxHP = 100;
        public int baseAttack = 10;
        public int speed = 10;

        [Header("2.5D 비주얼 설정")]
        public Color themeColor = Color.cyan;
        public Sprite characterSprite;

        public BattleActor CreateActor(int id)
        {
            return new BattleActor(id, characterName, maxHP, baseAttack, isPlayer, speed);
        }
    }
}
