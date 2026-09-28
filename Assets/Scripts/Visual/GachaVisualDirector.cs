using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using RPG25D.Core.Gacha;
using RPG25D.Core.Inventory;
using RPG25D.Core.Saju;
using RPG25D.Data;

namespace RPG25D.Visual
{
    /// <summary>
    /// [요구 산출물 1] GachaVisualDirector.cs
    /// 사주팔자 주사위 가챠 3D 비주얼 연출 총괄 디렉터
    /// - [소환 버튼 클릭]: 골드 차감 및 베이스 캐릭터 추첨
    /// - [주사위 투척 연출]: 3D 패판 위로 D10 4개(천간) + D12 4개(지지) 주사위 투척 및 구름 연출
    /// - [사주 각인 연출]: 년주, 월주, 일주, 시주 순으로 한자 텍스트 및 오행 빛기둥 각인
    /// - [캐릭터 등장]: 5성 황금빛/무지개 이펙트, 3~4성 청은빛 이펙트와 함께 2.5D 캐릭터 팝업
    /// - [결과 창 UI 연동]: 확정된 사주팔자 및 중복 비교 UI 노출
    /// </summary>
    public class GachaVisualDirector : MonoBehaviour
    {
        [Header("가챠 비용 설정")]
        [SerializeField] private int _singleRollCost = 100;
        public int SingleRollCost => _singleRollCost;

        [Header("3D 패판 및 연출 앵커")]
        [SerializeField] private Transform _altarBoardTransform;
        [SerializeField] private Vector3 _altarCenter = new Vector3(0f, 0.5f, 3f);
        [SerializeField] private float _altarRadius = 2.5f;

        [Header("카메라 및 조명")]
        [SerializeField] private Camera _stagingCamera;
        [SerializeField] private Light _altarSpotLight;

        [Header("이펙트 색상")]
        [SerializeField] private Color _fiveStarColor = new Color(1f, 0.85f, 0.2f); // 황금빛
        [SerializeField] private Color _fourStarColor = new Color(0.7f, 0.3f, 1f);  // 자줏빛/보라
        [SerializeField] private Color _threeStarColor = new Color(0.3f, 0.7f, 1f); // 청은빛

        // 런타임 시스템 인스턴스
        private GachaRoller _gachaRoller;
        private GachaDuplicateHandler _duplicateHandler;
        private CharacterInventoryManager _inventory;

        // 주사위 및 연출 오브젝트 관리
        private readonly List<GameObject> _activeDiceObjects = new List<GameObject>();
        private readonly List<GameObject> _activePillarInscriptions = new List<GameObject>();
        private GameObject _currentCharacterVisual;

        private bool _isSequenceRunning = false;
        public bool IsSequenceRunning => _isSequenceRunning;

        // 이벤트 정의 (헤드리스 CLI 테스트 및 UI 바인딩용)
        public event Action OnRollStarted;
        public event Action<int> OnDiceThrown; // 던져진 주사위 개수
        public event Action<int, SajuPillar> OnPillarInscribed; // 기둥 인덱스(0:년, 1:월, 2:일, 3:시), 기둥 정보
        public event Action<CharacterInstance, bool> OnCharacterAppeared; // 캐릭터, 5성여부
        public event Action<CharacterInstance, DuplicateGachaResult> OnGachaCompleted;

        private AudioSource _audioSource;
        private AudioClip _diceBounceClip;
        private AudioClip _impactClip;

        private void Awake()
        {
            _inventory = CharacterInventoryManager.Instance;
            _duplicateHandler = GachaDuplicateHandler.Instance;
            _gachaRoller = new GachaRoller();

            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            _diceBounceClip = ProceduralAudioGenerator.CreateDiceBounceClip();
            _impactClip = ProceduralAudioGenerator.CreateHitClip(15);

            EnsureAltarVisuals();
        }

        /// <summary>
        /// 3D 패판(Altar Board) 및 무대 환경이 없는 경우 프로시저럴 자동 구성
        /// </summary>
        private void EnsureAltarVisuals()
        {
            if (_altarBoardTransform == null)
            {
                var altarGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                altarGo.name = "[Procedural_Gacha_Altar]";
                altarGo.transform.position = _altarCenter;
                altarGo.transform.localScale = new Vector3(_altarRadius * 2f, 0.2f, _altarRadius * 2f);
                _altarBoardTransform = altarGo.transform;

                // 패판 머티리얼 (어두운 흑요석 및 청동 룬 문양 느낌)
                var mr = altarGo.GetComponent<MeshRenderer>();
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(litShader)
                {
                    color = new Color(0.12f, 0.14f, 0.18f)
                };
                mr.sharedMaterial = mat;
            }

            if (_altarSpotLight == null)
            {
                var lightGo = new GameObject("[Altar_Glow_Light]");
                lightGo.transform.position = _altarCenter + new Vector3(0f, 4f, -1f);
                _altarSpotLight = lightGo.AddComponent<Light>();
                _altarSpotLight.type = LightType.Spot;
                _altarSpotLight.range = 10f;
                _altarSpotLight.spotAngle = 60f;
                _altarSpotLight.color = new Color(0.85f, 0.95f, 1f);
                _altarSpotLight.intensity = 2.5f;
                lightGo.transform.LookAt(_altarCenter);
            }
        }

        /// <summary>
        /// [세부 개발 명세 1.1] 1회 소환 시작
        /// </summary>
        public void RollSingle()
        {
            if (_isSequenceRunning)
            {
                Debug.LogWarning("[GachaVisualDirector] 이미 소환 연출이 진행 중입니다.");
                return;
            }

            // 재화 차감
            var gmData = GameManagerData.Instance;
            if (gmData != null && gmData.CurrentGold < _singleRollCost)
            {
                Debug.LogWarning($"[GachaVisualDirector] 골드가 부족합니다. (보유: {gmData.CurrentGold}G, 필요: {_singleRollCost}G)");
                // 가챠 씬 단독 테스트 편의를 위해 골드가 0이면 1000골드 자동 충전
                if (gmData.CurrentGold < _singleRollCost)
                {
                    gmData.AddGold(1000);
                }
            }

            if (gmData != null)
            {
                gmData.AddGold(-_singleRollCost);
            }

            StartCoroutine(GachaSequenceRoutine());
        }

        /// <summary>
        /// [세부 개발 명세 1.1 ~ 1.5] 비동기 가챠 타임라인 코루틴
        /// </summary>
        private IEnumerator GachaSequenceRoutine()
        {
            _isSequenceRunning = true;
            CleanupPreviousVisuals();
            OnRollStarted?.Invoke();

            // 1. 순수 C# 모델: 등급 추첨 및 캐릭터/사주 확정
            var characterInstance = _gachaRoller.RollGacha();
            bool isFiveStar = characterInstance.Rarity == 5;

            // 중복 여부 확인
            bool isDuplicate = _duplicateHandler.IsDuplicate(characterInstance.BaseDataID);
            DuplicateGachaResult duplicateResult = null;
            if (isDuplicate)
            {
                duplicateResult = _duplicateHandler.HandleDuplicateRoll(characterInstance);
            }
            else
            {
                _inventory.AddCharacter(characterInstance);
            }

            // 2. [주사위 투척 연출]: D10 4개(천간) + D12 4개(지지) 3D 주사위 생성 및 투척
            yield return StartCoroutine(TossEightDiceRoutine(characterInstance.Saju));

            // 3. [사주 각인 연출]: 년주 -> 월주 -> 일주 -> 시주 순서로 한자 각인
            yield return StartCoroutine(InscribeFourPillarsRoutine(characterInstance.Saju));

            // 4. [캐릭터 등장 연출]: 등급별 빛기둥 이펙트 및 2.5D 캐릭터 팝업
            yield return StartCoroutine(SpawnCharacterVisualRoutine(characterInstance));

            // 5. [결과 완료]: 이벤트 발행하여 GachaResultUI 팝업 오픈
            _isSequenceRunning = false;
            OnGachaCompleted?.Invoke(characterInstance, duplicateResult);
        }

        /// <summary>
        /// 8개의 3D 주사위를 패판 위로 흩뿌려 구르게 하는 연출
        /// </summary>
        private IEnumerator TossEightDiceRoutine(CharacterSaju saju)
        {
            // 천간 4개 (D10 느낌: 다홍/금빛), 지지 4개 (D12 느낌: 청람/은빛)
            Color stemColor = new Color(0.95f, 0.55f, 0.2f);
            Color branchColor = new Color(0.25f, 0.65f, 0.95f);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            for (int i = 0; i < 8; i++)
            {
                bool isStem = i < 4;
                string dieName = isStem ? $"D10_Stem_{(HeavenlyStem)(i + 1)}" : $"D12_Branch_{(EarthlyBranch)(i - 3)}";

                var dieGo = GameObject.CreatePrimitive(isStem ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                dieGo.name = dieName;
                dieGo.transform.localScale = Vector3.one * 0.45f;

                // 스폰 위치: 패판 위 상공에서 약간의 랜덤 분산
                float angle = (i / 8f) * Mathf.PI * 2f;
                Vector3 spawnPos = _altarCenter + new Vector3(Mathf.Cos(angle) * 0.8f, 3.5f + (i * 0.15f), Mathf.Sin(angle) * 0.8f);
                dieGo.transform.position = spawnPos;

                var mr = dieGo.GetComponent<MeshRenderer>();
                var mat = new Material(litShader)
                {
                    color = isStem ? stemColor : branchColor
                };
                mr.sharedMaterial = mat;

                var rb = dieGo.AddComponent<Rigidbody>();
                rb.mass = 0.8f;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;

                // 투척 충격량
                Vector3 randomDir = (UnityEngine.Random.insideUnitSphere + Vector3.down * 1.5f).normalized;
                rb.AddForce(randomDir * 4.5f, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 20f, ForceMode.Impulse);

                _activeDiceObjects.Add(dieGo);
            }

            PlaySound(_diceBounceClip, 0.7f);
            OnDiceThrown?.Invoke(8);

            // 주사위가 굴러가서 안착할 때까지 대기 (약 1.2초)
            float elapsed = 0f;
            while (elapsed < 1.2f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 년주 -> 월주 -> 일주 -> 시주 순으로 패판 위에 사주 각인
        /// </summary>
        private IEnumerator InscribeFourPillarsRoutine(CharacterSaju saju)
        {
            SajuPillar[] pillars = new SajuPillar[]
            {
                saju.YearPillar,
                saju.MonthPillar,
                saju.DayPillar,
                saju.HourPillar
            };

            string[] pillarNames = new string[] { "년주(年柱)", "월주(月柱)", "일주(日柱)", "시주(時柱)" };

            // 패판 위 4개 방위 좌표 (년:북, 월:동, 일:중앙/남, 시:서)
            Vector3[] offsets = new Vector3[]
            {
                new Vector3(-1.2f, 0.15f,  0.4f),
                new Vector3(-0.4f, 0.15f,  0.4f),
                new Vector3( 0.4f, 0.15f,  0.4f),
                new Vector3( 1.2f, 0.15f,  0.4f)
            };

            for (int i = 0; i < 4; i++)
            {
                var pillar = pillars[i];
                Vector3 pos = _altarCenter + offsets[i];

                // 빛기둥 / 각인 판 생성
                var inscriptGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                inscriptGo.name = $"Pillar_{pillarNames[i]}_{pillar.ToKoreanString()}";
                inscriptGo.transform.position = pos;
                inscriptGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                inscriptGo.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

                // 오행 색상 계산
                Color elemColor = GetFiveElementColor(pillar.Stem);

                var mr = inscriptGo.GetComponent<MeshRenderer>();
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                var mat = new Material(unlit) { color = elemColor };
                mr.sharedMaterial = mat;

                _activePillarInscriptions.Add(inscriptGo);

                PlaySound(_impactClip, 0.5f);
                OnPillarInscribed?.Invoke(i, pillar);

                // 조명 플래시
                if (_altarSpotLight != null)
                {
                    _altarSpotLight.color = elemColor;
                }

                yield return new WaitForSeconds(0.28f);
            }
        }

        /// <summary>
        /// 캐릭터 2.5D 일러스트 팝업 및 등급별(3~5성) 오라 연출
        /// </summary>
        private IEnumerator SpawnCharacterVisualRoutine(CharacterInstance character)
        {
            Color gradeColor = character.Rarity switch
            {
                5 => _fiveStarColor,
                4 => _fourStarColor,
                _ => _threeStarColor
            };

            if (_altarSpotLight != null)
            {
                _altarSpotLight.color = gradeColor;
                _altarSpotLight.intensity = character.Rarity == 5 ? 4.5f : 3.0f;
            }

            // 캐릭터 비주얼 오브젝트 (2.5D 빌보드 Quad)
            _currentCharacterVisual = new GameObject($"CharacterVisual_{character.CharacterName}");
            Vector3 startPos = _altarCenter + new Vector3(0f, 0.2f, 0f);
            Vector3 targetPos = _altarCenter + new Vector3(0f, 2.0f, 0f);
            _currentCharacterVisual.transform.position = startPos;

            var visualQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visualQuad.name = "CharacterQuad";
            visualQuad.transform.parent = _currentCharacterVisual.transform;
            visualQuad.transform.localPosition = Vector3.zero;
            visualQuad.transform.localScale = new Vector3(1.8f, 2.8f, 1f);

            var mr = visualQuad.GetComponent<MeshRenderer>();
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(litShader) { color = gradeColor };
            mr.sharedMaterial = mat;

            // 후광 원기둥 (Aura Pillar)
            var auraGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            auraGo.name = "AuraPillar";
            auraGo.transform.parent = _currentCharacterVisual.transform;
            auraGo.transform.localPosition = Vector3.zero;
            auraGo.transform.localScale = new Vector3(2.5f, 3.0f, 2.5f);
            var auraMr = auraGo.GetComponent<MeshRenderer>();
            var auraCol = auraGo.GetComponent<Collider>();
            if (auraCol != null) Destroy(auraCol);

            var auraMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"))
            {
                color = new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0.35f)
            };
            auraMr.sharedMaterial = auraMat;

            PlaySound(_impactClip, 0.9f);
            OnCharacterAppeared?.Invoke(character, character.Rarity == 5);

            // 부드러운 상승 및 스케일 팝업 애니메이션
            float dur = 0.65f;
            float el = 0f;
            while (el < dur)
            {
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float easeOut = 1f - Mathf.Pow(1f - t, 3);
                _currentCharacterVisual.transform.position = Vector3.Lerp(startPos, targetPos, easeOut);
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>
        /// 천간에 따른 오행(목, 화, 토, 금, 수) 색상 반환
        /// </summary>
        public static Color GetFiveElementColor(HeavenlyStem stem)
        {
            return stem switch
            {
                HeavenlyStem.Gap or HeavenlyStem.Eul => new Color(0.2f, 0.85f, 0.4f),    // 목(木): 청/녹
                HeavenlyStem.Byeong or HeavenlyStem.Jeong => new Color(0.95f, 0.3f, 0.2f),// 화(火): 적
                HeavenlyStem.Mu or HeavenlyStem.Gi => new Color(0.95f, 0.85f, 0.25f),     // 토(土): 황
                HeavenlyStem.Gyeong or HeavenlyStem.Sin => new Color(0.9f, 0.95f, 1f),    // 금(金): 백/은
                HeavenlyStem.Im or HeavenlyStem.Gye => new Color(0.25f, 0.45f, 0.95f),   // 수(水): 흑/남
                _ => Color.white
            };
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (_audioSource != null && clip != null && !Application.isBatchMode)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }

        public void CleanupPreviousVisuals()
        {
            foreach (var die in _activeDiceObjects)
            {
                if (die != null) Destroy(die);
            }
            _activeDiceObjects.Clear();

            foreach (var ins in _activePillarInscriptions)
            {
                if (ins != null) Destroy(ins);
            }
            _activePillarInscriptions.Clear();

            if (_currentCharacterVisual != null)
            {
                Destroy(_currentCharacterVisual);
                _currentCharacterVisual = null;
            }
        }

        private void OnDestroy()
        {
            CleanupPreviousVisuals();
        }
    }
}
