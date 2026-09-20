using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG25D.Core.Dice;

namespace RPG25D.Visual
{
    /// <summary>
    /// [요구 산출물 2] DicePhysicsVisualizer.cs
    /// 실제 3D 물리 엔진(Rigidbody, Convex MeshCollider)을 사용하여 정이십면체(D20) 주사위를 던지고,
    /// 바닥 충돌 타격음(SFX), 굴림 감속 및 정지 감지, 상향 면 판독 및 산산조각(Shatter) 연출을 수행합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class DicePhysicsVisualizer : MonoBehaviour
    {
        [Header("물리 투척 파라미터")]
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 5.5f, -2.5f);
        [SerializeField] private float _throwForce = 6.0f;
        [SerializeField] private float _torqueForce = 25.0f;
        [SerializeField] private float _stopVelocityThreshold = 0.05f;
        [SerializeField] private float _maxRollDuration = 2.8f;

        [Header("오디오")]
        [SerializeField] private AudioSource _audioSource;

        private Rigidbody _rb;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MeshCollider _collider;

        private bool _isRolling = false;
        private int? _targetOutcome = null; // 특정 눈금 연출 강제 시 사용 (프리뷰 및 테스트 지원)
        private Action<int> _onSettleCallback;

        private AudioClip _bounceClip;

        public bool IsRolling => _isRolling;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();

            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2.5D 또렷한 2D 오디오 출력

            _bounceClip = ProceduralAudioGenerator.CreateDiceBounceClip();

            SetupMeshAndCollider();
        }

        private void SetupMeshAndCollider()
        {
            Mesh d20Mesh = D20MeshGenerator.GenerateD20Mesh(0.75f);
            _mf.sharedMesh = d20Mesh;

            _collider = GetComponent<MeshCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();

            _collider.sharedMesh = d20Mesh;
            _collider.convex = true;

            _rb.mass = 2.0f;
            _rb.linearDamping = 0.4f;
            _rb.angularDamping = 0.5f;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 바닥 관통 방지용 피직스 머티리얼 (탄성 및 마찰력)
            PhysicsMaterial mat = new PhysicsMaterial("D20_PhysicsMat")
            {
                bounciness = 0.45f,
                dynamicFriction = 0.6f,
                staticFriction = 0.7f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            _collider.material = mat;

            // 20개 면에 3D 숫자 라벨 부착
            D20MeshGenerator.CreateFaceLabels(transform, 0.75f);
        }

        /// <summary>
        /// 3D 상공에서 물리 주사위를 투척합니다.
        /// targetNumber가 지정되면 굴림 후 자연스럽게 해당 숫자가 카메라를 향하도록 유도합니다.
        /// </summary>
        public void ThrowDice(int? targetNumber, Action<int> onSettled)
        {
            gameObject.SetActive(true);
            _mr.enabled = true;
            SetFaceLabelsActive(true);

            _onSettleCallback = onSettled;
            _targetOutcome = targetNumber;
            _isRolling = true;

            // 1. 카메라 중앙 상공 위치 설정
            Camera cam = Camera.main;
            Vector3 centerPos = Vector3.zero;
            if (cam != null)
            {
                centerPos = new Vector3(cam.transform.position.x, 0f, 0f) + _spawnOffset;
            }
            else
            {
                centerPos = _spawnOffset;
            }

            transform.position = centerPos;
            transform.rotation = UnityEngine.Random.rotation;

            // 2. 물리 파라미터 리셋 및 충격량(Impulse) 적용
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            Vector3 throwDir = new Vector3(
                UnityEngine.Random.Range(-0.3f, 0.3f),
                -1.0f,
                UnityEngine.Random.Range(0.4f, 0.8f)
            ).normalized;

            _rb.AddForce(throwDir * _throwForce, ForceMode.Impulse);

            Vector3 torque = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            ).normalized * _torqueForce;

            _rb.AddTorque(torque, ForceMode.Impulse);

            StopAllCoroutines();
            StartCoroutine(WaitForSettleRoutine());
        }

        private IEnumerator WaitForSettleRoutine()
        {
            float timer = 0f;
            float stillTimer = 0f;
            Camera cam = Camera.main;

            // 최소 굴림 시간 보장 (초반 투척 단계 무시)
            yield return new WaitForSeconds(0.4f);

            while (timer < _maxRollDuration)
            {
                timer += Time.deltaTime;

                bool isNearlyStill = _rb.linearVelocity.sqrMagnitude < _stopVelocityThreshold
                                  && _rb.angularVelocity.sqrMagnitude < _stopVelocityThreshold;

                if (isNearlyStill)
                {
                    stillTimer += Time.deltaTime;
                    if (stillTimer > 0.25f) break; // 0.25초간 안정 정지 시 판정
                }
                else
                {
                    stillTimer = 0f;
                }

                yield return null;
            }

            // 물리 시뮬레이션 고정
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;

            // 특정 타깃 숫자가 지정되어 있다면 자연스럽게 해당 면이 카메라를 향하도록 회전 보정
            if (_targetOutcome.HasValue)
            {
                Quaternion targetRot = D20MeshGenerator.GetRotationForFaceUp(_targetOutcome.Value, cam);
                float orientTimer = 0f;
                Quaternion startRot = transform.rotation;
                while (orientTimer < 0.25f)
                {
                    orientTimer += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(startRot, targetRot, orientTimer / 0.25f);
                    yield return null;
                }
                transform.rotation = targetRot;
            }

            // 최종 카메라 방향 상향 면 숫자 판독
            int finalValue = D20MeshGenerator.ReadTopFace(transform, cam);
            if (_targetOutcome.HasValue) finalValue = _targetOutcome.Value;

            _isRolling = false;
            _onSettleCallback?.Invoke(finalValue);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isRolling) return;

            // 충돌 상대 속도에 따라 바닥 튕김음 발생
            float impact = collision.relativeVelocity.magnitude;
            if (impact > 0.8f)
            {
                float volume = Mathf.Clamp01(impact / 7.0f);
                if (_audioSource != null && _bounceClip != null)
                {
                    _audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.15f);
                    _audioSource.PlayOneShot(_bounceClip, volume * 0.85f);
                }
            }
        }

        /// <summary>
        /// 대실패(1) 시 주사위가 산산조각 나며 붉은색 연기 이펙트와 파편 폭발 발생
        /// </summary>
        public void PlayShatterEffect()
        {
            StartCoroutine(ShatterRoutine());
        }

        private IEnumerator ShatterRoutine()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");

            // 1. 붉은색 파편 큐브들 생성 및 방사형 폭발
            int shardCount = 12;
            Material shardMat = new Material(unlitShader) { color = new Color(0.95f, 0.15f, 0.15f) };

            GameObject shardParent = new GameObject("[CritFail_Shards]");
            shardParent.transform.position = transform.position;

            for (int i = 0; i < shardCount; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.transform.parent = shardParent.transform;
                shard.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * 0.25f;
                shard.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.12f, 0.24f);
                shard.GetComponent<MeshRenderer>().sharedMaterial = shardMat;

                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                Vector3 burstDir = (shard.transform.position - transform.position + Vector3.up * 0.6f).normalized;
                rb.AddForce(burstDir * UnityEngine.Random.Range(4f, 8f), ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 25f, ForceMode.Impulse);
            }

            // 2. 붉은색 연기 구름 (Red Smoke Burst) 생성
            int smokeCount = 8;
            Material smokeMat = new Material(unlitShader) { color = new Color(0.85f, 0.1f, 0.1f, 0.6f) };
            List<Transform> smokePuffs = new List<Transform>();

            for (int i = 0; i < smokeCount; i++)
            {
                var smokeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                smokeObj.transform.parent = shardParent.transform;
                smokeObj.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * 0.3f;
                smokeObj.transform.localScale = Vector3.one * 0.3f;
                Destroy(smokeObj.GetComponent<Collider>());
                smokeObj.GetComponent<MeshRenderer>().sharedMaterial = smokeMat;
                smokePuffs.Add(smokeObj.transform);
            }

            // 본체 및 라벨 숨기기
            _mr.enabled = false;
            SetFaceLabelsActive(false);

            // 0.8초간 파편 날아감 + 연기 구름 팽창 및 페이드아웃
            float elapsed = 0f;
            float totalDur = 0.85f;

            while (elapsed < totalDur)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / totalDur;

                // 연기 구름 팽창 및 상승
                for (int i = 0; i < smokePuffs.Count; i++)
                {
                    if (smokePuffs[i] != null)
                    {
                        smokePuffs[i].position += Vector3.up * Time.deltaTime * 0.8f;
                        smokePuffs[i].localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, progress);
                    }
                }

                // 파편 페이드 스케일
                if (progress > 0.5f)
                {
                    float fadeScale = 1f - ((progress - 0.5f) / 0.5f);
                    shardParent.transform.localScale = Vector3.one * fadeScale;
                }

                yield return null;
            }

            Destroy(shardParent);
            gameObject.SetActive(false);
            _mr.enabled = true;
            SetFaceLabelsActive(true);
        }

        /// <summary>
        /// 일반 적중(2~19) 시 주사위 상공에 홀로그램 눈금 숫자 및 눈금 비례 파티클 폭발 연출
        /// </summary>
        public void PlayHologramEffect(int d20Value)
        {
            StartCoroutine(HologramRoutine(d20Value));
        }

        private IEnumerator HologramRoutine(int d20Value)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");

            GameObject fxRoot = new GameObject($"[Hologram_D20_{d20Value}]");
            fxRoot.transform.position = transform.position;

            // 1. 홀로그램 링
            var ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObj.name = "HoloRing";
            ringObj.transform.parent = fxRoot.transform;
            Destroy(ringObj.GetComponent<Collider>());
            ringObj.transform.localPosition = Vector3.up * 0.35f;
            ringObj.transform.localScale = new Vector3(0.6f, 0.03f, 0.6f);

            Material ringMat = new Material(unlitShader);
            Color holoColor = Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(1f, 0.4f, 0.1f), (d20Value - 2) / 17f);
            ringMat.color = holoColor;
            ringObj.GetComponent<MeshRenderer>().sharedMaterial = ringMat;

            // 2. 홀로그램 3D 숫자 텍스트 (공중에 크고 선명하게 회전)
            var textObj = new GameObject("HoloNumberText");
            textObj.transform.parent = fxRoot.transform;
            textObj.transform.localPosition = Vector3.up * 0.9f;

            var tm = textObj.AddComponent<TextMesh>();
            tm.text = d20Value.ToString();
            tm.fontSize = 54;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = holoColor;

            Camera cam = Camera.main;
            if (cam != null)
            {
                textObj.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
            }

            // 3. 눈금 크기(2~19)에 비례한 파티클 스파크들
            int particleCount = Mathf.RoundToInt(Mathf.Lerp(6, 24, (d20Value - 2) / 17f));
            List<Transform> sparkList = new List<Transform>();
            Material sparkMat = new Material(unlitShader) { color = holoColor };

            for (int i = 0; i < particleCount; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.transform.parent = fxRoot.transform;
                spark.transform.position = transform.position + Vector3.up * 0.4f;
                spark.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.08f, 0.16f);
                Destroy(spark.GetComponent<Collider>());
                spark.GetComponent<MeshRenderer>().sharedMaterial = sparkMat;

                var rb = spark.AddComponent<Rigidbody>();
                rb.useGravity = false;
                Vector3 dir = (UnityEngine.Random.insideUnitSphere + Vector3.up * 0.6f).normalized;
                rb.linearVelocity = dir * UnityEngine.Random.Range(1.8f, 4.5f);
                sparkList.Add(spark.transform);
            }

            float dur = 0.75f;
            float el = 0f;
            Vector3 startRingScale = ringObj.transform.localScale;

            while (el < dur)
            {
                el += Time.deltaTime;
                float progress = el / dur;

                // 링 팽창 및 회전
                ringObj.transform.localPosition += Vector3.up * Time.deltaTime * 1.6f;
                ringObj.transform.localScale = Vector3.Lerp(startRingScale, new Vector3(2.2f, 0.01f, 2.2f), progress);
                ringObj.transform.Rotate(0f, 200f * Time.deltaTime, 0f);

                // 숫자 텍스트 상승 및 살짝 펄스
                textObj.transform.localPosition += Vector3.up * Time.deltaTime * 1.2f;
                float scalePulse = 1f + Mathf.Sin(progress * Mathf.PI) * 0.35f;
                textObj.transform.localScale = Vector3.one * scalePulse;

                if (progress > 0.65f)
                {
                    float fade = 1f - (progress - 0.65f) / 0.35f;
                    tm.color = new Color(holoColor.r, holoColor.g, holoColor.b, fade);
                }

                yield return null;
            }

            Destroy(fxRoot);
        }

        /// <summary>
        /// 절대 성공/즉사(20) 시 주사위 상공으로 솟구치는 황금빛 수직 광선(Pillar of Light) 및 황금 파티클 폭풍 연출
        /// </summary>
        public void PlayGoldenPillarEffect(float duration)
        {
            StartCoroutine(GoldenPillarRoutine(duration));
        }

        private IEnumerator GoldenPillarRoutine(float duration)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");

            GameObject root = new GameObject("[InstantKill_PillarRoot]");
            root.transform.position = transform.position;

            // 1. 메인 황금빛 기둥
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "GoldenPillar";
            pillar.transform.parent = root.transform;
            Destroy(pillar.GetComponent<Collider>());

            pillar.transform.localPosition = new Vector3(0f, 8f, 0f);
            pillar.transform.localScale = new Vector3(1.1f, 16f, 1.1f);

            Material pillarMat = new Material(litShader);
            Color golden = new Color(1.0f, 0.88f, 0.2f);
            pillarMat.color = golden;
            pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;

            // 2. 외곽 보조 황금 기둥 (투명도 및 팽창 효과)
            var outerPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outerPillar.name = "OuterGlow";
            outerPillar.transform.parent = root.transform;
            Destroy(outerPillar.GetComponent<Collider>());
            outerPillar.transform.localPosition = new Vector3(0f, 8f, 0f);
            outerPillar.transform.localScale = new Vector3(1.8f, 16f, 1.8f);

            Material outerMat = new Material(litShader);
            outerMat.color = new Color(1.0f, 0.6f, 0.1f, 0.5f);
            outerPillar.GetComponent<MeshRenderer>().sharedMaterial = outerMat;

            // 3. 소용돌이치는 황금 스파크 큐브들
            int sparkCount = 20;
            List<Transform> sparks = new List<Transform>();
            for (int i = 0; i < sparkCount; i++)
            {
                var sp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sp.transform.parent = root.transform;
                sp.transform.localPosition = new Vector3(
                    UnityEngine.Random.Range(-1.5f, 1.5f),
                    UnityEngine.Random.Range(0.5f, 8f),
                    UnityEngine.Random.Range(-1.5f, 1.5f)
                );
                sp.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.12f, 0.28f);
                Destroy(sp.GetComponent<Collider>());
                sp.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
                sparks.Add(sp.transform);
            }

            float el = 0f;
            while (el < duration)
            {
                el += Time.unscaledDeltaTime;
                float pulse = 1.1f + Mathf.Sin(el * 22f) * 0.35f;
                pillar.transform.localScale = new Vector3(pulse, 16f, pulse);
                outerPillar.transform.localScale = new Vector3(pulse * 1.5f, 16f, pulse * 1.5f);

                pillar.transform.Rotate(0f, 150f * Time.unscaledDeltaTime, 0f);
                outerPillar.transform.Rotate(0f, -100f * Time.unscaledDeltaTime, 0f);

                // 스파크 소용돌이 상승
                foreach (var sp in sparks)
                {
                    if (sp != null)
                    {
                        sp.localPosition += Vector3.up * Time.unscaledDeltaTime * 4.5f;
                        sp.Rotate(0f, 250f * Time.unscaledDeltaTime, 0f);
                    }
                }

                yield return null;
            }

            // 서서히 축소 소멸
            float shrinkDur = 0.35f;
            float shrinkEl = 0f;
            Vector3 sScale = pillar.transform.localScale;
            while (shrinkEl < shrinkDur)
            {
                shrinkEl += Time.unscaledDeltaTime;
                float t = shrinkEl / shrinkDur;
                pillar.transform.localScale = Vector3.Lerp(sScale, new Vector3(0f, 16f, 0f), t);
                outerPillar.transform.localScale = Vector3.Lerp(sScale * 1.5f, new Vector3(0f, 16f, 0f), t);
                yield return null;
            }

            Destroy(root);
        }

        private void SetFaceLabelsActive(bool active)
        {
            var labels = transform.Find("[FaceLabels]");
            if (labels != null)
            {
                labels.gameObject.SetActive(active);
            }
        }
    }
}
