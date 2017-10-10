namespace TogglToTimesheet
{
    using System;
    using Converters;
    using Data;
    using DTO;
    using Repository;

    public static class Timesheetify
    {
	    public static string CurrentAccountName;

        public static TogglToTimesheetResult UpdateTimesheet(string accountName, DateTime? startDate = null)
        {
	        CurrentAccountName = accountName;

            var worker = WorkerRepository.GetCurrentWorker();

            CheckAPIKey(worker);

            var toggl = new Toggl(worker.TogglApiKey);

            var entries = toggl.GetWeekEntries(startDate)
                .ToTimesheetEntries()
                .MergeSameDayEntries();

            Timesheet.FillWeek(entries, startDate);

            return new TogglToTimesheetResult(entries.Length);
        }

        public static TimesheetToTogglResult UpdateToggl(string accountName)
        {
	        CurrentAccountName = accountName;

			var worker = WorkerRepository.GetCurrentWorker();

            CheckAPIKey(worker);

            var items = Timesheet.GenerateTogglData(accountName);

            WorkerRepository.SaveWorkerAssignments(items, worker);

            return new Toggl(worker.TogglApiKey).SyncProjectsAndTags(items.ToTogglProjectsAndTags(), false);
        }

        private static void CheckAPIKey(Worker worker)
        {
            if (string.IsNullOrEmpty(worker?.TogglApiKey))
                throw new Exception("Toggl API key not found");
        }
    }
}
