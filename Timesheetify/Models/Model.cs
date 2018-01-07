using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Timesheetify.Models
{
	public class Model
	{
		public Model()
		{
			ShowSuccess = true;
			Approvers = new List<SelectListItem>();
		}

		public string Name { get; set; }
		public string Department { get; set; }
		public string ApiKey { get; set; }
		public bool ShowSuccess { get; set; }
		public string Error { get; set; }

		public bool ToggleCleanup { get; set; }

		public string Success { get; set; }

		public IList<SelectListItem> Weeks { get; set; }
		public IList<SelectListItem> Approvers { get; set; }
		public DateTime? SelectedWeek { get; set; }
		public NotificationModel Notification { get; set; }
		public bool AutosubmitEnabled { get; set; }
		public Guid? SelectedApprover { get; set; }

	}
}