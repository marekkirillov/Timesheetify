using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Timesheetify.Models
{
    public class Model
    {
        public Model()
        {
            ShowSuccess = true;
        }

        public string Name { get; set; }
        public string Department { get; set; }
        public string ApiKey { get; set; }
        public bool ShowSuccess { get; set; }
        public string Error { get; set; }

        public string Success { get; set; }
        public string Password { get; set; }

        public IList<SelectListItem> Weeks { get; set; }
        public DateTime? SelectedWeek { get; set; }
   }
}