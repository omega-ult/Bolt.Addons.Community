using System;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Runs a timer and outputs elapsed and remaining measurements.
    /// </summary>
    [UnitCategory("Time")]
    [TypeIcon(typeof(Timer))]
    [UnitOrder(7)]
    public sealed class ProgressTimer : Unit, IGraphElementWithData, IGraphEventListener
    {
        public sealed class Data : IGraphElementData
        {
            public float elapsed;

            public float duration;

            public bool active;

            public bool stopped;

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

        /// <summary>
        /// Trigger to stop the timer.
        /// </summary>
        [DoNotSerialize]
        public ControlInput stop { get; private set; }

        /// <summary>
        /// The duration of the timer.
        /// </summary>
        [DoNotSerialize]
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
        /// Called when the timer is paused.co
        /// </summary>
        [DoNotSerialize]
        public ControlOutput finished { get; private set; }
        
        /// <summary>
        /// Called when the timer is paused.co
        /// </summary>
        [DoNotSerialize]
        public ControlOutput cleared { get; private set; }

        /// <summary>
        /// Called when the timer is paused.co
        /// </summary>
        [DoNotSerialize]
        public ControlOutput stopped { get; private set; }

        /// <summary>
        /// Called each frame while the timer is active.
        /// </summary>
        [DoNotSerialize]
        public ControlOutput tick { get; private set; }

        /// <summary>
        /// The number of seconds elapsed since the timer started.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Elapsed")]
        public ValueOutput elapsedSeconds { get; private set; }
        
        /// <summary>
        /// The proportion of the duration that has elapsed (0-1).
        /// </summary>
        [DoNotSerialize]
        [PortLabel("Elapsed %")]
        public ValueOutput elapsedRatio { get; private set; }

        protected override void Definition()
        {
            isControlRoot = true;

            start = ControlInput(nameof(start), Start);
            stop = ControlInput(nameof(stop), Stop);

            duration = ValueInput(nameof(duration), 1.0f);
            unscaledTime = ValueInput(nameof(unscaledTime), false);
            elapsedSeconds = ValueOutput<float>(nameof(elapsedSeconds));
            elapsedRatio = ValueOutput<float>(nameof(elapsedRatio));

            started = ControlOutput(nameof(started));
            stopped = ControlOutput(nameof(stopped));
            cleared = ControlOutput(nameof(cleared));
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

            data.elapsed = 0;
            data.duration = Mathf.Max(0.01f, flow.GetValue<float>(duration));
            data.active = true;
            data.stopped = false;
            data.unscaled = flow.GetValue<bool>(unscaledTime);

            AssignMetrics(flow, data);

            return started;
        }

        private void AssignMetrics(Flow flow, Data data)
        {
            flow.SetValue(elapsedSeconds, data.elapsed);
            flow.SetValue(elapsedRatio, data.elapsed / data.duration);
        }

        private ControlOutput Stop(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            data.active = true;
            data.stopped = true;

            return stopped;
        }


        public void Update(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            if (!data.active)
            {
                return;
            }

            if (data.stopped) // progressive back.
            {
                data.elapsed -= data.unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                if (data.elapsed <= 0)
                {
                    data.elapsed = 0;
                    AssignMetrics(flow, data);
                    var stack = flow.PreserveStack();
                    flow.Invoke(cleared);
                    flow.DisposePreservedStack(stack);
                    data.active = false;
                }
                else
                {
                    AssignMetrics(flow, data);
                    var stack = flow.PreserveStack();
                    flow.Invoke(tick);
                    flow.DisposePreservedStack(stack);
                }
            }
            else
            {
                data.elapsed += data.unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                if (data.elapsed >= data.duration)
                {
                    data.elapsed = data.duration;
                    AssignMetrics(flow, data);
                    var stack = flow.PreserveStack();
                    flow.Invoke(finished);
                    flow.DisposePreservedStack(stack);
                    data.active = false;
                }
                else
                {
                    AssignMetrics(flow, data);
                    var stack = flow.PreserveStack();
                    flow.Invoke(tick);
                    flow.DisposePreservedStack(stack);
                }
            }
        }
    }
}