using System;

namespace TogglToTimesheet.DTO
{
    public class TogglProject
    {
        public string id { get; set; }
        public string wid { get; set; }
        public string cid { get; set; }
        public string name { get; set; }
        public bool billable { get; set; }
        public bool is_private { get; set; }
        public bool active { get; set; }
        public DateTime at { get; set; }
        public string color { get; set; }
    }
}
