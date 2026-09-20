using System;
using UnityEngine;

namespace RPG25D.Core.Exploration
{
    /// <summary>
    /// 순수 C# 기반 2.5D 탐색 이동 도메인 모델
    /// X-Z 평면 이동, 8방향 정규화, 그리드 변환 및 맵 바운드 검사를 수행합니다.
    /// </summary>
    public class ExplorationMovementModel : IExplorationMovementModel
    {
        public Vector3 Position { get; private set; }
        public Vector3 MoveDirection { get; private set; }
        public float MoveSpeed { get; set; } = 4.5f;
        public bool IsMoving => MoveDirection.sqrMagnitude > 0.001f;
        public bool IsFacingRight { get; private set; } = true;

        // 맵 바운드 (기본 넓은 범위)
        public Bounds? MapBounds { get; set; }

        public ExplorationMovementModel(Vector3 initialPosition, float speed = 4.5f)
        {
            Position = initialPosition;
            MoveSpeed = speed;
            MoveDirection = Vector3.zero;
        }

        public void UpdateMovement(Vector2 inputAxis, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // 1. 입력 벡터 정규화 (8방향 대각선 이동 속도 균등화)
            if (inputAxis.sqrMagnitude > 1f)
            {
                inputAxis.Normalize();
            }

            // 2. 2.5D X-Z 평면 이동 벡터 계산
            Vector3 desiredDir = new Vector3(inputAxis.x, 0f, inputAxis.y);
            MoveDirection = desiredDir;

            if (desiredDir.sqrMagnitude > 0.001f)
            {
                // 수평 이동에 따른 좌우 시선 갱신
                if (desiredDir.x > 0.05f) IsFacingRight = true;
                else if (desiredDir.x < -0.05f) IsFacingRight = false;

                Vector3 nextPos = Position + desiredDir * (MoveSpeed * deltaTime);

                // 3. 맵 바운드 클램프
                if (MapBounds.HasValue)
                {
                    var b = MapBounds.Value;
                    nextPos.x = Mathf.Clamp(nextPos.x, b.min.x, b.max.x);
                    nextPos.z = Mathf.Clamp(nextPos.z, b.min.z, b.max.z);
                }

                Position = nextPos;
            }
        }

        public void SetPosition(Vector3 newPosition)
        {
            Position = newPosition;
            MoveDirection = Vector3.zero;
        }

        public Vector2Int WorldToGrid(Vector3 worldPos, float tileSize)
        {
            if (tileSize <= 0.001f) tileSize = 1f;
            int x = Mathf.RoundToInt(worldPos.x / tileSize);
            int z = Mathf.RoundToInt(worldPos.z / tileSize);
            return new Vector2Int(x, z);
        }

        public Vector3 GridToWorld(Vector2Int gridPos, float tileSize)
        {
            if (tileSize <= 0.001f) tileSize = 1f;
            return new Vector3(gridPos.x * tileSize, Position.y, gridPos.y * tileSize);
        }
    }
}
