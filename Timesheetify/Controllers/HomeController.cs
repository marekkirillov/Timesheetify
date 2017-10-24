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
	using Helpers;
	using TogglToTimesheet.Data;
	using TogglToTimesheet.DTO;

	[System.Web.Mvc.Authorize]
	public class HomeController : BaseController
	{
		public ActionResult Index()
		{
			var model = new Model
			{
				Name = User.Identity.Name.CleanName(),
				ApiKey = CurrentWorker?.TogglApiKey,
				ShowSuccess = Redirected,
				Success = SuccessMsg,
				Error = ErrorMsg
			};

			try
			{
				model.Weeks = GetListOfPreviousMondays();

			}
			catch (Exception e)
			{
				model.Error = e.Message;
				LogError(e);
				model.Weeks = new List<SelectListItem>();
			}


			return View(model);
		}

		public ActionResult Save(Model model)
		{
			if (ApiIsValid(model.ApiKey))
				try
				{
					SaveKey(model);
				}
				catch (Exception e)
				{
					ErrorMsg = e.Message;
					LogError(e);
				}
			else
				ErrorMsg = "API key not valid";

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
					var result = Timesheetify.UpdateTimesheet(CurrentUsername, model.SelectedWeek);
					SuccessMsg = $"Successfully added {result.NewTimesheetLines} entries to Timesheet";
					LogRequest(Action.TogglToTimesheet, SuccessMsg);
				}
				catch (Exception e)
				{
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
				var result = Timesheetify.UpdateToggl(CurrentUsername);
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

		private static string GetResultMessage(TimesheetToTogglResult result)
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

			if (message.Length > 0 && (result.ProjectsResult.ArchivedProjects > 0 || result.TagsResult.RemovedTags > 0))
				message += " from Toggl.";

			return message;
		}



		private void SaveKey(Model model)
		{
			using (var context = new TimesheetifyEntities())
			{
				var worker = context.Workers.FirstOrDefault(f => f.Identity.Equals(CurrentUsername)) ?? new Worker
				{
					Identity = User.Identity.Name.CleanName()
				};

				new Toggl(model.ApiKey, null).ValidateApiKey(model.ApiKey);

				worker.TogglApiKey = model.ApiKey;
				context.Workers.AddOrUpdate(worker);
				context.SaveChanges();

				LogRequest(Action.APIKeySave, "OK");
			}
		}

		private IList<SelectListItem> GetListOfPreviousMondays()
		{
			return GetListOfPrevousMondays()
				.Select(m => new SelectListItem
				{
					Value = m.ToString("O"),
					Text = m.ToString("dd.MM.yyyy")
				}).ToList();
		}

		private IEnumerable<DateTime> GetListOfPrevousMondays()
		{
			var list = new List<DateTime>();
			var today = DateTime.Today.DayOfWeek;

			if (today == DayOfWeek.Sunday)
				today = DayOfWeek.Saturday;

			var currentMonth = DateTime.Today.Month;
			var isLastMonthMondayAdded = false;

			const int maxWeeks = 5;

			while (list.Count < maxWeeks)
			{
				var monday = DateTime.Today.AddDays(-(int)today + (int)DayOfWeek.Monday - list.Count * 7);

				if (monday.Month != currentMonth)
				{
					if (isLastMonthMondayAdded) break;
					isLastMonthMondayAdded = true;
				}

				list.Add(monday);
			}

			return list.Where(l => Timesheet.IsTimesheetOpen(CurrentUsername, l));
		}
	}
}
