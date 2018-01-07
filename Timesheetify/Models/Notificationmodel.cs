using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TogglToTimesheet.Active_Directory;

namespace Timesheetify.Models
{
	public class NotificationModel
	{
		public int Id { get; set; }
		public string Heading { get; set; }
		public string Content { get; set; }
	}
}