namespace TogglToTimesheet.DTO
{
    [Newtonsoft.Json.JsonObject(Title = "data")]

    public class TogglTag
    {
        public string id { get; set; }
        public string wid { get; set; }
        public string name { get; set; }

    }
}
