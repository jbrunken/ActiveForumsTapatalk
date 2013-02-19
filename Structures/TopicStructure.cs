using System;
using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct TopicStructure
    {
        [XmlRpcMember("forum_id")]
        public string ForumId;

        [XmlRpcMember("topic_id")]
        public string TopicId;

        [XmlRpcMember("topic_title")]
        public byte[] Title;

        [XmlRpcMember("topic_author_name")] 
        public byte[] AuthorName;

        [XmlRpcMember("is_subscribed")] 
        public bool IsSubscribed;

        [XmlRpcMember("can_subscribe")] 
        public bool CanSubscribe;

        [XmlRpcMember("is_closed")] 
        public bool IsLocked;

        [XmlRpcMember("icon_url")] 
        public string AuthorAvatarUrl;

        [XmlRpcMember("last_reply_time")] 
        public DateTime LastReplyDate;

        [XmlRpcMember("reply_number")] 
        public int ReplyCount;

        [XmlRpcMember("new_post")] 
        public bool HasNewPosts;

        [XmlRpcMember("view_number")] 
        public int ViewCount;

        [XmlRpcMember("short_content")] 
        public byte[] Summary;
    }
}