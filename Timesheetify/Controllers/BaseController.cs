using System.Web.Mvc;

namespace Timesheetify.Controllers
{
	using System;
	using System.IO;
	using System.ServiceModel;
	using System.Web.Services.Protocols;
	using Helpers;
	using TogglToTimesheet.Data;
	using TogglToTimesheet.Repository;

	public class BaseController : Controller
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

		public Worker CurrentWorker
		{
			get
			{
				var worker = (Worker)HttpContext.Items["worker"];

				if (worker == null)
				{
					worker = WorkerRepository.GetCurrentWorker(CurrentUsername);
					HttpContext.Items["worker"] = worker;
				}

				return worker;
			}
		}

		public string CurrentUsername => User.Identity.Name.CleanName();

		public enum Action
		{
			TogglToTimesheet = 1,
			TimesheetToToggl = 2,
			APIKeySave = 3,
			SaveSettings = 4
		}

		public void LogError(Exception e)
		{
			var path = GetPath();
			var error = $"{Environment.NewLine}ERROR - {DateTime.Now} - {User.Identity.Name.CleanName()} - {e.Message}";
			var stacktrace = $"{Environment.NewLine}{e.StackTrace}";

			if (e.InnerException != null)
			{
				error += $"- ({e.InnerException.Message})";
				stacktrace += $"{Environment.NewLine}{e.InnerException.StackTrace}";
			}

			System.IO.File.AppendAllText(path, error);
			System.IO.File.AppendAllText(path, stacktrace);
		}

		protected static bool ApiIsValid(string key)
		{
			return key != null && key.Length == 32;
		}

		private static string GetPath()
		{
			const string path = "C:\\Logs\\Timesheetify";

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			return Path.Combine(path, "Log.txt");
		}

		public void LogRequest(Action action, string success)
		{
			var path = GetPath(); var msg = $"{Environment.NewLine}ACTION - {DateTime.Now} - {User.Identity.Name.CleanName()} - {(action == Action.TimesheetToToggl ? "Timesheet -> Toggl" : action == Action.TogglToTimesheet ? "Toggl -> Timesheet" : "Toggl API key saved")} - with message:{success}";

			System.IO.File.AppendAllText(path, msg);
		}
	}
}