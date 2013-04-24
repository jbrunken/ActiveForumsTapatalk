using System;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class ForumPost
    {
        public int ForumId { get; set; }
        public string ForumName { get; set; }
        public int TopicId { get; set; }
        public int ReplyId { get; set; }
        public int ContentId { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public string Subject { get; set; }
        public string PostSubject { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DisplayName { get; set; }
        public bool IsUserOnline { get; set; }
    }
}