using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Delays flow by waiting until a condition becomes true.
    /// </summary>
    [UnitShortTitle("Wait Unity Event")]
    [UnitTitle("Wait For Unity Event")]
    [UnitCategory("Wait")]
    [TypeIcon(typeof(WaitUnit))]
    public class WaitForUnityEvent : WaitUnit
    {
        [DoNotSerialize] // No need to serialize ports.
        ControlInput Reset;
        /// <summary>
        /// The condition to await.
        /// </summary>
        [DoNotSerialize]
        public ValueInput Event { get; private set; }

        [DoNotSerialize] 
        public bool eventTriggered = false;

        void OnUnityEvent()
        {
            eventTriggered = true;
        }
        protected override void Definition()
        {
            base.Definition();

            Reset = ControlInput(nameof(Reset), (flow) =>
            {
                eventTriggered = false;
                return null;
            });
            
            Event = ValueInput<UnityEvent>(nameof(Event));
            Requirement(Event, enter);
        }

        protected override IEnumerator Await(Flow flow)
        {
            var e = flow.GetValue<UnityEvent>(Event);
            e.AddListener(OnUnityEvent);
            
            yield return new WaitUntil(() => eventTriggered);
            
            eventTriggered = false;
            e.RemoveListener(OnUnityEvent);
            yield return exit;
        }
    }
}
