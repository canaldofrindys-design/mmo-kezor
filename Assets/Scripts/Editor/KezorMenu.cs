using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Kezor.Client;

namespace Kezor.EditorTools
{
    /// <summary>Menu "Kezor" no editor: cria as cenas do cliente e do servidor dedicado e registra no Build Settings.</summary>
    public static class KezorMenu
    {
        [MenuItem("Kezor/Criar cena do jogo (cliente)")]
        public static void CreateClientScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.AddComponent<Camera>(); cam.AddComponent<AudioListener>();
            new GameObject("Kezor").AddComponent<KezorGame>();
            Save(scene, "Assets/Scenes/Kezor.unity", 0);
        }

        [MenuItem("Kezor/Criar cena do servidor dedicado")]
        public static void CreateServerScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Servidor Kezor").AddComponent<ServerBootstrap>();
            Save(scene, "Assets/Scenes/KezorServer.unity", -1);
        }

        private static void Save(UnityEngine.SceneManagement.Scene scene, string path, int buildIndex)
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, path);
            if (buildIndex >= 0)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                list.RemoveAll(s => s.path == path);
                list.Insert(0, new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Kezor] Cena salva em {path}. Aperte Play.");
        }
    }
}
