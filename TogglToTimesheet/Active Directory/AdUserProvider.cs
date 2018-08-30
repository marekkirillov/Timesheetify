using System;

namespace TogglToTimesheet.Active_Directory
{
    using System.DirectoryServices;
    using System.Linq;
    using NG.Timesheetify.Common.Active_Directory;

    public static class AdUserProvider
    {
        private const string LdapDomain = "LDAP://NETGROUPDIGITAL";

        public static User GetUserByAccountName(string username)
        {
            using (var de = new DirectoryEntry(LdapDomain))
            {
                using (var adSearch = new DirectorySearcher(de))
                {
                    adSearch.Filter = $"(sAMAccountName={username})";
                    var adSearchResult = adSearch.FindOne();
                    return CreateUser(adSearchResult);
                }
            }
        }

        private static User CreateUser(SearchResult adSearchResult)
        {
	        var displayName = adSearchResult.Properties["displayname"];
			if(displayName.Count ==0 ) throw new Exception("Could not find 'displayname' value from AD");

	        var samAccountName = adSearchResult.Properties["samaccountname"];
			if(samAccountName.Count == 0) throw new Exception("Could not find 'samaccountname' value from AD");

	        return new User
            {
                DisplayName = displayName[0].ToString(),
                AccountName = samAccountName[0].ToString()
            };
        }

        public static User GetUserByIdentityName(string name)
        {
            var user = GetUserByAccountName(name.Split('\\').Last().Trim());
            user.Identity = name;
            return user;
        }
    }
}
