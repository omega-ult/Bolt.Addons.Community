using System.Collections;
using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Unity.VisualScripting.Community
{
    public class UnitSearchWindow : EditorWindow
    {
        // public static Event e;
        enum MatchType
        {
            Type,
            Method,
            Field,
            Reference,
        }

        enum EntrySource
        {
            GraphAsset,
            SceneEmbedded,
            PrefabEmbedded
        }

        class UnitContainer
        {
            public string ScenePath;
            public string AssetPath;
            public EntrySource Source;
            public List<MatchUnit> Units;
        }

        class MatchUnit
        {
            public List<MatchType> Matches;
            public GraphReference Reference;
            public string ScenePath;
            public string AssetPath;
            public EntrySource Source;
            public string FullTypeName;
            public string MatchString;

            public IUnit Unit;
        }


        private string _filterContainer = "";
        private string _pattern = "";

        private bool _caseSensitive = true;
        private bool _wordMatch = true;
        private bool _matchType = true;
        private bool _matchMethod = true;
        private bool _matchField = true;

        private bool _matchReference = true;

        // 添加搜索选项
        private bool _searchInGraphAssets = true;
        private bool _searchInPrefabs = true;
        private bool _searchInScenes = true;
        private bool _isSearching = false;
        private float _searchProgress = 0f;
        private string _searchStatus = "";

        private static Dictionary<string, List<UnitContainer>> _unitContainerMap = new();


        // scroll view position
        private Vector2 _scrollViewRoot;
        private static Color _graphColor = new Color(0.5f, 0.8f, 0.6f);
        private static Color _prefabColor = new Color(0.2f, 0.7f, 0.9f);
        private static Color _sceneColor = new Color(0.7f, 0.7f, 0.7f);


        [MenuItem("Window/UVS Community/Search Unit")]
        public static void Open()
        {
            var window = GetWindow<UnitSearchWindow>();
            window.titleContent = new GUIContent("Search Unit");
        }


        void ClearResult()
        {
            _unitContainerMap.Clear();
        }

        private void OnGUI()
        {
            // e = Event.current;
            Event e = Event.current;
            if (e.keyCode == KeyCode.Return)
            {
                if (_pattern.Length > 1 && _pattern.Trim().Length > 1)
                {
                    Search();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Find", GUILayout.ExpandWidth(false));
            _pattern = GUILayout.TextField(_pattern);
            GUILayout.Label("In", GUILayout.ExpandWidth(false));
            _filterContainer = GUILayout.TextField(_filterContainer);

            if (_pattern.Length > 1 && _pattern.Trim().Length > 1)
            {
                if (GUILayout.Button("Search", GUILayout.ExpandWidth(false)))
                {
                    // GraphSearch();
                    Search();
                }
            }
            else
            {
                if (GUILayout.Button("Clean", GUILayout.ExpandWidth(false)))
                {
                    ClearResult();
                }
            }

            GUILayout.EndHorizontal();

            // 添加搜索选项
            GUILayout.BeginHorizontal();
            _caseSensitive = GUILayout.Toggle(_caseSensitive, "Case", GUILayout.ExpandWidth(false));
            _wordMatch = GUILayout.Toggle(_wordMatch, "Word", GUILayout.ExpandWidth(false));
            GUILayout.Label("|", GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.Label("|", GUILayout.ExpandWidth(false));
            _searchInGraphAssets = GUILayout.Toggle(_searchInGraphAssets, "Graph Assets", GUILayout.ExpandWidth(false));
            _searchInPrefabs = GUILayout.Toggle(_searchInPrefabs, "Prefabs", GUILayout.ExpandWidth(false));
            _searchInScenes = GUILayout.Toggle(_searchInScenes, "Scenes", GUILayout.ExpandWidth(false));
            GUILayout.Label("|", GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.Label("|", GUILayout.ExpandWidth(false));
            _matchType = GUILayout.Toggle(_matchType, "Type", GUILayout.ExpandWidth(false));
            _matchMethod = GUILayout.Toggle(_matchMethod, "Method", GUILayout.ExpandWidth(false));
            _matchField = GUILayout.Toggle(_matchField, "Field", GUILayout.ExpandWidth(false));
            _matchReference = GUILayout.Toggle(_matchReference, "Reference", GUILayout.ExpandWidth(false));
            // _showGraph = GUILayout.Toggle(_showGraph, "Graph", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            // 显示搜索进度
            if (_isSearching)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), _searchProgress, _searchStatus);
            }

            GUILayout.BeginVertical("box");
            _scrollViewRoot = GUILayout.BeginScrollView(_scrollViewRoot);

            Regex matchFile = null;
            if (_filterContainer.Length != 0)
                matchFile = new Regex(_filterContainer, RegexOptions.IgnoreCase);
            var containers = FilterContainer(matchFile).ToArray();
            foreach (var container in containers)
            {
                DrawContainer(container);
            }


            var empty = containers.Length == 0;
            if (empty)
            {
                GUILayout.Label("No result found.");
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        IEnumerable<UnitContainer> FilterContainer(Regex matchContainer)
        {
            foreach (var (key, containers) in _unitContainerMap)
            {
                if (matchContainer != null && !matchContainer.IsMatch(key))
                    continue;
                foreach (var container in containers)
                {
                    yield return container;
                }
            }
        }

        void DrawContainer(UnitContainer container)
        {
            GUILayout.BeginHorizontal();
            EditorGUIUtility.SetIconSize(new Vector2(16, 16));
            switch (container.Source)
            {
                case EntrySource.GraphAsset:
                    GUI.color = _graphColor;
                    var graph = AssetDatabase.LoadMainAssetAtPath(container.AssetPath);
                    var graphIcon = EditorGUIUtility.ObjectContent(graph, graph.GetType());
                    graphIcon.text = container.AssetPath;
                    if (GUILayout.Button(graphIcon, GUILayout.ExpandWidth(false)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(container.AssetPath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                        }
                    }

                    GUI.color = Color.white;
                    break;
                case EntrySource.PrefabEmbedded:
                    GUI.color = _prefabColor;
                    var prefab = AssetDatabase.LoadMainAssetAtPath(container.AssetPath);
                    var prefabIcon = EditorGUIUtility.ObjectContent(prefab, prefab.GetType());
                    prefabIcon.text = container.AssetPath;
                    if (GUILayout.Button(prefabIcon, GUILayout.ExpandWidth(false)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(container.AssetPath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                        }
                    }

                    GUI.color = Color.white;
                    break;
                case EntrySource.SceneEmbedded:
                    GUI.color = _sceneColor;
                    var scene = AssetDatabase.LoadMainAssetAtPath(container.ScenePath);
                    var sceneIcon = EditorGUIUtility.ObjectContent(scene, scene.GetType());
                    sceneIcon.text = container.ScenePath;
                    if (GUILayout.Button(sceneIcon, GUILayout.ExpandWidth(false)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(container.ScenePath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                        }
                    }

                    GUI.color = Color.white;
                    break;
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            foreach (var unit in container.Units)
            {
                var label = $"  {unit.FullTypeName} : {unit.MatchString}";
                if (GUILayout.Button(label, EditorStyles.linkLabel))
                {
                    UnitUtility.FocusUnit(unit.Reference, unit.Unit);
                }
            }

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }


        private void Search()
        {
            ClearResult();
            if (!(_pattern.Length > 1 && _pattern.Trim().Length > 1))
            {
                return;
            }

            _isSearching = true;
            _searchProgress = 0f;
            _searchStatus = "正在准备搜索...";

            var pattern = _pattern;
            if (_wordMatch)
                pattern = $@"\b{Regex.Escape(pattern)}\b";

            var matchWord = new Regex(pattern, _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

            // 使用 EditorApplication.delayCall 异步执行搜索
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // 搜索 Graph Assets
                    if (_searchInGraphAssets)
                    {
                        SearchInGraphAssets(matchWord);
                    }

                    // 搜索 Prefabs
                    if (_searchInPrefabs)
                    {
                        SearchInPrefabs(matchWord);
                    }

                    // 搜索 Scenes
                    if (_searchInScenes)
                    {
                        SearchInScenes(matchWord);
                    }
                }
                finally
                {
                    _isSearching = false;
                    _searchStatus = "搜索完成";
                    Repaint();
                }
            };
            // remove duplication
            var globalSeen = new HashSet<string>(); // 全局HashSet
            foreach (var key in _unitContainerMap.Keys)
            {
                var unitList = _unitContainerMap[key];
                if (unitList is not { Count: > 0 }) continue;
                foreach (var container in unitList.Where(container => container.Units != null))
                {
                    container.Units = container.Units
                        .GroupBy(u => u.Unit?.ToString())
                        .Select(g => g.First())
                        .Where(u =>
                        {
                            var keyStr = u.Unit?.ToString();
                            if (keyStr == null) return false;
                            return globalSeen.Add(keyStr); // 使用全局HashSet
                        })
                        .ToList();
                }
            }
        }

        private void SearchInGraphAssets(Regex matchWord)
        {
            _searchStatus = "正在搜索 Graph Assets...";
            // 搜索 ScriptGraphAsset
            var guids = AssetDatabase.FindAssets("t:ScriptGraphAsset", null);

            for (var i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                _searchProgress = (float)i / (guids.Length * 2); // 考虑到还有 StateGraphAsset
                _searchStatus = $"正在搜索 ScriptGraphAsset... ({i + 1}/{guids.Length})";

                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
                if (asset == null || asset.GetReference().graph is not FlowGraph flowGraph) continue;

                var unitContainer = new UnitContainer()
                {
                    ScenePath = "",
                    AssetPath = assetPath,
                    Source = EntrySource.GraphAsset,
                    Units = UnitUtility.TraverseFlowGraphUnit(asset.GetReference().AsReference())
                        .Select(x => CheckMatchUnit(matchWord, x.Item1, x.Item2))
                        .Where(x => x != null).ToList()
                };
                if (unitContainer.Units.Count > 0)
                {
                    if (_unitContainerMap.TryGetValue(assetPath, out var list))
                    {
                        list.Add(unitContainer);
                    }
                    else
                    {
                        _unitContainerMap[assetPath] = new List<UnitContainer>() { unitContainer };
                    }
                }

                // 定期刷新窗口以显示进度
                if (i % 10 == 0) Repaint();
            }

            // 搜索 StateGraphAsset
            guids = AssetDatabase.FindAssets("t:StateGraphAsset", null);
            for (int i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                _searchProgress = 0.5f + (float)i / (guids.Length * 2); // 从 0.5 开始
                _searchStatus = $"正在搜索 StateGraphAsset... ({i + 1}/{guids.Length})";

                // 这里添加 StateGraphAsset 的搜索逻辑，与原代码类似
                // ... 省略 StateGraphAsset 搜索逻辑 ...

                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
                if (asset == null || asset.GetReference().graph is not StateGraph stateGraph) continue;

                var unitContainer = new UnitContainer()
                {
                    ScenePath = "",
                    AssetPath = assetPath,
                    Source = EntrySource.GraphAsset,
                    Units = UnitUtility.TraverseStateGraphUnit(asset.GetReference().AsReference())
                        .Select(x => CheckMatchUnit(matchWord, x.Item1, x.Item2))
                        .Where(x => x != null).ToList()
                };
                if (unitContainer.Units.Count > 0)
                {
                    if (_unitContainerMap.TryGetValue(assetPath, out var list))
                    {
                        list.Add(unitContainer);
                    }
                    else
                    {
                        _unitContainerMap[assetPath] = new List<UnitContainer>() { unitContainer };
                    }
                }

                // 定期刷新窗口以显示进度
                if (i % 10 == 0) Repaint();
            }
        }

        void ProcessGameObject(string scenePath, string assetPath, EntrySource source, GameObject gameObject,
            Regex matchWord)
        {
            // 搜索 ScriptMachine
            var scriptMachines = gameObject.GetComponentsInChildren<ScriptMachine>(true);
            foreach (var machine in scriptMachines)
            {
                if (machine.graph == null || machine.nest?.source == GraphSource.Macro) continue;
                var reference = GraphReference.New(machine, false);
                // 处理 FlowGraph
                var container = new UnitContainer()
                {
                    ScenePath = scenePath,
                    AssetPath = assetPath,
                    Source = source,
                    Units = UnitUtility.TraverseFlowGraphUnit(reference)
                        .Select(x => CheckMatchUnit(matchWord, x.Item1, x.Item2))
                        .Where(x => x != null).ToList()
                };
                if (container.Units.Count <= 0) continue;
                var storeKey = container.AssetPath;
                if (source == EntrySource.SceneEmbedded)
                {
                    storeKey = container.ScenePath;
                }

                if (_unitContainerMap.TryGetValue(storeKey, out var list))
                {
                    list.Add(container);
                }
                else
                {
                    _unitContainerMap[storeKey] = new List<UnitContainer>() { container };
                }
            }

            // 搜索 StateMachine
            var stateMachines = gameObject.GetComponentsInChildren<StateMachine>(true);
            foreach (var machine in stateMachines)
            {
                if (machine.graph == null || machine.nest?.source == GraphSource.Macro) continue;
                var reference = GraphReference.New(machine, false);

                // 处理 FlowGraph
                var container = new UnitContainer()
                {
                    ScenePath = scenePath,
                    AssetPath = assetPath,
                    Source = source,
                    Units = UnitUtility.TraverseStateGraphUnit(reference)
                        .Select(x => CheckMatchUnit(matchWord, x.Item1, x.Item2))
                        .Where(x => x != null).ToList()
                };
                if (container.Units.Count <= 0) continue;
                var storeKey = container.AssetPath;
                if (source == EntrySource.SceneEmbedded)
                {
                    storeKey = container.ScenePath;
                }

                if (_unitContainerMap.TryGetValue(storeKey, out var list))
                {
                    list.Add(container);
                }
                else
                {
                    _unitContainerMap[storeKey] = new List<UnitContainer>() { container };
                }
            }
        }

        private void SearchInPrefabs(Regex matchWord)
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
                ProcessGameObject("", assetPath, EntrySource.PrefabEmbedded, prefab, matchWord);
                // 定期刷新窗口以显示进度
                if (i % 10 == 0) Repaint();
            }
        }


        private void SearchInScenes(Regex matchWord)
        {
            _searchStatus = "正在搜索 Scenes...";
            var scenes = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            for (var i = 0; i < scenes.Length; i++)
            {
                var scenePath = scenes[i];
                _searchProgress = (float)i / scenes.Length;
                _searchStatus = $"正在搜索 Scenes... ({i + 1}/{scenes.Length})";

                // 我们不实际加载场景，只检查它是否已经加载
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
                if (scene.isLoaded)
                {
                    // 搜索 ScriptMachine
                    foreach (var gameObject in scene.GetRootGameObjects())
                    {
                        ProcessGameObject(scenePath, "", EntrySource.SceneEmbedded, gameObject, matchWord);
                    }
                }

                // 定期刷新窗口以显示进度
                if (i % 10 == 0) Repaint();
            }
        }

        MatchUnit CheckMatchUnit(Regex matchWord, GraphReference reference, Unit unit)
        {
            var matchRecord = new MatchUnit()
            {
                Matches = new List<MatchType>(),
                Reference = reference,
                Unit = unit
            };

            // brutal force to create, this can be optimized, but when?
            // match type name.
            var typeName = unit.GetType().ToString().Split(".").Last();
            if (unit is InvokeMember invoker)
            {
                typeName = invoker.invocation.targetTypeName;
                if (matchWord.IsMatch(typeName))
                {
                    matchRecord.Matches.Add(MatchType.Type);
                    matchRecord.MatchString = typeName;
                }

                if (matchWord.IsMatch(invoker.invocation.name))
                {
                    matchRecord.Matches.Add(MatchType.Method);
                    matchRecord.MatchString = invoker.invocation.name;
                }
            }

            matchRecord.FullTypeName = typeName;

            // fit fields
            var fields = unit.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var fitField = false;

            // try read serialized members
            foreach (var kvp in unit.defaultValues)
            {
                var value = kvp.Value;
                if (value == null) continue;
                var serializedValue = value.ToString();
                if (!matchWord.IsMatch(serializedValue)) continue;
                matchRecord.Matches.Add(MatchType.Field);
                matchRecord.MatchString = serializedValue;
                fitField = true;
                break;
            }

            // try read class member
            if (!fitField)
            {
                foreach (var field in fields)
                {
                    var value = field.GetValue(unit);
                    if (value == null) continue;
                    var serializedValue = value.ToString();
                    if (!matchWord.IsMatch(serializedValue)) continue;
                    matchRecord.Matches.Add(MatchType.Field);
                    matchRecord.MatchString = serializedValue;
                    fitField = true;
                    break;
                }
            }

            if (!fitField)
            {
                if (unit is GetMember getMember)
                {
                    if (matchWord.IsMatch(getMember.member.name))
                    {
                        matchRecord.Matches.Add(MatchType.Field);
                        matchRecord.MatchString = getMember.member.name;
                        fitField = true;
                    }
                }
            }

            if (!fitField)
            {
                if (unit is SetMember setMember)
                {
                    if (matchWord.IsMatch(setMember.member.name))
                    {
                        matchRecord.Matches.Add(MatchType.Field);
                        matchRecord.MatchString = setMember.member.name;
                        fitField = true;
                    }
                }
            }

            if (!fitField)
            {
                switch (unit)
                {
                    case SwitchOnString switchOnString:
                    {
                        foreach (var option in switchOnString.options.Where(option => matchWord.IsMatch(option)))
                        {
                            matchRecord.Matches.Add(MatchType.Field);
                            matchRecord.MatchString = option;
                        }

                        break;
                    }
                    case SwitchOnInteger switchOnInteger:
                    {
                        foreach (var option in
                                 switchOnInteger.options.Where(option => matchWord.IsMatch(option.ToString())))
                        {
                            matchRecord.Matches.Add(MatchType.Field);
                            matchRecord.MatchString = option.ToString();
                        }

                        break;
                    }
                    case SwitchOnEnum { enumType: not null } switchOnEnum:
                    {
                        foreach (var option in switchOnEnum.enumType.GetEnumValues())
                        {
                            if (!matchWord.IsMatch(option.ToString())) continue;
                            matchRecord.Matches.Add(MatchType.Field);
                            matchRecord.MatchString = option.ToString();
                        }

                        break;
                    }
                    case UnifiedVariableUnit { defaultValues: not null } unifiedVariableUnit:
                    {
                        var str = (string)unifiedVariableUnit.defaultValues[nameof(unifiedVariableUnit.name)];
                        if (matchWord.IsMatch(str))
                        {
                            matchRecord.Matches.Add(MatchType.Field);
                            matchRecord.MatchString = str;
                        }

                        break;
                    }
                }
            }

            if (unit is StateUnit or SubgraphUnit)
            {
                var assetPath = "";
                switch (unit)
                {
                    case StateUnit stateUnit:
                    {
                        if (stateUnit.nest.source == GraphSource.Macro)
                        {
                            var childReference = reference.ChildReference(stateUnit, false);
                            var context = UnitUtility.GetEntryContext(childReference);
                            assetPath = context.assetPath;
                        }
                        break;
                    }
                    case SubgraphUnit subgraphUnit:
                        if (subgraphUnit.nest.source == GraphSource.Macro)
                        {
                            
                            var childReference = reference.ChildReference(subgraphUnit, false);
                            var context = UnitUtility.GetEntryContext(childReference);
                            assetPath = context.assetPath;
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(assetPath) && matchWord.IsMatch(assetPath))
                {
                    matchRecord.Unit = unit;
                    matchRecord.Matches.Add(MatchType.Reference);
                    matchRecord.MatchString = assetPath;
                }
            }

            foreach (var kvp in unit.defaultValues)
            {
                if (kvp.Value is not Object obj) continue;
                if (!AssetDatabase.Contains(obj)) continue;
                var aPath = AssetDatabase.GetAssetPath(obj);
                if (!matchWord.IsMatch(aPath)) continue;

                matchRecord.Matches.Add(MatchType.Reference);
                matchRecord.MatchString = aPath;
                break;
            }

            return matchRecord.Matches.Count > 0 ? matchRecord : null;
        }

        bool ShouldShowItem(IEnumerable<MatchUnit> list)
        {
            foreach (var match in list)
            {
                if (_matchType && match.Matches.Contains(MatchType.Type))
                {
                    return true;
                }

                if (_matchMethod && match.Matches.Contains(MatchType.Method))
                {
                    return true;
                }

                if (_matchField && match.Matches.Contains(MatchType.Field))
                {
                    return true;
                }

                if (_matchReference && match.Matches.Contains(MatchType.Reference))
                {
                    return true;
                }
            }

            return false;
        }
    }
}