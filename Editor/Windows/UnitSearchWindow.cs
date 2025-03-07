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
            Reference
        }

        class MatchUnit
        {
            public List<MatchType> Matches;
            public ScriptGraphAsset ScriptGraphAsset;
            public StateGraphAsset StateGraphAsset;
            public GraphReference Reference;
            public string FullTypeName;
            public string MatchString;
            public IUnit Unit;
            public string AssetName => ScriptGraphAsset != null ? ScriptGraphAsset.name : StateGraphAsset.name;
        }

        // only match reference
        class MatchGraph
        {
            public ScriptGraphAsset ScriptGraphAsset;
            public StateGraphAsset StateGraphAsset;
            public GraphReference Reference;
            public string MatchString;
            public Graph Graph;
            public string AssetName => ScriptGraphAsset != null ? ScriptGraphAsset.name : StateGraphAsset.name;
        }

        private string _filterGraph = "";
        private string _pattern = "";

        private bool _caseSensitive = true;
        private bool _wordMatch = true;
        private bool _matchType = true;
        private bool _matchMethod = true;
        private bool _matchField = true;
        private bool _matchReference = true;
        private bool _showGraph = true;
        private List<MatchUnit> _matchObjects = new();
        private List<MatchGraph> _matchGraph = new();
        private Dictionary<ScriptGraphAsset, List<MatchUnit>> _matchScriptGraphMap = new();
        private List<ScriptGraphAsset> _sortedScriptGraphKey = new();
        private Dictionary<StateGraphAsset, List<MatchUnit>> _matchStateGraphMap = new();
        private List<StateGraphAsset> _sortedStateGraphKey = new();


        // scroll view position
        private Vector2 _scrollViewRoot;


        [MenuItem("Window/UVS Community/Search Node")]
        public static void Open()
        {
            var window = GetWindow<UnitSearchWindow>();
            window.titleContent = new GUIContent("Search Node");
        }

        private void OnDisable()
        {
            ClearResult();
        }

        void ClearResult()
        {
            _matchObjects.Clear();
            _matchGraph.Clear();
            _matchScriptGraphMap.Clear();
            _sortedScriptGraphKey.Clear();
            _matchStateGraphMap.Clear();
            _sortedStateGraphKey.Clear();
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
                else
                {
                    ClearResult();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Find", GUILayout.ExpandWidth(false));
            _pattern = GUILayout.TextField(_pattern);
            GUILayout.Label("In", GUILayout.ExpandWidth(false));
            _filterGraph = GUILayout.TextField(_filterGraph);
            _caseSensitive = GUILayout.Toggle(_caseSensitive, "Case", GUILayout.ExpandWidth(false));
            _wordMatch = GUILayout.Toggle(_wordMatch, "Word", GUILayout.ExpandWidth(false));
            if (GUILayout.Button("Search", GUILayout.ExpandWidth(false)))
            {
                // GraphSearch();
                Search();
            }

            GUILayout.EndHorizontal();


            // find arguments.
            GUILayout.BeginHorizontal();
            _matchType = GUILayout.Toggle(_matchType, "Type", GUILayout.ExpandWidth(false));
            _matchMethod = GUILayout.Toggle(_matchMethod, "Method", GUILayout.ExpandWidth(false));
            _matchField = GUILayout.Toggle(_matchField, "Field", GUILayout.ExpandWidth(false));
            _matchReference = GUILayout.Toggle(_matchReference, "Reference", GUILayout.ExpandWidth(false));
            _showGraph = GUILayout.Toggle(_showGraph, "Graph", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical("box");
            _scrollViewRoot = GUILayout.BeginScrollView(_scrollViewRoot);

            Regex matchFile = null;
            if (_filterGraph.Length != 0)
                matchFile = new Regex(_filterGraph, RegexOptions.IgnoreCase);
            var empty = true;
            // for flow graph asset.
            foreach (var key in _sortedScriptGraphKey)
            {
                if (matchFile != null && !matchFile.IsMatch(key.name)) continue;
                var list = _matchScriptGraphMap[key];
                // check show items
                if (!ShouldShowItem(list)) continue;
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                var icon = EditorGUIUtility.ObjectContent(key, typeof(ScriptGraphAsset));
                GUILayout.Label(icon);
                // GUILayout.Label(key.name);
                foreach (var match in list)
                {
                    // if(match.Unit)
                    if (match.Unit.graph == null) continue;
                    var pathNames = GetUnitPath(match.Reference);
                    var label = $"      {pathNames}{match.FullTypeName} : {match.MatchString}";
                    var tex = Icons.Icon(match.Unit.GetType());
                    var unitIcon = new GUIContent(tex[IconSize.Small]) ;
                    unitIcon.text = label;
                    if (GUILayout.Button(unitIcon, EditorStyles.linkLabel))
                    {
                        FocusMatchUnit(match);
                    }
                }

                empty = false;
            }

            // for state graph asset.
            foreach (var key in _sortedStateGraphKey)
            {
                if (matchFile != null && !matchFile.IsMatch(key.name)) continue;
                var list = _matchStateGraphMap[key];
                // check show items
                if (!ShouldShowItem(list)) continue;
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                var icon = EditorGUIUtility.ObjectContent(key, typeof(ScriptGraphAsset));
                GUILayout.Label(icon);
                // GUILayout.Label(key.name);
                foreach (var match in list)
                {
                    // var parents = match.StateParents.Select(x => x == null ? "" : x.nest.graph.title).ToList();
                    // parents.Add(match.FlowGraph.title);
                    var pathNames = GetUnitPath(match.Reference);
                    var label = $"      {pathNames}{match.FullTypeName} : {match.MatchString}";
                    if (GUILayout.Button(label, EditorStyles.linkLabel))
                    {
                        FocusMatchUnit(match);
                    }
                }

                empty = false;
            }

            if (_showGraph)
            {
                foreach (var item in _matchGraph)
                {
                    EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                    if (item.ScriptGraphAsset != null)
                    {
                        var icon = EditorGUIUtility.ObjectContent(item.ScriptGraphAsset, typeof(ScriptGraphAsset));
                        GUILayout.Label(icon);
                    }
                    else
                    {
                        var icon = EditorGUIUtility.ObjectContent(item.StateGraphAsset, typeof(StateGraphAsset));
                        GUILayout.Label(icon);
                    }

                    if (GUILayout.Button($"      {item.AssetName}", EditorStyles.linkLabel))
                    {
                        FocusMatchGraph(item);
                    }

                    empty = false;
                }
            }


            if (empty)
            {
                GUILayout.Label("No result found.");
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        string GetUnitPath(GraphReference reference)
        {
            var nodePath = reference;
            var pathNames = "";
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            while (nodePath != null)
            {
                var prefix = "::";
                if (nodePath.graph != null)
                {
                    if (string.IsNullOrEmpty(nodePath.graph.title))
                    {
                        prefix = nodePath.serializedObject != null ? nodePath.serializedObject.name : nodePath.graph.GetType().ToString().Split(".").Last();
                    }
                    else
                    {
                        prefix = nodePath.graph.title;
                    }

                    prefix += "->";
                }

                pathNames = prefix + pathNames; //
                nodePath = nodePath.ParentReference(false);
            }

            return pathNames;
        }


        private void Search()
        {
            _matchObjects.Clear();
            _matchGraph.Clear();
            _matchScriptGraphMap.Clear();
            _sortedScriptGraphKey.Clear();
            _matchStateGraphMap.Clear();
            _sortedStateGraphKey.Clear();
            var pattern = _pattern;
            if (_wordMatch)
                pattern = $@"\b{Regex.Escape(pattern)}\b";

            var matchWord = new Regex(pattern, _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            // for script graphs.
            // begin of script graph
            var guids = AssetDatabase.FindAssets("t:ScriptGraphAsset", null);
            foreach (var guid in guids)
            {
                // continue;
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
                if (asset.GetReference().graph is not FlowGraph flowGraph) continue;
                var baseRef = asset.GetReference().AsReference();
                foreach (var element in UnitUtility.TraverseFlowGraphUnit(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    var newMatch = CheckMatchUnit(matchWord, unit);
                    if (newMatch == null) continue;
                    newMatch.ScriptGraphAsset = asset;
                    newMatch.Reference = reference;
                    if (_matchScriptGraphMap.TryGetValue(newMatch.ScriptGraphAsset, out var list))
                    {
                        list.Add(newMatch);
                    }
                    else
                    {
                        _matchScriptGraphMap[newMatch.ScriptGraphAsset] = new List<MatchUnit>() { newMatch };
                    }
                }

                // add self first
                var baseMatch = CheckMatchGraph(matchWord, flowGraph, assetPath);
                if (baseMatch != null)
                {
                    baseMatch.ScriptGraphAsset = asset;
                    baseMatch.Reference = baseRef;
                    _matchGraph.Add(baseMatch);
                }

                foreach (var (reference, graph) in UnitUtility.TraverseFlowGraph(baseRef))
                {
                    var newMatch = CheckMatchGraph(matchWord, graph, null);
                    if (newMatch == null) continue;
                    newMatch.ScriptGraphAsset = asset;
                    newMatch.Reference = reference;
                    _matchGraph.Add(newMatch);
                }
            }

            _sortedScriptGraphKey = _matchScriptGraphMap.Keys.ToList();
            _sortedScriptGraphKey.Sort((a, b) => String.Compare(a.name, b.name, StringComparison.Ordinal));
            // end of script graph

            // begin of script graph
            guids = AssetDatabase.FindAssets("t:StateGraphAsset", null);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);

                if (asset.GetReference().graph is not StateGraph stateGraph) continue;
                var baseRef = asset.GetReference().AsReference();
                foreach (var element in UnitUtility.TraverseStateGraphUnit(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    var newMatch = CheckMatchUnit(matchWord, unit);
                    if (newMatch == null) continue;
                    newMatch.StateGraphAsset = asset;
                    newMatch.Reference = reference;
                    _matchObjects.Add(newMatch);
                    if (_matchStateGraphMap.TryGetValue(newMatch.StateGraphAsset, out var list))
                    {
                        list.Add(newMatch);
                    }
                    else
                    {
                        _matchStateGraphMap[newMatch.StateGraphAsset] = new List<MatchUnit>() { newMatch };
                    }
                }

                // add self first
                var baseMatch = CheckMatchGraph(matchWord, stateGraph, assetPath);
                if (baseMatch != null)
                {
                    baseMatch.StateGraphAsset = asset;
                    baseMatch.Reference = baseRef;
                    _matchGraph.Add(baseMatch);
                }

                foreach (var (reference, graph) in UnitUtility.TraverseStateGraph(baseRef))
                {
                    var newMatch = CheckMatchGraph(matchWord, graph, null);
                    if (newMatch == null) continue;
                    newMatch.StateGraphAsset = asset;
                    newMatch.Reference = reference;
                    _matchGraph.Add(newMatch);
                }
            }

            _sortedStateGraphKey = _matchStateGraphMap.Keys.ToList();
            _sortedStateGraphKey.Sort((a, b) => String.Compare(a.name, b.name, StringComparison.Ordinal));

            _matchGraph.Sort((a, b) =>
                String.Compare(a.AssetName, b.AssetName, StringComparison.Ordinal));
        }

        MatchGraph CheckMatchGraph(Regex matchWord, Graph graph, string assetPath = null)
        {
            string element = null;

            if (assetPath != null)
            {
                var assetName = Path.GetFileNameWithoutExtension(assetPath);
                if (matchWord.IsMatch(assetName))
                {
                    element = assetName;
                }
            }
            else if (graph.title != null && matchWord.IsMatch(graph.title))
            {
                element = graph.title;
            }
            else if (graph.summary != null && matchWord.IsMatch(graph.summary))
            {
                element = graph.summary;
            }

            if (element != null)
            {
                return new MatchGraph()
                {
                    ScriptGraphAsset = null,
                    StateGraphAsset = null,
                    MatchString = element,
                    Graph = graph,
                };
            }

            return null;
        }


        MatchUnit CheckMatchUnit(Regex matchWord, Unit unit)
        {
            var matchRecord = new MatchUnit()
            {
                Matches = new List<MatchType>(),
                ScriptGraphAsset = null,
                StateGraphAsset = null,
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

        void FocusMatchGraph(MatchGraph match)
        {
            if (match.ScriptGraphAsset != null)
            {
                EditorGUIUtility.PingObject(match.ScriptGraphAsset);
                Selection.activeObject = match.ScriptGraphAsset;
            }
            else if (match.StateGraphAsset != null)
            {
                EditorGUIUtility.PingObject(match.StateGraphAsset);
                Selection.activeObject = match.StateGraphAsset;
            }
            // Locate 

            // open
            GraphReference reference = match.Reference;
            GraphWindow.OpenActive(reference);
        }

        void FocusMatchUnit(MatchUnit match)
        {
            if (match.ScriptGraphAsset != null)
            {
                EditorGUIUtility.PingObject(match.ScriptGraphAsset);
                Selection.activeObject = match.ScriptGraphAsset;
            }
            else if (match.StateGraphAsset != null)
            {
                EditorGUIUtility.PingObject(match.StateGraphAsset);
                Selection.activeObject = match.StateGraphAsset;
            }
            // Locate 

            // open
            GraphReference reference = match.Reference;
            GraphWindow.OpenActive(reference);

            // focus
            var context = reference.Context();
            if (context == null)
                return;
            context.BeginEdit();
            context.canvas?.ViewElements(((IGraphElement)match.Unit).Yield());
        }
    }
}
