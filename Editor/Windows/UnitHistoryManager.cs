using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.IO;
using UnityEditor.SceneManagement;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// 静态管理器，用于监听和记录Unit选择历史，即使在UnitHistoryWindow关闭时也能工作
    /// </summary>
    [InitializeOnLoad]
    public static class UnitHistoryManager
    {
        [Serializable]
        public class UnitHistoryEntry
        {
            public string scenePath;
            public string assetPath;
            public string path;
            public string type;
            public string name;
            public string meta;
            public DateTime timestamp;

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

        public class UnitInfo
        {
            public GraphReference AssetReference;
            public GraphReference Reference;
            public IUnit Unit;
            public string Name;
            public string Meta;
        }

        // 使用ScriptableSingleton来持久化存储历史数据
        private class HistoryData : ScriptableSingleton<HistoryData>
        {
            [SerializeField] public List<UnitHistoryEntry> historyEntries = new List<UnitHistoryEntry>();
            [SerializeField] public int maxHistoryCount = 50;
            [SerializeField] public bool autoCleanInvalidEntries = true;
        }

        private static HashSet<IUnit> _lastSelectedUnits = new HashSet<IUnit>();

        // 标记是否是从历史窗口触发的跳转，防止从历史窗口点击时产生新的历史记录
        private static bool _isJumpingFromHistory = false;

        // 静态构造函数，在编辑器加载时自动执行
        static UnitHistoryManager()
        {
            EditorApplication.update += CheckGraphWindowSelection;
        }

        public static void SetJumpingFromHistory(bool value)
        {
            _isJumpingFromHistory = value;
        }

        public static List<UnitHistoryEntry> GetHistoryEntries()
        {
            return HistoryData.instance.historyEntries;
        }

        public static int GetMaxHistoryCount()
        {
            return HistoryData.instance.maxHistoryCount;
        }

        public static void SetMaxHistoryCount(int count)
        {
            HistoryData.instance.maxHistoryCount = count;
        }

        public static bool GetAutoCleanInvalidEntries()
        {
            return HistoryData.instance.autoCleanInvalidEntries;
        }

        public static void SetAutoCleanInvalidEntries(bool value)
        {
            HistoryData.instance.autoCleanInvalidEntries = value;
        }

        public static void ClearHistory()
        {
            HistoryData.instance.historyEntries.Clear();
        }

        public static void RemoveHistoryEntry(int index)
        {
            if (index >= 0 && index < HistoryData.instance.historyEntries.Count)
            {
                HistoryData.instance.historyEntries.RemoveAt(index);
            }
        }

        private static void CheckGraphWindowSelection()
        {
            if (GraphWindow.active == null || GraphWindow.active.context == null) return;

            var currentSelection = GraphWindow.active.context.selection;
            if (currentSelection == null) return;

            // 获取当前选中的Units
            var currentSelectedUnits = new HashSet<IUnit>();
            foreach (var element in currentSelection)
            {
                if (element is IUnit unit)
                {
                    currentSelectedUnits.Add(unit);
                }
            }

            // 如果选择发生变化，记录历史，但跳过从历史窗口触发的跳转
            if (!currentSelectedUnits.SetEquals(_lastSelectedUnits) && currentSelectedUnits.Count > 0 &&
                !_isJumpingFromHistory)
            {
                RecordSelectionHistory(currentSelectedUnits);
                _lastSelectedUnits = new HashSet<IUnit>(currentSelectedUnits);
            }

            // 重置标记
            if (_isJumpingFromHistory)
            {
                _isJumpingFromHistory = false;
            }
        }

        // Get the full path of a transform in the scene hierarchy
        private static string GetTransformPath(Transform transform)
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

        private static void RecordSelectionHistory(HashSet<IUnit> selectedUnits)
        {
            if (GraphWindow.active == null) return;
            var window = GraphWindow.active;
            if (window == null) return;
            var reference = window.reference;

            var scenePath = "";
            var assetPath = AssetDatabase.GetAssetPath(reference.serializedObject);
            // 对于嵌入式图表，serializedObject可能是GameObject或其他对象
            if (string.IsNullOrEmpty(assetPath))
            {
                var obj = reference.gameObject;
                // // 对于场景中的对象，使用场景路径
                scenePath = obj.scene.path;
                if (!string.IsNullOrEmpty(scenePath))
                {
                    assetPath = GetTransformPath(obj.transform);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        Debug.LogWarning("Unsupported method in non serialized graph.");
                        return;
                    }
                }

                if (window.context.isPrefabInstance)
                {
                    Debug.LogWarning("Unsupported method in non serialized graph.");
                    return;
                }
            } else if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                assetPath = stage.assetPath;
            }

            // 只处理第一个节点，多选时只记录第一个
            if (selectedUnits.Count > 0)
            {
                var unit = selectedUnits.First();
                var uName = unit.ToString();
                var entry = new UnitHistoryEntry()
                {
                    scenePath = scenePath,
                    assetPath = assetPath,
                    name = uName,
                    timestamp = DateTime.Now
                };

                var info = BuildUnitInfo(entry);
                if (info != null && info.Unit != null)
                {
                    entry.meta = info.Meta;
                    entry.path = UnitUtility.GetGraphPath(info.Reference);
                    entry.type = info.Unit.GetType().AssemblyQualifiedName;

                    // 检查是否已存在相同的条目
                    var existingIndex = HistoryData.instance.historyEntries.FindIndex(x =>
                        x.assetPath == entry.assetPath &&
                        x.name == entry.name);

                    if (existingIndex >= 0)
                    {
                        // 更新时间戳并移动到列表顶部
                        HistoryData.instance.historyEntries.RemoveAt(existingIndex);
                    }

                    // 添加到历史记录开头
                    HistoryData.instance.historyEntries.Insert(0, entry);

                    // 限制历史记录数量
                    if (HistoryData.instance.historyEntries.Count > HistoryData.instance.maxHistoryCount)
                    {
                        HistoryData.instance.historyEntries.RemoveAt(HistoryData.instance.historyEntries.Count - 1);
                    }
                }
            }

            // 如果启用了自动清理，清除无效的条目
            if (HistoryData.instance.autoCleanInvalidEntries)
            {
                CleanInvalidEntries();
            }
        }

        public static void CleanInvalidEntries()
        {
            HistoryData.instance.historyEntries.RemoveAll(entry => !IsEntryValid(entry));
        }

        public static bool IsEntryValid(UnitHistoryEntry entry)
        {
            if (string.IsNullOrEmpty(entry.scenePath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.assetPath);
                return asset != null && entry.type != null;
            }

            return true;
        }

        private static List<GameObject> FindObjectsByPath(GameObject root, string path)
        {
            var results = new List<GameObject>();
            if (string.IsNullOrEmpty(path))
            {
                results.Add(root);
                return results;
            }

            // Split path by '/' and handle each segment
            string[] pathSegments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var currentTransforms = new List<Transform> { root.transform };

            // Check if first segment matches root name
            if (pathSegments[0] != root.name)
            {
                return results;
            }

            // Skip root segment and process remaining path
            for (int i = 1; i < pathSegments.Length; i++)
            {
                var segment = pathSegments[i];
                var nextTransforms = new List<Transform>();
                foreach (var current in currentTransforms)
                {
                    // Get all children with matching name
                    for (int j = 0; j < current.childCount; j++)
                    {
                        var child = current.GetChild(j);
                        if (child.name == segment)
                        {
                            nextTransforms.Add(child);
                        }
                    }
                }

                if (nextTransforms.Count == 0)
                    return results; // Return empty list if no matching path found

                currentTransforms = nextTransforms;
            }

            // Convert all matching transforms to GameObjects
            results.AddRange(currentTransforms.Select(t => t.gameObject));
            return results;
        }

        public static IEnumerable<GraphReference> LoadAssetReference(UnitHistoryEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.scenePath))
            {
                // 尝试加载场景
                var sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(entry.scenePath);
                if (sceneAsset == null) yield break;
                // 如果是场景资产，需要确保场景已加载
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(entry.scenePath);
                if (!scene.isLoaded) yield break;
                var roots = scene.GetRootGameObjects();
                List<GameObject> founds = new();
                foreach (var root in roots)
                {
                    founds.AddRange(FindObjectsByPath(root, entry.assetPath));
                }

                foreach (var go in founds)
                {
                    // 在已加载的场景中查找所有ScriptMachine和StateMachine
                    var scriptMachines = go.GetComponentsInChildren<ScriptMachine>();
                    foreach (var machine in scriptMachines)
                    {
                        if (machine.graph == null) continue;
                        var reference = GraphReference.New(machine, false);
                        yield return reference;
                    }

                    var stateMachines = go.GetComponentsInChildren<StateMachine>();
                    foreach (var machine in stateMachines)
                    {
                        if (machine.graph == null) continue;
                        var reference = GraphReference.New(machine, false);
                        yield return reference;
                    }
                }

                yield break;
            }

            if (entry.assetPath.EndsWith(".prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Object>(entry.assetPath) as GameObject;
                if (prefab == null) yield break;

                var scriptMachines = prefab.GetComponentsInChildren<ScriptMachine>(true);
                foreach (var machine in scriptMachines)
                {
                    if (machine.graph == null) continue;
                    if (machine.nest.source == GraphSource.Macro) continue;
                    var reference = GraphReference.New(machine, false);
                    yield return reference;
                }

                var stateMachines = prefab.GetComponentsInChildren<StateMachine>(true);
                foreach (var machine in stateMachines)
                {
                    if (machine.graph == null) continue;
                    if (machine.nest.source == GraphSource.Macro) continue;
                    var reference = GraphReference.New(machine, false);
                    yield return reference;
                }
            }
            else
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.assetPath);
                switch (asset)
                {
                    case ScriptGraphAsset scriptGraphAsset:
                    {
                        var baseRef = scriptGraphAsset.GetReference().AsReference();
                        yield return baseRef;
                    }
                        break;
                    case StateGraphAsset stateGraphAsset:
                    {
                        var baseRef = stateGraphAsset.GetReference().AsReference();
                        yield return baseRef;
                    }
                        break;
                    case GameObject prefab:
                    {
                        // 处理预制体中的图表
                        var scriptMachines = prefab.GetComponentsInChildren<ScriptMachine>(true);
                        foreach (var machine in scriptMachines)
                        {
                            if (machine.graph == null) continue;
                            var reference = GraphReference.New(machine, false);
                            yield return reference;
                        }

                        var stateMachines = prefab.GetComponentsInChildren<StateMachine>(true);
                        foreach (var machine in stateMachines)
                        {
                            if (machine.graph == null) continue;
                            var reference = GraphReference.New(machine, false);
                            yield return reference;
                        }

                        yield break;
                    }
                    default:
                        yield break;
                }
            }
        }

        public static UnitInfo BuildUnitInfo(UnitHistoryEntry entry)
        {
            foreach (var reference in LoadAssetReference(entry))
            {
                var correctRef = UnitUtility.GetUnitGraphReference(reference, entry.name);
                if (correctRef == null) continue;
                var detail = new UnitInfo
                {
                    AssetReference = correctRef,
                };
                detail.Reference = UnitUtility.GetUnitGraphReference(detail.AssetReference, entry.name);
                detail.Unit = FindNode(detail.Reference, entry.name);
                if (detail.Unit == null) return null;
                detail.Name = detail.Unit.ToString().Split('#')[0];
                detail.Meta = UnitUtility.UnitBrief(detail.Unit);
                return detail;
            }

            return null;
        }

        private static IUnit FindNode(GraphReference reference, string nodeName)
        {
            if (reference == null) return null;

            // 处理Flow图表中的节点
            foreach (var enumerator in UnitUtility.TraverseFlowGraphUnit(reference))
            {
                if (enumerator.Item2.ToString() == nodeName)
                {
                    return enumerator.Item2;
                }
            }

            // 处理State图表中的节点
            foreach (var enumerator in UnitUtility.TraverseStateGraphUnit(reference))
            {
                if (enumerator.Item2.ToString() == nodeName)
                {
                    return enumerator.Item2;
                }
            }

            // 如果在当前图表中没有找到，尝试在场景中查找
            if (reference.serializedObject is GameObject gameObject)
            {
                // 查找所有ScriptMachine和StateMachine组件
                var scriptMachines = gameObject.GetComponentsInChildren<ScriptMachine>(true);
                foreach (var machine in scriptMachines)
                {
                    if (machine.graph == null) continue;
                    var machineRef = GraphReference.New(machine, false);
                    var result = FindNode(machineRef, nodeName);
                    if (result != null) return result;
                }

                var stateMachines = gameObject.GetComponentsInChildren<StateMachine>(true);
                foreach (var machine in stateMachines)
                {
                    if (machine.graph == null) continue;
                    var machineRef = GraphReference.New(machine, false);
                    var result = FindNode(machineRef, nodeName);
                    if (result != null) return result;
                }
            }

            return null;
        }
    }
}