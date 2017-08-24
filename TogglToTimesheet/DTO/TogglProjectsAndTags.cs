namespace TogglToTimesheet.DTO
{
    using System.Collections.Generic;

    public class TogglProjectsAndTags
    {
        public TogglProjectsAndTags()
        {
            Projects = new List<string>();
            Tags = new List<string>();
        }

        public List<string> Projects { get; set; }
        public List<string> Tags { get; set; }

        public void AddTags(string tag)
        {
            if (Tags.Contains(tag))
                return;

            Tags.Add(tag);
        }
    }
}
