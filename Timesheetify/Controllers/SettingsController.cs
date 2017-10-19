using System.Web.Mvc;

namespace Timesheetify.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Models;
	using TogglToTimesheet;

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
				var worker = CurrentWorker;
				worker.Cleanup = model.Cleanup;
				worker.WorkspaceName = model.WorkspaceId;
				worker.SaveChanges();
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
			return new Toggl(apiKey, CurrentWorker.WorkspaceName).GetAllToggleWorkspaces().Select(w => new SelectListItem
			{
				Value = w.id,
				Text = w.name
			}).ToList();
		}

	}
}