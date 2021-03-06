﻿using System;

namespace TogglToTimesheet.Repository
{
	using System.Collections.Generic;
	using System.Data.Entity.Migrations;
	using System.Linq;
	using Data;
	using DTO;

	public static class WorkerRepository
	{
		public static Worker GetCurrentWorker(string accountName)
		{
			using (var context = new TimesheetifyEntities())
				return context.Workers.FirstOrDefault(f => f.Identity.Equals(accountName));
		}

		public static List<WorkerAssignment> GetWorkerAssignments(string accountName)
		{
			using (var context = new TimesheetifyEntities())
			{
				var worker = GetCurrentWorker(accountName);
				return context.WorkerAssignments.Where(wa => wa.WorkerId.Equals(worker.Id)).ToList();
			}
		}

		public static void SaveWorkerAssignments(List<ResourceAssignment> assignments, Worker worker)
		{
			using (var context = new TimesheetifyEntities())
			{
				//Take all worker assignments, 
				//group by tag (incase there are more than one subtask per project with same name), 
				//take the latest subtask (latest end date of assignment),
				//save or update GUIDs by tag so that tags refer always to newest assignments

				var workerAssignments = context.WorkerAssignments.Where(wa => wa.WorkerId == worker.Id);
				var assignmentsGrouped = assignments.GroupBy(a => a.Tag, StringComparer.InvariantCultureIgnoreCase);
				var assignmentsDistincted = assignmentsGrouped.Select(g => g.OrderByDescending(a => a.End).First());

				foreach (var resourceAssignment in assignmentsDistincted)
				{
					var workerAssignment = workerAssignments.FirstOrDefault(wa => wa.Tag.Equals(resourceAssignment.Tag, StringComparison.InvariantCultureIgnoreCase)) ??
										   new WorkerAssignment
										   {
											   Tag = resourceAssignment.Tag,
											   WorkerId = worker.Id
										   };

					workerAssignment.AssignmentGuid = resourceAssignment.AssignmentUid;
					workerAssignment.TaskGuid = resourceAssignment.TaskUid;
					workerAssignment.ProjectGuid = resourceAssignment.ProjectUid;

					context.WorkerAssignments.AddOrUpdate(workerAssignment);
				}

				context.SaveChanges();
			}
		}

		public static void UpdateApprover(string accountName, Guid approverValue)
		{
			using (var context = new TimesheetifyEntities())
			{
				var worker = context.Workers.FirstOrDefault(f => f.Identity.Equals(accountName));
				if (worker == null) return;
				worker.ApproverGuid = approverValue;
				context.Workers.AddOrUpdate(worker);
				context.SaveChanges();
			}
		}
	}
}
