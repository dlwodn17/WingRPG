using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPG25D.Core.Exploration
{
    /// <summary>
    /// 순수 C# 기반 심볼 인카운터 판정 도메인 모델
    /// 거리 및 처치 여부 기반으로 인카운터 발동을 판정합니다.
    /// </summary>
    public class EncounterDetectionModel : IEncounterDetection
    {
        public float MaxVerticalDifference { get; set; } = 2.0f;

        public bool CheckEncounter(Vector3 playerPos, Vector3 enemyPos, float triggerDistance)
        {
            // 수직 높이 차이가 너무 크면 2.5D 단차로 간주하여 인카운터 제외
            if (Mathf.Abs(playerPos.y - enemyPos.y) > MaxVerticalDifference)
            {
                return false;
            }

            // X-Z 평면 거리 제곱 비교 (고속 연산)
            float dx = playerPos.x - enemyPos.x;
            float dz = playerPos.z - enemyPos.z;
            float sqrDist = dx * dx + dz * dz;

            return sqrDist <= (triggerDistance * triggerDistance);
        }

        public bool IsEncounterActive(int encounterId, IReadOnlyCollection<int> defeatedIds)
        {
            if (defeatedIds == null || defeatedIds.Count == 0) return true;
            return !defeatedIds.Contains(encounterId);
        }

        public int? DetectNearestEncounter(Vector3 playerPos, IEnumerable<EncounterSymbolData> symbols, IReadOnlyCollection<int> defeatedIds)
        {
            if (symbols == null) return null;

            int? nearestId = null;
            float minSqrDist = float.MaxValue;

            foreach (var symbol in symbols)
            {
                if (!IsEncounterActive(symbol.encounterId, defeatedIds))
                {
                    continue;
                }

                float dx = playerPos.x - symbol.position.x;
                float dz = playerPos.z - symbol.position.z;
                float sqrDist = dx * dx + dz * dz;
                float maxAllowedSqr = symbol.triggerRadius * symbol.triggerRadius;

                if (sqrDist <= maxAllowedSqr && sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearestId = symbol.encounterId;
                }
            }

            return nearestId;
        }
    }
}
