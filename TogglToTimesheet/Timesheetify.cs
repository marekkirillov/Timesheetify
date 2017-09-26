namespace TogglToTimesheet
{
    using System;
    using Converters;
    using DTO;
    using NG.Timesheetify.Common.Active_Directory;

    public static class Timesheetify
    {
		public static int UpdateTimesheet(string toggleKey, User user, DateTime? startDate = null)
        {
            var toggl = new Toggl(toggleKey);

            var entries = toggl.GetWeekEntries(startDate)
                .ToTimesheetEntries()
                .MergeSameDayEntries();

            Timesheet.FilltWeek(entries, user, startDate);

            return entries.Length;
        }

        public static TimesheetToTogglResult UpdateToggl(string toggleKey, User user, bool cleanup)
        {
            var items = Timesheet.GenerateTogglData(user);
            return new Toggl(toggleKey).SyncProjectsAndTags(items, cleanup);
        }
    }
}
