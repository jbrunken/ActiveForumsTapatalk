using System;
using System.Collections;
using DotNetNuke.Entities.Modules;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class ActiveForumsTapatalkModuleSettings
    {
        private const string IsOpenKey = "IsOpen";
        private const string AllowAnonymousKey = "AllowAnonymous";
        private const string ForumModuleIdKey = "ForumModuleId";
        private const string ForumTabIdKey = "ForumTabId";
        private const string RegistrationUrlKey = "RegistrationUrl";
        private const string SearchPermissionKey = "SearchPermission";
        private const string IsTapatalkDetectionEnabledKey = "IsTapatalkDetectionEnabled";

        private const bool IsOpenDefault = false;
        private const bool AllowAnonymousDefault = true;
        private const int ForumModuleIdDefault = -1;
        private const int ForumTabIdDefault = -1;
        private const string RegistrationUrlDefault = "register.aspx";
        private const SearchPermissions SearchPermissionDefault = SearchPermissions.Everyone;
        private const bool IsTapatalkDetectionEnabledDefault = true;

        public bool IsOpen { get; set; }
        public bool AllowAnonymous { get; set; }
        public int ForumModuleId { get; set; }
        public int ForumTabId { get; set; }
        public string RegistrationUrl { get; set; }
        public SearchPermissions SearchPermission { get; set; }
        public bool IsTapatalkDetectionEnabled { get; set; }

        public bool Save(ModuleController moduleController, int moduleId)
        {
            try
            {
                if (moduleController == null || moduleId < 0)
                    return false;

                moduleController.UpdateModuleSetting(moduleId, IsOpenKey, IsOpen.ToString());
                moduleController.UpdateModuleSetting(moduleId, AllowAnonymousKey, AllowAnonymous.ToString());
                moduleController.UpdateModuleSetting(moduleId, ForumModuleIdKey, ForumModuleId.ToString());
                moduleController.UpdateModuleSetting(moduleId, ForumTabIdKey, ForumTabId.ToString());
                moduleController.UpdateModuleSetting(moduleId, RegistrationUrlKey, RegistrationUrl);
                moduleController.UpdateModuleSetting(moduleId, SearchPermissionKey, ((int)SearchPermission).ToString());
                moduleController.UpdateModuleSetting(moduleId, IsTapatalkDetectionEnabledKey, IsTapatalkDetectionEnabled.ToString());

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static ActiveForumsTapatalkModuleSettings Create(Hashtable moduleSettings)
        {
            var result = new ActiveForumsTapatalkModuleSettings
                             {
                                IsOpen = ReadBool(moduleSettings, IsOpenKey, IsOpenDefault),
                                AllowAnonymous = ReadBool(moduleSettings, AllowAnonymousKey, AllowAnonymousDefault), 
                                ForumModuleId = ReadInt(moduleSettings, ForumModuleIdKey, ForumModuleIdDefault),
                                ForumTabId = ReadInt(moduleSettings, ForumTabIdKey, ForumTabIdDefault),
                                RegistrationUrl = ReadString(moduleSettings, RegistrationUrlKey, RegistrationUrlDefault),
                                SearchPermission = (SearchPermissions)ReadInt(moduleSettings, SearchPermissionKey, (int)SearchPermissionDefault),
                                IsTapatalkDetectionEnabled = ReadBool(moduleSettings, IsTapatalkDetectionEnabledKey, IsTapatalkDetectionEnabledDefault)
                             };

            return result;
        }

        private static bool ReadBool(IDictionary settings, string key, bool defaultValue)
        {
            if (settings == null || settings.Count == 0)
                return defaultValue;

            var value = settings[key];
            
            if(value == null)
                return defaultValue;

            bool parseResult;

            return bool.TryParse(value.ToString(), out parseResult) ? parseResult : defaultValue;
        }

        private static int ReadInt(IDictionary settings, string key, int defaultValue)
        {
            if (settings == null || settings.Count == 0)
                return defaultValue;

            var value = settings[key];

            if (value == null)
                return defaultValue;

            int parseResult;    

            return int.TryParse(value.ToString(), out parseResult) ? parseResult : defaultValue;
        }

        private static string ReadString(IDictionary settings, string key, string defaultValue)
        {
            if (settings == null || settings.Count == 0)
                return defaultValue;

            var value = settings[key] as string;

            return value ?? defaultValue;
        }

        public enum SearchPermissions
        {
            Disabled = 0,
            Everyone = 1,
            RegisteredUsers = 2
        }
    }
}