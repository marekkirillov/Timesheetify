namespace Timesheetify
{
	using System.Web.Mvc;
	using Helpers;
	using TogglToTimesheet;

	public class RequestFilter: IActionFilter
	{
		public void OnActionExecuting(ActionExecutingContext filterContext)
		{
			Timesheetify.CurrentAccountName = filterContext.HttpContext.User.Identity.Name.CleanName();
		}

		public void OnActionExecuted(ActionExecutedContext filterContext)
		{

		}
	}
}