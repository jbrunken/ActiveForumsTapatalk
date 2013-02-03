using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using CookComputing.XmlRpc;
using DotNetNuke.Modules.ActiveForums;
using DotNetNuke.Modules.ActiveForumsTapatalk.Classes;
using DotNetNuke.Modules.ActiveForumsTapatalk.Extensions;
using DotNetNuke.Security.Membership;
using UserController = DotNetNuke.Entities.Users.UserController;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Services
{
    [XmlRpcService(Name = "ActiveForums.Tapatalk", Description = "Tapatalk Service For Active Forums", UseIntTag = true)]
    public class TapatalkAPIService : XmlRpcService
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
                                    {"guest_okay", aftContext.ModuleSettings.AllowAnonymous}
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
                .Where(o => o.Active && Permissions.HasPerm(o.ReadRoles, aftContext.ForumUser.UserRoles))
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
                    ForumId = group.ID.ToString(),
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
                        ParentId = group.ID.ToString(),
                        LogoUrl = null,
                        HasNewPosts = aftContext.UserId > 0 &&  groupForum.LastPostDate > groupForum.LastRead,
                        IsProtected = false,
                        IsSubscribed = forumSubscriptions.Any(fs => fs.ForumId == groupForum.ID),
                        CanSubscribe = Permissions.HasPerm(groupForum.SubscribeRoles, aftContext.ForumUser.UserRoles),
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
                                CanSubscribe = Permissions.HasPerm(subForum.SubscribeRoles, aftContext.ForumUser.UserRoles),
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

            UserController.ValidateUser(aftContext.Portal.PortalID, strLogin, strPassword, string.Empty, aftContext.Portal.PortalName, Context.Request.UserHostAddress, ref loginStatus);

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
                var userInfo = UserController.GetUserByName(aftContext.Module.PortalID, strLogin);

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
    }
}