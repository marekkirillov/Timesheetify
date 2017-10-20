namespace Timesheetify
{
	using System.Web.Mvc;
	using TogglToTimesheet;

	public class RequestFilter: IActionFilter
	{
		public void OnActionExecuting(ActionExecutingContext filterContext)
		{
			Timesheetify.CurrentAccountName = filterContext.HttpContext.User.Identity.Name;
		}

		public void OnActionExecuted(ActionExecutedContext filterContext)
		{

		}
	}
}