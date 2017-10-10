namespace TogglToTimesheet.DTO
{
    using System;

    public class ResourceAssignment
    {
        public string ProjectName { get; set; }
        public Guid ProjectUid { get; set; }

        public string TaskName { get; set; }
        public Guid TaskUid { get; set; }

        public string Tag => $"PS: {TaskName} > {ProjectName}";
        public Guid AssignmentUid { get; set; }
        public DateTime End { get; set; }
    }
}
