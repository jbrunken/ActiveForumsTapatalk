using System;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Modules.ActiveForums;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class ActiveForumsTapatalkModuleContext
    {
        private HttpContext _context;
        private ModuleInfo _module;
        private ActiveForumsTapatalkModuleSettings _moduleSettings;
        private PortalInfo _portal;
        private int? _userId;
        private User _forumUser;
         
        public  bool? AuthCookieExpired { get; internal set; }

        public int ModuleId { get; internal set; }

        public ModuleInfo Module
        {
            get { return _module ?? (_module = new ModuleController().GetModule(ModuleId)); }
        } 

        public ActiveForumsTapatalkModuleSettings ModuleSettings
        {
            get
            {
                if(_moduleSettings == null && Module != null)
                {
                    _moduleSettings = ActiveForumsTapatalkModuleSettings.Create(Module.ModuleSettings);
                }

                return _moduleSettings;
            }
        }

        public PortalInfo Portal
        {
            get
            {
                if(_portal == null && Module != null)
                {
                    _portal = new PortalController().GetPortal(Module.PortalID);
                }

                return _portal;
            }
        }

        public string AuthCookieName
        {
            get { return ".ActiveForumsTapatalk_" + ModuleId; }
        }

        public int UserId
        {
            get
            {
                if(!_userId.HasValue)
                {
                    _userId = 0;

                    var authCookie = _context.Request.Cookies[AuthCookieName];
                    if(authCookie != null && !string.IsNullOrWhiteSpace(authCookie.Value))
                    {
                        var ticket = FormsAuthentication.Decrypt(authCookie.Value);
                        if(ticket.Expired)
                        {
                            _context.Response.Cookies.Add(new HttpCookie(AuthCookieName, string.Empty) { Expires = DateTime.Now.AddDays(-1) });
                        }
                        else
                        {
                            _userId = int.Parse(ticket.UserData); 
                        }  
                    }
                }

                return _userId.Value;
            }
        }

        public User ForumUser
        {
            get
            {
                if (_forumUser == null && Module != null)
                {
                    _forumUser =  (UserId > 0) ? new UserController().GetUser(Module.PortalID, ModuleId, UserId) : new User();
                }

                return _forumUser;
            }
        }

        public static ActiveForumsTapatalkModuleContext Create(HttpContext context)
        {
            var match = Regex.Match(context.Request.Path, @"\/aft(\d+)\/mobiquo.php", RegexOptions.IgnoreCase);

            int moduleId;

            if (match.Groups.Count < 2 || !int.TryParse(match.Groups[1].Value, out moduleId))
                return null;

            var result = new ActiveForumsTapatalkModuleContext { _context = context, ModuleId = moduleId };

            return result;


        }
    }
}