using System;
using System.Net.Mail;

namespace Timesheetify.Helpers
{
	public static class EmailSender
	{
		public static void Send(string body)
		{
			try
			{
				var mail = new MailMessage("timesheetify@timesheetify.com", "marek.kirillov@netgroup.ee");
				var client = new SmtpClient();

				mail.Subject = "ERROR in Timesheetify";
				mail.Body = body;

				client.Send(mail);
			}
			catch (Exception e)
			{
				// ignored
			}
		}
	}
}