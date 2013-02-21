using System;
using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct SubscribedTopicStructure
    {
        [XmlRpcMember("forum_id")]
        public string ForumId;

        [XmlRpcMember("forum_name")] 
        public byte[] ForumName;

        [XmlRpcMember("topic_id")]
        public string TopicId;

        [XmlRpcMember("topic_title")]
        public byte[] Title;

        [XmlRpcMember("post_author_name")]
        public byte[] AuthorName;

        [XmlRpcMember("post_time")] 
        public DateTime DateCreated;

        [XmlRpcMember("is_closed")]
        public bool IsLocked;

        [XmlRpcMember("icon_url")]
        public string AuthorAvatarUrl;

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