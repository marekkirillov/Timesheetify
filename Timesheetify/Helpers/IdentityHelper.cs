namespace Timesheetify.Helpers
{
	public static class IdentityHelper
	{
		public static string CleanName(this string s)
		{
			return s.Replace("0#.w|", "");
		}
	}
}