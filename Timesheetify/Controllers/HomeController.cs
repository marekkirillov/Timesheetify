using System.Web.Mvc;

namespace Timesheetify.Controllers
{
   using System;
   using System.Data.Entity.Migrations;
   using System.IO;
   using System.Linq;
   using Data;
   using Models;
   using TogglToTimesheet;
   using System.Web.Http;
   using NG.Timesheetify.Common.Active_Directory;
   using TogglToTimesheet.Active_Directory;
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

      public string Error
      {
         get
         {
            var obj = TempData["error"];
            return obj?.ToString();
         }
         set { TempData["error"] = value; }
      }

      public string Success
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
            model.Error = Error;
            model.Success = Success;
         }

         return View(model);
      }

      public ActionResult Save(Model model)
      {
         SaveKey(model);

         Redirected = true;
         return RedirectToAction("Index");
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

      [HttpPost]
      public ActionResult UpdateTimesheet(Model model)
      {
         var key = GetApiKey();
         if (key != null)
         {
            try
            {
               var result = Program.UpdateTimesheet(key, GetUser(model.Password));
               Success = $"Successfully added {result} entries to Timesheet";
               LogRequest(Action.TogglToTimesheet, Success);
            }
            catch (Exception e)
            {
               if (e.Message.Contains("GeneralItemDoesNotExist"))
                  Error = $"Could not find '{Timesheet.ItemInProgress}' from your project server tasklist. Contact your project manager!";
               else
                  Error = e.Message + Environment.NewLine + e.InnerException;

               LogError(e);
            }
         }
         else
            Error = "Toggl API key not set";

         Redirected = true;
         return RedirectToAction("Index");
      }

      [HttpPost]
      public ActionResult UpdateToggl(Model model)
      {
         var key = GetApiKey();

         if (key != null)
         {
            try
            {
               var items = Program.UpdateToggl(key, GetUser(model.Password));
               Success = items.Item1 == 0 && items.Item2 == 0 ? "Already up-to-date" : $"Successfully added {items.Item1} new projects and {items.Item2} new assignemts to Toggl";
               LogRequest(Action.TimesheetToToggl, Success);
            }
            catch (Exception e)
            {
               Error = e.Message + Environment.NewLine + e.InnerException;

               LogError(e);
            }
         }
         else
            Error = "Toggl API key not set";

         Redirected = true;
         return RedirectToAction("Index");
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
         var path = Path.Combine(Server.MapPath("~"), "Logs");

         if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

         return Path.Combine(path, "Log.txt");
      }

      public void LogRequest(Action action, string success)
      {
         var path = GetPath(); var msg = $"{Environment.NewLine}ACTION - {DateTime.Now} - {User.Identity.Name} - {(action == Action.TimesheetToToggl ? "Timesheet -> Toggl" : action == Action.TogglToTimesheet ? "Toggl -> Timesheet" : "Toggl API key saved")} - with message:{success}";

         System.IO.File.AppendAllText(path, msg);
      }

      public enum Action
      {
         TogglToTimesheet = 1,
         TimesheetToToggl = 2,
         APIKeySave = 3
      }

      #region Impersonisation POC
      //public ContentResult POC()
      //{
      //    using (var projectContext = new ProjectContext(Constants.PwaPath))
      //    {
      //        activeContex = projectContext;
      //        projectContext.Load(projectContext.TimeSheetPeriods);
      //        projectContext.ExecutingWebRequest += ProjectContextOnExecutingWebRequest;
      //        projectContext.ExecuteQuery();
      //    }
      //    return new ContentResult() { Content = "OK" };
      //}
      //private static ProjectContext activeContex;

      //private void ProjectContextOnExecutingWebRequest(object sender, WebRequestEventArgs webRequestEventArgs)
      //{
      //    var httpWebRequest = webRequestEventArgs.WebRequestExecutor.WebRequest;
      //    var servNameIndex = httpWebRequest.RequestUri.AbsolutePath.LastIndexOf("/") + 1;
      //    var forwardedFrom = "/_vti_bin/psi/" + httpWebRequest.RequestUri.AbsolutePath.Substring(servNameIndex, httpWebRequest.RequestUri.AbsolutePath.Length - servNameIndex);

      //    httpWebRequest.UseDefaultCredentials = true;
      //    httpWebRequest.PreAuthenticate = true;
      //    httpWebRequest.Headers.Add("PjAuth", GetImpersonationHeader(activeContex));
      //    httpWebRequest.Headers.Add("ForwardedFrom", forwardedFrom);

      //    httpWebRequest.Headers.Remove("X-FORMS_BASED_AUTH_ACCEPTED");
      //    httpWebRequest.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
      //}

      //public string GetImpersonationHeader(ProjectContext contex)
      //{
      //    //Microsoft.Office.Project.Server.Library.PSContextInfo contextInsfo = new Microsoft.Office.Project.Server.Library.PSContextInfo(true, "", new Guid(), Guid.Empty, Guid.Empty, null, null);
      //    //public PSContextInfo(bool isWindowsUser, string userName, Guid userGuid, Guid trackingGuid, Guid siteGuid, CultureInfo languageCulture, CultureInfo localeCulture)
      //    //var ResourceDs = .ReadResources("", false);
      //    var UserId = string.Concat("i:0#.w|NETGROUPDIGITAL", @"", GetUsername()); // epm is domain name
      //    var CurrentUser = string.Concat("NETGROUPDIGITAL", @"", UserId);
      //    var contextInfo = new Microsoft.Office.Project.Server.Library.PSContextInfo(true, CurrentUser, Guid.Empty, Guid.Empty, Guid.Empty, 0, null, null, Guid.Empty, string.Empty);
      //    return Microsoft.Office.Project.Server.Library.PSContextInfo.SerializeToString(contextInfo);
      //}

      //private string GetUsername()
      //{
      //    return User.Identity.Name.Split('\\').Last().Trim();
      //}
      #endregion

   }
}
