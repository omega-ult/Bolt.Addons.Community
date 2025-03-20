using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [Widget(typeof(GlobalDefinedEventNode))]
    public sealed class GlobalDefinedEventWidget : UnitWidget<GlobalDefinedEventNode>
    {
        public GlobalDefinedEventWidget(FlowCanvas canvas, GlobalDefinedEventNode unit) : base(canvas, unit)
        {
        }
        
#if VISUAL_SCRIPTING_DDK_1_9
        protected override NodeColorMix baseColor => NodeColor.Green;
        protected override bool ShowMiniLabel => true;
        protected override string MiniLabel => unit.eventType == null ? base.MiniLabel : $"{unit.eventType.Name}";
        protected override Color MiniLabelColor => ( Color.green + Color.gray );
#endif
        
        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                yield return new DropdownOption((Action)ConvertEvent, "Convert To Trigger");

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
            var newUnit = new TriggerGlobalDefinedEvent();
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
        }
    }
}