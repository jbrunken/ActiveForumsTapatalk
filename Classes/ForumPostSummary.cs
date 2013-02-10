namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class ForumPostSummary
    {
        public int ForumGroupId { get; set; }
        public string GroupName { get; set; }
        public int ForumId { get; set; }
        public string ForumName { get; set; }
        public int ParentForumId { get; set; }
        public string ParentForumName { get; set; }
        public int ReplyCount { get; set; }
        public int LastPostId { get; set; }
        public int SubscriptionType { get; set; }
        public string Subject { get; set; }
        public bool IsPinned { get; set; }
        public bool IsLocked { get; set; }
    }
}