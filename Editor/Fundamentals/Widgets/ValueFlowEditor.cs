using UnityEngine;
using Unity.VisualScripting;
using System;
using System.Collections.Generic;
using Unity.VisualScripting.Community;
using UnityEditor;

[Widget(typeof(ValueConnection))]
public sealed class ValueConnectionWidget : UnitConnectionWidget<ValueConnection>
{
    private bool canTrigger = true;
    private Color _color = Color.white;
    private new ValueConnection.DebugData ConnectionDebugData => GetDebugData<ValueConnection.DebugData>();

    public ValueConnectionWidget(FlowCanvas canvas, ValueConnection connection) : base(canvas, connection)
    {
    }

    public override void DrawForeground()
    {
        base.DrawForeground();
        if (BoltFlow.Configuration.showConnectionValues)
        {
            var showLastValue = EditorApplication.isPlaying && ConnectionDebugData.assignedLastValue;
            var showPredictedvalue = BoltFlow.Configuration.predictConnectionValues && !EditorApplication.isPlaying &&
                                     Flow.CanPredict(connection.source, reference);

            if (showLastValue || showPredictedvalue)
            {
                var previousIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(IconSize.Small, IconSize.Small));

                object value;

                if (showLastValue)
                {
                    value = ConnectionDebugData.lastValue;
                }
                else // if (showPredictedvalue)
                {
                    value = Flow.Predict(connection.source, reference);
                }

                var label = new GUIContent(value.ToShortString(), Icons.Type(value?.GetType())?[IconSize.Small]);
                var labelSize = Styles.prediction.CalcSize(label);
                var labelPosition = new Rect(position.position - labelSize / 2, labelSize);

                BeginDim();

                GUI.Label(labelPosition, label, Styles.prediction);

                EndDim();

                EditorGUIUtility.SetIconSize(previousIconSize);
            }
        }
    }

    public override void HandleInput()
    {
        base.HandleInput();

        var outputFlowReroute = (connection.source.unit as ValueReroute);
        var inputFlowReroute = (connection.destination.unit as ValueReroute);

        if (selection.Contains(connection.destination.unit) && outputFlowReroute != null)
        {
            if (selection.Contains(outputFlowReroute))
            {
                if (e.keyCode == KeyCode.Backspace && e.rawType == EventType.KeyDown && canTrigger)
                {
                    canTrigger = false;

                    outputFlowReroute.outputVisible = !outputFlowReroute.outputVisible;
                }

                if (e.keyCode == KeyCode.Backspace && e.rawType == EventType.KeyUp)
                {
                    canTrigger = true;
                }
            }
        }

        if (selection.Contains(connection.source.unit) && inputFlowReroute != null)
        {
            if (selection.Contains(inputFlowReroute))
            {
                if (e.keyCode == KeyCode.Backspace && e.rawType == EventType.KeyDown && canTrigger)
                {
                    canTrigger = false;

                    inputFlowReroute.inputVisible = !inputFlowReroute.inputVisible;
                }

                if (e.keyCode == KeyCode.Backspace && e.rawType == EventType.KeyUp)
                {
                    canTrigger = true;
                }
            }
        }
    }

    protected override void DrawConnection()
    {
        var outputFlowReroute = (connection.source.unit as ValueReroute);
        var inputFlowReroute = (connection.destination.unit as ValueReroute);

        if (outputFlowReroute != null && !outputFlowReroute.outputVisible)
        {
            if (clippingPosition.Contains(mousePosition) && outputFlowReroute.showFlowOnHover)
            {
                base.DrawConnection();
            }

            return;
        }
        else if (inputFlowReroute != null && !inputFlowReroute.inputVisible)
        {
            if (clippingPosition.Contains(mousePosition) && inputFlowReroute.showFlowOnHover)
            {
                base.DrawConnection();
            }

            return;
        }

        base.DrawConnection();
    }

    protected override bool colorIfActive => !BoltFlow.Configuration.animateControlConnections ||
                                             !BoltFlow.Configuration.animateValueConnections;

    #region Droplets

    public override IEnumerable<IWidget> subWidgets => base.subWidgets;

    protected override bool showDroplets => BoltFlow.Configuration.animateControlConnections;

    public override Color color =>
        Unity.VisualScripting.ValueConnectionWidget.DetermineColor(connection.source.type, connection.destination.type);

    protected override Vector2 GetDropletSize()
    {
        return BoltFlow.Icons.valuePortConnected?[12].Size() ?? 13 * Vector2.one;
    }

    protected override void DrawDroplet(Rect position)
    {
        if (BoltFlow.Icons.valuePortConnected != null)
        {
            GUI.DrawTexture(position, BoltFlow.Icons.valuePortConnected[12]);
        }
    }

    #endregion

    private static class Styles
    {
        static Styles()
        {
            prediction = new GUIStyle(EditorStyles.label);
            prediction.normal.textColor = Color.white;
            prediction.fontSize = 12;
            prediction.normal.background = new Color(0, 0, 0, 0.5f).GetPixel();
            prediction.padding = new RectOffset(0, 2, 0, 0);
            prediction.margin = new RectOffset(0, 0, 0, 0);
            prediction.alignment = TextAnchor.MiddleCenter;
        }

        public static readonly GUIStyle prediction;
    }
}