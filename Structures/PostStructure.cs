using System;
using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public class PostStructure
    {
        [XmlRpcMember("post_id")] 
        public string PostID;

        [XmlRpcMember("post_title")]
        public byte[] Subject;

        [XmlRpcMember("post_content")] 
        public byte[] Body;

        [XmlRpcMember("post_author_name")] 
        public byte[] AuthorName;

        [XmlRpcMember("post_author_id")]
        public string AuthorId;

        [XmlRpcMember("is_online")] 
        public bool IsOnline;

        [XmlRpcMember("can_edit")] 
        public bool CanEdit;

        [XmlRpcMember("icon_url")]
        public string AuthorAvatarUrl;

        [XmlRpcMember("post_time")] public 
        DateTime PostDate;

    }
}