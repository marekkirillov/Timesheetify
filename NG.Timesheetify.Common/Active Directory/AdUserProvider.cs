namespace NG.Timesheetify.Common.Active_Directory
{
    using System.DirectoryServices;
    using System.DirectoryServices.AccountManagement;
    using System.Linq;

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
            return new User
            {
                DisplayName = adSearchResult.Properties["displayname"][0].ToString(),
                AccountName = adSearchResult.Properties["samaccountname"][0].ToString()
            };
        }

        public static User GetUserByIdentityName(string name)
        {
            return GetUserByAccountName(name.Split('\\').Last().Trim());
        }
    }
}
