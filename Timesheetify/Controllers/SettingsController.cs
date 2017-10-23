using System.Web.Mvc;

namespace Timesheetify.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Data.Entity.Migrations;
	using System.Linq;
	using Helpers;
	using Models;
	using TogglToTimesheet;
	using TogglToTimesheet.Data;

	public class SettingsController : BaseController
	{
		// GET: Settings
		public ActionResult Index()
		{
			var model = new SettingsModel
			{
				Workspaces = GetListOfWorkspaces(CurrentWorker.TogglApiKey),
				WorkspaceId = CurrentWorker.WorkspaceName,
				Cleanup = CurrentWorker.Cleanup
			};

			return PartialView(model);
		}
		public ActionResult SaveSettings(SettingsModel model)
		{
			try
			{
				using (var context = new TimesheetifyEntities())
				{
					var worker = context.Workers.First(f => f.Identity.Equals(CurrentUsername));
					worker.Cleanup = model.Cleanup;
					worker.WorkspaceName = model.WorkspaceId;
					context.Workers.AddOrUpdate(worker);
					context.SaveChanges();
				}

				LogRequest(Action.SaveSettings, "OK");

			}
			catch (Exception e)
			{
				ErrorMsg = e.Message + Environment.NewLine + e.InnerException;
				LogError(e);
			}

			Redirected = true;
			return Redirect(Url.Action("Index", "Home"));
		}


		private IList<SelectListItem> GetListOfWorkspaces(string apiKey)
		{
			if (ApiIsValid(apiKey))
				return new Toggl(apiKey, CurrentWorker.WorkspaceName).GetAllToggleWorkspaces().Select(w => new SelectListItem
				{
					Value = w.id,
					Text = w.name
				}).ToList();

			return new List<SelectListItem>();
		}
	}
}