using System;
using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct TopicStatusStructure
    {
        [XmlRpcMember("topic_id")]
        public string TopicId;

        [XmlRpcMember("is_subscribed")]
        public bool IsSubscribed;

        [XmlRpcMember("can_subscribe")]
        public bool CanSubscribe;

        [XmlRpcMember("is_closed")]
        public bool IsLocked;

        [XmlRpcMember("last_reply_time")]
        public DateTime LastReplyDate;

        [XmlRpcMember("new_post")]
        public bool HasNewPosts;

        [XmlRpcMember("reply_number")]
        public int ReplyCount;

        [XmlRpcMember("view_number")]
        public int ViewCount;

    }
}