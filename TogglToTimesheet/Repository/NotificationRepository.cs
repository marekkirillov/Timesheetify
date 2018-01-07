using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TogglToTimesheet.Data;

namespace TogglToTimesheet.Repository
{
	public class NotificationRepository
	{
		public void Dissmiss(int notificationId, int workerId)
		{
			using (var context = new TimesheetifyEntities())
			{
				var worker = context.Workers.Find(workerId);
				var notification = context.Notifications.Find(notificationId);
				worker.Notifications.Remove(notification);
				context.Workers.AddOrUpdate(worker);
				context.SaveChanges();
			}
		}

		public void Add(int workerId, int notificationId)
		{
			using (var context = new TimesheetifyEntities())
			{
				var worker = context.Workers.Find(workerId);
				var notification = context.Notifications.Find(notificationId);
				worker.Notifications.Add(notification);
				context.Workers.AddOrUpdate(worker);
				context.SaveChanges();
			}
		}

		public int Insert(string heading, string content) 
		{
			using (var context = new TimesheetifyEntities())
			{
				var notification = new Notification
				{
					Heading = heading,
					ContentHTML = content,
				};

				context.Notifications.Add(notification);
				context.SaveChanges();

				return notification.Id;
			}
		}

		public Notification Get(int workerId)
		{
			using (var context = new TimesheetifyEntities())
			{
				return context.Workers.Find(workerId)?.Notifications?.OrderBy(n => n.Id).FirstOrDefault();
			}
		}
	}
}
