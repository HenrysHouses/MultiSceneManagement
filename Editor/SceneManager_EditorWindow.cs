// *   Multi Scene Tools For Unity
// *
// *   Copyright (C) 2023 Henrik Hustoft
// *
// *   Licensed under the Apache License, Version 2.0 (the "License");
// *   you may not use this file except in compliance with the License.
// *   You may obtain a copy of the License at
// *
// *       http://www.apache.org/licenses/LICENSE-2.0
// *
// *   Unless required by applicable law or agreed to in writing, software
// *   distributed under the License is distributed on an "AS IS" BASIS,
// *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// *   See the License for the specific language governing permissions and
// *   limitations under the License.

using System.Reflection;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using HH.MultiSceneTools;

namespace HH.MultiSceneToolsEditor
{
    [InitializeOnLoadAttribute]
    public class SceneManager_EditorWindow : EditorWindow
    {
        public static SceneManager_EditorWindow Instance;
        string[] page = new string[]{"Tools", "Info"};
        int pageIndex;
        Scene[] LoadedScenes;
        string[] _sceneOptions;
        SceneAsset _SelectedScene;
        SceneAsset[] currLoadedAssets;
        string[] loadedSceneOptions;
        SceneCollection[] _Collection;
        string[] Collection = new string[0];
        public SceneCollection GetLoadedCollection()
        {
            if(MultiSceneToolsConfig.instance)
                return MultiSceneToolsConfig.instance.currentLoadedCollection;  
            else
                return null;
        } 
        [SerializeField, HideInInspector] public SceneCollection SelectedCollection;
        int UnloadScene;

        SceneManager_EditorWindow()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;
            Instance = this;
        }

        [MenuItem("Tools/Multi Scene Tools/Manager", false, 1)]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            SceneManager_EditorWindow window = (SceneManager_EditorWindow)EditorWindow.GetWindow(typeof(SceneManager_EditorWindow));
            window.titleContent = new GUIContent("Multi Scene Manager", "Loads, Unloads, and Saves Scene Collections");
            window.Show();
        }

        protected void OnEnable ()
        {
            // Here we retrieve the data if it exists or we save the default field initialisers we set above
            var data = EditorPrefs.GetString("MultiSceneManagerWindow", JsonUtility.ToJson(this, false));
            // Then we apply them to this window
            JsonUtility.FromJsonOverwrite(data, this);
        }
    
        protected void OnDisable ()
        {
            // We get the Json data
            var data = JsonUtility.ToJson(this, false);
            // And we save it
            EditorPrefs.SetString("MultiSceneManagerWindow", data);
        }

        private void LogPlayModeState(PlayModeStateChange state)
        {
            if(PlayModeStateChange.ExitingEditMode.Equals(state))
            {
                if(SelectedCollection)
                    MultiSceneToolsConfig.instance.setCurrCollection(SelectedCollection);
            }
        }

        void OnGUI()
        {
            pageIndex = GUILayout.Toolbar(pageIndex, page);
            DrawInfo();

            // Draw Info Tab
            if(pageIndex == 1)
            {
                // Project Settings
                GUILayout.Space(8);
                GUILayout.Label("Project Settings", EditorStyles.boldLabel);

                if(GUILayout.Button("Add Open Scenes To Build"))
                {
                    SetEditorBuildSettingsScenes();
                }


                if(GUILayout.Button("Find All Collections"))
                {
                    // Get type
                    var editorAssembly = typeof(EditorWindow).Assembly;
                    System.Type projectBrowserType = editorAssembly.GetType("UnityEditor.ProjectBrowser");

                    // Get window instance
                    var WindowInstanceInfo = projectBrowserType.GetField("s_LastInteractedProjectBrowser");
                    // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/ProjectBrowser.cs#L53

                    // Call method
                    MethodInfo setSearchInfo = projectBrowserType.GetMethod("SetSearch", new [] {typeof(string)});
                    // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/ProjectBrowser.cs#L534
                    setSearchInfo.Invoke(WindowInstanceInfo.GetValue(projectBrowserType), new object[]{"t:SceneCollection"});
                }

                if(GUILayout.Button("Reload Project Collections"))
                {
                    MultiSceneToolsConfig.instance.UpdateCollections();
                }
                return;
            }

            // Draw Tools Tab

            // Load Collection
            GUILayout.Space(8);
            GUILayout.Label("Loading", EditorStyles.boldLabel);


            SelectedCollection = (SceneCollection)EditorGUILayout.ObjectField(new GUIContent("Collection"), SelectedCollection, typeof(SceneCollection), false);
            
            if(!SelectedCollection)
                GUI.enabled = false;
            if(GUILayout.Button("Load Collection"))
            {
                EditorLoadCollection();
            }
            if(!SelectedCollection)
                GUI.enabled = true;

            
            // Load Scene
            loadBuildScenesAsOptions();

            DrawFieldSelectLoadAdditive();


            if(!_SelectedScene)
                GUI.enabled = false;
            if(GUILayout.Button("Load Scene Additively"))
            {
                LoadSceneAdditively();
            }
            if(!_SelectedScene)
                GUI.enabled = true;

            // Un-Load selected scene
            DrawPopupSelectUnload();

            if(UnloadScene == 0)
                GUI.enabled = false;
            if(GUILayout.Button("Unload Scene"))
            {
                UnLoadSelectedScene();
            }
            if(UnloadScene == 0)
                GUI.enabled = true;

            // Asset Management
            GUILayout.Space(8);
            GUILayout.Label("Asset Management", EditorStyles.boldLabel);

            if(GUILayout.Button("Save Collection"))
            {
                SaveCollection(GetLoadedCollection());
            }

            if(GUILayout.Button("Create Collection From Loaded Scenes"))
            {
                CreateCollection();
            }

            if(GUILayout.Button("Create New Scene"))
            {
                CreateNewScene();
            }
        }

        // Other Draw Functions
        void DrawInfo()
        {
            var collection = GetLoadedCollection();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Current Loaded Collection:", collection, typeof(SceneCollection), false);
            GUI.enabled = true;

            if(collection)
                EditorGUILayout.TextField("Title:", collection.Title, EditorStyles.boldLabel);
            else
                EditorGUILayout.TextField("Title:", "None", EditorStyles.boldLabel);

            if(pageIndex == 0)
                return;

            GUILayout.Space(8);
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            GUI.enabled = false;
            EditorGUILayout.ObjectField(new GUIContent("Config"), MultiSceneToolsConfig.instance, typeof(MultiSceneToolsConfig), false);

            EditorGUILayout.Toggle("Start Wizard On Update", MultiSceneToolsConfig.instance.startWizardOnUpdate);
            EditorGUILayout.Toggle("Allow References", !EditorSceneManager.preventCrossSceneReferences);
            EditorGUILayout.Toggle("Log Scene Changes", MultiSceneToolsConfig.instance.LogOnSceneChange);
            EditorGUILayout.Toggle("Keep Boot-Scene", MultiSceneToolsConfig.instance.UseBootScene);
            EditorGUILayout.ObjectField("Target Boot Scene", MultiSceneToolsConfig.instance._TargetBootScene, typeof(SceneAsset), false);
            EditorGUILayout.TextField("Boot Scene Path", MultiSceneToolsConfig.instance._BootScenePath);
            EditorGUILayout.TextField("Scene Collection Path", MultiSceneToolsConfig.instance._SceneCollectionPath);
            GUI.enabled = true;

        }


        // Draw Popup functions
        void DrawFieldSelectLoadAdditive()
        {
            _SelectedScene = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent("Scene"), _SelectedScene, typeof(SceneAsset), true);
        }

        void DrawPopupSelectUnload()
        {
            if(loadedSceneOptions != null)
            {
                if(loadedSceneOptions.Length > 0)
                    UnloadScene = EditorGUILayout.Popup("Un-Load" ,UnloadScene, loadedSceneOptions);
            }
            else
                EditorGUILayout.Popup(0, new string[]{"Unload Select"});
        }

        // Button functions
        public void EditorLoadCollection()
        {
            if(SelectedCollection == null)
            {
                EditorSceneManager.OpenScene("Assets/~Scenes/SampleScene.unity", OpenSceneMode.Single);
                MultiSceneToolsConfig.instance.setCurrCollection(null);
                return;
            }

            SelectedCollection.LoadCollection();
            EditorUtility.FocusProjectWindow();

            // Selection.activeObject = SelectedCollection;
        }

        void LoadSceneAdditively()
        {
            string path = AssetDatabase.GetAssetPath(_SelectedScene);

            EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        void UnLoadSelectedScene()
        {
            if(EditorSceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(LoadedScenes[UnloadScene], true);
            else
                EditorSceneManager.OpenScene(MultiSceneToolsConfig.instance._BootScenePath, OpenSceneMode.Single);
        }

        void SaveCollection(SceneCollection saveTarget)
        {
            if(saveTarget)
            {
                currLoadedAssets = GetSceneAssetsFromPaths(GetLoadedScenePaths());
                saveTarget.saveCollection(currLoadedAssets);
            }
            else
                Debug.LogWarning("SceneManager: save target was null");
        }

        void CreateCollection()
        {
            string path = MultiSceneToolsConfig.instance._SceneCollectionPath;
            if(!Directory.Exists(path)) 
                    Directory.CreateDirectory(path);

            SceneCollection _NewCollection = SceneCollection.CreateSceneCollection();

            SaveCollection(_NewCollection);
            AssetDatabase.SaveAssets();

            MultiSceneToolsConfig.instance.setCurrCollection(_NewCollection);
            
            EditorUtility.FocusProjectWindow();

            Selection.activeObject = _NewCollection;
        }
        
        public void SetEditorBuildSettingsScenes()
        {
            // Find valid Scene paths and make a list of EditorBuildSettingsScene
            List<EditorBuildSettingsScene> editorBuildSettingsScenes = new List<EditorBuildSettingsScene>();
            int _sceneCount = SceneManager.sceneCountInBuildSettings;     

            for (int i = 0; i < _sceneCount; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                if (!string.IsNullOrEmpty(scenePath))
                    editorBuildSettingsScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene _addScene = EditorSceneManager.GetSceneAt(i);
                bool found = false;

                foreach (EditorBuildSettingsScene item in editorBuildSettingsScenes)
                {
                    if(item.path.Equals(_addScene.path))
                    {
                        found = true;
                        break;
                    }
                }
                if(!found)
                {
                    editorBuildSettingsScenes.Add(new EditorBuildSettingsScene(_addScene.path, true));            
                    Debug.Log("Added scene: " + _addScene.name + " to build settings");
                }
            }

            // Set the Build Settings window Scene list
            EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
        }

        void CreateNewScene()
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveOpenScenes();

            SetEditorBuildSettingsScenes();
        }

        // Helper functions
        void OnInspectorUpdate()
        {


            // Storing loaded scenes
            int sceneCount = EditorSceneManager.sceneCount;     

            LoadedScenes = new Scene[sceneCount+1];
            loadedSceneOptions = new string[sceneCount+1];
            loadedSceneOptions[0] = "Select";
            for (int i = 0; i < sceneCount; i++)
            {
                LoadedScenes[i+1] = EditorSceneManager.GetSceneAt(i);
                loadedSceneOptions[i+1] = LoadedScenes[i+1].name;
            }
        }

        SceneAsset[] GetSceneAssetsFromPaths(string[] ScenePaths)
        {
            int buildSceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;     
            SceneAsset[] _assets = new SceneAsset[ScenePaths.Length];

            for (int i = 0; i < ScenePaths.Length; i++)
            {
                _assets[i] = (SceneAsset)AssetDatabase.LoadAssetAtPath(ScenePaths[i], typeof(SceneAsset));
            }
            return _assets;
        }

        string[] GetLoadedScenePaths()
        {
            Scene[] _currScenes = new Scene[EditorSceneManager.loadedSceneCount];
            for (int i = 0; i < _currScenes.Length; i++)
            {
                _currScenes[i] = EditorSceneManager.GetSceneAt(i);
            }

            string[] _paths = new string[_currScenes.Length];

            for (int i = 0; i < _paths.Length; i++)
            {
                _paths[i] = _currScenes[i].path;
            }
            return _paths;
        }

        // string GetScenePath(string scene)
        // {
        //     int buildSceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;     
        //     for (int i = 0; i < buildSceneCount; i++)
        //     {
        //         string currScene = System.IO.Path.GetFileNameWithoutExtension( UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex( i ) );
        //         if(currScene.Equals(scene))
        //         {
        //             return UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex( i );
        //         }
        //     }
        //     return null;
        // }

        void loadBuildScenesAsOptions()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;     

            if(_sceneOptions == null)
                _sceneOptions = new string[0];

            if(sceneCount > _sceneOptions.Length)
            {
                string[] scenes = new string[sceneCount];

                for( int i = 0; i < sceneCount; i++ )
                {
                    scenes[i] = System.IO.Path.GetFileNameWithoutExtension( UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex( i ) );
                }
                _sceneOptions = scenes;
            }
        }
    }
}
