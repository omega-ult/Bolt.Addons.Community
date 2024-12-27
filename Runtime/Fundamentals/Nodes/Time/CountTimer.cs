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
    public sealed class CountTimer : Unit, IGraphElementWithData, IGraphEventListener
    {
        public sealed class Data : IGraphElementData
        {
            public int count;
            
            public int current;
            
            public float elapsed;
            
            public float interval;

            public bool unscaled;

            public Delegate update;

            public bool active => count < 0 || current < count;
            
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
        /// The total count of the timer.
        /// negative for non-stop 
        /// </summary>
        [DoNotSerialize]
        public ValueInput count { get; private set; }

        /// <summary>
        /// The interval of the timer.
        /// </summary>
        [DoNotSerialize]
        public ValueInput interval { get; private set; }

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
        public ControlOutput finished { get; private set; }
        
        /// <summary>
        /// The number of seconds remaining until the cooldown is ready.
        /// </summary>
        [DoNotSerialize]
        [PortLabel("index")]
        public ValueOutput index { get; private set; }

        protected override void Definition()
        {
            isControlRoot = true;

            start = ControlInput(nameof(start), Start);
            stop = ControlInput(nameof(stop), Stop);

            count = ValueInput(nameof(count), -1);
            interval = ValueInput(nameof(interval), 1.0f);
            immediateStart = ValueInput(nameof(immediateStart), true);
            unscaledTime = ValueInput(nameof(unscaledTime), false);
            index = ValueOutput<int>(nameof(index));

            started = ControlOutput(nameof(started));
            tick = ControlOutput(nameof(tick));
            finished = ControlOutput(nameof(finished));
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

            data.count = flow.GetValue<int>(count);
            data.interval = flow.GetValue<float>(interval);
            data.current = 0;
            data.elapsed = 0;
            data.unscaled = flow.GetValue<bool>(unscaledTime);

            AssignMetrics(flow, data);
            
            var startNow = flow.GetValue<bool>(immediateStart);
            if (startNow)
            {
                data.elapsed = data.interval;
            }

            return started;
        }
        private void AssignMetrics(Flow flow, Data data)
        {
            flow.SetValue(index, data.current);
        }

        private ControlOutput Stop(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            data.current = 1;
            data.count = 0;

            return finished;
        }


        public void Update(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            if (!data.active)
            {
                return;
            }

            data.elapsed += data.unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!(data.elapsed >= data.interval)) return;
                
            var stack = flow.PreserveStack();
            
            AssignMetrics(flow, data);
            flow.Invoke(tick);
            
            data.elapsed = 0f;
            data.current += 1;

            if (!data.active)
            {
                flow.RestoreStack(stack);
                flow.Invoke(finished);
            }
            flow.DisposePreservedStack(stack);


        }
    }
}