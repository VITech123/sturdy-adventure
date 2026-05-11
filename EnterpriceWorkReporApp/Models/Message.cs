using System;

namespace EnterpriseWorkReport.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public string Subject { get; set; }
        public string Content { get; set; }
        public string AttachmentPath { get; set; }
        public string AttachmentType { get; set; }
        public bool IsRead { get; set; }
        public bool IsBroadcast { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // For display purposes
        public string SenderName { get; set; }
        public string SenderProfilePicture { get; set; }
        public string ReceiverName { get; set; }
    }
}
