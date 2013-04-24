using System.Collections.Generic;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class TopicSearchResults
    {
        public int TotalTopics { get; set; }
        public int SearchId { get; set; }

        public IEnumerable<ForumTopic> Topics { get; set; }
    }
}