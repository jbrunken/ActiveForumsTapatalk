using DotNetNuke.ComponentModel.DataAnnotations;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    [TableName("activeforums_Subscriptions")]
    [PrimaryKey("Id", AutoIncrement = true)]
    public class ForumSubscription
    {
        public int Id { get; set; }
        public int PortalId { get; set; }
        public int ModuleId { get; set; }
        public int ForumId { get; set; }
        public int TopicId { get; set; }
        public int Mode { get; set; }
        public int UserId { get; set; }
    }
}