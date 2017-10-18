using System.Collections.Generic;
using System.Web.Mvc;

namespace Timesheetify.Controllers
{
    using System;
    using System.Data.Entity.Migrations;
    using System.IO;
    using System.Linq;
    using Models;
    using TogglToTimesheet;
    using System.Web.Http;
    using System.Web.Services.Protocols;
    using Microsoft.Office.Project.Server.Library;
    using TogglToTimesheet.Active_Directory;
    using TogglToTimesheet.Data;
    using TogglToTimesheet.DTO;
    using User = NG.Timesheetify.Common.Active_Directory.User;

    [System.Web.Mvc.Authorize]
    public class HomeController : Controller
    {

        #region Tempdata

        public bool Redirected
        {
            get
            {
                var obj = TempData["redirected"];
                return obj != null && bool.Parse(obj.ToString());
            }
            set { TempData["redirected"] = value; }
        }

        public string ErrorMsg
        {
            get
            {
                var obj = TempData["error"];
                return obj?.ToString();
            }
            set { TempData["error"] = value; }
        }

        public string SuccessMsg
        {
            get
            {
                var obj = TempData["success"];
                return obj?.ToString();
            }
            set { TempData["success"] = value; }
        }

        #endregion

        public ActionResult Index()
        {
            var model = new Model();

            using (var context = new TimesheetifyEntities())
            {
                model.Name = User.Identity.Name;
                model.ApiKey = context.Workers.FirstOrDefault(f => f.Identity.Equals(User.Identity.Name))?.TogglApiKey;
                model.ShowSuccess = Redirected;
                model.Error = ErrorMsg;
                model.Success = SuccessMsg;
                model.Weeks = GetListOfPreviousMondays();
            }

            return View(model);
        }

        public ActionResult Save(Model model)
        {
            SaveKey(model);

            Redirected = true;
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult UpdateTimesheet(Model model)
        {
            if (model.SelectedWeek.HasValue && GetListOfPrevousMondays().Contains(model.SelectedWeek.Value))
            {
                try
                {
                    var result = Timesheetify.UpdateTimesheet(User.Identity.Name, model.SelectedWeek);
                    SuccessMsg = $"Successfully added {result.NewTimesheetLines} entries to Timesheet";
                    LogRequest(Action.TogglToTimesheet, SuccessMsg);
                }
                catch (Exception e)
                {
                    var ww = new PSClientError(e as SoapException);
                    ErrorMsg = e.Message + Environment.NewLine + e.InnerException;
                    LogError(e);
                }
            }
            else
                ErrorMsg = "Week to sync is invalid";

            Redirected = true;
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult UpdateToggl(Model model)
        {
            try
            {
                var result = Timesheetify.UpdateToggl(User.Identity.Name);
                SuccessMsg = result.IsUpToDate ? "Already up-to-date" : GetResultMessage(result);
                LogRequest(Action.TimesheetToToggl, SuccessMsg);
            }
            catch (Exception e)
            {
                ErrorMsg = e.Message + Environment.NewLine + e.InnerException;
                LogError(e);
            }

            Redirected = true;
            return RedirectToAction("Index");
        }

        private string GetResultMessage(TimesheetToTogglResult result)
        {
            var message = "";

            if (result.ProjectsResult.AddedProjects > 0)
                message += $"Successfully added {result.ProjectsResult.AddedProjects} new projects";
            if (result.TagsResult.AddedTags > 0)
                message += $"{(message.Length > 0 ? " and" : "Successfully added")} new {result.TagsResult.AddedTags} tags ";

            if (message.Length > 0)
                message += " to Toggl.";

            if (result.ProjectsResult.ArchivedProjects > 0)
                message += $"Successfully archived {result.ProjectsResult.ArchivedProjects} projects";
            if (result.TagsResult.RemovedTags > 0)
                message += $"{(message.Length > 0 ? " and" : "Successfully")} removed {result.TagsResult.RemovedTags} tags ";

            if (message.Length > 0)
                message += " from Toggl.";

            return message;
        }

        public string GetApiKey()
        {
            using (var context = new TimesheetifyEntities())
                return context.Workers.FirstOrDefault(f => f.Identity.Equals(User.Identity.Name))?.TogglApiKey;
        }

        private User GetUser(string password)
        {
            var user = AdUserProvider.GetUserByIdentityName(User.Identity.Name);
            user.Password = password;
            return user;
        }

        public void LogError(Exception e)
        {
            var path = GetPath();
            var error = $"{Environment.NewLine}ERROR - {DateTime.Now} - {User.Identity.Name} - {e.Message}";
            var stacktrace = $"{Environment.NewLine}{e.StackTrace}";

            if (e.InnerException != null)
            {
                error += $"- ({e.InnerException.Message})";
                stacktrace += $"{Environment.NewLine}{e.InnerException.StackTrace}";
            }

            System.IO.File.AppendAllText(path, error);
            System.IO.File.AppendAllText(path, stacktrace);
        }

        private string GetPath()
        {
            var path = "C:\\Logs\\Timesheetify";

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return Path.Combine(path, "Log.txt");
        }

        public void LogRequest(Action action, string success)
        {
            var path = GetPath(); var msg = $"{Environment.NewLine}ACTION - {DateTime.Now} - {User.Identity.Name} - {(action == Action.TimesheetToToggl ? "Timesheet -> Toggl" : action == Action.TogglToTimesheet ? "Toggl -> Timesheet" : "Toggl API key saved")} - with message:{success}";

            System.IO.File.AppendAllText(path, msg);
        }

        private void SaveKey(Model model)
        {
            using (var context = new TimesheetifyEntities())
            {
                var worker = context.Workers.FirstOrDefault(f => f.Identity.Equals(User.Identity.Name)) ?? new Worker
                {
                    Identity = User.Identity.Name
                };

                worker.TogglApiKey = model.ApiKey;
                context.Workers.AddOrUpdate(worker);
                context.SaveChanges();

                LogRequest(Action.APIKeySave, "OK");
            }
        }

        private static IList<SelectListItem> GetListOfPreviousMondays()
        {
            return GetListOfPrevousMondays()
                .Select(m => new SelectListItem
                {
                    Value = m.ToString("O"),
                    Text = m.ToShortDateString()
                }).ToList();
        }

        private static IEnumerable<DateTime> GetListOfPrevousMondays()
        {
            var list = new List<DateTime>();
            var today = (int)DateTime.Today.DayOfWeek;
            var currentMonth = DateTime.Today.Month;
            var isLastMonthMondayAdded = false;

            const int maxWeeks = 5;

            while (list.Count < maxWeeks)
            {
                var monday = DateTime.Today.AddDays(-today + (int)DayOfWeek.Monday - list.Count * 7);

                if (monday.Month != currentMonth)
                {
                    if (isLastMonthMondayAdded) break;
                    isLastMonthMondayAdded = true;
                }

                list.Add(monday);
            }

            return list;
        }


        public enum Action
        {
            TogglToTimesheet = 1,
            TimesheetToToggl = 2,
            APIKeySave = 3
        }
    }
}
