using System;
using System.Collections.Generic;
using UnityEngine;
using RPG25D.Data;

namespace RPG25D.Visual
{
    /// <summary>
    /// [요구 산출물 1] CharacterWingVisual.cs
    /// 2.5D 캐릭터 등 뒤 날개 스프라이트 렌더러 컴포넌트
    /// - 오브젝트 계층 구조:
    ///   - WingAnchor (Transform)
    ///     - SubWingLeftRenderer (부 날개 좌)
    ///     - SubWingRightRenderer (부 날개 우)
    ///     - MainWingLeftRenderer (주 날개 좌)
    ///     - MainWingRightRenderer (주 날개 우)
    /// - 소팅 오더:
    ///   - 부 날개: -2, 주 날개: -1, 캐릭터 본체: 0 (캐릭터 등 뒤에 정렬)
    /// - 동적 갱신:
    ///   - UpdateVisual(CharacterInstance character)로 4개 슬롯 장착 상태 실시간 반영
    /// - 아이들(Idle) 시 사인파(Sine Wave) 기반 날개 펄럭임 회전/스케일 연출
    /// - 3D 빌보드 동기화 지원
    /// </summary>
    [ExecuteAlways]
    public class CharacterWingVisual : MonoBehaviour
    {
        [Header("날개 앵커 및 4부위 렌더러")]
        [SerializeField] private Transform _wingAnchor;
        [SerializeField] private SpriteRenderer _subWingLeftRenderer;
        [SerializeField] private SpriteRenderer _subWingRightRenderer;
        [SerializeField] private SpriteRenderer _mainWingLeftRenderer;
        [SerializeField] private SpriteRenderer _mainWingRightRenderer;

        [Header("소팅 레이어 및 오더")]
        [SerializeField] private string _sortingLayerName = "Default";
        [SerializeField] private int _subWingSortingOrder = -2;
        [SerializeField] private int _mainWingSortingOrder = -1;

        [Header("날개 기본 오프셋 좌표 (앵커 기준)")]
        [SerializeField] private Vector3 _mainWingLeftOffset = new Vector3(-0.65f, 0.35f, 0.02f);
        [SerializeField] private Vector3 _mainWingRightOffset = new Vector3(0.65f, 0.35f, 0.02f);
        [SerializeField] private Vector3 _subWingLeftOffset = new Vector3(-0.40f, 0.05f, 0.04f);
        [SerializeField] private Vector3 _subWingRightOffset = new Vector3(0.40f, 0.05f, 0.04f);

        [Header("날개 기본 스케일")]
        [SerializeField] private Vector3 _mainWingScale = new Vector3(1.0f, 1.0f, 1.0f);
        [SerializeField] private Vector3 _subWingScale = new Vector3(0.75f, 0.75f, 1.0f);

        [Header("아이들 펄럭임(Idle Flapping) 설정")]
        [SerializeField] private bool _enableIdleFlapping = true;
        [SerializeField] private float _flapSpeed = 3.2f;
        [SerializeField] private float _mainWingFlapAngle = 14f;
        [SerializeField] private float _subWingFlapAngle = 9f;
        [SerializeField] private float _scaleFlapIntensity = 0.04f;

        [Header("빌보드 동기화")]
        [SerializeField] private BillboardActor25D _billboardActor;

        // 런타임 캐시
        private static readonly Dictionary<string, Sprite> _proceduralSpriteCache = new Dictionary<string, Sprite>();
        private float _animationTimer = 0f;

        public Transform WingAnchor => _wingAnchor;
        public SpriteRenderer SubWingLeftRenderer => _subWingLeftRenderer;
        public SpriteRenderer SubWingRightRenderer => _subWingRightRenderer;
        public SpriteRenderer MainWingLeftRenderer => _mainWingLeftRenderer;
        public SpriteRenderer MainWingRightRenderer => _mainWingRightRenderer;

        private void Awake()
        {
            EnsureWingHierarchy();
            if (_billboardActor == null)
            {
                _billboardActor = GetComponentInParent<BillboardActor25D>() ?? GetComponent<BillboardActor25D>();
            }
        }

        private void Start()
        {
            EnsureWingHierarchy();
            ApplySortingSettings();
        }

        private void Update()
        {
            if (!_enableIdleFlapping) return;

            // 에디터 및 런타임 공통 시간 진행
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            _animationTimer += dt * _flapSpeed;

            AnimateFlapping(_animationTimer);
        }

        /// <summary>
        /// 날개 앵커 및 4개 자식 스프라이트 렌더러 계층 구조가 없으면 자동 생성/보장합니다.
        /// </summary>
        public void EnsureWingHierarchy()
        {
            if (_wingAnchor == null)
            {
                var existingAnchor = transform.Find("WingAnchor");
                if (existingAnchor != null)
                {
                    _wingAnchor = existingAnchor;
                }
                else
                {
                    var anchorGo = new GameObject("WingAnchor");
                    anchorGo.transform.SetParent(transform, false);
                    anchorGo.transform.localPosition = new Vector3(0f, 0.4f, 0.05f); // 등 뒤 약간 후면
                    anchorGo.transform.localRotation = Quaternion.identity;
                    anchorGo.transform.localScale = Vector3.one;
                    _wingAnchor = anchorGo.transform;
                }
            }

            // 1. SubWingLeft
            if (_subWingLeftRenderer == null)
            {
                _subWingLeftRenderer = GetOrCreateRenderer("SubWingLeftRenderer", _subWingLeftOffset, _subWingScale, isLeft: true);
            }

            // 2. SubWingRight
            if (_subWingRightRenderer == null)
            {
                _subWingRightRenderer = GetOrCreateRenderer("SubWingRightRenderer", _subWingRightOffset, _subWingScale, isLeft: false);
            }

            // 3. MainWingLeft
            if (_mainWingLeftRenderer == null)
            {
                _mainWingLeftRenderer = GetOrCreateRenderer("MainWingLeftRenderer", _mainWingLeftOffset, _mainWingScale, isLeft: true);
            }

            // 4. MainWingRight
            if (_mainWingRightRenderer == null)
            {
                _mainWingRightRenderer = GetOrCreateRenderer("MainWingRightRenderer", _mainWingRightOffset, _mainWingScale, isLeft: false);
            }

            ApplySortingSettings();
        }

        private SpriteRenderer GetOrCreateRenderer(string name, Vector3 localPos, Vector3 localScale, bool isLeft)
        {
            Transform t = _wingAnchor.Find(name);
            GameObject go;
            if (t != null)
            {
                go = t.gameObject;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(_wingAnchor, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = localScale;
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.AddComponent<SpriteRenderer>();
            }

            // 좌측 날개는 X축 반전 플립
            sr.flipX = isLeft;
            return sr;
        }

        /// <summary>
        /// 소팅 레이어 및 오더(Order in Layer) 적용
        /// 부 날개: -2, 주 날개: -1, 캐릭터 본체: 0 (캐릭터 등 뒤에 깔끔하게 정렬)
        /// </summary>
        public void ApplySortingSettings()
        {
            if (_subWingLeftRenderer != null)
            {
                _subWingLeftRenderer.sortingLayerName = _sortingLayerName;
                _subWingLeftRenderer.sortingOrder = _subWingSortingOrder;
            }

            if (_subWingRightRenderer != null)
            {
                _subWingRightRenderer.sortingLayerName = _sortingLayerName;
                _subWingRightRenderer.sortingOrder = _subWingSortingOrder;
            }

            if (_mainWingLeftRenderer != null)
            {
                _mainWingLeftRenderer.sortingLayerName = _sortingLayerName;
                _mainWingLeftRenderer.sortingOrder = _mainWingSortingOrder;
            }

            if (_mainWingRightRenderer != null)
            {
                _mainWingRightRenderer.sortingLayerName = _sortingLayerName;
                _mainWingRightRenderer.sortingOrder = _mainWingSortingOrder;
            }
        }

        /// <summary>
        /// [세부 개발 명세 1] 동적 갱신 로직:
        /// 4개 슬롯(MainLeft, MainRight, SubLeft, SubRight)의 장착 여부를 검사하여
        /// 장착된 날개의 스프라이트를 할당하고 활성화, 미장착 시 해당 스프라이트를 비활성화합니다.
        /// </summary>
        public void UpdateVisual(CharacterInstance character)
        {
            EnsureWingHierarchy();

            if (character == null)
            {
                SetRendererActive(_subWingLeftRenderer, false);
                SetRendererActive(_subWingRightRenderer, false);
                SetRendererActive(_mainWingLeftRenderer, false);
                SetRendererActive(_mainWingRightRenderer, false);
                return;
            }

            // 1. 주 날개 (좌)
            UpdateSlotRenderer(WingSlotType.MainLeft, _mainWingLeftRenderer, character);

            // 2. 주 날개 (우)
            UpdateSlotRenderer(WingSlotType.MainRight, _mainWingRightRenderer, character);

            // 3. 부 날개 (좌)
            UpdateSlotRenderer(WingSlotType.SubLeft, _subWingLeftRenderer, character);

            // 4. 부 날개 (우)
            UpdateSlotRenderer(WingSlotType.SubRight, _subWingRightRenderer, character);
        }

        private void UpdateSlotRenderer(WingSlotType slot, SpriteRenderer renderer, CharacterInstance character)
        {
            if (renderer == null) return;

            var wing = character.GetEquippedWing(slot);
            if (wing != null)
            {
                SetRendererActive(renderer, true);

                // 스프라이트 획득: 데이터에 지정된 스프라이트 또는 절차적 날개 그래픽 생성
                Sprite sprite = GetWingSprite(wing);
                renderer.sprite = sprite;

                // 희귀도/세트별 비주얼 틴트 컬러 적용
                renderer.color = GetRarityTintColor(wing.Rarity, wing.SetID);
            }
            else
            {
                SetRendererActive(renderer, false);
            }
        }

        private void SetRendererActive(SpriteRenderer sr, bool active)
        {
            if (sr != null && sr.gameObject.activeSelf != active)
            {
                sr.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// 아이들 펄럭임 연출 (사인파 기반 회전 및 스케일 모션)
        /// 좌/우 날개는 대칭(Symmetrical)으로 회전하며, 주 날개와 부 날개는 위상차를 주어 유기적인 날갯짓을 형성합니다.
        /// </summary>
        private void AnimateFlapping(float time)
        {
            float mainSin = Mathf.Sin(time);
            float subSin = Mathf.Sin(time - 0.5f); // 0.5 라디안 위상차

            // 주 날개 좌/우 회전 (좌: +, 우: -)
            if (_mainWingLeftRenderer != null && _mainWingLeftRenderer.gameObject.activeSelf)
            {
                float angle = mainSin * _mainWingFlapAngle;
                _mainWingLeftRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                float scaleMod = 1f + mainSin * _scaleFlapIntensity;
                _mainWingLeftRenderer.transform.localScale = new Vector3(_mainWingScale.x * scaleMod, _mainWingScale.y * scaleMod, 1f);
            }

            if (_mainWingRightRenderer != null && _mainWingRightRenderer.gameObject.activeSelf)
            {
                float angle = -mainSin * _mainWingFlapAngle;
                _mainWingRightRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                float scaleMod = 1f + mainSin * _scaleFlapIntensity;
                _mainWingRightRenderer.transform.localScale = new Vector3(_mainWingScale.x * scaleMod, _mainWingScale.y * scaleMod, 1f);
            }

            // 부 날개 좌/우 회전
            if (_subWingLeftRenderer != null && _subWingLeftRenderer.gameObject.activeSelf)
            {
                float angle = subSin * _subWingFlapAngle;
                _subWingLeftRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                float scaleMod = 1f + subSin * _scaleFlapIntensity;
                _subWingLeftRenderer.transform.localScale = new Vector3(_subWingScale.x * scaleMod, _subWingScale.y * scaleMod, 1f);
            }

            if (_subWingRightRenderer != null && _subWingRightRenderer.gameObject.activeSelf)
            {
                float angle = -subSin * _subWingFlapAngle;
                _subWingRightRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                float scaleMod = 1f + subSin * _scaleFlapIntensity;
                _subWingRightRenderer.transform.localScale = new Vector3(_subWingScale.x * scaleMod, _subWingScale.y * scaleMod, 1f);
            }
        }

        /// <summary>
        /// 날개 아이템 인스턴스로부터 렌더링에 사용할 스프라이트를 반환합니다.
        /// 에셋이 없더라도 즉시 시각화할 수 있도록 절차적 날개 스프라이트를 자동 생성합니다.
        /// </summary>
        public static Sprite GetWingSprite(WingItemInstance wing)
        {
            if (wing == null) return null;

            string key = $"ProceduralWing_{wing.SlotType}_{wing.Rarity}";
            if (_proceduralSpriteCache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var generated = CreateProceduralWingSprite(wing.SlotType, wing.Rarity);
            _proceduralSpriteCache[key] = generated;
            return generated;
        }

        /// <summary>
        /// 순수 런타임/에디터 상에서 깃털 날개 형태의 텍스처와 스프라이트를 동적 생성합니다.
        /// </summary>
        public static Sprite CreateProceduralWingSprite(WingSlotType slot, int rarity)
        {
            int width = 64;
            int height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Tex_Wing_{slot}_{rarity}"
            };

            Color[] pixels = new Color[width * height];
            bool isSub = (slot == WingSlotType.SubLeft || slot == WingSlotType.SubRight);

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / height; // 0 (하단) ~ 1 (상단)
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width; // 0 (안쪽 관절) ~ 1 (바깥 날개 끝)

                    // 날개 깃털 곡선 아웃라인 계산
                    float wingShape = Mathf.Sin(v * Mathf.PI * 0.9f) * (isSub ? 0.75f : 0.95f);
                    float edgeDist = Mathf.Abs(u - (0.15f + wingShape * 0.7f));

                    if (u <= 0.15f + wingShape * 0.8f && v >= 0.05f && v <= 0.95f)
                    {
                        // 날개 본체 그라데이션
                        float alpha = Mathf.Clamp01(1f - (edgeDist * 2.2f));
                        float brightness = 0.7f + 0.3f * Mathf.Sin(v * Mathf.PI);

                        // 등급별 깃털 질감 톤
                        Color baseColor = rarity switch
                        {
                            5 => new Color(1.0f * brightness, 0.92f * brightness, 0.55f * brightness, alpha),
                            4 => new Color(0.9f * brightness, 0.65f * brightness, 1.0f * brightness, alpha),
                            _ => new Color(0.85f * brightness, 0.92f * brightness, 1.0f * brightness, alpha)
                        };

                        // 깃털 겹침 효과를 위한 약간의 대각선 무늬
                        float featherStripe = Mathf.Sin((x + y * 1.5f) * 0.4f) * 0.08f;
                        baseColor.r = Mathf.Clamp01(baseColor.r + featherStripe);
                        baseColor.g = Mathf.Clamp01(baseColor.g + featherStripe);
                        baseColor.b = Mathf.Clamp01(baseColor.b + featherStripe);

                        pixels[y * width + x] = baseColor;
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // 피벗(Pivot)을 관절 부위(x: 0.15, y: 0.35)로 지정하여 자연스러운 날갯짓 보장
            Vector2 pivot = new Vector2(0.15f, 0.35f);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 64f);
        }

        public static Color GetRarityTintColor(int rarity, string setId)
        {
            if (!string.IsNullOrEmpty(setId))
            {
                if (setId.IndexOf("BLUE_DRAGON", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new Color(0.35f, 0.75f, 1.0f, 1.0f); // 청룡 푸른빛
                if (setId.IndexOf("SOLAR_PHOENIX", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new Color(1.0f, 0.55f, 0.2f, 1.0f);  // 태양 불꽃 주황
                if (setId.IndexOf("WHITE_TIGER", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new Color(0.85f, 0.92f, 1.0f, 1.0f); // 백호 서리 은백색
                if (setId.IndexOf("BLACK_TORTOISE", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new Color(0.3f, 0.8f, 0.55f, 1.0f);  // 현무 암록빛
            }

            return rarity switch
            {
                5 => new Color(1.0f, 0.88f, 0.25f, 1.0f),  // 5성 황금
                4 => new Color(0.85f, 0.5f, 1.0f, 1.0f),   // 4성 보라
                _ => new Color(0.85f, 0.9f, 0.95f, 1.0f)   // 3성 은백
            };
        }

        /// <summary>
        /// [에디터 검증 유틸리티] 더미 4부위 날개를 즉시 장착하여 시각 렌더링 반영 여부를 테스트합니다.
        /// </summary>
        [ContextMenu("Test Equip Dummy 4-Slot Wings")]
        public void TestEquipDummyWings()
        {
            EnsureWingHierarchy();

            var dummyChar = new CharacterInstance(
                CharacterBaseData.Create("HERO_DUMMY_WING_TEST", "천공의 검성", 5, 60),
                null);

            dummyChar.EquipWing(WingSlotType.MainLeft, new WingItemInstance("W_01", "청룡의 천공익[좌]", WingSlotType.MainLeft, 5, "SET_BLUE_DRAGON", 60));
            dummyChar.EquipWing(WingSlotType.MainRight, new WingItemInstance("W_02", "청룡의 천공익[우]", WingSlotType.MainRight, 5, "SET_BLUE_DRAGON", 60));
            dummyChar.EquipWing(WingSlotType.SubLeft, new WingItemInstance("W_03", "백호의 서리날개[좌]", WingSlotType.SubLeft, 4, "SET_WHITE_TIGER", 30));
            dummyChar.EquipWing(WingSlotType.SubRight, new WingItemInstance("W_04", "백호의 서리날개[우]", WingSlotType.SubRight, 4, "SET_WHITE_TIGER", 30));

            UpdateVisual(dummyChar);
            Debug.Log($"🪽 [CharacterWingVisual] 더미 4부위 날개 장착 완료! (총 대미지 가산치: +{dummyChar.TotalWingBonusDamage})");
        }

        [ContextMenu("Clear All Wings")]
        public void ClearAllWings()
        {
            EnsureWingHierarchy();
            UpdateVisual(null);
            Debug.Log("🪽 [CharacterWingVisual] 모든 날개 스프라이트 비활성화 완료.");
        }
    }
}
