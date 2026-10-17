using MonsoonRooftops.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonsoonRooftops.EditorTools
{
    public static class GreyboxSceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Greybox.unity";

        [MenuItem("Monsoon/Create Greybox Scene")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Phase1Bootstrap").AddComponent<Phase1Bootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Greybox scene saved and set as the only build scene. Press Play, or Build And Run.");
        }
    }
}
