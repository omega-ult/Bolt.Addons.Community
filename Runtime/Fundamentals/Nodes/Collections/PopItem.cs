using System.Collections;

namespace Unity.VisualScripting.Community
{
    [UnitCategory("Collections")]
    [TypeIcon(typeof(RemoveListItem))]
    public class PopItem : Unit
    {
        [DoNotSerialize] [PortLabelHidden] public ControlInput input { get; private set; }

        [DoNotSerialize] [PortLabelHidden] public ControlOutput output { get; private set; }

        /// <summary>
        /// for each items.
        /// </summary>
        [Serialize]
        [Inspectable, UnitHeaderInspectable("Dictionary")]
        [InspectorToggleLeft]
        public bool dictionary { get; set; }

        /// <summary>
        /// The collection.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput collection { get; private set; }

        /// <summary>
        /// The key.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput key { get; private set; }

        /// <summary>
        /// The collection is empty or not.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput item { get; private set; }

        protected override void Definition()
        {
            collection = ValueInput<ICollection>(nameof(collection));
            key = ValueInput<object>(nameof(key));
            item = ValueOutput<object>(nameof(item));
            output = ControlOutput(nameof(output));
            input = ControlInput(nameof(input), flow =>
                {
                    if (dictionary)
                    {
                        var dictionary = flow.GetValue<IDictionary>(collection);
                        var key = flow.GetValue<object>(this.key);
                        var value = dictionary[key];
                        flow.SetValue(item, value);
                        dictionary.Remove(key);
                    }
                    else
                    {
                        var list = flow.GetValue<IList>(collection);
                        var key = flow.GetValue<object>(this.key);
                        flow.SetValue(item, key);
                        list.Remove(key);
                    }

                    return output;
                }
            );

            Succession(input, output);
            Requirement(collection, item);
        }
    }

    /// <summary>
    /// Runs a timer and outputs elapsed and remaining measurements.
    /// </summary>
    [UnitCategory("Collections")]
    [TypeIcon(typeof(RemoveListItem))]
    public class PopItemAt : Unit
    {
        [DoNotSerialize] [PortLabelHidden] public ControlInput input { get; private set; }

        [DoNotSerialize] [PortLabelHidden] public ControlOutput output { get; private set; }

        /// <summary>
        /// The collection.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput collection { get; private set; }

        /// <summary>
        /// The key.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput index { get; private set; }

        /// <summary>
        /// The collection is empty or not.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput item { get; private set; }

        protected override void Definition()
        {
            collection = ValueInput<IList>(nameof(collection));
            index = ValueInput<int>(nameof(index),0);
            item = ValueOutput<object>(nameof(item));
            output = ControlOutput(nameof(output));
            input = ControlInput(nameof(input), flow =>
                {
                    {
                        var list = flow.GetValue<IList>(collection);
                        var key = flow.GetValue<int>(index);
                        flow.SetValue(item, list[key]);
                        list.RemoveAt(key);
                    }

                    return output;
                }
            );
            Succession(input, output);
            Requirement(collection, item);
        }
    }
}