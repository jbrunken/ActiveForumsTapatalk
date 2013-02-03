using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Web.Api;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Services
{
    public class UtilityController : DnnApiController
    {
        // This is used to redirect from our "tapatalk" forum URL to the actual forum URL"

        [AllowAnonymous]
        [HttpGet]
        public HttpResponseMessage Forums(int moduleId)
        {
            var mc = new ModuleController();

            var module = mc.GetModule(moduleId);

            var tabID = 186; // module.ModuleSettings["ForumTabID"];

            var tc = new TabController();

            var tabInfo = tc.GetTab(tabID, -1, true);

            var friendlyUrl = Common.Globals.FriendlyUrl(tabInfo, string.Empty);

            var response = Request.CreateResponse(HttpStatusCode.Moved);

            response.Headers.Location = new Uri(friendlyUrl);

            return response;
        }
    }
}