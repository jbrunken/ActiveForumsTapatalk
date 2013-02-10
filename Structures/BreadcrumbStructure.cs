using CookComputing.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct BreadcrumbStructure
    {
        [XmlRpcMember("forum_id")]
        public string ForumId;

        [XmlRpcMember("forum_name")]
        public byte[] Name;

        [XmlRpcMember("sub_only")] 
        public bool IsCategory;
    }
}