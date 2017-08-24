namespace TogglToTimesheet
{
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using Common;
    using Helper;
    using SvcAdmin;
    using SvcTimeSheet;
    using System;
    using System.Net;
    using NG.Timesheetify.Common.Active_Directory;
    using PSLibrary = Microsoft.Office.Project.Server.Library;

    public static class TimeSheetExtended
    {
        private const string TimesheetServicePath = "_vti_bin/psi/timesheet.asmx";
        private static Dictionary<string, Guid> _adminstrativeRows;

        public static Dictionary<string, Guid> GetAdministrativeRows(User user)
        {
            if (_adminstrativeRows == null)
            {
                using (var adminWebSvc = GetAdminSvc(user))
                {
                    var tsLineClassDs = adminWebSvc.ReadLineClasses(LineClassType.AllNonProject, LineClassState.Enabled);

                    _adminstrativeRows = tsLineClassDs
                        .LineClasses
                        .Rows
                        .Cast<DataRow>()
                        .ToDictionary(lineClassesRow => lineClassesRow["TS_LINE_CLASS_NAME"].ToString(), lineClassesRow => new Guid(lineClassesRow["TS_LINE_CLASS_UID"].ToString()));
                }
            }

            return _adminstrativeRows;
        }

        public static void AddAdministrativeLine(Guid timesheetId, string taskname, string comment, User user)
        {
            using (var timeSheetSvc = GetTimeSheetSvc(user))
            {
                var timesheet = timeSheetSvc.ReadTimesheet(timesheetId);

                Guid lineClassUid;
                var rowFound = GetAdministrativeRows(user).TryGetValue(taskname, out lineClassUid);

                if (!rowFound)
                    throw new Exception($"Could not find Administrative task {taskname}");

                var line = GetNewLine(timesheetId, taskname, timesheet);

                line.TS_LINE_COMMENT = comment;
                line.TS_LINE_CLASS_UID = lineClassUid;

                SaveLine(timesheetId, timesheet, line, timeSheetSvc, user);
            }
        }

        private static void SaveLine(Guid timesheetId, TimesheetDataSet timesheet, TimesheetDataSet.LinesRow line, TimeSheet timeSheetSvc, User user)
        {
            timesheet.Lines.AddLinesRow(line);
            timeSheetSvc.PrepareTimesheetLine(timesheetId, ref timesheet, new[] { line.TS_LINE_UID });

            var jobUid = Guid.NewGuid();
            timeSheetSvc.QueueUpdateTimesheet(jobUid, timesheetId, timesheet);
            QueueHelper.Wait(jobUid, user);
        }

        private static TimesheetDataSet.LinesRow GetNewLine(Guid timesheetId, string taskname, TimesheetDataSet timesheet)
        {
            var line = timesheet.Lines.NewLinesRow();

            line.TS_UID = timesheetId;
            line.TS_LINE_UID = Guid.NewGuid();
            line.TS_LINE_STATUS = (byte)PSLibrary.TimesheetEnum.LineStatus.NotApplicable;
            line.TS_LINE_VALIDATION_TYPE = (byte)PSLibrary.TimesheetEnum.ValidationType.Unverified;
            line.TS_LINE_CACHED_ASSIGN_NAME = taskname;
            return line;
        }

        private static TimeSheet GetTimeSheetSvc(User user)
        {
            var timeSheetSvc = new TimeSheet
            {
                UseDefaultCredentials = true,
                Url = Constants.PwaPath + TimesheetServicePath,
            };

            if (!user.UseDefaultCredentials)
                timeSheetSvc.Credentials = new NetworkCredential(user.AccountName, user.Password);

            return timeSheetSvc;
        }

        private static Admin GetAdminSvc(User user)
        {
            var adminSvc = new Admin
            {
                UseDefaultCredentials = true,
                Url = Constants.PwaPath + TimesheetServicePath
            };

            if (!user.UseDefaultCredentials)
                adminSvc.Credentials = new NetworkCredential(user.AccountName, user.Password);

            return adminSvc;
        }
    }
}
