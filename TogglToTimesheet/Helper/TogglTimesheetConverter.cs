namespace TogglToTimesheet.Converters
{
   using System;
   using System.Linq;
   using Common;
   using DTO;

   public static class TogglTimesheetConverter
   {
      public static TimesheetEntry[] ToTimesheetEntries(this TogglEntry[] entries)
      {
         return entries?.Where(e => e != null).Select(Convert).ToArray();
      }

      public static TimesheetEntry[] MergeSameDayEntries(this TimesheetEntry[] entries)
      {
         return entries?.Where(c => c.IsValid)
            .GroupBy(c => new
            {
               c.Project,
               c.Start.Date.DayOfWeek,
               c.TaskIdentifier
            })
            .Select(gcs => new TimesheetEntry
            {
               Project = gcs.Key.Project,
               Start = gcs.Min(v => v.Start),
               End = gcs.Max(v => v.End),
               Duration = new TimeSpan(gcs.Sum(v => v.Duration.Ticks)),
               TaskHierarchy = gcs.First().TaskHierarchy,
               Comment = gcs.First().Comment
            }).ToArray();
      }

      private static TimesheetEntry Convert(TogglEntry entry)
      {
         return new TimesheetEntry
         {
            Start = entry.start,
            End = entry.stop,
            Duration = GetDuration(entry),
            Project = entry.TogglProject?.name,
            TaskHierarchy = ParseHierarchy(entry),
            TaskIdentifier = GetTag(entry),
            Comment = entry.description
         };
      }

      private static TimeSpan GetDuration(TogglEntry entry)
      {
         var durationInSeconds = int.Parse(entry.duration);

         if (durationInSeconds < 0)
            return DateTime.Now - entry.start;

         return new TimeSpan(0, 0, durationInSeconds);
      }

      private static string[] ParseHierarchy(TogglEntry entry)
      {
         var tag = GetTag(entry);

         return tag?.Replace("PS:", "")
             .Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries)
             .Select(s => s.Trim())
             .ToArray();
      }

      private static string GetTag(TogglEntry entry)
      {
         return entry.tags?.FirstOrDefault(t => t.StartsWith(Constants.ProjectServerTagPrefix));
      }
   }
}
