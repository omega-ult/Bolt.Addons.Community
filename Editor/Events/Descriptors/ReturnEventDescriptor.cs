using System.Linq;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// A descriptor that assigns the ReturnEvents icon.
    /// </summary>
    [Descriptor(typeof(ReturnEvent))]
    public sealed class ReturnEventDescriptor : EventUnitDescriptor<ReturnEvent>
    {
        public ReturnEventDescriptor(ReturnEvent target) : base(target)
        {

        }

        protected override EditorTexture DefaultIcon()
        {
            return PathUtil.Load("return_event", CommunityEditorPath.Events);
        }

        protected override EditorTexture DefinedIcon()
        {
            return PathUtil.Load("return_event", CommunityEditorPath.Events);
        }
        
        protected override void DefinedPort(IUnitPort port, UnitPortDescription portDescription)
        {
            base.DefinedPort(port, portDescription);

            if (unit.argumentNames == null || unit.argumentNames.Count == 0) return;
            var skip = 0;
            foreach (var (input, i) in unit.valueOutputs.Select((p, i) => (p, i)))
            {
                if (input.key == "data")
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