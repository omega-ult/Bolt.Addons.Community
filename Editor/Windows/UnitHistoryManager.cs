using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.Serialization;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// 静态管理器，用于监听和记录Unit选择历史，即使在UnitHistoryWindow关闭时也能工作
    /// </summary>
    [InitializeOnLoad]
    public static class UnitHistoryManager
    {
        [Serializable]
        public class HistoryEntry
        {
            public string path;
            public string type;
            public string name;
            public string meta;
            public UnitUtility.EntryContext context;
            public string AssetPath => context.assetPath;
            public string ScenePath => context.scenePath;
            public string DisplayLabel
            {
                get
                {
                    var fName = Path.GetFileNameWithoutExtension(context.assetPath);
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
            [SerializeField] public List<HistoryEntry> historyEntries = new List<HistoryEntry>();
            [SerializeField] public int maxHistoryCount = 50;
            [SerializeField] public bool autoCleanInvalidEntries = true;
        }

        private static HashSet<IUnit> _lastSelectedUnits = new HashSet<IUnit>();

        private static double _lastCheckTime = -1f;

        // 检查间隔时间(秒)
        private const float CHECK_INTERVAL = 0.5f;

        // 当前时间
        // 标记是否是从历史窗口触发的跳转，防止从历史窗口点击时产生新的历史记录
        private static bool _isJumpingFromHistory = false;

        // 静态构造函数，在编辑器加载时自动执行
        static UnitHistoryManager()
        {
            RestartListen();
        }

        public static void RestartListen()
        {
            EditorApplication.update -= CheckGraphWindowSelection;
            EditorApplication.update += CheckGraphWindowSelection;
        }

        public static void SetJumpingFromHistory(bool value)
        {
            _isJumpingFromHistory = value;
        }

        public static List<HistoryEntry> GetHistoryEntries()
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
        // 上次检查时间

        private static void CheckGraphWindowSelection()
        {
            if (EditorApplication.timeSinceStartup - _lastCheckTime < CHECK_INTERVAL)
            {
                return;
            }

            if (GraphWindow.active == null || GraphWindow.active.context == null) return;
            _lastCheckTime = EditorApplication.timeSinceStartup;


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


        private static void RecordSelectionHistory(HashSet<IUnit> selectedUnits)
        {
            if (GraphWindow.active == null) return;
            var window = GraphWindow.active;
            if (window == null) return;
            var reference = window.reference;

            // 只处理第一个节点，多选时只记录第一个
            if (selectedUnits.Count > 0)
            {
                var unit = selectedUnits.First();
                var uName = unit.ToString();
                var entry = new HistoryEntry()
                {
                    context = UnitUtility.GetEntryContext(reference),
                    name = uName,
                };

                var info = BuildUnitInfo(entry);
                if (info != null && info.Unit != null)
                {
                    entry.meta = info.Meta;
                    entry.path = UnitUtility.GetGraphPath(info.Reference);
                    entry.type = info.Unit.GetType().AssemblyQualifiedName;

                    // 检查是否已存在相同的条目
                    var existingIndex = HistoryData.instance.historyEntries.FindIndex(x =>
                        x.AssetPath == entry.AssetPath &&
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

        public static bool IsEntryValid(HistoryEntry entry)
        {
            if (string.IsNullOrEmpty(entry.ScenePath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.AssetPath);
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

        public static IEnumerable<GraphReference> LoadAssetReference(HistoryEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.ScenePath))
            {
                // 尝试加载场景
                var sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(entry.ScenePath);
                if (sceneAsset == null) yield break;
                // 如果是场景资产，需要确保场景已加载
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(entry.ScenePath);
                if (!scene.isLoaded) yield break;
                var roots = scene.GetRootGameObjects();
                List<GameObject> founds = new();
                foreach (var root in roots)
                {
                    founds.AddRange(FindObjectsByPath(root, entry.AssetPath));
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

            if (entry.AssetPath.EndsWith(".prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Object>(entry.AssetPath) as GameObject;
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
                var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.AssetPath);
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

        public static UnitInfo BuildUnitInfo(HistoryEntry entry)
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