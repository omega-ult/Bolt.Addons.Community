using System;

namespace Unity.VisualScripting.Community.Utility
{
    [RenamedFrom("Bolt.Community.Addons.Utility.UnitButtonAttribute")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class NodeButtonAttribute : Attribute
    {
        public string action;
        public string displayName;

        public NodeButtonAttribute(string action)
        {
            this.action = action;
            this.displayName = "Trigger";
        }
        public NodeButtonAttribute(string action, string displayName)
        {
            this.action = action;
            this.displayName = displayName;
        }
    }
}