namespace TogglToTimesheet.DTO
{
    public class TimesheetToTogglResult
    {
        public TimesheetToTogglResult() { }

        public TimesheetToTogglResult(bool keyNotFound)
        {
            KeyNotFound = keyNotFound;
        }

        public ProjectsResult ProjectsResult { get; set; }
        public TagsResult TagsResult { get; set; }
        public TimesheetToTogglResult(ProjectsResult projectsResult, TagsResult tagsResult)
        {
            ProjectsResult = projectsResult;
            TagsResult = tagsResult;
        }

        public bool IsUpToDate => ProjectsResult.AddedProjects == 0 && ProjectsResult.ArchivedProjects == 0 && TagsResult.AddedTags == 0 && TagsResult.RemovedTags == 0;
        public bool KeyNotFound { get; set; }
    }
    public class ProjectsResult
    {
        public ProjectsResult(int addedProjects, int archivedProjects)
        {
            AddedProjects = addedProjects;
            ArchivedProjects = archivedProjects;
        }

        public int AddedProjects { get; set; }
        public int ArchivedProjects { get; set; }
    }

    public class TagsResult
    {
        public TagsResult(int addedTags, int removedTags)
        {
            AddedTags = addedTags;
            RemovedTags = removedTags;
        }

        public int AddedTags { get; set; }
        public int RemovedTags { get; set; }
    }
}
