namespace TogglToTimesheet
{
	using System;
	using Converters;
	using Data;
	using DTO;
	using Repository;

	public static class Timesheetify
	{
		public static TogglToTimesheetResult UpdateTimesheet(string accountName, DateTime? startDate, Guid? approver)
		{
			var worker = WorkerRepository.GetCurrentWorker(accountName);

			if (approver.HasValue) WorkerRepository.UpdateApprover(accountName, approver.Value);

			CheckAPIKey(worker);

			var toggl = new Toggl(worker.TogglApiKey, worker.WorkspaceName);

			var entries = toggl.GetWeekEntries(startDate)
				.ToTimesheetEntries()
				.MergeSameDayEntries();

			var message = Timesheet.FillWeek(accountName, entries, startDate, approver);

			return new TogglToTimesheetResult(entries.Length, message);
		}

		public static TimesheetToTogglResult UpdateToggl(string accountName)
		{

			var worker = WorkerRepository.GetCurrentWorker(accountName);

			CheckAPIKey(worker);

			var items = Timesheet.GenerateTogglData(accountName);

			WorkerRepository.SaveWorkerAssignments(items, worker);

			return new Toggl(worker.TogglApiKey, worker.WorkspaceName).SyncProjectsAndTags(items.ToTogglProjectsAndTags(), worker.Cleanup);
		}

		private static void CheckAPIKey(Worker worker)
		{
			if (string.IsNullOrEmpty(worker?.TogglApiKey))
				throw new Exception("Toggl API key not found");
		}
	}
}
