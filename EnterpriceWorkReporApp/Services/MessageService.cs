using System;
using System.Collections.Generic;
using System.IO;
using Dapper;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    public class MessageService
    {
        public static string AttachmentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", "attachments");

        public static void EnsureAttachmentsFolder()
        {
            if (!Directory.Exists(AttachmentsFolder))
                Directory.CreateDirectory(AttachmentsFolder);
        }

        public List<Message> GetInbox(int userId)
        {
            EnsureAttachmentsFolder();
            using (var conn = DatabaseService.GetConnection())
            {
                var sql = @"
                    SELECT m.*, 
                           s.FullName as SenderName,
                           s.ProfilePicture as SenderProfilePicture,
                           r.FullName as ReceiverName
                    FROM Messages m
                    LEFT JOIN Users s ON m.SenderId = s.Id
                    LEFT JOIN Users r ON m.ReceiverId = r.Id
                    WHERE m.ReceiverId = @UserId OR m.IsBroadcast = 1
                    ORDER BY m.CreatedAt DESC";
                return conn.Query<Message>(sql, new { UserId = userId }).AsList();
            }
        }

        public List<Message> GetSentMessages(int userId)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var sql = @"
                    SELECT m.*, 
                           s.FullName as SenderName,
                           s.ProfilePicture as SenderProfilePicture,
                           r.FullName as ReceiverName
                    FROM Messages m
                    LEFT JOIN Users s ON m.SenderId = s.Id
                    LEFT JOIN Users r ON m.ReceiverId = r.Id
                    WHERE m.SenderId = @UserId
                    ORDER BY m.CreatedAt DESC";
                return conn.Query<Message>(sql, new { UserId = userId }).AsList();
            }
        }

        public List<Message> GetUnreadMessages(int userId)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var sql = @"
                    SELECT m.*, 
                           s.FullName as SenderName,
                           s.ProfilePicture as SenderProfilePicture
                    FROM Messages m
                    LEFT JOIN Users s ON m.SenderId = s.Id
                    WHERE (m.ReceiverId = @UserId OR m.IsBroadcast = 1) AND m.IsRead = 0
                    ORDER BY m.CreatedAt DESC";
                return conn.Query<Message>(sql, new { UserId = userId }).AsList();
            }
        }

        public int GetUnreadCount(int userId)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var sql = @"SELECT COUNT(*) FROM Messages WHERE (ReceiverId = @UserId OR IsBroadcast = 1) AND IsRead = 0";
                return (int)conn.ExecuteScalar<long>(sql, new { UserId = userId });
            }
        }

        public void SendMessage(int senderId, int? receiverId, string subject, string content, string attachmentPath = null, bool isBroadcast = false)
        {
            EnsureAttachmentsFolder();
            
            string savedPath = null;
            string attachmentType = null;
            
            // Save attachment if provided
            if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
            {
                var fileName = Guid.NewGuid() + "_" + Path.GetFileName(attachmentPath);
                savedPath = Path.Combine(AttachmentsFolder, fileName);
                File.Copy(attachmentPath, savedPath, true);
                attachmentType = Path.GetExtension(attachmentPath).ToLower();
            }

            using (var conn = DatabaseService.GetConnection())
            {
                var sql = @"INSERT INTO Messages (SenderId, ReceiverId, Subject, Content, AttachmentPath, AttachmentType, IsBroadcast) 
                           VALUES (@SenderId, @ReceiverId, @Subject, @Content, @AttachmentPath, @AttachmentType, @IsBroadcast)";
                conn.Execute(sql, new { 
                    SenderId = senderId, 
                    ReceiverId = receiverId, 
                    Subject = subject ?? "", 
                    Content = content, 
                    AttachmentPath = savedPath,
                    AttachmentType = attachmentType,
                    IsBroadcast = isBroadcast ? 1 : 0
                });
                
                AuditService.Log("Message Sent", $"To: {(receiverId?.ToString() ?? "All")}, Subject: {subject}");
            }
        }

        public void MarkAsRead(int messageId)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                conn.Execute("UPDATE Messages SET IsRead = 1 WHERE Id = @Id", new { Id = messageId });
            }
        }

        public void MarkAllAsRead(int userId)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                conn.Execute("UPDATE Messages SET IsRead = 1 WHERE ReceiverId = @UserId OR IsBroadcast = 1", new { UserId = userId });
            }
        }

        public void DeleteMessage(int messageId)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                // Get attachment path before deleting
                var msg = conn.QueryFirstOrDefault<Message>("SELECT * FROM Messages WHERE Id = @Id", new { Id = messageId });
                if (msg != null && !string.IsNullOrEmpty(msg.AttachmentPath) && File.Exists(msg.AttachmentPath))
                {
                    File.Delete(msg.AttachmentPath);
                }
                
                conn.Execute("DELETE FROM Messages WHERE Id = @Id", new { Id = messageId });
                AuditService.Log("Message Deleted", $"Message ID: {messageId}");
            }
        }
    }
}
