using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG25D.Visual
{
    /// <summary>
    /// 정이십면체(D20) 3D 메시 및 20개 면(Face)의 법선 벡터를 프로그래밍 방식으로 생성합니다.
    /// 외부 3D 모델 에셋 없이도 완벽한 3D 물리 D20 주사위를 인게임에서 즉시 구축할 수 있습니다.
    /// </summary>
    public static class D20MeshGenerator
    {
        // D20 면 정보 (1~20 번호 및 로컬 법선 벡터)
        public struct D20FaceInfo
        {
            public int FaceNumber;
            public Vector3 LocalNormal;
            public Vector3 LocalCenter;
        }

        // 20개 면의 로컬 정보 캐시 (1번부터 20번까지 매핑)
        private static D20FaceInfo[] _cachedFaceInfos;

        public static IReadOnlyList<D20FaceInfo> FaceInfos
        {
            get
            {
                if (_cachedFaceInfos == null) GenerateD20Mesh();
                return _cachedFaceInfos;
            }
        }

        /// <summary>
        /// 평면 셰이딩(Flat Shading)이 적용된 정이십면체 메시를 생성합니다.
        /// </summary>
        public static Mesh GenerateD20Mesh(float radius = 0.8f)
        {
            float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f; // 황금비 (약 1.618)

            // 12개 기본 정점 (정규화 후 반지름 스케일 적용)
            Vector3[] baseVerts = new Vector3[]
            {
                new Vector3(-1,  t,  0).normalized * radius,
                new Vector3( 1,  t,  0).normalized * radius,
                new Vector3(-1, -t,  0).normalized * radius,
                new Vector3( 1, -t,  0).normalized * radius,

                new Vector3( 0, -1,  t).normalized * radius,
                new Vector3( 0,  1,  t).normalized * radius,
                new Vector3( 0, -1, -t).normalized * radius,
                new Vector3( 0,  1, -t).normalized * radius,

                new Vector3( t,  0, -1).normalized * radius,
                new Vector3( t,  0,  1).normalized * radius,
                new Vector3(-t,  0, -1).normalized * radius,
                new Vector3(-t,  0,  1).normalized * radius
            };

            // 20개 삼각형 인덱스 구성 (오른손/왼손 와인딩)
            int[][] faceIndices = new int[][]
            {
                new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
                new[] { 1, 5, 9 },  new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                new[] { 3, 9, 4 },  new[] { 3, 4, 2 },  new[] { 3, 2, 6 },  new[] { 3, 6, 8 },  new[] { 3, 8, 9 },
                new[] { 4, 9, 5 },  new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 },  new[] { 9, 8, 1 }
            };

            // 플랫 셰이딩을 위해 면마다 고유 정점 3개씩 분리 생성 (총 60 정점)
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            _cachedFaceInfos = new D20FaceInfo[20];

            // 대향면 합이 21이 되도록 1~20 번호 배정 (표준 주사위 룰: 1 <-> 20, 2 <-> 19 등)
            int[] faceNumbers = new int[]
            {
                20, 1, 19, 2, 18, 3, 17, 4, 16, 5,
                15, 6, 14, 7, 13, 8, 12, 9, 11, 10
            };

            for (int f = 0; f < 20; f++)
            {
                Vector3 v0 = baseVerts[faceIndices[f][0]];
                Vector3 v1 = baseVerts[faceIndices[f][1]];
                Vector3 v2 = baseVerts[faceIndices[f][2]];

                Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                Vector3 faceCenter = (v0 + v1 + v2) / 3f;

                int startIdx = vertices.Count;
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);

                normals.Add(faceNormal);
                normals.Add(faceNormal);
                normals.Add(faceNormal);

                uvs.Add(new Vector2(0.5f, 1f));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));

                triangles.Add(startIdx);
                triangles.Add(startIdx + 1);
                triangles.Add(startIdx + 2);

                _cachedFaceInfos[f] = new D20FaceInfo
                {
                    FaceNumber = faceNumbers[f],
                    LocalNormal = faceNormal,
                    LocalCenter = faceCenter
                };
            }

            Mesh mesh = new Mesh
            {
                name = "Procedural_D20",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                triangles = triangles.ToArray(),
                uv = uvs.ToArray()
            };

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 주사위의 현재 월드 회전값을 기반으로 카메라를 향하는(또는 가장 윗면) 면의 숫자를 판독합니다.
        /// </summary>
        public static int ReadTopFace(Transform diceTransform, Camera cam = null)
        {
            if (_cachedFaceInfos == null) GenerateD20Mesh();

            if (cam == null) cam = Camera.main;
            // 카메라가 있으면 주사위에서 카메라로 향하는 시선 벡터를 기준으로 판독, 없으면 수직 상향(Vector3.up)
            Vector3 viewDir = cam != null
                ? (cam.transform.position - diceTransform.position).normalized
                : Vector3.up;

            int bestFace = 1;
            float highestDot = -2f;

            foreach (var face in _cachedFaceInfos)
            {
                Vector3 worldNormal = diceTransform.TransformDirection(face.LocalNormal);
                float dot = Vector3.Dot(worldNormal, viewDir);
                if (dot > highestDot)
                {
                    highestDot = dot;
                    bestFace = face.FaceNumber;
                }
            }

            return bestFace;
        }

        /// <summary>
        /// 특정 목표 숫자가 카메라(또는 Vector3.up)를 향하도록 만드는 월드 회전값을 계산합니다.
        /// </summary>
        public static Quaternion GetRotationForFaceUp(int targetNumber, Camera cam = null)
        {
            if (_cachedFaceInfos == null) GenerateD20Mesh();

            if (cam == null) cam = Camera.main;
            Vector3 targetDir = cam != null
                ? (cam.transform.position - Vector3.zero).normalized
                : Vector3.up;

            foreach (var face in _cachedFaceInfos)
            {
                if (face.FaceNumber == targetNumber)
                {
                    return Quaternion.FromToRotation(face.LocalNormal, targetDir);
                }
            }

            return Quaternion.identity;
        }

        /// <summary>
        /// 주사위 20개 면의 중심에 3D 숫자 라벨(TextMesh)을 생성하여 물리 시뮬레이션 중 눈금이 또렷이 보이도록 합니다.
        /// </summary>
        public static void CreateFaceLabels(Transform diceTransform, float radius = 0.75f)
        {
            // 기존 라벨이 있다면 제거
            var oldLabels = diceTransform.Find("[FaceLabels]");
            if (oldLabels != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(oldLabels.gameObject);
                else UnityEngine.Object.DestroyImmediate(oldLabels.gameObject);
            }

            GameObject labelsRoot = new GameObject("[FaceLabels]");
            labelsRoot.transform.SetParent(diceTransform, false);
            labelsRoot.transform.localPosition = Vector3.zero;
            labelsRoot.transform.localRotation = Quaternion.identity;

            if (_cachedFaceInfos == null) GenerateD20Mesh(radius);

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            foreach (var face in _cachedFaceInfos)
            {
                GameObject labelObj = new GameObject($"Face_{face.FaceNumber}");
                labelObj.transform.SetParent(labelsRoot.transform, false);

                // 면 중심에서 법선 방향으로 약간 띄움
                labelObj.transform.localPosition = face.LocalCenter * 1.03f;
                labelObj.transform.localRotation = Quaternion.LookRotation(face.LocalNormal, Vector3.up);

                var tm = labelObj.AddComponent<TextMesh>();
                tm.text = face.FaceNumber.ToString();
                tm.fontSize = 42;
                tm.characterSize = 0.055f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontStyle = FontStyle.Bold;
                if (defaultFont != null) tm.font = defaultFont;

                // 1은 붉은색, 20은 황금색, 일반 숫자는 백색
                if (face.FaceNumber == 1) tm.color = new Color(1f, 0.35f, 0.35f);
                else if (face.FaceNumber == 20) tm.color = new Color(1f, 0.9f, 0.2f);
                else tm.color = new Color(0.95f, 0.95f, 0.95f);
            }
        }
    }
}
