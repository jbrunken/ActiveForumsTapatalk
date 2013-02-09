using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using CookComputing.XmlRpc;
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
        // Forum API

        [XmlRpcMethod("get_config")]
        public XmlRpcStruct GetConfig()
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context"); 

            //if(aftContext.UserId < 0)
                Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false"); 

            var rpcstruct = new XmlRpcStruct
                                {
                                    {"version", "dev"}, 
                                    {"is_open", aftContext.ModuleSettings.IsOpen}, 
                                    {"api_level", "3"},
                                    {"guest_okay", aftContext.ModuleSettings.AllowAnonymous},
                                    {"disable_bbcode", "0"},
                                    {"reg_url", "/register"},
                                    {"charset", "UTF-8"}
                                    //{"disable_html", "1"},
                                    //{"announcement", "1"}
                                };        

            return rpcstruct;

        }


        [XmlRpcMethod("get_forum")]
        public ForumStructure[] GetForums()
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false"); 

            var fc = new AFTForumController();
            var forumIds = fc.GetForumsForUser(aftContext.ForumUser.UserRoles, aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, "CanRead");
            var forumTable = fc.GetForumView(aftContext.Module.PortalID, aftContext.ModuleSettings.ForumModuleId, aftContext.UserId, aftContext.ForumUser.IsSuperUser, forumIds);
            var forumSubscriptions =  fc.GetSubscriptionsForUser(aftContext.ModuleSettings.ForumModuleId, aftContext.UserId, null, 0).ToList();

            var result = new List<ForumStructure>();

            // Note that all the fields in the DataTable are strings if they come back from the cache, so they have to be converted appropriately.

            // Get the distict list of groups
            var groups = forumTable.AsEnumerable()
                .Select(r => new
                {
                    ID = Convert.ToInt32(r ["ForumGroupId"]), 
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

            foreach(var group in groups)
            {
                // Find any root level forums for this group
                var groupForums = visibleForums.Where(vf => vf.ParentForumId == 0 && vf.ForumGroupId == group.ID).ToList();

                if(!groupForums.Any())
                    continue;

                // Create the structure to represent the group
                var groupStructure = new ForumStructure()
                {
                    ForumId =  "G" + group.ID.ToString(), // Append G to distinguish between forums and groups with the same id.
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
                foreach(var groupForum in groupForums)
                {
                    var forumStructure = new ForumStructure
                    {
                        ForumId = groupForum.ID.ToString(),
                        Name = Utilities.StripHTMLTag(groupForum.Name).ToBytes(),
                        Description = Utilities.StripHTMLTag(groupForum.Description).ToBytes(),
                        ParentId = 'G' + group.ID.ToString(),
                        LogoUrl = null,
                        HasNewPosts = aftContext.UserId > 0 &&  groupForum.LastPostDate > groupForum.LastRead,
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
                                Description = Utilities.StripHTMLTag(subForum.Description).ToBytes(),
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


        // Topic API

        [XmlRpcMethod("get_topic")]
        public TopicListStructure  GetTopic(params object[] parameters)
        {
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
                                               ForumName = forumTopicsSummary.ForumName,
                                               TopicCount = forumTopicsSummary.TopicCount,
                                               Topics = forumTopics.Select(t => new TopicStructure{ 
                                                   TopicId = t.TopicId.ToString(),
                                                   AuthorAvatarUrl = string.Format("{0}?userId={1}&w=64&h=64", profilePath, t.AuthorId),
                                                   AuthorName = GetAuthorName(mainSettings, t).ToBytes(),
                                                   CanSubscribe = canSubscribe,
                                                   ForumId = forumId.ToString(),
                                                   HasNewPosts = t.LastReplyId > t.UserLastReplyRead,
                                                   IsLocked = t.IsLocked,
                                                   IsSubscribed = t.SubscriptionType > 0,
                                                   LastReplyDate = t.LastReplyDate,
                                                   ReplyCount = t.ReplyCount,
                                                   Summary = GetSummary(t.Summary, t.Body).ToBytes(),
                                                   ViewCount = t.ViewCount,
                                                   Title = t.Subject.ToBytes()
                                               }).ToArray()
                                           };
                                             
                             

            return forumTopicsStructure;
        }

        [XmlRpcMethod("new_topic")]
        public XmlRpcStruct NewTopic(params object[] parameters)
        {
            if(parameters.Length >= 3)
            {
                var forumId = Convert.ToInt32(parameters[0]);
                var subject = Encoding.Default.GetString((byte[])parameters[1]);
                var body = Encoding.Default.GetString((byte[])parameters[2]);

                return NewTopic(forumId, subject, body);
            }
            

            throw new XmlRpcFaultException(100, "Invalid Method Signature"); 
        }

        private XmlRpcStruct NewTopic(int forumId, string subject, string body)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);
            
            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            var portalId = aftContext.Module.PortalID;
            var forumModuleId = aftContext.ModuleSettings.ForumModuleId;

            var fc = new ForumController();

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
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex); 
            }


            var rpcstruct = new XmlRpcStruct
                                {
                                    {"result", "true"}, //"true" for success
                                    {"result_text", "Test Mode".ToBytes()}, 
                                    {"topic_id", "5000"},
                                   // {"state", 0}, // 1 if moderation required
                                };

            return rpcstruct;
        }


        // Post API

        [XmlRpcMethod("get_thread")]
        public PostListStructure GetThread(params object[] parameters)
        {
            if (parameters.Length >= 3)
                return GetThread(Convert.ToInt32(parameters[0]), Convert.ToInt32(parameters[1]), Convert.ToInt32(parameters[2]));

            throw new XmlRpcFaultException(100, "Invalid Method Signature");
        }

        private PostListStructure GetThread(int topicId, int startIndex, int endIndex)
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
                                          PostID = p.ReplyId > 0 ? p.ReplyId : p.TopicId,
                                          AuthorAvatarUrl = string.Format("{0}?userId={1}&w=64&h=64", profilePath, p.AuthorId),
                                          AuthorName = GetAuthorName(mainSettings, p).ToBytes(),
                                          Body = HtmlToTapatalk(p.Body).ToBytes(),
                                          CanEdit = false, // TODO: Fix this
                                          IsOnline = p.IsUserOnline,
                                          PostDate = p.DateCreated,
                                          Subject = p.Subject.ToBytes()
                                    }).ToArray(),
                                 Breadcrumbs = breadCrumbs.ToArray()
                              
                             };

            return result;
        }


        [XmlRpcMethod("get_quote_post")]
        public XmlRpcStruct GetQuote(string post_id)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if (aftContext == null || aftContext.Module == null)
                throw new XmlRpcFaultException(100, "Invalid Context");

            Context.Response.AddHeader("Mobiquo_is_login", aftContext.UserId > 0 ? "true" : "false");

            return new XmlRpcStruct
                                {
                                    {"post_id", post_id},
                                    {"post_title", "".ToBytes()}, 
                                    {"post_content", "[quote]Some post content that's been quoted[/quote]".ToBytes()}
                                };
        }

        // User API

        [XmlRpcMethod("login")]
        public XmlRpcStruct Login(byte[] login_name, byte[] password)
        {
            var aftContext = ActiveForumsTapatalkModuleContext.Create(Context);

            if(aftContext == null || aftContext.Portal == null)
                throw new XmlRpcFaultException(100, "Invalid Context"); 

            var loginStatus = UserLoginStatus.LOGIN_FAILURE;

            var strLogin = Encoding.Default.GetString(login_name);
            var strPassword = Encoding.Default.GetString(password);

            Entities.Users.UserController.ValidateUser(aftContext.Portal.PortalID, strLogin, strPassword, string.Empty, aftContext.Portal.PortalName, Context.Request.UserHostAddress, ref loginStatus);

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


            if(result)
            { 
                // Get the User
                var userInfo = Entities.Users.UserController.GetUserByName(aftContext.Module.PortalID, strLogin);

                if(userInfo == null)
                {
                    result = false;
                    resultText = "Unknown Login Error";
                }
                else
                {
                    // Set Login Cookie
                    var expiration = DateTime.Now.Add(FormsAuthentication.Timeout); 

                    var ticket = new FormsAuthenticationTicket(1, strLogin, DateTime.Now, expiration, false, userInfo.UserID.ToString());
                    var authCookie = new HttpCookie(aftContext.AuthCookieName, FormsAuthentication.Encrypt(ticket))
                    {
                        Domain = FormsAuthentication.CookieDomain,
                        Path = FormsAuthentication.FormsCookiePath,
                    };


                    Context.Response.SetCookie(authCookie);
                }
            }

            Context.Response.AddHeader("Mobiquo_is_login", result ? "true" : "false"); 

            var rpcstruct = new XmlRpcStruct
                                {
                                    {"result", result },
                                    {"result_text", resultText.ToBytes()}, 
                                    {"can_upload_avatar", false}
                                };
          

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


        // Helper Methods

        private static string GetSummary(string summary, string body)
        {
            var result = !string.IsNullOrWhiteSpace(summary) ? summary : body;

            result = result + string.Empty;

            result = Utilities.StripHTMLTag(result);

            result = result.Length > 200 ? result.Substring(0, 200) : result;

            return result.Trim();
        }

        private static string TapatalkToHtml(string input)
        {
            input = input.Replace("\n", "<br />");

            input = Regex.Replace(input, @"\[img\](.+)\[\/img\]", "<img src='$1' />", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[url\](.+)\[\/url\]", "<a href='$1'>$1</a>", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"\[quote\=(.+)\](.+)\[\/quote\]", "<blockquote class='afQuote'><span class='afQuoteTitle'>Originaly Posted By <span class='afQuoteAuthor'>$1</span></span><br />$2</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[quote\](.+)\[\/quote\]", "<blockquote class='afQuote'>$1</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[b\](.+)\[\/b\]", "<strong>$1</strong>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            input = Regex.Replace(input, @"\[i\](.+)\[\/i\]", "<i>$1</i>", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            return input;
        }

        private static string HtmlToTapatalk(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            input = Regex.Replace(input, @"\s+", " ", RegexOptions.Multiline);

            var htmlBlock = new HtmlDocument();
            htmlBlock.LoadHtml(input);

            var tapatalkMarkup = new StringBuilder();

            ProcessNode(tapatalkMarkup, htmlBlock.DocumentNode);

            tapatalkMarkup.Replace("&nbsp;", " ");

            var result = tapatalkMarkup.ToString();

            return result;
        }

        private static void ProcessNodes(StringBuilder output, IEnumerable<HtmlNode> nodes, bool textOnly = false)
        {
            foreach (var node in nodes)
                ProcessNode(output, node);
        }

        private static void ProcessNode(StringBuilder output, HtmlNode node, bool textOnly = false)
        {
            const string lineBreak = "<br /> ";

            if (node == null || output == null || (textOnly && node.Name != "#text"))
                return;

            switch (node.Name)
            {
                // No action needed for these node types
                case "#text":
                    var text = HttpUtility.HtmlDecode(node.InnerHtml).Replace("<", "&lt;").Replace(">", "&gt;");
                    output.Append(text);
                    return;

                case "table":
                    output.Append("<br /> { Table Removed } <br />");
                    return;

                case "script":
                    return;

                case "ol":
                case "ul":
                    output.Append(lineBreak);
                    ProcessNodes(output, node.ChildNodes, textOnly);
                    output.Append(lineBreak);
                    return;

                case "li":
                    output.Append("* ");
                    ProcessNodes(output, node.ChildNodes, textOnly);
                    output.Append(lineBreak);
                    return;

                case "p":
                    output.Append("<p>");
                    ProcessNodes(output, node.ChildNodes, textOnly);
                    output.Append("</p><br />");
                    return;

                case "b":
                case "strong":
                    output.Append("<b>");
                    ProcessNodes(output, node.ChildNodes, textOnly);
                    output.Append("</b>");
                    return;

                case "i":
                    output.Append("<i>");
                    ProcessNodes(output, node.ChildNodes, textOnly);
                    output.Append("</i>");
                    return;

                case "blockquote":
                    output.Append("[quote]");
                    ProcessNodes(output, node.ChildNodes, textOnly);
                    output.Append("[/quote]" + lineBreak);
                    return;

                case "br":
                    output.Append("<br />");
                    return;


                case "img":

                    var src = node.Attributes["src"];
                    if (src == null || string.IsNullOrWhiteSpace(src.Value))
                        return;

                    output.AppendFormat(src.Value.IndexOf("emoticon", 0, StringComparison.InvariantCultureIgnoreCase) >= 0 ? "<img src='{0}' />" : "[img]{0}[/img]", src.Value);

                    return;

                case "a":

                    var href = node.Attributes["href"];
                    if (href == null || string.IsNullOrWhiteSpace(href.Value))
                        return;

                    output.AppendFormat("[url={0}]", href.Value);
                    ProcessNodes(output, node.ChildNodes, true);
                    output.Append("[/url]");

                    return;

            }

            ProcessNodes(output, node.ChildNodes);
        }

        private static string GetAuthorName(SettingsInfo settings, UserInfo user)
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

    }
}