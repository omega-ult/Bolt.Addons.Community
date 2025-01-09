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
    public class UnitIndexWindow : EditorWindow
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

        class UnitInfo
        {
            public GraphReference Reference;
            public IUnit Unit;
            public string Name;
            public string Path;
            public string Meta;
        }

        [SerializeField] private List<GraphInfo> _graphList = new();
        private List<GraphInfo> _historyList = new();
        private GraphInfo _selectedGraphInfo;
        private string _unitFilterString = "";
        private string _graphFilterString = "";

        Vector2 _unitScrollPosition = Vector2.zero;
        Vector2 _graphScrollPosition = Vector2.zero;
        Vector2 _historyScrollPosition = Vector2.zero;

        [SerializeField] private int historyCount = 50;

        [MenuItem("Window/UVS Community/Node Index")]
        public static void Open()
        {
            var window = GetWindow<UnitIndexWindow>();
            window.titleContent = new GUIContent("Node Index");
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(250));
            DrawGraphList();
            DrawHistory();
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
            var units = GetDetailUnit(_selectedGraphInfo);
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
                    var icon = new GUIContent(tex[IconSize.Small]);
                    icon.text = label;
                    // GUILayout.ic
                    if (GUILayout.Button(icon,
                            EditorStyles.linkLabel, GUILayout.MaxHeight(IconSize.Small + 4)))
                    {
                        UnitUtility.FocusUnit(unit.Reference, unit.Unit);
                    }
                }
            }


            GUILayout.EndScrollView(); // 结束滚动视图
            GUILayout.EndVertical();


            GUILayout.EndHorizontal(); // 结束整体布局
        }

        void DrawGraphList()
        {
            // GUILayout.
            // 如果没有记录的 Script Graph 文件，显示提示
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
                // 显示记录的每个 Script Graph 文件
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
                        }
                    }

                    if (assetInfo.source.Equals("Graph"))
                    {
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(assetInfo.assetPath);

                        // 使用按钮显示每个 Script Graph 文件路径
                        EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                        var icon = EditorGUIUtility.ObjectContent(assetInfo.reference, assetInfo.type);
                        icon.text = fileName;
                        if (GUILayout.Button(icon))
                        {
                            // 用户点击某个按钮时，跳转并高亮显示该文件
                            PingObjectInProject(assetInfo.assetPath);
                            // var detail = GetDetail(assetInfo.AssetPath);
                            _selectedGraphInfo = assetInfo;
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
                            }

                            GUILayout.Label("Missing");
                        }
                        else
                        {
                            string fileName = assetInfo.reference.name;
                            string gameObjectPath = SceneManager.GetActiveScene().path;

                            // 使用按钮显示每个 Script Graph 文件路径
                            if (GUILayout.Button(fileName))
                            {
                                // 用户点击某个按钮时，跳转并高亮显示该文件
                                PingObjectInProject(gameObjectPath);
                                _selectedGraphInfo = assetInfo;
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

                    // 使用按钮显示每个 Script Graph 文件路径
                    EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                    var icon = EditorGUIUtility.ObjectContent(assetInfo.reference, assetInfo.type);
                    icon.text = fileName;
                    if (GUILayout.Button(icon))
                    {
                        // 用户点击某个按钮时，跳转并高亮显示该文件
                        PingObjectInProject(assetInfo.assetPath);
                        // var detail = GetDetail(assetInfo.AssetPath);
                        _selectedGraphInfo = assetInfo;
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
                        }

                        GUILayout.Label("Missing");
                    }
                    else
                    {
                        string fileName = assetInfo.reference.name;
                        string gameObjectPath = SceneManager.GetActiveScene().path;

                        // 使用按钮显示每个 Script Graph 文件路径
                        if (GUILayout.Button(fileName))
                        {
                            // 用户点击某个按钮时，跳转并高亮显示该文件
                            PingObjectInProject(gameObjectPath);
                            _selectedGraphInfo = assetInfo;
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
                detail.Path = UnitUtility.GetUnitPath(reference);
                detail.Name = unit.ToString().Split('#')[0];
                detail.Unit = unit;
                detail.Reference = reference;
                detail.Meta = UnitUtility.UnitBrief(unit);
                result.Add(detail);
            }

            return result;
        }

        private Dictionary<string, List<UnitInfo>> GetDetailUnit(GraphInfo graphInfo)
        {
            Dictionary<string, List<UnitInfo>> result = new();
            if (graphInfo == null)
                return result;
            List<UnitInfo> fetched = null;

            if (graphInfo.source.Equals("Graph"))
            {
                var scriptAsset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(graphInfo.assetPath);
                if (scriptAsset != null)
                {
                    if (scriptAsset.GetReference().graph is not FlowGraph flowGraph) return result;
                    var baseRef = scriptAsset.GetReference().AsReference();
                    fetched = BuildUnitDetail(UnitUtility.TraverseFlowGraphUnit(baseRef));
                }

                var stateAsset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(graphInfo.assetPath);
                if (stateAsset != null)
                {
                    if (stateAsset.GetReference().graph is not StateGraph stateGraph) return result;
                    var baseRef = stateAsset.GetReference().AsReference();
                    fetched = BuildUnitDetail(UnitUtility.TraverseStateGraphUnit(baseRef));
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
                        fetched = BuildUnitDetail(UnitUtility.TraverseFlowGraphUnit(baseRef));
                    }

                    var selectedStateAsset = graphInfo.reference.GetComponentInChildren<StateMachine>();
                    if (selectedStateAsset != null && selectedStateAsset.GetReference() != null)
                    {
                        if (selectedStateAsset.GetReference().graph is not StateGraph stateGraph) return result;
                        var baseRef = selectedStateAsset.GetReference().AsReference();
                        fetched = BuildUnitDetail(UnitUtility.TraverseStateGraphUnit(baseRef));
                    }
                }
                catch
                {
                    // pass
                    // failed if exit from prefab mode.
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

                foreach (var element in UnitUtility.TraverseFlowGraphUnit(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    var node = UnitUtility.GetUnitPath(reference);
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
                foreach (var element in UnitUtility.TraverseStateGraphUnit(baseRef))
                {
                    var reference = element.Item1;
                    var unit = element.Item2;
                    //Debug.Log(unit);

                    var node = UnitUtility.GetUnitPath(reference);
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


        void SortHistory()
        {
            _graphList.Sort((GraphInfo x, GraphInfo y) => x.title.CompareTo(y.title));
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
                    dirty = true;
                }
            }
            else if (selectedObject != null && selectedObject is GameObject)
            {
                var sm = selectedObject.GetComponent<ScriptMachine>();

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
                            assetPath = "", //selectedObject.name
                        };
                        _graphList.Add(graphInfo);
                        _selectedGraphInfo = graphInfo;
                        dirty = true;
                    }
                }

                var state = selectedObject.GetComponent<StateMachine>();
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
                            assetPath = "", //selectedObject.name
                        };
                        _graphList.Add(graphInfo);
                        _selectedGraphInfo = graphInfo;
                        dirty = true;
                    }
                }
            }

            if (!dirty) return;
            AddSelectionHistory();
            SortHistory();
            Repaint();
        }

        void AddSelectionHistory()
        {
            if (_selectedGraphInfo != null)
            {
                _historyList.Add(_selectedGraphInfo);
                if (_historyList.Count > historyCount)
                {
                    _historyList = _historyList.TakeLast(historyCount).ToList();
                }
            }
        }
    }
}