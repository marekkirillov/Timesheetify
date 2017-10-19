namespace TogglToTimesheet.Data
{
	using System;
	using System.Data.Entity.Migrations;
	using System.Linq;

	[Serializable]
	public partial class Worker
	{
		public void SaveChanges()
		{
			using (var context = new TimesheetifyEntities())
			{
				var worker = context.Workers.First(f => f.Id == this.Id);

				worker.Cleanup = this.Cleanup;
				worker.WorkspaceName = this.WorkspaceName;

				context.Workers.AddOrUpdate(worker);
				context.SaveChanges();
			}
		}
	}
}
