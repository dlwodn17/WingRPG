using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RPG25D.Core.Exploration;
using RPG25D.Data;
using RPG25D.Data.Events;
using RPG25D.Gameplay.Exploration;
using RPG25D.UI;
using RPG25D.Visual;

namespace RPG25D.Editor
{
    /// <summary>
    /// [요구 산출물 3] ExplorationSceneBuilder.cs
    /// Unity CLI / MCP 및 에디터 메뉴에서 단 한 번의 호출로 2.5D 탐색 맵, 플레이어,
    /// 적 심볼(3종), 보물상자, NPC, 카메라, UI를 포함하는 탐색 씬을 완전 자동 구성합니다.
    /// </summary>
    public static class ExplorationSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Level01_Exploration.unity";
        private const string LegacyScenePath = "Assets/Scenes/ExplorationScene.unity";
        private const string DataDir = "Assets/Data/Exploration";
        private const string GameManagerDataPath = "Assets/Data/GameManagerData.asset";
        private const string ResourcesDir = "Assets/Resources";

        [MenuItem("Tools/RPG25D/Setup Exploration Scene")]
        public static void BuildExplorationSceneMenu()
        {
            BuildExplorationScene();
        }

        [MenuItem("Tools/RPG25D/Attach EnemySymbol to Scene Objects")]
        public static void AttachEnemySymbolsToScenesMenu()
        {
            AttachEnemySymbolsToScene(ScenePath);
            AttachEnemySymbolsToScene(LegacyScenePath);
        }

        public static void AttachEnemySymbolsToScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[ExplorationSceneBuilder] 씬 파일이 존재하지 않습니다: {scenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var rootObjects = scene.GetRootGameObjects();
            int attachedCount = 0;

            foreach (var root in rootObjects)
            {
                var actors = root.GetComponentsInChildren<EnemySymbolActor>(true);
                foreach (var actor in actors)
                {
                    var symbol = actor.GetComponent<EnemySymbol>();
                    if (symbol == null)
                    {
                        symbol = actor.gameObject.AddComponent<EnemySymbol>();
                        symbol.Initialize(actor.SymbolId);
                        attachedCount++;
                        Debug.Log($"[ExplorationSceneBuilder] ➕ EnemySymbol 부착: {actor.gameObject.name} (ID: {symbol.EnemyID})");
                    }
                    else if (string.IsNullOrEmpty(symbol.EnemyID))
                    {
                        symbol.Initialize(actor.SymbolId);
                    }
                    EditorUtility.SetDirty(actor.gameObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ExplorationSceneBuilder] ✅ 씬 '{scenePath}' 내 EnemySymbol 부착/ID 부여 완료 (부착 수: {attachedCount})");
        }

        public static void BuildExplorationScene()
        {
            Debug.Log("==================================================");
            Debug.Log("🗺️ [ExplorationSceneBuilder] 2.5D 탐색 씬(Level01_Exploration) 자동 구성을 시작합니다.");
            Debug.Log("==================================================");

            EnsureDirectories();

            // 1. ScriptableObject 이벤트 및 데이터 에셋 생성
            var chestSO = CreateOrLoadSO<TreasureChestEventSO>($"{DataDir}/Event_TreasureChest_Golden.asset", so =>
            {
                so.Initialize("CHEST_GOLDEN_01", "고대의 황금 상자", "엘릭서 (Elixir)", 250);
            });

            var npcSO = CreateOrLoadSO<NPCInteractionEventSO>($"{DataDir}/Event_NPC_Guide.asset", so =>
            {
                so.Initialize("NPC_GUIDE_01", "방랑 기사", new[]
                {
                    "이 앞은 몬스터들의 서식지다. 붉은 심볼에 닿으면 즉시 D20 전투가 시작되니 주의하게!",
                    "보물상자에서 엘릭서를 획득하고 충분히 채비를 마친 후 진입하는 것이 좋을 거야."
                });
            });

            // GameManagerData.asset 생성 및 동기화
            var gameManagerSO = CreateOrLoadSO<GameManager>(GameManagerDataPath, so =>
            {
                so.ResetGameData();
            });

            var resourcesGameManagerSO = CreateOrLoadSO<GameManager>($"{ResourcesDir}/GameManager.asset", so =>
            {
                so.ResetGameData();
            });

            GameManager.Instance = gameManagerSO;

            // 2. 신규 탐색 씬 생성
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 머티리얼 구성
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var groundMat = CreateOrLoadMaterial("Assets/Materials/M_Exploration_Ground.mat", litShader, new Color(0.22f, 0.26f, 0.32f));
            var wallMat = CreateOrLoadMaterial("Assets/Materials/M_Exploration_Wall.mat", litShader, new Color(0.35f, 0.38f, 0.44f));
            var heroMat = CreateOrLoadMaterial("Assets/Materials/M_Hero.mat", litShader, new Color(0.1f, 0.7f, 1.0f));
            var goblinMat = CreateOrLoadMaterial("Assets/Materials/M_Goblin.mat", litShader, new Color(0.9f, 0.25f, 0.2f));
            var orcMat = CreateOrLoadMaterial("Assets/Materials/M_Orc.mat", litShader, new Color(0.9f, 0.5f, 0.1f));
            var slimeMat = CreateOrLoadMaterial("Assets/Materials/M_Slime.mat", litShader, new Color(0.3f, 0.9f, 0.35f));
            var chestMat = CreateOrLoadMaterial("Assets/Materials/M_Chest.mat", litShader, new Color(1f, 0.8f, 0.15f));
            var npcMat = CreateOrLoadMaterial("Assets/Materials/M_NPC.mat", litShader, new Color(0.3f, 0.7f, 0.4f));

            // 3. 조명 생성
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.96f, 0.9f);
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // 4. 3D 필드 바닥 및 장식 벽 생성
            var groundRoot = new GameObject("Ground_Environment");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground_Floor";
            floor.transform.parent = groundRoot.transform;
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3.5f, 1f, 3.5f); // 35m x 35m 넓은 탐색 필드
            floor.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // 외곽 경계 벽 4개
            CreateWall("Wall_North", new Vector3(0f, 1.5f, 17.5f), new Vector3(35f, 3f, 1f), groundRoot.transform, wallMat);
            CreateWall("Wall_South", new Vector3(0f, 1.5f, -17.5f), new Vector3(35f, 3f, 1f), groundRoot.transform, wallMat);
            CreateWall("Wall_West", new Vector3(-17.5f, 1.5f, 0f), new Vector3(1f, 3f, 35f), groundRoot.transform, wallMat);
            CreateWall("Wall_East", new Vector3(17.5f, 1.5f, 0f), new Vector3(1f, 3f, 35f), groundRoot.transform, wallMat);

            // 필드 기둥 장식
            CreatePillar("Pillar_NW", new Vector3(-6f, 2f, 6f), groundRoot.transform, wallMat);
            CreatePillar("Pillar_NE", new Vector3(6f, 2f, 6f), groundRoot.transform, wallMat);
            CreatePillar("Pillar_SW", new Vector3(-6f, 2f, -6f), groundRoot.transform, wallMat);
            CreatePillar("Pillar_SE", new Vector3(6f, 2f, -6f), groundRoot.transform, wallMat);

            // 5. 2.5D 플레이어 캐릭터 생성
            var playerGo = Create25DActor("Player_Hero", new Vector3(0f, 1f, -10f), heroMat, true);
            var playerController = playerGo.AddComponent<ExplorationPlayerController>();

            // 6. 2.5D 적 심볼 3개 배치 (고블린, 오크, 슬라임)
            var symbolsRoot = new GameObject("Enemy_Symbols");

            var goblinSymGo = Create25DActor("Symbol_Goblin_01", new Vector3(-5f, 1f, 2f), goblinMat, false);
            goblinSymGo.transform.parent = symbolsRoot.transform;
            var gobSymbol = goblinSymGo.AddComponent<EnemySymbol>();
            gobSymbol.Initialize("Symbol_Goblin_01");
            var gobActor = goblinSymGo.AddComponent<EnemySymbolActor>();
            gobActor.Initialize(1, "고블린 정찰병", 1.4f, "Symbol_Goblin_01");

            var orcSymGo = Create25DActor("Symbol_Orc_02", new Vector3(5f, 1f, 3f), orcMat, false);
            orcSymGo.transform.parent = symbolsRoot.transform;
            var orcSymbol = orcSymGo.AddComponent<EnemySymbol>();
            orcSymbol.Initialize("Symbol_Orc_02");
            var orcActor = orcSymGo.AddComponent<EnemySymbolActor>();
            orcActor.Initialize(2, "오크 돌격병", 1.5f, "Symbol_Orc_02");

            var slimeSymGo = Create25DActor("Symbol_Slime_03", new Vector3(0f, 1f, 9f), slimeMat, false);
            slimeSymGo.transform.parent = symbolsRoot.transform;
            var slimeSymbol = slimeSymGo.AddComponent<EnemySymbol>();
            slimeSymbol.Initialize("Symbol_Slime_03");
            var slimeActor = slimeSymGo.AddComponent<EnemySymbolActor>();
            slimeActor.Initialize(3, "산성 슬라임", 1.3f, "Symbol_Slime_03");

            // 7. 상호작용 오브젝트 배치 (보물상자 & NPC)
            var interactablesRoot = new GameObject("Field_Interactables");

            // 보물상자
            var chestGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestGo.name = "Chest_Golden";
            chestGo.transform.parent = interactablesRoot.transform;
            chestGo.transform.position = new Vector3(-8f, 0.5f, -2f);
            chestGo.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            chestGo.GetComponent<MeshRenderer>().sharedMaterial = chestMat;
            var chestInter = chestGo.AddComponent<ExplorationInteractable>();
            chestInter.SetEvent(chestSO);

            // NPC
            var npcGo = Create25DActor("NPC_Guide", new Vector3(3.5f, 1f, -8f), npcMat, false);
            npcGo.transform.parent = interactablesRoot.transform;
            var npcInter = npcGo.AddComponent<ExplorationInteractable>();
            npcInter.SetEvent(npcSO);

            // 8. 2.5D 쿼터뷰 카메라 설정
            var camObj = new GameObject("Main Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = false;
            cam.fieldOfView = 34f;
            camObj.transform.position = playerGo.transform.position + new Vector3(0f, 7.5f, -8f);
            camObj.transform.rotation = Quaternion.Euler(34f, 0f, 0f);
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<CameraShakeController>();
            var camFollow = camObj.AddComponent<ExplorationCameraFollow>();
            camFollow.Target = playerGo.transform;
            camFollow.SnapToTarget();

            // 9. 매니저 시스템 구성
            var managersGo = new GameObject("[Exploration_Managers]");
            var encMgr = managersGo.AddComponent<EncounterManager>();
            managersGo.AddComponent<ExplorationUIManager>();

            // 10. 씬 저장 및 Build Settings 등록
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.SaveScene(scene, LegacyScenePath);
            RegisterSceneInBuildSettings(ScenePath);
            RegisterSceneInBuildSettings(LegacyScenePath);
            RegisterSceneInBuildSettings("Assets/Scenes/SampleScene.unity");

            Debug.Log("==================================================");
            Debug.Log($"✅ [ExplorationSceneBuilder] 탐색 씬 생성 및 저장 완료: {ScenePath} & {LegacyScenePath}");
            Debug.Log("==================================================");
        }

        private static GameObject Create25DActor(string name, Vector3 pos, Material mat, bool isPlayer)
        {
            var root = new GameObject(name);
            root.transform.position = pos;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = "Visual";
            visual.transform.parent = root.transform;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(1.2f, 1.9f, 0.1f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // 콜라이더 트리거 설정
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 1.8f, 1f);
            col.isTrigger = true;

            root.AddComponent<BillboardActor25D>();
            return root;
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 size, Transform parent, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.parent = parent;
            wall.transform.position = pos;
            wall.transform.localScale = size;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void CreatePillar(string name, Vector3 pos, Transform parent, Material mat)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = name;
            pillar.transform.parent = parent;
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            if (!Directory.Exists(ResourcesDir)) Directory.CreateDirectory(ResourcesDir);
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
        }

        private static T CreateOrLoadSO<T>(string assetPath, Action<T> initializer) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                initializer(asset);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                initializer(asset);
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static Material CreateOrLoadMaterial(string path, Shader shader, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void RegisterSceneInBuildSettings(string path)
        {
            var existingScenes = EditorBuildSettings.scenes;
            if (Array.Exists(existingScenes, s => s.path == path)) return;

            var newScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            Array.Copy(existingScenes, newScenes, existingScenes.Length);
            newScenes[existingScenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = newScenes;
        }
    }
}
