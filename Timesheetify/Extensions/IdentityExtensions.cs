using System.Security.Principal;
using TogglToTimesheet.Active_Directory;

namespace Timesheetify.Extensions
{
   public static class IdentityExtensions
   {
      public static bool IsBeetaTeamMember(this IIdentity identity)
      {
         return AdUserProvider.GetUserByIdentityName(identity.Name).Department == TogglToTimesheet.Common.Constants.Beeta;
      }
   }
}