using System.Collections.Generic;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class PostSearchResults
    {
        public int TotalPosts { get; set; }
        public int SearchId { get; set; }

        public IEnumerable<ForumPost> Topics { get; set; }
    }
}