using ActiveForumsTapatalk.XmlRpc;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Structures
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct TopicListStructure
    {
        [XmlRpcMember("total_topic_num")]
        public int TopicCount;

        [XmlRpcMember("forum_id")]
        public string ForumId;

        [XmlRpcMember("forum_name")]
        public byte[] ForumName;

        [XmlRpcMember("can_post")]
        public bool CanPost;

        [XmlRpcMember("topics")]
        public TopicStructure[] Topics;
    }
}