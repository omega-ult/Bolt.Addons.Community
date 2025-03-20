using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// The visuals for the TriggerReturnEvent Unit.
    /// </summary>
    [Widget(typeof(TriggerReturnEvent))]
    public sealed class TriggerReturnEventWidget : UnitWidget<TriggerReturnEvent>
    {
        public TriggerReturnEventWidget(FlowCanvas canvas, TriggerReturnEvent unit) : base(canvas, unit)
        {
        }

        
#if VISUAL_SCRIPTING_DDK_1_9
        protected override bool ShowMiniLabel => true;
        protected override string MiniLabel => unit.name.hasValidConnection ? base.MiniLabel : $"{unit.defaultValues[nameof(unit.name)]}";
        protected override Color MiniLabelColor => ( Color.yellow + Color.gray );
#endif
        /// <summary>
        /// Sets the TriggerReturnEvent Units color to gray. Since it is an even unit under the hood, we need to make it look like it is not.
        /// </summary>
        protected override NodeColorMix baseColor => NodeColor.Gray;

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
            //convert TriggerCustomEvent to CustomEvent
            var preservation = UnitPreservation.Preserve(unit);
            var newUnit = new ReturnEvent();
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
        }
    }
}