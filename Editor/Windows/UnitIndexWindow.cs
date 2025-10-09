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
            public GraphReference graph;
        }

        class UnitInfo
        {
            public GraphReference Reference;
            public IUnit Unit;
            public string Name;
            public string Path;
            public string Meta;
            public List<string> Values;
        }

        [SerializeField] private List<GraphInfo> _graphList = new();
        private GraphInfo _selectedGraphInfo;
        private string _unitFilterString = "";

        Vector2 _unitScrollPosition = Vector2.zero;

        [SerializeField] private int historyCount = 50;
        [SerializeField] private float leftPanelWidth = 250f; // 添加左侧面板宽度变量
        [SerializeField] private float historyPanelHeight = 300f; // 添加历史面板高度变量
        // private bool isDragging = false; // 是否正在拖拽

        [MenuItem("Window/UVS Community/Unit Index")]
        public static void Open()
        {
            var window = GetWindow<UnitIndexWindow>();
            window.titleContent = new GUIContent("Unit Index");
        }


        private void OnGUI()
        {
            GUILayout.BeginHorizontal();

            // // 使用动态宽度而不是固定值
            // GUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));
            // DrawGraphList();
            // GUILayout.EndVertical();

            // 添加拖拽分隔线
            // Rect dragRect = new Rect(leftPanelWidth, 0, 5, position.height);
            // EditorGUI.DrawRect(dragRect, new Color(0.1f, 0.1f, 0.1f, 0.0f));

            // // 处理拖拽事件
            // HandleDragEvents(dragRect);
            //
            // // 鼠标悬停时改变光标
            // EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.ResizeHorizontal);

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
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("   ", GUILayout.ExpandWidth(false));
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
                    GUILayout.EndHorizontal();
                }
            }


            GUILayout.EndScrollView(); // 结束滚动视图
            GUILayout.EndVertical();


            GUILayout.EndHorizontal(); // 结束整体布局
        }

        void OnActiveContextChanged(IGraphContext context)
        {
            if (GraphWindow.active == null || GraphWindow.active.reference == null)
            {
                return;
            }

            var entry = UnitUtility.GetEntryContext(GraphWindow.active.reference);
            var activeGraph = GraphWindow.active.reference;
            var graphInfo = new GraphInfo()
            {
                title = System.IO.Path.GetFileNameWithoutExtension(entry.assetPath),
                graph = activeGraph,
            };
            _selectedGraphInfo = graphInfo;
            Repaint();
        }


        bool FilterDisplayUnit(Regex pattern, UnitInfo unit)
        {
            if (string.IsNullOrEmpty(_unitFilterString)) return true;
            if (pattern.IsMatch(unit.Name)) return true;
            var metaFit = unit.Meta != null && pattern.IsMatch(unit.Meta);
            var valueFit = unit.Values.Any(pattern.IsMatch);
            return metaFit || valueFit;
        }


        private void OnEnable()
        {
            GraphWindow.activeContextChanged += OnActiveContextChanged;
        }

        private void OnDisable()
        {
            GraphWindow.activeContextChanged -= OnActiveContextChanged;
        }

        List<UnitInfo> BuildUnitDetail(IEnumerable<(GraphReference, Unit)> iterator)
        {
            var result = new List<UnitInfo>();
            foreach (var element in iterator)
            {
                var detail = new UnitInfo();
                var reference = element.Item1;
                var unit = element.Item2;
                detail.Path = UnitUtility.GetGraphPath(reference);
                detail.Name = unit.ToString().Split('#')[0];
                detail.Unit = unit;
                detail.Reference = reference;
                detail.Meta = UnitUtility.UnitBrief(unit);
                detail.Values = UnitUtility.UnitValues(unit);
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

            var graphRef = graphInfo.graph; 
            if (graphRef == null) return result;
            {
                if (graphRef.graph is FlowGraph flowGraph)
                {
                    fetched = BuildUnitDetail(UnitUtility.TraverseFlowGraphUnit(graphRef));
                }
                else if (graphRef.graph is StateGraph stateGraph)
                {
                    fetched = BuildUnitDetail(UnitUtility.TraverseStateGraphUnit(graphRef));
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

    }
}