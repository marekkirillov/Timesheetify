namespace TogglToTimesheet.DTO
{
    using System;
    using System.Linq;
    using Common;

    public class TimesheetEntry
    {
        public string Project { get; set; }
        public string Tag { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public Decimal Duration { get; set; }

        public string Comment { get; set; }

	    public string Task => Tag.Replace("PS:", "").Split(new[] {">"}, StringSplitOptions.RemoveEmptyEntries).First().Trim();

        public bool IsValid =>
            Tag != null &&
            Project != null &&
            Task != null &&
            End > Start &&
            Duration > 0;

        public bool IsAdministrative => Project.Equals(Constants.AdministrativeWork);
    }
}
