using System;

namespace RPG25D.Core.Battle
{
    /// <summary>
    /// 전투 참가자 도메인 모델 (플레이어 / 몬스터)
    /// 순수 C#으로 작성되어 테스트 러너 및 헤드리스 환경에서 즉시 검증 가능합니다.
    /// </summary>
    public class BattleActor
    {
        public int Id { get; }
        public string Name { get; }
        public bool IsPlayer { get; }
        public int MaxHP { get; private set; }
        public int CurrentHP { get; private set; }
        public int Shield { get; private set; }
        public int BaseAttack { get; set; }
        public int Speed { get; set; }

        public bool IsAlive => CurrentHP > 0;

        public event Action<int, int> OnHPChanged;         // (current, max)
        public event Action<int> OnShieldChanged;          // (shield)
        public event Action<int, bool> OnDamageTaken;      // (damage, wasShieldBreak)
        public event Action OnDeath;

        public BattleActor(int id, string name, int maxHP, int baseAttack = 10, bool isPlayer = false, int speed = 10)
        {
            Id = id;
            Name = name;
            MaxHP = maxHP;
            CurrentHP = maxHP;
            Shield = 0;
            BaseAttack = baseAttack;
            IsPlayer = isPlayer;
            Speed = speed;
        }

        public int TakeDamage(int rawDamage)
        {
            if (!IsAlive || rawDamage <= 0) return 0;

            int remainingDamage = rawDamage;
            bool hadShield = Shield > 0;

            // 1. 실드가 피해를 먼저 흡수
            if (Shield > 0)
            {
                if (Shield >= remainingDamage)
                {
                    Shield -= remainingDamage;
                    remainingDamage = 0;
                }
                else
                {
                    remainingDamage -= Shield;
                    Shield = 0;
                }
                OnShieldChanged?.Invoke(Shield);
            }

            // 2. 남은 피해를 체력에서 차감
            int hpLost = 0;
            if (remainingDamage > 0)
            {
                hpLost = Math.Min(CurrentHP, remainingDamage);
                CurrentHP -= hpLost;
                OnHPChanged?.Invoke(CurrentHP, MaxHP);
            }

            OnDamageTaken?.Invoke(rawDamage, hadShield && Shield == 0);

            if (CurrentHP <= 0)
            {
                CurrentHP = 0;
                OnDeath?.Invoke();
            }

            return rawDamage;
        }

        /// <summary>
        /// D20 = 20 (절대 성공) 발동 시 남은 체력과 상관없이 즉시 사망 처리합니다.
        /// </summary>
        public void KillInstantly()
        {
            if (!IsAlive) return;

            CurrentHP = 0;
            Shield = 0;
            OnDamageTaken?.Invoke(MaxHP, false);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
            OnDeath?.Invoke();
        }

        public void AddShield(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            Shield += amount;
            OnShieldChanged?.Invoke(Shield);
        }

        public void ClearShield()
        {
            if (Shield > 0)
            {
                Shield = 0;
                OnShieldChanged?.Invoke(Shield);
            }
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }
    }
}
