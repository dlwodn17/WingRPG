using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Core.Dice;
using RPG25D.Core.Skills;
using RPG25D.Data;

namespace RPG25D.Gameplay
{
    /// <summary>
    /// [요구 산출물 2] SkillSlot.cs
    /// 스킬 발동 조건 검증 및 주사위 할당/소모 로직을 관리하는 컴포넌트입니다.
    /// 플레이어가 굴린 주사위를 슬롯에 배치하고, 스킬 발동 요건(단일 눈금, 합산, 짝수 등)을 실시간 검증합니다.
    /// </summary>
    public class SkillSlot : MonoBehaviour
    {
        [Header("스킬 데이터 연결")]
        [SerializeField] private SkillDataSO _skillData;

        private SkillModel _skillModel;
        private readonly List<Die> _assignedDice = new List<Die>();

        public SkillModel Model => _skillModel;
        public SkillDataSO Data => _skillData;
        public IReadOnlyList<Die> AssignedDice => _assignedDice;

        public event Action<SkillSlot> OnSlotUpdated;
        public event Action<SkillSlot, bool, string> OnConditionChecked;

        private void Awake()
        {
            if (_skillData != null)
            {
                Initialize(_skillData);
            }
        }

        public void Initialize(SkillDataSO data)
        {
            _skillData = data;
            _skillModel = data.CreateModel();
            _assignedDice.Clear();
            NotifyUpdate();
        }

        public void InitializeWithModel(SkillModel model)
        {
            _skillModel = model;
            _assignedDice.Clear();
            NotifyUpdate();
        }

        /// <summary>
        /// 주사위를 슬롯에 할당합니다.
        /// </summary>
        public bool AssignDie(Die die)
        {
            if (die == null || die.IsConsumed || _assignedDice.Contains(die))
            {
                return false;
            }

            if (_skillModel == null) return false;

            // 이미 필요 개수를 초과했는지 확인
            if (_assignedDice.Count >= _skillModel.Condition.RequiredDiceCount)
            {
                return false;
            }

            _assignedDice.Add(die);
            die.IsLocked = true; // 슬롯에 들어간 주사위는 리롤 방지를 위해 잠금

            NotifyUpdate();
            return true;
        }

        /// <summary>
        /// 슬롯에서 특정 주사위를 제거하여 다시 풀로 반환합니다.
        /// </summary>
        public bool UnassignDie(Die die)
        {
            if (die == null || !_assignedDice.Contains(die))
            {
                return false;
            }

            _assignedDice.Remove(die);
            die.IsLocked = false;

            NotifyUpdate();
            return true;
        }

        /// <summary>
        /// 슬롯의 모든 주사위 할당을 취소합니다.
        /// </summary>
        public void ClearAssignedDice()
        {
            foreach (var die in _assignedDice)
            {
                die.IsLocked = false;
            }
            _assignedDice.Clear();
            NotifyUpdate();
        }

        /// <summary>
        /// 스킬 발동 조건 충족 여부를 검증합니다.
        /// </summary>
        public bool CheckCondition(out string failureReason)
        {
            if (_skillModel == null)
            {
                failureReason = "스킬 데이터가 초기화되지 않았습니다.";
                return false;
            }

            bool canActivate = _skillModel.Condition.CanActivate(_assignedDice, out failureReason);
            OnConditionChecked?.Invoke(this, canActivate, failureReason);
            return canActivate;
        }

        public bool IsConditionSatisfied => CheckCondition(out _);

        public int CalculatePower()
        {
            if (_skillModel == null) return 0;
            return _skillModel.CalculatePower(_assignedDice);
        }

        /// <summary>
        /// 행동 실행 시 슬롯에 할당된 주사위들을 소모 처리합니다.
        /// </summary>
        public void ConsumeAssignedDice(DiceManager diceManager)
        {
            foreach (var die in _assignedDice)
            {
                die.IsConsumed = true;
                diceManager?.ConsumeDie(die.Id);
            }
            _assignedDice.Clear();
            NotifyUpdate();
        }

        private void NotifyUpdate()
        {
            CheckCondition(out string reason);
            OnSlotUpdated?.Invoke(this);
        }
    }
}
