using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Unity.VisualScripting.Community
{
    public class NodeBookmarkWindow : EditorWindow
    {
        [Serializable]
        class Bookmark
        {
            public string assetPath;
            public string name;
        }

        class UnitInfo
        {
            public GraphReference Reference;
            public IUnit Unit;
            public string Name;
            public string Path;
            public string Meta;
        }

        private string _unitFilterString = "";
        private string _graphFilterString = "";
        [SerializeField] List<Bookmark> _bookmarkList = new();

        Vector2 _unitScrollPosition = Vector2.zero; // 你需要在类的字段中定义这个变量
        // Vector2 _graphScrollPosition = Vector2.zero; // 你需要在类的字段中定义这个变量

        // [SerializeField] private int historyCount = 50;

        [MenuItem("Window/UVS Community/Node Bookmark")]
        public static void Open()
        {
            var window = GetWindow<NodeBookmarkWindow>();
            window.titleContent = new GUIContent("Node Bookmark");
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            _unitScrollPosition = GUILayout.BeginScrollView(_unitScrollPosition, "box");
            for (var index = 0; index < _bookmarkList.Count; index++)
            {
                var bookmark = _bookmarkList[index];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("x", GUILayout.ExpandWidth(false)))
                {
                    _bookmarkList.RemoveAt(index);
                }

                var unit = BuildUnitInfo(bookmark);
                if (unit != null)
                {
                    var tex = Icons.Icon(unit.Unit.GetType());
                    var icon = new GUIContent(tex[IconSize.Small]);
                    var fName = Path.GetFileNameWithoutExtension(bookmark.assetPath);
                    var label = $"{fName}=>{unit.Name}";
                    if (!string.IsNullOrEmpty(unit.Meta))
                    {
                        label += " (" + unit.Meta + ")";
                    }
                    icon.text = label;
                    if (GUILayout.Button(icon,
                            EditorStyles.linkLabel, GUILayout.MaxHeight(IconSize.Small + 4)))
                    {
                        FocusMatchObject(unit.Reference, unit.Unit);
                    }
                }
                else
                {
                    GUILayout.Label($"Missing {bookmark.name}:{bookmark.assetPath}");
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            if (GUILayout.Button("Add Selected", GUILayout.ExpandHeight(false)))
            {
                AddBookmark();
            }


            GUILayout.EndHorizontal(); // 结束整体布局
        }

        UnitInfo BuildUnitInfo(Bookmark bookmark)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(bookmark.assetPath);
            if (asset == null)
            {
                return null;
            }

            var detail = new UnitInfo();
            switch (asset)
            {
                case ScriptGraphAsset scriptGraphAsset:
                {
                    var baseRef = scriptGraphAsset.GetReference().AsReference();
                    detail.Path = GetUnitPath(baseRef);
                    detail.Reference = baseRef;
                    break;
                }
                case StateGraphAsset stateGraphAsset:
                {
                    var baseRef = stateGraphAsset.GetReference().AsReference();
                    detail.Path = GetUnitPath(baseRef);
                    detail.Reference = baseRef;
                    break;
                }
                default:
                    return null;
            }

            detail.Unit = FindNode(detail.Reference, bookmark.name);
            if (detail.Unit == null) return null;
            detail.Name = detail.Unit.ToString().Split('#')[0];
            switch (detail.Unit)
            {
                case FlowReroute:
                case GraphOutput:
                case GraphInput:
                case ValueReroute:
                    break;
                case Literal literal:
                    detail.Meta = $"{literal.value}";
                    return detail;
                    break;
                case MemberUnit invokeMember:
                    detail.Meta = $"{invokeMember.member.targetTypeName}->{invokeMember.member.name}";
                    return detail;
                    break;
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

                    detail.Meta = $"{setVariable.kind}:{vName}";
                    return detail;
                    break;
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

                    detail.Meta = $"{eName} : [{customEvent.argumentCount}]";
                    return detail;
                    break;
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

                    detail.Meta = $"{teName} : [{triggerEvent.argumentCount}]";
                    return detail;
                    break;

                case TriggerDefinedEvent triggerDefinedEvent:
                    detail.Meta = $"{triggerDefinedEvent.eventType}";
                    return detail;
                    break;
                case TriggerGlobalDefinedEvent triggerDefinedEvent:
                    detail.Meta = $"{triggerDefinedEvent.eventType}";
                    return detail;
                    break;
                case GlobalDefinedEventNode definedEventNode:
                    detail.Meta = $"{definedEventNode.eventType}";
                    return detail;
                    break;
                case DefinedEventNode definedEventNode:
                    detail.Meta = $"{definedEventNode.eventType}";
                    return detail;
                    break;
                case MissingType missingType:
                    detail.Meta = $"{missingType.formerType}";
                    return detail;
                    break;
                default:
                    return detail;
                    break;
            }

            return detail;
        }

        IUnit FindNode(GraphReference reference, string nodeName)
        {
            if (reference == null) return null;
            foreach (var enumerator in TraverseFlowGraph(reference))
            {
                if (enumerator.Item2.ToString() == nodeName)
                {
                    return enumerator.Item2;
                }
            }

            foreach (var enumerator in TraverseStateGraph(reference))
            {
                if (enumerator.Item2.ToString() == nodeName)
                {
                    return enumerator.Item2;
                }
            }

            return null;
        }

        void AddBookmark()
        {
            if (GraphWindow.active == null) return;
            var window = GraphWindow.active;
            if (window == null) return;
            var assetPath = AssetDatabase.GetAssetPath(window.reference.serializedObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("Unsupported method in non serialized graph.");
                return;
            }

            foreach (var elem in window.context.selection)
            {
                if (elem is IUnit unit)
                {
                    var uName = unit.ToString();
                    var existed = _bookmarkList.Any(x => x.assetPath == assetPath && x.name == uName);
                    if (!existed)
                    {
                        _bookmarkList.Add(new Bookmark()
                        {
                            assetPath = assetPath,
                            name = uName,
                        });
                    }
                }
            }
        }

        private void PingObjectInProject(string assetPath)
        {
            // 加载该资源
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (asset != null)
            {
                // 高亮显示该资源
                EditorGUIUtility.PingObject(asset);
                // 设置该资源为选中状态
                Selection.activeObject = asset;
            }
        }

        List<UnitInfo> BuildUnitDetail(IEnumerable<(GraphReference, Unit)> iterator)
        {
            var result = new List<UnitInfo>();
            foreach (var element in iterator)
            {
                var detail = new UnitInfo();
                var reference = element.Item1;
                var unit = element.Item2;
                detail.Path = GetUnitPath(reference);
                detail.Name = unit.ToString().Split('#')[0];
                detail.Unit = unit;
                detail.Reference = reference;
                switch (unit)
                {
                    case FlowReroute:
                    case GraphOutput:
                    case GraphInput:
                    case ValueReroute:
                        break;
                    case Literal literal:
                        detail.Meta = $"{literal.value}";
                        result.Add(detail);
                        break;
                    case MemberUnit invokeMember:
                        detail.Meta = $"{invokeMember.member.targetTypeName}->{invokeMember.member.name}";
                        result.Add(detail);
                        break;
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

                        detail.Meta = $"{setVariable.kind}:{vName}";
                        result.Add(detail);
                        break;
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

                        detail.Meta = $"{eName} : [{customEvent.argumentCount}]";
                        result.Add(detail);
                        break;
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

                        detail.Meta = $"{teName} : [{triggerEvent.argumentCount}]";
                        result.Add(detail);
                        break;

                    case TriggerDefinedEvent triggerDefinedEvent:
                        detail.Meta = $"{triggerDefinedEvent.eventType}";
                        result.Add(detail);
                        break;
                    case TriggerGlobalDefinedEvent triggerDefinedEvent:
                        detail.Meta = $"{triggerDefinedEvent.eventType}";
                        result.Add(detail);
                        break;
                    case GlobalDefinedEventNode definedEventNode:
                        detail.Meta = $"{definedEventNode.eventType}";
                        result.Add(detail);
                        break;
                    case DefinedEventNode definedEventNode:
                        detail.Meta = $"{definedEventNode.eventType}";
                        result.Add(detail);
                        break;
                    case MissingType missingType:
                        detail.Meta = $"{missingType.formerType}";
                        result.Add(detail);
                        break;
                    default:
                        result.Add(detail);
                        break;
                }
            }

            return result;
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

        void FocusMatchObject(GraphReference reference, IGraphElement unit)
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
    }
}