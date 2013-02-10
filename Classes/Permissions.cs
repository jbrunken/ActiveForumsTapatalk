

using DotNetNuke.ComponentModel.DataAnnotations;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    [TableName("activeforums_Permissions)")]
    [PrimaryKey("PermissionId", AutoIncrement = true)]
    public class Permissions
    {
        public int PermissionsId { get; set; }
        public string CanView { get; set; }
        public string CanRead { get; set; }
        public string CanCreate { get; set; }
        public string CanReply { get; set; }
        public string CanEdit { get; set; }
        public string CanDelete { get; set; }
        public string CanLock { get; set; }
        public string CanPin { get; set; }
        public string CanAttach { get; set; }
        public string CanPoll { get; set; }
        public string CanBlock { get; set; }
        public string CanTrust { get; set; }
        public string CanSubscribe { get; set; }
        public string CanAnnounce { get; set; }
        public string CanModApprove { get; set; }
        public string CanModMove { get; set; }
        public string CanModSplit { get; set; }
        public string CanModDelete { get; set; }
        public string CanModUser { get; set; }
        public string CanModEdit { get; set; }
        public string CanModLock { get; set; }
        public string CanModPin { get; set; }
        public string CanTag { get; set; }
        public string CanCategorize { get; set; }
        public string CanPrioritize { get; set; }
    }
}