using System.Linq;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// A descriptor that assigns the ReturnEvents icon.
    /// </summary>
    [Descriptor(typeof(TriggerReturnEvent))]
    public sealed class TriggerReturnEventDescriptor : EventUnitDescriptor<TriggerReturnEvent>
    {
        public TriggerReturnEventDescriptor(TriggerReturnEvent target) : base(target)
        {

        }
        
        protected override void DefinedPort(IUnitPort port, UnitPortDescription portDescription)
        {
            base.DefinedPort(port, portDescription);

            if (unit.argumentNames == null || unit.argumentNames.Count == 0) return;
            var skip = 0;
            foreach (var (input, i) in unit.valueInputs.Select((p, i) => (p, i)))
            {
                if (input.key is "name" or "target")
                {
                    skip++;
                    continue;
                }
                if (input != port) continue;
                var index = i - skip;
                if (index >= unit.argumentNames.Count) continue;
                var name = unit.argumentNames[index];
                portDescription.label = name;
                portDescription.summary = $"The {name} argument of the event.";
            }
        }
    }
}