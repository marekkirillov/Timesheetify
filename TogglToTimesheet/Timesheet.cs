namespace TogglToTimesheet
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Linq;
	using System.Runtime.Caching;
	using System.ServiceModel;
	using Common;
	using DTO;
	using Microsoft.Office.Project.Server.Library;
	using Repository;
	using SvcAdmin;
	using SvcResource;
	using SvcTimeSheet;
	using Resource = SvcResource.Resource;
	using ResourceAssignmentDataSet = SvcResource.ResourceAssignmentDataSet;

	public static class Timesheet
	{
		private static Dictionary<string, Guid> _lineClasses;

		public static bool IsTimesheetOpen(DateTime startDate)
		{
			var timesheetPeriod = GetTimesheetPeriod(startDate);
			using (var context = new ImpersonationContext<TimeSheetClient, TimeSheet>(Timesheetify.CurrentAccountName))
			{
				try
				{
					var timesheet = context.Client.ReadTimesheetByPeriod(context.UserUid, timesheetPeriod.WPRD_UID, Navigation.Current);
					if (timesheet == null)
						return true;

					if (timesheet.Headers.Count == 0)
						return false;

					var status = timesheet.Headers[0].TS_STATUS_ENUM;
					return status == (byte)TimesheetEnum.Status.InProgress || status == (byte)TimesheetEnum.Status.Rejected;
				}
				catch (Exception e)
				{
					if (e is FaultException)
					{
						var xml = ((FaultException)e).CreateMessageFault().GetDetail<SvcTimeSheet.ServerExecutionFault>()
							.ExceptionDetails.InnerXml;
						throw new Exception(xml);
					}
					throw;
				}
			}
		}

		internal static void FillWeek(TimesheetEntry[] timesheetEntries, DateTime? startDate = null)
		{
			LoadLineClasses();

			var timesheetPeriod = GetTimesheetPeriod(startDate);

			using (var context = new ImpersonationContext<TimeSheetClient, TimeSheet>(Timesheetify.CurrentAccountName))
			{
				var timesheet = GetTimesheet(context, timesheetPeriod, out var TS_UID);
				var timesheetRows = timesheet.Lines.Rows.Cast<TimesheetDataSet.LinesRow>().ToList();
				var workerAssignments = WorkerRepository.GetWorkerAssignments();
				var timesheetEntriesGrouped = timesheetEntries.GroupBy(t => t.Tag);

				foreach (var timesheetEntryGroup in timesheetEntriesGrouped)
				{
					var workerAssignment = workerAssignments.FirstOrDefault(w => w.Tag.Equals(timesheetEntryGroup.Key));

					if (workerAssignment == null)
						continue;

					var line = timesheetRows.FirstOrDefault(l => l.TASK_UID.Equals(workerAssignment.TaskGuid));
					var first = timesheetEntryGroup.First();

					if (line == null)
					{
						line = timesheet.Lines.NewLinesRow();

						line.TASK_UID = workerAssignment.TaskGuid;
						line.TS_LINE_CLASS_UID = GetLineClass(first);
						line.TS_LINE_CACHED_ASSIGN_NAME = first.Task;
						line.TS_LINE_CACHED_PROJ_NAME = first.Project;
						line.TS_LINE_UID = Guid.NewGuid();
						line.TS_UID = TS_UID;
						line.TS_LINE_ACT_SUM_VALUE = 0.0M;

						if (first.IsAdministrative)
						{
							line.TS_LINE_STATUS = (byte)TimesheetEnum.LineStatus.NotApplicable;
							line.TS_LINE_VALIDATION_TYPE = (byte)TimesheetEnum.ValidationType.Unverified;
						}
						else
						{
							line.PROJ_UID = workerAssignment.ProjectGuid;
							line.ASSN_UID = workerAssignment.AssignmentGuid;

							line.TS_LINE_STATUS = (byte)TimesheetEnum.LineStatus.Approved;
							line.TS_LINE_VALIDATION_TYPE = (byte)TimesheetEnum.ValidationType.Verified;
						}

						timesheet.Lines.AddLinesRow(line);
					}

					line.TS_LINE_COMMENT = first.IsAdministrative
						? string.Join(", ", timesheetEntryGroup.Select(t => t.Comment).Distinct())
						: string.Empty;

					context.Client.PrepareTimesheetLine(TS_UID, ref timesheet, new[] { line.TS_LINE_UID });

					foreach (var timesheetEntry in timesheetEntryGroup)
					{
						var actualWorkTsActStartDate = timesheetEntry.Start.Date;
						var actualWork = timesheet.Actuals.FindByTS_LINE_UIDTS_ACT_START_DATE(line.TS_LINE_UID, actualWorkTsActStartDate);

						if (actualWork != null)
							actualWork.TS_ACT_VALUE = timesheetEntry.Duration * 60 * 1000;
						else
						{
							actualWork = timesheet.Actuals.NewActualsRow();
							actualWork.TS_LINE_UID = line.TS_LINE_UID;
							actualWork.TS_ACT_FINISH_DATE = timesheetEntry.End.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
							actualWork.TS_ACT_START_DATE = actualWorkTsActStartDate;
							actualWork.TS_ACT_VALUE = timesheetEntry.Duration * 60 * 1000;
							timesheet.Actuals.AddActualsRow(actualWork);
						}
					}
				}

				context.Client.QueueUpdateTimesheet(Guid.NewGuid(), TS_UID, timesheet);
			}
		}

		private static void LoadLineClasses()
		{
			_lineClasses = GetLineClasses();
		}

		private static Guid GetLineClass(TimesheetEntry timesheetEntry)
		{
			return timesheetEntry.IsAdministrative ? _lineClasses[timesheetEntry.Task] : _lineClasses["Standard"];
		}

		private static Dictionary<string, Guid> GetLineClasses()
		{
			var cache = new MemoryCache("static");
			var value = (Dictionary<string, Guid>)cache.Get(Constants.LineClassCacheKey);
			if (value == null)
			{
				using (var context = new ImpersonationContext<AdminClient, SvcAdmin.Admin>(Timesheetify.CurrentAccountName))
				{
					var lineclasses = context.Client.ReadLineClasses(LineClassType.All, LineClassState.Enabled);
					value = lineclasses
						.LineClasses
						.Rows
						.Cast<DataRow>()
						.ToDictionary(lineClassesRow => lineClassesRow["TS_LINE_CLASS_NAME"].ToString(),
							lineClassesRow => new Guid(lineClassesRow["TS_LINE_CLASS_UID"].ToString()));

					cache.Add(Constants.LineClassCacheKey, value, DateTimeOffset.Now.AddHours(16));
				}
			}
			return value;
		}

		private static TimesheetDataSet GetTimesheet(ImpersonationContext<TimeSheetClient, TimeSheet> context, TimePeriodDataSet.TimePeriodsRow timesheetPeriod, out Guid TS_UID)
		{
			var timesheet = context.Client.ReadTimesheetByPeriod(context.UserUid, timesheetPeriod.WPRD_UID, Navigation.Current);
			TS_UID = Guid.Empty;
			if (timesheet == null)
			{
				var timesheetDs = new TimesheetDataSet();
				var headersRow = timesheetDs.Headers.NewHeadersRow();

				headersRow.RES_UID = context.UserUid;
				headersRow.TS_UID = Guid.NewGuid();
				headersRow.WPRD_UID = timesheetPeriod.WPRD_UID;
				headersRow.TS_CREATOR_RES_UID = context.UserUid;
				headersRow.TS_ENTRY_MODE_ENUM = (byte)TimesheetEnum.EntryMode.Weekly;
				timesheetDs.Headers.AddHeadersRow(headersRow);

				context.Client.CreateTimesheet(timesheetDs, PreloadType.None);
				timesheet = context.Client.ReadTimesheet(headersRow.TS_UID);

				TS_UID = headersRow.TS_UID;
			}
			else
			{
				TS_UID = timesheet.Headers[0].TS_UID;
			}
			return timesheet;
		}

		private static TimePeriodDataSet.TimePeriodsRow GetTimesheetPeriod(DateTime? startDate)
		{
			using (var context = new ImpersonationContext<AdminClient, SvcAdmin.Admin>(Timesheetify.CurrentAccountName))
			{
				try
				{
					var periods = context.Client.ReadPeriods(PeriodState.Open);

					foreach (TimePeriodDataSet.TimePeriodsRow periodsTimePeriod in periods.TimePeriods.Rows)
						if (periodsTimePeriod.WPRD_START_DATE.Date.Equals((startDate ?? GetFirstDayOfWeek()).Date))
							return periodsTimePeriod;
				}
				catch (Exception e)
				{

					if (e is FaultException)
					{
						var xml = ((FaultException)e).CreateMessageFault().GetDetail<SvcAdmin.ServerExecutionFault>().ExceptionDetails.InnerXml;
						throw new Exception(xml);
					}

					throw;
				}

			}

			return null;
		}

		public static DateTime GetFirstDayOfWeek()
		{
			var firstDayInWeek = DateTime.Now;
			while (firstDayInWeek.DayOfWeek != DayOfWeek.Monday)
				firstDayInWeek = firstDayInWeek.AddDays(-1);

			return firstDayInWeek;
		}

		internal static List<ResourceAssignment> GenerateTogglData(string accountName)
		{
			var startCriteria = DateTime.Now.AddMonths(-13);
			var assignments = new List<ResourceAssignment>();

			using (var context = new ImpersonationContext<ResourceClient, Resource>(accountName))
			{
				var resourceAssignmentDataSet = new ResourceAssignmentDataSet();
				var filter = new Filter
				{
					FilterTableName = resourceAssignmentDataSet.ResourceAssignment.TableName,
					Fields = new Filter.FieldCollection
					{
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.PROJ_NAMEColumn.ColumnName),
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.PROJ_UIDColumn.ColumnName),
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.ASSN_UIDColumn.ColumnName),
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.TASK_NAMEColumn.ColumnName),
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.TASK_UIDColumn.ColumnName),
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.RES_NAMEColumn.ColumnName),
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.ASSN_START_DATEColumn.ColumnName),
						new Filter.Field(resourceAssignmentDataSet.ResourceAssignment.TableName, resourceAssignmentDataSet.ResourceAssignment.ASSN_FINISH_DATEColumn.ColumnName),

					},

					Criteria = new Filter.LogicalOperator(Filter.LogicalOperationType.And,
						new Filter.FieldOperator(Filter.FieldOperationType.Equal, resourceAssignmentDataSet.ResourceAssignment.RES_UIDColumn.ColumnName, context.UserUid),
						new Filter.FieldOperator(Filter.FieldOperationType.GreaterThan, resourceAssignmentDataSet.ResourceAssignment.ASSN_START_DATEColumn.ColumnName, startCriteria))
				};

				try
				{
					resourceAssignmentDataSet = context.Client.ReadResourceAssignments(filter.GetXml());
				}
				catch (Exception e)
				{
					if (e is FaultException)
					{
						var xml = ((FaultException)e).CreateMessageFault().GetDetail<SvcResource.ServerExecutionFault>()
							.ExceptionDetails.InnerXml;
						throw new Exception(xml);
					}
					throw;
				}

				for (var i = 0; i < resourceAssignmentDataSet.ResourceAssignment.Rows.Count; i++)
				{
					var row = resourceAssignmentDataSet.ResourceAssignment.Rows[i];

					var start = (DateTime)row["ASSN_START_DATE"];
					var end = (DateTime)row["ASSN_FINISH_DATE"];

					if (start > startCriteria || start == end)
					{
						var projectName = row["PROJ_NAME"].ToString();

						assignments.Add(new ResourceAssignment
						{
							ProjectName = projectName,
							TaskName = row["TASK_NAME"].ToString(),
							ProjectUid = (Guid)row["PROJ_UID"],
							TaskUid = (Guid)row["TASK_UID"],
							AssignmentUid = (Guid)row["ASSN_UID"],
							End = end
						});
					}
				}
			}

			AddAdministrativeTasks(assignments);

			return assignments;
		}

		private static void AddAdministrativeTasks(List<ResourceAssignment> assignments)
		{
			using (var context = new ImpersonationContext<AdminClient, SvcAdmin.Admin>(Timesheetify.CurrentAccountName))
			{
				var tsLineClassDs = context.Client.ReadLineClasses(LineClassType.AllNonProject, LineClassState.Enabled);

				var result = tsLineClassDs
					 .LineClasses
					 .Rows
					 .Cast<DataRow>()
					 .ToDictionary(lineClassesRow => lineClassesRow["TS_LINE_CLASS_NAME"].ToString(), lineClassesRow => new Guid(lineClassesRow["TS_LINE_CLASS_UID"].ToString()));

				assignments.AddRange(result.Select(a => new ResourceAssignment
				{
					TaskName = a.Key,
					End = DateTime.Now,
					ProjectName = Constants.AdministrativeWork,
					TaskUid = a.Value,
					ProjectUid = Guid.Empty,
					AssignmentUid = Guid.Empty
				}));
			}
		}
	}
}
