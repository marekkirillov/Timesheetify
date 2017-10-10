namespace TogglToTimesheet
{
    using System;
    using System.Globalization;
    using System.ServiceModel;
    using System.ServiceModel.Web;
    using Common;
    using SvcResource;

    public class ImpersonationContext<T, T1> : IDisposable where T : ClientBase<T1> where T1 : class
    {
        private readonly OperationContextScope _operationContextScope = null;
        public readonly T Client;
        public readonly Guid UserUid;
        private readonly string _accountName;

        public ImpersonationContext(string accountName)
        {
            _accountName = Constants.AccountNamePrefix + accountName;

            UserUid = GetUserUid();
            var contextString = GetImpersonationContext(true, _accountName, UserUid, Guid.Empty, Guid.Empty, null, null);

            Client = EndpointProvider<T, T1>.ConfigureEndpoint();
            _operationContextScope = new OperationContextScope(Client.InnerChannel);

            ManageHeaders(contextString);
        }

        private static void ManageHeaders(string contextString)
        {
            WebOperationContext.Current.OutgoingRequest.Headers.Remove("PjAuth");
            WebOperationContext.Current.OutgoingRequest.Headers.Add("PjAuth", contextString);

            ManageXForms();
        }

        private static void ManageXForms()
        {
            WebOperationContext.Current.OutgoingRequest.Headers.Remove("X-FORMS_BASED_AUTH_ACCEPTED");
            WebOperationContext.Current.OutgoingRequest.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
        }

        private static string GetImpersonationContext(bool isWindowsUser, String userNTAccount, Guid userGuid, Guid trackingGuid, Guid siteId, CultureInfo languageCulture, CultureInfo localeCulture)
        {
            var contextInfo = new Microsoft.Office.Project.Server.Library.PSContextInfo(isWindowsUser, userNTAccount, userGuid, trackingGuid, siteId, languageCulture, localeCulture);
            return Microsoft.Office.Project.Server.Library.PSContextInfo.SerializeToString(contextInfo);
        }

        private Guid GetUserUid()
        {
            var resourceClient = EndpointProvider<ResourceClient, Resource>.ConfigureEndpoint();
            using (var scope = new OperationContextScope(resourceClient.InnerChannel))
            {
                ManageXForms();

                var resourceUid = Guid.Empty;
                var resourceDs = new ResourceDataSet();

                var filter = new Microsoft.Office.Project.Server.Library.Filter { FilterTableName = resourceDs.Resources.TableName };

                var accountField = new Microsoft.Office.Project.Server.Library.Filter.Field(resourceDs.Resources.TableName, resourceDs.Resources.WRES_ACCOUNTColumn.ColumnName);
                filter.Fields.Add(accountField);

                var op = new Microsoft.Office.Project.Server.Library.Filter.FieldOperator(Microsoft.Office.Project.Server.Library.Filter.FieldOperationType.Equal, resourceDs.Resources.WRES_ACCOUNTColumn.ColumnName, _accountName);
                filter.Criteria = op;

                var filterXml = filter.GetXml();
                resourceDs = resourceClient.ReadResources(filterXml, false);

                if (resourceDs.Resources.Rows.Count > 0)
                    resourceUid = (Guid)resourceDs.Resources.Rows[0]["RES_UID"];

                return resourceUid;
            }
        }

        public void Dispose()
        {
            Client.Close();
            _operationContextScope.Dispose();
        }
    }
}
