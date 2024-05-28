using System.Collections;
using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

namespace Unity.VisualScripting.Community
{
    public class SearchNodeWindow : EditorWindow
    {
        // public static Event e;
        enum MatchType
        {
            Type,
            Method,
            Field,
            Reference
        }

        class MatchObject
        {
            public List<MatchType> Matches;
            public ScriptGraphAsset ScriptGraphAsset;
            public StateGraphAsset StateGraphAsset;
            public GraphReference Reference;
            public string FullTypeName;
            public IUnit Unit;
        }

        private string _pattern = "";

        private bool _caseSensitive = true;
        private bool _matchType = true;
        private bool _matchMethod = true;
        private bool _matchField = true;
        private bool _matchReference = true;
        private List<MatchObject> _matchObjects = new();
        private Dictionary<ScriptGraphAsset, List<MatchObject>> _matchScriptGraphMap = new();
        private List<ScriptGraphAsset> _sortedScriptGraphKey = new();
        private Dictionary<StateGraphAsset, List<MatchObject>> _matchStateGraphMap = new();
        private List<StateGraphAsset> _sortedStateGraphKey = new();


        // scroll view position
        private Vector2 _scrollViewRoot;


        [MenuItem("Window/UVS Community/Search Node")]
        public static void Open()
        {
            var window = GetWindow<SearchNodeWindow>();
            window.titleContent = new GUIContent("Search Node");
        }

        private void OnDisable()
        {
            _matchObjects.Clear();
            _matchScriptGraphMap.Clear();
            _sortedScriptGraphKey.Clear();
            _matchStateGraphMap.Clear();
            _sortedStateGraphKey.Clear();
        }

        private void OnGUI()
        {
            // e = Event.current;
            Event e = Event.current;
            if (e.keyCode == KeyCode.Return) Search();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Find", GUILayout.ExpandWidth(false));
            _pattern = GUILayout.TextField(_pattern);
            _caseSensitive = GUILayout.Toggle(_caseSensitive, "MatchCase", GUILayout.ExpandWidth(false));
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
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical("box");
            _scrollViewRoot = GUILayout.BeginScrollView(_scrollViewRoot);

            var empty = true;
            // for flow graph asset.
            foreach (var key in _sortedScriptGraphKey)
            {
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
                    var label = $"      {pathNames} : {match.FullTypeName}";
                    if (GUILayout.Button(label, EditorStyles.linkLabel))
                    {
                        FocusMatchObject(match);
                    }
                }

                empty = false;
            }

            // for state graph asset.
            foreach (var key in _sortedStateGraphKey)
            {
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
                    var label = $"      {pathNames} : {match.FullTypeName}";
                    if (GUILayout.Button(label, EditorStyles.linkLabel))
                    {
                        FocusMatchObject(match);
                    }
                }

                empty = false;
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
                        prefix = nodePath.graph.GetType().ToString().Split(".").Last();
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
            _matchScriptGraphMap.Clear();
            _sortedScriptGraphKey.Clear();
            _matchStateGraphMap.Clear();
            _sortedStateGraphKey.Clear();

            var matchWord = new Regex(_pattern, _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
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
                foreach (var element in TraverseFlowGraph(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    var newMatch = MatchUnit(matchWord, unit);
                    if (newMatch == null) continue;
                    newMatch.ScriptGraphAsset = asset;
                    newMatch.Reference = reference;
                    if (_matchScriptGraphMap.TryGetValue(newMatch.ScriptGraphAsset, out var list))
                    {
                        list.Add(newMatch);
                    }
                    else
                    {
                        _matchScriptGraphMap[newMatch.ScriptGraphAsset] = new List<MatchObject>() { newMatch };
                    }
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

                var baseRef = asset.GetReference().AsReference();
                foreach (var element in TraverseStateGraph(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    var newMatch = MatchUnit(matchWord, unit);
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
                        _matchStateGraphMap[newMatch.StateGraphAsset] = new List<MatchObject>() { newMatch };
                    }
                }
            }

            _sortedStateGraphKey = _matchStateGraphMap.Keys.ToList();
            _sortedStateGraphKey.Sort((a, b) => String.Compare(a.name, b.name, StringComparison.Ordinal));
        }

        IEnumerable<(List<SuperState>, FlowStateTransition, FlowGraph)> GetSubStates(
            GraphElementCollection<IState> states,
            GraphConnectionCollection<IStateTransition, IState, IState> transitions,
            SuperState parent,
            List<SuperState> nestParent)
        {
            nestParent = new List<SuperState>(nestParent);
            nestParent.Add(parent);


            // var stateGraph = states.nest.graph;
            // yield direct graphs first.
            foreach (var state in states)
            {
                if (state is not FlowState flowState) continue;
                // check flow graphs
                FlowGraph graph = null;
                graph = flowState.nest.embed ?? flowState.nest.graph;

                if (graph == null) continue;
                yield return (nestParent, null, graph);
            }

            // yield transitions.
            foreach (var transition in transitions)
            {
                if (transition is not FlowStateTransition flowStateTransition) continue;
                FlowGraph graph = null;
                graph = flowStateTransition.nest.embed ?? flowStateTransition.nest.graph;

                if (graph == null) continue;
                yield return (nestParent, flowStateTransition, graph);
            }

            // traverse sub states.
            foreach (var subState in states)
            {
                if (subState is not SuperState subSuperState) continue;
                var subStateGraph = subSuperState.nest.graph;
                var subTransitions = subStateGraph.transitions;
                foreach (var item in GetSubStates(subStateGraph.states, subTransitions, subSuperState, nestParent))
                {
                    yield return item;
                }
            }
        }

        IEnumerable<(GraphReference, Unit)> TraverseFlowGraph(GraphReference graphReference)
        {
            var flowGraph = graphReference.graph as FlowGraph;
            if (flowGraph == null) yield break;
            var units = flowGraph.units;
            foreach (var element in units)
            {
                var unit = element as Unit;
                switch (unit)
                {
                    // going deep
                    case SubgraphUnit subgraphUnit:
                    {
                        var subGraph = subgraphUnit.nest.embed ?? subgraphUnit.nest.graph;
                        if (subGraph == null) continue;
                        // find sub graph.
                        var childReference = graphReference.ChildReference(subgraphUnit, false);
                        foreach (var item in TraverseFlowGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case StateUnit stateUnit:
                    {
                        var stateGraph = stateUnit.nest.embed ?? stateUnit.nest.graph;
                        if (stateGraph == null) continue;
                        // find state graph.
                        var childReference = graphReference.ChildReference(stateUnit, false);
                        foreach (var item in TraverseStateGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    default:
                        yield return (graphReference, unit);
                        break;
                }
            }
        }

        IEnumerable<(GraphReference, Unit)> TraverseStateGraph(GraphReference graphReference)
        {
            var stateGraph = graphReference.graph as StateGraph;
            if (stateGraph == null) yield break;

            // var stateGraph = states.nest.graph;
            // yield direct graphs first.
            foreach (var state in stateGraph.states)
            {
                switch (state)
                {
                    case FlowState flowState:
                    {
                        // check flow graphs, which is the base of a state.
                        var graph = flowState.nest.embed ?? flowState.nest.graph;

                        if (graph == null) continue;
                        var childReference = graphReference.ChildReference(flowState, false);
                        foreach (var item in TraverseFlowGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case SuperState superState:
                    {
                        // check state graphs
                        var subStateGraph = superState.nest.embed ?? superState.nest.graph;
                        if (subStateGraph == null) continue;
                        var childReference = graphReference.ChildReference(superState, false);
                        foreach (var item in TraverseStateGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case AnyState:
                        continue;
                }
            }

            // don't forget transition nodes.
            foreach (var transition in stateGraph.transitions)
            {
                if (transition is not FlowStateTransition flowStateTransition) continue;
                var graph = flowStateTransition.nest.embed ?? flowStateTransition.nest.graph;
                if (graph == null) continue;
                var childReference = graphReference.ChildReference(flowStateTransition, false);
                foreach (var item in TraverseFlowGraph(childReference))
                {
                    yield return item;
                }
            }
        }


        MatchObject MatchUnit(Regex matchWord, Unit unit)
        {
            var matchRecord = new MatchObject()
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
                if (invoker.invocation.targetType != null)
                {
                    typeName = invoker.invocation.targetType.ToString().Split(".").Last();
                }

                if (matchWord.IsMatch(typeName))
                {
                    matchRecord.Matches.Add(MatchType.Type);
                }


                try
                {
                    if (matchWord.IsMatch(invoker.invocation.methodInfo.Name))
                    {
                        matchRecord.Matches.Add(MatchType.Method);
                    }
                }
                catch
                {
                    // pass
                }
            }

            matchRecord.FullTypeName = typeName;


            // fit fields
            var fields = unit.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var fitField = false;

            // var fitStrings = new List<string>();

            // try read serialized members
            foreach (var kvp in unit.defaultValues)
            {
                var value = kvp.Value;
                if (value == null) continue;
                var serializedValue = value.ToString();
                if (!matchWord.IsMatch(serializedValue)) continue;
                matchRecord.Matches.Add(MatchType.Field);
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
                break;
            }

            return matchRecord.Matches.Count > 0 ? matchRecord : null;
        }

        bool ShouldShowItem(IEnumerable<MatchObject> list)
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

        void FocusMatchObject(MatchObject match)
        {
            var asset = match.ScriptGraphAsset;
            // Locate
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;

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