using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using RPG25D.Core.Battle;
using RPG25D.Core.Dice;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.Gameplay
{
    /// <summary>
    /// [요구 산출물] D20 턴제 전투 루프 상태 머신 열거형 (BattlePhase)
    /// </summary>
    public enum BattlePhase
    {
        BattleStart,    // 파티 생성, UI 초기화, 턴 순서 타임라인 계산
        TurnSetup,      // 다음 행동 캐릭터 선정 및 행동 중 표시
        PlayerInput,    // 아군 턴: 스킬 및 대상 선택 대기
        EnemyAI,        // 적 턴: 최저 체력 타깃 등 AI 판단
        ActionExecute,  // 스킬 시전 -> D20 물리 롤 -> 타격 피드백(셰이크/히트스톱) -> 대미지/즉사
        CheckVictory,   // 아군/적군 전멸 여부 실시간 판정
        BattleVictory,  // 적 전멸 승리 팝업 및 연출
        BattleDefeat    // 아군 전멸 패배 팝업 및 화면 회색조
    }

    /// <summary>
    /// 하위 호환성을 위한 Type 별칭 열거형
    /// </summary>
    public enum BattleLoopState
    {
        BattleStart = BattlePhase.BattleStart,
        TurnSetup = BattlePhase.TurnSetup,
        PlayerInput = BattlePhase.PlayerInput,
        EnemyAI = BattlePhase.EnemyAI,
        ActionExecute = BattlePhase.ActionExecute,
        CheckVictory = BattlePhase.CheckVictory,
        BattleVictory = BattlePhase.BattleVictory,
        BattleDefeat = BattlePhase.BattleDefeat
    }

    /// <summary>
    /// 구버전 테스트 호환용 열거형
    /// </summary>
    public enum D20BattlePhase
    {
        BattleStart = BattlePhase.BattleStart,
        TurnSetup = BattlePhase.TurnSetup,
        PlayerTurn = BattlePhase.PlayerInput,
        PlayerInput = BattlePhase.PlayerInput,
        EnemyTurn = BattlePhase.EnemyAI,
        EnemyAI = BattlePhase.EnemyAI,
        ActionExecute = BattlePhase.ActionExecute,
        CheckVictory = BattlePhase.CheckVictory,
        Victory = BattlePhase.BattleVictory,
        BattleVictory = BattlePhase.BattleVictory,
        Defeat = BattlePhase.BattleDefeat,
        BattleDefeat = BattlePhase.BattleDefeat
    }

    /// <summary>
    /// [요구 산출물 2] BattleTurnController.cs
    /// 아군 3인 vs 적군 3인 다대다 턴제 전투 전체 루프를 총괄하는 상태 머신 컨트롤러입니다.
    /// Speed 기반 턴 타임라인, 적 AI(최저 체력 타깃팅), D20 물리 굴림 및 타격 피드백 연동,
    /// 실시간 승패 판정 및 CLI 고속 동기 시뮬레이션 메서드를 제공합니다.
    /// </summary>
    public class BattleTurnController : MonoBehaviour
    {
        [Header("파티 매니저")]
        [SerializeField] private PartyManager _partyManager;

        [Header("보유 공격 스킬 풀 (3종)")]
        [SerializeField] private List<AttackSkill> _defaultSkills = new List<AttackSkill>();

        [Header("턴 연출 딜레이 (초)")]
        [SerializeField] private float _turnStepDelay = 0.45f;

        // 주사위 롤러
        private D20DiceRoller _diceRoller;

        // 턴 타임라인 큐
        private readonly Queue<BattleActor> _turnQueue = new Queue<BattleActor>();
        private readonly List<BattleActor> _currentRoundOrder = new List<BattleActor>();

        // 현재 턴 실행 정보
        public BattlePhase CurrentPhase { get; private set; } = BattlePhase.BattleStart;
        public BattlePhase CurrentState => CurrentPhase;
        public int RoundNumber { get; private set; } = 0;
        public int TurnInRound { get; private set; } = 0;

        [Header("인카운터 유발 심볼 정보")]
        [SerializeField] private string _triggeringEnemyID = "";
        [SerializeField] private int _triggeringEncounterId = -1;

        public string TriggeringEnemyID
        {
            get => !string.IsNullOrEmpty(_triggeringEnemyID) ? _triggeringEnemyID : (GameManager.Instance != null ? GameManager.Instance.CurrentEnemyID : string.Empty);
            set => _triggeringEnemyID = value;
        }

        public int TriggeringEncounterId
        {
            get => _triggeringEncounterId >= 0 ? _triggeringEncounterId : (GameManager.Instance != null ? GameManager.Instance.LastEncounterId : -1);
            set => _triggeringEncounterId = value;
        }

        public BattleActor ActiveActor { get; private set; }
        public AttackSkill SelectedSkill { get; private set; }
        public BattleActor SelectedTarget { get; private set; }
        public D20RollResult LastRollResult { get; private set; }
        public AttackExecutionResult LastExecutionResult { get; private set; }

        public PartyManager Party => _partyManager;
        public IReadOnlyList<BattleActor> TurnTimeline => _currentRoundOrder;
        public IReadOnlyList<AttackSkill> AvailableSkills => _defaultSkills;

        // 편의 및 단위 테스트용 호환 프로퍼티
        public BattleActor Player => (_partyManager != null && _partyManager.Allies.Count > 0) ? _partyManager.Allies[0] : null;
        public IReadOnlyList<BattleActor> Allies => _partyManager != null ? _partyManager.Allies : Array.Empty<BattleActor>();
        public IReadOnlyList<BattleActor> Enemies => _partyManager != null ? _partyManager.Enemies : Array.Empty<BattleActor>();

        // 이벤트 발행
        public event Action<BattlePhase> OnStateChanged;
        public event Action<BattleActor> OnActiveActorChanged;
        public event Action<D20RollResult> OnD20Rolled;
        public event Action<AttackExecutionResult> OnActionExecuted;
        public event Action<string> OnBattleLog;

        private void Awake()
        {
            EnsurePartyManager();
            if (_diceRoller == null) _diceRoller = new D20DiceRoller();
            EnsureDefaultSkills();
        }

        private void Start()
        {
            StartBattle();
        }

        private void EnsurePartyManager()
        {
            if (_partyManager == null) _partyManager = GetComponent<PartyManager>() ?? FindAnyObjectByType<PartyManager>();
            if (_partyManager == null)
            {
                _partyManager = gameObject.AddComponent<PartyManager>();
            }
        }

        private void EnsureDefaultSkills()
        {
            if (_defaultSkills == null) _defaultSkills = new List<AttackSkill>();
            if (_defaultSkills.Count == 0)
            {
                _defaultSkills.Add(AttackSkill.CreateInstance("참격 (Slash)", baseDmg: 14, multiplier: 2.0f, hits: 1));
                _defaultSkills.Add(AttackSkill.CreateInstance("연속 베기 (Dual Strike)", baseDmg: 9, multiplier: 1.5f, hits: 2));
                _defaultSkills.Add(AttackSkill.CreateInstance("결전 강타 (Heavy Blow)", baseDmg: 24, multiplier: 3.5f, hits: 1));
            }
        }

        /// <summary>
        /// 단위 테스트 및 헤드리스 환경에서 파티 데이터를 즉시 초기화하고 전투 대기 상태로 설정합니다.
        /// </summary>
        public void SetupBattle(int? diceSeed = null)
        {
            EnsurePartyManager();
            if (_diceRoller == null || diceSeed.HasValue) _diceRoller = new D20DiceRoller(diceSeed ?? 12345);
            EnsureDefaultSkills();

            _partyManager.InitializeParties();
            RoundNumber = 1;
            TurnInRound = 1;

            if (_partyManager.Allies.Count > 0)
            {
                ActiveActor = _partyManager.Allies[0];
            }
            if (_defaultSkills.Count > 0)
            {
                SelectedSkill = _defaultSkills[0];
            }
            if (_partyManager.Enemies.Count > 0)
            {
                SelectedTarget = _partyManager.Enemies[0];
            }

            SetState(BattlePhase.PlayerInput);
        }

        /// <summary>
        /// 3 vs 3 단일 전투를 시작합니다 (코루틴 루프 실행).
        /// </summary>
        public void StartBattle(int? diceSeed = null)
        {
            if (diceSeed.HasValue) _diceRoller = new D20DiceRoller(diceSeed.Value);

            EnsurePartyManager();
            EnsureDefaultSkills();

            StopAllCoroutines();
            StartCoroutine(BattleStartRoutine());
        }

        private IEnumerator BattleStartRoutine()
        {
            SetState(BattlePhase.BattleStart);
            RoundNumber = 0;

            LogBattle("==================================================");
            LogBattle("⚔️ [3 vs 3] 2.5D 턴제 RPG 전투 개시!");
            LogBattle("==================================================");

            // 1. 파티 초기화 및 필드 배치
            _partyManager.InitializeParties();

            LogBattle($"[아군 파티 3인] {string.Join(", ", _partyManager.Allies.Select(a => $"{a.Name}(HP:{a.MaxHP}, Spd:{a.Speed})"))}");
            LogBattle($"[적군 파티 3인] {string.Join(", ", _partyManager.Enemies.Select(a => $"{a.Name}(HP:{a.MaxHP}, Spd:{a.Speed})"))}");

            yield return new WaitForSeconds(_turnStepDelay);

            // 첫 라운드 시작
            StartNewRound();
        }

        private void StartNewRound()
        {
            RoundNumber++;
            TurnInRound = 0;
            LogBattle($"\n========== [라운드 {RoundNumber} 시작] ==========");

            // 생존자들의 Speed 기반 턴 순서 계산
            _currentRoundOrder.Clear();
            _currentRoundOrder.AddRange(_partyManager.CalculateTurnOrder());

            _turnQueue.Clear();
            foreach (var actor in _currentRoundOrder)
            {
                _turnQueue.Enqueue(actor);
            }

            LogBattle($"⚡ [행동 순서] {string.Join(" ➔ ", _currentRoundOrder.Select(a => a.Name))}");

            ProceedNextTurn();
        }

        private void ProceedNextTurn()
        {
            // 생존 중인 다음 캐릭터 탐색
            while (_turnQueue.Count > 0)
            {
                var next = _turnQueue.Dequeue();
                if (next.IsAlive)
                {
                    ActiveActor = next;
                    TurnInRound++;
                    StartCoroutine(TurnSetupRoutine());
                    return;
                }
            }

            // 라운드 종료 -> 승패 검사 후 다음 라운드
            if (!CheckVictoryCondition())
            {
                StartNewRound();
            }
        }

        private IEnumerator TurnSetupRoutine()
        {
            SetState(BattlePhase.TurnSetup);
            _partyManager.SetActiveTurnActor(ActiveActor);
            OnActiveActorChanged?.Invoke(ActiveActor);

            LogBattle($"\n👉 [턴 {TurnInRound}] {ActiveActor.Name}의 차례! (HP: {ActiveActor.CurrentHP}/{ActiveActor.MaxHP})");

            yield return new WaitForSeconds(_turnStepDelay * 0.7f);

            if (ActiveActor.IsPlayer)
            {
                // 아군 턴: 플레이어 입력 대기
                SetState(BattlePhase.PlayerInput);
                SelectedSkill = _defaultSkills.FirstOrDefault();
                SelectedTarget = _partyManager.Enemies.FirstOrDefault(e => e.IsAlive);
            }
            else
            {
                // 적 턴: AI 자동 선택
                SetState(BattlePhase.EnemyAI);
                yield return StartCoroutine(EnemyAIRoutine());
            }
        }

        public void SelectSkill(int index)
        {
            if (index >= 0 && index < _defaultSkills.Count)
            {
                SelectedSkill = _defaultSkills[index];
                LogBattle($"[스킬 선택] {SelectedSkill.skillName}");
            }
        }

        public void SelectTarget(BattleActor target)
        {
            if (target != null && target.IsAlive)
            {
                SelectedTarget = target;
                LogBattle($"[타깃 지정] {SelectedTarget.Name} (HP: {SelectedTarget.CurrentHP}/{SelectedTarget.MaxHP})");
            }
        }

        /// <summary>
        /// 아군 턴에서 스킬 및 대상을 확정하고 D20 굴림 공격을 실행합니다.
        /// </summary>
        public void ConfirmPlayerAction(int? forcedD20Value = null)
        {
            if (CurrentPhase != BattlePhase.PlayerInput)
            {
                Debug.LogWarning($"[BattleTurnController] 현재 상태({CurrentPhase})에서는 행동을 확정할 수 없습니다.");
                return;
            }

            if (SelectedTarget == null || !SelectedTarget.IsAlive)
            {
                SelectedTarget = _partyManager.Enemies.FirstOrDefault(e => e.IsAlive);
            }

            if (SelectedTarget == null)
            {
                CheckVictoryCondition();
                return;
            }

            StartCoroutine(ActionExecuteRoutine(ActiveActor, SelectedSkill, SelectedTarget, forcedD20Value));
        }

        /// <summary>
        /// 하위 호환용 스킬 확정 실행 메서드
        /// </summary>
        public void ConfirmAndExecutePlayerSkill(int? forcedD20Value = null)
        {
            ConfirmPlayerAction(forcedD20Value);
        }

        /// <summary>
        /// 적군 AI: 체력이 가장 낮은 아군을 우선 타깃으로 자동 선정하여 공격을 시전합니다.
        /// </summary>
        private IEnumerator EnemyAIRoutine(int? forcedD20Value = null)
        {
            yield return new WaitForSeconds(_turnStepDelay);

            // 1. AI 타깃 선정 (가장 HP 비율이 낮은 아군)
            var target = _partyManager.Allies
                .Where(a => a.IsAlive)
                .OrderBy(a => (float)a.CurrentHP / a.MaxHP)
                .ThenBy(a => a.CurrentHP)
                .FirstOrDefault();

            if (target == null)
            {
                CheckVictoryCondition();
                yield break;
            }

            // 2. AI 스킬 무작위 선정
            var skill = _defaultSkills[UnityEngine.Random.Range(0, _defaultSkills.Count)];

            LogBattle($"🤖 [적 AI] {ActiveActor.Name} ➔ 최저 체력 타깃 [{target.Name}]을(를) 향해 [{skill.skillName}] 시전!");

            yield return StartCoroutine(ActionExecuteRoutine(ActiveActor, skill, target, forcedD20Value));
        }

        /// <summary>
        /// ACTION_EXECUTE: 스킬 시전 -> D20 물리 롤 -> 판정 -> 타격 피드백 -> 대미지/즉사 적용
        /// </summary>
        private IEnumerator ActionExecuteRoutine(BattleActor attacker, AttackSkill skill, BattleActor target, int? forcedD20Value)
        {
            SetState(BattlePhase.ActionExecute);

            D20RollResult roll = default;
            bool rollFinished = false;

            // 그래픽 환경인 경우 3D 물리 D20 주사위 굴림 및 시각 피드백 실행
            var feedbackMgr = BattleFeedbackManager.Instance;
            if (feedbackMgr != null && !Application.isBatchMode && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                feedbackMgr.ExecuteDiceRollSequence(forcedD20Value, (res) =>
                {
                    roll = _diceRoller.ProcessPhysicsResult(res.Value);
                    rollFinished = true;
                });

                while (!rollFinished)
                {
                    yield return null;
                }
            }
            else
            {
                // 헤드리스/CLI 환경: 순수 소프트웨어 난수 또는 강제값 즉시 평가
                roll = forcedD20Value.HasValue
                    ? _diceRoller.ProcessPhysicsResult(forcedD20Value.Value)
                    : _diceRoller.Roll();

                yield return new WaitForSeconds(_turnStepDelay * 0.4f);
            }

            LastRollResult = roll;
            OnD20Rolled?.Invoke(roll);
            LogBattle($"🎲 [D20 결과] {roll}");

            // 타격감 피드백 트리거 (히트스톱 & 2.5D 카메라 셰이크)
            if (ImpactFeedbackManager.Instance != null)
            {
                ImpactFeedbackManager.Instance.TriggerImpact(roll.Value);
            }

            // 스킬 실행 및 대미지/즉사 적용
            AttackExecutionResult result = skill.Execute(roll, attacker, target);
            LastExecutionResult = result;
            OnActionExecuted?.Invoke(result);
            LogBattle(result.LogMessage);

            yield return new WaitForSeconds(_turnStepDelay);

            // 승패 검사
            SetState(BattlePhase.CheckVictory);
            if (CheckVictoryCondition()) yield break;

            // 다음 턴 진행
            ProceedNextTurn();
        }

        /// <summary>
        /// 아군 또는 적군의 전멸 여부를 검사하고 승패 상태로 전이합니다.
        /// </summary>
        public bool CheckVictoryCondition()
        {
            if (_partyManager == null) return false;

            if (_partyManager.IsEnemiesDefeated)
            {
                SetState(BattlePhase.BattleVictory);
                _partyManager.SetActiveTurnActor(null);
                LogBattle("\n==================================================");
                LogBattle("🎉 [전투 승리! (VICTORY)] 적군을 모두 격파했습니다!");
                LogBattle("==================================================");

                // [요구 산출물 3] 적 처치 데이터 기록 (DefeatedEnemyIDs에 등록)
                RecordBattleVictory();

                return true;
            }

            if (_partyManager.IsAlliesDefeated)
            {
                SetState(BattlePhase.BattleDefeat);
                _partyManager.SetActiveTurnActor(null);
                LogBattle("\n==================================================");
                LogBattle("💀 [전투 패배... (DEFEAT)] 아군이 모두 쓰러졌습니다.");
                LogBattle("==================================================");
                return true;
            }

            return false;
        }

        /// <summary>
        /// [요구 산출물 3] 적 처치 데이터 기록:
        /// 전투 승리 시, 현재 전투를 유발한 적 심볼의 고유 ID를 GameManagerData.DefeatedEnemyIDs에 기록합니다.
        /// </summary>
        public void RecordBattleVictory()
        {
            if (GameManager.Instance == null) return;

            string idToRecord = TriggeringEnemyID;
            if (!string.IsNullOrEmpty(idToRecord))
            {
                GameManager.Instance.MarkEncounterDefeated(idToRecord);
                Debug.Log($"[BattleTurnController] 📝 처치 적 심볼 ID 기록 완료: {idToRecord}");
            }

            int encId = TriggeringEncounterId;
            if (encId >= 0)
            {
                GameManager.Instance.MarkEncounterDefeated(encId);
            }
        }

        /// <summary>
        /// [요구 산출물 1] 전투 승리 후 탐색 씬(Level01_Exploration)으로 비동기 로드 복귀합니다.
        /// </summary>
        public void ReturnToExploration(string sceneName = "Level01_Exploration")
        {
            RecordBattleVictory();

            Debug.Log($"[BattleTurnController] 🗺️ 탐색 씬 '{sceneName}'(으)로 복귀 로드 시작");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBattleVictory(sceneName);
            }
            else
            {
                SceneManager.LoadSceneAsync(sceneName);
            }
        }

        /// <summary>
        /// 테스트 및 이전 버전 호환용 승패 검사 메서드
        /// </summary>
        public bool CheckBattleEnd()
        {
            return CheckVictoryCondition();
        }

        /// <summary>
        /// CLI 및 단위 테스트용: 1회 공격 행동을 동기적(프레임 대기 없이)으로 즉시 시뮬레이션합니다.
        /// </summary>
        public AttackExecutionResult SimulateActionSync(BattleActor attacker, AttackSkill skill, BattleActor target, int d20Value)
        {
            if (attacker == null || skill == null || target == null)
            {
                Debug.LogWarning("[BattleTurnController] SimulateActionSync: 액터 또는 스킬이 null입니다.");
                return default;
            }

            if (_diceRoller == null) _diceRoller = new D20DiceRoller();

            var roll = _diceRoller.ProcessPhysicsResult(d20Value);
            LastRollResult = roll;
            OnD20Rolled?.Invoke(roll);

            var result = skill.Execute(roll, attacker, target);
            LastExecutionResult = result;
            OnActionExecuted?.Invoke(result);

            LogBattle($"[CLI Sync] {result.LogMessage}");
            return result;
        }

        /// <summary>
        /// CLI 및 단위 테스트용: 플레이어(아군)의 공격 행동을 동기적으로 즉시 시뮬레이션합니다.
        /// </summary>
        public AttackExecutionResult SimulatePlayerActionSync(int skillIndex, int targetIndex, int d20Value)
        {
            EnsurePartyManager();
            EnsureDefaultSkills();

            var skill = (skillIndex >= 0 && skillIndex < _defaultSkills.Count) ? _defaultSkills[skillIndex] : _defaultSkills[0];
            var enemies = _partyManager.Enemies;
            var target = (targetIndex >= 0 && targetIndex < enemies.Count) ? enemies[targetIndex] : enemies.FirstOrDefault(e => e.IsAlive);
            var attacker = (ActiveActor != null && ActiveActor.IsAlive && ActiveActor.IsPlayer) ? ActiveActor : Player;

            var result = SimulateActionSync(attacker, skill, target, d20Value);
            CheckVictoryCondition();
            return result;
        }

        /// <summary>
        /// CLI 및 단위 테스트용: 적군의 공격 행동을 동기적으로 즉시 시뮬레이션합니다.
        /// </summary>
        public AttackExecutionResult SimulateEnemyActionSync(int enemyIndex, int skillIndex, int d20Value, int targetAllyIndex = 0)
        {
            EnsurePartyManager();
            EnsureDefaultSkills();

            var skill = (skillIndex >= 0 && skillIndex < _defaultSkills.Count) ? _defaultSkills[skillIndex] : _defaultSkills[0];
            var enemies = _partyManager.Enemies;
            var attacker = (enemyIndex >= 0 && enemyIndex < enemies.Count) ? enemies[enemyIndex] : enemies.FirstOrDefault(e => e.IsAlive);
            var allies = _partyManager.Allies;
            var target = (targetAllyIndex >= 0 && targetAllyIndex < allies.Count) ? allies[targetAllyIndex] : (Player ?? allies.FirstOrDefault(a => a.IsAlive));

            var result = SimulateActionSync(attacker, skill, target, d20Value);
            CheckVictoryCondition();
            return result;
        }

        /// <summary>
        /// CLI 및 에디터 테스트용: 아군 3인 vs 적군 3인의 전체 전투를 프레임 대기 없이 동기적으로 완주 시뮬레이션합니다.
        /// </summary>
        public BattlePhase SimulateFullBattleSync(int maxRounds = 30)
        {
            EnsurePartyManager();
            EnsureDefaultSkills();
            if (_diceRoller == null) _diceRoller = new D20DiceRoller();

            _partyManager.InitializeParties();
            RoundNumber = 0;

            LogBattle($"\n[CLI Batch Simulation] 3 vs 3 전체 전투 시뮬레이션 시작 (최대 {maxRounds}라운드)");

            while (RoundNumber < maxRounds)
            {
                RoundNumber++;
                var turnOrder = _partyManager.CalculateTurnOrder();

                foreach (var actor in turnOrder)
                {
                    if (!actor.IsAlive) continue;
                    if (_partyManager.IsEnemiesDefeated || _partyManager.IsAlliesDefeated) break;

                    // 타깃 선정
                    BattleActor target = null;
                    if (actor.IsPlayer)
                    {
                        target = _partyManager.Enemies.FirstOrDefault(e => e.IsAlive);
                    }
                    else
                    {
                        target = _partyManager.Allies.Where(a => a.IsAlive).OrderBy(a => a.CurrentHP).FirstOrDefault();
                    }

                    if (target == null) break;

                    var skill = _defaultSkills[UnityEngine.Random.Range(0, _defaultSkills.Count)];
                    var roll = _diceRoller.Roll();
                    skill.Execute(roll, actor, target);
                }

                if (_partyManager.IsEnemiesDefeated)
                {
                    SetState(BattlePhase.BattleVictory);
                    LogBattle($"🏆 [시뮬레이션 결과] {RoundNumber}라운드 만에 아군 승리(VICTORY)!");
                    return BattlePhase.BattleVictory;
                }

                if (_partyManager.IsAlliesDefeated)
                {
                    SetState(BattlePhase.BattleDefeat);
                    LogBattle($"💀 [시뮬레이션 결과] {RoundNumber}라운드 만에 아군 패배(DEFEAT)...");
                    return BattlePhase.BattleDefeat;
                }
            }

            LogBattle($"⏳ [시뮬레이션 결과] {maxRounds}라운드 초과");
            return BattlePhase.CheckVictory;
        }

        private void SetState(BattlePhase newState)
        {
            CurrentPhase = newState;
            OnStateChanged?.Invoke(newState);
        }

        private void LogBattle(string msg)
        {
            Debug.Log($"[3v3Battle] {msg}");
            OnBattleLog?.Invoke(msg);
        }
    }
}
