using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace Unity.VisualScripting.Community
{
    public class VariableRenameWindow : EditorWindow
    {
        [Serializable]
        class GraphInfo
        {
            public string title;
            public string source;
            public Object reference;
            public Type type;
            public string assetPath;
        }

        [Serializable]
        class RenameCommand
        {
            public GraphInfo GraphInfo;
            public VariableKind Kind;
            public string OldName;
            public string NewName;

            public RenameCommand(GraphInfo graphInfo, VariableKind kind, string oldName, string newName)
            {
                GraphInfo = graphInfo;
                Kind = kind;
                OldName = oldName;
                NewName = newName;
            }
        }

        class VariableInfo
        {
            public GraphReference Reference;
            public IUnit Unit;
            public string Name;
            public string Path;
            public VariableKind Kind;
            public string DefaultName;
        }

        // 撤销和重做栈
        private Stack<RenameCommand> _undoStack = new Stack<RenameCommand>();
        private Stack<RenameCommand> _redoStack = new Stack<RenameCommand>();

        [SerializeField] private List<GraphInfo> _graphList = new();
        private List<GraphInfo> _historyList = new();
        private GraphInfo _selectedGraphInfo;
        private string _variableFilterString = "";
        private string _graphFilterString = "";
        private string _renameToString = "";
        private VariableInfo _selectedVariable;

        // 当前选中的变量类型标签
        private VariableKind _selectedKind = VariableKind.Flow;

        Vector2 _variableScrollPosition = Vector2.zero;
        Vector2 _graphScrollPosition = Vector2.zero;
        Vector2 _historyScrollPosition = Vector2.zero;
        Vector2 _nodeScrollPosition = Vector2.zero;

        [SerializeField] private int historyCount = 50;

        [MenuItem("Window/UVS Community/Variable Rename")]
        public static void Open()
        {
            var window = GetWindow<VariableRenameWindow>();
            window.titleContent = new GUIContent("Variable Rename");
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(250));
            DrawGraphList();
            DrawHistory();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            DrawVariableTabs();
            DrawVariableList();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(300));
            DrawNodeList();
            DrawRenamePanel();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        void DrawVariableTabs()
        {
            GUILayout.BeginHorizontal();

            // 绘制变量类型标签页
            foreach (VariableKind kind in Enum.GetValues(typeof(VariableKind)))
            {
                var style = _selectedKind == kind ? EditorStyles.toolbarButton : EditorStyles.toolbarDropDown;
                if (GUILayout.Toggle(_selectedKind == kind, kind.ToString(), style))
                {
                    if (_selectedKind != kind)
                    {
                        _selectedKind = kind;
                        _selectedVariable = null;
                    }
                }
            }

            GUILayout.EndHorizontal();

            // 变量过滤器
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.ExpandWidth(false));
            _variableFilterString = GUILayout.TextField(_variableFilterString);
            if (GUILayout.Button("x", GUILayout.Width(20)))
            {
                _variableFilterString = "";
            }

            GUILayout.EndHorizontal();
        }

        void DrawVariableList()
        {
            _variableScrollPosition =
                GUILayout.BeginScrollView(_variableScrollPosition, "box", GUILayout.ExpandHeight(true));

            if (_selectedGraphInfo != null)
            {
                var variables = GetVariables(_selectedGraphInfo, _selectedKind);
                var pattern = new Regex(_variableFilterString, RegexOptions.IgnoreCase);

                // 按变量名称分组
                var groupedVariables = variables
                    .GroupBy(v => v.DefaultName)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedVariables)
                {
                    if (!string.IsNullOrEmpty(_variableFilterString) && !pattern.IsMatch(group.Key))
                        continue;

                    var variableCount = group.Count();
                    var label = $"{group.Key} ({variableCount})";

                    // 获取变量图标
                    var firstVar = group.First();
                    var tex = BoltCore.Icons.VariableKind(firstVar.Kind);
                    var icon = new GUIContent(tex[IconSize.Small]);
                    icon.text = label;

                    var style = _selectedVariable != null && _selectedVariable.DefaultName == group.Key
                        ? EditorStyles.boldLabel
                        : EditorStyles.label;

                    if (GUILayout.Button(icon, style, GUILayout.MaxHeight(IconSize.Small + 4)))
                    {
                        _selectedVariable = firstVar;
                    }
                }
            }
            else
            {
                GUILayout.Label("Select a graph to view variables");
            }

            GUILayout.EndScrollView();
        }

        void DrawNodeList()
        {
            GUILayout.Label("Node Locations", EditorStyles.boldLabel);

            _nodeScrollPosition = GUILayout.BeginScrollView(_nodeScrollPosition, "box", GUILayout.ExpandHeight(true));

            if (_selectedVariable != null)
            {
                var variables = GetVariables(_selectedGraphInfo, _selectedKind)
                    .Where(v => v.DefaultName == _selectedVariable.DefaultName)
                    .OrderBy(v => v.Path);

                foreach (var variable in variables)
                {
                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button($"{variable.Path}{variable.Unit.ToString()}", EditorStyles.linkLabel))
                    {
                        UnitUtility.FocusUnit(variable.Reference, variable.Unit);
                    }

                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("Select a variable to see its locations");
            }

            GUILayout.EndScrollView();
        }

        void DrawRenamePanel()
        {
            GUILayout.Label("Rename Variable", EditorStyles.boldLabel);

            if (_selectedVariable != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Current Name:", GUILayout.Width(100));
                GUILayout.Label(_selectedVariable.DefaultName);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Rename To:", GUILayout.Width(100));
                _renameToString = GUILayout.TextField(_renameToString);
                GUILayout.EndHorizontal();

                GUI.enabled = !string.IsNullOrEmpty(_renameToString) &&
                              _renameToString != _selectedVariable.DefaultName;

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Rename All Occurrences"))
                {
                    // 记录重命名命令到撤销栈
                    _undoStack.Push(new RenameCommand(_selectedGraphInfo, _selectedKind, _selectedVariable.DefaultName,
                        _renameToString));
                    // 执行重命名操作
                    RenameVariables(_selectedGraphInfo, _selectedKind, _selectedVariable.DefaultName, _renameToString);
                    // 清空重做栈
                    _redoStack.Clear();
                    _selectedVariable = null;
                    _renameToString = "";
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();

                GUI.enabled = _undoStack.Count > 0;
                if (GUILayout.Button("Undo"))
                {
                    if (_undoStack.Count > 0)
                    {
                        var command = _undoStack.Pop();
                        _redoStack.Push(new RenameCommand(command.GraphInfo, command.Kind, command.NewName,
                            command.OldName));
                        RenameVariables(command.GraphInfo, command.Kind, command.NewName, command.OldName);
                    }
                }

                GUI.enabled = _redoStack.Count > 0;
                if (GUILayout.Button("Redo"))
                {
                    if (_redoStack.Count > 0)
                    {
                        var command = _redoStack.Pop();
                        _undoStack.Push(new RenameCommand(command.GraphInfo, command.Kind, command.NewName,
                            command.OldName));
                        RenameVariables(command.GraphInfo, command.Kind, command.NewName, command.OldName);
                    }
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label("Select a variable to rename");
            }
        }

        void DrawGraphList()
        {
            if (_graphList.Count == 0)
            {
                GUILayout.Label("Select any Graph Asset or Prefab to start.");
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Filter", GUILayout.ExpandWidth(false));
                _graphFilterString = GUILayout.TextField(_graphFilterString);
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    _graphFilterString = "";
                }

                GUILayout.EndHorizontal();

                _graphScrollPosition = GUILayout.BeginScrollView(_graphScrollPosition, GUILayout.ExpandHeight(true));
                var graphPattern = new Regex(_graphFilterString, RegexOptions.IgnoreCase);

                for (var index = 0; index < _graphList.Count; index++)
                {
                    var assetInfo = _graphList[index];
                    if (!FilterDisplayGraph(graphPattern, assetInfo)) continue;

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("x", GUILayout.ExpandWidth(false)))
                    {
                        _graphList.RemoveAt(index);
                        if (_selectedGraphInfo == assetInfo)
                        {
                            _selectedGraphInfo = null;
                            _selectedVariable = null;
                        }
                    }

                    if (assetInfo.source.Equals("Graph"))
                    {
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(assetInfo.assetPath);

                        EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                        var icon = EditorGUIUtility.ObjectContent(assetInfo.reference, assetInfo.type);
                        icon.text = fileName;
                        if (GUILayout.Button(icon))
                        {
                            PingObjectInProject(assetInfo.assetPath);
                            _selectedGraphInfo = assetInfo;
                            _selectedVariable = null;
                            AddSelectionHistory();
                        }
                    }
                    else if (assetInfo.source.Equals("Embed"))
                    {
                        if (assetInfo.reference == null)
                        {
                            _graphList.RemoveAt(index);
                            if (_selectedGraphInfo == assetInfo)
                            {
                                _selectedGraphInfo = null;
                                _selectedVariable = null;
                            }

                            GUILayout.Label("Missing");
                        }
                        else
                        {
                            string fileName = assetInfo.reference.name;
                            string gameObjectPath = SceneManager.GetActiveScene().path;

                            if (GUILayout.Button(fileName))
                            {
                                PingObjectInProject(gameObjectPath);
                                _selectedGraphInfo = assetInfo;
                                _selectedVariable = null;
                                AddSelectionHistory();
                            }
                        }
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
            }
        }

        void DrawHistory()
        {
            historyCount = EditorGUILayout.IntField("HistoryCount", historyCount, GUILayout.ExpandHeight(false));
            _historyScrollPosition =
                GUILayout.BeginScrollView(_historyScrollPosition, "box", GUILayout.ExpandHeight(false),
                    GUILayout.MaxHeight(300));

            for (var index = 0; index < _historyList.Count; index++)
            {
                var assetInfo = _historyList[index];
                if (assetInfo.source.Equals("Graph"))
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(assetInfo.assetPath);

                    EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                    var icon = EditorGUIUtility.ObjectContent(assetInfo.reference, assetInfo.type);
                    icon.text = fileName;
                    if (GUILayout.Button(icon))
                    {
                        PingObjectInProject(assetInfo.assetPath);
                        _selectedGraphInfo = assetInfo;
                        _selectedVariable = null;
                    }
                }
                else if (assetInfo.source.Equals("Embed"))
                {
                    if (assetInfo.reference == null)
                    {
                        _historyList.RemoveAt(index);
                        if (_selectedGraphInfo == assetInfo)
                        {
                            _selectedGraphInfo = null;
                            _selectedVariable = null;
                        }

                        GUILayout.Label("Missing");
                    }
                    else
                    {
                        string fileName = assetInfo.reference.name;
                        string gameObjectPath = SceneManager.GetActiveScene().path;

                        if (GUILayout.Button(fileName))
                        {
                            PingObjectInProject(gameObjectPath);
                            _selectedGraphInfo = assetInfo;
                            _selectedVariable = null;
                        }
                    }
                }
            }

            GUILayout.EndScrollView();
        }

        bool FilterDisplayGraph(Regex pattern, GraphInfo graph)
        {
            if (string.IsNullOrEmpty(_graphFilterString)) return true;
            return pattern.IsMatch(graph.title);
        }

        private void PingObjectInProject(string assetPath)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private List<VariableInfo> GetVariables(GraphInfo graphInfo, VariableKind kind)
        {
            var result = new List<VariableInfo>();
            if (graphInfo == null)
                return result;

            if (graphInfo.source.Equals("Graph"))
            {
                var scriptAsset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(graphInfo.assetPath);
                if (scriptAsset != null)
                {
                    if (scriptAsset.GetReference().graph is not FlowGraph flowGraph) return result;
                    var baseRef = scriptAsset.GetReference().AsReference();
                    result.AddRange(FindVariableUnits(baseRef, kind));
                }

                var stateAsset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(graphInfo.assetPath);
                if (stateAsset != null)
                {
                    if (stateAsset.GetReference().graph is not StateGraph stateGraph) return result;
                    var baseRef = stateAsset.GetReference().AsReference();
                    result.AddRange(FindVariableUnits(baseRef, kind));
                }
            }
            else if (graphInfo.source.Equals("Embed") && graphInfo.reference != null)
            {
                try
                {
                    var selectedScriptAsset = graphInfo.reference.GetComponentInChildren<ScriptMachine>();
                    if (selectedScriptAsset != null && selectedScriptAsset.GetReference() != null)
                    {
                        if (selectedScriptAsset.GetReference().graph is not FlowGraph flowGraph) return result;
                        var baseRef = selectedScriptAsset.GetReference().AsReference();
                        result.AddRange(FindVariableUnits(baseRef, kind));
                    }

                    var selectedStateAsset = graphInfo.reference.GetComponentInChildren<StateMachine>();
                    if (selectedStateAsset != null && selectedStateAsset.GetReference() != null)
                    {
                        if (selectedStateAsset.GetReference().graph is not StateGraph stateGraph) return result;
                        var baseRef = selectedStateAsset.GetReference().AsReference();
                        result.AddRange(FindVariableUnits(baseRef, kind));
                    }
                }
                catch
                {
                    // 如果退出预制体模式可能会失败
                }
            }

            return result;
        }

        private List<VariableInfo> FindVariableUnits(GraphReference reference, VariableKind kind)
        {
            var result = new List<VariableInfo>();

            // 遍历FlowGraph中的所有Unit
            foreach (var (graphRef, unit) in UnitUtility.TraverseFlowGraphUnit(reference))
            {
                // 检查是否是UnifiedVariableUnit
                if (unit is UnifiedVariableUnit unifiedVarUnit && unifiedVarUnit.kind == kind)
                {
                    var varInfo = new VariableInfo
                    {
                        Reference = graphRef,
                        Unit = unit,
                        Path = UnitUtility.GetGraphPath(graphRef),
                        Kind = kind,
                        Name = unit.ToString().Split('#')[0]
                    };

                    // 获取defaultName
                    var namePort = unifiedVarUnit.valueInputs.FirstOrDefault(p => p.key == "name");
                    if (namePort != null && namePort.connection == null)
                    {
                        if (unit.defaultValues.TryGetValue(namePort.key, out var val)) //
                            varInfo.DefaultName = val.ToString();
                    }
                    else
                    {
                        varInfo.DefaultName = "";
                    }

                    if (!string.IsNullOrEmpty(varInfo.DefaultName))
                    {
                        result.Add(varInfo);
                    }
                }
                // 检查是否是旧版VariableUnit
                else if (unit is UnifiedVariableUnit varUnit)
                {
                    bool isMatchingKind = false;

                    // 根据接口类型判断变量类型
                    switch (kind)
                    {
                        case VariableKind.Flow:
                            isMatchingKind = !(unit is IGraphVariableUnit || unit is IObjectVariableUnit ||
                                               unit is ISceneVariableUnit || unit is IApplicationVariableUnit ||
                                               unit is ISavedVariableUnit);
                            break;
                        case VariableKind.Graph:
                            isMatchingKind = unit is IGraphVariableUnit;
                            break;
                        case VariableKind.Object:
                            isMatchingKind = unit is IObjectVariableUnit;
                            break;
                        case VariableKind.Scene:
                            isMatchingKind = unit is ISceneVariableUnit;
                            break;
                        case VariableKind.Application:
                            isMatchingKind = unit is IApplicationVariableUnit;
                            break;
                        case VariableKind.Saved:
                            isMatchingKind = unit is ISavedVariableUnit;
                            break;
                    }

                    if (isMatchingKind)
                    {
                        var varInfo = new VariableInfo
                        {
                            Reference = graphRef,
                            Unit = unit,
                            Path = UnitUtility.GetGraphPath(graphRef),
                            Kind = kind,
                            Name = unit.ToString().Split('#')[0],
                            DefaultName = varUnit.defaultValues[nameof(name)].ToString()
                        };

                        if (!string.IsNullOrEmpty(varInfo.DefaultName))
                        {
                            result.Add(varInfo);
                        }
                    }
                }
            }

            // 遍历StateGraph中的所有Unit
            foreach (var (graphRef, unit) in UnitUtility.TraverseStateGraphUnit(reference))
            {
                // 检查是否是UnifiedVariableUnit
                if (unit is UnifiedVariableUnit unifiedVarUnit && unifiedVarUnit.kind == kind)
                {
                    var varInfo = new VariableInfo
                    {
                        Reference = graphRef,
                        Unit = unit,
                        Path = UnitUtility.GetGraphPath(graphRef),
                        Kind = kind,
                        Name = unit.ToString().Split('#')[0]
                    };

                    // 获取defaultName
                    var namePort = unifiedVarUnit.valueInputs.FirstOrDefault(p => p.key == "name");
                    if (namePort != null && namePort.connection == null)
                    {
                        if (unit.defaultValues.TryGetValue(namePort.key, out var val)) //
                            varInfo.DefaultName = val.ToString();
                    }
                    else
                    {
                        varInfo.DefaultName = "";
                    }

                    if (!string.IsNullOrEmpty(varInfo.DefaultName))
                    {
                        result.Add(varInfo);
                    }
                }
                // 检查是否是旧版VariableUnit
                else if (unit is UnifiedVariableUnit varUnit)
                {
                    bool isMatchingKind = false;

                    // 根据接口类型判断变量类型
                    switch (kind)
                    {
                        case VariableKind.Flow:
                            isMatchingKind = !(unit is IGraphVariableUnit || unit is IObjectVariableUnit ||
                                               unit is ISceneVariableUnit || unit is IApplicationVariableUnit ||
                                               unit is ISavedVariableUnit);
                            break;
                        case VariableKind.Graph:
                            isMatchingKind = unit is IGraphVariableUnit;
                            break;
                        case VariableKind.Object:
                            isMatchingKind = unit is IObjectVariableUnit;
                            break;
                        case VariableKind.Scene:
                            isMatchingKind = unit is ISceneVariableUnit;
                            break;
                        case VariableKind.Application:
                            isMatchingKind = unit is IApplicationVariableUnit;
                            break;
                        case VariableKind.Saved:
                            isMatchingKind = unit is ISavedVariableUnit;
                            break;
                    }

                    if (isMatchingKind)
                    {
                        var varInfo = new VariableInfo
                        {
                            Reference = graphRef,
                            Unit = unit,
                            Path = UnitUtility.GetGraphPath(graphRef),
                            Kind = kind,
                            Name = unit.ToString().Split('#')[0],
                            DefaultName = varUnit.defaultValues[nameof(name)].ToString()
                        };

                        if (!string.IsNullOrEmpty(varInfo.DefaultName))
                        {
                            result.Add(varInfo);
                        }
                    }
                }
            }

            return result;
        }

        private void RenameVariables(GraphInfo graphInfo, VariableKind kind, string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
                return;

            // 检查新旧名称是否相同，如果相同则跳过
            if (oldName == newName)
            {
                Debug.Log($"变量名称未更改: {oldName}");
                return;
            }

            var variables = GetVariables(graphInfo, kind);
            var toRename = variables.Where(v => v.DefaultName == oldName).ToList();

            if (toRename.Count == 0)
                return;

            Undo.RecordObject(graphInfo.reference, "Rename Variables");

            // 更新变量引用节点
            foreach (var variable in toRename)
            {
                if (variable.Unit is UnifiedVariableUnit unifiedVarUnit)
                {
                    var namePort = unifiedVarUnit.valueInputs.FirstOrDefault(p => p.key == "name");
                    if (namePort != null && namePort.connection == null)
                    {
                        namePort.SetDefaultValue(newName);
                    }
                }
                // 对于旧版VariableUnit，我们无法直接修改defaultName，因为它是只读的
                // 但我们可以修改name端口的默认值
                else if (variable.Unit is UnifiedVariableUnit varUnit)
                {
                    var namePort = varUnit.valueInputs.FirstOrDefault(p => p.key == "name");
                    if (namePort != null && namePort.connection == null)
                    {
                        namePort.SetDefaultValue(newName);
                    }
                }
            }

            // 更新变量声明
            UpdateVariableDeclarations(graphInfo, kind, oldName, newName);

            // 保存更改
            if (graphInfo.source.Equals("Graph"))
            {
                AssetDatabase.SaveAssets();
            }

            EditorUtility.SetDirty(graphInfo.reference);
        }

        private void UpdateVariableDeclarations(GraphInfo graphInfo, VariableKind kind, string oldName, string newName)
        {
            if (graphInfo.source.Equals("Graph"))
            {
                var scriptAsset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(graphInfo.assetPath);
                if (scriptAsset != null)
                {
                    if (scriptAsset.GetReference().graph is FlowGraph flowGraph)
                    {
                        UpdateGraphVariableDeclarations(flowGraph, kind, oldName, newName);
                    }
                }

                var stateAsset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(graphInfo.assetPath);
                if (stateAsset != null)
                {
                    if (stateAsset.GetReference().graph is StateGraph stateGraph)
                    {
                        UpdateGraphVariableDeclarations(stateGraph, kind, oldName, newName);
                    }
                }
            }
            else if (graphInfo.source.Equals("Embed") && graphInfo.reference != null)
            {
                try
                {
                    var selectedScriptAsset = graphInfo.reference.GetComponentInChildren<ScriptMachine>();
                    if (selectedScriptAsset != null && selectedScriptAsset.GetReference() != null)
                    {
                        if (selectedScriptAsset.GetReference().graph is FlowGraph flowGraph)
                        {
                            UpdateGraphVariableDeclarations(flowGraph, kind, oldName, newName);
                        }
                    }

                    var selectedStateAsset = graphInfo.reference.GetComponentInChildren<StateMachine>();
                    if (selectedStateAsset != null && selectedStateAsset.GetReference() != null)
                    {
                        if (selectedStateAsset.GetReference().graph is StateGraph stateGraph)
                        {
                            UpdateGraphVariableDeclarations(stateGraph, kind, oldName, newName);
                        }
                    }
                }
                catch
                {
                    // 如果退出预制体模式可能会失败
                }
            }
        }

        private void UpdateGraphVariableDeclarations(IGraph graph, VariableKind kind, string oldName, string newName)
        {
            // 获取图表中的变量声明

            switch (graph)
            {
                // 递归处理子图表
                case FlowGraph flowGraph:
                {
                    var declarations = flowGraph.variables;
                    // 检查变量是否存在
                    try
                    {
                        var declaration = declarations.GetDeclaration(oldName);
                        if (declaration != null)
                        {
                            Debug.Log("Please rename the variable in the graph inspector manually.");
                        }
                    }
                    catch
                    {
                        // ignored
                    }

                    foreach (var unit in flowGraph.units)
                    {
                        if (unit is SubgraphUnit subgraphUnit)
                        {
                            var subGraph = subgraphUnit.nest.embed ?? subgraphUnit.nest.graph;
                            if (subGraph != null)
                            {
                                UpdateGraphVariableDeclarations(subGraph, kind, oldName, newName);
                            }
                        }
                        else if (unit is StateUnit stateUnit)
                        {
                            var stateGraph = stateUnit.nest.embed ?? stateUnit.nest.graph;
                            if (stateGraph != null)
                            {
                                UpdateGraphVariableDeclarations(stateGraph, kind, oldName, newName);
                            }
                        }
                    }

                    break;
                }
                case StateGraph stateGraph:
                {
                    foreach (var state in stateGraph.states)
                    {
                        if (state is FlowState flowState)
                        {
                            var stateFlowGraph = flowState.nest.embed ?? flowState.nest.graph;
                            if (stateFlowGraph != null)
                            {
                                UpdateGraphVariableDeclarations(stateFlowGraph, kind, oldName, newName);
                            }
                        }
                        else if (state is SuperState superState)
                        {
                            var subStateGraph = superState.nest.embed ?? superState.nest.graph;
                            if (subStateGraph != null)
                            {
                                UpdateGraphVariableDeclarations(subStateGraph, kind, oldName, newName);
                            }
                        }
                    }

                    foreach (var transition in stateGraph.transitions)
                    {
                        if (transition is FlowStateTransition flowStateTransition)
                        {
                            var transitionGraph = flowStateTransition.nest.embed ?? flowStateTransition.nest.graph;
                            if (transitionGraph != null)
                            {
                                UpdateGraphVariableDeclarations(transitionGraph, kind, oldName, newName);
                            }
                        }
                    }

                    break;
                }
            }
        }

        private void AddSelectionHistory()
        {
            if (_selectedGraphInfo == null) return;

            // 如果历史记录中已经有这个图，先移除它
            _historyList.RemoveAll(x =>
                (x.source == "Graph" && x.assetPath == _selectedGraphInfo.assetPath) ||
                (x.source == "Embed" && x.reference == _selectedGraphInfo.reference));

            // 添加到历史记录的开头
            _historyList.Insert(0, _selectedGraphInfo);

            // 限制历史记录数量
            while (_historyList.Count > historyCount)
            {
                _historyList.RemoveAt(_historyList.Count - 1);
            }
        }

        private void OnSelectionChanged()
        {
            var selectedObject = Selection.activeObject;
            var dirty = false;

            if (selectedObject != null && selectedObject is ScriptGraphAsset or StateGraphAsset)
            {
                string assetPath = AssetDatabase.GetAssetPath(selectedObject);

                var existed = _graphList.Any(x => x.assetPath.Equals(assetPath));
                if (!existed)
                {
                    var graphInfo = new GraphInfo()
                    {
                        title = selectedObject.name,
                        source = "Graph",
                        type = selectedObject.GetType(),
                        reference = selectedObject,
                        assetPath = assetPath
                    };
                    _graphList.Add(graphInfo);
                    _selectedGraphInfo = graphInfo;
                    _selectedVariable = null;
                    dirty = true;
                }
            }
            else if (selectedObject != null && selectedObject is GameObject gameObject)
            {
                var sm = gameObject.GetComponent<ScriptMachine>();
                if (sm != null)
                {
                    var existed = _graphList.Any(x => x.reference.Equals(selectedObject));
                    if (!existed)
                    {
                        var graphInfo = new GraphInfo()
                        {
                            title = selectedObject.name,
                            source = "Embed",
                            type = typeof(ScriptGraphAsset),
                            reference = selectedObject,
                            assetPath = "",
                        };
                        _graphList.Add(graphInfo);
                        _selectedGraphInfo = graphInfo;
                        _selectedVariable = null;
                        dirty = true;
                    }
                }

                var state = gameObject.GetComponent<StateMachine>();
                if (state != null)
                {
                    var existed = _graphList.Any(x => x.reference.Equals(selectedObject));
                    if (!existed)
                    {
                        var graphInfo = new GraphInfo()
                        {
                            title = selectedObject.name,
                            source = "Embed",
                            type = typeof(StateGraphAsset),
                            reference = selectedObject,
                            assetPath = "",
                        };
                        _graphList.Add(graphInfo);
                        _selectedGraphInfo = graphInfo;
                        _selectedVariable = null;
                        dirty = true;
                    }
                }
            }

            if (dirty)
            {
                AddSelectionHistory();
            }
        }
    }
}