using System.Collections.Generic;
using System.Data;
using DotNetNuke.Data;
using DotNetNuke.Modules.ActiveForums;
using DotNetNuke.Modules.ActiveForumsTapatalk.Structures;

namespace DotNetNuke.Modules.ActiveForumsTapatalk.Classes
{
    public class AFTForumController : ForumController
    {
        internal string GetForumsForUser(string userRoles, int portalId, int moduleId, string permissionType = "CanView")
		{
			var db = new ActiveForums.Data.ForumsDB();
			var forumIds = string.Empty;
			var fc = db.Forums_List(portalId, moduleId);
			foreach (Forum f in fc)
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
				var canView = ActiveForums.Permissions.HasPerm(roles, userRoles);
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

        public Permissions GetForumPermissions(int forumId)
        {
            Permissions result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteSingleOrDefault<Permissions>(CommandType.StoredProcedure, "activeforumstapatalk_Forum_Permissions", forumId);
            }

            return result;
        }

        public ForumTopicSummary GetForumTopicSummary(int portalId, int moduleId, int forumId, int userId, string mode)
        {
            ForumTopicSummary result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteSingleOrDefault<ForumTopicSummary>(CommandType.StoredProcedure, "activeforumstapatalk_Forum_TopicsSummary", portalId, moduleId, forumId, userId, mode);
            }

            return result;
        }

        public IEnumerable<ForumTopic> GetForumTopics(int portalId, int moduleId, int forumId, int userId, int rowIndex, int maxRows, string mode)
        {
            IEnumerable<ForumTopic> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ForumTopic>(CommandType.StoredProcedure, "activeforumstapatalk_Topics", portalId, moduleId, forumId, userId, rowIndex, maxRows, mode);
            }

            return result; 
        }

        public int GetTopicForumId(int topicId)
        {
            int forumId;

            using (var ctx = DataContext.Instance())
            {
                forumId = ctx.ExecuteScalar<int>(CommandType.StoredProcedure, "activeforumstapatalk_GetTopicForumId", topicId);
            }

            return forumId; 
        }

        public ForumPostSummary GetForumPostSummary(int portalId, int moduleId, int forumId, int topicId, int userId)
        {
            ForumPostSummary result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteSingleOrDefault<ForumPostSummary>(CommandType.StoredProcedure, "activeforumstapatalk_Forum_TopicPostSummary", portalId, moduleId, forumId, topicId, userId);
            }

            return result;
        }

        public IEnumerable<ForumPost> GetForumPosts(int portalId, int moduleId, int forumId, int topicId, int userId, int rowIndex, int maxRows, bool updateTrackingAndCounts = true)
        {
            IEnumerable<ForumPost> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ForumPost>(CommandType.StoredProcedure, "activeforumstapatalk_Forum_TopicPosts", portalId, moduleId, forumId, topicId, userId, rowIndex, maxRows, updateTrackingAndCounts);
            }

            return result;
        }

        public ForumPost GetForumPost(int portalId, int moduleId, int contentId)
        {
            ForumPost result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteSingleOrDefault<ForumPost>(CommandType.StoredProcedure, "activeforumstapatalk_PostByContentId", portalId, moduleId, contentId);
            }

            return result;
        }
    }
}