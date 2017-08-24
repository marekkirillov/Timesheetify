using System;

namespace TogglToTimesheet.Helper
{
    using System.Net;
    using System.Threading;
    using Common;
    using NG.Timesheetify.Common.Active_Directory;
    using SvcQueueSystem;

    static class QueueHelper
    {
        public static void Wait(Guid jobId, User user)
        {
           Thread.Sleep(500);
           return;

            using (var wssQueueSystem = GetQueueSystemSvc(user))
            {
                var attemptsCount = 0;
                var wait = wssQueueSystem.GetJobWaitTime(jobId);

                Thread.Sleep(wait * 500);
                do
                {
                    string xmlError;
                    var jobState = wssQueueSystem.GetJobCompletionState(jobId, out xmlError);

                    if (jobState == JobState.Success)
                    {
                        return;
                    }
                    if (jobState == JobState.Unknown
                        || jobState == JobState.Failed
                        || jobState == JobState.FailedNotBlocking
                        || jobState == JobState.CorrelationBlocked
                        || jobState == JobState.Canceled)
                    {
                        throw new Exception($"Queue request {jobState} for Job ID {jobId}.\r\n{xmlError}");
                    }

                    attemptsCount++;
                    Thread.Sleep(500);
                }
                while (attemptsCount > 20);
            }
        }

        private static QueueSystem GetQueueSystemSvc(User user)
        {
            var queueSystemSvc = new QueueSystem
            {
                UseDefaultCredentials = true,
                Url = Constants.PwaPath + "_vti_bin/psi/QueueSystem.asmx"
            };

            if (!user.UseDefaultCredentials)
                queueSystemSvc.Credentials = new NetworkCredential(user.DisplayName, user.Password);

            return queueSystemSvc;
        }
    }
}
