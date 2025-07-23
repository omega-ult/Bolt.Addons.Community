using System;

namespace Unity.VisualScripting.Community {
    [Widget(typeof(WaitForUnityEvent))]
    public class WaitForUnityEventWidget : UnitWidget<WaitForUnityEvent> {

        private Type _currentType;
        
        public WaitForUnityEventWidget(FlowCanvas canvas, WaitForUnityEvent unit) : base(canvas, unit) {
        }

        public override void Update() {
            var type = item.Event?.connection?.source?.type;

            if (type != _currentType) {
                item.UpdatePorts();
                _currentType = type;
            }
        }
        
    }
}