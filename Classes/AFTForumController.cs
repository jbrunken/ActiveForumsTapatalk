using System.Collections.Generic;
using DotNetNuke.Data;
using DotNetNuke.Modules.ActiveForums;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class AFTForumController : ForumController
    {
        internal string GetForumsForUser(string userRoles, int portalId, int moduleId, string permissionType = "CanView")
		{
			var db = new ActiveForums.Data.ForumsDB();
			var forumIds = string.Empty;
			var fc = db.Forums_List(portalId, moduleId);
			foreach (ActiveForums.Forum f in fc)
			{
				string roles;
				switch (permissionType)
				{
					case "CanView":
						roles = f.Security.View;
						break;
					case "CanRead":
						roles = f.Security.Read;
						break;
					case "CanApprove":
						roles = f.Security.ModApprove;
						break;
					case "CanEdit":
						roles = f.Security.ModEdit;
						break;
					default:
						roles = f.Security.View;
						break;
				}
				var canView = Permissions.HasPerm(roles, userRoles);
				if ((canView || (f.Hidden == false && (permissionType == "CanView" || permissionType == "CanRead"))) && f.Active)
				{
					forumIds += f.ForumID + ";";
				}
			}
			return forumIds;
		}

        public IEnumerable<ForumSubscription> GetSubscriptionsForUser(int moduleId, int userId, int? forumId, int? topicId)
        {
            if(userId <= 0)
                return new List<ForumSubscription>();

            IEnumerable<ForumSubscription> subscriptions;
            
            using (var ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<ForumSubscription>();

                var whereClause = "WHERE [ModuleId] = @0 AND [UserId] = @1";
                
                if (forumId.HasValue)
                    whereClause = whereClause + " AND @2 = [ForumId]";

                if (topicId.HasValue)
                    whereClause = whereClause + " AND @3 = [TopicId]";

                subscriptions = rep.Find(whereClause, moduleId, userId, forumId, topicId);
            }

            return subscriptions;
        }
    }
}