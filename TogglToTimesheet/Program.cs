namespace TogglToTimesheet
{
    using System;
    using System.DirectoryServices.AccountManagement;
    using System.Linq;
    using Active_Directory;
    using Converters;
    using NG.Timesheetify.Common.Active_Directory;

    public static class Program
    {
        static void Main(string[] args)
        {
            var command = args.Any() ? args[0] : null;
            var user = AdUserProvider.GetUserByAccountName(UserPrincipal.Current.SamAccountName);
            user.UseDefaultCredentials = true;
            var task = "";
            if (command == "-ts")
            {
                UpdateTimesheet(TimesheetifyCore.GetKey(), user);
                return;
            }

            if (command == "-tl")
            {
                UpdateToggl(TimesheetifyCore.GetKey(), user);
                return;
            }

            TimesheetifyCore.Install();
        }

        public static int UpdateTimesheet(string toggleKey, User user)
        {
            var toggl = new Toggl(toggleKey);

            var entries = toggl.GetCurrentWeekEntries()
                .ToTimesheetEntries()
                .MergeSameDayEntries();

            Timesheet.FillCurrentWeek(entries, user);

            return entries.Length;
        }

        public static Tuple<int, int> UpdateToggl(string toggleKey, User user)
        {
            var items = Timesheet.GenerateTogglData(user);
            return new Toggl(toggleKey).SyncProjectsAndTags(items);
        }
    }
}
