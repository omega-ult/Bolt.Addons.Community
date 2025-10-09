using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.VisualScripting.Community
{
    public static class UnitUtility
    {
        public enum EntrySource
        {
            GraphAsset,
            SceneEmbedded,
            PrefabEmbedded
        }

        [Serializable]
        public class EntryContext
        {
            public string scenePath;
            public string assetPath;
            [NonSerialized] public Object RootObject;
            public string objectPath;
            public string prefabStage;
            public EntrySource source;
        }

        public static EntryContext GetEntryContext(GraphReference assetEntry)
        {
            var reference = assetEntry;

            var scenePath = "";
            var assetPath = AssetDatabase.GetAssetPath(reference?.serializedObject);
            var embeddedSource = EntrySource.GraphAsset;

            var context = new EntryContext()
            {
                RootObject = reference?.rootObject,
                prefabStage = PrefabStageUtility.GetCurrentPrefabStage()?.assetPath
            };

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    assetPath = stage.assetPath;
                    if (reference?.rootObject != null)
                    {
                        var obj = reference.rootObject as GameObject;
                        if (obj != null)
                        {
                            context.objectPath = GetTransformPath(obj.transform);
                        }
                    }
                }

                embeddedSource = EntrySource.PrefabEmbedded;
            }
            else if (string.IsNullOrEmpty(assetPath))
            {
                // 对于嵌入式图表，serializedObject可能是GameObject或其他对象
                var obj = reference?.gameObject;
                // // 对于场景中的对象，使用场景路径
                scenePath = obj?.scene.path;
                context.scenePath = scenePath;
                if (!string.IsNullOrEmpty(scenePath))
                {
                    var objPath = GetTransformPath(obj.transform);
                    if (obj != null)
                    {
                        context.objectPath = objPath;
                    }

                    if (string.IsNullOrEmpty(objPath))
                    {
                        Debug.LogWarning("Unsupported method in non serialized graph.");
                        return null;
                    }

                    embeddedSource = EntrySource.SceneEmbedded;
                }

                var window = GraphWindow.active;
                if (window != null && window.context.isPrefabInstance)
                {
                    Debug.LogWarning("Unsupported method in non serialized graph.");
                    return null;
                }
            }

            var entrySource = EntrySource.GraphAsset;
            if (reference?.machine != null)
            {
                entrySource = reference.machine.nest.source == GraphSource.Embed
                    ? embeddedSource
                    : EntrySource.GraphAsset;
            }

            context.scenePath = scenePath;
            context.assetPath = assetPath;
            context.source = entrySource;

            return context;
        }

        public static void RestoreContext(EntryContext context)
        {
            if (context.source == EntrySource.GraphAsset)
            {
                if (context.RootObject != null && Selection.activeObject != context.RootObject)
                {
                    Selection.activeObject = context.RootObject;
                }
            }
            else if (context.source == EntrySource.PrefabEmbedded)
            {
                var opening = PrefabStageUtility.GetCurrentPrefabStage()?.assetPath;
                if (!string.IsNullOrEmpty(context.prefabStage))
                {
                    if (opening != context.prefabStage)
                        PrefabStageUtility.OpenPrefab(context.prefabStage);
                }
                else
                {
                    if (string.IsNullOrEmpty(opening))
                    {
                        StageUtility.GoToMainStage();
                    }
                }

                PrefabUtility.LoadPrefabContents(context.prefabStage);
            }
            else if (context.source == EntrySource.SceneEmbedded)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.path != context.scenePath)
                {
                    EditorSceneManager.OpenScene(context.scenePath);
                    var obj = UnitUtility.GetTransform(context.objectPath);
                    if (obj != null)
                    {
                        Selection.activeGameObject = obj.gameObject;
                    }
                }
            }
        }

        private static Color _graphColor = new Color(0.5f, 0.8f, 0.6f);
        private static Color _prefabColor = new Color(0.2f, 0.7f, 0.9f);
        private static Color _sceneColor = new Color(0.7f, 0.7f, 0.7f);

        public static void DrawContextButton(EntryContext context, Action onClick = null)
        {
            switch (context.source)
            {
                case EntrySource.GraphAsset:
                    GUI.color = _graphColor;
                    if (GUILayout.Button("G", GUILayout.ExpandWidth(false)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(context.assetPath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                            onClick?.Invoke();
                        }
                    }

                    GUI.color = Color.white;
                    break;
                case EntrySource.PrefabEmbedded:
                    GUI.color = _prefabColor;
                    if (GUILayout.Button("P", GUILayout.ExpandWidth(false)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(context.assetPath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                            onClick?.Invoke();
                        }
                    }

                    GUI.color = Color.white;
                    break;
                case EntrySource.SceneEmbedded:
                    GUI.color = _sceneColor;
                    if (GUILayout.Button("S", GUILayout.ExpandWidth(false)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(context.scenePath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                            onClick?.Invoke();
                        }
                    }

                    GUI.color = Color.white;
                    break;
            }
        }

        public static bool IsContextValid(EntryContext context)
        {
            switch (context.source)
            {
                case EntrySource.GraphAsset:
                    return !string.IsNullOrEmpty(context.assetPath);
                case EntrySource.PrefabEmbedded:
                    return !string.IsNullOrEmpty(context.prefabStage);
                case EntrySource.SceneEmbedded:
                    return !string.IsNullOrEmpty(context.scenePath);
                default:
                    return false;
            }
        }

        public static GraphReference GetGraphReference(EntryContext context)
        {
            switch (context.source)
            {
                case EntrySource.GraphAsset:
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(context.assetPath);
                    switch (asset)
                    {
                        case ScriptGraphAsset scriptGraphAsset:
                        {
                            var baseRef = scriptGraphAsset.GetReference().AsReference();
                            return baseRef;
                        }
                        case StateGraphAsset stateGraphAsset:
                        {
                            var baseRef = stateGraphAsset.GetReference().AsReference();
                            return baseRef;
                        }
                        default:
                            return null;
                    }
                }
                case EntrySource.PrefabEmbedded:
                    // TODO
                    return null;
                case EntrySource.SceneEmbedded:
                    // TODO
                    return null;
                default:
                    return null;
            }
        }

        public static GraphReference GetUnitGraphReference(GraphReference assetEntry, string unitName)
        {
            if (assetEntry == null) return null;
            foreach (var (reference, unit) in TraverseFlowGraphUnit(assetEntry))
            {
                if (unit.ToString() == unitName)
                {
                    return reference;
                }
            }

            foreach (var (reference, unit) in TraverseStateGraphUnit(assetEntry))
            {
                if (unit.ToString() == unitName)
                {
                    return reference;
                }
            }

            return null;
        }

        public static string GetGraphPath(GraphReference reference, string separator = "->", bool ignoreRoot = true)
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
                        var hash = nodePath.graph.GetHashCode();
                        prefix = nodePath.graph.GetType().ToString().Split(".").Last();
                        prefix += $"[{hash.ToString("X").Substring(0, 4)}]";
                    }
                    else
                    {
                        prefix = nodePath.graph.title;
                    }

                    prefix += separator;
                }

                if (ignoreRoot && nodePath == reference)
                {
                    pathNames = prefix + pathNames;
                }

                nodePath = nodePath.ParentReference(false);
            }

            return pathNames;
        }

        public static void FocusUnit(GraphReference reference, IGraphElement unit)
        {
            // open
            GraphWindow.OpenActive(reference);

            // focus
            var context = reference.Context();
            if (context == null)
                return;
            context.BeginEdit();
            context.canvas?.ViewElements(((IGraphElement)unit).Yield());
        }


        public static IEnumerable<(GraphReference, Unit)> TraverseFlowGraphUnit(GraphReference graphReference)
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
                        yield return (graphReference, subgraphUnit);
                        var subGraph = subgraphUnit.nest.embed ?? subgraphUnit.nest.graph;
                        if (subGraph == null) continue;
                        // find sub graph.
                        var childReference = graphReference.ChildReference(subgraphUnit, false);
                        foreach (var item in TraverseFlowGraphUnit(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case StateUnit stateUnit:
                    {
                        yield return (graphReference, stateUnit);
                        var stateGraph = stateUnit.nest.embed ?? stateUnit.nest.graph;
                        if (stateGraph == null) continue;
                        // find state graph.
                        var childReference = graphReference.ChildReference(stateUnit, false);
                        foreach (var item in TraverseStateGraphUnit(childReference))
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

        public static IEnumerable<(GraphReference, Graph)> TraverseFlowGraph(GraphReference graphReference)
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
                        yield return (graphReference, subGraph);
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
                        yield return (graphReference, stateGraph);
                        var childReference = graphReference.ChildReference(stateUnit, false);
                        foreach (var item in TraverseStateGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                }
            }
        }

        // Get the full path of a transform in the scene hierarchy
        public static string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        // Get a transform from a path in the scene hierarchy
        // path format: "Root/Child/GrandChild"
        public static Transform GetTransform(string transformPath)
        {
            var path = transformPath.Split("/");
            var root = GameObject.Find(path[0]);
            if (root == null) return null;
            var transform = root.transform;
            for (var i = 1; i < path.Length; i++)
            {
                transform = transform.Find(path[i]);
                if (transform == null) return null;
            }

            return transform;
        }

        // for graph node only
        public static IEnumerable<(GraphReference, Graph)> TraverseStateGraph(GraphReference graphReference)
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
                        yield return (graphReference, graph);
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
                        yield return (graphReference, subStateGraph);
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
                yield return (graphReference, graph);
                var childReference = graphReference.ChildReference(flowStateTransition, false);
                foreach (var item in TraverseFlowGraph(childReference))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<(GraphReference, Unit)> TraverseStateGraphUnit(GraphReference graphReference)
        {
            var stateGraph = graphReference.graph as StateGraph;
            if (stateGraph == null) yield break;

            // yield direct graphs first.
            foreach (var state in stateGraph.states)
            {
                //Debug.Log(state);
                switch (state)
                {
                    case FlowState flowState:
                    {
                        // check flow graphs, which is the base of a state.
                        var graph = flowState.nest.embed ?? flowState.nest.graph;

                        if (graph == null) continue;
                        var childReference = graphReference.ChildReference(flowState, false);
                        foreach (var item in TraverseFlowGraphUnit(childReference))
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
                        foreach (var item in TraverseStateGraphUnit(childReference))
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
                foreach (var item in TraverseFlowGraphUnit(childReference))
                {
                    yield return item;
                }
            }
        }

        public static string UnitBrief(IUnit unit)
        {
            if (unit == null) return null;
            switch (unit)
            {
                case FlowReroute:
                case GraphOutput:
                case GraphInput:
                case ValueReroute:
                    break;
                case Literal literal:
                    return $"{literal.value}";
                case MemberUnit invokeMember:
                    return $"{invokeMember.member.targetTypeName}->{invokeMember.member.name}";
                case UnifiedVariableUnit setVariable:
                    var vName = "";
                    if (!setVariable.name.hasValidConnection)
                    {
                        if (setVariable.defaultValues.TryGetValue(nameof(setVariable.name), out var v))
                        {
                            vName = v.ToString();
                        }
                    }
                    else
                    {
                        vName = setVariable.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    return $"{setVariable.kind}:{vName}";
                case CustomEvent customEvent:
                    var eName = "";
                    if (!customEvent.name.hasValidConnection)
                    {
                        if (customEvent.defaultValues.TryGetValue(nameof(customEvent.name), out var v))
                        {
                            eName = v.ToString();
                        }
                    }
                    else
                    {
                        eName = customEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    return $"{eName} : [{customEvent.argumentCount}]";
                case TriggerCustomEvent triggerEvent:
                    var teName = "";
                    if (!triggerEvent.name.hasValidConnection)
                    {
                        if (triggerEvent.defaultValues.TryGetValue(nameof(triggerEvent.name), out var v))
                        {
                            teName = v.ToString();
                        }
                    }
                    else
                    {
                        teName = triggerEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    return $"{teName} : [{triggerEvent.argumentCount}]";
                case TriggerDefinedEvent triggerDefinedEvent:
                    return $"{triggerDefinedEvent.eventType}";
                case TriggerGlobalDefinedEvent triggerDefinedEvent:
                    return $"{triggerDefinedEvent.eventType}";
                case GlobalDefinedEventNode definedEventNode:
                    return $"{definedEventNode.eventType}";
                case DefinedEventNode definedEventNode:
                    return $"{definedEventNode.eventType}";
                case TriggerReturnEvent triggerReturnEvent:
                    var trName = "";
                    if (!triggerReturnEvent.name.hasValidConnection)
                    {
                        if (triggerReturnEvent.defaultValues.TryGetValue(nameof(triggerReturnEvent.name), out var v))
                        {
                            trName = v.ToString();
                        }
                    }
                    else
                    {
                        trName = triggerReturnEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    var trGlobal = triggerReturnEvent.global ? "[G]" : "";
                    return $"{trGlobal}{trName} : [{triggerReturnEvent.count}]";
                case ReturnEvent returnEvent:
                    var rName = "";
                    if (!returnEvent.name.hasValidConnection)
                    {
                        if (returnEvent.defaultValues.TryGetValue(nameof(returnEvent.name), out var v))
                        {
                            rName = v.ToString();
                        }
                    }
                    else
                    {
                        rName = returnEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    var rGlobal = returnEvent.global ? "[G]" : "";
                    return $"{rGlobal}{rName} : [{returnEvent.count}]";
                case BoltUnityEvent boltUnityEvent:
                    var uEvent = "";
                    if (!boltUnityEvent.name.hasValidConnection)
                    {
                        if (boltUnityEvent.defaultValues.TryGetValue(nameof(boltUnityEvent.name), out var v))
                        {
                            uEvent = v.ToString();
                        }
                    }
                    else
                    {
                        uEvent = boltUnityEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    return $"{uEvent}";
                case MissingType missingType:
                    return $"{missingType.formerType}";
                case SwitchOnString switchOnString:
                    return $"{string.Join(",", switchOnString.options)}";
                case SwitchOnInteger switchOnInteger:
                    return $"{string.Join(",", switchOnInteger.options)}";
                case SwitchOnEnum switchOnEnum:
                    return $"{switchOnEnum.enumType?.Name}";
                default:
                    break;
            }

            return null;
        }

        public static List<string> UnitValues(IUnit unit)
        {
            if (unit == null) return null;
            // try read serialized members
            var result = new List<string>();
            switch (unit)
            {
                case UnifiedVariableUnit { defaultValues: not null } unifiedVariableUnit:
                {
                    var str = (string)unifiedVariableUnit.defaultValues[nameof(unifiedVariableUnit.name)];
                    result.Add(str);
                    break;
                }
                default:
                    foreach (var kvp in unit.defaultValues)
                    {
                        var value = kvp.Value;
                        if (value == null) continue;
                        var serializedValue = value.ToString();
                        result.Add(serializedValue);
                    }
                    break;
            }

            return result;
        }
    }
}