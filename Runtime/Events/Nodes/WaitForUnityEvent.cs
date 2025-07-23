using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Delays flow by waiting until a condition becomes true.
    /// </summary>
    [UnitShortTitle("Wait For Event")]
    [UnitTitle("Wait For Unity Event")]
    [UnitCategory("Wait")]
    [TypeIcon(typeof(WaitUnit))]
    public class WaitForUnityEvent : WaitUnit
    {
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
            Event = ValueInput<UnityEvent>(nameof(Event));
            Requirement(Event, enter);
        }

        protected override IEnumerator Await(Flow flow)
        {
            var e = flow.GetValue<UnityEvent>(Event);
            eventTriggered = false;
            e.AddListener(OnUnityEvent);
            
            yield return new WaitUntil(() => eventTriggered);
            
            e.RemoveListener(OnUnityEvent);
            eventTriggered = false;
            yield return exit;
        }
    }
}
