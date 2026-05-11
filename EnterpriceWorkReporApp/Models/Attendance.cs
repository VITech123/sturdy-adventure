using System;

namespace EnterpriseWorkReport.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } // Present, Absent, Half Day, Leave
        public string Remarks { get; set; }
        
        // Clock In/Out times
        public string ClockInTime { get; set; }
        public string ClockOutTime { get; set; }
        public double HoursWorked { get; set; }
        
        // Late/Early flags (computed from settings)
        public bool IsLateArrival { get; set; }
        public bool IsEarlyDeparture { get; set; }

        // Computed helpers
        public string HoursWorkedDisplay => HoursWorked > 0 ? $"{HoursWorked:F1}h" : (ClockInTime != null && ClockOutTime == null ? "In Progress" : "-");
        public string StatusDisplay => GetStatusDisplay();

        private string GetStatusDisplay()
        {
            string status = Status ?? "";
            if (IsLateArrival && !string.IsNullOrEmpty(ClockInTime))
                status += " (Late)";
            if (IsEarlyDeparture && !string.IsNullOrEmpty(ClockOutTime))
                status += " (Early)";
            return status;
        }
    }
}
