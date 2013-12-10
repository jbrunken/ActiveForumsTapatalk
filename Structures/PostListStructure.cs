using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    public class PostListStructure
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
    }

    public class PostListPlusPositionStructure : PostListStructure
    {
        public PostListPlusPositionStructure()
        {
            
        }

        public PostListPlusPositionStructure(PostListStructure postList, int position)
        {
            PostCount = postList.PostCount;
            ForumId = postList.ForumId;
            ForumName = postList.ForumName;
            TopicId = postList.TopicId;
            Subject = postList.Subject;
            IsSubscribed = postList.IsSubscribed;
            CanSubscribe = postList.CanSubscribe;
            IsLocked = postList.IsLocked;
            CanReply = postList.CanReply;
            Posts = postList.Posts;
            Breadcrumbs = postList.Breadcrumbs;
            Position = position;
        }

        [XmlRpcMember("position")] 
        public int Position; 
    }
}