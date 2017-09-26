namespace TogglToTimesheet
{
   using System;
   using System.Linq;
   using System.Net;
   using System.Text;
   using System.Threading;
   using Common;
   using DTO;
   using Microsoft.ProjectServer.Client;
   using Microsoft.SharePoint.Client;
   using User = NG.Timesheetify.Common.Active_Directory.User;

   public static class Timesheet
   {
      public static string ItemInProgress;

      public static void FilltWeek(TimesheetEntry[] timesheetEntries, User user, DateTime? startDate = null)
      {
         using (var projectContext = new ProjectContext(Constants.PwaPath))
         {
            if (!user.UseDefaultCredentials)
               projectContext.Credentials = new NetworkCredential(user.AccountName, user.Password);

            if (timesheetEntries == null)
               return;

            var firstDayOfWeek = startDate ?? GetFirstDayOfWeek();

            LoadInitialData(projectContext, firstDayOfWeek);

            var entriesPerProject = timesheetEntries.GroupBy(t => t.Project);

            var weeklyTimesheet = GetTimeSheetPeriod(projectContext, firstDayOfWeek);

            foreach (var projectGroup in entriesPerProject)
            {
               if (projectGroup == null)
                  throw new ArgumentNullException("projectGroup");

               if (projectGroup.Key == Constants.AdministrativeWork)
               {
                  foreach (var entry in projectGroup)
                  {
                     ItemInProgress = entry.Tag;
                     var timesheetLine = GetAdministrativeTimeSheetLine(weeklyTimesheet, entry, projectContext, user);

                     if (timesheetLine == null)
                        continue;

                     AddWork(entry, timesheetLine, projectContext);
                  }

                  continue;
               }

               var project = projectContext.Projects.FirstOrDefault(p => p.Name.Equals(projectGroup.Key));

               if (project == null)
                  throw new Exception($"Could not find project {projectGroup.Key} from project server");

               LoadMyAssignments(projectContext, project, user.DisplayName);

               foreach (var entry in projectGroup)
               {
                  ItemInProgress = entry.Tag;
                  var assignment = GetPublishedAssignment(project, entry);
                  var timesheetLine = GetTimeSheetLine(weeklyTimesheet, assignment, project, projectContext);

                  AddWork(entry, timesheetLine, projectContext);
               }
            }

            projectContext.ExecuteQuery();
         }
      }

      public static TogglProjectsAndTags GenerateTogglData(User user)
      {
         var result = new TogglProjectsAndTags();
         var startDate = DateTime.Now.AddMonths(-18);
         var endDate = DateTime.Now.AddDays(-30);

         using (var projectContext = new ProjectContext(Constants.PwaPath))
         {
            if (!user.UseDefaultCredentials)
               projectContext.Credentials = new NetworkCredential(user.AccountName, user.Password);

            projectContext.Load(projectContext.Projects, projects => projects.Where(p => (p.StartDate > startDate && p.FinishDate > endDate) || p.StartDate == p.FinishDate)
               .IncludeWithDefaultProperties(p => p.ProjectResources, pr => pr.Name));

            projectContext.ExecuteQuery();
            var myProjects = GetPublishedProjects(user, projectContext);

            foreach (var project in myProjects)
            {
               result.Projects.Add(project.Name);

               LoadMyAssignments(projectContext, project, user.DisplayName);

               foreach (var projectAssignment in project.Assignments)
               {
                  var tagBuilder = new StringBuilder("PS: ");

                  tagBuilder.Append(projectAssignment.Task.Name);

                  var isNull = projectAssignment.Task.Parent.ServerObjectIsNull;
                  if (isNull.HasValue && !isNull.Value)
                  {
                     tagBuilder.Append(" > ");
                     tagBuilder.Append(projectAssignment.Task.Parent.Name);
                  }

                  tagBuilder.Append(" > ");
                  tagBuilder.Append(project.Name);

                  result.AddTags(tagBuilder.ToString());
               }
            }

            AddAdministrativeTasks(result, user);
         }

         return result;
      }

      private static IQueryable<PublishedProject> GetPublishedProjects(User user, ProjectContext projectContext)
      {
         var myProjects =
            projectContext.Projects.Where(
               p => p.ProjectResources.Any(pr => pr.Name.Equals(user.DisplayName)));

	      var projectResources = projectContext.Projects.Select(p =>
		      p.Name + " - " + string.Join(";", p.ProjectResources.Select(pr => pr.Name)));

         return myProjects;
      }

      private static void AddAdministrativeTasks(TogglProjectsAndTags result, User user)
      {
         result.Projects.Add(Constants.AdministrativeWork);

         var rows = TimeSheetExtended.GetAdministrativeRows(user);

         foreach (var task in rows.Keys)
            result.AddTags($"PS: {task} > Administrative");
      }

      private static void LoadInitialData(ProjectContext projectContext, DateTime firstDayOfWeek)
      {
         var lastWeekLastDay = firstDayOfWeek.AddDays(-1);
         var nextWeekFirstDay = firstDayOfWeek.AddDays(7);

         projectContext.Load(projectContext.Projects);
         projectContext.Load(projectContext.TimeSheetPeriods,
             timesheetPeriods => timesheetPeriods
                 .IncludeWithDefaultProperties(
                     timesheetPeriod => timesheetPeriod.TimeSheet,
                     timesheetPeriod => timesheetPeriod.TimeSheet.Lines)
                 .Where(t => t.Start > lastWeekLastDay && t.End < nextWeekFirstDay));

         projectContext.ExecuteQuery();
      }

      private static TimeSheetPeriod GetTimeSheetPeriod(ProjectContext projectContext, DateTime firstDayOfWeek)
      {
         var weeklyTimesheet = projectContext.TimeSheetPeriods.First(t => t.Start.Date == firstDayOfWeek.Date);
         var createTimesheet = !weeklyTimesheet.TimeSheet.ServerObjectIsNull.HasValue || weeklyTimesheet.TimeSheet.ServerObjectIsNull.Value;

         if (createTimesheet)
         {
            weeklyTimesheet.CreateTimeSheet();
            projectContext.Load(weeklyTimesheet.TimeSheet);
            projectContext.ExecuteQuery();
         }

         projectContext.Load(weeklyTimesheet.TimeSheet.Lines);
         projectContext.ExecuteQuery();

         return weeklyTimesheet;
      }

      private static void LoadMyAssignments(ProjectContext projectContext, PublishedProject project, string name)
      {
         projectContext.Load(project.Assignments,
             assignments => assignments
             .IncludeWithDefaultProperties(
                assignment => assignment.Task, 
                assignment => assignment.Resource,
                assignment => assignment.Task.Parent)
             .Where(a => a.Resource.Name == name));

         projectContext.ExecuteQuery();
      }

      private static void AddWork(TimesheetEntry entry, TimeSheetLine timesheetLine, ProjectContext projectContext)
      {
         var workCreation = new TimeSheetWorkCreationInformation
         {
            ActualWork = $"{entry.Duration.TotalHours.ToString("#0.##").Replace(".", ",")}h",
            Start = entry.Start,
            End = entry.End,
            NonBillableOvertimeWork = Constants.ZeroHour,
            NonBillableWork = Constants.ZeroHour,
            OvertimeWork = Constants.ZeroHour,
            PlannedWork = Constants.ZeroHour
         };

         timesheetLine.Work.Add(workCreation);
         timesheetLine.TimeSheet.Update();

         projectContext.ExecuteQuery();
      }

      private static PublishedAssignment GetPublishedAssignment(PublishedProject project, TimesheetEntry entry)
      {
         var assignments = project.Assignments.Where(a => a.Task.Name.Equals(entry.TaskHierarchy[0]));

         if (assignments.Count() > 1)
            assignments = assignments.Where(a => a.Task.Parent.ServerObjectIsNull.HasValue && !a.Task.Parent.ServerObjectIsNull.Value && a.Task.Parent.Name.Equals(entry.TaskHierarchy[1]));

         var assignment = assignments.Last();
         return assignment;
      }

      private static TimeSheetLine GetTimeSheetLine(TimeSheetPeriod weeklyTimesheet, PublishedAssignment assignment, PublishedProject project, ProjectContext projectContext)
      {
         var timesheetLine = FindTimesheetLine(weeklyTimesheet, assignment.Task.Name, project.Name);

         if (timesheetLine == null)
         {
            var timeSheetLineCreationInformation = new TimeSheetLineCreationInformation
            {
               LineClass = TimeSheetLineClass.StandardLine,
               ProjectId = project.Id,
               TaskName = assignment.Task.Name,
               AssignmentId = assignment.Id
            };

            weeklyTimesheet.TimeSheet.Lines.Add(timeSheetLineCreationInformation);
            weeklyTimesheet.TimeSheet.Update();

            projectContext.Load(weeklyTimesheet.TimeSheet.Lines);
            projectContext.ExecuteQuery();

            timesheetLine = FindTimesheetLine(weeklyTimesheet, assignment.Task.Name, project.Name);
         }

         projectContext.Load(timesheetLine.Work);
         projectContext.ExecuteQuery();

         return timesheetLine;
      }

      private static TimeSheetLine GetAdministrativeTimeSheetLine(TimeSheetPeriod weeklyTimesheet, TimesheetEntry entry, ProjectContext projectContext, User user)
      {
         var taskName = entry.TaskHierarchy.First();
         var timesheetLine = FindTimesheetLine(weeklyTimesheet, taskName, Constants.AdministrativeWork);
         var retryCount = 10;

         if (timesheetLine == null)
         {
            TimeSheetExtended.AddAdministrativeLine(weeklyTimesheet.TimeSheet.Id, taskName, entry.Comment, user);

            while (timesheetLine == null)
            {
               Thread.Sleep(500);

               retryCount -= 1;

               projectContext.Load(weeklyTimesheet.TimeSheet.Lines);
               projectContext.ExecuteQuery();
               timesheetLine = FindTimesheetLine(weeklyTimesheet, taskName, Constants.AdministrativeWork);

               if (retryCount == 0 && timesheetLine == null)
                  throw new Exception("Could not create Administrative line to timesheet - Too many jobs in queue! Try again later");
            }
         }
         else if (!string.IsNullOrWhiteSpace(entry.Comment))
         {
            if (string.IsNullOrWhiteSpace(timesheetLine.Comment))
               timesheetLine.Comment = entry.Comment;
            else if (!timesheetLine.Comment.Contains(entry.Comment))
               timesheetLine.Comment = $"{timesheetLine.Comment}, {entry.Comment}";

            weeklyTimesheet.TimeSheet.Update();
         }

         projectContext.Load(timesheetLine.Work);
         projectContext.ExecuteQuery();

         return timesheetLine;
      }

      private static TimeSheetLine FindTimesheetLine(TimeSheetPeriod weeklyTimesheet, string taskName, string projectName)
      {
         return weeklyTimesheet.TimeSheet.Lines.FirstOrDefault(l => l.TaskName.Equals(taskName) && l.ProjectName.Equals(projectName));
      }

      public static DateTime GetFirstDayOfWeek()
      {
         var firstDayInWeek = DateTime.Now;
         while (firstDayInWeek.DayOfWeek != DayOfWeek.Monday)
            firstDayInWeek = firstDayInWeek.AddDays(-1);

         return firstDayInWeek;
      }
   }
}

