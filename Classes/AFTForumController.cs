using System;
using System.Collections.Generic;
using System.Data;
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
                var hasRequestedPermission = ActiveForums.Permissions.HasPerm(roles, userRoles);
				if (hasRequestedPermission && f.Active)
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

        public TopicSearchResults SearchTopics(int portalId, int moduleId, int userId, string forumIds, string searchText, int rowIndex, int maxRows, string searchId, SettingsInfo mainSettings)
        {
            int searchIdValue;
            int.TryParse(searchId, out searchIdValue);

            var result = new TopicSearchResults();

            if (!string.IsNullOrWhiteSpace(forumIds))
                forumIds = forumIds.Replace(';', ':');

            var ds = ActiveForums.DataProvider.Instance().Search(portalId, moduleId, userId, searchIdValue, rowIndex, maxRows, searchText, 0, 0, 0, 0, null, forumIds, null, 0, 0, 1, mainSettings.FullText);

            if(ds.Tables.Count > 2)
                return null;

            var dtSummary = ds.Tables[0];
            var dtResults = ds.Tables[1];

            result.SearchId = dtSummary.Rows[0].GetInt("SearchId");
            result.TotalTopics = dtSummary.Rows[0].GetInt("TotalRecords");
            result.Topics = new List<ForumTopic>(dtResults.Rows.Count);

            foreach(var row in dtResults.AsEnumerable())
            {
                ((List<ForumTopic>)result.Topics).Add(new ForumTopic
                                                          {
                                                            ForumId = row.GetInt("ForumId"),
                                                            ForumName = row.GetString("ForumName"),
                                                            LastReplyId = row.GetInt("LastReplyId"),
                                                            TopicId = row.GetInt("TopicId"),
                                                            ViewCount = row.GetInt("ViewCount"),
                                                            ReplyCount = row.GetInt("ReplyCount"),
                                                            IsLocked = row.GetBoolean("IsLocked"),
                                                            IsPinned = row.GetBoolean("IsPinned"),
                                                            TopicIcon = row.GetString("TopicIcon"),
                                                            StatusId = row.GetInt("StatusId"),
                                                            AnnounceStart = row.GetDateTime("AnnounceStart"),
                                                            AnnounceEnd = row.GetDateTime("AnnounceEnd"),
                                                            TopicType = row.GetString("TopicType"),
                                                            Subject = row.GetString("Subject"),
                                                            Summary = row.GetString("Summary"),
                                                            AuthorId = row.GetInt("AuthorId"),
                                                            AuthorName = row.GetString("AuthorName"),
                                                            Body = row.GetString("Body"),
                                                            LastReplyBody = row.GetString("LastReplyBody"),
                                                            DateCreated = row.GetDateTime("DateCreated"),
                                                            AuthorUserName = row.GetString("AuthorUserName"),
                                                            AuthorFirstName = row.GetString("AuthorFirstName"),
                                                            AuthorLastName = row.GetString("AuthorLastName"),
                                                            AuthorDisplayName = row.GetString("AuthorDisplayName"),
                                                            LastReplySubject = row.GetString("LastReplySubject"),
                                                            LastReplySummary = row.GetString("LastReplySummary"),
                                                            LastReplyAuthorId = row.GetInt("LastReplyAuthorId"),
                                                            LastReplyAuthorName = row.GetString("LastReplyAuthorName"),
                                                            LastReplyUserName = row.GetString("LastReplyUserName"),
                                                            LastReplyFirstName = row.GetString("LastReplyFirstName"),
                                                            LastReplyLastName = row.GetString("LastReplyLastName"),
                                                            LastReplyDisplayName = row.GetString("LastReplyDisplayName"),
                                                            LastReplyDate = row.GetDateTime("LastReplyDate"),
                                                            UserLastReplyRead = row.GetInt("UserLastReplyRead"),
                                                            UserLastTopicRead = row.GetInt("UserLastTopicRead"),
                                                            SubscriptionType = row.GetInt("SubscriptionType")
                                                          });
            }

            return result;
        }

        public PostSearchResults SearchPosts(int portalId, int moduleId, int userId, string forumIds, string searchText, int rowIndex, int maxRows, string searchId, SettingsInfo mainSettings)
        {
            int searchIdValue;
            int.TryParse(searchId, out searchIdValue);

            var result = new PostSearchResults();

            if (!string.IsNullOrWhiteSpace(forumIds))
                forumIds = forumIds.Replace(';', ':');

            var ds = ActiveForums.DataProvider.Instance().Search(portalId, moduleId, userId, searchIdValue, rowIndex, maxRows, searchText, 0, 0, 0, 0, null, forumIds, null, 1, 0, 1, mainSettings.FullText);

            if (ds.Tables.Count > 2)
                return null;

            var dtSummary = ds.Tables[0];
            var dtResults = ds.Tables[1];

            result.SearchId = dtSummary.Rows[0].GetInt("SearchId");
            result.TotalPosts = dtSummary.Rows[0].GetInt("TotalRecords");
            result.Topics = new List<ForumPost>(dtResults.Rows.Count);

            foreach (var row in dtResults.AsEnumerable())
            {
                ((List<ForumPost>)result.Topics).Add(new ForumPost
                {
                    ForumId = row.GetInt("ForumId"),
                    ForumName = row.GetString("ForumName"),
                    TopicId = row.GetInt("TopicId"),
                    ReplyId = row.GetInt("ReplyId"),
                    ContentId = row.GetInt("ContentId"),
                    Subject = row.GetString("Subject"),
                    PostSubject = row.GetString("PostSubject"),
                    Summary = row.GetString("Summary"),
                    AuthorId = row.GetInt("AuthorId"),
                    AuthorName = row.GetString("AuthorName"),
                    UserName = row.GetString("AuthorUserName"),
                    FirstName = row.GetString("AuthorFirstName"),
                    LastName = row.GetString("AuthorLastName"),
                    DisplayName = row.GetString("AuthorDisplayName"),
                    Body = row.GetString("Body"),
                    DateCreated = row.GetDateTime("DateCreated"),
                    DateUpdated = row.GetDateTime("DateCreated")
                });
            }

            return result;
        }

        public UserInfo GetUser(int portalId, int userId, int currentUserId)
        {
            UserInfo result;

            using (var ctx = DataContext.Instance())
            {
                result = ctx.ExecuteSingleOrDefault<UserInfo>(CommandType.StoredProcedure, "activeforumstapatalk_Users_Get", portalId, userId, currentUserId);
            }

            return result;
        }

        //DotNetNuke.Services.Social.Messaging.Internal.InternalMessagingController.Instance.GetInbox()


    }
}