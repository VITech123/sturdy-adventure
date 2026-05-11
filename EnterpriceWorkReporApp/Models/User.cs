using System;

namespace EnterpriseWorkReport.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Extended profile fields
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Place { get; set; }
        public string Education { get; set; }
        public string EmergencyNumber { get; set; }
        public DateTime? JoinDate { get; set; }
        public DateTime? RelievingDate { get; set; }
        
        public string Department { get; set; }
        public string Designation { get; set; }
        public string ProfilePicture { get; set; }
        public string WallpaperPath { get; set; }
        public string SelectedTheme { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }
    }
}
