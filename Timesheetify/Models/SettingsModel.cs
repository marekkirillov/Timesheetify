namespace Timesheetify.Models
{
	using System.Collections.Generic;
	using System.Web.Mvc;

	public class SettingsModel
	{
		public bool Cleanup { get; set; }
		public string WorkspaceId { get; set; }

		public IList<SelectListItem> Workspaces { get; set; }
	}
}