#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RPG25D.UI;
using RPG25D.Visual;

namespace RPG25D.EditorScripts
{
    /// <summary>
    /// GachaScene.unity 자동 구성 및 UI/비주얼 컴포넌트 일괄 세팅 에디터 유틸리티
    /// </summary>
    public static class GachaSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/GachaScene.unity";

        [MenuItem("RPG25D/Setup Gacha Scene")]
        public static void SetupGachaScene()
        {
            var currentScene = SceneManager.GetActiveScene();
            if (currentScene.path != ScenePath)
            {
                currentScene = EditorSceneManager.OpenScene(ScenePath);
            }

            // 1. 기존 루트 오브젝트 정리
            var rootObjects = currentScene.GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                Object.DestroyImmediate(go);
            }

            // 2. 메인 카메라 생성
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 3.5f, -2.5f);
            camGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();

            // 3. 디렉셔널 라이트 생성
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.position = new Vector3(0f, 8f, -4f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.88f, 0.92f, 1f);
            dirLight.intensity = 1.1f;

            // 4. 패판 (3D Altar Board)
            var altarGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altarGo.name = "[Gacha_Altar_Board]";
            altarGo.transform.position = new Vector3(0f, 0.2f, 3.0f);
            altarGo.transform.localScale = new Vector3(5.5f, 0.2f, 5.5f);
            var altarCol = altarGo.GetComponent<Collider>();
            if (altarCol != null) Object.DestroyImmediate(altarCol);
            var altarMeshCol = altarGo.AddComponent<MeshCollider>();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var altarMat = new Material(litShader)
            {
                color = new Color(0.12f, 0.14f, 0.18f)
            };
            altarGo.GetComponent<MeshRenderer>().sharedMaterial = altarMat;

            // 5. 알타 스팟라이트
            var spotLightGo = new GameObject("[Altar_SpotLight]");
            spotLightGo.transform.position = new Vector3(0f, 5.5f, 2.5f);
            spotLightGo.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            var spotLight = spotLightGo.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.range = 10f;
            spotLight.spotAngle = 65f;
            spotLight.intensity = 2.8f;
            spotLight.color = new Color(0.85f, 0.95f, 1f);

            // 6. 가챠 메인 컨트롤러 (GachaVisualDirector, UI 매니저들)
            var controllerGo = new GameObject("[Gacha_GameController]");
            var director = controllerGo.AddComponent<GachaVisualDirector>();
            var resultUI = controllerGo.AddComponent<GachaResultUI>();
            var partyUI = controllerGo.AddComponent<PartyFormationUI>();
            var detailModal = controllerGo.AddComponent<CharacterDetailModal>();
            var sceneHUD = controllerGo.AddComponent<GachaSceneHUD>();

            // 7. 씬 저장
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);

            Debug.Log($"🎉 [GachaSceneSetup] {ScenePath} 씬 구성 및 컴포넌트 연결 완료!");
        }
    }
}
#endif
