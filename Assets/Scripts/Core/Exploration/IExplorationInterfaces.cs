using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG25D.Core.Exploration
{
    /// <summary>
    /// 플레이어 이동 모델 인터페이스 (순수 C# 단위 테스트 가능)
    /// </summary>
    public interface IExplorationMovementModel
    {
        Vector3 Position { get; }
        Vector3 MoveDirection { get; }
        float MoveSpeed { get; set; }
        bool IsMoving { get; }
        bool IsFacingRight { get; }

        void UpdateMovement(Vector2 inputAxis, float deltaTime);
        void SetPosition(Vector3 newPosition);
        Vector2Int WorldToGrid(Vector3 worldPos, float tileSize);
        Vector3 GridToWorld(Vector2Int gridPos, float tileSize);
    }

    /// <summary>
    /// 필드 위의 적 심볼 데이터 구조체
    /// </summary>
    [System.Serializable]
    public struct EncounterSymbolData
    {
        public int encounterId;
        public string monsterName;
        public Vector3 position;
        public float triggerRadius;

        public EncounterSymbolData(int id, string name, Vector3 pos, float radius = 1.2f)
        {
            encounterId = id;
            monsterName = name;
            position = pos;
            triggerRadius = radius;
        }
    }

    /// <summary>
    /// 심볼 인카운터 감지 및 판정 인터페이스 (순수 C# 단위 테스트 가능)
    /// </summary>
    public interface IEncounterDetection
    {
        bool CheckEncounter(Vector3 playerPos, Vector3 enemyPos, float triggerDistance);
        bool IsEncounterActive(int encounterId, IReadOnlyCollection<int> defeatedIds);
        int? DetectNearestEncounter(Vector3 playerPos, IEnumerable<EncounterSymbolData> symbols, IReadOnlyCollection<int> defeatedIds);
    }

    /// <summary>
    /// 상호작용 이벤트 실행 시 전달되는 탐색 컨텍스트
    /// </summary>
    public class ExplorationContext
    {
        public Vector3 PlayerPosition { get; set; }
        public List<string> Inventory { get; } = new List<string>();
        public int Gold { get; set; }
        public HashSet<string> TriggeredEventIds { get; } = new HashSet<string>();

        public ExplorationContext(Vector3 playerPos, int initialGold = 0)
        {
            PlayerPosition = playerPos;
            Gold = initialGold;
        }
    }

    /// <summary>
    /// 이벤트 실행 결과 데이터
    /// </summary>
    public class ExplorationEventResult
    {
        public bool Success { get; }
        public string EventId { get; }
        public string Message { get; }
        public string AwardedItem { get; }
        public int AwardedGold { get; }

        public ExplorationEventResult(bool success, string eventId, string message, string awardedItem = null, int awardedGold = 0)
        {
            Success = success;
            EventId = eventId;
            Message = message;
            AwardedItem = awardedItem;
            AwardedGold = awardedGold;
        }
    }

    /// <summary>
    /// 탐색 필드 이벤트 인터페이스 (상자, NPC 등)
    /// </summary>
    public interface IExplorationEvent
    {
        string EventId { get; }
        string EventName { get; }
        bool CanExecute(ExplorationContext context);
        ExplorationEventResult Execute(ExplorationContext context);
    }
}
