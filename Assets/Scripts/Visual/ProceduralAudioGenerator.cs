using System;
using UnityEngine;

namespace RPG25D.Visual
{
    /// <summary>
    /// 외부 오디오 파일 의존성 없이 순수 C# 코드로 실시간 사운드(SFX)를 합성/생성하는 프로시저럴 오디오 생성기입니다.
    /// D20 튕김음, 대실패 유리 파편음, 일반 타격음(피치 스케일링), 즉사 팡파르/심장 박동음을 즉시 연주합니다.
    /// </summary>
    public static class ProceduralAudioGenerator
    {
        private const int SampleRate = 44100;

        /// <summary>
        /// 주사위가 바닥/벽에 부딪힐 때 발생하는 물리 타격음 (둔탁한 레진/우드 틱음)
        /// </summary>
        public static AudioClip CreateDiceBounceClip()
        {
            int samples = (int)(SampleRate * 0.08f); // 80ms
            float[] data = new float[samples];
            System.Random rand = new System.Random();

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 60f); // 빠른 감쇠

                // 저음 바디(180Hz) + 충격 노이즈
                float body = Mathf.Sin(2 * Mathf.PI * 180f * t);
                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.5f;

                data[i] = (body * 0.7f + noise * 0.3f) * env;
            }

            var clip = AudioClip.Create("SFX_DiceBounce", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 대실패(1): 유리가 깨지는 듯한 불협화음 SFX
        /// </summary>
        public static AudioClip CreateCritFailClip()
        {
            int samples = (int)(SampleRate * 0.65f); // 650ms
            float[] data = new float[samples];
            System.Random rand = new System.Random();

            // 불협화음 클러스터 주파수 (단2도/증4도 불협화음)
            float f1 = 440f; // A4
            float f2 = 466.16f; // Bb4 (단2도 불협)
            float f3 = 622.25f; // Eb5 (트라이톤)
            float f4 = 880f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 7f); // 감쇠
                float glassNoise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 20f);

                float tone = Mathf.Sin(2 * Mathf.PI * f1 * t) * 0.3f
                           + Mathf.Sin(2 * Mathf.PI * f2 * t) * 0.3f
                           + Mathf.Sin(2 * Mathf.PI * f3 * t) * 0.25f
                           + Mathf.Sin(2 * Mathf.PI * f4 * t) * 0.15f;

                data[i] = Mathf.Clamp((tone * 0.6f + glassNoise * 0.4f) * env, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_CritFail", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 일반 적중(2~19): 주사위 눈금 크기에 비례하여 피치와 강도가 점진 상승하는 타격음
        /// </summary>
        public static AudioClip CreateHitClip(int d20Value)
        {
            int samples = (int)(SampleRate * 0.22f); // 220ms
            float[] data = new float[samples];
            System.Random rand = new System.Random();

            // 눈금(2~19)에 따라 주파수 상승: 140Hz ~ 380Hz
            float progress = Mathf.Clamp01((d20Value - 2) / 17f);
            float baseFreq = Mathf.Lerp(140f, 380f, progress);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * (18f - progress * 6f)); // 고눈금일수록 서스테인 길어짐
                float pitchDrop = Mathf.Lerp(baseFreq * 1.5f, baseFreq * 0.8f, t / 0.22f);

                float body = Mathf.Sin(2 * Mathf.PI * pitchDrop * t);
                float punch = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 40f);

                data[i] = (body * 0.75f + punch * 0.25f) * env;
            }

            var clip = AudioClip.Create($"SFX_Hit_{d20Value}", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 절대 성공/즉사(20): 웅장한 승리 팡파르 코드와 심장 박동 서브베이스 SFX
        /// </summary>
        public static AudioClip CreateInstantKillFanfareClip()
        {
            int samples = (int)(SampleRate * 1.2f); // 1.2초
            float[] data = new float[samples];

            // C Major 팡파르 아르페지오/코드 (C5, E5, G5, C6)
            float c5 = 523.25f;
            float e5 = 659.25f;
            float g5 = 783.99f;
            float c6 = 1046.50f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;

                // 심장 박동 서브베이스 (0~0.25초 구간)
                float heartbeat = 0f;
                if (t < 0.25f)
                {
                    heartbeat = Mathf.Sin(2 * Mathf.PI * 55f * t) * Mathf.Exp(-t * 15f) * 0.8f;
                }

                // 팡파르 브라스 사운드 (0.15초 이후 점진 상승)
                float fanfareEnv = t < 0.15f ? 0f : Mathf.Exp(-(t - 0.15f) * 3.5f);
                float chord = (Mathf.Sin(2 * Mathf.PI * c5 * t)
                             + Mathf.Sin(2 * Mathf.PI * e5 * t)
                             + Mathf.Sin(2 * Mathf.PI * g5 * t)
                             + Mathf.Sin(2 * Mathf.PI * c6 * t) * 0.8f) * 0.25f;

                // 고음 반짝임 하모닉스
                float shimmer = Mathf.Sin(2 * Mathf.PI * (c6 * 1.5f) * t) * 0.1f * fanfareEnv;

                data[i] = Mathf.Clamp(heartbeat + (chord + shimmer) * fanfareEnv, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_InstantKillFanfare", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
