using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using RPG25D.Data;
using RPG25D.Gameplay;
using RPG25D.UI;
using RPG25D.Visual;

namespace RPG25D.Editor
{
    public static class SceneSetupBuilder
    {
        [MenuItem("Tools/RPG25D/Setup 2.5D Battle Scene")]
        public static void BuildScene()
        {
            Debug.Log("[SceneSetupBuilder] 3D 물리 D20 및 극단적 비주얼 피드백 시스템 씬 구축을 시작합니다...");

            // 1. Data 폴더 확인
            string dataDir = "Assets/Data";
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
                AssetDatabase.Refresh();
            }

            // 2. 아군 3인 & 적군 3인 캐릭터 ScriptableObjects 생성
            var heroData = CreateOrLoadSO<CharacterDataSO>($"{dataDir}/HeroData.asset", so =>
            {
                so.characterName = "용사 (Knight)";
                so.isPlayer = true;
                so.maxHP = 130;
                so.baseAttack = 16;
                so.speed = 12;
                so.themeColor = new Color(0.2f, 0.75f, 1f);
            });

            var mageData = CreateOrLoadSO<CharacterDataSO>($"{dataDir}/MageData.asset", so =>
            {
                so.characterName = "마법사 (Mage)";
                so.isPlayer = true;
                so.maxHP = 90;
                so.baseAttack = 22;
                so.speed = 15;
                so.themeColor = new Color(0.85f, 0.35f, 1f);
            });

            var clericData = CreateOrLoadSO<CharacterDataSO>($"{dataDir}/ClericData.asset", so =>
            {
                so.characterName = "사제 (Cleric)";
                so.isPlayer = true;
                so.maxHP = 110;
                so.baseAttack = 12;
                so.speed = 9;
                so.themeColor = new Color(0.25f, 0.95f, 0.6f);
            });

            var goblinData = CreateOrLoadSO<CharacterDataSO>($"{dataDir}/GoblinData.asset", so =>
            {
                so.characterName = "고블린 돌격병";
                so.isPlayer = false;
                so.maxHP = 65;
                so.baseAttack = 11;
                so.speed = 14;
                so.themeColor = new Color(0.9f, 0.3f, 0.2f);
            });

            var orcData = CreateOrLoadSO<CharacterDataSO>($"{dataDir}/OrcData.asset", so =>
            {
                so.characterName = "오크 광전사";
                so.isPlayer = false;
                so.maxHP = 95;
                so.baseAttack = 15;
                so.speed = 8;
                so.themeColor = new Color(0.9f, 0.5f, 0.1f);
            });

            var slimeData = CreateOrLoadSO<CharacterDataSO>($"{dataDir}/SlimeData.asset", so =>
            {
                so.characterName = "산성 슬라임";
                so.isPlayer = false;
                so.maxHP = 50;
                so.baseAttack = 9;
                so.speed = 10;
                so.themeColor = new Color(0.3f, 0.9f, 0.35f);
            });

            // 3. D20 공격 스킬 ScriptableObjects 생성
            var skillSlash = CreateOrLoadSO<AttackSkill>($"{dataDir}/Skill_Slash.asset", so =>
            {
                so.skillId = "SKILL_SLASH";
                so.skillName = "참격 (Slash)";
                so.description = "단일 대상을 향한 정밀한 베기. 기본 대미지 15 + D20 눈금 * 2.0";
                so.baseDamage = 15;
                so.scaleMultiplier = 2.0f;
                so.hitCount = 1;
            });

            var skillDual = CreateOrLoadSO<AttackSkill>($"{dataDir}/Skill_DualStrike.asset", so =>
            {
                so.skillId = "SKILL_DUAL";
                so.skillName = "연속 베기 (Dual Strike)";
                so.description = "두 번 연속으로 적을 난도질합니다. 기본 대미지 10 + D20 눈금 * 1.5";
                so.baseDamage = 10;
                so.scaleMultiplier = 1.5f;
                so.hitCount = 2;
            });

            var skillHeavy = CreateOrLoadSO<AttackSkill>($"{dataDir}/Skill_HeavyBlow.asset", so =>
            {
                so.skillId = "SKILL_HEAVY";
                so.skillName = "결전 강타 (Heavy Blow)";
                so.description = "모든 힘을 실은 회심의 일격. 기본 대미지 25 + D20 눈금 * 3.5";
                so.baseDamage = 25;
                so.scaleMultiplier = 3.5f;
                so.hitCount = 1;
            });

            var skillGoblinBite = CreateOrLoadSO<AttackSkill>($"{dataDir}/Skill_GoblinBite.asset", so =>
            {
                so.skillId = "SKILL_BITE";
                so.skillName = "물어뜯기 (Bite)";
                so.description = "날카로운 이빨로 물어뜯습니다. 기본 대미지 8 + D20 눈금 * 1.2";
                so.baseDamage = 8;
                so.scaleMultiplier = 1.2f;
                so.hitCount = 1;
            });

            var skillAcidSpray = CreateOrLoadSO<AttackSkill>($"{dataDir}/Skill_AcidSpray.asset", so =>
            {
                so.skillId = "SKILL_ACID";
                so.skillName = "독액 분사 (Acid Spray)";
                so.description = "강한 산성 독액을 뿜어냅니다. 기본 대미지 12 + D20 눈금 * 1.8";
                so.baseDamage = 12;
                so.scaleMultiplier = 1.8f;
                so.hitCount = 1;
            });

            // 4. 피드백 설정 SO 생성
            var feedbackConfig = CreateOrLoadSO<D20FeedbackConfigSO>($"{dataDir}/D20FeedbackConfig.asset", so =>
            {
                so.failColor = new Color(0.95f, 0.2f, 0.2f);
                so.failCameraShake = 0.25f;
                so.hitMinColor = new Color(1f, 0.9f, 0.3f);
                so.hitMaxColor = new Color(1f, 0.35f, 0.1f);
                so.hitShakeMin = 0.15f;
                so.hitShakeMax = 0.55f;
                so.hitStopThreshold = 15;
                so.hitStopDuration = 0.12f;
                so.instantKillColor = new Color(1.0f, 0.85f, 0.1f);
                so.instantKillShake = 0.9f;
                so.instantKillHitStop = 0.22f;
                so.slowMotionScale = 0.2f;
                so.slowMotionDuration = 0.8f;
            });

            // 4-B. 타격감 (히트스톱 & 2.5D 카메라 셰이크) 설정 SO 생성
            var impactConfig = CreateOrLoadSO<ImpactFeedbackConfigSO>($"{dataDir}/ImpactFeedbackConfig.asset", so =>
            {
                // 눈금 1 (대실패)
                so.critFailProfile = new ImpactProfile
                {
                    hitStopDuration = 0.20f,
                    hitStopTimeScale = 0f,
                    shakeAmplitude = 0.28f,
                    shakeFrequency = 14f,
                    shakeDuration = 0.45f,
                    useLetterbox = true,
                    axisMultiplier = new Vector3(0.9f, 0.4f, 0.5f)
                };
                // 눈금 2~10 (약한 타격)
                so.weakHitProfileMin = new ImpactProfile
                {
                    hitStopDuration = 0.03f,
                    hitStopTimeScale = 0f,
                    shakeAmplitude = 0.12f,
                    shakeFrequency = 22f,
                    shakeDuration = 0.20f,
                    useLetterbox = false,
                    axisMultiplier = new Vector3(1.0f, 0.3f, 0.6f)
                };
                so.weakHitProfileMax = new ImpactProfile
                {
                    hitStopDuration = 0.06f,
                    hitStopTimeScale = 0f,
                    shakeAmplitude = 0.25f,
                    shakeFrequency = 28f,
                    shakeDuration = 0.25f,
                    useLetterbox = false,
                    axisMultiplier = new Vector3(1.0f, 0.35f, 0.7f)
                };
                // 눈금 11~19 (강한 타격)
                so.strongHitProfileMin = new ImpactProfile
                {
                    hitStopDuration = 0.09f,
                    hitStopTimeScale = 0f,
                    shakeAmplitude = 0.30f,
                    shakeFrequency = 30f,
                    shakeDuration = 0.28f,
                    useLetterbox = false,
                    axisMultiplier = new Vector3(1.0f, 0.4f, 0.8f)
                };
                so.strongHitProfileMax = new ImpactProfile
                {
                    hitStopDuration = 0.16f,
                    hitStopTimeScale = 0f,
                    shakeAmplitude = 0.60f,
                    shakeFrequency = 38f,
                    shakeDuration = 0.38f,
                    useLetterbox = false,
                    axisMultiplier = new Vector3(1.1f, 0.45f, 0.9f)
                };
                // 눈금 20 (즉사)
                so.instantKillProfile = new ImpactProfile
                {
                    hitStopDuration = 0.25f,
                    hitStopTimeScale = 0f,
                    shakeAmplitude = 0.95f,
                    shakeFrequency = 45f,
                    shakeDuration = 0.55f,
                    useLetterbox = true,
                    axisMultiplier = new Vector3(1.2f, 0.5f, 1.0f)
                };
                so.maxShakeClamp = new Vector3(1.2f, 0.5f, 0.9f);
            });

            AssetDatabase.SaveAssets();

            // 5. 머티리얼 생성 (URP Lit 기반 그림자 반응형)
            string matDir = "Assets/Materials";
            if (!Directory.Exists(matDir))
            {
                Directory.CreateDirectory(matDir);
                AssetDatabase.Refresh();
            }

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var groundMat = CreateOrLoadMaterial($"{matDir}/M_Ground.mat", litShader, new Color(0.18f, 0.22f, 0.28f));
            var heroMat = CreateOrLoadMaterial($"{matDir}/M_Hero.mat", litShader, new Color(0.1f, 0.7f, 1.0f));
            var goblinMat = CreateOrLoadMaterial($"{matDir}/M_Goblin.mat", litShader, new Color(0.9f, 0.25f, 0.2f));
            var slimeMat = CreateOrLoadMaterial($"{matDir}/M_Slime.mat", litShader, new Color(0.3f, 0.9f, 0.35f));
            var pillarMat = CreateOrLoadMaterial($"{matDir}/M_Pillar.mat", litShader, new Color(0.4f, 0.45f, 0.5f));
            var diceMat = CreateOrLoadMaterial($"{matDir}/M_DiceD20.mat", litShader, new Color(0.85f, 0.15f, 0.2f)); // 깊고 고급스러운 루비 레드 D20 주사위

            // 6. 씬 오브젝트 구성
            var activeScene = SceneManager.GetActiveScene();

            var roots = activeScene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.name == "Ground_3D" || root.name == "2.5D_Actors" || root.name == "[BattleSystem]" || root.name == "StageDecorations" || root.name == "[DicePhysicsVisualizer]" || root.name == "[Party_Battle_Field]")
                {
                    Object.DestroyImmediate(root);
                }
            }

            // A. 메인 카메라 2.5D 튜닝
            var camObj = GameObject.Find("Main Camera");
            if (camObj == null)
            {
                camObj = new GameObject("Main Camera");
                camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
            camObj.transform.position = new Vector3(0.5f, 3.8f, -9.5f);
            camObj.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            var cam = camObj.GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            if (camObj.GetComponent<CameraShakeController>() == null)
            {
                camObj.AddComponent<CameraShakeController>();
            }

            // B. 3D 지형 (3D X-Z 평면)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground_3D";
            ground.transform.position = new Vector3(0.5f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(18f, 1f, 10f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // C. 무대 장식
            var decorRoot = new GameObject("StageDecorations");
            for (int i = -3; i <= 3; i++)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Pillar_{i}";
                pillar.transform.parent = decorRoot.transform;
                pillar.transform.position = new Vector3(i * 2.5f + 0.5f, 1.5f, 4.2f);
                pillar.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
            }

            // D. 라이트 그림자 튜닝
            var lightObj = GameObject.Find("Directional Light");
            if (lightObj != null)
            {
                lightObj.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                var lightComp = lightObj.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.shadows = LightShadows.Soft;
                    lightComp.shadowStrength = 0.85f;
                }
            }

            // F. 3D 물리 D20 주사위 오브젝트 생성
            var diceObj = new GameObject("[DicePhysicsVisualizer]");
            diceObj.transform.position = new Vector3(0f, 4.5f, 0f);
            var diceVisualizer = diceObj.AddComponent<DicePhysicsVisualizer>();
            var diceRenderer = diceObj.GetComponent<MeshRenderer>();
            diceRenderer.sharedMaterial = diceMat;
            diceRenderer.shadowCastingMode = ShadowCastingMode.On;
            diceRenderer.receiveShadows = true;

            // G. 전투 시스템 및 파티 매니저 구성
            var battleSystemObj = new GameObject("[BattleSystem]");

            var partyManager = battleSystemObj.AddComponent<PartyManager>();
            var soParty = new SerializedObject(partyManager);

            var allyListProp = soParty.FindProperty("_allyDataList");
            allyListProp.arraySize = 3;
            allyListProp.GetArrayElementAtIndex(0).objectReferenceValue = heroData;
            allyListProp.GetArrayElementAtIndex(1).objectReferenceValue = mageData;
            allyListProp.GetArrayElementAtIndex(2).objectReferenceValue = clericData;

            var enemyListProp = soParty.FindProperty("_enemyDataList");
            enemyListProp.arraySize = 3;
            enemyListProp.GetArrayElementAtIndex(0).objectReferenceValue = goblinData;
            enemyListProp.GetArrayElementAtIndex(1).objectReferenceValue = orcData;
            enemyListProp.GetArrayElementAtIndex(2).objectReferenceValue = slimeData;
            soParty.ApplyModifiedProperties();

            var turnController = battleSystemObj.AddComponent<BattleTurnController>();
            var soController = new SerializedObject(turnController);
            soController.FindProperty("_partyManager").objectReferenceValue = partyManager;

            var defaultSkillsProp = soController.FindProperty("_defaultSkills");
            defaultSkillsProp.arraySize = 3;
            defaultSkillsProp.GetArrayElementAtIndex(0).objectReferenceValue = skillSlash;
            defaultSkillsProp.GetArrayElementAtIndex(1).objectReferenceValue = skillDual;
            defaultSkillsProp.GetArrayElementAtIndex(2).objectReferenceValue = skillHeavy;
            soController.ApplyModifiedProperties();

            // H. 비주얼 피드백 매니저 추가 및 연결
            var feedbackManager = battleSystemObj.AddComponent<BattleFeedbackManager>();
            var soFeedback = new SerializedObject(feedbackManager);
            soFeedback.FindProperty("_diceVisualizer").objectReferenceValue = diceVisualizer;
            soFeedback.FindProperty("_turnController").objectReferenceValue = turnController;
            soFeedback.FindProperty("_config").objectReferenceValue = feedbackConfig;
            soFeedback.ApplyModifiedProperties();

            // I. 타격감 (히트스톱 & 2.5D 카메라 셰이크) 매니저 추가 및 연결
            var impactManager = battleSystemObj.AddComponent<ImpactFeedbackManager>();
            var soImpact = new SerializedObject(impactManager);
            soImpact.FindProperty("_config").objectReferenceValue = impactConfig;
            soImpact.FindProperty("_targetCamera").objectReferenceValue = cam;
            soImpact.ApplyModifiedProperties();

            // J. UI 매니저 추가
            battleSystemObj.AddComponent<BattleUIManager>();

            // 7. 씬 저장
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("==================================================");
            Debug.Log("[SceneSetupBuilder] ✅ 3D 물리 D20 및 도파민 피드백 씬 구축 완료!");
            Debug.Log("==================================================");
        }

        private static GameObject CreateBillboardActor(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var actorObj = new GameObject(name);
            actorObj.transform.parent = parent;
            actorObj.transform.position = pos;

            var visualObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visualObj.name = "Visual";
            visualObj.transform.parent = actorObj.transform;
            visualObj.transform.localPosition = Vector3.zero;
            visualObj.transform.localScale = scale;

            var mr = visualObj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            actorObj.AddComponent<BillboardActor25D>();

            return actorObj;
        }

        private static T CreateOrLoadSO<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                configure?.Invoke(asset);
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                configure?.Invoke(asset);
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }

        private static Material CreateOrLoadMaterial(string path, Shader shader, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.color = color;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
