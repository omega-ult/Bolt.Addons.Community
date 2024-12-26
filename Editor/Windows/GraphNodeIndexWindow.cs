using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.Collections;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace Unity.VisualScripting.Community
{
    public class GraphInfo
    {
        public string Title;
        public string Meta;
        public Object Reference;
        public Type Type;
        public string AssetPath;
    }

    class UnitInfo
    {
        public GraphReference Reference;
        public IUnit Unit;
        public string Name;
        public string Path;
        public string Meta;
    }

    public class LastNode : EditorWindow
    {
        public List<GraphInfo> GraphList = new();
        public GraphInfo selectedGraphInfo;
        private string _unitFilterString = "";
        private string _graphFilterString = "";

        Vector2 _unitScrollPosition = Vector2.zero; // 你需要在类的字段中定义这个变量
        Vector2 _graphScrollPosition = Vector2.zero; // 你需要在类的字段中定义这个变量

        // [SerializeField] private int historyCount = 50;

        [MenuItem("Window/UVS Community/Node Index")]
        public static void Open()
        {
            var window = GetWindow<LastNode>();
            window.titleContent = new GUIContent("Node Index");
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(200));
            // GUILayout.
            // 如果没有记录的 Script Graph 文件，显示提示
            if (GraphList.Count == 0)
            {
                GUILayout.Label("Select any Graph Asset or Prefab to start.");
            }
            else
            {
                GUILayout.BeginScrollView(_graphScrollPosition,  GUILayout.ExpandHeight(true));
                GUILayout.BeginHorizontal();
                GUILayout.Label("Filter", GUILayout.ExpandWidth(false));
                _graphFilterString = GUILayout.TextField(_graphFilterString);
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    _graphFilterString = "";
                }
                GUILayout.EndHorizontal();
                // 显示记录的每个 Script Graph 文件
                var graphPattern = new Regex(_graphFilterString, RegexOptions.IgnoreCase);
                foreach (GraphInfo assetInfo in GraphList)
                {
                    if (!FilterDisplayGraph(graphPattern, assetInfo)) continue;
                    if (assetInfo.Meta.Contains("Graph"))
                    {
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(assetInfo.AssetPath);

                        // 使用按钮显示每个 Script Graph 文件路径
                        EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                        var icon = EditorGUIUtility.ObjectContent(assetInfo.Reference, assetInfo.Type);
                        icon.text = fileName;
                        // GUILayout.Label(icon);
                        if (GUILayout.Button(icon))
                        {
                            // 用户点击某个按钮时，跳转并高亮显示该文件
                            PingObjectInProject(assetInfo.AssetPath);
                            // var detail = GetDetail(assetInfo.AssetPath);
                            selectedGraphInfo = assetInfo;
                        }
                    }
                    else if (assetInfo.Meta.Contains("Embed"))
                    {
                        string fileName = assetInfo.AssetPath;
                        // var gameObject = GameObject.Find(assetInfo.AssetPath);
                        string gameObjectPath = SceneManager.GetActiveScene().path;

                        // 使用按钮显示每个 Script Graph 文件路径
                        if (GUILayout.Button(fileName))
                        {
                            // 用户点击某个按钮时，跳转并高亮显示该文件
                            PingObjectInProject(gameObjectPath);
                            selectedGraphInfo = assetInfo;
                        }
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();

            // 如果有选中的 Script Graph 文件，显示额外信息
            // Debug.Log(selectedAsset);

            // Debug.Log(selectedAsset.Keys);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.ExpandWidth(false));
            _unitFilterString = GUILayout.TextField(_unitFilterString);
            if (GUILayout.Button("x", GUILayout.Width(20)))
            {
                _unitFilterString = "";
            }
            GUILayout.EndHorizontal();


            _unitScrollPosition = GUILayout.BeginScrollView(_unitScrollPosition, "box", GUILayout.ExpandHeight(true));
            var units = GetDetailUnit(selectedGraphInfo);
            var pattern = new Regex(_unitFilterString, RegexOptions.IgnoreCase);
            foreach (var (path, unitList) in units)
            {
                GUILayout.Label(path);
                foreach (var unit in unitList)
                {
                    if (!FilterDisplayUnit(pattern, unit)) continue;
                    var label = $"  {unit.Name}";
                    if (!string.IsNullOrEmpty(unit.Meta))
                    {
                        label += " (" + unit.Meta + ")";
                    }

                    var tex = Icons.Icon(unit.Unit.GetType());
                    var icon = new GUIContent(tex[IconSize.Small]) ;
                    icon.text = label;
                    // GUILayout.ic
                    if (GUILayout.Button(icon,
                            EditorStyles.linkLabel))
                    {
                        FocusMatchObject(unit.Reference, unit.Unit);
                    }
                }
            }


            GUILayout.EndScrollView(); // 结束滚动视图
            GUILayout.EndVertical();


            GUILayout.EndHorizontal(); // 结束整体布局
        }

        bool FilterDisplayGraph(Regex pattern, GraphInfo graph)
        {
            if (string.IsNullOrEmpty(_graphFilterString)) return true;
            return pattern.IsMatch(graph.Title);
        }
        bool FilterDisplayUnit(Regex pattern, UnitInfo unit)
        {
            if (string.IsNullOrEmpty(_unitFilterString)) return true;
            if (pattern.IsMatch(unit.Name)) return true;
            return unit.Meta != null && pattern.IsMatch(unit.Meta);
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

        private void OnEnable()
        {
            // 监听 Unity 编辑器中的资源选择变化
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            // 注销监听
            Selection.selectionChanged -= OnSelectionChanged;
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

        private Dictionary<string, List<UnitInfo>> GetDetailUnit(GraphInfo graphInfo)
        {
            Dictionary<string, List<UnitInfo>> result = new();
            if (graphInfo == null)
                return result;
            List<UnitInfo> fetched = null;

            if (graphInfo.Meta.Contains("Graph"))
            {
                var scriptAsset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(graphInfo.AssetPath);
                if (scriptAsset != null)
                {
                    if (scriptAsset.GetReference().graph is not FlowGraph flowGraph) return result;
                    var baseRef = scriptAsset.GetReference().AsReference();
                    fetched = BuildUnitDetail(TraverseFlowGraph(baseRef));
                }

                var stateAsset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(graphInfo.AssetPath);
                if (stateAsset != null)
                {
                    if (stateAsset.GetReference().graph is not StateGraph stateGraph) return result;
                    var baseRef = stateAsset.GetReference().AsReference();
                    fetched = BuildUnitDetail(TraverseStateGraph(baseRef));
                }
            }
            else if (graphInfo.Meta.Contains("Embed"))
            {
                var selectedScriptAsset = GameObject.Find(graphInfo.AssetPath).GetComponentInChildren<ScriptMachine>();
                if (selectedScriptAsset != null)
                {
                    if (selectedScriptAsset.GetReference().graph is not FlowGraph flowGraph) return result;
                    var baseRef = selectedScriptAsset.GetReference().AsReference();
                    fetched = BuildUnitDetail(TraverseFlowGraph(baseRef));
                }

                var selectedStatetAsset = GameObject.Find(graphInfo.AssetPath).GetComponentInChildren<StateMachine>();
                if (selectedStatetAsset != null)
                {
                    if (selectedStatetAsset.GetReference().graph is not StateGraph stateGraph) return result;
                    //ChildReference(flowState, false);
                    var baseRef = selectedScriptAsset.GetReference().AsReference();
                    fetched = BuildUnitDetail(TraverseStateGraph(baseRef));
                }
            }

            if (fetched != null)
            {
                foreach (var unit in fetched)
                {
                    if (!result.TryGetValue(unit.Path, out var list))
                    {
                        list = new List<UnitInfo>();
                        result[unit.Path] = list;
                    }

                    list.Add(unit);
                }

                foreach (var (path, list) in result)
                {
                    list.Sort((x, y) => x.Name.CompareTo(y.Name));
                }
            }


            return result;
        }

        private Dictionary<string, string> GetDetail(string assetPath)
        {
            var detail = new Dictionary<string, string>();

            var scriptAsset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (scriptAsset != null)
            {
                if (scriptAsset.GetReference().graph is not FlowGraph flowGraph) return detail;
                var baseRef = scriptAsset.GetReference().AsReference();

                foreach (var element in TraverseFlowGraph(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    var node = GetUnitPath(reference);
                    if (detail.ContainsKey(node))
                    {
                        detail[node] = detail[node] + unit.ToString().Split('#')[0];
                        detail[node] += "\n";
                    }
                    else
                    {
                        var val = unit.ToString().Split('#')[0] + "\n";
                        detail[node] = val;
                    }
                }

                return detail;
            }

            var stateAsset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
            if (stateAsset != null)
            {
                if (stateAsset.GetReference().graph is not StateGraph stateGraph) return detail;
                var baseRef = stateAsset.GetReference().AsReference();
                foreach (var element in TraverseStateGraph(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    //Debug.Log(unit);

                    var node = GetUnitPath(reference);
                    if (detail.ContainsKey(node))
                    {
                        detail[node] = detail[node] + unit.ToString().Split('#')[0];
                        detail[node] += "\n";
                    }
                    else
                    {
                        var val = unit.ToString().Split('#')[0] + "\n";
                        detail[node] = val;
                    }
                }

                return detail;
            }

            return detail;
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

        void SortHistory()
        {
            GraphList.Sort((GraphInfo x, GraphInfo y) => x.Title.CompareTo(y.Title));
        }

        private void OnSelectionChanged()
        {
            // 获取当前选中的对象
            var selectedObject = Selection.activeObject;
            var dirty = false;

            // 判断当前选中的对象是否为 Script Graph 文件
            if (selectedObject != null && selectedObject is ScriptGraphAsset or StateGraphAsset)
            {
                // 检查扩展名或其他方式来确定是否是 Script Graph 文件
                string assetPath = AssetDatabase.GetAssetPath(selectedObject);
                GraphInfo graphInfo = new GraphInfo()
                {
                    Title = selectedObject.name,
                    Meta = "Graph",
                    Type = selectedObject.GetType(),
                    Reference = selectedObject,
                    AssetPath = assetPath
                };

                int intDeleteIndex = -1;
                for (int i = 0; i < GraphList.Count; i++)
                {
                    GraphInfo info = (GraphInfo)GraphList[i];
                    if (info.AssetPath.Equals(graphInfo.AssetPath))
                        intDeleteIndex = i;
                }

                if (intDeleteIndex != -1)
                    GraphList.RemoveAt(intDeleteIndex);
                GraphList.Insert(0, graphInfo);
                //alRecentGraphs.Add(graphInfo);

                // if (GraphList.Count > historyCount)
                // {
                //     GraphList.RemoveAt(GraphList.Count - 1); // 删除最后一个
                // }

                selectedGraphInfo = graphInfo;
                dirty = true;
            }
            else if (selectedObject != null && selectedObject is GameObject)
            {
                var sm = selectedObject.GetComponent<ScriptMachine>();

                if (sm != null)
                {
                    GraphInfo graphInfo = new GraphInfo()
                    {
                        Title = selectedObject.name,
                        Meta = "Embed",
                        Type = typeof(ScriptGraphAsset),
                        Reference = selectedObject,
                        AssetPath = selectedObject.name
                    };

                    int intDeleteIndex = -1;
                    for (int i = 0; i < GraphList.Count; i++)
                    {
                        GraphInfo info = (GraphInfo)GraphList[i];
                        if (info.AssetPath.Equals(graphInfo.AssetPath))
                            intDeleteIndex = i;
                    }

                    if (intDeleteIndex != -1)
                        GraphList.RemoveAt(intDeleteIndex);
                    GraphList.Insert(0, graphInfo);

                    // if (GraphList.Count > historyCount)
                    // {
                    //     GraphList.RemoveAt(GraphList.Count - 1); // 删除最后一个
                    // }

                    selectedGraphInfo = graphInfo;
                    dirty = true;
                }

                var state = selectedObject.GetComponent<StateMachine>();

                if (state != null)
                {
                    GraphInfo graphInfo = new GraphInfo()
                    {
                        Title = selectedObject.name,
                        Meta = "Embed",
                        Type = typeof(StateGraphAsset),
                        Reference = selectedObject,
                        AssetPath = selectedObject.name
                    };
                    
                    int intDeleteIndex = -1;
                    for (int i = 0; i < GraphList.Count; i++)
                    {
                        GraphInfo info = (GraphInfo)GraphList[i];
                        if (info.AssetPath.Equals(graphInfo.AssetPath))
                            intDeleteIndex = i;
                    }

                    if (intDeleteIndex != -1)
                        GraphList.RemoveAt(intDeleteIndex);
                    GraphList.Insert(0, graphInfo);
                    
                    // if (GraphList.Count > historyCount)
                    // {
                    //     GraphList.RemoveAt(GraphList.Count - 1); // 删除最后一个
                    // }
                    selectedGraphInfo = graphInfo;
                    dirty = true;
                }
            }

            if (!dirty) return;
            SortHistory();
            Repaint();
        }
    }
}