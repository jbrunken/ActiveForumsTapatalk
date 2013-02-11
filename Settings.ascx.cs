using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Entities.Users;
using DotNetNuke.Modules.ActiveForumsTapatalk.Classes;
using Exceptions = DotNetNuke.Services.Exceptions.Exceptions;

namespace DotNetNuke.Modules.ActiveForumsTapatalk
{
    public partial class Settings : ActiveForumsTapatalkModuleSettingsBase
    {
        #region Base Method Implementations

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// LoadSettings loads the settings from the Database and displays them
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override void LoadSettings()
        {
            try
            {
                if (Page.IsPostBack == false)
                {
                    var moduleSettings = new ModuleController().GetModuleSettings(ModuleId);
                    var settings = ActiveForumsTapatalkModuleSettings.Create(moduleSettings);

                    // Bind the Simple Settings
                    ckEnabled.Checked = settings.IsOpen;
                    ckAllowAnonymous.Checked = settings.AllowAnonymous;
                    txtRegistrationPage.Text = settings.RegistrationUrl;

                    // Bind Active Forum Instances
                    ddlAFInstance.Items.Clear();
                    ddlAFInstance.ClearSelection();

                    var mc = new ModuleController();
                    var tc = new TabController();

                    var selectedValue = string.Format("{0}|{1}", settings.ForumTabId, settings.ForumModuleId);

                    foreach(ModuleInfo mi in mc.GetModulesByDefinition(PortalId, "Active Forums"))
                    {
                        if(mi.IsDeleted)
                            continue;

                        var ti = tc.GetTab(mi.TabID, PortalId, false);
                        if(ti != null && !ti.IsDeleted)
                        {
                            var itemValue = string.Format("{0}|{1}", ti.TabID, mi.ModuleID);
                            ddlAFInstance.Items.Add(new ListItem
                                                        {
                                                            Text = ti.TabName + " - " + mi.DesktopModule.ModuleName,
                                                            Value = ti.TabID + "|" + mi.ModuleID,
                                                            Selected = itemValue == selectedValue
                                                        });
                        }
                    }

                    // Bind the Tapatalk.Com info
                    txtForumUrl.Text = string.Format("{0}://{1}", Request.Url.Scheme, Request.Url.Host);
                    txtInstallationDirectoryName.Text = string.Format("aft{0}", ModuleId);
                    txtFileExtension.Text = "ashx";

                    // Bind the Tapatalk API Handler
                    var isTapatalkAPIHandlerEnabled = IsTapatalkAPIHandlerEnabled();

                    ckEnabled.Enabled = isTapatalkAPIHandlerEnabled;
                    lbInstallHandler.Text =
                        LocalizeString(isTapatalkAPIHandlerEnabled
                                           ? "UninstallTapatalkAPIHandler"
                                           : "InstallTapatalkAPIHandler");

                    var u = UserController.GetCurrentUserInfo();
                    lbInstallHandler.Enabled = u.IsSuperUser && !PortalSettings.PortalAlias.HTTPAlias.Contains("/");

                }
            }
            catch (Exception exc) //Module failed to load
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// UpdateSettings saves the modified settings to the Database
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override void UpdateSettings()
        {
            try
            {
                var tabId = -1;
                var forumModuleId = -1;

                if(!string.IsNullOrWhiteSpace(ddlAFInstance.SelectedValue))
                {
                    var selectedIDArray = ddlAFInstance.SelectedValue.Split('|');
                    tabId = int.Parse(selectedIDArray[0]);
                    forumModuleId = int.Parse(selectedIDArray[1]);
                }

                var mc = new ModuleController();

                var moduleSettings = mc.GetModuleSettings(ModuleId);
                var settings = ActiveForumsTapatalkModuleSettings.Create(moduleSettings);

                settings.IsOpen = ckEnabled.Checked;
                settings.ForumTabId = tabId;
                settings.ForumModuleId = forumModuleId;
                settings.AllowAnonymous = ckAllowAnonymous.Checked;
                settings.RegistrationUrl = txtRegistrationPage.Text.Trim();

                settings.Save(mc, ModuleId);
            }
            catch (Exception exc) //Module failed to load
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        public void ToggleTapatalkAPIHandler(object sender, EventArgs e)
        {
            var u = UserController.GetCurrentUserInfo();

            if (!u.IsSuperUser || PortalSettings.PortalAlias.HTTPAlias.Contains("/"))
                return;

            if(IsTapatalkAPIHandlerEnabled())
            {
                if (DisableTapatalkAPIHandler())
                {
                    lbInstallHandler.Text = LocalizeString("InstallTapatalkAPIHandler");
                    ckEnabled.Checked = false;
                    ckEnabled.Enabled = false;
                }
                    
            }
            else
            {
                if (EnableTapatalkAPIHandler())
                {
                    lbInstallHandler.Text = LocalizeString("UninstallTapatalkAPIHandler");
                    ckEnabled.Enabled = true;
                }
                    
            }
        }


        private static string GetFile(string filePath)
        {
            var sContents = string.Empty;
            if (File.Exists(filePath))
            {
                try
                {
                    using (var sr = new StreamReader(filePath))
                    {
                        sContents = sr.ReadToEnd();
                        sr.Close();
                    }
                }
                catch (Exception exc)
                {
                    sContents = exc.Message;
                }
            }
            return sContents;
        }

        private static bool IsTapatalkAPIHandlerEnabled()
        {
            try
            {
                var sConfig = GetFile(HttpContext.Current.Server.MapPath("~/web.config"));
                return sConfig.Contains("DotNetNuke.Modules.ActiveForumsTapatalk.Handlers.TapatalkAPIHandler");
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private static bool EnableTapatalkAPIHandler()
        {
            try
            {
                var configPath = HttpContext.Current.Server.MapPath("~/web.config");

                var xDoc = new XmlDocument();
                xDoc.Load(configPath);

                var xRoot = xDoc.DocumentElement;
                if (xRoot == null)
                    return false;

                var xNode = xRoot.SelectSingleNode("//system.webServer/handlers");
                if (xNode == null)
                    return false;

                var isInstalled = xNode.ChildNodes.Cast<XmlNode>().Any(n => n.Attributes != null && n.Attributes["name"].Value == "ActiveForumTapatalkAPIHandler");
                if (isInstalled)
                    return true;

                var xNewNode = xDoc.CreateElement("add");
                var xAttrib = xDoc.CreateAttribute("name");
                xAttrib.Value = "ActiveForumTapatalkAPIHandler";
                xNewNode.Attributes.Append(xAttrib);
                xAttrib = xDoc.CreateAttribute("verb");
                xAttrib.Value = "*";
                xNewNode.Attributes.Append(xAttrib);
                xAttrib = xDoc.CreateAttribute("path");
                xAttrib.Value = "mobiquo.ashx";
                xNewNode.Attributes.Append(xAttrib);
                xAttrib = xDoc.CreateAttribute("type");
                xAttrib.Value = "DotNetNuke.Modules.ActiveForumsTapatalk.Handlers.TapatalkAPIHandler, DotNetNuke.Modules.ActiveForumsTapatalk";
                xNewNode.Attributes.Append(xAttrib);
                xAttrib = xDoc.CreateAttribute("preCondition");
                xAttrib.Value = "integratedMode";
                xNewNode.Attributes.Append(xAttrib);
                xNode.PrependChild(xNewNode);
                xDoc.Save(configPath);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }


        }

        private static bool DisableTapatalkAPIHandler()
        {
            try
            {
                var configPath = HttpContext.Current.Server.MapPath("~/web.config");

                var xDoc = new XmlDocument();
                xDoc.Load(configPath);

                var xRoot = xDoc.DocumentElement;
                if (xRoot == null)
                    return false;

                var xNode = xRoot.SelectSingleNode("//system.webServer/handlers");
                if (xNode == null)
                    return false;

                var isInstalled = false;
                foreach (var n in from XmlNode n in xNode.ChildNodes where n.Attributes != null && n.Attributes["name"].Value == "ActiveForumTapatalkAPIHandler" select n)
                {
                    xNode.RemoveChild(n);
                    isInstalled = true;
                    break;
                }
                if (isInstalled)
                {
                    xDoc.Save(configPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }


        }

        #endregion
    }
}