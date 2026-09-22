using System;
using UnityEngine;

namespace RPG25D.Data
{
    /// <summary>
    /// GameManagerData를 상속하여 기존 시스템 및 직렬화 에셋과의 100% 하위 호환성을 제공하는 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameManager", menuName = "RPG25D/GameManager")]
    public class GameManager : GameManagerData
    {
        private static GameManager _runtimeGameManager;

        public new static GameManager Instance
        {
            get
            {
                if (GameManagerData.Instance is GameManager gm)
                {
                    return gm;
                }

                if (_runtimeGameManager == null)
                {
#if UNITY_EDITOR
                    _runtimeGameManager = UnityEditor.AssetDatabase.LoadAssetAtPath<GameManager>("Assets/Data/GameManagerData.asset");
#endif
                    if (_runtimeGameManager == null)
                    {
                        _runtimeGameManager = Resources.Load<GameManager>("GameManager");
                    }
                    if (_runtimeGameManager == null)
                    {
                        var found = Resources.FindObjectsOfTypeAll<GameManager>();
                        if (found != null && found.Length > 0)
                        {
                            _runtimeGameManager = found[0];
                        }
                    }
                    if (_runtimeGameManager == null)
                    {
                        _runtimeGameManager = CreateInstance<GameManager>();
                        _runtimeGameManager.name = "[Runtime_GameManager]";
                    }
                }

                if (GameManagerData.Instance == null)
                {
                    GameManagerData.Instance = _runtimeGameManager;
                }

                return _runtimeGameManager;
            }
            set
            {
                _runtimeGameManager = value;
                GameManagerData.Instance = value;
            }
        }
    }
}
