﻿using FitnessAppAPI.Common;
using FitnessAppAPI.Data.Models;
using FitnessAppAPI.Data.Services.Notifications.Models;
using FitnessAppAPI.Data.Services.Teams.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net;

namespace FitnessAppAPI.Data.Services.Notifications
{
    /// <summary>
    ///     Notification service class to implement INotificationService interface.
    /// </summary>
    public class NotificationService(FitnessAppAPIContext DB) : BaseService(DB), INotificationService
    {

        public async Task<ServiceActionResult<BaseModel>> AddTeamInviteNotification(string receiverUserId, string senderUserId, long teamId)
        {
            var teamName = await DBAccess.Teams.Where(t => t.Id == teamId).Select(t => t.Name).FirstOrDefaultAsync();

            // Must not happen
            teamName ??= "Unknown";

            // Create the invite to team notification
            var notification = new Notification
            {
                NotificationType = Constants.NotificationType.INVITED_TO_TEAM.ToString(),
                ReceiverUserId = receiverUserId,
                SenderUserId = senderUserId,
                NotificationText = string.Format(Constants.DBConstants.InviteToTeamNotification, teamName),
                DateTime = DateTime.Now,
                IsActive = true,
                TeamId = teamId
            };

            return await AddNotification(notification);
        }

        public async Task<ServiceActionResult<BaseModel>> AddAcceptedDeclinedNotification(string senderUserId, long teamId, string notificationType)
        {
            var notificationText = "";
            var senderName = await DBAccess.UserProfiles.Where(u => u.UserId == senderUserId).Select(t => t.FullName).FirstOrDefaultAsync();

            // Must not happen
            senderName ??= "Unknown user";

            // Find the notification receiver id
            var team = await DBAccess.Teams.Where(t => t.Id == teamId).FirstOrDefaultAsync();
            if (team == null)
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.NotFound, Constants.MSG_FAILED_TO_TEAM_OWNER);
            }

            if (notificationType == Constants.NotificationType.JOINED_TEAM.ToString()) 
            {
                notificationText = string.Format(Constants.DBConstants.AcceptTeamInvitationNotification, senderName, team.Name);
            } 
            else
            {
                notificationText = string.Format(Constants.DBConstants.DeclineTeamInvitationNotification, senderName, team.Name);
            }

            var notification = new Notification
            {
                NotificationType = notificationType,
                ReceiverUserId = team.UserId,
                SenderUserId = senderUserId,
                NotificationText = notificationText,
                DateTime = DateTime.Now,
                IsActive = true,
                TeamId = teamId
            };

            return await AddNotification(notification);
        }

        public async Task<ServiceActionResult<BaseModel>> UpdateNotification(Dictionary<string, string> requestData, bool isActive)
        {
            // Check if the neccessary data is provided
            if (!requestData.TryGetValue("id", out string? idString))
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.BadRequest, Constants.MSG_FAILED_TO_GET_NOTIFICATION_DETAILS);
            }

            if (!long.TryParse(idString, out long id))
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.BadRequest, Constants.MSG_FAILED_TO_GET_NOTIFICATION_DETAILS);
            }

            return await UpdateNotification(id, isActive);
        }

        public async Task<ServiceActionResult<BaseModel>> UpdateNotification(long id, bool isActive)
        {
            var notification = await DBAccess.Notifications.Where(n => n.Id == id).FirstOrDefaultAsync();
            if (notification == null)
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.NotFound, Constants.MSG_FAILED_TO_GET_NOTIFICATION_DETAILS);
            }

            notification.IsActive = isActive;
            DBAccess.Entry(notification).State = EntityState.Modified;
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<BaseModel>(HttpStatusCode.OK);
        }

        public async Task<ServiceActionResult<BaseModel>> DeleteNotification(long notificationId, string userId)
        {
            // Check if the neccessary data is provided
            if (notificationId <= 0)
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.BadRequest, Constants.MSG_DELETE_NOTIFICATION_FAILED);
            }

            var notification = await DBAccess.Notifications.Where(n => n.Id == notificationId).FirstOrDefaultAsync();
            if (notification == null)
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.NotFound, Constants.MSG_DELETE_NOTIFICATION_FAILED);
            }

            if (notification.IsActive && notification.NotificationType == Constants.NotificationType.INVITED_TO_TEAM.ToString())
            { 
                // If the user is deleting INVITED_TO_TEAM notification, remove the record from TeamMembers
                var record = await DBAccess.TeamMembers.Where(tm => tm.UserId == userId && 
                                                              tm.TeamId == notification.TeamId && 
                                                              tm.State == Constants.MemberTeamState.INVITED.ToString())
                                                        .FirstOrDefaultAsync();

                if (record != null)
                {
                    // Remove the record and send notification to the team owner
                    DBAccess.TeamMembers.Remove(record);
                    await AddAcceptedDeclinedNotification(userId, record.TeamId, Constants.NotificationType.DECLINED_TEAM_INVITATION.ToString());
                }
            }

            // Delete the notification
            DBAccess.Notifications.Remove(notification);
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<BaseModel>(HttpStatusCode.OK, Constants.MSG_NOTIFICATION_DELETED);
        }

        public async Task<ServiceActionResult<BaseModel>> DeleteNotifications(TeamMemberModel data)
        {
            // Find all notifications related to the deleted TeamMember record
            var notifications = await DBAccess.Notifications.Where(n => n.TeamId == data.TeamId && 
                                                                  (n.SenderUserId == data.UserId || n.ReceiverUserId == data.UserId))
                                                            .ToListAsync();

            if (notifications.Count > 0) {
                DBAccess.RemoveRange(notifications);
                await DBAccess.SaveChangesAsync();
            }

            return new ServiceActionResult<BaseModel>(HttpStatusCode.OK);  
        }

        public async Task<ServiceActionResult<NotificationModel>> GetNotifications(string userId)
        {
            var notifcationModels = new List<NotificationModel>();
            var notifications = await DBAccess.Notifications.Where(n => n.ReceiverUserId == userId)
                                                             .OrderByDescending(n => n.DateTime)
                                                             .ToListAsync();

            foreach (var n in notifications) {
                notifcationModels.Add(await ModelMapper.MapToNotificationModel(n, DBAccess));    
            }

            return new ServiceActionResult<NotificationModel>(HttpStatusCode.OK, Constants.MSG_SUCCESS, notifcationModels);
        }

        public async Task<bool> HasNotification(string userId)
        {
            var notification = await DBAccess.Notifications.Where(n => n.ReceiverUserId == userId && n.IsActive).FirstOrDefaultAsync();

            return notification != null;
        }

        private async Task<ServiceActionResult<BaseModel>> AddNotification(Notification data)
        {
            DBAccess.Notifications.Add(data);
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<BaseModel>(HttpStatusCode.Created);
        }

        public async Task<ServiceActionResult<JoinTeamNotificationModel>> GetJoinTeamNotificationDetails(long notificationId)
        {
            // Check if the neccessary data is provided
            if (notificationId <= 0)
            {
                return new ServiceActionResult<JoinTeamNotificationModel>(HttpStatusCode.BadRequest, Constants.MSG_FAILED_TO_GET_NOTIFICATION_DETAILS);
            }

            var formattedDate = "";
            var description = "";
            var teamModel = ModelMapper.GetEmptyTeamModel();

            var notification = await DBAccess.Notifications.Where(n => n.Id == notificationId).FirstOrDefaultAsync();

            if (notification == null)
            {
                return new ServiceActionResult<JoinTeamNotificationModel>(HttpStatusCode.NotFound, Constants.MSG_FAILED_TO_GET_NOTIFICATION_DETAILS);
            }

            formattedDate = Utils.FormatDefaultDateTime(notification.DateTime);

            var team = await DBAccess.Teams.Where(t => t.Id == notification.TeamId).FirstOrDefaultAsync();
            if (team != null)
            {
                teamModel.Name = team.Name;
                teamModel.Image = Utils.EncodeByteArrayToBase64Image(team.Image);
                teamModel.Id = team.Id;
            }

            var sender = await DBAccess.UserProfiles.Where(p => p.UserId == notification.SenderUserId).FirstOrDefaultAsync();
            if (sender != null)
            {
                if (!string.IsNullOrWhiteSpace(sender.FullName))
                {
                    // Use sender name
                    description = string.Format(Constants.NotificationText.AskTeamInviteAccept, sender.FullName, formattedDate);
                } 
                else
                {
                    // Do not use sender name
                    description = string.Format(Constants.NotificationText.AskTeamInviteAcceptNoSender, formattedDate);
                }
            }

            var model = new JoinTeamNotificationModel
            {
                Id = notificationId,
                TeamName = teamModel.Name,
                Description = description,
                TeamImage = teamModel.Image,
                NotificationType = Constants.NotificationType.INVITED_TO_TEAM.ToString(),
                TeamId = teamModel.Id
            };

            return new ServiceActionResult<JoinTeamNotificationModel>(HttpStatusCode.OK, Constants.MSG_SUCCESS, [model]);
        }
    }
}
