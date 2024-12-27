using System;
using System.Collections;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Runs a timer and outputs elapsed and remaining measurements.
    /// </summary>
    [UnitCategory("Time")]
    [TypeIcon(typeof(Timer))]
    [UnitOrder(7)]
    public class ForEachTimer : Unit, IGraphElementWithData, IGraphEventListener
    {
        public sealed class Data : IGraphElementData
        {
            public IEnumerator enumerator;
            public IDictionaryEnumerator dictionaryEnumerator;

            public int current;

            public float elapsed;

            public float interval;

            public bool unscaled;

            public Delegate update;

            public bool active;

            public bool isListening;
        }

        /// <summary>
        /// The moment at which to start the timer.
        /// If the timer is already started, this will reset it.
        /// If the timer is paused, this will resume it.
        /// </summary>
        [DoNotSerialize]
        public ControlInput start { get; private set; }

        /// <summary>
        /// Trigger to stop the timer.
        /// </summary>
        [DoNotSerialize]
        public ControlInput stop { get; private set; }

        /// <summary>
        /// for each items.
        /// </summary>
        [Serialize]
        [Inspectable, UnitHeaderInspectable("Dictionary")]
        [InspectorToggleLeft]
        public bool dictionary { get; set; }

        /// <summary>
        /// The interval of the timer.
        /// </summary>
        [DoNotSerialize]
        public ValueInput interval { get; private set; }

        /// <summary>
        /// The collection over which to loop.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput collection { get; private set; }

        /// <summary>
        /// If set, start 0 ticker immediate.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("ImmediateStart")]
        public ValueInput immediateStart { get; private set; }

        /// <summary>
        /// Whether to ignore the time scale.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Unscaled")]
        public ValueInput unscaledTime { get; private set; }

        /// <summary>
        /// Called each time while the timer is active.
        /// </summary>
        [DoNotSerialize]
        public ControlOutput tick { get; private set; }

        /// <summary>
        /// Called each frame while the timer is active.
        /// </summary>
        [DoNotSerialize]
        public ControlOutput finished { get; private set; }


        /// <summary>
        /// The current index of the loop.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Index")]
        public ValueOutput currentIndex { get; private set; }

        /// <summary>
        /// The key of the current item of the loop.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Key")]
        public ValueOutput currentKey { get; private set; }

        /// <summary>
        /// The current item of the loop.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Item")]
        public ValueOutput currentItem { get; private set; }


        protected override void Definition()
        {
            isControlRoot = true;

            start = ControlInput(nameof(start), Start);
            stop = ControlInput(nameof(stop), Stop);

            currentIndex = ValueOutput<int>(nameof(currentIndex));
            collection = dictionary
                ? ValueInput<IDictionary>(nameof(collection))
                : ValueInput<IEnumerable>(nameof(collection));
            if (dictionary)
            {
                currentKey = ValueOutput<object>(nameof(currentKey));
            }

            currentItem = ValueOutput<object>(nameof(currentItem));

            interval = ValueInput(nameof(interval), 1.0f);
            immediateStart = ValueInput(nameof(immediateStart), true);
            unscaledTime = ValueInput(nameof(unscaledTime), false);

            finished = ControlOutput(nameof(finished));
            tick = ControlOutput(nameof(tick));
        }

        public IGraphElementData CreateData()
        {
            return new Data();
        }

        public void StartListening(GraphStack stack)
        {
            var data = stack.GetElementData<Data>(this);

            if (data.isListening)
            {
                return;
            }

            var reference = stack.ToReference();
            var hook = new EventHook(EventHooks.Update, stack.machine);
            Action<EmptyEventArgs> update = args => TriggerUpdate(reference);
            EventBus.Register(hook, update);
            data.update = update;
            data.isListening = true;
        }

        public void StopListening(GraphStack stack)
        {
            var data = stack.GetElementData<Data>(this);

            if (!data.isListening)
            {
                return;
            }

            var hook = new EventHook(EventHooks.Update, stack.machine);
            EventBus.Unregister(hook, data.update);

            // stack.ClearReference();

            data.update = null;
            data.isListening = false;
        }

        public bool IsListening(GraphPointer pointer)
        {
            return pointer.GetElementData<Data>(this).isListening;
        }

        private void TriggerUpdate(GraphReference reference)
        {
            using (var flow = Flow.New(reference))
            {
                Update(flow);
            }
        }

        private ControlOutput Start(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            if (dictionary)
            {
                var dict = flow.GetValue<IDictionary>(collection);
                // data.count = dict.Count;
                data.dictionaryEnumerator = dict.GetEnumerator();
                data.enumerator = data.dictionaryEnumerator;
            }
            else
            {
                var list = flow.GetValue<IList>(collection);
                // data.count = list.Count; 
                data.enumerator = list.GetEnumerator();
                data.dictionaryEnumerator = null;
            }

            data.interval = flow.GetValue<float>(interval);
            data.current = 0;
            data.elapsed = 0;
            data.active = true;
            data.unscaled = flow.GetValue<bool>(unscaledTime);


            var startNow = flow.GetValue<bool>(immediateStart);
            if (startNow)
            {
                data.elapsed = data.interval;
            }

            return null;
        }


        private void AssignEnumerator(Flow flow, Data data)
        {
            flow.SetValue(currentIndex, data.current);
            if (!data.active) return;
            if (dictionary)
            {
                flow.SetValue(currentKey, data.dictionaryEnumerator.Key);
                flow.SetValue(currentItem, data.dictionaryEnumerator.Value);
            }
            else
            {
                flow.SetValue(currentItem, data.enumerator.Current);
            }
        }

        void CleanData(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);
            data.current = 0;
            data.dictionaryEnumerator = null;
            data.enumerator = null;
        }

        private ControlOutput Stop(Flow flow)
        {
            CleanData(flow);

            return null;
        }


        public void Update(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);
            if (data.enumerator == null)
            {
                return;
            }

            data.elapsed += data.unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!(data.elapsed >= data.interval)) return;

            var stack = flow.PreserveStack();
            data.active = data.enumerator.MoveNext();
            AssignEnumerator(flow, data);

            data.elapsed = 0f;
            data.current += 1;

            if (data.active)
            {
                flow.Invoke(tick);
            }
            else
            {
                flow.RestoreStack(stack);
                flow.Invoke(finished);
                CleanData(flow);
            }

            flow.DisposePreservedStack(stack);
        }
    }
}