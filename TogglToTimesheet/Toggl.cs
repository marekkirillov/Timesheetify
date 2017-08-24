namespace TogglToTimesheet
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Web;
    using DTO;
    using Newtonsoft.Json;

    public class Toggl
    {
        private const string TogglUrl = "https://www.toggl.com/api/v8/";
        private const string TogglTimeEntriesUrl = TogglUrl + "time_entries?start_date={0}&end_date={1}";
        private const string TogglWorkspaceItemsUrl = TogglUrl + "workspaces/{0}/{1}";
        private const string TogglWorkspacesUrl = TogglUrl + "workspaces";
        private const string TogglTagsUrl = TogglUrl + "tags";
        private const string TogglProjectUrl = TogglUrl + "projects";
        private const string Workspace = "Net Group";

        private readonly HttpClient _httpClient;

        public Toggl(string apiKey)
        {
            var togglApiPwd = apiKey + ":api_token";
            var togglPwdB64 = Convert.ToBase64String(Encoding.Default.GetBytes(togglApiPwd.Trim()));
            var toggleAuthHeader = "Basic " + togglPwdB64;

            _httpClient = new HttpClient
            {
                DefaultRequestHeaders = { { "Authorization", toggleAuthHeader } }
            };
        }

        public TogglEntry[] GetCurrentWeekEntries()
        {
            var workspace = GetTogglWorkspace();

            if (workspace == null)
                throw new Exception("Could not find workspace 'Net Group' from Toggl");

            var togglEntries = GetEntries()?.Where(e => e.wid.Equals(workspace.id) && e.tags != null && e.tags.Any()).ToArray();
            return AddProjectNames(togglEntries, workspace.id);
        }

        public Tuple<int, int> SyncProjectsAndTags(TogglProjectsAndTags data)
        {
            var workspace = GetTogglWorkspace();

            if (workspace == null)
                throw new Exception("Could not find workspace 'Net Group' from Toggl");

            return new Tuple<int, int>(AddOrUpdateProjects(data.Projects, workspace.id), AddOrUpdateTags(data.Tags, workspace.id));
        }

        private TogglWorkspace GetTogglWorkspace()
        {
            var workspaces = TogglGet<TogglWorkspace[]>(TogglWorkspacesUrl);
            var workspace = workspaces.FirstOrDefault(w => w.name == Workspace);
            return workspace;
        }

        private int AddOrUpdateProjects(List<string> dataProjects, string wid)
        {
            var projects = TogglGet<TogglProject[]>(TogglWorkspaceItemsUrl, wid, "projects")?.Select(t => t.name).ToList() ?? new List<string>();
            var i = 0;

            foreach (var project in dataProjects)
            {
                if (projects.Contains(project))
                    continue;

                TogglPost<TogglProject, object>(TogglProjectUrl, new { project = new TogglProject { name = project, wid = wid } });
                i++;
            }
            return i;
        }

        private int AddOrUpdateTags(List<string> dataTags, string wid)
        {
            var tags = TogglGet<TogglTag[]>(TogglWorkspaceItemsUrl, wid, "tags")?.Select(t => t.name).ToList() ?? new List<string>();
            var i = 0;
            foreach (var tag in dataTags)
            {
                if (tags.Contains(tag))
                    continue;

                TogglPost<TogglTag, object>(TogglTagsUrl, new { tag = new TogglTag { name = tag, wid = wid } });
                i++;
            }
            return i;
        }

        private TogglEntry[] GetEntries()
        {

            var startDate = DateTime.Today.AddDays(-1 * (int)(DateTime.Today.DayOfWeek)).Date;
            var endDate = DateTime.Now.AddDays(1);

            var startDateFormatted = HttpUtility.UrlEncode(startDate.ToString("o"));
            var endDateFormatted = HttpUtility.UrlEncode(endDate.ToString("o"));

            return TogglGet<TogglEntry[]>(TogglTimeEntriesUrl, startDateFormatted, endDateFormatted);
        }

        private TogglEntry[] AddProjectNames(TogglEntry[] values, string workspaceId)
        {
            var projects = TogglGet<TogglProject[]>(TogglWorkspaceItemsUrl, workspaceId, "projects");

            foreach (var togglEntry in values)
                togglEntry.TogglProject = projects.FirstOrDefault(p => p.id == togglEntry.pid);

            return values;
        }


        private T TogglPost<T, T1>(string url, T1 data)
        {
            var requestData = JsonConvert.SerializeObject(data, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var content = new StringContent(requestData);

            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");

            var result = _httpClient.PostAsync(url, content).Result;
            var response = result.Content.ReadAsStringAsync().Result;

            return JsonConvert.DeserializeObject<T>(response);
        }

        private T TogglGet<T>(string url, params object[] @params)
        {
            var result = _httpClient.GetStringAsync(string.Format(url, @params)).Result;
            return JsonConvert.DeserializeObject<T>(result);
        }
    }
}
