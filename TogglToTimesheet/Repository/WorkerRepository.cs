namespace TogglToTimesheet.Repository
{
    using System.Collections.Generic;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using Data;
    using DTO;

    public static class WorkerRepository
    {
        public static Worker GetCurrentWorker()
        {
            using (var context = new TimesheetifyEntities())
                return context.Workers.FirstOrDefault(f => f.Identity.Equals(Timesheetify.CurrentAccountName));
        }

        public static List<WorkerAssignment> GetWorkerAssignments()
        {
            using (var context = new TimesheetifyEntities())
            {
                var worker = GetCurrentWorker();
                return context.WorkerAssignments.Where(wa => wa.WorkerId.Equals(worker.Id)).ToList();
            }
        }

        public static void SaveWorkerAssignments(List<ResourceAssignment> assignments, Worker worker)
        {
            using (var context = new TimesheetifyEntities())
            {
                //Take all worker assignments, 
                //group by tag (incase there are more than one subtask per project with same name), 
                //take the newest subtask (newest end date of assignment),
                //save or update GUIDs by tag so that tags refer always to newest assignments

                var workerAssignments = context.WorkerAssignments.Where(wa => wa.WorkerId == worker.Id);
                var assignmentsGrouped = assignments.GroupBy(a => a.Tag);
                var assignmentsDistincted = assignmentsGrouped.Select(g => g.OrderByDescending(a => a.End).First());

                foreach (var resourceAssignment in assignmentsDistincted)
                {
                    var workerAssignment = workerAssignments.FirstOrDefault(wa => wa.Tag.Equals(resourceAssignment.Tag)) ??
                                           new WorkerAssignment
                                           {
                                               Tag = resourceAssignment.Tag,
                                               Worker = worker
                                           };

                    workerAssignment.AssignmentGuid = resourceAssignment.AssignmentUid;
                    workerAssignment.TaskGuid = resourceAssignment.TaskUid;
                    workerAssignment.ProjectGuid = resourceAssignment.ProjectUid;

                    context.WorkerAssignments.AddOrUpdate(workerAssignment);
                }

                context.SaveChanges();
            }
        }
    }
}
