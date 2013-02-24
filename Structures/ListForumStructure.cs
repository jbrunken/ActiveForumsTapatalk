using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public class ListForumStructure
    {
        [XmlRpcMember("forum_id")]
        public string ForumId;

        [XmlRpcMember("forum_name")] 
        public byte[] ForumName;

        [XmlRpcMember("is_protected")] 
        public bool IsProtected;

        [XmlRpcMember("new_post")] 
        public bool HasNewPosts;
    }
}