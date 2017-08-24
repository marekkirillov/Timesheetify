namespace TogglToTimesheet
{
    using System;
    using System.IO;
    using Microsoft.Win32.TaskScheduler;

    public static class TimesheetifyCore
    {
        public static void Install()
        {
            Console.WriteLine("Installing Timesheetify ..." + Environment.NewLine);

            var src = AppDomain.CurrentDomain.BaseDirectory;
            var dest = CProgramFilesTimisheetify;

            foreach (var dirPath in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(src, dest));

            foreach (var newPath in Directory.GetFiles(src, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(src, dest), true);

            Console.WriteLine($"Copied files to {dest}" + Environment.NewLine);

            Console.Write("Please enter your TogglAPI key:" + Environment.NewLine);
            var data = Console.ReadLine();

            File.WriteAllText(CProgramFilesTimisheetify + "settings.txt", data);

            Console.WriteLine($"Key saved to {CProgramFilesTimisheetify}settings.txt" + Environment.NewLine);
            Console.WriteLine($"Enter the time (in format hh:mm) when daily Toggl projects and tags update task will be executed:" + Environment.NewLine);

            var time = Console.ReadLine();

            using (var ts = new TaskService())
            {
                var td = ts.NewTask();
                td.RegistrationInfo.Description = "Update Toggl projects and tags";

                td.Triggers.Add(new DailyTrigger
                {
                    DaysInterval = 1,
                    StartBoundary = Convert.ToDateTime($"{DateTime.Today.ToShortDateString()} {time}"),
                    Enabled = true,
                });

                td.Actions.Add(new ExecAction(dest, "-tl"));
                ts.RootFolder.RegisterTaskDefinition(@"Update Toggl", td);
            }

            Console.Write("Daily Toggl projects and tags update task added to Windows Task Scheduler" + Environment.NewLine);

            Console.WriteLine($"Enter the day (1-7 where 1 = Monday) when weekly Timesheet update task will be executed:" + Environment.NewLine);

            var day = Console.ReadLine();

            Console.WriteLine($"Enter the time (in format hh:mm) when weekly Timesheet update task will be executed:" + Environment.NewLine);

            var dayTime = Console.ReadLine();

            using (var ts = new TaskService())
            {
                var td = ts.NewTask();
                td.RegistrationInfo.Description = "Update Timesheet";

                td.Triggers.Add(new WeeklyTrigger
                {
                    DaysOfWeek = (DaysOfTheWeek)Enum.Parse(typeof(DaysOfTheWeek), GetDayValue(day)),
                    StartBoundary = Convert.ToDateTime($"{DateTime.Today.ToShortDateString()} {dayTime}"),
                    Enabled = true,
                });

                td.Actions.Add(new ExecAction(dest, "-ts"));
                ts.RootFolder.RegisterTaskDefinition(@"Update Timesheet", td);
            }

            Console.Write("Weekly Timesheet update task added to Windows Task Scheduler" + Environment.NewLine);

            Console.WriteLine("Install successful" + Environment.NewLine);
            Console.WriteLine("Press any key exit");

            Console.ReadKey();
        }

        private static string GetDayValue(string day)
        {
            switch (day)
            {
                case "3":
                    day = "4";
                    break;
                case "4":
                    day = "8";
                    break;
                case "5":
                    day = "16";
                    break;
                case "6":
                    day = "32";
                    break;
                case "7":
                    day = "64";
                    break;
            }

            return day;
        }

        private static string CProgramFilesTimisheetify => "C:\\Program Files\\Timisheetify\\";

        public static string GetKey()
        {
            return File.ReadAllText(CProgramFilesTimisheetify + "settings.txt");
        }
    }
}
