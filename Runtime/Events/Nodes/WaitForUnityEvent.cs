using System;
using System.Collections;
using System.Reflection;
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

        public Type Type { get; private set; }


        [DoNotSerialize] public bool eventTriggered = false;

        private object _eventListener;
        // generic values
        private object[] _valueArray = new object[4] { null, null, null, null };

        protected override void Definition()
        {
            base.Definition();
            Event = ValueInput<UnityEventBase>(nameof(Event));
            
            if (Type != null)
            {
                var genericArguments = Type.GetGenericArguments();
                for (var i = 0; i < genericArguments.Length; i++)
                {
                    ValueOutput(genericArguments[i], $"arg{i}");
                }
            }

            Requirement(Event, enter);
        }


        protected override IEnumerator Await(Flow flow)
        {
            var e = flow.GetValue<UnityEventBase>(Event);
            eventTriggered = false;
            var addMethod = e.GetType().GetMethod(nameof(UnityEngine.Events.UnityEvent.AddListener));
            var removeMethod = e.GetType().GetMethod(nameof(UnityEngine.Events.UnityEvent.RemoveListener));

            var delegateType = addMethod?.GetParameters()[0].ParameterType;
            _eventListener = CreateAction(delegateType);
            addMethod?.Invoke(e, new[] { _eventListener });

            yield return new WaitUntil(() => eventTriggered);
            AssignArguments(flow);

            removeMethod?.Invoke(e, new[] { _eventListener });
            eventTriggered = false;
            yield return exit;
        }
        public void UpdatePorts()
        {
            Type = GetEventType();
            Define();
        }

        public void Trigger(object[] args)
        {
            eventTriggered = true;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    _valueArray[i] = args[i];
                }
            }
        }
        
        private Type GetEventType() {
            var eventType = Event?.connection?.source?.type;

            while (eventType != null && eventType.BaseType != typeof(UnityEventBase)) {
                eventType = eventType.BaseType;
            }

            return eventType;
        }

        private object CreateAction(Type delegateType)
        {
            var numParams = delegateType.GetGenericArguments().Length;

            if (numParams == 0)
            {
                void Action()
                {
                    Trigger(null);
                }

                return (UnityAction)Action;
            }

            string methodName;

            if (numParams == 1) methodName = nameof(OneParamHandler);
            else if (numParams == 2) methodName = nameof(TwoParamsHandler);
            else if (numParams == 3) methodName = nameof(ThreeParamsHandler);
            else methodName = nameof(FourParamsHandler);

            var method = GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod);

            return method?.MakeGenericMethod(delegateType.GetGenericArguments()).Invoke(this, new object[] { });
        }

        internal UnityAction<T> OneParamHandler<T>()
        {
            return arg0 => { Trigger(new object[] { arg0 }); };
        }

        internal UnityAction<T0, T1> TwoParamsHandler<T0, T1>()
        {
            return (arg0, arg1) => { Trigger(new object[] { arg0, arg1 }); };
        }

        internal UnityAction<T0, T1, T2> ThreeParamsHandler<T0, T1, T2>()
        {
            return (arg0, arg1, arg2) => { Trigger(new object[] { arg0, arg1, arg2 }); };
        }

        internal UnityAction<T0, T1, T2, T3> FourParamsHandler<T0, T1, T2, T3>()
        {
            return (arg0, arg1, arg2, arg3) => { Trigger(new object[] { arg0, arg1, arg2, arg3 }); };
        }
        
        protected void AssignArguments(Flow flow) {
            var numOutputs = valueOutputs.Count;
            
            if(numOutputs > 0) flow.SetValue(valueOutputs[0], _valueArray[0]);
            if(numOutputs > 1) flow.SetValue(valueOutputs[1], _valueArray[1]);
            if(numOutputs > 2) flow.SetValue(valueOutputs[2], _valueArray[2]);
            if(numOutputs > 3) flow.SetValue(valueOutputs[3], _valueArray[3]);
        }


    }
}