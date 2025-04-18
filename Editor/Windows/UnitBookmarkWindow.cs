using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.IO;
using UnityEngine.Serialization;

namespace Unity.VisualScripting.Community
{
    public class UnitBookmarkWindow : EditorWindow
    {
        [Serializable]
        class Bookmark
        {
            public string assetPath;
            public string path;
            public string type;
            public string name;
            public string meta;

            public string DisplayLabel
            {
                get
                {
                    var fName = Path.GetFileNameWithoutExtension(assetPath);
                    var label = $"{fName}=>{path}{name}";
                    if (!string.IsNullOrEmpty(meta))
                    {
                        label += " (" + meta + ")";
                    }

                    return label;
                }
            }
        }

        class UnitInfo
        {
            public GraphReference AssetReference;
            public GraphReference Reference;
            public IUnit Unit;

            public string Name;

            // public string Path;
            public string Meta;
        }

        // private string _unitFilterString = "";
        // private string _graphFilterString = "";
        [SerializeField] List<Bookmark> _bookmarkList = new();

        Vector2 _unitScrollPosition = Vector2.zero;
        Vector2 _linkScrollPosition = Vector2.zero;

        private Bookmark _activeBookmark;

        [MenuItem("Window/UVS Community/Unit Bookmark")]
        public static void Open()
        {
            var window = GetWindow<UnitBookmarkWindow>();
            window.titleContent = new GUIContent("Unit Bookmark");
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            _unitScrollPosition = GUILayout.BeginScrollView(_unitScrollPosition, "box");
            for (var index = 0; index < _bookmarkList.Count; index++)
            {
                DisplayBookmark(index);
            }

            GUILayout.EndScrollView();
            DisplayLinked();

            if (GUILayout.Button("Add Selected", GUILayout.ExpandHeight(false)))
            {
                AddBookmark();
            }


            GUILayout.EndHorizontal(); // 结束整体布局
        }

        void DisplayBookmark(int index)
        {
            GUILayout.BeginHorizontal();
            var bookmark = _bookmarkList[index];
            if (GUILayout.Button("x", GUILayout.ExpandWidth(false)))
            {
                _bookmarkList.RemoveAt(index);
            }

            if (IsBookmarkValid(bookmark))
            {
                var iconType = Type.GetType(bookmark.type);
                var tex = Icons.Icon(iconType);
                var icon = new GUIContent(tex[IconSize.Small])
                {
                    text = bookmark.DisplayLabel
                };
                if (GUILayout.Button(icon,
                        EditorStyles.linkLabel, GUILayout.MaxHeight(IconSize.Small + 4)))
                {
                    var unit = BuildUnitInfo(bookmark);
                    if (unit != null && unit.Unit != null)
                    {
                        _activeBookmark = bookmark;
                        UnitUtility.FocusUnit(unit.Reference, unit.Unit);
                    }
                    else
                    {
                        _activeBookmark = null;
                        Debug.LogError($"Missing {bookmark.name}:{bookmark.assetPath}");
                    }
                }
            }
            else
            {
                GUILayout.Label($"Missing {bookmark.name}:{bookmark.assetPath}");
            }

            GUILayout.EndHorizontal();
        }

        void DisplayLinked()
        {
            if (!EditorApplication.isPlaying || _activeBookmark == null)
            {
                return;
            }


            var iconType = Type.GetType(_activeBookmark.type);
            var tex = Icons.Icon(iconType);
            var icon = new GUIContent(tex[IconSize.Small])
            {
                text = _activeBookmark.DisplayLabel
            };
            GUILayout.Label("Active Objects");
            GUILayout.Label(icon, GUILayout.MaxHeight(IconSize.Small + 4));
            var asset = AssetDatabase.LoadAssetAtPath<Object>(_activeBookmark.assetPath);
            Dictionary<GameObject, (GraphReference, Unit)> linkedGameObjectMap = new();
            var flowMachines = FindObjectsOfType<ScriptMachine>(true);
            foreach (var machine in flowMachines)
            {
                if (machine.GetReference() == null) continue;
                foreach (var (reference, unit) in UnitUtility.TraverseFlowGraphUnit(machine.GetReference()
                             .AsReference()))
                {
                    if (unit.ToString() == _activeBookmark.name)
                    {
                        linkedGameObjectMap[machine.gameObject] = (reference, unit);
                    }
                }
            }

            var stateMachines = FindObjectsOfType<StateMachine>(true);
            foreach (var machine in stateMachines)
            {
                if (machine.GetReference() == null) continue;
                foreach (var (reference, unit) in UnitUtility.TraverseStateGraphUnit(machine.GetReference()
                             .AsReference()))
                {
                    if (unit.ToString() == _activeBookmark.name)
                    {
                        linkedGameObjectMap[machine.gameObject] = (reference, unit);
                    }
                }
            }


            _linkScrollPosition = GUILayout.BeginScrollView(_linkScrollPosition, "box", GUILayout.ExpandHeight(false));
            foreach (var (go, valueTuple) in linkedGameObjectMap)
            {
                if (GUILayout.Button(go.name, EditorStyles.linkLabel))
                {
                    Selection.activeObject = go;
                    EditorGUIUtility.PingObject(go);

                    var (reference, unit) = valueTuple;
                    if (unit != null)
                    {
                        UnitUtility.FocusUnit(reference, unit);
                    }
                }
            }

            GUILayout.EndScrollView();
        }


        bool IsBookmarkValid(Bookmark bookmark)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(bookmark.assetPath);
            return asset != null && bookmark.type != null;
        }

        GraphReference LoadAssetReference(Bookmark bookmark)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(bookmark.assetPath);
            if (asset == null)
            {
                return null;
            }

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

        UnitInfo BuildUnitInfo(Bookmark bookmark)
        {
            var detail = new UnitInfo
            {
                AssetReference = LoadAssetReference(bookmark)
            };
            if (detail.AssetReference == null)
            {
                return null;
            }

            detail.Reference = UnitUtility.GetUnitGraphReference(detail.AssetReference, bookmark.name);
            detail.Unit = FindNode(detail.Reference, bookmark.name);
            if (detail.Unit == null) return null;
            detail.Name = detail.Unit.ToString().Split('#')[0];
            detail.Meta = UnitUtility.UnitBrief(detail.Unit);
            return detail;
        }

        IUnit FindNode(GraphReference reference, string nodeName)
        {
            if (reference == null) return null;
            foreach (var enumerator in UnitUtility.TraverseFlowGraphUnit(reference))
            {
                if (enumerator.Item2.ToString() == nodeName)
                {
                    return enumerator.Item2;
                }
            }

            foreach (var enumerator in UnitUtility.TraverseStateGraphUnit(reference))
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
                        var bookmark = new Bookmark()
                        {
                            assetPath = assetPath,
                            name = uName,
                        };
                        var info = BuildUnitInfo(bookmark);
                        bookmark.meta = info.Meta;
                        bookmark.path = UnitUtility.GetGraphPath(info.Reference);
                        bookmark.type = info.Unit.GetType().AssemblyQualifiedName;
                        _bookmarkList.Add(bookmark);
                    }
                }
            }
        }
    }
}