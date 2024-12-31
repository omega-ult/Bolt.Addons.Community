using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.IO;

namespace Unity.VisualScripting.Community
{
    public class UnitBookmarkWindow : EditorWindow
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
            var window = GetWindow<UnitBookmarkWindow>();
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
                        UnitUtility.FocusUnit(unit.Reference, unit.Unit);
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
                    detail.Path = UnitUtility.GetUnitPath(baseRef);
                    detail.Reference = baseRef;
                    break;
                }
                case StateGraphAsset stateGraphAsset:
                {
                    var baseRef = stateGraphAsset.GetReference().AsReference();
                    detail.Path = UnitUtility.GetUnitPath(baseRef);
                    detail.Reference = baseRef;
                    break;
                }
                default:
                    return null;
            }

            detail.Unit = FindNode(detail.Reference, bookmark.name);
            if (detail.Unit == null) return null;
            detail.Name = detail.Unit.ToString().Split('#')[0];
            detail.Meta = UnitUtility.UnitBrief(detail.Unit);
            return detail;
        }

        IUnit FindNode(GraphReference reference, string nodeName)
        {
            if (reference == null) return null;
            foreach (var enumerator in UnitUtility.TraverseFlowGraph(reference))
            {
                if (enumerator.Item2.ToString() == nodeName)
                {
                    return enumerator.Item2;
                }
            }

            foreach (var enumerator in UnitUtility.TraverseStateGraph(reference))
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

    }
}