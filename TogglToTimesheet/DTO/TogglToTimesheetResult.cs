namespace TogglToTimesheet.DTO
{
    public class TogglToTimesheetResult
    {
        public TogglToTimesheetResult(int newTimesheetLines, string message)
        {
            NewTimesheetLines = newTimesheetLines;
	        Message = message;
        }
        public int NewTimesheetLines { get; set; }

		public string Message { get; set; }
    }
}
