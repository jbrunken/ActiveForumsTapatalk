using System;
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
            var cacheKey = string.Format("aftfp:{0}", forumId);

            var result = DataCache.CacheRetrieve(cacheKey) as Permissions;

            if(result == null)
            {
                using (var ctx = DataContext.Instance())
                {
                    result = ctx.ExecuteSingleOrDefault<Permissions>(CommandType.StoredProcedure, "activeforumstapatalk_Forum_Permissions", forumId);
                }

                if (result != null)
                    DataCache.CacheStore(cacheKey, result, DateTime.Now.AddMinutes(5));
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

        public string BuildUrl(int tabId, int moduleId, string groupPrefix, string forumPrefix, int forumGroupId, int forumID, int topicId, string topicURL, int tagId, int categoryId, string otherPrefix, int pageId, int socialGroupId)
        {
            var mainSettings = DataCache.MainSettings(moduleId);
            string[] @params = { };
            if (!mainSettings.URLRewriteEnabled || (((string.IsNullOrEmpty(forumPrefix) && forumID > 0 && string.IsNullOrEmpty(groupPrefix)) | (string.IsNullOrEmpty(forumPrefix) && string.IsNullOrEmpty(groupPrefix) && forumGroupId > 0)) && string.IsNullOrEmpty(otherPrefix)))
            {
                if (forumID > 0 && topicId == -1)
                {
                    @params = Utilities.AddParams(ParamKeys.ForumId + "=" + forumID.ToString(), @params);
                }
                else if (forumGroupId > 0 && topicId == -1)
                {
                    @params = Utilities.AddParams(ParamKeys.GroupId + "=" + forumGroupId.ToString(), @params);
                }
                else if (tagId > 0)
                {
                    //afv=grid&afgt=tags&aftg=
                    @params = Utilities.AddParams("afv=grid", @params);
                    @params = Utilities.AddParams("afgt=tags", @params);
                    @params = Utilities.AddParams("aftg=" + tagId.ToString(), @params);


                }
                else if (categoryId > 0)
                {
                    @params = Utilities.AddParams("act=" + categoryId.ToString(), @params);
                }
                else if (!(string.IsNullOrEmpty(otherPrefix)))
                {
                    @params = Utilities.AddParams("afv=grid", @params);
                    @params = Utilities.AddParams("afgt=" + otherPrefix, @params);
                }
                else if (topicId > 0)
                {
                    @params = Utilities.AddParams(ParamKeys.TopicId + "=" + topicId.ToString(), @params);
                }
                if (pageId > 1)
                {
                    @params = Utilities.AddParams(ParamKeys.PageId + "=" + pageId, @params);
                }
                if (socialGroupId > 0)
                {
                    @params = Utilities.AddParams("GroupId=" + socialGroupId, @params);
                }
                return Utilities.NavigateUrl(tabId, "", @params);
            }
            else
            {
                string sURL = string.Empty;
                if (!(string.IsNullOrEmpty(mainSettings.PrefixURLBase)))
                {
                    sURL += "/" + mainSettings.PrefixURLBase;
                }
                if (!(string.IsNullOrEmpty(groupPrefix)))
                {
                    sURL += "/" + groupPrefix;
                }
                if (!(string.IsNullOrEmpty(forumPrefix)))
                {
                    sURL += "/" + forumPrefix;
                }
                if (!(string.IsNullOrEmpty(topicURL)))
                {
                    sURL += "/" + topicURL;
                }
                if (tagId > 0)
                {
                    sURL += "/" + mainSettings.PrefixURLTag + "/" + otherPrefix;
                }
                else if (categoryId > 0)
                {
                    sURL += "/" + mainSettings.PrefixURLCategory + "/" + otherPrefix;
                }
                else if (!(string.IsNullOrEmpty(otherPrefix)) && (tagId == -1 || categoryId == -1))
                {
                    sURL += "/" + mainSettings.PrefixURLOther + "/" + otherPrefix;
                }
                if (topicId > 0 && string.IsNullOrEmpty(topicURL))
                {
                    return Utilities.NavigateUrl(tabId, "", ParamKeys.TopicId + "=" + topicId.ToString());
                }
                if (pageId > 1)
                {
                    if (string.IsNullOrEmpty(sURL))
                    {
                        return Utilities.NavigateUrl(tabId, "", ParamKeys.PageId + "=" + pageId);
                    }
                    sURL += "/" + pageId.ToString();
                }
                if (string.IsNullOrEmpty(sURL))
                {
                    return Utilities.NavigateUrl(tabId);
                }
                return sURL + "/";
            }
        }

        public PostIndex GetForumPostIndex(int contentId)
        {
            PostIndex result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteSingleOrDefault<PostIndex>(CommandType.StoredProcedure, "activeforumstapatalk_Forum_TopicPostIndex", contentId);
            }

            return result;  
        }

        public int GetForumPostIndexUnread(int topicId, int userId)
        {
            int? result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteScalar<int?>(CommandType.StoredProcedure, "activeforumstapatalk_Forum_TopicPostIndexUnread", topicId, userId);
            }

            return result.HasValue ? result.Value : 1; 
        }

        public IEnumerable<ListForum> GetSubscribedForums(int portalId, int moduleId, int userId, string forumIds)
        {
            IEnumerable<ListForum> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ListForum>(CommandType.StoredProcedure, "activeforumstapatalk_Forums_Subscribed", portalId, moduleId, userId, forumIds);
            }

            return result;
        }

        public IEnumerable<ListForum> GetParticipatedForums(int portalId, int moduleId, int userId, string forumIds)
        {
            IEnumerable<ListForum> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ListForum>(CommandType.StoredProcedure, "activeforumstapatalk_Forums_Participated", portalId, moduleId, userId, forumIds);
            }

            return result;
        }

        public IEnumerable<ListForum> GetForumStatus(int portalId, int moduleId, int userId, string forumIds)
        {
            IEnumerable<ListForum> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ListForum>(CommandType.StoredProcedure, "activeforumstapatalk_Forums_Status", portalId, moduleId, userId, forumIds);
            }

            return result;
        }

        public IEnumerable<ForumTopic> GetSubscribedTopics(int portalId, int moduleId, int userId, string forumIds, int rowIndex, int maxRows)
        {
            IEnumerable<ForumTopic> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ForumTopic>(CommandType.StoredProcedure, "activeforumstapatalk_ForumTopics_Subscribed", portalId, moduleId, userId, forumIds, rowIndex, maxRows);
            }

            return result; 
        }

        public IEnumerable<ForumTopic> GetUnreadTopics(int portalId, int moduleId, int userId, string forumIds, int rowIndex, int maxRows)
        {
            IEnumerable<ForumTopic> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ForumTopic>(CommandType.StoredProcedure, "activeforumstapatalk_ForumTopics_Unread", portalId, moduleId, userId, forumIds, rowIndex, maxRows);
            }

            return result;
        }

        public IEnumerable<ForumTopic> GetParticipatedTopics(int portalId, int moduleId, int userId, string forumIds, int participantUserId, int rowIndex, int maxRows)
        {
            IEnumerable<ForumTopic> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ForumTopic>(CommandType.StoredProcedure, "activeforumstapatalk_ForumTopics_Participated", portalId, moduleId, userId, forumIds, participantUserId, rowIndex, maxRows);
            }

            return result;
        }

        public IEnumerable<ForumTopic> GetLatestTopics(int portalId, int moduleId, int userId, string forumIds, int rowIndex, int maxRows)
        {
            IEnumerable<ForumTopic> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ForumTopic>(CommandType.StoredProcedure, "activeforumstapatalk_ForumTopics_Latest", portalId, moduleId, userId, forumIds, rowIndex, maxRows);
            }

            return result;
        }

        public IEnumerable<ForumTopic> GetTopicStatus(int portalId, int moduleId, int userId, string forumIds, string topicIds)
        {
            IEnumerable<ForumTopic> result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteQuery<ForumTopic>(CommandType.StoredProcedure, "activeforumstapatalk_ForumTopics_Status", portalId, moduleId, userId, forumIds, topicIds);
            }

            return result;
        }

        public bool MarkTopicsRead(int portalId, int moduleId, int userId, string forumIds, string topicIds)
        {

            using (var ctx = DataContext.Instance())
            {
                ctx.Execute(CommandType.StoredProcedure, "activeforumstapatalk_ForumTopics_MarkAsRead", portalId, moduleId, userId, forumIds, topicIds);
            }

            return true;
        }


        //DotNetNuke.Services.Social.Messaging.Internal.InternalMessagingController.Instance.GetInbox()


    }
}