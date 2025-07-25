using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.VisualScripting.Community
{
    public class UnitHistoryWindow : EditorWindow
    {
        // 使用UnitHistoryManager中的UnitHistoryEntry类型
        private List<UnitHistoryManager.HistoryEntry> _historyEntries = new();
        private int maxHistoryCount = 50;
        private bool autoCleanInvalidEntries = true;
        private string _filterString = "";

        private Vector2 _historyScrollPosition = Vector2.zero;

        [MenuItem("Window/UVS Community/Unit History")]
        public static void Open()
        {
            var window = GetWindow<UnitHistoryWindow>();
            window.titleContent = new GUIContent("Unit History");
        }

        [MenuItem("Window/UVS Community/History/Previous %LEFT")]
        public static void PreviousHistory()
        {
            var entries = UnitHistoryManager.GetHistoryEntries();
            if (entries.Count == 0) return;
            UnitHistoryManager.historyCursor = Mathf.Max(0, UnitHistoryManager.historyCursor + 1);
            JumpToHistory(UnitHistoryManager.historyCursor);
        }

        [MenuItem("Window/UVS Community/History/Next %RIGHT")]
        public static void NextHistory()
        {
            var entries = UnitHistoryManager.GetHistoryEntries();
            if (entries.Count == 0) return;
            UnitHistoryManager.historyCursor = Mathf.Min(entries.Count - 1, UnitHistoryManager.historyCursor - 1);
            JumpToHistory(UnitHistoryManager.historyCursor);
        }

        private static void JumpToHistory(int index)
        {
            var entries = UnitHistoryManager.GetHistoryEntries();
            if (index < 0 || index >= entries.Count) return;
            var entry = entries[index];
            // 复用已有的跳转逻辑
            var unit = UnitHistoryManager.BuildUnitInfo(entry);
            if (unit != null && unit.Unit != null)
            {
                UnitHistoryManager.SetJumpingFromHistory(true);
                UnitUtility.FocusUnit(unit.Reference, unit.Unit);
            }
        }

        private void OnEnable()
        {
            // 从UnitHistoryManager获取历史记录
            _historyEntries = UnitHistoryManager.GetHistoryEntries();
            maxHistoryCount = UnitHistoryManager.GetMaxHistoryCount();
            autoCleanInvalidEntries = UnitHistoryManager.GetAutoCleanInvalidEntries();
        }

        private void OnDisable()
        {
            // 保存设置到UnitHistoryManager
            UnitHistoryManager.SetMaxHistoryCount(maxHistoryCount);
            UnitHistoryManager.SetAutoCleanInvalidEntries(autoCleanInvalidEntries);
        }

        // 窗口不再需要监听选择变化，由UnitHistoryManager负责
        // 仅在需要刷新UI时更新历史记录列表
        private void Update()
        {
            // 定期从UnitHistoryManager获取最新历史记录
            _historyEntries = UnitHistoryManager.GetHistoryEntries();
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();

            // 设置区域
            GUILayout.BeginHorizontal();
            GUILayout.Label("Max History:", GUILayout.Width(80));
            int newMaxCount = EditorGUILayout.IntField(maxHistoryCount, GUILayout.Width(50));
            if (newMaxCount != maxHistoryCount)
            {
                maxHistoryCount = newMaxCount;
                UnitHistoryManager.SetMaxHistoryCount(maxHistoryCount);
            }

            GUILayout.FlexibleSpace();
            bool newAutoClean = EditorGUILayout.ToggleLeft("Auto Clean Invalid", autoCleanInvalidEntries);
            if (newAutoClean != autoCleanInvalidEntries)
            {
                autoCleanInvalidEntries = newAutoClean;
                UnitHistoryManager.SetAutoCleanInvalidEntries(autoCleanInvalidEntries);
            }

            if (GUILayout.Button("Restart", GUILayout.Width(90)))
            {
                UnitHistoryManager.RestartListen();
            }

            if (GUILayout.Button("Clear", GUILayout.Width(80)))
            {
                UnitHistoryManager.ClearHistory();
                _historyEntries = UnitHistoryManager.GetHistoryEntries();
            }

            if (GUILayout.Button("Clean Invalid", GUILayout.Width(100)))
            {
                UnitHistoryManager.CleanInvalidEntries();
                _historyEntries = UnitHistoryManager.GetHistoryEntries();
            }

            GUILayout.EndHorizontal();

            // 过滤区域
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(40));
            _filterString = GUILayout.TextField(_filterString);
            if (GUILayout.Button("x", GUILayout.Width(20)))
            {
                _filterString = "";
            }

            GUILayout.EndHorizontal();

            // 历史记录列表
            _historyScrollPosition =
                GUILayout.BeginScrollView(_historyScrollPosition, "box", GUILayout.ExpandHeight(true));

            for (var index = 0; index < _historyEntries.Count; index++)
            {
                var entry = _historyEntries[index];

                // 应用过滤
                if (!string.IsNullOrEmpty(_filterString) &&
                    !entry.DisplayLabel.Contains(_filterString, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DisplayHistoryEntry(index);
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private int _removedIndex = -1;
        private static Color _graphColor = new Color(0.5f, 0.8f, 0.6f);
        private static Color _prefabColor = new Color(0.2f, 0.7f, 0.9f);
        private static Color _sceneColor = new Color(0.7f, 0.7f, 0.7f);

        private void DisplayHistoryEntry(int index)
        {
            GUILayout.BeginHorizontal();
            var entry = _historyEntries[index];


            // 删除按钮
            if (GUILayout.Button("x", GUILayout.ExpandWidth(false)))
            {
                _removedIndex = index;
            }
            UnitUtility.DrawContextButton(entry.context);

            // 显示条目
            if (UnitHistoryManager.IsEntryValid(entry))
            {
                var iconType = Type.GetType(entry.type);
                var tex = Icons.Icon(iconType);
                var icon = new GUIContent(tex[IconSize.Small])
                {
                    text = entry.DisplayLabel
                };
                if (index == UnitHistoryManager.historyCursor)
                {
                    GUILayout.Label(icon, GUILayout.MaxHeight(IconSize.Small + 4));
                }
                else if (GUILayout.Button(icon, EditorStyles.linkLabel, GUILayout.MaxHeight(IconSize.Small + 4)))
                {
                    UnitUtility.RestoreContext(entry.context);

                    var unit = UnitHistoryManager.BuildUnitInfo(entry);
                    if (unit != null && unit.Unit != null)
                    {
                        // 设置标记，表示这是从历史窗口触发的跳转
                        UnitHistoryManager.SetJumpingFromHistory(true);
                        UnitUtility.FocusUnit(unit.Reference, unit.Unit);
                    }
                    else
                    {
                        Debug.LogError($"Missing {entry.name}:{entry.AssetPath}");
                        if (autoCleanInvalidEntries)
                        {
                            UnitHistoryManager.RemoveHistoryEntry(index);
                            _historyEntries = UnitHistoryManager.GetHistoryEntries();
                        }
                    }
                }
            }
            else
            {
                GUILayout.Label($"Missing {entry.name}:{entry.AssetPath}");
                if (autoCleanInvalidEntries)
                {
                    UnitHistoryManager.RemoveHistoryEntry(index);
                    _historyEntries = UnitHistoryManager.GetHistoryEntries();
                }
            }


            GUILayout.EndHorizontal();

            if (_removedIndex <= -1) return;
            UnitHistoryManager.RemoveHistoryEntry(_removedIndex);
            _historyEntries = UnitHistoryManager.GetHistoryEntries();
            _removedIndex = -1;
        }

    }
}