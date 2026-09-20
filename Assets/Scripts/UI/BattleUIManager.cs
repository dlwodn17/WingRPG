using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using RPG25D.Core.Battle;
using RPG25D.Core.Dice;
using RPG25D.Data;
using RPG25D.Gameplay;

namespace RPG25D.UI
{
    /// <summary>
    /// [요구 산출물 3] BattleUIManager.cs
    /// 아군 3인 vs 적군 3인의 인게임 HUD 및 승리(Victory)/패배(Defeat) 팝업 UI 매니저입니다.
    /// MVVM/이벤트 구독 패턴을 통해 BattleTurnController와 PartyManager의 데이터 변화를 실시간 반영하며,
    /// 1280x720 가상 좌표계 변환을 통해 모든 해상도에서 완벽한 2.5D 레이아웃을 제공합니다.
    /// </summary>
    public class BattleUIManager : MonoBehaviour
    {
        [Header("연결 컴포넌트")]
        [SerializeField] private BattleTurnController _turnController;

        private readonly List<string> _battleLogs = new List<string>();

        // GUI 스타일
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _d20Style;
        private GUIStyle _popupTitleStyle;
        private GUIStyle _popupSubStyle;
        private GUIStyle _logStyle;

        private int _selectedSkillIndex = 0;

        private void Start()
        {
            if (_turnController == null) _turnController = FindAnyObjectByType<BattleTurnController>();

            if (_turnController != null)
            {
                _turnController.OnBattleLog += AddLog;
                _turnController.OnStateChanged += HandleStateChanged;
            }

            var party = _turnController != null ? _turnController.Party : FindAnyObjectByType<PartyManager>();
            if (party != null)
            {
                party.OnPartyUpdated += RepaintUI;
            }
        }

        private void OnDestroy()
        {
            if (_turnController != null)
            {
                _turnController.OnBattleLog -= AddLog;
                _turnController.OnStateChanged -= HandleStateChanged;
            }

            var party = _turnController != null ? _turnController.Party : null;
            if (party != null)
            {
                party.OnPartyUpdated -= RepaintUI;
            }
        }

        private void AddLog(string msg)
        {
            _battleLogs.Add(msg);
            if (_battleLogs.Count > 35) _battleLogs.RemoveAt(0);
        }

        private void HandleStateChanged(BattlePhase state)
        {
            // 상태 변화 시 필요한 사운드나 연출 트리거 가능
        }

        private void RepaintUI() { }

        private void InitStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            _headerStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _selectedButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _bannerStyle.normal.textColor = Color.white;

            _d20Style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _popupTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _popupSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            _logStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
        }

        private void OnGUI()
        {
            InitStyles();

            // 1280x720 가상 해상도 좌표 변환
            Matrix4x4 origMatrix = GUI.matrix;
            float rx = (float)Screen.width / 1280f;
            float ry = (float)Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(rx, ry, 1f));

            if (_turnController == null || _turnController.Party == null)
            {
                GUI.matrix = origMatrix;
                return;
            }

            // 1. 상단 타임라인 배너
            DrawTopTimelineBanner();

            // 2. 아군 3인 상태 패널 (좌상단 세로 배치)
            DrawAllyPartyPanels();

            // 3. 적군 3인 상태 패널 (우상단 세로 배치)
            DrawEnemyPartyPanels();

            // 4. 하단 공격 제어 데크
            DrawBottomControlDeck();

            // 5. 승리/패배 결과 팝업 오버레이
            if (_turnController.CurrentState == BattlePhase.BattleVictory)
            {
                DrawVictoryPopup();
            }
            else if (_turnController.CurrentState == BattlePhase.BattleDefeat)
            {
                DrawDefeatPopup();
            }

            GUI.matrix = origMatrix;
        }

        private void DrawTopTimelineBanner()
        {
            string stateDesc = GetStateDescription(_turnController.CurrentState);
            string banner = $"[라운드 {_turnController.RoundNumber}] {stateDesc}";

            GUI.backgroundColor = new Color(0.1f, 0.15f, 0.25f, 0.88f);
            GUI.Box(new Rect(380, 10, 520, 34), banner, _bannerStyle);
            GUI.backgroundColor = Color.white;

            // 행동 순서 타임라인 미니 칩 (Timeline)
            var timeline = _turnController.TurnTimeline;
            if (timeline != null && timeline.Count > 0)
            {
                int chipX = 380;
                int chipW = 520 / Mathf.Max(1, timeline.Count);
                for (int i = 0; i < timeline.Count; i++)
                {
                    var actor = timeline[i];
                    bool isActive = actor == _turnController.ActiveActor;

                    Color bg = actor.IsPlayer ? new Color(0.2f, 0.6f, 0.9f, 0.75f) : new Color(0.85f, 0.3f, 0.2f, 0.75f);
                    if (isActive) bg = Color.yellow;
                    if (!actor.IsAlive) bg = Color.gray;

                    Color orig = GUI.backgroundColor;
                    GUI.backgroundColor = bg;
                    string tag = actor.IsAlive ? actor.Name.Split(' ')[0] : "사망";
                    GUI.Box(new Rect(chipX, 46, chipW - 2, 20), tag);
                    GUI.backgroundColor = orig;

                    chipX += chipW;
                }
            }
        }

        private void DrawAllyPartyPanels()
        {
            var allies = _turnController.Party.Allies;
            int sy = 12;

            for (int i = 0; i < allies.Count; i++)
            {
                var ally = allies[i];
                bool isActive = ally == _turnController.ActiveActor;

                Color panelBg = isActive ? new Color(0.15f, 0.45f, 0.75f, 0.95f) : new Color(0.08f, 0.18f, 0.30f, 0.82f);
                GUI.backgroundColor = panelBg;
                GUI.Box(new Rect(15, sy, 220, 58), "");
                GUI.backgroundColor = Color.white;

                string activeTag = isActive ? " <color=yellow>★[행동중]</color>" : "";
                string title = $"🛡️ {ally.Name}{activeTag}";
                GUI.Label(new Rect(25, sy + 3, 200, 18), $"<b>{title}</b>", _headerStyle);

                string hpText = ally.IsAlive ? $"HP: {ally.CurrentHP} / {ally.MaxHP} (Spd:{ally.Speed})" : "<color=gray>[전투 불능]</color>";
                GUI.Label(new Rect(25, sy + 22, 200, 16), hpText);

                if (ally.IsAlive)
                {
                    DrawBar(new Rect(25, sy + 40, 200, 10), (float)ally.CurrentHP / ally.MaxHP, Color.green);
                }

                sy += 64;
            }
        }

        private void DrawEnemyPartyPanels()
        {
            var enemies = _turnController.Party.Enemies;
            int sy = 12;
            int x = 1280 - 235;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                bool isSelected = enemy == _turnController.SelectedTarget;
                bool isActive = enemy == _turnController.ActiveActor;

                Color panelBg = isSelected
                    ? new Color(0.85f, 0.25f, 0.15f, 0.95f)
                    : (isActive ? new Color(0.55f, 0.15f, 0.10f, 0.9f) : new Color(0.28f, 0.08f, 0.08f, 0.82f));

                GUI.backgroundColor = panelBg;
                GUI.Box(new Rect(x, sy, 220, 58), "");
                GUI.backgroundColor = Color.white;

                string targetTag = isSelected ? " <color=yellow>[타깃]</color>" : (isActive ? " <color=orange>★[행동]</color>" : "");
                GUI.Label(new Rect(x + 10, sy + 3, 200, 18), $"<b>👾 {enemy.Name}</b>{targetTag}", _headerStyle);

                string hpText = enemy.IsAlive ? $"HP: {enemy.CurrentHP} / {enemy.MaxHP} (Spd:{enemy.Speed})" : "<color=gray>[격파됨]</color>";
                GUI.Label(new Rect(x + 10, sy + 22, 200, 16), hpText);

                if (enemy.IsAlive)
                {
                    DrawBar(new Rect(x + 10, sy + 40, 140, 10), (float)enemy.CurrentHP / enemy.MaxHP, Color.red);

                    // 타깃 선택 버튼
                    if (_turnController.CurrentState == BattlePhase.PlayerInput)
                    {
                        if (GUI.Button(new Rect(x + 155, sy + 25, 60, 26), "선택", _buttonStyle))
                        {
                            _turnController.SelectTarget(enemy);
                        }
                    }
                }

                sy += 64;
            }
        }

        private void DrawBottomControlDeck()
        {
            int deckY = 510;
            int deckH = 200;

            GUI.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            GUI.Box(new Rect(10, deckY, 1260, deckH), "");
            GUI.backgroundColor = Color.white;

            // 좌측: 공격 스킬 3종 선택
            DrawSkillSelection(20, deckY + 10, 360);

            // 중앙: D20 주사위 및 공격 실행 버튼
            DrawD20ActionSection(395, deckY + 10, 490);

            // 우측: 전투 기록 로그
            DrawBattleLogSection(900, deckY + 10, 360, deckH - 20);
        }

        private void DrawSkillSelection(int x, int y, int width)
        {
            string actorName = _turnController.ActiveActor != null ? _turnController.ActiveActor.Name : "아군";
            GUI.Label(new Rect(x, y, width, 20), $"<b>1. [{actorName}] 스킬 선택</b>", _headerStyle);

            var skills = _turnController.AvailableSkills;
            int btnH = 42;
            int sy = y + 25;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                bool isSelected = i == _selectedSkillIndex;

                Color orig = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? new Color(0.2f, 0.7f, 1f) : new Color(0.25f, 0.28f, 0.35f);

                string hitStr = skill.hitCount > 1 ? $" ({skill.hitCount}회 연속)" : "";
                string label = $"{skill.skillName}{hitStr}\n<size=11>기본 {skill.baseDamage} + (D20 * {skill.scaleMultiplier:F1})</size>";

                GUI.enabled = _turnController.CurrentState == BattlePhase.PlayerInput;
                if (GUI.Button(new Rect(x, sy, width, btnH), label, _buttonStyle))
                {
                    _selectedSkillIndex = i;
                    _turnController.SelectSkill(i);
                }
                GUI.enabled = true;

                GUI.backgroundColor = orig;
                sy += 48;
            }
        }

        private void DrawD20ActionSection(int x, int y, int width)
        {
            string targetName = _turnController.SelectedTarget != null ? _turnController.SelectedTarget.Name : "미지정";
            GUI.Label(new Rect(x, y, width, 20), $"<b>2. 공격 대상: <color=yellow>[{targetName}]</color></b>", _headerStyle);

            // 최근 D20 롤 결과 박스
            int d20Y = y + 25;
            var lastRoll = _turnController.LastRollResult;
            string d20Text = lastRoll.Value > 0 ? $"{lastRoll}" : "🎲 [D20 굴림 대기 중]";

            Color d20Color = Color.white;
            if (lastRoll.Outcome == D20Outcome.Fail) d20Color = new Color(1f, 0.35f, 0.35f);
            else if (lastRoll.Outcome == D20Outcome.InstantKill) d20Color = new Color(1f, 0.88f, 0.2f);
            else if (lastRoll.Outcome == D20Outcome.Hit) d20Color = new Color(0.4f, 1f, 0.5f);

            GUI.color = d20Color;
            GUI.Box(new Rect(x, d20Y, width, 55), d20Text, _d20Style);
            GUI.color = Color.white;

            // [D20 굴림 및 스킬 발동] 메인 버튼
            bool canAct = _turnController.CurrentState == BattlePhase.PlayerInput
                          && _turnController.SelectedTarget != null
                          && _turnController.SelectedTarget.IsAlive;

            GUI.enabled = canAct;
            GUI.backgroundColor = canAct ? new Color(0.1f, 0.85f, 0.4f) : Color.gray;

            if (GUI.Button(new Rect(x, d20Y + 65, width, 52), "⚔️ D20 굴림 및 공격 실행 (Roll D20 & Execute)", _buttonStyle))
            {
                _turnController.ConfirmPlayerAction();
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void DrawBattleLogSection(int x, int y, int width, int height)
        {
            GUI.Label(new Rect(x, y, width, 20), "<b>📜 전투 기록</b>", _headerStyle);

            GUI.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.85f);
            GUI.Box(new Rect(x, y + 24, width, height - 24), "");
            GUI.backgroundColor = Color.white;

            int lineY = y + 28;
            var recentLogs = _battleLogs.Skip(Mathf.Max(0, _battleLogs.Count - 6)).Take(6);
            foreach (var log in recentLogs)
            {
                GUI.Label(new Rect(x + 8, lineY, width - 16, 20), log, _logStyle);
                lineY += 22;
            }
        }

        private void DrawVictoryPopup()
        {
            // 화면 중앙 모달 배경
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int popW = 540;
            int popH = 320;
            int px = (1280 - popW) / 2;
            int py = (720 - popH) / 2;

            GUI.backgroundColor = new Color(0.12f, 0.25f, 0.15f, 0.95f);
            GUI.Box(new Rect(px, py, popW, popH), "");
            GUI.backgroundColor = Color.white;

            _popupTitleStyle.normal.textColor = new Color(1.0f, 0.88f, 0.2f);
            GUI.Label(new Rect(px, py + 25, popW, 45), "🏆 VICTORY (전투 승리!)", _popupTitleStyle);

            _popupSubStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(px, py + 80, popW, 25), $"모든 적을 완벽하게 격파했습니다! (총 라운드: {_turnController.RoundNumber})", _popupSubStyle);

            // [요구 산출물 1] 승리 팝업 '확인' 버튼: Level01_Exploration 탐색 씬으로 비동기 로드 복귀
            int mainBtnW = 280;
            int mainBtnH = 50;
            int mainBtnX = px + (popW - mainBtnW) / 2;
            int mainBtnY = py + 130;

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
            if (GUI.Button(new Rect(mainBtnX, mainBtnY, mainBtnW, mainBtnH), "✅ 확인 (탐색으로 복귀)", _buttonStyle))
            {
                ReturnToExploration();
            }

            // 하단 보조 버튼들: [다음 전투], [마을로]
            int subBtnW = 160;
            int subBtnH = 38;
            int subBtnY = py + 200;

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
            if (GUI.Button(new Rect(px + 80, subBtnY, subBtnW, subBtnH), "⚔️ 다음 전투", _buttonStyle))
            {
                _turnController.StartBattle();
            }

            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            if (GUI.Button(new Rect(px + 300, subBtnY, subBtnW, subBtnH), "🏰 마을로", _buttonStyle))
            {
                _turnController.StartBattle();
            }

            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// [요구 산출물 1] SceneManager.LoadSceneAsync를 통해 탐색 씬(Level01_Exploration)으로 복귀합니다.
        /// </summary>
        public void ReturnToExploration(string sceneName = "Level01_Exploration")
        {
            if (_turnController != null)
            {
                _turnController.ReturnToExploration(sceneName);
            }
            else
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnBattleVictory(sceneName);
                }
                else
                {
                    SceneManager.LoadSceneAsync(sceneName);
                }
            }
        }

        private void DrawDefeatPopup()
        {
            // 화면 전체 어두운 회색조(Grayscale) 오버레이
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int popW = 540;
            int popH = 320;
            int px = (1280 - popW) / 2;
            int py = (720 - popH) / 2;

            GUI.backgroundColor = new Color(0.28f, 0.10f, 0.10f, 0.95f);
            GUI.Box(new Rect(px, py, popW, popH), "");
            GUI.backgroundColor = Color.white;

            _popupTitleStyle.normal.textColor = new Color(0.95f, 0.25f, 0.25f);
            GUI.Label(new Rect(px, py + 25, popW, 45), "💀 DEFEAT (전투 패배...)", _popupTitleStyle);

            _popupSubStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(px, py + 80, popW, 25), "아군 파티가 모두 쓰러졌습니다.", _popupSubStyle);

            // 버튼들: [재도전], [나가기]
            int btnW = 200;
            int btnH = 48;
            int btnY = py + 190;

            GUI.backgroundColor = new Color(0.85f, 0.3f, 0.2f);
            if (GUI.Button(new Rect(px + 50, btnY, btnW, btnH), "🔄 재도전 (Retry)", _buttonStyle))
            {
                _turnController.StartBattle();
            }

            GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            if (GUI.Button(new Rect(px + 290, btnY, btnW, btnH), "🚪 나가기 (Exit)", _buttonStyle))
            {
                _turnController.StartBattle();
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawBar(Rect rect, float fill01, Color color)
        {
            GUI.Box(rect, "");
            Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill01), rect.height);
            Color orig = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = orig;
        }

        private string GetStateDescription(BattlePhase state)
        {
            switch (state)
            {
                case BattlePhase.BattleStart: return "전투 진입 및 파티 배치 중...";
                case BattlePhase.TurnSetup: return "행동 캐릭터 선정 중...";
                case BattlePhase.PlayerInput: return "아군 턴: 스킬과 대상을 선택하고 공격하세요!";
                case BattlePhase.EnemyAI: return "적 AI 턴: 대상 선정 및 스킬 연산 중...";
                case BattlePhase.ActionExecute: return "스킬 시전 및 D20 주사위 굴림 진행 중!";
                case BattlePhase.CheckVictory: return "승패 판정 확인 중...";
                case BattlePhase.BattleVictory: return "🎉 축하합니다! 전투 승리!";
                case BattlePhase.BattleDefeat: return "💀 아군 전멸... 패배했습니다.";
                default: return state.ToString();
            }
        }
    }
}
