
using System;
using DotNetNuke.Common;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Actions;
using DotNetNuke.Modules.ActiveForumsTapatalk.Classes;

namespace DotNetNuke.Modules.ActiveForumsTapatalk
{
    /// -----------------------------------------------------------------------------
    /// <summary>
    /// The View class displays the content
    /// 
    /// Typically your view control would be used to display content or functionality in your module.
    /// 
    /// View may be the only control you have in your project depending on the complexity of your module
    /// 
    /// Because the control inherits from ActiveForumsTapatalkModuleBase you have access to any custom properties
    /// defined there, as well as properties from DNN such as PortalId, ModuleId, TabId, UserId and many more.
    /// 
    /// </summary>
    /// -----------------------------------------------------------------------------
    public partial class View : ActiveForumsTapatalkModuleBase, IActionable
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            tapatalkConfigWarning.Visible = false;

            var settings = ActiveForumsTapatalkModuleSettings.Create(Settings);

            if (Globals.IsEditMode())
                CheckConfig(settings);

            const string tapatalkDetectionKey = "TapatalkDetection";

            if (!Globals.IsEditMode() && settings.IsTapatalkDetectionEnabled && !Page.ClientScript.IsClientScriptIncludeRegistered(tapatalkDetectionKey))
            {
                Framework.jQuery.RequestRegistration(); 
                Page.ClientScript.RegisterClientScriptInclude(tapatalkDetectionKey, Page.ResolveUrl("~/DesktopModules/ActiveForumsTapatalk/tapatalkdetect.js"));
            }
        }




        public ModuleActionCollection ModuleActions
        {
            get
            {
                var actions = new ModuleActionCollection
                    {
                        //{
                            //GetNextActionID(), Localization.GetString("EditModule", LocalResourceFile), "", "", "", EditUrl(), false, SecurityAccessLevel.Edit, true, false
                        //}
                    };
                return actions;
            }
        }

        private void CheckConfig(ActiveForumsTapatalkModuleSettings settings)
        {
            if (settings.ForumModuleId >= 0 && settings.ForumTabId >= 0) 
                return;

            tapatalkConfigWarning.Visible = true;
            tapatalkConfigWarning.InnerText = LocalizeString("ConfigWarning");
        }
    }
}