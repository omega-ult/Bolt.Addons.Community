using Unity.VisualScripting.Community.Utility;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Listens for an event by type, rather than by name.  In other respects, it acts similar to the built-in Custom Unit
    /// </summary>
    [UnitCategory("Events/Community")]
    [UnitTitle("Defined Event")]
    [RenamedFrom("Bolt.Addons.Community.DefinedEvents.Units.DefinedEvent")]
    [RenamedFrom("Bolt.Addons.Community.DefinedEvents.Units.DefinedEventUnit")]
    public class DefinedEventNode : GameObjectEventUnit<DefinedEventArgs>, IDefinedEventNode
    {
        const string EventName = "OnDefinedEvent";

        #region Event Type Handling

        [SerializeAs(nameof(eventType))] private System.Type _eventType;


        [DoNotSerialize]
        [InspectableIf(nameof(IsNotRestricted))]
        public System.Type eventType
        {
            get { return _eventType; }
            set { _eventType = value; }
        }

        [DoNotSerialize]
        [Inspectable, UnitHeaderInspectable("SealArgument")]
        public bool sealArgument
        {
            get => _sealArgument;
            set => _sealArgument = value;
        }

        [SerializeAs(nameof(sealArgument))] private bool _sealArgument = false;

        [DoNotSerialize]
        [UnitHeaderInspectable]
        [InspectableIf(nameof(IsRestricted))]
        [Unity.VisualScripting.TypeFilter(TypesMatching.AssignableToAll, typeof(IDefinedEvent))]
        public System.Type restrictedEventType
        {
            get { return _eventType; }
            set { _eventType = value; }
        }

        public bool IsRestricted
        {
            get { return CommunityOptionFetcher.DefinedEvent_RestrictEventTypes; }
        }

        public bool IsNotRestricted
        {
            get { return !IsRestricted; }
        }

        #endregion


        [DoNotSerialize] public ValueOutput eventArgument;

        [DoNotSerialize] public List<ValueOutput> outputPorts { get; } = new List<ValueOutput>();

        [DoNotSerialize] private ReflectedInfo Info;

        public override Type MessageListenerType => null;
        protected override string hookName => EventName;


        protected override bool ShouldTrigger(Flow flow, DefinedEventArgs args)
        {
            if (eventType == null) return false;
            return args.eventData.GetType() == _eventType;
        }


        protected override void Definition()
        {
            base.Definition();

            BuildFromInfo();
        }


        private void BuildFromInfo()
        {
            outputPorts.Clear();
            if (_eventType == null)
                return;

            if (_sealArgument)
            {
                eventArgument = ValueOutput(_eventType, _eventType.Name);
            }
            else
            {
                Info = ReflectedInfo.For(_eventType);
                foreach (var field in Info.reflectedFields)
                {
                    outputPorts.Add(ValueOutput(field.Value.FieldType, field.Value.Name));
                }


                foreach (var property in Info.reflectedProperties)
                {
                    outputPorts.Add(ValueOutput(property.Value.PropertyType, property.Value.Name));
                }
            }
        }

        protected override void AssignArguments(Flow flow, DefinedEventArgs args)
        {
            if (_sealArgument)
            {
                flow.SetValue(eventArgument, args.eventData);
            }
            else
            {
                for (var i = 0; i < outputPorts.Count; i++)
                {
                    var outputPort = outputPorts[i];
                    var key = outputPort.key;
                    if (Info.reflectedFields.TryGetValue(key, out var field))
                    {
                        flow.SetValue(outputPort, field.GetValue(args.eventData));
                    }
                    else if (Info.reflectedProperties.TryGetValue(key, out var property))
                    {
                        flow.SetValue(outputPort, property.GetValue(args.eventData));
                    }
                }
            }
        }

        public override EventHook GetHook(GraphReference reference)
        {
            var refData = reference.GetElementData<Data>(this);
            return ConstructHook(refData.target, _eventType);
        }

        private static EventHook ConstructHook(GameObject target, Type eventType)
        {
            EventHook hook;
            if (DefinedEventSupport.IsOptimized() && eventType != null)
                hook = new EventHook(EventName, target, eventType.GetTypeInfo().FullName);
            else
                hook = new EventHook(EventName, target);
            return hook;
        }


        public static void Trigger(GameObject target, object eventData)
        {
            var eventHook = ConstructHook(target, eventData.GetType());
            EventBus.Trigger(eventHook, new DefinedEventArgs(eventData));
        }


        public static IDisposable RegisterListener<T>(GameObject target, Action<T> onEvent)
        {
            var eventHook = ConstructHook(target, typeof(T));
            Action<DefinedEventArgs> action = (x) =>
            {
                if (x.eventData.GetType() == typeof(T))
                    onEvent((T)x.eventData);
            };
            EventBus.Register<DefinedEventArgs>(eventHook, action);

            return Disposable.Create(() => { EventBus.Unregister(eventHook, action); });
        }
    }
}