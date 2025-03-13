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
    public sealed class ToggleTimer : Unit, IGraphElementWithData, IGraphEventListener
    {
        public sealed class Data : IGraphElementData
        {
            public float elapsed;
            
            public bool active;

            public bool paused;

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
        /// Trigger to pause the timer.
        /// </summary>
        [DoNotSerialize]
        public ControlInput pause { get; private set; }

        /// <summary>
        /// Trigger to resume the timer.
        /// </summary>
        [DoNotSerialize]
        public ControlInput resume { get; private set; }

        /// <summary>
        /// Trigger to toggle the timer.
        /// If it is idle, it will start.
        /// If it is active, it will pause.
        /// If it is paused, it will resume.
        /// </summary>
        [DoNotSerialize]
        public ControlInput toggle { get; private set; }

        // /// <summary>
        // /// The total duration of the timer.
        // /// </summary>
        // [DoNotSerialize]
        // public ValueInput duration { get; private set; }

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
        public ControlOutput paused { get; private set; }
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

        protected override void Definition()
        {
            isControlRoot = true;

            start = ControlInput(nameof(start), Start);
            pause = ControlInput(nameof(pause), Pause);
            resume = ControlInput(nameof(resume), Resume);
            toggle = ControlInput(nameof(toggle), Toggle);

            elapsedSeconds = ValueOutput<float>(nameof(elapsedSeconds));
            unscaledTime = ValueInput(nameof(unscaledTime), false);

            started = ControlOutput(nameof(started));
            paused = ControlOutput(nameof(paused));
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
            flow.SetValue(elapsedSeconds, data.elapsed);
            data.active = true;
            data.paused = false;
            data.unscaled = flow.GetValue<bool>(unscaledTime);

            // AssignMetrics(flow, data);

            return started;
        }

        private ControlOutput Pause(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);
            flow.SetValue(elapsedSeconds, data.elapsed);

            data.paused = true;

            return paused;
        }

        private ControlOutput Resume(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);
            flow.SetValue(elapsedSeconds, data.elapsed);
            data.paused = false;

            return started;
        }

        private ControlOutput Toggle(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            if (!data.active)
            {
                return Start(flow);
            }
            else
            {
                return data.paused ? Resume(flow) : Pause(flow);
            }
        }


        public void Update(Flow flow)
        {
            var data = flow.stack.GetElementData<Data>(this);

            if (!data.active || data.paused)
            {
                return;
            }

            data.elapsed += data.unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

            var stack = flow.PreserveStack();
            flow.SetValue(elapsedSeconds, data.elapsed);

            flow.Invoke(tick);

            flow.DisposePreservedStack(stack);
        }
    }
}