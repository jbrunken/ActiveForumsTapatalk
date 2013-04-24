using System;
using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public class ExtendedPostStructure
    {
        [XmlRpcMember("forum_id")]
        public string ForumId;

        [XmlRpcMember("forum_name")]
        public byte[] ForumName;

        [XmlRpcMember("topic_id")]
        public string TopicId;

        [XmlRpcMember("topic_title")]
        public byte[] TopicTitle;

        [XmlRpcMember("post_id")] 
        public string PostID;

        [XmlRpcMember("post_title")]
        public byte[] PostTitle;

        [XmlRpcMember("post_author_id")] 
        public string AuthorId;

        [XmlRpcMember("post_author_name")] 
        public byte[] AuthorName;

        [XmlRpcMember("post_time")] public 
        DateTime PostDate;

        [XmlRpcMember("icon_url")]
        public string AuthorAvatarUrl;

        [XmlRpcMember("short_content")]
        public byte[] Summary;
    }
}