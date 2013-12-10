using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;
using ActiveForumsTapatalk.XmlRpc;
using DotNetNuke.Entities.Users;
using DotNetNuke.Modules.ActiveForums;
using DotNetNuke.Modules.ActiveForumsTapatalk.Classes;
using DotNetNuke.Modules.ActiveForumsTapatalk.Structures;
using DotNetNuke.Modules.ActiveForumsTapatalk.Extensions;
using DotNetNuke.Security.Membership;
using DotNetNuke.Services.Social.Notifications;
using HtmlAgilityPack;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Handlers
{
    [XmlRpcService(Name = "ActiveForums.Tapatalk", Description = "Tapatalk Service For Active Forums", UseIntTag = true, AppendTimezoneOffset = true)]
    public class TapatalkAPIHandler : XmlRpcService
    {
        private enum ProcessModes { Normal, TextOnly, Quote }

        #region Forum API

        [XmlRpcMethod("get_config")]
        public XmlRpcStruct GetConfig()
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context"); 

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false"); 

            var rpcstruct = new XmlRpcStruct
            {
                {"version", "dev"}, 
                {"is_open", aftContext.ModuleSettings.IsOpen}, 
                {"api_level", "4"},
                {"guest_okay", aftContext.ModuleSettings.AllowAnonymous},
                {"disable_bbcode", "0"},
                {"min_search_length", "3"},
                {"reg_url", "register.aspx"},
                {"charset", "UTF-8"},
                {"subscribe_forum", "1"},
                {"multi_quote", "1"},
                {"goto_unread", "1"},
                {"goto_post", "1"},
                {"announcement", "1"},
                {"no_refresh_on_post", "1"},
                {"subscribe_load", "1"},
                {"user_id", "1"},
                {"avatar", "0"},
                {"disable_subscribe_forum", "0"},
                {"get_latest_topic", "1"},
                {"mark_read", "1"},
                {"mark_forum", "1"},
                {"get_forum_status", "1"},
                {"hide_forum_id", ""},
                {"mark_topic_read", "1"},
                {"get_topic_status", "1"},
                {"get_participated_forum", "1"},
                {"get_forum", "1"},
                {"guest_search", aftContext.ModuleSettings.SearchPermission == ActiveForumsTapatalkModuleSettings.SearchPermissions.Everyone ? "1" : "0"},
                {"advanced_search", "0"},
                {"searchid", "1"},
                /* Not Yet Implemented */
                {"can_unread", "0"},
                {"conversation", "0"},
                {"inbox_stat", "0"},
                {"push", "0"},   
                {"allow_moderate", "0"},
                {"report_post", "0"},
                {"report_pm", "0"},
                {"get_id_by_url", "0"},
                {"delete_reason", "0"},
                {"mod_approve", "0"},
                {"mod_delete", "0"},
                {"mod_report", "0"},
                {"pm_load", "0"},              
                {"mass_subscribe", "0"},
                {"emoji", "0"},
                {"get_smiles", "0"},
                {"get_online_users", "0"},
                {"mark_pm_unread", "0"},
                
                {"get_alert", "0"},
                {"advanced_delete", "0"},
                {"default_smiles", "0"}
            };        

            return rpcstruct;

        }


        [XmlRpcMethod("get_forum")]
        public ForumStructure[] GetForums(params object[] parameters)
        {
            var includeDescription = (parameters == null || parameters.Length == 0) || Convert.ToBoolean(parameters[0]);

            if (parameters == null || parameters.Length <= 1)
                return GetForums(includeDescription);

            var forumId = Convert.ToString(parameters[1]);

            return GetForums(forumId, includeDescription);
        }

        private ForumStructure[] GetForums(bool includeDescription)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, "CanRead");
            var forumTable = fc.GetForumView(aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, aftContext.UserId, aftContext.ForumUser.IsSuperUser, forumIds);
            var forumSubscriptions = fc.GetSubscriptionsForUser(aftContext.ModuleSettings.ForumModuleId, aftContext.UserId, null, 0).ToList();

            var result = new List<ForumStructure>();

            // Note that all the fields in the DataTable are strings if they come back from the cache, so they have to be converted appropriately.

            // Get the distict list of groups
            var groups = forumTable.AsEnumerable()
                .Select(r => new
                {
                    ID = Convert.ToInt32(r["ForumGroupId"]),
                    Name = r["GroupName"].ToString(),
                    SortOrder = Convert.ToInt32(r["GroupSort"]),
                    Active = Convert.ToBoolean(r["GroupActive"])
                }).Distinct().Where(o => o.Active).OrderBy(o => o.SortOrder);

            // Get all forums the user can read
            var visibleForums = forumTable.AsEnumerable()
                .Select(f => new
                {
                    ID = Convert.ToInt32(f["ForumId"]),
                    ForumGroupId = Convert.ToInt32(f["ForumGroupId"]),
                    Name = f["ForumName"].ToString(),
                    Description = f["ForumDesc"].ToString(),
                    ParentForumId = Convert.ToInt32(f["ParentForumId"]),
                    ReadRoles = f["CanRead"].ToString(),
                    SubscribeRoles = f["CanSubscribe"].ToString(),
                    LastRead = Convert.ToDateTime(f["LastRead"]),
                    LastPostDate = Convert.ToDateTime(f["LastPostDate"]),
                    SortOrder = Convert.ToInt32(f["ForumSort"]),
                    Active = Convert.ToBoolean(f["ForumActive"])
                })
                .Where(o => o.Active && ActiveForums.Permissions.HasPerm(o.ReadRoles, aftContext.ForumUser.UserRoles))
                .OrderBy(o => o.SortOrder).ToList();

            foreach (var group in groups)
            {
                // Find any root level forums for this group
                var groupForums = visibleForums.Where(vf => vf.ParentForumId == 0 && vf.ForumGroupId == group.ID).ToList();

                if (!groupForums.Any())
                    continue;

                // Create the structure to represent the group
                var groupStructure = new ForumStructure()
                {
                    ForumId = "G" + group.ID.ToString(), // Append G to distinguish between forums and groups with the same id.
                    Name = group.Name.ToBytes(),
                    Description = null,
                    ParentId = "-1",
                    LogoUrl = null,
                    HasNewPosts = false,
                    IsProtected = false,
                    IsSubscribed = false,
                    CanSubscribe = false,
                    Url = null,
                    IsGroup = true,
                };

                // Add the Child Forums
                var groupChildren = new List<ForumStructure>();
                foreach (var groupForum in groupForums)
                {
                    var forumStructure = new ForumStructure
                    {
                        ForumId = groupForum.ID.ToString(),
                        Name = Utilities.StripHTMLTag(groupForum.Name).ToBytes(),
                        Description = includeDescription ? Utilities.StripHTMLTag(groupForum.Description).ToBytes() : string.Empty.ToBytes(),
                        ParentId = 'G' + group.ID.ToString(),
                        LogoUrl = null,
                        HasNewPosts = aftContext.UserId > 0 && groupForum.LastPostDate > groupForum.LastRead,
                        IsProtected = false,
                        IsSubscribed = forumSubscriptions.Any(fs => fs.ForumId == groupForum.ID),
                        CanSubscribe = ActiveForums.Permissions.HasPerm(groupForum.SubscribeRoles, aftContext.ForumUser.UserRoles),
                        Url = null,
                        IsGroup = false
                    };

                    // Add any sub-forums

                    var subForums = visibleForums.Where(vf => vf.ParentForumId == groupForum.ID).ToList();

                    if (subForums.Any())
                    {
                        var forumChildren = new List<ForumStructure>();

                        foreach (var subForum in subForums)
                        {
                            forumChildren.Add(new ForumStructure
                            {
                                ForumId = subForum.ID.ToString(),
                                Name = Utilities.StripHTMLTag(subForum.Name).ToBytes(),
                                Description = includeDescription ? Utilities.StripHTMLTag(subForum.Description).ToBytes() : String.Empty.ToBytes(),
                                ParentId = groupForum.ID.ToString(),
                                LogoUrl = null,
                                HasNewPosts = aftContext.UserId > 0 && subForum.LastPostDate > subForum.LastRead,
                                IsProtected = false,
                                IsSubscribed = forumSubscriptions.Any(fs => fs.ForumId == subForum.ID),
                                CanSubscribe = ActiveForums.Permissions.HasPerm(subForum.SubscribeRoles, aftContext.ForumUser.UserRoles),
                                Url = null,
                                IsGroup = false
                            });
                        }

                        forumStructure.Children = forumChildren.ToArray();
                    }

                    groupChildren.Add(forumStructure);
                }

                groupStructure.Children = groupChildren.ToArray();

                result.Add(groupStructure);
            }

            return result.ToArray();
        }

        private ForumStructure[] GetForums(string forumId, bool includeDescription)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, "CanRead");
            var forumTable = fc.GetForumView(aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, aftContext.UserId, aftContext.ForumUser.IsSuperUser, forumIds);
            var forumSubscriptions = fc.GetSubscriptionsForUser(aftContext.ModuleSettings.ForumModuleId, aftContext.UserId, null, 0).ToList();

            var result = new List<ForumStructure>();

            // Note that all the fields in the DataTable are strings if they come back from the cache, so they have to be converted appropriately.

            // Get the distict list of groups
            /*
            var groups = forumTable.AsEnumerable()
                .Select(r => new
                {
                    ID = Convert.ToInt32(r["ForumGroupId"]),
                    Name = r["GroupName"].ToString(),
                    SortOrder = Convert.ToInt32(r["GroupSort"]),
                    Active = Convert.ToBoolean(r["GroupActive"])
                }).Distinct().Where(o => o.Active).OrderBy(o => o.SortOrder);
             */

            // Get all forums the user can read
            var visibleForums = forumTable.AsEnumerable()
                .Select(f => new
                {
                    ID = Convert.ToInt32(f["ForumId"]),
                    ForumGroupId = Convert.ToInt32(f["ForumGroupId"]),
                    Name = f["ForumName"].ToString(),
                    Description = f["ForumDesc"].ToString(),
                    ParentForumId = Convert.ToInt32(f["ParentForumId"]),
                    ReadRoles = f["CanRead"].ToString(),
                    SubscribeRoles = f["CanSubscribe"].ToString(),
                    LastRead = Convert.ToDateTime(f["LastRead"]),
                    LastPostDate = Convert.ToDateTime(f["LastPostDate"]),
                    SortOrder = Convert.ToInt32(f["ForumSort"]),
                    Active = Convert.ToBoolean(f["ForumActive"])
                })
                .Where(o => o.Active && ActiveForums.Permissions.HasPerm(o.ReadRoles, aftContext.ForumUser.UserRoles))
                .OrderBy(o => o.SortOrder).ToList();


            if(forumId.StartsWith("G"))
            {
                var groupId = Convert.ToInt32(forumId.Substring(1));
                
                foreach(var forum in visibleForums.Where(f => f.ForumGroupId == groupId && f.ParentForumId == 0))
                {
                    var forumStructure = new ForumStructure
                    {
                        ForumId = forum.ID.ToString(),
                        Name = Utilities.StripHTMLTag(forum.Name).ToBytes(),
                        Description = includeDescription ? Utilities.StripHTMLTag(forum.Description).ToBytes() : string.Empty.ToBytes(),
                        ParentId = forumId,
                        LogoUrl = null,
                        HasNewPosts = aftContext.UserId > 0 && forum.LastPostDate > forum.LastRead,
                        IsProtected = false,
                        IsSubscribed = forumSubscriptions.Any(fs => fs.ForumId == forum.ID),
                        CanSubscribe = ActiveForums.Permissions.HasPerm(forum.SubscribeRoles, aftContext.ForumUser.UserRoles),
                        Url = null,
                        IsGroup = false
                    };

                    result.Add(forumStructure);
                }
            }
            else
            {
                foreach (var forum in visibleForums.Where(f => f.ParentForumId == int.Parse(forumId)))
                {
                    var forumStructure = new ForumStructure
                    {
                        ForumId = forum.ID.ToString(),
                        Name = Utilities.StripHTMLTag(forum.Name).ToBytes(),
                        Description = includeDescription ? Utilities.StripHTMLTag(forum.Description).ToBytes() : string.Empty.ToBytes(),
                        ParentId = forumId,
                        LogoUrl = null,
                        HasNewPosts = aftContext.UserId > 0 && forum.LastPostDate > forum.LastRead,
                        IsProtected = false,
                        IsSubscribed = forumSubscriptions.Any(fs => fs.ForumId == forum.ID),
                        CanSubscribe = ActiveForums.Permissions.HasPerm(forum.SubscribeRoles, aftContext.ForumUser.UserRoles),
                        Url = null,
                        IsGroup = false
                    };

                    result.Add(forumStructure);
                }
            }

            return result.ToArray();
        }


        [XmlRpcMethod("mark_all_as_read")]
        public XmlRpcStruct MarkForumAsRead(params object[] parameters)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var forumId = (parameters != null && parameters.Any()) ? Convert.ToString(parameters[0]) : "0";

            if(!forumId.StartsWith("G")) // Don't do anything for groups
                DataProvider.Instance().Utility_MarkAllRead(aftContext.ModuleSettings.ForumModuleId, aftContext.UserId, int.Parse(forumId));

            return new XmlRpcStruct
            {
                {"result", true}
            };
        }

        [XmlRpcMethod("get_participated_forum")]
        public XmlRpcStruct GetParticipatedForums()
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var subscribedForums = fc.GetParticipatedForums(portalId, forumModuleId, userId, forumIds).ToList();

            return new XmlRpcStruct
                       {
                           {"total_forums_num", subscribedForums.Count},
                           {"forums", subscribedForums.Select(f => new ListForumStructure
                                {
                                    ForumId = f.ForumId.ToString(),
                                    ForumName = f.ForumName.ToBytes(),
                                    IsProtected = false,
                                    HasNewPosts =  f.LastPostDate > f.LastAccessDate
                                }).ToArray()}
                       };
        }

        [XmlRpcMethod("get_forum_status")]
        public XmlRpcStruct GetForumStatus(params object[] parameters)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead") + string.Empty;

            // Clean up our forum id list before we split it up.
            forumIds = Regex.Replace(forumIds, @"\;+$", string.Empty);

            var forumIdList = !string.IsNullOrWhiteSpace(forumIds)
                                  ? forumIds.Split(';').Select(int.Parse).ToList()
                                  : new List<int>();

            // Intersect requested forums with avialable forums
            var requestedForumIds = (parameters != null && parameters.Any())
                                        ? ((object[]) parameters[0]).Select(Convert.ToInt32).Where(forumIdList.Contains)
                                        : new int[] {};

            // Convert the new list of forums back to a string for the proc.
            forumIds = requestedForumIds.Aggregate(string.Empty, (current, id) => current + (id.ToString() + ";"));

            var forumStatus = fc.GetForumStatus(portalId, forumModuleId, userId, forumIds).ToList();

            return new XmlRpcStruct
                       {
                           {"forums", forumStatus.Select(f => new ListForumStructure
                                {
                                    ForumId = f.ForumId.ToString(),
                                    ForumName = f.ForumName.ToBytes(),
                                    IsProtected = false,
                                    HasNewPosts =  f.LastPostDate > f.LastAccessDate
                                }).ToArray()}
                       };
        }

        #endregion

        #region Topic API

        [XmlRpcMethod("get_topic")]
        public TopicListStructure  GetTopic(params object[] parameters)
        {
            if(parameters[0].ToString().StartsWith("G"))
            {
                var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

                if (aftContext == null || aftContext.Module == null)
                    throw new XmlRpcFaultException(100, "Invalid Context");

                Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

                return new TopicListStructure
                {
                    CanPost = false,
                    ForumId = parameters[0].ToString(),
                    ForumName = string.Empty.ToBytes(),
                    TopicCount = 0
                };
            }


            if (parameters.Length == 3)
                return GetTopic(Convert.ToInt32(parameters[0]), Convert.ToInt32(parameters[1]), Convert.ToInt32(parameters[2]));
            
            if (parameters.Length == 4)
                return GetTopic(Convert.ToInt32(parameters[0]), Convert.ToInt32(parameters[1]), Convert.ToInt32(parameters[2]), parameters[3].ToString());

            throw new XmlRpcFaultException(100, "Invalid Method Signature");
        }

        private TopicListStructure GetTopic(int forumId, int startIndex, int endIndex, string mode = null)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;

            var fc = new AFTForumController();

            var fp = fc.GetForumPermissions(forumId);

            if (!ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fp.CanRead))
                throw new XmlRpcFaultException(100, "No Read Permissions");

            var maxRows = endIndex + 1 - startIndex;

            var forumTopicsSummary = fc.GetForumTopicSummary(portalId, forumModuleId, forumId, aftContext.UserId, mode);
            var forumTopics = fc.GetForumTopics(portalId, forumModuleId, forumId, aftContext.UserId, startIndex, maxRows, mode);

            var canSubscribe = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fp.CanSubscribe);

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var profilePath = string.Format("{0}://{1}{2}", Context.Request.Url.Scheme, Context.Request.Url.Host, VirtualPathUtility.ToAbsolute("~/profilepic.ashx"));

            var forumTopicsStructure = new TopicListStructure
                                           {
                                               CanPost = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fp.CanCreate),
                                               ForumId = forumId.ToString(),
                                               ForumName = forumTopicsSummary.ForumName.ToBytes(),
                                               TopicCount = forumTopicsSummary.TopicCount,
                                               Topics = forumTopics.Select(t => new TopicStructure{ 
                                                   TopicId = t.TopicId.ToString(),
                                                   AuthorAvatarUrl = string.Format("{0}?userId={1}&w=64&h=64", profilePath, t.AuthorId),
                                                   AuthorName = GetAuthorName(mainSettings, t).ToBytes(),
                                                   CanSubscribe = canSubscribe,
                                                   ForumId = forumId.ToString(),
                                                   HasNewPosts =  (t.LastReplyId < 0 && t.TopicId > t.UserLastTopicRead) || t.LastReplyId > t.UserLastReplyRead,
                                                   IsLocked = t.IsLocked,
                                                   IsSubscribed = t.SubscriptionType > 0,
                                                   LastReplyDate = t.LastReplyDate,
                                                   ReplyCount = t.ReplyCount,
                                                   Summary = GetSummary(t.Summary, t.Body).ToBytes(),
                                                   ViewCount = t.ViewCount,
                                                   Title = HttpUtility.HtmlDecode(t.Subject + string.Empty).ToBytes()
                                               }).ToArray()
                                           };
                                             
                             

            return forumTopicsStructure;
        }

        [XmlRpcMethod("new_topic")]
        public XmlRpcStruct NewTopic(params object[] parameters)
        {
            if (parameters.Length < 3)
                throw new XmlRpcFaultException(100, "Invalid Method Signature");

            var forumId = Convert.ToInt32(parameters[0]);
            var subject = Encoding.UTF8.GetString((byte[])parameters[1]);
            var body = Encoding.UTF8.GetString((byte[])parameters[2]);

            var prefixId = parameters.Length >= 4 ? Convert.ToString(parameters[3]) : null;
            var attachmentIdObjArray = parameters.Length >= 5 ? (object[])parameters[4] : null;
            var groupId = parameters.Length >= 6 ? Convert.ToString(parameters[5]) : null;

            var attachmentIds = (attachmentIdObjArray != null)
                        ? attachmentIdObjArray.Select(Convert.ToString)
                        : new string[] { }; 

            return NewTopic(forumId, subject, body, prefixId, attachmentIds, groupId);

        }

        private XmlRpcStruct NewTopic(int forumId, string subject, string body, string prefixId, IEnumerable<string> attachmentIds, string groupId)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);
            
            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;

            var fc = new AFTForumController();

            var forumInfo = fc.GetForum(portalId, forumModuleId, forumId);

            // Verify Post Permissions
            if(!ActiveForums.Permissions.HasPerm(forumInfo.Security.Create, aftContext.ForumUser.UserRoles))
            {
                return new XmlRpcStruct
                                {
                                    {"result", "false"}, //"true" for success
                                    {"result_text", "Not Authorized to Post".ToBytes()}, 
                                };
            }

            // Build User Permissions
            var canModApprove = ActiveForums.Permissions.HasPerm(forumInfo.Security.ModApprove, aftContext.ForumUser.UserRoles);
            var canTrust = ActiveForums.Permissions.HasPerm(forumInfo.Security.Trust, aftContext.ForumUser.UserRoles);
            var userProfile =  aftContext.UserId > 0 ? aftContext.ForumUser.Profile : new UserProfileInfo { TrustLevel = -1 };
            var userIsTrusted = Utilities.IsTrusted((int)forumInfo.DefaultTrustValue, userProfile.TrustLevel, canTrust, forumInfo.AutoTrustLevel, userProfile.PostCount);

            // Determine if the post should be approved
            var isApproved = !forumInfo.IsModerated || userIsTrusted || canModApprove;

            var mainSettings = new SettingsInfo {MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId)};

            var dnnUser = Entities.Users.UserController.GetUserById(portalId, aftContext.UserId);

            var authorName = GetAuthorName(mainSettings, dnnUser);

            var themePath = string.Format("{0}://{1}{2}", Context.Request.Url.Scheme, Context.Request.Url.Host, VirtualPathUtility.ToAbsolute("~/DesktopModules/activeforums/themes/" + mainSettings.Theme + "/"));

            subject = Utilities.CleanString(portalId, subject, false, EditorTypes.TEXTBOX, forumInfo.UseFilter, false, forumModuleId, themePath, false);
            body = Utilities.CleanString(portalId, TapatalkToHtml(body), forumInfo.AllowHTML, EditorTypes.HTMLEDITORPROVIDER, forumInfo.UseFilter, false, forumModuleId, themePath, forumInfo.AllowEmoticons);

            // Create the topic

            var ti = new TopicInfo();
            var dt = DateTime.Now;
            ti.Content.DateCreated = dt;
            ti.Content.DateUpdated = dt;
            ti.Content.AuthorId = aftContext.UserId;
            ti.Content.AuthorName = authorName;
            ti.Content.IPAddress = Context.Request.UserHostAddress;
            ti.TopicUrl = string.Empty;
            ti.Content.Body = body;
            ti.Content.Subject = subject;
            ti.Content.Summary = string.Empty;

            ti.IsAnnounce = false;
            ti.IsPinned = false;
            ti.IsLocked = false;
            ti.IsDeleted = false;
            ti.IsArchived = false;

            ti.StatusId = -1;
            ti.TopicIcon = string.Empty;
            ti.TopicType = 0;

            ti.IsApproved = isApproved;

            // Save the topic
            var tc = new TopicsController();
            var topicId = tc.TopicSave(portalId, ti);
            ti = tc.Topics_Get(portalId, forumModuleId, topicId, forumId, -1, false);

            if(ti == null)
            {
                return new XmlRpcStruct
                                {
                                    {"result", "false"}, //"true" for success
                                    {"result_text", "Error Creating Post".ToBytes()}, 
                                };
            }

            // Update stats
            tc.Topics_SaveToForum(forumId, topicId, portalId, forumModuleId);
            if (ti.IsApproved && ti.Author.AuthorId > 0)
            {
                var uc = new ActiveForums.Data.Profiles();
                uc.Profile_UpdateTopicCount(portalId, ti.Author.AuthorId);
            }


            try
            {
                // Clear the cache
                var cachekey = string.Format("AF-FV-{0}-{1}", portalId, forumModuleId);
                DataCache.CacheClearPrefix(cachekey);

                // Subscribe the user if they have auto-subscribe set.
                if (userProfile.PrefSubscriptionType != SubscriptionTypes.Disabled && !(Subscriptions.IsSubscribed(portalId, forumModuleId, forumId, topicId, SubscriptionTypes.Instant, aftContext.UserId)))
                {
                    new SubscriptionController().Subscription_Update(portalId, forumModuleId, forumId, topicId, (int)userProfile.PrefSubscriptionType, aftContext.UserId, aftContext.ForumUser.UserRoles);
                }

                if(isApproved)
                {
                    // Send User Notifications
                    Subscriptions.SendSubscriptions(portalId, forumModuleId, aftContext.ModuleSettings.ForumTabId, forumInfo, topicId, 0, ti.Content.AuthorId);

                    // Add Journal entry
                    var forumTabId = aftContext.ModuleSettings.ForumTabId;
                    var fullURL = new ControlUtils().BuildUrl(forumTabId, forumModuleId, forumInfo.ForumGroup.PrefixURL, forumInfo.PrefixURL, forumInfo.ForumGroupId, forumInfo.ForumID, topicId, ti.TopicUrl, -1, -1, string.Empty, 1, forumInfo.SocialGroupId);
                    new Social().AddTopicToJournal(portalId, forumModuleId, forumId, topicId, ti.Author.AuthorId, fullURL, ti.Content.Subject, string.Empty, ti.Content.Body, forumInfo.ActiveSocialSecurityOption, forumInfo.Security.Read, forumInfo.SocialGroupId);
                }
                else
                {
                    // Send Mod Notifications
                    var mods = Utilities.GetListOfModerators(portalId, forumId);
                    var notificationType = NotificationsController.Instance.GetNotificationType("AF-ForumModeration");
                    var notifySubject = Utilities.GetSharedResource("NotificationSubjectTopic");
                    notifySubject = notifySubject.Replace("[DisplayName]", dnnUser.DisplayName);
                    notifySubject = notifySubject.Replace("[TopicSubject]", ti.Content.Subject);
                    var notifyBody = Utilities.GetSharedResource("NotificationBodyTopic");
                    notifyBody = notifyBody.Replace("[Post]", ti.Content.Body);
                    var notificationKey = string.Format("{0}:{1}:{2}:{3}:{4}", aftContext.ModuleSettings.ForumTabId, forumModuleId, forumId, topicId, 0);

                    var notification = new Notification
                    {
                        NotificationTypeID = notificationType.NotificationTypeId,
                        Subject = notifySubject,
                        Body = notifyBody,
                        IncludeDismissAction = false,
                        SenderUserID = dnnUser.UserID,
                        Context = notificationKey
                    };

                    NotificationsController.Instance.SendNotification(notification, portalId, null, mods);
                }
 
            }
            catch(Exception ex)
            {
                Services.Exceptions.Exceptions.LogException(ex); 
            }


            var result = new XmlRpcStruct
            {
                {"result", true}, //"true" for success
               // {"result_text", "OK".ToBytes()}, 
                {"topic_id", ti.TopicId.ToString()},
            };

            if(!isApproved)
                result.Add("state", 1);

            return result;

        }

        [XmlRpcMethod("get_unread_topic")]
        public XmlRpcStruct GetUnreadTopics(params object[] parameters)
        {
            var startIndex = parameters.Any() ? Convert.ToInt32(parameters[0]) : 0;
            var endIndex = parameters.Count() > 1 ? Convert.ToInt32(parameters[1]) : startIndex + 49;
            var searchId = parameters.Count() > 2 ? Convert.ToString(parameters[2]) : null;
            var filters = parameters.Count() > 3 ? parameters[3] : null;

            return GetUnreadTopics(startIndex, endIndex, searchId, filters);
        }

        private XmlRpcStruct GetUnreadTopics(int startIndex, int endIndex, string searchId, object filters)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // If user is not signed in, don't return any unread topics
            if(userId <= 0)
                return new XmlRpcStruct
                       {
                           {"total_topic_num", 0},
                           {"topics", new object[]{}}
                       };  


            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var maxRows = endIndex + 1 - startIndex;

            var unreadTopics = fc.GetUnreadTopics(portalId, forumModuleId, userId, forumIds, startIndex, maxRows).ToList();

            return new XmlRpcStruct
                       {
                           {"total_topic_num", unreadTopics.Count > 0 ? unreadTopics[0].TopicCount : 0},
                           {"topics", unreadTopics.Select(t => new ExtendedTopicStructure{ 
                                                   TopicId = t.TopicId.ToString(),
                                                   AuthorAvatarUrl = GetAvatarUrl(t.LastReplyAuthorId),
                                                   AuthorId = t.LastReplyAuthorId.ToString(),
                                                   AuthorName = GetLastReplyAuthorName(mainSettings, t).ToBytes(),
                                                   ForumId = t.ForumId.ToString(),
                                                   ForumName = t.ForumName.ToBytes(),
                                                   HasNewPosts =  (t.LastReplyId < 0 && t.TopicId > t.UserLastTopicRead) || t.LastReplyId > t.UserLastReplyRead,
                                                   IsLocked = t.IsLocked,
                                                   IsSubscribed = t.SubscriptionType > 0,
                                                   CanSubscribe = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fc.GetForumPermissions(t.ForumId).CanSubscribe), // GetforumPermissions uses cache so it shouldn't be a performance issue
                                                   ReplyCount = t.ReplyCount,
                                                   Summary = GetSummary(null, t.LastReplyBody).ToBytes(),
                                                   ViewCount = t.ViewCount,
                                                   DateCreated = t.LastReplyDate,
                                                   Title = HttpUtility.HtmlDecode(t.Subject + string.Empty).ToBytes()
                                               }).ToArray()}
                       };  
        }

        [XmlRpcMethod("get_participated_topic")]
        public XmlRpcStruct GetParticipatedTopics(params object[] parameters)
        {
            var userName = parameters.Any() ?  Encoding.UTF8.GetString((byte[])parameters[0]) : null;
            var startIndex = parameters.Count() > 1 ? Convert.ToInt32(parameters[1]) : 0;
            var endIndex = parameters.Count() > 2 ? Convert.ToInt32(parameters[2]) : startIndex + 49;
            var searchId = parameters.Count() > 3 ? Convert.ToString(parameters[3]) : null;
            var userId = parameters.Count() > 4 ? Convert.ToInt32(parameters[4]) : 0;

            return GetParticipatedTopics(userName, startIndex, endIndex, searchId, userId);
        }

        private XmlRpcStruct GetParticipatedTopics(string participantUserName, int startIndex, int endIndex, string searchId, int participantUserId)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Lookup the user id for the username if needed
            if(participantUserId <= 0)
            {
                var forumUser = new ActiveForums.UserController().GetUser(aftContext.Portal.PortalID, aftContext.ModuleSettings.ForumModuleId, participantUserName);

                if (forumUser != null)
                    participantUserId = forumUser.UserId;
            }

            // If we don't have a valid participant user id at this point, return invalid result
            if(participantUserId <= 0)
            {
                return new XmlRpcStruct
                           {
                               {"result", false},
                               {"result_text", "User Not Found".ToBytes()},
                               {"total_topic_num", 0},
                               {"total_unread_num", 0}
                           };
            }


            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var maxRows = endIndex + 1 - startIndex;

            var participatedTopics = fc.GetParticipatedTopics(portalId, forumModuleId, userId, forumIds, participantUserId, startIndex, maxRows).ToList();

            return new XmlRpcStruct
                       {
                           {"result", true},
                           {"total_topic_num", participatedTopics.Count > 0 ? participatedTopics[0].TopicCount : 0},
                           {"total_unread_num", participatedTopics.Count > 0 ? participatedTopics[0].UnreadTopicCount : 0},
                           {"topics", participatedTopics.Select(t => new ExtendedTopicStructure{ 
                                                   TopicId = t.TopicId.ToString(),
                                                   AuthorAvatarUrl = GetAvatarUrl(t.LastReplyAuthorId),
                                                   AuthorId = t.LastReplyAuthorId.ToString(),
                                                   AuthorName = GetLastReplyAuthorName(mainSettings, t).ToBytes(),
                                                   ForumId = t.ForumId.ToString(),
                                                   ForumName = t.ForumName.ToBytes(),
                                                   HasNewPosts =  (t.LastReplyId < 0 && t.TopicId > t.UserLastTopicRead) || t.LastReplyId > t.UserLastReplyRead,
                                                   IsLocked = t.IsLocked,
                                                   IsSubscribed = t.SubscriptionType > 0,
                                                   CanSubscribe = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fc.GetForumPermissions(t.ForumId).CanSubscribe), // GetforumPermissions uses cache so it shouldn't be a performance issue
                                                   ReplyCount = t.ReplyCount,
                                                   Summary = GetSummary(null, t.LastReplyBody).ToBytes(),
                                                   ViewCount = t.ViewCount,
                                                   DateCreated = t.LastReplyDate,
                                                   Title = HttpUtility.HtmlDecode(t.Subject + string.Empty).ToBytes()
                                               }).ToArray()}
                       };
        }


        [XmlRpcMethod("get_new_topic")]
        public XmlRpcStruct GetNewTopics()
        {
            return GetLatestTopics(0, 100, null, null);
        }


        [XmlRpcMethod("get_latest_topic")]
        public XmlRpcStruct GetLatestTopics(params object[] parameters)
        {
            var startIndex = parameters.Any() ? Convert.ToInt32(parameters[0]) : 0;
            var endIndex = parameters.Count() > 1 ? Convert.ToInt32(parameters[1]) : startIndex + 49;
            var searchId = parameters.Count() > 2 ? Convert.ToString(parameters[2]) : null;
            var filters = parameters.Count() > 3 ? parameters[3] : null;

            return GetLatestTopics(startIndex, endIndex, searchId, filters);
        }

        private XmlRpcStruct GetLatestTopics(int startIndex, int endIndex, string searchId, object filters)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var maxRows = endIndex + 1 - startIndex;

            var latestTopics = fc.GetLatestTopics(portalId, forumModuleId, userId, forumIds, startIndex, maxRows).ToList();

            return new XmlRpcStruct
                       {
                           {"result", true},
                           {"total_topic_num", latestTopics.Count > 0 ? latestTopics[0].TopicCount : 0},
                           {"topics", latestTopics.Select(t => new ExtendedTopicStructure{ 
                                                   TopicId = t.TopicId.ToString(),
                                                   AuthorAvatarUrl = GetAvatarUrl(t.LastReplyAuthorId),
                                                   AuthorId = t.LastReplyAuthorId.ToString(),
                                                   AuthorName = GetLastReplyAuthorName(mainSettings, t).ToBytes(),
                                                   ForumId = t.ForumId.ToString(),
                                                   ForumName = t.ForumName.ToBytes(),
                                                   HasNewPosts =  (t.LastReplyId < 0 && t.TopicId > t.UserLastTopicRead) || t.LastReplyId > t.UserLastReplyRead,
                                                   IsLocked = t.IsLocked,
                                                   IsSubscribed = t.SubscriptionType > 0,
                                                   CanSubscribe = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fc.GetForumPermissions(t.ForumId).CanSubscribe), // GetforumPermissions uses cache so it shouldn't be a performance issue
                                                   ReplyCount = t.ReplyCount,
                                                   Summary = GetSummary(null, t.LastReplyBody).ToBytes(),
                                                   ViewCount = t.ViewCount,
                                                   DateCreated = t.LastReplyDate,
                                                   Title = HttpUtility.HtmlDecode(t.Subject + string.Empty).ToBytes()
                                               }).ToArray()}
                       };
        }


        [XmlRpcMethod("get_topic_status")]
        public XmlRpcStruct GetTopicStatus(params object[] parameters)
        {
            var topicIds = parameters.Any() ? ((object[])parameters[0]).Select(Convert.ToInt32) : new int[]{};

            return GetTopicStatus(topicIds);
        }

        private XmlRpcStruct GetTopicStatus(IEnumerable<int> topicIds)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var topicIdsString = topicIds.Aggregate(string.Empty, (current, topicId) => current + (topicId.ToString() + ";"));

            var unreadTopics = fc.GetTopicStatus(portalId, forumModuleId, userId, forumIds, topicIdsString).ToList();

            return new XmlRpcStruct
                       {
                           {"result", true},
                           {"status", unreadTopics.Select(t => new TopicStatusStructure(){ 
                                                   TopicId = t.TopicId.ToString(),
                                                   HasNewPosts =  (t.LastReplyId < 0 && t.TopicId > t.UserLastTopicRead) || t.LastReplyId > t.UserLastReplyRead,
                                                   IsLocked = t.IsLocked,
                                                   IsSubscribed = t.SubscriptionType > 0,
                                                   CanSubscribe = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fc.GetForumPermissions(t.ForumId).CanSubscribe), // GetforumPermissions uses cache so it shouldn't be a performance issue
                                                   ReplyCount = t.ReplyCount,
                                                   ViewCount = t.ViewCount,
                                                   LastReplyDate = t.LastReplyDate
                                               }).ToArray()}
                       };
        }

        [XmlRpcMethod("mark_topic_read")]
        public XmlRpcStruct MarkTopicsRead(params object[] parameters)
        {
            var topicIds = ((object[]) parameters[0]).Select(Convert.ToInt32);

            return MarkTopicsRead(topicIds);
        }

        public XmlRpcStruct MarkTopicsRead(IEnumerable<int> topicIds)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Build a list of forums the user has access to
            var fc = new AFTForumController();

            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");
            var topicIdsStr = topicIds.Aggregate(string.Empty, (current, topicId) => current + (topicId.ToString() + ";"));

            fc.MarkTopicsRead(portalId, forumModuleId, userId, forumIds, topicIdsStr);

            return new XmlRpcStruct
            {
                {"result", true}
            };
        }


        #endregion

        #region Post API

        [XmlRpcMethod("get_thread")]
        public PostListStructure GetThread(params object[] parameters)
        {
            if (parameters.Length == 3)
                return GetThread(Convert.ToInt32(parameters[0]), Convert.ToInt32(parameters[1]), Convert.ToInt32(parameters[2]), false);

            if (parameters.Length == 4)
                return GetThread(Convert.ToInt32(parameters[0]), Convert.ToInt32(parameters[1]), Convert.ToInt32(parameters[2]), Convert.ToBoolean(parameters[3]));

            throw new XmlRpcFaultException(100, "Invalid Method Signature");
        }

        private PostListStructure GetThread(int topicId, int startIndex, int endIndex, bool returnHtml)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;

            var fc = new AFTForumController();

            var forumId = fc.GetTopicForumId(topicId);

            if(forumId <= 0)
                throw new XmlRpcFaultException(100, "Invalid Topic");

            var fp = fc.GetForumPermissions(forumId);

            if (!ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fp.CanRead))
                throw new XmlRpcFaultException(100, "No Read Permissions");

            var maxRows = endIndex + 1 - startIndex;

            var forumPostSummary = fc.GetForumPostSummary(aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, forumId, topicId, aftContext.UserId);
            var forumPosts = fc.GetForumPosts(aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, forumId, topicId, aftContext.UserId, startIndex, maxRows);

            var breadCrumbs = new List<BreadcrumbStructure>
                                  {
                                      new BreadcrumbStructure
                                          {
                                              ForumId = 'G' + forumPostSummary.ForumGroupId.ToString(),
                                              IsCategory = true,
                                              Name = forumPostSummary.GroupName.ToBytes()
                                          },
                                  };

            // If we're in a sub forum, add the parent to the breadcrumb
            if(forumPostSummary.ParentForumId > 0)
                breadCrumbs.Add(new BreadcrumbStructure
                {
                    ForumId = forumPostSummary.ParentForumId.ToString(),
                    IsCategory = false,
                    Name = forumPostSummary.ParentForumName.ToBytes()
                });

            breadCrumbs.Add(new BreadcrumbStructure
            {
                ForumId = forumId.ToString(),
                IsCategory = false,
                Name = forumPostSummary.ForumName.ToBytes()
            });

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var profilePath = string.Format("{0}://{1}{2}", Context.Request.Url.Scheme, Context.Request.Url.Host, VirtualPathUtility.ToAbsolute("~/profilepic.ashx"));

            var result = new PostListStructure
                             { 
                                 PostCount = forumPostSummary.ReplyCount + 1,
                                 CanReply = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fp.CanReply),
                                 CanSubscribe = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fp.CanSubscribe),
                                 ForumId = forumId,
                                 ForumName = forumPostSummary.ForumName.ToBytes(),
                                 IsLocked = forumPostSummary.IsLocked,
                                 IsSubscribed = forumPostSummary.SubscriptionType > 0,
                                 Subject = forumPostSummary.Subject.ToBytes(),
                                 TopicId = topicId,
                                 Posts = forumPosts.Select(p => new PostStructure
                                    {
                                          PostID = p.ContentId.ToString(),
                                          AuthorAvatarUrl = string.Format("{0}?userId={1}&w=64&h=64", profilePath, p.AuthorId),
                                          AuthorName = GetAuthorName(mainSettings, p).ToBytes(),
                                          AuthorId = p.AuthorId.ToString(),
                                          Body =  HtmlToTapatalk(p.Body, returnHtml).ToBytes(),
                                          CanEdit = false, // TODO: Fix this
                                          IsOnline = p.IsUserOnline,
                                          PostDate = p.DateCreated,
                                          Subject = p.Subject.ToBytes()
                                    }).ToArray(),
                                 Breadcrumbs = breadCrumbs.ToArray()
                              
                             };

            return result;
        }

        [XmlRpcMethod("get_thread_by_unread")]
        public PostListPlusPositionStructure GetThreadByUnread(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature");

            var topicId = Convert.ToInt32(parameters[0]);
            var postsPerRequest = parameters.Length >= 2 ? Convert.ToInt32(parameters[1]) : 20;
            var returnHtml = parameters.Length >= 3 && Convert.ToBoolean(parameters[2]);

            return GetThreadByUnread(topicId, postsPerRequest, returnHtml);
        }

        private PostListPlusPositionStructure GetThreadByUnread(int topicId, int postsPerRequest, bool returnHtml)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            var postIndex = new AFTForumController().GetForumPostIndexUnread(topicId, aftContext.UserId);

            var pageIndex = (postIndex - 1)/postsPerRequest;
            var startIndex = pageIndex * postsPerRequest;
            var endIndex = startIndex + postsPerRequest - 1;

            var result = GetThread(topicId, startIndex, endIndex, returnHtml);

            // Post index is zero base, so we need to add 1 before sending it to Tapatalk

            return new PostListPlusPositionStructure(result, postIndex + 1); //result;
        }

        [XmlRpcMethod("get_thread_by_post")]
        public PostListStructure GetThreadByPost(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature");

            var postId = Convert.ToInt32(parameters[0]);
            var postsPerRequest = parameters.Length >= 2 ? Convert.ToInt32(parameters[1]) : 20;
            var returnHtml = parameters.Length >= 3 && Convert.ToBoolean(parameters[2]);

            return GetThreadByPost(postId, postsPerRequest, returnHtml);
        }

        private PostListStructure GetThreadByPost(int postId, int postsPerRequest, bool returnHtml)
        {
            // PostId = ContentId for our purposes

            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            var postIndexResult = new AFTForumController().GetForumPostIndex(postId);
                
            if(postIndexResult == null)
                throw new XmlRpcFaultException(100, "Post Not Found");

            var pageIndex = (postIndexResult.RowIndex - 1) / postsPerRequest;
            var startIndex = pageIndex * postsPerRequest;
            var endIndex = startIndex + postsPerRequest - 1;

            var result = GetThread(postIndexResult.TopicId, startIndex, endIndex, returnHtml);

            // Post index is zero base, so we need to add 1 before sending it to Tapatalk

            return new PostListPlusPositionStructure(result, postIndexResult.RowIndex + 1); //result;
        }

        [XmlRpcMethod("get_quote_post")]
        public XmlRpcStruct GetQuote(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature");

            var postIds = Convert.ToString(parameters[0]);

            return GetQuote(postIds);
        }

        private XmlRpcStruct GetQuote(string postIds)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;

            var fc = new AFTForumController();

            // Load our forum settings
            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            // Get our quote template info
            var postedByTemplate = Utilities.GetSharedResource("[RESX:PostedBy]") + " {0} {1} {2}";
            var sharedOnText = Utilities.GetSharedResource("On.Text");

            var contentIds = postIds.Split('-').Select(int.Parse).ToList();

            if(contentIds.Count > 25) // Let's be reasonable
                throw new XmlRpcFaultException(100, "Bad Request");


            var postContent = new StringBuilder();

            foreach (var contentId in contentIds)
            {
                // Retrieve the forum post
                var forumPost = fc.GetForumPost(portalId, forumModuleId, contentId);

                if (forumPost == null)
                    throw new XmlRpcFaultException(100, "Bad Request");

                // Verify read permissions - Need to do this for every content id as we can not assume they are all from the same forum (even though they probably should be)
                var fp = fc.GetForumPermissions(forumPost.ForumId);

                if (!ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fp.CanRead))
                    continue;


                // Build our sanitized quote
                var postedBy = string.Format(postedByTemplate, GetAuthorName(mainSettings, forumPost), sharedOnText,
                                             GetServerDateTime(mainSettings, forumPost.DateCreated));

                postContent.Append(HtmlToTapatalkQuote(postedBy, forumPost.Body));
                postContent.Append("\r\n");
                // add the result

            }

            return new XmlRpcStruct
            {
                {"post_id", postIds},
                {"post_title", string.Empty.ToBytes()},
                {"post_content", postContent.ToString().ToBytes()}
            };
        }

        [XmlRpcMethod("reply_post")]
        public XmlRpcStruct Reply(params object[] parameters)
        {
            if (parameters.Length < 4)
                throw new XmlRpcFaultException(100, "Invalid Method Signature");

            var forumId = Convert.ToInt32(parameters[0]);
            var topicId = Convert.ToInt32(parameters[1]);
            var subject = Encoding.UTF8.GetString((byte[]) parameters[2]);
            var body = Encoding.UTF8.GetString((byte[])parameters[3]);

            var attachmentIdObjArray = parameters.Length >= 5 ? (object[]) parameters[4] : null;
            var groupId = parameters.Length >= 6 ? Convert.ToString(parameters[5]) : null;
            var returnHtml = parameters.Length >= 7 && Convert.ToBoolean(parameters[6]);

            var attachmentIds = (attachmentIdObjArray != null)
                                    ? attachmentIdObjArray.Select(Convert.ToString)
                                    : new string[] {}; 

            return Reply(forumId, topicId, subject, body, attachmentIds, groupId, returnHtml);
        }

        private XmlRpcStruct Reply(int forumId, int topicId, string subject, string body, IEnumerable<string> attachmentIds, string groupID, bool returnHtml)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;

            var fc = new AFTForumController();

            var forumInfo = fc.GetForum(portalId, forumModuleId, forumId);

            // Verify Post Permissions
            if (!ActiveForums.Permissions.HasPerm(forumInfo.Security.Reply, aftContext.ForumUser.UserRoles))
            {
                return new XmlRpcStruct
                                {
                                    {"result", "false"}, //"true" for success
                                    {"result_text", "Not Authorized to Reply".ToBytes()}, 
                                };
            }

            // Build User Permissions
            var canModApprove = ActiveForums.Permissions.HasPerm(forumInfo.Security.ModApprove, aftContext.ForumUser.UserRoles);
            var canTrust = ActiveForums.Permissions.HasPerm(forumInfo.Security.Trust, aftContext.ForumUser.UserRoles);
            var canDelete = ActiveForums.Permissions.HasPerm(forumInfo.Security.Delete, aftContext.ForumUser.UserRoles);
            var canModDelete = ActiveForums.Permissions.HasPerm(forumInfo.Security.ModDelete, aftContext.ForumUser.UserRoles);
            var canEdit = ActiveForums.Permissions.HasPerm(forumInfo.Security.Edit, aftContext.ForumUser.UserRoles);
            var canModEdit = ActiveForums.Permissions.HasPerm(forumInfo.Security.ModEdit, aftContext.ForumUser.UserRoles);

            var userProfile = aftContext.UserId > 0 ? aftContext.ForumUser.Profile : new UserProfileInfo { TrustLevel = -1 };
            var userIsTrusted = Utilities.IsTrusted((int)forumInfo.DefaultTrustValue, userProfile.TrustLevel, canTrust, forumInfo.AutoTrustLevel, userProfile.PostCount);

            // Determine if the post should be approved
            var isApproved = !forumInfo.IsModerated || userIsTrusted || canModApprove;

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var dnnUser = Entities.Users.UserController.GetUserById(portalId, aftContext.UserId);

            var authorName = GetAuthorName(mainSettings, dnnUser);

            var themePath = string.Format("{0}://{1}{2}", Context.Request.Url.Scheme, Context.Request.Url.Host, VirtualPathUtility.ToAbsolute("~/DesktopModules/activeforums/themes/" + mainSettings.Theme + "/"));

            subject = Utilities.CleanString(portalId, subject, false, EditorTypes.TEXTBOX, forumInfo.UseFilter, false, forumModuleId, themePath, false);
            body = Utilities.CleanString(portalId, TapatalkToHtml(body), forumInfo.AllowHTML, EditorTypes.HTMLEDITORPROVIDER, forumInfo.UseFilter, false, forumModuleId, themePath, forumInfo.AllowEmoticons);

            var dt = DateTime.Now;

            var ri = new ReplyInfo();
            ri.Content.DateCreated = dt;
            ri.Content.DateUpdated = dt;
            ri.Content.AuthorId = aftContext.UserId;
            ri.Content.AuthorName = authorName;
            ri.Content.IPAddress = Context.Request.UserHostAddress;
            ri.Content.Subject = subject;
            ri.Content.Summary = string.Empty;
            ri.Content.Body = body;
            ri.TopicId = topicId;
            ri.IsApproved = isApproved;
            ri.IsDeleted = false;
            ri.StatusId = -1;

            // Save the topic
            var rc = new ReplyController();
            var replyId = rc.Reply_Save(portalId, ri);
            ri = rc.Reply_Get(portalId, forumModuleId, topicId, replyId);

            if (ri == null)
            {
                return new XmlRpcStruct
                                {
                                    {"result", "false"}, //"true" for success
                                    {"result_text", "Error Creating Post".ToBytes()}, 
                                };
            }

            try
            {
                // Clear the cache
                var cachekey = string.Format("AF-FV-{0}-{1}", portalId, forumModuleId);
                DataCache.CacheClearPrefix(cachekey);

                // Subscribe the user if they have auto-subscribe set.
                if (userProfile.PrefSubscriptionType != SubscriptionTypes.Disabled && !(Subscriptions.IsSubscribed(portalId, forumModuleId, forumId, topicId, SubscriptionTypes.Instant, aftContext.UserId)))
                {
                    new SubscriptionController().Subscription_Update(portalId, forumModuleId, forumId, topicId, (int)userProfile.PrefSubscriptionType, aftContext.UserId, aftContext.ForumUser.UserRoles);
                }

                if (isApproved)
                {
                    // Send User Notifications
                    Subscriptions.SendSubscriptions(portalId, forumModuleId, aftContext.ModuleSettings.ForumTabId, forumInfo, topicId, ri.ReplyId, ri.Content.AuthorId);

                    // Add Journal entry
                    var forumTabId = aftContext.ModuleSettings.ForumTabId;
                    var ti = new TopicsController().Topics_Get(portalId, forumModuleId, topicId, forumId, -1, false);
                    var fullURL = new ControlUtils().BuildUrl(forumTabId, forumModuleId, forumInfo.ForumGroup.PrefixURL, forumInfo.PrefixURL, forumInfo.ForumGroupId, forumInfo.ForumID, topicId, ti.TopicUrl, -1, -1, string.Empty, 1, forumInfo.SocialGroupId);
                    new Social().AddReplyToJournal(portalId, forumModuleId, forumId, topicId, ri.ReplyId, ri.Author.AuthorId, fullURL, ri.Content.Subject, string.Empty, ri.Content.Body, forumInfo.ActiveSocialSecurityOption, forumInfo.Security.Read, forumInfo.SocialGroupId);
                }
                else
                {
                    // Send Mod Notifications
                    var mods = Utilities.GetListOfModerators(portalId, forumId);
                    var notificationType = NotificationsController.Instance.GetNotificationType("AF-ForumModeration");
                    var notifySubject = Utilities.GetSharedResource("NotificationSubjectReply");
                    notifySubject = notifySubject.Replace("[DisplayName]", dnnUser.DisplayName);
                    notifySubject = notifySubject.Replace("[TopicSubject]", ri.Content.Subject);
                    var notifyBody = Utilities.GetSharedResource("NotificationBodyReply");
                    notifyBody = notifyBody.Replace("[Post]", ri.Content.Body);
                    var notificationKey = string.Format("{0}:{1}:{2}:{3}:{4}", aftContext.ModuleSettings.ForumTabId, forumModuleId, forumId, topicId, replyId);

                    var notification = new Notification
                    {
                        NotificationTypeID = notificationType.NotificationTypeId,
                        Subject = notifySubject,
                        Body = notifyBody,
                        IncludeDismissAction = false,
                        SenderUserID = dnnUser.UserID,
                        Context = notificationKey
                    };

                    NotificationsController.Instance.SendNotification(notification, portalId, null, mods);
                }

            }
            catch (Exception ex)
            {
                Services.Exceptions.Exceptions.LogException(ex);
            }


            var result = new XmlRpcStruct
            {
                {"result", true}, //"true" for success
                //{"result_text", "OK".ToBytes()}, 
                {"post_id", ri.ContentId.ToString()},
                {"post_content", HtmlToTapatalk(ri.Content.Body, false).ToBytes() },
                {"can_edit", canEdit || canModEdit },
                {"can_delete", canDelete || canModDelete },
                {"post_time", dt}/*,
                {"attachments", new {}}*/
            };

            if(!isApproved)
                result.Add("state", 1);

            return result;


        }


        #endregion

        #region User API

        [XmlRpcMethod("login")]
        public XmlRpcStruct Login(params object[] parameters)
        {
            if (parameters.Length < 2)
                throw new XmlRpcFaultException(100, "Invalid Method Signature"); 

            var login = Encoding.UTF8.GetString((byte[]) parameters[0]);
            var password = Encoding.UTF8.GetString((byte[])parameters[1]);

            return Login(login, password);
        }

        public XmlRpcStruct Login(string login, string password)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if(aftContext == null || aftContext.Portal == null)
                throw new XmlRpcFaultException(100, "Invalid Context"); 

            var loginStatus = UserLoginStatus.LOGIN_FAILURE;

            Entities.Users.UserController.ValidateUser(aftContext.Portal.PortalID, login, password, string.Empty, aftContext.Portal.PortalName, Context.Request.UserHostAddress, ref loginStatus);

            var result = false;
            var resultText = string.Empty;

            switch(loginStatus)
            {
                case UserLoginStatus.LOGIN_SUCCESS:
                case UserLoginStatus.LOGIN_SUPERUSER:
                    result = true;
                    break;

                case UserLoginStatus.LOGIN_FAILURE:
                    resultText = "Invalid Login/Password Combination";
                    break;

                case UserLoginStatus.LOGIN_USERNOTAPPROVED:
                    resultText = "User Not Approved";
                    break;

                case UserLoginStatus.LOGIN_USERLOCKEDOUT:
                    resultText = "User Temporarily Locked Out";
                    break;

                default:
                    resultText = "Unknown Login Error";
                    break;
            }

            User forumUser = null;

            if(result)
            { 
                // Get the User
                var userInfo = Entities.Users.UserController.GetUserByName(aftContext.Module.PortalID, login);

                if(userInfo == null)
                {
                    result = false;
                    resultText = "Unknown Login Error";
                }
                else
                {
                    // Set Login Cookie
                    var expiration = DateTime.Now.Add(FormsAuthentication.Timeout); 

                    var ticket = new FormsAuthenticationTicket(1, login, DateTime.Now, expiration, false, userInfo.UserID.ToString());
                    var authCookie = new HttpCookie(aftContext.AuthCookieName, FormsAuthentication.Encrypt(ticket))
                    {
                        Domain = FormsAuthentication.CookieDomain,
                        Path = FormsAuthentication.FormsCookiePath,
                    };


                    Context.Response.SetCookie(authCookie);

                    forumUser = new ActiveForums.UserController().GetUser(aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, userInfo.UserID);
                }
            }

            Context.Response.AddHeader("Mobiquo_is_login", result ? "true" : "false"); 

            

            var rpcstruct = new XmlRpcStruct
                                {
                                    {"result", result },
                                    {"result_text", resultText.ToBytes()}, 
                                    {"can_upload_avatar", false}
                                };
          
            if(result && forumUser != null)
            {
                rpcstruct.Add("user_id", forumUser.UserId.ToString());
                rpcstruct.Add("username", forumUser.UserName.ToBytes());
                rpcstruct.Add("email", forumUser.Email.ToBytes());
                rpcstruct.Add("usergroup_id", new string[]{});
                rpcstruct.Add("post_count", forumUser.PostCount);
                rpcstruct.Add("icon_url", GetAvatarUrl(forumUser.UserId));
                
            }


            return rpcstruct;

        }

        [XmlRpcMethod("logout_user")]
        public void Logout()
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Portal == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", "false");

            var authCookie = new HttpCookie(aftContext.AuthCookieName, string.Empty)
                            {
                                Expires = DateTime.Now.AddDays(-1)
                            };


            Context.Response.SetCookie(authCookie);
        }

        [XmlRpcMethod("get_user_info")]
        public XmlRpcStruct GetUser(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature"); 

            var username = Encoding.UTF8.GetString((byte[])parameters[0]);
            var userIdStr = parameters.Length >= 2 ? Convert.ToString(parameters[1]) : null;

            var userId = Utilities.SafeConvertInt(userIdStr);

            return GetUser(username, userId);
        }

        private XmlRpcStruct GetUser(string username, int userId)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            // Not fully implemented...  Disable for now.

            // for our purposes, we ignore the username and require userId
            // If we don't have a userid, pass back a anonymous user object
            if(true || userId <= 0)
            {
                return new XmlRpcStruct
                           {
                               { "user_id", userId.ToString() },
                               { "user_name", username.ToBytes() },
                               { "post_count", 1 }
                           };
            }

            var portalId = aftContext.Module.PortalID;
            var currentUserId = aftContext.UserId;

            var fc = new AFTForumController();

            var user = fc.GetUser(portalId, userId, currentUserId);

            const bool allowPM = false; //TODO : Tie in with PM's
            const bool acceptFollow = false;

            if(user == null)
            {
                return new XmlRpcStruct
                           {
                               { "user_id", userId.ToString() },
                               { "user_name", username.ToBytes() },
                               { "post_count", 1 }
                           }; 
            }

            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            return new XmlRpcStruct
                           {
                               { "user_id", userId.ToString() },
                               { "user_name", GetUserName(mainSettings, user).ToBytes() },
                               { "post_count", user.PostCount },
                               { "reg_time", user.DateCreated },
                               { "last_activity_date", user.DateLastActivity },
                               { "is_online", user.IsUserOnline },
                               { "accept_pm", allowPM },
                               { "i_follow_u", user.Following },
                               { "u_follow_me", user.IsFollower },
                               { "accept_follow", acceptFollow },
                               { "following_count", user.FollowingCount },
                               { "follower", user.FollowerCount },
                               { "display_text", user.UserCaption.ToBytes() },
                               { "icon_url", GetAvatarUrl(userId) }, 
                           };
        }

        #endregion

        #region Subscribe API (Feature Complete)

        [XmlRpcMethod("subscribe_forum")]
        public XmlRpcStruct SubscribeForum(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature"); 

            var forumId = Convert.ToInt32(parameters[0]);

            return Subscribe(forumId, null, false);
        }

        [XmlRpcMethod("subscribe_topic")]
        public XmlRpcStruct SubscribeTopic(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature"); 

            var topicId = Convert.ToInt32(parameters[0]);

            return Subscribe(null, topicId, false);
        }

        [XmlRpcMethod("unsubscribe_forum")]
        public XmlRpcStruct UnsubscribeForum(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature"); 

            var forumId = Convert.ToInt32(parameters[0]);

            return Subscribe(forumId, null, true);
        }

        [XmlRpcMethod("unsubscribe_topic")]
        public XmlRpcStruct UnsubscribeTopic(params object[] parameters)
        {
            if (parameters.Length < 1)
                throw new XmlRpcFaultException(100, "Invalid Method Signature"); 

            var topicId = Convert.ToInt32(parameters[0]);

            return Subscribe(null, topicId, true);
        }

        private XmlRpcStruct Subscribe(int? forumId, int? topicId, bool unsubscribe)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            if (!forumId.HasValue && !topicId.HasValue)
                return new XmlRpcStruct{{"result", "0"},{"result_text", "Bad Request".ToBytes()}};


            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;

            // Look up the forum Id if needed
            if(!forumId.HasValue)
            {
                var ti = new TopicsController().Topics_Get(portalId, forumModuleId, topicId.Value);
                if(ti == null)
                    return new XmlRpcStruct { { "result", false }, { "result_text", "Topic Not Found".ToBytes() } };

                var post = new AFTForumController().GetForumPost(portalId, forumModuleId, ti.ContentId);
                if(post == null)
                    return new XmlRpcStruct { { "result", false }, { "result_text", "Topic Post Not Found".ToBytes() } };

                forumId = post.ForumId;
            }

            var subscriptionType = unsubscribe ? SubscriptionTypes.Disabled : SubscriptionTypes.Instant;

            var sub = new SubscriptionController().Subscription_Update(portalId, forumModuleId, forumId.Value, topicId.HasValue ? topicId.Value : -1, (int)subscriptionType, aftContext.UserId, aftContext.ForumUser.UserRoles);

            var result = (sub >= 0) ? "1" : "0";

            return new XmlRpcStruct
            {
                {"result", result},
                {"result_text", string.Empty.ToBytes()}, 
            };
        }
    
        [XmlRpcMethod("get_subscribed_forum")]
        public XmlRpcStruct GetSubscribedForums()
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var subscribedForums = fc.GetSubscribedForums(portalId, forumModuleId, userId, forumIds).ToList();

            return new XmlRpcStruct
                       {
                           {"total_forums_num", subscribedForums.Count},
                           {"forums", subscribedForums.Select(f => new ListForumStructure
                                {
                                    ForumId = f.ForumId.ToString(),
                                    ForumName = f.ForumName.ToBytes(),
                                    IsProtected = false,
                                    HasNewPosts =  f.LastPostDate > f.LastAccessDate
                                }).ToArray()}
                       };
        }

        [XmlRpcMethod("get_subscribed_topic")]
        public XmlRpcStruct GetSubscribedTopics(params object[] parameters)
        {
            var startIndex = parameters.Any() ? Convert.ToInt32(parameters[0]) : 0;
            var endIndex = parameters.Count() > 1 ? Convert.ToInt32(parameters[1]) : startIndex + 49;

            if (endIndex < startIndex)
                return null;

            if (endIndex > startIndex + 49)
                endIndex = startIndex + 49;

            return GetSubscribedTopics(startIndex, endIndex);
        }

        private XmlRpcStruct GetSubscribedTopics(int startIndex, int endIndex)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var profilePath = string.Format("{0}://{1}{2}", Context.Request.Url.Scheme, Context.Request.Url.Host, VirtualPathUtility.ToAbsolute("~/profilepic.ashx"));

            var maxRows = endIndex + 1 - startIndex;

            var subscribedTopics = fc.GetSubscribedTopics(portalId, forumModuleId, userId, forumIds, startIndex, maxRows).ToList();



            return new XmlRpcStruct
                       {
                           {"total_topic_num", subscribedTopics.Count > 0 ? subscribedTopics[0].TopicCount : 0},
                           {"topics", subscribedTopics.Select(t => new ExtendedTopicStructure{ 
                                                   TopicId = t.TopicId.ToString(),
                                                   AuthorAvatarUrl = string.Format("{0}?userId={1}&w=64&h=64", profilePath, t.LastReplyAuthorId),
                                                   AuthorName = GetLastReplyAuthorName(mainSettings, t).ToBytes(),
                                                   AuthorId = t.LastReplyAuthorId.ToString(),
                                                   ForumId = t.ForumId.ToString(),
                                                   ForumName = t.ForumName.ToBytes(),
                                                   HasNewPosts =  (t.LastReplyId < 0 && t.TopicId > t.UserLastTopicRead) || t.LastReplyId > t.UserLastReplyRead,
                                                   IsLocked = t.IsLocked,
                                                   ReplyCount = t.ReplyCount,
                                                   Summary = GetSummary(null, t.LastReplyBody).ToBytes(),
                                                   ViewCount = t.ViewCount,
                                                   DateCreated = t.LastReplyDate,
                                                   Title = HttpUtility.HtmlDecode(t.Subject + string.Empty).ToBytes()
                                               }).ToArray()}
                       };   
        }

        #endregion

        #region Search API
        
        [XmlRpcMethod("search_topic")]
        public XmlRpcStruct SearchTopics(params object[] parameters)
        {
            if(parameters.Length == 0)
                throw new XmlRpcFaultException(100, "Invalid Method Signature");

            var searchString = Encoding.UTF8.GetString((byte[])parameters[0]);
            var startIndex = parameters.Length >= 3 ? Convert.ToInt32(parameters[1]) : 0;
            var endIndex = parameters.Length >= 3 ? Convert.ToInt32(parameters[2]) : 20;
            var searchId = parameters.Length >= 4 ? Convert.ToString(parameters[3]) : null;

            return SearchTopics(searchString, startIndex, endIndex, searchId);
        }

        private XmlRpcStruct SearchTopics(string searchString, int startIndex, int endIndex, string searchId)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Verify Search Permissions
            var searchPermissions = aftContext.ModuleSettings.SearchPermission;
            if(searchPermissions == ActiveForumsTapatalkModuleSettings.SearchPermissions.Disabled || (userId <= 0 && searchPermissions == ActiveForumsTapatalkModuleSettings.SearchPermissions.RegisteredUsers ))
                throw new XmlRpcFaultException(102, "Insufficent Search Permissions");

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var maxRows = endIndex + 1 - startIndex;

            var searchResults = fc.SearchTopics(portalId, forumModuleId, userId, forumIds, searchString, startIndex, maxRows, searchId, mainSettings);

            if (searchResults == null)
                throw new XmlRpcFaultException(101, "Search Error");

            return new XmlRpcStruct
            {
                {"total_topic_num", searchResults.TotalTopics },
                {"search_id", searchResults.SearchId.ToString() },
                {"topics", searchResults.Topics.Select(t => new ExtendedTopicStructure { 
                                        TopicId = t.TopicId.ToString(),
                                        AuthorAvatarUrl = GetAvatarUrl(t.LastReplyAuthorId),
                                        AuthorId = t.LastReplyAuthorId.ToString(),
                                        AuthorName = GetLastReplyAuthorName(mainSettings, t).ToBytes(),
                                        ForumId = t.ForumId.ToString(),
                                        ForumName = t.ForumName.ToBytes(),
                                        HasNewPosts =  (t.LastReplyId < 0 && t.TopicId > t.UserLastTopicRead) || t.LastReplyId > t.UserLastReplyRead,
                                        IsLocked = t.IsLocked,
                                        IsSubscribed = t.SubscriptionType > 0,
                                        CanSubscribe = ActiveForums.Permissions.HasPerm(aftContext.ForumUser.UserRoles, fc.GetForumPermissions(t.ForumId).CanSubscribe), // GetforumPermissions uses cache so it shouldn't be a performance issue
                                        ReplyCount = t.ReplyCount,
                                        Summary = GetSummary(null, t.LastReplyBody).ToBytes(),
                                        ViewCount = t.ViewCount,
                                        DateCreated = t.LastReplyDate,
                                        Title = HttpUtility.HtmlDecode(t.Subject + string.Empty).ToBytes()
                                    }).ToArray()}
            };  
        }

        [XmlRpcMethod("search_post")]
        public XmlRpcStruct SearchPosts(params object[] parameters)
        {
            if (parameters.Length == 0)
                throw new XmlRpcFaultException(100, "Invalid Method Signature");

            var searchString = Encoding.UTF8.GetString((byte[])parameters[0]);
            var startIndex = parameters.Length >= 3 ? Convert.ToInt32(parameters[1]) : 0;
            var endIndex = parameters.Length >= 3 ? Convert.ToInt32(parameters[2]) : 20;
            var searchId = parameters.Length >= 4 ? Convert.ToString(parameters[3]) : null;

            return SearchPosts(searchString, startIndex, endIndex, searchId);
        }

        private XmlRpcStruct SearchPosts(string searchString, int startIndex, int endIndex, string searchId)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;
            var userId = aftContext.UserId;

            // Verify Search Permissions
            var searchPermissions = aftContext.ModuleSettings.SearchPermission;
            if (searchPermissions == ActiveForumsTapatalkModuleSettings.SearchPermissions.Disabled || (userId <= 0 && searchPermissions == ActiveForumsTapatalkModuleSettings.SearchPermissions.RegisteredUsers))
                throw new XmlRpcFaultException(102, "Insufficent Search Permissions");

            // Build a list of forums the user has access to
            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, portalId, forumModuleId, "CanRead");

            var mainSettings = new SettingsInfo { MainSettings = new Entities.Modules.ModuleController().GetModuleSettings(forumModuleId) };

            var maxRows = endIndex + 1 - startIndex;

            var searchResults = fc.SearchPosts(portalId, forumModuleId, userId, forumIds, searchString, startIndex, maxRows, searchId, mainSettings);

            if (searchResults == null)
                throw new XmlRpcFaultException(101, "Search Error");

            return new XmlRpcStruct
            {
                {"total_post_num", searchResults.TotalPosts },
                {"search_id", searchResults.SearchId.ToString() },
                {"posts", searchResults.Topics.Select(t => new ExtendedPostStructure { 
                                        TopicId = t.TopicId.ToString(),
                                        AuthorAvatarUrl = GetAvatarUrl(t.AuthorId),
                                        AuthorId = t.AuthorId.ToString(),
                                        AuthorName = GetAuthorName(mainSettings, t).ToBytes(),
                                        ForumId = t.ForumId.ToString(),
                                        ForumName = t.ForumName.ToBytes(),
                                        PostID = t.ContentId.ToString(),
                                        Summary = GetSummary(null, t.Body).ToBytes(),
                                        PostDate = t.DateCreated,
                                        TopicTitle = HttpUtility.HtmlDecode(t.Subject + string.Empty).ToBytes(),
                                        PostTitle = HttpUtility.HtmlDecode(t.PostSubject + string.Empty).ToBytes()
                                    }).ToArray()}
            };
        }

        #endregion

        #region Private Helper Methods

        private static string GetSummary(string summary, string body)
        {
            var result = !string.IsNullOrWhiteSpace(summary) ? summary : body;

            result = result + string.Empty;

            result = HttpUtility.HtmlDecode(Utilities.StripHTMLTag(result));

            result = result.Length > 200 ? result.Substring(0, 200) : result;

            return result.Trim();
        }

        private static string TapatalkToHtml(string input)
        {
            input = input.Replace("<", "&lt;");
            input = input.Replace(">", "&gt;");

            input = input.Replace("\r\n", "\n");
            input = input.Trim(new [] {' ', '\n', '\r', '\t'}).Replace("\n", "<br />");

            input = Regex.Replace(input, @"\[quote\=\'([^\]]+?)\'\]", "<blockquote class='afQuote'><span class='afQuoteTitle'>$1</span><br />", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[quote\=\""([^\]]+?)\""\]", "<blockquote class='afQuote'><span class='afQuoteTitle'>$1</span><br />", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[quote\=([^\]]+?)\]",  "<blockquote class='afQuote'><span class='afQuoteTitle'>$1</span><br />", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[quote\]", "<blockquote class='afQuote'>", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[\/quote\]", "</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[img\](.+?)\[\/img\]", "<img src='$1' />", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[url=\'(.+?)\'\](.+?)\[\/url\]", "<a href='$1'>$2</a>", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[url=\""(.+?)\""\](.+?)\[\/url\]", "<a href='$1'>$2</a>", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[url=(.+?)\](.+?)\[\/url\]", "<a href='$1'>$2</a>", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[url\](.+?)\[\/url\]", "<a href='$1'>$1</a>", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[(\/)?b\]", "<$1strong>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[(\/)?i\]", "<$1i>", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            return input;
        }

        private static string HtmlToTapatalk(string input, bool returnHtml)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            input = Regex.Replace(input, @"\s+", " ", RegexOptions.Multiline);

            input = EncodeUnmatchedBrackets(input);

            var htmlBlock = new HtmlDocument();
            htmlBlock.LoadHtml(input);

            var tapatalkMarkup = new StringBuilder();

            ProcessNode(tapatalkMarkup, htmlBlock.DocumentNode, ProcessModes.Normal, returnHtml);

            return tapatalkMarkup.ToString().Trim(new[] { ' ', '\n', '\r', '\t' });
        }

        private static string HtmlToTapatalkQuote(string postedBy, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            input = Regex.Replace(input, @"\s+", " ", RegexOptions.Multiline);

            input = EncodeUnmatchedBrackets(input);

            var htmlBlock = new HtmlDocument();
            htmlBlock.LoadHtml(input);

            var tapatalkMarkup = new StringBuilder();

            ProcessNode(tapatalkMarkup, htmlBlock.DocumentNode, ProcessModes.Quote, false);

            return string.Format("[quote={0}]\r\n{1}\r\n[/quote]\r\n", postedBy, tapatalkMarkup.ToString().Trim(new[] { ' ', '\n', '\r', '\t' }));
        }

        private static void ProcessNodes(StringBuilder output, IEnumerable<HtmlNode> nodes, ProcessModes mode, bool returnHtml)
        {
            foreach (var node in nodes)
                ProcessNode(output, node, mode, returnHtml);
        }

        private static void ProcessNode(StringBuilder output, HtmlNode node, ProcessModes mode, bool returnHtml)
        {
            var lineBreak = returnHtml ? "<br />" : "\r\n"; // (mode == ProcessModes.Quote) ? "\n" : "<br /> ";

            if (node == null || output == null || (mode == ProcessModes.TextOnly && node.Name != "#text"))
                return;

            switch (node.Name)
            {
                // No action needed for these node types
                case "#text":
                    var text = HttpUtility.HtmlDecode(node.InnerHtml);
                    //if (mode != ProcessModes.Quote)
                    //    text = HttpContext.Current.Server.HtmlEncode(text);
                    text = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                    output.Append(text);
                    return;

                case "tr":
                    ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                    output.Append(lineBreak);
                    return;

                case "script":
                    return;

                case "ol":
                case "ul":

                    if(mode != ProcessModes.Normal)
                        return;

                    output.Append(lineBreak);

                    var listItemNodes = node.SelectNodes("//li");

                    for(var i = 0;  i < listItemNodes.Count; i++)
                    {
                        var listItemNode = listItemNodes[i];
                        output.AppendFormat("{0} ", node.Name == "ol" ? (i + 1).ToString() : "*");
                        ProcessNodes(output, listItemNode.ChildNodes, mode, returnHtml);
                        output.Append(lineBreak);
                    }
                    
                    return;

                case "li":

                    if(mode == ProcessModes.Quote)
                        return; 

                    output.Append("* ");
                    ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                    output.Append(lineBreak);
                    return;

                case "p":
                    ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                    output.Append(lineBreak);
                    return;

                case "b":
                case "strong":

                    if(mode != ProcessModes.Quote)
                    {
                        output.Append("<b>");
                        ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                        output.Append("</b>");
                    }
                    else
                    {
                        output.Append("[b]");
                        ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                        output.Append("[/b]");
                    }

                    return;

                case "i":
                    if(mode != ProcessModes.Quote)
                    {
                        output.Append("<i>");
                        ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                        output.Append("</i>");
                    }
                    else
                    {
                        output.Append("[i]");
                        ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                        output.Append("[/i]");
                    }

                    return;

                case "blockquote":

                    if(mode != ProcessModes.Normal)
                        return;

                    output.Append("[quote]");
                    ProcessNodes(output, node.ChildNodes, mode, returnHtml);
                    output.Append("[/quote]" + lineBreak);
                    return;

                case "br":
                    output.Append(lineBreak);
                    return;


                case "img":

                    var src = node.Attributes["src"];
                    if (src == null || string.IsNullOrWhiteSpace(src.Value))
                        return;

                    var isEmoticon = src.Value.IndexOf("emoticon", 0, StringComparison.InvariantCultureIgnoreCase) >= 0;

                    var url = src.Value.Trim();
                    var request = HttpContext.Current.Request;

                    // Make a fully qualifed URL
                    if (!url.ToLower().StartsWith("http"))
                    {
                        var rootDirectory = url.StartsWith("/") ? string.Empty : "/";
                        url = string.Format("{0}://{1}{2}{3}", request.Url.Scheme, request.Url.Host, rootDirectory,  url);
                    }

                    if(mode == ProcessModes.Quote && isEmoticon)
                        return;

                    output.AppendFormat(isEmoticon ? "<img src='{0}' />" : "[img]{0}[/img]", url);

                    return;

                case "a":

                    var href = node.Attributes["href"];
                    if (href == null || string.IsNullOrWhiteSpace(href.Value))
                        return;

                    output.AppendFormat("[url={0}]", href.Value);
                    ProcessNodes(output, node.ChildNodes, ProcessModes.TextOnly, returnHtml); 
                    output.Append("[/url]");

                    return;


            }

            ProcessNodes(output, node.ChildNodes, mode, returnHtml);
        }

        private static string GetAuthorName(SettingsInfo settings, Entities.Users.UserInfo user)
        {
            if (user == null || user.UserID <= 0)
                return "Guest";

            switch (settings.UserNameDisplay.ToUpperInvariant())
            {
                case "USERNAME":
                    return user.Username.Trim();
                case "FULLNAME":
                    return (user.FirstName.Trim() + " " + user.LastName.Trim());
                case "FIRSTNAME":
                    return user.FirstName.Trim();
                case "LASTNAME":
                    return user.LastName.Trim();
                default:
                    return user.DisplayName.Trim();
            }

        }

        private static string GetAuthorName(SettingsInfo settings, ForumTopic topic)
        {
            if (topic == null || topic.AuthorId <= 0)
                return "Guest";

            switch (settings.UserNameDisplay.ToUpperInvariant())
            {
                case "USERNAME":
                    return topic.AuthorUserName.Trim();
                case "FULLNAME":
                    return (topic.AuthorFirstName.Trim() + " " + topic.AuthorLastName.Trim());
                case "FIRSTNAME":
                    return topic.AuthorFirstName.Trim();
                case "LASTNAME":
                    return topic.AuthorLastName.Trim();
                default:
                    return topic.AuthorDisplayName.Trim();
            }

        }

        private static string GetLastReplyAuthorName(SettingsInfo settings, ForumTopic topic)
        {
            if (topic == null || topic.AuthorId <= 0)
                return "Guest";

            switch (settings.UserNameDisplay.ToUpperInvariant())
            {
                case "USERNAME":
                    return topic.LastReplyUserName.Trim();
                case "FULLNAME":
                    return (topic.LastReplyFirstName.Trim() + " " + topic.LastReplyLastName.Trim());
                case "FIRSTNAME":
                    return topic.LastReplyFirstName.Trim();
                case "LASTNAME":
                    return topic.LastReplyLastName.Trim();
                default:
                    return topic.LastReplyDisplayName.Trim();
            }

        }

        private static string GetAuthorName(SettingsInfo settings, ForumPost post)
        {
            if (post == null || post.AuthorId <= 0)
                return "Guest";

            switch (settings.UserNameDisplay.ToUpperInvariant())
            {
                case "USERNAME":
                    return post.UserName.Trim();
                case "FULLNAME":
                    return (post.FirstName.Trim() + " " + post.LastName.Trim());
                case "FIRSTNAME":
                    return post.FirstName.Trim();
                case "LASTNAME":
                    return post.LastName.Trim();
                default:
                    return post.DisplayName.Trim();
            }

        }

        private static string GetUserName(SettingsInfo settings, Classes.UserInfo userInfo)
        {
            if (userInfo == null || userInfo.UserId <= 0)
                return "Guest";

            switch (settings.UserNameDisplay.ToUpperInvariant())
            {
                case "USERNAME":
                    return userInfo.UserName.Trim();
                case "FULLNAME":
                    return (userInfo.FirstName.Trim() + " " + userInfo.LastName.Trim());
                case "FIRSTNAME":
                    return userInfo.FirstName.Trim();
                case "LASTNAME":
                    return userInfo.LastName.Trim();
                default:
                    return userInfo.DisplayName.Trim();
            }

        }

        private static string GetServerDateTime(SettingsInfo settings, DateTime displayDate)
        {
            //Dim newDate As Date 
            string dateString;
            try
            {
                dateString = displayDate.ToString(settings.DateFormatString + " " + settings.TimeFormatString);
                return dateString;
            }
            catch (Exception ex)
            {
                dateString = displayDate.ToString();
                return dateString;
            }
        }

        private static string GetAvatarUrl(int userId)
        {
            const string urlTemplate = "{0}://{1}{2}";
            const string profilePathTemplate = "~/profilepic.ashx";

            var request = HttpContext.Current.Request;

            var profilePath = string.Format(urlTemplate, request.Url.Scheme, request.Url.Host, VirtualPathUtility.ToAbsolute(profilePathTemplate));

            return string.Format("{0}?userId={1}&w=64&h=64", profilePath, userId);
        }

        private static string EncodeUnmatchedBrackets(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var sb = new StringBuilder(input);

            var index = GetFirstUnmatchedBracket(input);

            while(index >= 0)
            {
                var bracket = sb[index];
                sb.Remove(index, 1);
                sb.Insert(index, (bracket == '<') ? "&lt;" : "&gt;");

                index = GetFirstUnmatchedBracket(sb.ToString());
            }

            return sb.ToString();
        }

        private static int GetFirstUnmatchedBracket(string text)
        {
            var m = Regex.Match(text, @"^(?>\<(?<X>)|\>(?<-X>)|(?!\<|\>).)+(?(X)(?!))");
            return m.Length < text.Length ? m.Length : -1;
        }

        #endregion

    }
} 