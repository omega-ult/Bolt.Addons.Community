using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;
using UnityEngine.UIElements;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEditor.SceneManagement;
using UnityEditor.Search;
using Object = UnityEngine.Object;

namespace Unity.VisualScripting.Community
{
    public class CopyVariablesWindow : EditorWindow
    {
        private Object selectedObject;
        private VariableDeclarations copiedData = new();
        private bool showSubGraph = false;

        [MenuItem("Window/UVS Community/Copy Variables")]
        public static void Open()
        {
            var window = GetWindow<CopyVariablesWindow>();
            window.titleContent = new GUIContent("CopyVariables");
        }


        void DrawVariables(GameObject target, VariableDeclarations vars, string type, string prefix = "")
        {
            if (vars.Any())
            {
                var enums = vars.GetEnumerator();
                using IDisposable enums1 = enums;
                GUILayout.Label($"{prefix}{type} Variables:");
                while (enums.MoveNext())
                {
                    var decl = enums.Current;
                    GUILayout.Label($"{prefix}---{decl.name} : {decl.value}");
                }
            }


            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Copy {type} Variables"))
            {
                copiedData = new VariableDeclarations();
                var enums = vars.GetEnumerator();
                while (enums.MoveNext())
                {
                    var decl = enums.Current;
                    copiedData.Set(decl.name, decl.value);
                }
            }

            if (GUILayout.Button($"Paste to"))
            {
                if (type == "Object")
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(stage.prefabContentsRoot);
                    }
                    else
                    {
                        GameObject activeGameObject = Selection.activeGameObject;
                        if (activeGameObject != null)
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(activeGameObject);
                        }
                    }
                }

                var enums = copiedData.GetEnumerator();
                while (enums.MoveNext())
                {
                    var decl = enums.Current;
                    vars.Set(decl.name, decl.value);
                }

                EditorUtility.SetDirty(target);

                Repaint();
            }

            if (GUILayout.Button($"Add to"))
            {
                if (type == "Object")
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(stage.prefabContentsRoot);
                    }
                    else
                    {
                        GameObject activeGameObject = Selection.activeGameObject;
                        if (activeGameObject != null)
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(activeGameObject);
                        }
                    }
                }

                var enums = copiedData.GetEnumerator();
                while (enums.MoveNext())
                {
                    var decl = enums.Current;
                    if (!vars.IsDefined(decl.name))
                    {
                        vars.Set(decl.name, decl.value);
                    }
                }

                EditorUtility.SetDirty(target);

                Repaint();
            }

            GUILayout.EndHorizontal();
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Selected Object", EditorStyles.boldLabel);
            if (selectedObject != null)
            {
                GUILayout.BeginVertical("box");
                EditorGUILayout.ObjectField("Selected Object", selectedObject, typeof(Object), true);

                if (selectedObject.GetType() == typeof(ScriptGraphAsset))
                {
                    var graph = selectedObject as ScriptGraphAsset;
                    var vars = VisualScripting.Variables.Graph(graph.GetReference());
                    DrawVariables(null, vars, "Graph");
                }
                else if (selectedObject.GetType() == typeof(GameObject))
                {
                    var obj = selectedObject as GameObject;
                    var vars = VisualScripting.Variables.Object(obj);
                    DrawVariables(obj, vars, "Object");
                }

                GUILayout.EndVertical();
                // check selection 
                GUILayout.Space(10);
            }


            GUILayout.Label("Active Graph Editor", EditorStyles.boldLabel);
            showSubGraph = GUILayout.Toggle(showSubGraph, "Show Sub Graphs");
            GUILayout.BeginVertical("box");

            var activeGraph = GraphWindow.active?.reference?.graph.Canvas().graph;
            // Debug.Log(graphSelection);
            if (activeGraph != null)
            {
                if (activeGraph is FlowGraph flowGraph)
                {
                    // GUILayout.Label(activeGraph.GetType().ToString());
                    DrawVariables(null, flowGraph.variables, "Graph");
                }

                if (showSubGraph)
                {
                    var graphElems = activeGraph.elements;
                    var prefix = "||> ";
                    foreach (var node in graphElems)
                    {
                        if (node is not SubgraphUnit subUnit) continue;
                        GUILayout.Label($"Graph Variables of {subUnit.nest.graph.title} / {subUnit}");
                        var graph = subUnit.nest.macro;
                        if (graph != null)
                        {
                            var vars = VisualScripting.Variables.Graph(graph.GetReference());
                            DrawVariables(null, vars, "Graph", prefix);
                        }

                        var embed = subUnit.nest.embed;
                        if (embed != null)
                        {
                            DrawVariables(null, embed.variables, "Embed", prefix);
                        }
                    }
                }
            }

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical("box");
            GUILayout.Label("Clipboard:");
            var copiedEnums = copiedData.GetEnumerator();
            while (copiedEnums.MoveNext())
            {
                var decl = copiedEnums.Current;
                GUILayout.Label($"---{decl.name} : {decl.value}");
            }

            GUILayout.EndVertical();


            GUILayout.EndVertical();
        }


        private void OnSelectionChange()
        {
            selectedObject = Selection.activeObject;
            Repaint();
        }

        private void OnDestroy()
        {
        }
    }
}