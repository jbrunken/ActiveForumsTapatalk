using System.Collections;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class ActiveForumsTapatalkModuleSettings
    {
        private const string IsOpenKey = "IsOpen";
        private const string AllowAnonymousKey = "AllowAnonymous";
        private const string ForumModuleIdKey = "ForumModuleId";
        private const string ForumTabIdKey = "ForumTabId";

        private const bool IsOpenDefault = true;
        private const bool AllowAnonymousDefault = true;
        private const int ForumModuleIdDefault = 724; //-1;
        private const int ForumTabIdDefault = 186; //-1;

        public bool IsOpen { get; set; }
        public bool AllowAnonymous { get; set; }
        public int ForumModuleId { get; set; }
        public int ForumTabId { get; set; }

        public static ActiveForumsTapatalkModuleSettings Create(Hashtable moduleSettings)
        {
            var result = new ActiveForumsTapatalkModuleSettings
                             {
                                IsOpen = ReadBool(moduleSettings, IsOpenKey, IsOpenDefault),
                                AllowAnonymous = ReadBool(moduleSettings, AllowAnonymousKey, AllowAnonymousDefault), 
                                ForumModuleId = ReadInt(moduleSettings, ForumModuleIdKey, ForumModuleIdDefault),
                                ForumTabId = ReadInt(moduleSettings, ForumTabIdKey, ForumTabIdDefault)
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

    }
}