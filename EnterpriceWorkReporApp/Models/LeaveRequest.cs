using System;

namespace EnterpriseWorkReport.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EmployeeName { get; set; }
        public string LeaveType { get; set; } // Casual, Sick, Paid, Unpaid
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    }
}
