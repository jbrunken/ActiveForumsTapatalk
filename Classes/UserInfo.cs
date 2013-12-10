

using System;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class UserInfo
    {
        public int UserId { get; set; }
        public int PostCount { get; set; }
        public string UserCaption { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastActivity { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DisplayName { get; set; }
        public bool IsUserOnline { get; set; } 
        public string DateLastPost { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollower { get; set; }
        public bool Following { get; set; }
    }
}