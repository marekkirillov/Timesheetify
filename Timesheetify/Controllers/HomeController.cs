using System.Collections.Generic;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
using TogglToTimesheet.Active_Directory;
using TogglToTimesheet.Repository;

namespace Timesheetify.Controllers
{
	using System;
	using System.Data.Entity.Migrations;
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
			EnsureUser();

			var notification = new NotificationRepository().Get(CurrentWorker.Id);

			var model = new Model
			{
				Name = User.Identity.Name.CleanName(),
				ApiKey = CurrentWorker?.TogglApiKey,
				Success = SuccessMsg,
				Error = ErrorMsg,
				AutosubmitEnabled = CurrentWorker?.AutoSubmit ?? false
			};

			if (notification != null)
			{
				model.Notification = new NotificationModel
				{
					Id = notification.Id,
					Content = notification.ContentHTML,
					Heading = notification.Heading.Replace("$User",
						AdUserProvider.GetUserByIdentityName(User.Identity.Name.CleanName()).DisplayName)
				};
			}

			try
			{
				FillLists(model);
			}
			catch (Exception e)
			{
				model.Error = e.Message;
				LogError(e);
				model.Weeks = new List<SelectListItem>();
				model.Approvers = new List<SelectListItem>();
			}

			return View(model);
		}

		public ActionResult Save(Model model)
		{
			if (ApiIsValid(model.ApiKey))
				try
				{
					SaveKey(model);
					SuccessMsg = "Changes saved";
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
					var result = Timesheetify.UpdateTimesheet(CurrentUsername, model.SelectedWeek, model.SelectedApprover);
					SuccessMsg = $"Successfully added {result.NewTimesheetLines} entries to Timesheet";
					LogRequest(Action.TogglToTimesheet, SuccessMsg);

					if (model.SelectedApprover != null && string.IsNullOrEmpty(result.Message) && CurrentWorker.AutoSubmit.GetValueOrDefault())
						SuccessMsg = $"Timesheet with {result.NewTimesheetLines} entries submitted successfully";
					else
						ErrorMsg = result.Message;
				}
				catch (Exception e)
				{
					if (e.Message.Contains("GeneralItemDoesNotExist") ||
						e.InnerException != null && e.InnerException.Message.Contains("GeneralItemDoesNotExist"))
						ErrorMsg = "Timesheet throw an error (GeneralItemDoesNotExist). Please sync from Timesheet to Toggl with Cleanup Toggl option (from Advanced settings) and try again.";
					if (e.Message.Contains("GeneralInvalidOperation") ||
					    e.InnerException != null && e.InnerException.Message.Contains("GeneralInvalidOperation"))
						ErrorMsg = $"Timesheet throw an error (GeneralInvalidOperation). Please verify that all rows in Toggl for the week starting at {model.SelectedWeek.Value.ToShortDateString()} has matching PROJECTS and TAGS selected.";
					else
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
				var worker = context.Workers.FirstOrDefault(f => f.Identity.Equals(CurrentUsername));

				new Toggl(model.ApiKey, null).ValidateApiKey(model.ApiKey);

				worker.TogglApiKey = model.ApiKey;
				context.Workers.AddOrUpdate(worker);
				context.SaveChanges();

				LogRequest(Action.APIKeySave, "OK");
			}
		}

		private void FillLists(Model model)
		{
			var approvers = model.AutosubmitEnabled ? new Dictionary<Guid, List<TimesheetApprover>>() : null;
			model.Weeks = GetListOfPrevousMondays(approvers).Select(m => new SelectListItem
			{
				Value = m.ToString("O"),
				Text = m.ToString("dd.MM.yyyy")
			}).ToList();

			if (model.AutosubmitEnabled)
			{
				var uniqueApprovers = approvers.SelectMany(a => a.Value).DistinctBy(a => a.Uid);
				model.Approvers = uniqueApprovers.Select(m => new SelectListItem
				{
					Value = m.Uid.ToString(),
					Text = m.Name
				}).ToList();

				model.Approvers.Insert(0, new SelectListItem
				{
					Value = null,
					Text = "Do not submit"
				});

				model.SelectedApprover = CurrentWorker?.ApproverGuid;
			}
		}

		private IEnumerable<DateTime> GetListOfPrevousMondays(Dictionary<Guid, List<TimesheetApprover>> approvers = null)
		{
			var list = new List<DateTime>();
			var today = DateTime.Today.DayOfWeek;

			if (today == DayOfWeek.Sunday)
				today = DayOfWeek.Saturday;

			const int maxWeeks = 5;

			for (var i = 1; i <= maxWeeks; i++)
			{
				var monday = DateTime.Today.AddDays(-(int)today + (int)DayOfWeek.Monday - list.Count * 7);
				if (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);
				list.Add(monday);
			}

			return list.Where(l => Timesheet.IsTimesheetOpen(CurrentUsername, l, approvers));
		}

		[HttpPost]
		public JsonResult DismissNotification(int id)
		{
			new NotificationRepository().Dissmiss(id, CurrentWorker.Id);
			return null;
		}
	}
}
