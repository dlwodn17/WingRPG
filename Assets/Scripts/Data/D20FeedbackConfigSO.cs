using UnityEngine;

namespace RPG25D.Data
{
    /// <summary>
    /// D20 결과별 극단적 도파민 연출 파라미터 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "D20FeedbackConfig", menuName = "RPG25D/D20 Feedback Config")]
    public class D20FeedbackConfigSO : ScriptableObject
    {
        [Header("대실패 (눈금 1) 설정")]
        public Color failColor = new Color(0.95f, 0.2f, 0.2f);
        public float failCameraShake = 0.2f;

        [Header("일반 성공 (눈금 2~19) 설정")]
        public Color hitMinColor = new Color(1f, 0.9f, 0.3f); // 저눈금 (노랑)
        public Color hitMaxColor = new Color(1f, 0.4f, 0.1f); // 고눈금 (강렬한 오렌지/레드)
        public float hitShakeMin = 0.15f;
        public float hitShakeMax = 0.55f;
        public float hitStopThreshold = 15; // 15 이상 눈금 시 히트스톱 발동
        public float hitStopDuration = 0.12f;

        [Header("절대 성공/즉사 (눈금 20) 설정")]
        public Color instantKillColor = new Color(1.0f, 0.85f, 0.1f); // 황금빛
        public float instantKillShake = 1.0f;
        public float instantKillHitStop = 0.25f;
        public float slowMotionScale = 0.2f;
        public float slowMotionDuration = 0.8f;

        [Header("대미지 텍스트 폰트 크기")]
        public int minFontSize = 18;
        public int maxFontSize = 44;

        public int GetFontSizeForRoll(int d20)
        {
            if (d20 == 1) return minFontSize;
            if (d20 == 20) return maxFontSize;
            float t = (d20 - 2) / 17f;
            return Mathf.RoundToInt(Mathf.Lerp(minFontSize, maxFontSize - 6, t));
        }

        public Color GetColorForRoll(int d20)
        {
            if (d20 == 1) return failColor;
            if (d20 == 20) return instantKillColor;
            float t = (d20 - 2) / 17f;
            return Color.Lerp(hitMinColor, hitMaxColor, t);
        }
    }
}
