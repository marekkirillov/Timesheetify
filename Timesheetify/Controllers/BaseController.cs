using System.Data.Entity.Migrations;
using System.Web.Mvc;

namespace Timesheetify.Controllers
{
	using System;
	using System.IO;
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

		public void EnsureUser()
		{
			if (WorkerRepository.GetCurrentWorker(CurrentUsername) == null)
			{
				using (var context = new TimesheetifyEntities())
				{
					var worker = new Worker
					{
						Identity = User.Identity.Name.CleanName()
					};

					worker.Notifications.Add(context.Notifications.Find(1));

					context.Workers.AddOrUpdate(worker);
					context.SaveChanges();

					LogRequest(Action.NewUser, "OK");
					HttpContext.Items["worker"] = worker;
				}
			}
		}

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
			SaveSettings = 4,
			NewUser = 5
		}

		public void LogError(Exception e, string msg = null)
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

			EmailSender.Send("Message: " + msg + Environment.NewLine + Environment.NewLine + error + Environment.NewLine + Environment.NewLine + stacktrace);
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
			var path = GetPath(); var msg = $"{Environment.NewLine}ACTION - {DateTime.Now} - {User.Identity.Name.CleanName()} - {GetMessage(action)} - with message:{success}";

			System.IO.File.AppendAllText(path, msg);
		}

		private static string GetMessage(Action action)
		{
			switch (action)
			{
				case Action.NewUser:
					return "New user created";
				case Action.APIKeySave:
					return "Toggl API key saved";
				case Action.TimesheetToToggl:
					return "Synced data from Timesheet to Toggl";
				case Action.TogglToTimesheet:
					return "Synced data from Toggl to Timesheet";
			}

			return string.Empty;
		}
	}
}