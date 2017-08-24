namespace TogglToTimesheet.DTO
{
    using System;

    public class TimesheetEntry
    {
        public string Project { get; set; }
        public string[] TaskHierarchy { get; set; }
        public string TaskIdentifier { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Duration { get; set; }

        public string Comment { get; set; }

        public string Tag => string.Join(" > ", TaskHierarchy);

        public bool IsValid =>
            TaskIdentifier != null &&
            Project != null &&
            TaskHierarchy != null &&
            End > Start &&
            Duration > TimeSpan.MinValue;
    }
}
