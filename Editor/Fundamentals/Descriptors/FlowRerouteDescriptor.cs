namespace Unity.VisualScripting.Community
{
    [Descriptor(typeof(FlowReroute))]
    public sealed class FlowRerouteDescriptor : UnitDescriptor<FlowReroute>
    {
        public FlowRerouteDescriptor(FlowReroute target) : base(target)
        {
        }

        protected override void DefinedPort(IUnitPort port, UnitPortDescription portDescription)
        {
            base.DefinedPort(port, portDescription);

            portDescription.showLabel = false;
        }

        protected override EditorTexture DefaultIcon()
        {
            return PathUtil.Load("flow_reroute", CommunityEditorPath.Fundamentals);
        }

        protected override EditorTexture DefinedIcon()
        {
            return PathUtil.Load("flow_reroute", CommunityEditorPath.Fundamentals);
        }
    }
} 