using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    public struct PostListStructure
    {
        [XmlRpcMember("total_post_num")] 
        public int PostCount;

        [XmlRpcMember("forum_id")] 
        public int ForumId;

        [XmlRpcMember("forum_topic")] 
        public byte[] ForumName;

        [XmlRpcMember("topic_id")] 
        public int TopicId;

        [XmlRpcMember("topic_title")] 
        public byte[] Subject;

        [XmlRpcMember("is_subscribed")] 
        public bool IsSubscribed;

        [XmlRpcMember("can_subscribe")] 
        public bool CanSubscribe;

        [XmlRpcMember("is_closed")] 
        public bool IsLocked;

        [XmlRpcMember("can_reply")] 
        public bool CanReply;

        [XmlRpcMember("posts")]
        public PostStructure[] Posts;

        [XmlRpcMember("breadcrumb")]
        public BreadcrumbStructure[] Breadcrumbs;

        [XmlRpcMember("position")] 
        public int Position;
    }
}