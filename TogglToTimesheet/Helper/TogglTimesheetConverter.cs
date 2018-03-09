namespace TogglToTimesheet.Converters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Common;
	using DTO;

	public static class TogglTimesheetConverter
	{
		public static TimesheetEntry[] ToTimesheetEntries(this TogglEntry[] entries)
		{
			return entries?.Where(e => e != null).Select(Convert).Where(e => e != null).ToArray();
		}

		public static TimesheetEntry[] MergeSameDayEntries(this TimesheetEntry[] entries)
		{
			return entries?.Where(c => c.IsValid)
			   .GroupBy(c => new
			   {
				   c.Project,
				   c.Start.Date.DayOfWeek,
				   c.Tag
			   })
			   .Select(gcs => new TimesheetEntry
			   {
				   Project = gcs.Key.Project,
				   Start = gcs.Min(v => v.Start),
				   End = gcs.Max(v => v.End),
				   Duration = gcs.Sum(v => v.Duration),
				   Comment = string.Join(", ", gcs.Select(t => t.Comment).Distinct()),
				   Tag = gcs.First().Tag
			   }).ToArray();
		}

		private static TimesheetEntry Convert(TogglEntry entry)
		{
			var item = new TimesheetEntry
			{
				Start = entry.start,
				Duration = Math.Round((decimal)GetDuration(entry).TotalHours, 2),
				Project = entry.TogglProject?.name,
				Tag = GetTag(entry),
				Comment = entry.description
			};

			if (item.Tag == null) return null;

			if (string.IsNullOrEmpty(item.Project))
				throw new InvalidOperationException($"Missing project on entry starting at {item.Start} with tag {item.Tag}");

			if (!item.Tag.Contains(item.Project))
				throw new InvalidOperationException($"Mismatching tag and project between {item.Tag} and {item.Project} on Toggl entry starting at {item.Start}");

			item.End = entry.stop == DateTime.MinValue ? item.End = item.Start.Add(GetDuration(entry)) : entry.stop;

			return item;
		}

		private static TimeSpan GetDuration(TogglEntry entry)
		{
			var durationInSeconds = int.Parse(entry.duration);

			if (durationInSeconds < 0)
				return DateTime.Now - entry.start;

			return new TimeSpan(0, 0, durationInSeconds);
		}

		private static string GetTag(TogglEntry entry)
		{
			return entry.tags?.FirstOrDefault(t => t.StartsWith(Constants.ProjectServerTagPrefix));
		}

		public static TogglProjectsAndTags ToTogglProjectsAndTags(this List<ResourceAssignment> resourceAssignments)
		{
			var togglProjectsAndTags = new TogglProjectsAndTags();

			foreach (var resourceAssignment in resourceAssignments)
			{
				togglProjectsAndTags.AddProjects(resourceAssignment.ProjectName);
				togglProjectsAndTags.AddTags(resourceAssignment.Tag);
			}

			return togglProjectsAndTags;
		}
	}
}
