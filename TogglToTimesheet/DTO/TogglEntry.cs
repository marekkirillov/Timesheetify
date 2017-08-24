using System;

namespace TogglToTimesheet.DTO
{
    public class TogglEntry
    {
        public string id { get; set; }
        public string wid { get; set; }
        public string pid { get; set; }
        public bool billable { get; set; }
        public DateTime start { get; set; }
        public DateTime stop { get; set; }
        public string duration { get; set; }
        public string description { get; set; }
        public string[] tags { get; set; }
        public DateTime at { get; set; }
        public TogglProject TogglProject { get; set; }
    }
}
