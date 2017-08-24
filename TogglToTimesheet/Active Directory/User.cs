namespace NG.Timesheetify.Common.Active_Directory
{
    public class User
    {
        public string DisplayName { get; set; }
        public string AccountName { get; set; }
        public string Password { get; set; }

        public bool UseDefaultCredentials { get; set; }
    }
}
