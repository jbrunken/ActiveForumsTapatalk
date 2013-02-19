using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct ForumStructure
    {
        [XmlRpcMember("forum_id")]
        public string ForumId;

        [XmlRpcMember("forum_name")]
        public byte[] Name;

        [XmlRpcMember("description")]
        public byte[] Description;

        [XmlRpcMember("parent_id")]
        public string ParentId;

        [XmlRpcMember("logo_url")]
        public string LogoUrl;

        [XmlRpcMember("new_post")]
        public bool HasNewPosts;

        [XmlRpcMember("is_protected")]
        public bool IsProtected;

        [XmlRpcMember("is_subscribed")]
        public bool IsSubscribed;

        [XmlRpcMember("can_subscribe")]
        public bool CanSubscribe;

        [XmlRpcMember("url")]
        public string Url;

        [XmlRpcMember("sub_only")]
        public bool IsGroup;

        [XmlRpcMember("child")]
        public ForumStructure[] Children;

    }
}