namespace RPG25D.Core.Battle
{
    /// <summary>
    /// 주사위 턴제 전투의 세부 턴 단계 상태 열거형
    /// </summary>
    public enum BattleTurnState
    {
        None,
        BattleStart,            // 전투 진입
        PlayerTurnStart,        // 플레이어 턴 시작
        RollPhase,              // 주사위 굴림
        AssignPhase,            // 주사위 슬롯 배치
        ExecutePlayerActions,   // 플레이어 스킬 행동 순차 실행
        EnemyTurn,              // 적 턴 실행
        TurnEnd,                // 턴 종료 처리 및 상태 정리
        Victory,                // 모든 적 격파
        Defeat                  // 플레이어 사망
    }
}
