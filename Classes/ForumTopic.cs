using System;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class ForumTopic
    {
        public int ForumId { get; set; }
        public int LastReplyId { get; set; }
        public int TopicId { get; set; }
        public int ViewCount { get; set; }
        public int ReplyCount { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPinned { get; set; }
        public string TopicIcon { get; set; }
        public int StatusId { get; set; }
        public bool IsAnnounce { get; set; }
        public DateTime AnnounceStart { get; set; }
        public DateTime AnnounceEnd { get; set; }
        public string TopicType { get; set; }
        public string Subject { get; set; }
        public string Summary { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string Body { get; set; }
        public DateTime DateCreated { get; set; }
        public string AuthorUserName { get; set; }
        public string AuthorFirstName { get; set; }
        public string AuthorLastName { get; set; }
        public string AuthorDisplayName { get; set; }
        public string LastReplySubject { get; set; }
        public string LastReplySummary { get; set; }
        public int LastReplyAuthorId { get; set; }
        public string LastReplyAuthorName { get; set; }
        public string LastReplyUserName { get; set; }
        public string LastReplyFirstName { get; set; }
        public string LastReplyLastName { get; set; }
        public string LastReplyDisplayName { get; set; }
        public DateTime LastReplyDate { get; set; }
        public int UserLastReplyRead { get; set; }
        public int UserLastTopicRead { get; set; }
        public int SubscriptionType { get; set; }

        // Used by GetSubscribedTopics
        public int SubscribedTopicCount { get; set; } 
        public string ForumName { get; set; }
    }
}