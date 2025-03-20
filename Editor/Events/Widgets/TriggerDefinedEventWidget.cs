using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [Widget(typeof(TriggerDefinedEvent))]
    public sealed class TriggerDefinedEventWidget : UnitWidget<TriggerDefinedEvent>
    {
        public TriggerDefinedEventWidget(FlowCanvas canvas, TriggerDefinedEvent unit) : base(canvas, unit)
        {
        }
        
#if VISUAL_SCRIPTING_DDK_1_9
        protected override bool ShowMiniLabel => true;
        protected override string MiniLabel => unit.eventType == null ? base.MiniLabel : $"{unit.eventType.Name}";
        protected override Color MiniLabelColor => ( Color.yellow + Color.gray );
#endif
        
        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                yield return new DropdownOption((Action)ConvertEvent, "Convert To Receiver");

                foreach (var option in base.contextOptions)
                {
                    yield return option;
                }
            }
        }

        private void ConvertEvent()
        {
            //copy old event args to new event args.
            var preservation = UnitPreservation.Preserve(unit);
            var newUnit = new DefinedEventNode();
            newUnit.eventType = unit.eventType;
            newUnit.Define();
            newUnit.guid = Guid.NewGuid();
            newUnit.position = unit.position;
            preservation.RestoreTo(newUnit);
            var graph = unit.graph;
            unit.graph.units.Remove(unit);
            graph.units.Add(newUnit);
            selection.Select(newUnit);
            GUI.changed = true;
            context.EndEdit();
        }
    }
}