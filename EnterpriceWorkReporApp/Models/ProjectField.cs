namespace EnterpriseWorkReport.Models
{
    public class ProjectField
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string InternalFieldName { get; set; }
        public string FieldLabel { get; set; }
        public string FieldType { get; set; } // "Text" or "Number"
        public bool IsRequired { get; set; }
        public bool IncludeInBilling { get; set; }
        public string Instructions { get; set; }
        public int SortOrder { get; set; }
    }
}
