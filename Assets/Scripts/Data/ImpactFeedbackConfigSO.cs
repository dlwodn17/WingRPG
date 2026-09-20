using System;
using UnityEngine;

namespace RPG25D.Data
{
    /// <summary>
    /// 타격감 단일 프로필 (히트스톱 및 2.5D 카메라 셰이크 파라미터)
    /// </summary>
    [Serializable]
    public struct ImpactProfile
    {
        [Tooltip("히트 스톱 지속 시간 (초, Realtime)")]
        public float hitStopDuration;

        [Tooltip("히트 스톱 중 타임 스케일 (일반적으로 0, 미세 움직임 시 0.02)")]
        [Range(0f, 0.1f)]
        public float hitStopTimeScale;

        [Tooltip("카메라 셰이크 진폭 (Amplitude, 흔들림 강도)")]
        public float shakeAmplitude;

        [Tooltip("카메라 셰이크 주파수 (Frequency, Hz, 진동 빠르기)")]
        public float shakeFrequency;

        [Tooltip("카메라 셰이크 지속 시간 (초)")]
        public float shakeDuration;

        [Tooltip("화면 암전 시네마틱 레터박스 동시 활성화 여부")]
        public bool useLetterbox;

        [Tooltip("2.5D 축 가중치 (X=좌우, Y=상하(제한), Z=광축/전후 펀치)")]
        public Vector3 axisMultiplier;

        public static ImpactProfile Default => new ImpactProfile
        {
            hitStopDuration = 0.1f,
            hitStopTimeScale = 0f,
            shakeAmplitude = 0.35f,
            shakeFrequency = 25f,
            shakeDuration = 0.3f,
            useLetterbox = false,
            axisMultiplier = new Vector3(1.0f, 0.35f, 0.7f)
        };
    }

    /// <summary>
    /// D20 눈금별 차등 타격감(히트스톱 & 2.5D 카메라 셰이크) 설정 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "ImpactFeedbackConfig", menuName = "RPG25D/Impact Feedback Config")]
    public class ImpactFeedbackConfigSO : ScriptableObject
    {
        [Header("1. 대실패 (눈금 1) 설정")]
        [Tooltip("주는 피해 없음: 무겁고 둔탁한 저주파 셰이크 + 긴 동결 + 불길한 레터박스")]
        public ImpactProfile critFailProfile = new ImpactProfile
        {
            hitStopDuration = 0.20f,
            hitStopTimeScale = 0f,
            shakeAmplitude = 0.28f,
            shakeFrequency = 14f, // 둔탁하고 느린 주파수
            shakeDuration = 0.45f,
            useLetterbox = true,
            axisMultiplier = new Vector3(0.9f, 0.4f, 0.5f)
        };

        [Header("2. 약한 타격 (눈금 2 ~ 10) 설정")]
        [Tooltip("경쾌하고 가벼운 타격: 최소 히트스톱(0.04s) 및 미세 셰이크")]
        public ImpactProfile weakHitProfileMin = new ImpactProfile
        {
            hitStopDuration = 0.03f,
            hitStopTimeScale = 0f,
            shakeAmplitude = 0.12f,
            shakeFrequency = 22f,
            shakeDuration = 0.20f,
            useLetterbox = false,
            axisMultiplier = new Vector3(1.0f, 0.3f, 0.6f)
        };

        public ImpactProfile weakHitProfileMax = new ImpactProfile
        {
            hitStopDuration = 0.06f,
            hitStopTimeScale = 0f,
            shakeAmplitude = 0.25f,
            shakeFrequency = 28f,
            shakeDuration = 0.25f,
            useLetterbox = false,
            axisMultiplier = new Vector3(1.0f, 0.35f, 0.7f)
        };

        [Header("3. 강한 타격 (눈금 11 ~ 19) 설정")]
        [Tooltip("뚜렷하고 묵직한 타격: 눈금 15 이상 뚜렷한 히트스톱(0.12~0.16s) 및 강도 점진 증가")]
        public ImpactProfile strongHitProfileMin = new ImpactProfile
        {
            hitStopDuration = 0.09f,
            hitStopTimeScale = 0f,
            shakeAmplitude = 0.30f,
            shakeFrequency = 30f,
            shakeDuration = 0.28f,
            useLetterbox = false,
            axisMultiplier = new Vector3(1.0f, 0.4f, 0.8f)
        };

        public ImpactProfile strongHitProfileMax = new ImpactProfile
        {
            hitStopDuration = 0.16f,
            hitStopTimeScale = 0f,
            shakeAmplitude = 0.60f,
            shakeFrequency = 38f,
            shakeDuration = 0.38f,
            useLetterbox = false,
            axisMultiplier = new Vector3(1.1f, 0.45f, 0.9f)
        };

        [Header("4. 절대 성공 / 즉사 (눈금 20) 설정")]
        [Tooltip("화면 전체가 찢어지는 듯한 최고 강도 셰이크 + 0.25s 강력 히트스톱 + 시네마틱 레터박스")]
        public ImpactProfile instantKillProfile = new ImpactProfile
        {
            hitStopDuration = 0.25f,
            hitStopTimeScale = 0f,
            shakeAmplitude = 0.95f, // 최대 진폭
            shakeFrequency = 45f,   // 초고주파 격렬 셰이크
            shakeDuration = 0.55f,
            useLetterbox = true,
            axisMultiplier = new Vector3(1.2f, 0.5f, 1.0f)
        };

        [Header("2.5D 안전 제한 (배경 레이어링 왜곡 방지)")]
        [Tooltip("카메라가 기준 좌표에서 벗어날 수 있는 최대 절대 반경 (X, Y, Z)")]
        public Vector3 maxShakeClamp = new Vector3(1.2f, 0.5f, 0.9f);

        /// <summary>
        /// D20 눈금(1~20)에 맞는 정밀 타격감 프로필을 계산합니다.
        /// </summary>
        public ImpactProfile GetProfileForRoll(int d20)
        {
            if (d20 <= 1) return critFailProfile;
            if (d20 >= 20) return instantKillProfile;

            if (d20 <= 10)
            {
                // 2 ~ 10 선형 보간
                float t = (d20 - 2) / 8.0f;
                return LerpProfile(weakHitProfileMin, weakHitProfileMax, t);
            }
            else
            {
                // 11 ~ 19 선형 보간
                float t = (d20 - 11) / 8.0f;
                return LerpProfile(strongHitProfileMin, strongHitProfileMax, t);
            }
        }

        private static ImpactProfile LerpProfile(ImpactProfile a, ImpactProfile b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ImpactProfile
            {
                hitStopDuration = Mathf.Lerp(a.hitStopDuration, b.hitStopDuration, t),
                hitStopTimeScale = Mathf.Lerp(a.hitStopTimeScale, b.hitStopTimeScale, t),
                shakeAmplitude = Mathf.Lerp(a.shakeAmplitude, b.shakeAmplitude, t),
                shakeFrequency = Mathf.Lerp(a.shakeFrequency, b.shakeFrequency, t),
                shakeDuration = Mathf.Lerp(a.shakeDuration, b.shakeDuration, t),
                useLetterbox = a.useLetterbox || b.useLetterbox,
                axisMultiplier = Vector3.Lerp(a.axisMultiplier, b.axisMultiplier, t)
            };
        }
    }
}
