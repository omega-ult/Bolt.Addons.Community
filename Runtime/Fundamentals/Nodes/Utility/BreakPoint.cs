using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Utility;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.VisualScripting.Community
{
    [UnitTitle("BreakPoint")]
    [TypeIcon(typeof(Debug))]
    [UnitCategory("Community/Utility")]
    public class BreakPoint : Unit
    {
        [DoNotSerialize] [PortLabelHidden] public ControlInput Enter { get; private set; }

        [DoNotSerialize] [PortLabelHidden] public ControlOutput Exit { get; private set; }

        public ValueInput Enabled { get; private set; }
        public ValueInput LogObject { get; private set; }


        [DoNotSerialize] [UnitHeaderInspectable] [NodeButton("TriggerButton", "Continue")]
        public NodeButton triggerButton;

        public void TriggerButton(GraphReference reference)
        {
            if (Application.isEditor)
            {
                if (Application.isPlaying)
                {
                    if (IsEditorPaused()) PlayEditor();
                }
            }
        }

        // Start is called before the first frame update
        protected override void Definition()
        {
            Enter = ControlInput(nameof(Enter), flow =>
            {
                var enabled = flow.GetValue<bool>(Enabled);
                var logObject = flow.GetValue(LogObject);
                if (logObject == null)
                {
                    Debug.Log("Break point hit.", flow.stack.gameObject);
                }
                else
                {
                    Debug.Log(logObject, flow.stack.gameObject);
                }

                if (enabled)
                {
                    PauseEditor();
                }

                return Exit;
            });
            Exit = ControlOutput(nameof(Exit));
            Enabled = ValueInput<bool>(nameof(Enabled), true).AllowsNull();
            LogObject = ValueInput<object>(nameof(LogObject)).AllowsNull();
            Succession(Enter, Exit);
        }

        public bool IsEditorPaused()
        {
#if UNITY_EDITOR
            return EditorApplication.isPaused;
#else
            return false;
#endif
        }

        public void PauseEditor()
        {
#if UNITY_EDITOR
            EditorApplication.isPaused = true;
#endif
        }

        public void PlayEditor()
        {
#if UNITY_EDITOR
            EditorApplication.isPaused = false;
#endif
        }
    }
}