using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using Object = UnityEngine.Object;

namespace Unity.VisualScripting.Community
{
    public class UnitDefinitionExceptionWindow : EditorWindow
    {

        [MenuItem("Window/UVS Community/Definition Exception Finder")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnitDefinitionExceptionWindow>("Definition Exception Finder");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private string _searchFilter = "";
        private string _filterText = "";
        private Vector2 _scrollPosition;
        private bool _searchInGraphAssets = true;
        private bool _searchInPrefabs = true;
        private bool _searchInScenes = true;
        private List<UnitExceptionInfo> _exceptionInfos = new List<UnitExceptionInfo>();
        private List<UnitExceptionInfo> _filteredExceptionInfos = new List<UnitExceptionInfo>();
        private bool _isSearching = false;
        private float _searchProgress = 0f;
        private string _searchStatus = "";
        private bool _isFiltering = false;

        private class UnitExceptionInfo
        {
            public string UnitName { get; set; }
            public string ExceptionMessage { get; set; }
            // public string StackTrace { get; set; }
            public string AssetPath { get; set; }
            public string GraphPath { get; set; }
            public GraphReference Reference { get; set; }
            public Unit Unit { get; set; }
            public bool IsExpanded { get; set; }
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();

            // Search options
            GUILayout.Label("搜索选项", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _searchInGraphAssets = EditorGUILayout.ToggleLeft("Graph Assets", _searchInGraphAssets, GUILayout.Width(120));
            _searchInPrefabs = EditorGUILayout.ToggleLeft("Prefabs", _searchInPrefabs, GUILayout.Width(120));
            _searchInScenes = EditorGUILayout.ToggleLeft("Scenes", _searchInScenes, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            // Search button
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全盘搜索", GUILayout.Width(100)))
            {
                _searchFilter = ""; // 清空搜索过滤条件
                StartSearch();
            }
            EditorGUILayout.EndHorizontal();

            // Progress bar
            if (_isSearching)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), _searchProgress, _searchStatus);
            }

            // Filter section
            if (_exceptionInfos.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("结果过滤", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                
                // 保存当前的过滤文本
                string previousFilterText = _filterText;
                
                // 过滤文本输入框
                _filterText = EditorGUILayout.TextField(_filterText);
                
                // 如果过滤文本发生变化，应用过滤
                if (previousFilterText != _filterText)
                {
                    ApplyFilter();
                }
                
                // 清除过滤按钮
                if (GUILayout.Button("清除过滤", GUILayout.Width(100)))
                {
                    _filterText = "";
                    ApplyFilter();
                }
                EditorGUILayout.EndHorizontal();
            }

            // Results
            GUILayout.Space(10);
            var displayList = string.IsNullOrEmpty(_filterText) ? _exceptionInfos : _filteredExceptionInfos;
            GUILayout.Label($"搜索结果 ({displayList.Count})", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var info in displayList)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);

                // Header with unit name and asset path
                EditorGUILayout.BeginHorizontal();
                info.IsExpanded = EditorGUILayout.Foldout(info.IsExpanded, $"{info.UnitName}", true);
                GUILayout.FlexibleSpace();
                GUILayout.Label(info.AssetPath, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                if (info.IsExpanded)
                {
                    // Graph path
                    EditorGUILayout.LabelField("Graph Path:", info.GraphPath);

                    // Exception message
                    GUILayout.Label("Exception:", EditorStyles.boldLabel);
                    EditorGUILayout.TextArea(info.ExceptionMessage, EditorStyles.wordWrappedLabel);

                    // Stack trace (optional)
                    // if (!string.IsNullOrEmpty(info.StackTrace))
                    // {
                    //     GUILayout.Label("Stack Trace:", EditorStyles.boldLabel);
                    //     EditorGUILayout.TextArea(info.StackTrace, EditorStyles.wordWrappedLabel);
                    // }

                    // Buttons
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Focus Unit", GUILayout.Width(100)))
                    {
                        FocusOnUnit(info);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void StartSearch()
        {
            _exceptionInfos.Clear();
            _isSearching = true;
            _searchProgress = 0f;
            _searchStatus = "正在准备搜索...";

            // Start the search process asynchronously
            EditorApplication.delayCall += () => {
                try
                {
                    // Search in Graph Assets
                    if (_searchInGraphAssets)
                    {
                        SearchInGraphAssets();
                    }

                    // Search in Prefabs
                    if (_searchInPrefabs)
                    {
                        SearchInPrefabs();
                    }

                    // Search in Scenes
                    if (_searchInScenes)
                    {
                        SearchInScenes();
                    }
                }
                finally
                {
                    _isSearching = false;
                    _searchStatus = "搜索完成";
                    Repaint();
                }
            };
        }

        private void SearchInGraphAssets()
        {
            _searchStatus = "正在搜索 Graph Assets...";
            var graphAssets = AssetDatabase.FindAssets("t:ScriptGraphAsset OR t:StateGraphAsset")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();

            for (int i = 0; i < graphAssets.Length; i++)
            {
                var assetPath = graphAssets[i];
                _searchProgress = (float)i / graphAssets.Length;
                _searchStatus = $"正在搜索 Graph Assets... ({i + 1}/{graphAssets.Length})";

                var scriptGraphAsset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
                if (scriptGraphAsset != null)
                {
                    ProcessScriptGraphAsset(scriptGraphAsset, assetPath);
                }
                var stateGraphAsset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
                if (stateGraphAsset != null)
                {
                    ProcessStateGraphAsset(stateGraphAsset, assetPath);
                }


                // Repaint the window periodically to show progress
                if (i % 10 == 0) Repaint();
            }
        }

        private void SearchInPrefabs()
        {
            _searchStatus = "正在搜索 Prefabs...";
            var prefabs = AssetDatabase.FindAssets("t:Prefab")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();

            for (int i = 0; i < prefabs.Length; i++)
            {
                var assetPath = prefabs[i];
                _searchProgress = (float)i / prefabs.Length;
                _searchStatus = $"正在搜索 Prefabs... ({i + 1}/{prefabs.Length})";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                var scriptMachines = prefab.GetComponentsInChildren<ScriptMachine>(true);
                foreach (var machine in scriptMachines)
                {
                    if (machine.graph == null) continue;
                    var reference = GraphReference.New(machine, false);
                    ProcessGraphReference(reference, assetPath);
                }

                var stateMachines = prefab.GetComponentsInChildren<StateMachine>(true);
                foreach (var machine in stateMachines)
                {
                    if (machine.graph == null) continue;
                    var reference = GraphReference.New(machine, false);
                    ProcessGraphReference(reference, assetPath);
                }

                // Repaint the window periodically to show progress
                if (i % 10 == 0) Repaint();
            }
        }

        private void SearchInScenes()
        {
            _searchStatus = "正在搜索 Scenes...";
            var scenes = AssetDatabase.FindAssets("t:Scene")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();

            for (int i = 0; i < scenes.Length; i++)
            {
                var assetPath = scenes[i];
                _searchProgress = (float)i / scenes.Length;
                _searchStatus = $"正在搜索 Scenes... ({i + 1}/{scenes.Length})";

                // We don't actually load the scene, just check if it's already loaded
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(assetPath);
                if (scene.isLoaded)
                {
                    var scriptMachines = Object.FindObjectsOfType<ScriptMachine>();
                    foreach (var machine in scriptMachines)
                    {
                        if (machine.graph == null) continue;
                        var reference = GraphReference.New(machine, false);
                        ProcessGraphReference(reference, assetPath);
                    }

                    var stateMachines = Object.FindObjectsOfType<StateMachine>();
                    foreach (var machine in stateMachines)
                    {
                        if (machine.graph == null) continue;
                        var reference = GraphReference.New(machine, false);
                        ProcessGraphReference(reference, assetPath);
                    }
                }

                // Repaint the window periodically to show progress
                if (i % 10 == 0) Repaint();
            }
        }

        private void ProcessScriptGraphAsset(ScriptGraphAsset asset, string assetPath)
        {
            try
            {
                var reference = GraphReference.New(asset, false);
                ProcessGraphReference(reference, assetPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing graph asset {assetPath}: {ex.Message}");
            }
        }
        private void ProcessStateGraphAsset(StateGraphAsset asset, string assetPath)
        {
            try
            {
                var reference = GraphReference.New(asset, false);
                ProcessGraphReference(reference, assetPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing graph asset {assetPath}: {ex.Message}");
            }
        }
        private void ProcessGraphReference(GraphReference reference, string assetPath)
        {
            // Process flow graph units
            foreach (var (unitRef, unit) in UnitUtility.TraverseFlowGraphUnit(reference))
            {
                CheckUnitForException(unit, unitRef, assetPath);
            }

            // Process state graph units
            foreach (var (unitRef, unit) in UnitUtility.TraverseStateGraphUnit(reference))
            {
                CheckUnitForException(unit, unitRef, assetPath);
            }
        }

        private void CheckUnitForException(Unit unit, GraphReference reference, string assetPath)
        {
            if (unit == null || unit.definitionException == null) return;

            var exceptionMessage = unit.definitionException.Message;
            // var stackTrace = unit.definitionException.StackTrace;

            var info = new UnitExceptionInfo
            {
                UnitName = unit.ToString(),
                ExceptionMessage = exceptionMessage,
                // StackTrace = stackTrace,
                AssetPath = assetPath,
                GraphPath = UnitUtility.GetGraphPath(reference),
                Reference = reference,
                Unit = unit,
                IsExpanded = false
            };

            _exceptionInfos.Add(info);
        }

        private void FocusOnUnit(UnitExceptionInfo info)
        {
            if (info.Reference != null && info.Unit != null)
            {
                UnitUtility.FocusUnit(info.Reference, info.Unit);
            }
        }

        private void ApplyFilter()
        {
            _filteredExceptionInfos.Clear();
            
            if (string.IsNullOrEmpty(_filterText))
            {
                // 如果过滤文本为空，不应用过滤
                return;
            }
            
            // 对搜索结果应用过滤
            var regex = new System.Text.RegularExpressions.Regex(_filterText, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (var info in _exceptionInfos)
            {
                if (regex.IsMatch(info.UnitName) ||
                    regex.IsMatch(info.ExceptionMessage) ||
                    // regex.IsMatch((info.StackTrackTrace.Contains(_filterText)) ||
                    regex.IsMatch(info.AssetPath) ||
                    info.GraphPath.Contains(_filterText))
                {
                    _filteredExceptionInfos.Add(info);
                }
            }
            
            Repaint();
        }
    }
}