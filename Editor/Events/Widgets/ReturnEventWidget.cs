using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// The visuals for the ReturnEvent Unit.
    /// </summary>
    [Widget(typeof(ReturnEvent))]
    public sealed class ReturnEventWidget : UnitWidget<ReturnEvent>
    {
        public ReturnEventWidget(FlowCanvas canvas, ReturnEvent unit) : base(canvas, unit)
        {
        }
        
#if VISUAL_SCRIPTING_DDK_1_9
        protected override bool ShowMiniLabel => unit.trigger.hasValidConnection;
        protected override string MiniLabel => unit.name.hasValidConnection ? base.MiniLabel : $"{unit.defaultValues[nameof(unit.name)]}";
        protected override Color MiniLabelColor => ( Color.green + Color.gray );
        
#endif

        /// <summary>
        /// Sets the color of the ReturnEvent Unit to green.
        /// </summary>
        protected override NodeColorMix baseColor => NodeColor.Green;
        
        
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
            var newUnit = new TriggerReturnEvent();
            newUnit.count = unit.count;
            newUnit.argumentNames = new List<string>(unit.argumentNames);
            newUnit.argumentTypes = new List<Type>(unit.argumentTypes);
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