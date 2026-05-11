using System;

namespace EnterpriseWorkReport.Models
{
    public class ResumeFolder
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; } // e.g. "📁" or FontAwesome icon code
        
        // Helper for display
        public string DisplayName => $"{Icon ?? "📁"} {Name}";
        public int DocumentCount { get; set; }
    }
}
