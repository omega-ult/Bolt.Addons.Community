using System;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Runs a timer and outputs elapsed and remaining measurements.
    /// </summary>
    [UnitCategory("Time")]
    [TypeIcon(typeof(Timer))]
    [UnitOrder(9)]
    public sealed class DelayableTimer : Unit, IGraphElementWithData, IGraphEventListener
    {
        public sealed class Data : IGraphElementData
        {
            public float elapsed;

            public float duration;

            public float totalElapsed;

            public bool active;

            public bool unscaled;

            public Delegate update;

            public bool isListening;
        }

        /// <summary>
        /// The moment at which to start the timer.
        /// If the timer is already started, this will reset it.
        /// If the timer is paused, this will resume it.
        /// </summary>
        [DoNotSerialize]
        public ControlInput start { get; private set; }

        // /// <summary>
        // /// The total duration of the timer.
        // /// </summary>
        // [DoNotSerialize]
        public ValueInput duration { get; private set; }

        /// <summary>
        /// Whether to ignore the time scale.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Unscaled")]
        public ValueInput unscaledTime { get; private set; }

        /// <summary>
        /// Called when the timer is started.co
        /// </summary>
        [DoNotSerialize]
        public ControlOutput started { get; private set; }

        /// <summary>
        /// Called each time while the timer is active.
        /// </summary>
        [DoNotSerialize]
        public ControlOutput tick { get; private set; }

        /// <summary>
        /// Called each frame while the timer is active.
        /// </summary>
        [DoNotSerialize]
        public ControlOutput finish { get; private set; }

        /// <summary>
        /// The number of seconds elapsed since the timer started.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Elapsed")]
        public ValueOutput elapsedSeconds { get; private set; }

        /// <summary>
        /// The number of seconds elapsed since the timer started.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Elapsed %")]
        public ValueOutput elapsedPercent { get; private set; }

        /// <summary>
        /// The number of seconds elapsed since the first start.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Total Elapsed")]
        public ValueOutput elapsedTotal { get; private set; }

        protected override void Definition()
        {
            isControlRoot = true;

            start = ControlInput(nameof(start), Start);

            duration = ValueInput(nameof(duration), 1.0f);
            elapsedSeconds = ValueOutput<float>(nameof(elapsedSeconds));
            elapsedPercent = ValueOutput<float>(nameof(elapsedPercent));
            elapsedTotal = ValueOutput<float>(nameof(elapsedTotal));
            unscaledTime = ValueInput(nameof(unscaledTime), false);

            started = ControlOutput(nameof(started));
            tick = ControlOutput(nameof(tick));
            finish = ControlOutput(nameof(finish));
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

            if (!data.active)
            {
                data.elapsed = 0;
                data.totalElapsed = 0;
                data.active = true;
                data.duration = flow.GetValue<float>(duration);
                data.unscaled = flow.GetValue<bool>(unscaledTime);

                AssignMetrics(flow, data);

                return started;
            }
            else
            {
                data.elapsed = 0;
                data.totalElapsed += data.elapsed;
                data.duration = flow.GetValue<float>(duration);
                AssignMetrics(flow, data);
                return null;
            }
        }

        private void AssignMetrics(Flow flow, Data data)
        {
            flow.SetValue(elapsedSeconds, data.elapsed);
            flow.SetValue(elapsedPercent, data.elapsed / Mathf.Max(0.01f, data.duration));
            flow.SetValue(elapsedTotal, data.totalElapsed);
        }


        public void Update(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            if (!data.active)
            {
                return;
            }

            data.elapsed += data.unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (data.elapsed < data.duration)
            {
                AssignMetrics(flow, data);
                var stack = flow.PreserveStack();

                flow.Invoke(tick);

                flow.DisposePreservedStack(stack);
            }
            else
            {
                data.elapsed = data.duration;
                data.active = false;
                AssignMetrics(flow, data);
                var stack = flow.PreserveStack();

                flow.Invoke(finish);

                flow.DisposePreservedStack(stack);
            }
        }
    }
}