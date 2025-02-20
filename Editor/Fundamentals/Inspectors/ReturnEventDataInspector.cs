using UnityEditor;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [Inspector(typeof(ReturnEventData))] // Update this to use HDRColor instead of Color
    public class ReturnEventDataInspector : Inspector
    {
        public ReturnEventDataInspector(Metadata metadata) : base(metadata) { }

        protected override float GetHeight(float width, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            position = BeginLabeledBlock(metadata, position, label);

            // Retrieve the color value from the HDRColor struct
            var data = (ReturnEventData)metadata.value;
            EditorGUI.LabelField(position, data.args.name);
            position.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.ObjectField(position, data.args.target, typeof(GameObject), false);

            EndBlock(metadata);
        }
    }
}