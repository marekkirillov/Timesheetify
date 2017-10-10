namespace TogglToTimesheet.DTO
{
    public class TogglToTimesheetResult
    {
        public TogglToTimesheetResult(int newTimesheetLines)
        {
            NewTimesheetLines = newTimesheetLines;
        }
        public int NewTimesheetLines { get; set; }
    }
}
