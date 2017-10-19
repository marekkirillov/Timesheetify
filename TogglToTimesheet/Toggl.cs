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
		private const string DefaultWorkspace = "Net Group";
		private static string _workspaceId;

		private readonly HttpClient _httpClient;

		public Toggl(string apiKey, string workspaceId)
		{
			_workspaceId = workspaceId;
			var togglApiPwd = apiKey + ":api_token";
			var togglPwdB64 = Convert.ToBase64String(Encoding.Default.GetBytes(togglApiPwd.Trim()));
			var toggleAuthHeader = "Basic " + togglPwdB64;

			_httpClient = new HttpClient
			{
				DefaultRequestHeaders = { { "Authorization", toggleAuthHeader } }
			};
		}

		public TogglEntry[] GetWeekEntries(DateTime? startDate = null)
		{
			DateTime endDate;

			var togglWorkspaceId = GetTogglWorkspaceId();

			if (togglWorkspaceId == null)
				throw new Exception("Could not find workspace 'Net Group' from Toggl");

			if (startDate == null)
			{
				startDate = DateTime.Today.AddDays(-1 * (int)DateTime.Today.DayOfWeek).Date;
				endDate = DateTime.Now.AddDays(1);
			}
			else
			{
				//todo: peaks kontrollima, et kui on sama nädal ka?
				endDate = startDate.Value.AddDays(6);
			}

			var togglEntries = GetEntries(startDate.Value, endDate)?.Where(e => e.wid.Equals(togglWorkspaceId) && e.tags != null && e.tags.Any()).ToArray();
			return AddProjectNames(togglEntries, togglWorkspaceId);
		}

		public TimesheetToTogglResult SyncProjectsAndTags(TogglProjectsAndTags data, bool cleanup)
		{
			var togglWorkspaceId = GetTogglWorkspaceId();

			if (togglWorkspaceId == null)
				throw new Exception("Could not find workspace 'Net Group' from Toggl");

			return new TimesheetToTogglResult(AddOrUpdateProjects(data.Projects, togglWorkspaceId, cleanup), AddOrUpdateTags(data.Tags, togglWorkspaceId, cleanup));
		}

		private string GetTogglWorkspaceId()
		{
			if (!string.IsNullOrWhiteSpace(_workspaceId))
				return _workspaceId;

			var workspaces = TogglGet<TogglWorkspace[]>(TogglWorkspacesUrl);
			var workspace = workspaces.FirstOrDefault(w => w.name == DefaultWorkspace);
			return workspace?.id;
		}

		private ProjectsResult AddOrUpdateProjects(List<string> dataProjects, string wid, bool cleanup)
		{
			var projects = TogglGet<TogglProject[]>(TogglWorkspaceItemsUrl, wid, "projects")?.ToList() ?? new List<TogglProject>();
			var projectNames = projects.Select(p => p.name).ToList();
			var projectsToArchive = projects.Where(t => !dataProjects.Contains(t.name)).ToList();

			var i = 0;
			foreach (var name in dataProjects)
			{
				if (projectNames.Contains(name))
				{
					var project = projects.First(p => p.name == name);

					if (project.active)
						continue;

					project.active = true;
					TogglPut($"{TogglProjectUrl}/{project.id}", project);
				}
				else
					TogglPost<TogglProject, object>(TogglProjectUrl, new { project = new TogglProject { name = name, wid = wid } });

				i++;
			}

			var j = 0;

			if (cleanup)
			{
				foreach (var togglProject in projectsToArchive)
				{
					togglProject.active = false;
					TogglPut($"{TogglProjectUrl}/{togglProject.id}", new { project = togglProject });
					j++;
				}
			}

			return new ProjectsResult(i, j);
		}

		public void ValidateApiKey(string apikey)
		{
			if (apikey.Length != 32)
				throw new Exception("API token length has to be 32 characters!");

			try
			{
				GetAllToggleWorkspaces();
			}
			catch (Exception e)
			{
				if (e.InnerException != null && e.InnerException.ToString().Contains(HttpStatusCode.Forbidden.ToString()))
				{
					throw new Exception("Error validating API token!");
				}
				throw new Exception("Error validating API token:" + e.Message);
			}
		}

		public IList<TogglWorkspace> GetAllToggleWorkspaces()
		{
			return TogglGet<TogglWorkspace[]>(TogglWorkspacesUrl).ToList();
		}

		private TagsResult AddOrUpdateTags(List<string> dataTags, string wid, bool cleanup)
		{
			var tags = TogglGet<TogglTag[]>(TogglWorkspaceItemsUrl, wid, "tags")?.ToList() ?? new List<TogglTag>();
			var tagNames = tags.Select(t => t.name).ToList();
			var tagsToRemove = tags.Where(t => t.name.StartsWith("PS:") && !dataTags.Contains(t.name)).ToList();

			var i = 0;
			foreach (var tag in dataTags)
			{
				if (tagNames.Contains(tag))
					continue;

				TogglPost<TogglTag, object>(TogglTagsUrl, new { tag = new TogglTag { name = tag, wid = wid } });
				i++;
			}


			var j = 0;
			if (cleanup)
			{
				foreach (var togglTag in tagsToRemove)
				{
					TogglDelete($"{TogglTagsUrl}/{togglTag.id}");
					j++;
				}
			}

			return new TagsResult(i, j);
		}

		private TogglEntry[] GetEntries(DateTime startDate, DateTime endDate)
		{
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
			var content = GetContent(data);
			var result = _httpClient.PostAsync(url, content).Result;
			var response = result.Content.ReadAsStringAsync().Result;

			return JsonConvert.DeserializeObject<T>(response);
		}

		private static StringContent GetContent<T1>(T1 data)
		{
			var requestData =
				JsonConvert.SerializeObject(data, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
			var content = new StringContent(requestData);

			content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
			return content;
		}

		private T TogglGet<T>(string url, params object[] @params)
		{
			var result = _httpClient.GetStringAsync(string.Format(url, @params)).Result;
			return JsonConvert.DeserializeObject<T>(result);
		}

		private void TogglDelete(string url)
		{
			_httpClient.DeleteAsync(url);
		}

		private void TogglPut<T>(string url, T data)
		{
			var content = GetContent(data);
			var result = _httpClient.PutAsync(url, content).Result;
			var response = result.Content.ReadAsStringAsync().Result;
			var _ = JsonConvert.DeserializeObject<T>(response);
		}

	}
}
