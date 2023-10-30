// Deze klasse is een deel van mijn bachelorproef. Hier worden basis API calls gemaakt in GraphQL voor te vergelijken met de andere architecturen.

using Dapper;
using HotChocolate.Types;
using KreontaEdgeService.Services;
using MySql.Data.MySqlClient;
using SharedLib.Data;
using SharedLib.DTO;
using SharedLib.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KreontaEdgeService.GraphQL
{
    public class Query
    {
        private readonly EdgeDataContext _dbContext;
        private readonly string conn = new ConnectionString()._connectionString;

        public Query(EdgeDataContext dbContext)
        {
            _dbContext = dbContext;
        }

        public List<WebServiceDTO> GetAllWebServices()
        {
            using (MySqlConnection mConnection = new MySqlConnection(conn))
            {

                List<WebService> WebServices = mConnection.Query<WebService>(@"Select * from WebService;").ToList();
                foreach (WebService WebService in WebServices)
                {
                    string destinationId = mConnection.Query<string>(@"Select DestinationId from WebService where WebServiceId = '" + WebService.WebServiceId + "';").FirstOrDefault();
                    string groupId = mConnection.Query<string>(@"Select WebServiceGroupId from WebService where WebServiceId = '" + WebService.WebServiceId + "';").FirstOrDefault();
                    WebService.Destination = mConnection.Query<Destination>(@"Select * from Destination where DestinationId = '" + destinationId + "';").FirstOrDefault();
                    WebService.Group = mConnection.Query<WebServiceGroup>(@"Select * from WebServiceGroup where WebServiceGroupId = '" + groupId + "';").FirstOrDefault();
                    WebService.Elements = mConnection.Query<WebServiceElement>(@"Select * from WebServiceElement where WebServiceId = '" + WebService.WebServiceId + "';").ToList();
                    WebService.Calls = mConnection.Query<WebServiceCall>(@"Select * from WebServiceCall where WebServiceId = '" + WebService.WebServiceId + "';").ToList();
                }
                return WebServices.Select(ws => new WebServiceDTO(ws)).ToList();
            }
        }

        public WebServiceDTO FindWebserviceById(Guid id)
        {
            using (MySqlConnection mConnection = new MySqlConnection(conn))
            {

                WebService webService = mConnection.Query<WebService>(@"Select * from WebService where WebServiceId = '" + id.ToString() + "';").FirstOrDefault();
                string destinationId = mConnection.Query<string>(@"Select DestinationId from WebService where WebServiceId = '" + webService.WebServiceId + "';").FirstOrDefault();
                string groupId = mConnection.Query<string>(@"Select WebServiceGroupId from WebService where WebServiceId = '" + webService.WebServiceId + "';").FirstOrDefault();
                webService.Destination = mConnection.Query<Destination>(@"Select * from Destination where DestinationId = '" + destinationId + "';").FirstOrDefault();
                webService.Group = mConnection.Query<WebServiceGroup>(@"Select * from WebServiceGroup where WebServiceGroupId = '" + groupId + "';").FirstOrDefault();
                webService.Elements = mConnection.Query<WebServiceElement>(@"Select * from WebServiceElement where WebServiceId = '" + webService.WebServiceId + "';").ToList();
                webService.Calls = mConnection.Query<WebServiceCall>(@"Select * from WebServiceCall where WebServiceId = '" + webService.WebServiceId + "';").ToList();

                return (new WebServiceDTO(webService));
            }
        }
    }

    public class Mutation
    {

        private readonly EdgeDataContext _dbContext;
        private readonly string conn = new ConnectionString()._connectionString;
        public WebServiceDTO UpdateWebService(WebServiceDTO req)
        {

            WebService ws = req.FromDTO();
            WebServiceDTO dto = req;
            using (MySqlConnection mConnection = new MySqlConnection(conn))
            {

                Destination dest = mConnection.Query<Destination>(@"Select * from Destination where DestinationId = '" + req.DestinationId + "';").FirstOrDefault();
                WebServiceGroup grp = mConnection.Query<WebServiceGroup>(@"Select * from WebServiceGroup where WebServiceGroupId = '" + req.WebServiceGroupId + "';").FirstOrDefault();
                ws.Group = grp;
                int rowsAffected = mConnection.Execute(@"UPDATE webservice SET Name = @Name, Delay = @Delay, Bundle = @Bundle, KeepSequence = @KeepSequence, RetryCount = @RetryCount
                                         , RetryDelay = @RetryDelay, HistoryHours = @HistoryHours, Enabled = @Enabled, Instant = @Instant, DestinationId = @DestinationId, WebServiceGroupId = @WebServiceGroupId WHERE WebServiceId = @WebServiceId;",
                        new { ws.Name, ws.Delay, ws.Bundle, ws.KeepSequence, ws.RetryCount, ws.RetryDelay, ws.HistoryHours, ws.Enabled, ws.Instant, dest.DestinationId, ws.Group.WebServiceGroupId, ws.WebServiceId });
                if (rowsAffected == 0)
                {
                    //mConnection.Execute(@"UPDATE globalconfig SET Value = " + 60 + " WHERE WebServiceId = '" + ws.WebServiceId + "';");
                    mConnection.Execute(@"INSERT INTO WebService (WebServiceId, Name, Delay, Bundle, KeepSequence, RetryCount, RetryDelay, HistoryHours, Enabled, Instant, DestinationId, WebServiceGroupId)
                             VALUES (@WebServiceId, @Name, @Delay, @Bundle, @KeepSequence, @RetryCount, @RetryDelay, @HistoryHours, @Enabled, @Instant,  @DestinationId, @WebServiceGroupId);",
                        new { ws.WebServiceId, ws.Name, ws.Delay, ws.Bundle, ws.KeepSequence, ws.RetryCount, ws.RetryDelay, ws.HistoryHours, ws.Enabled, ws.Instant, dest.DestinationId, grp.WebServiceGroupId });
                }
                return dto;
            }
        }

        public string RemoveWebService(Guid id)
        {
            using (MySqlConnection mConnection = new MySqlConnection(conn))
            {

                WebService ws = mConnection.Query<WebService>(@"Select * from WebService where WebServiceId = '" + id.ToString() + "';").FirstOrDefault();
                mConnection.Execute("DELETE FROM  WebService where WebServiceId = '" + ws.WebServiceId + "';");
                return ("verwijderd");
            }
        }
    }

    public class WebServiceType : ObjectType<WebServiceDTO>
    {
        protected override void Configure(IObjectTypeDescriptor<WebServiceDTO> descriptor)
        {
            descriptor.Field(x => x.WebServiceId).Type<IdType>();
            descriptor.Field(x => x.Name).Type<StringType>();
            descriptor.Field(x => x.Delay).Type<IntType>();
            descriptor.Field(x => x.Bundle).Type<BooleanType>();
            descriptor.Field(x => x.KeepSequence).Type<BooleanType>();
            descriptor.Field(x => x.RetryCount).Type<IntType>();
            descriptor.Field(x => x.RetryDelay).Type<IntType>();
            descriptor.Field(x => x.HistoryHours).Type<IntType>();
            descriptor.Field(x => x.Enabled).Type<BooleanType>();
            descriptor.Field(x => x.Instant).Type<BooleanType>();
            descriptor.Field(x => x.DestinationId).Type<StringType>();
            descriptor.Field(x => x.WebServiceGroupId).Type<StringType>();
            descriptor.Field(x => x.ElementIds).Type<ListType<StringType>>();
            descriptor.Field(x => x.CallIds).Type<ListType<StringType>>();
            // Add other fields here
        }
    }

    public class QueryType : ObjectType<Query>
    {
        protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
        {
            descriptor.Field(q => q.GetAllWebServices())
                .Type<NonNullType<ListType<NonNullType<WebServiceType>>>>().Name("GetAllWebServices");

            descriptor.Field(q => q.FindWebserviceById(default)).Type<WebServiceType>()
                .Argument("id", arg => arg.Type<NonNullType<IdType>>()).Name("FindWebServiceById");
        }
    }

    public class MutationType : ObjectType<Mutation>
    {
        protected override void Configure(IObjectTypeDescriptor<Mutation> descriptor)
        {
            descriptor.Field(m => m.UpdateWebService(default)).Type<WebServiceType>()
                .Argument("req", arg => arg.Type<WebServiceDTOInput>()).Name("UpdateWebService");

            descriptor.Field(m => m.RemoveWebService(default)).Type<NonNullType<StringType>>()
                .Argument("id", arg => arg.Type<NonNullType<IdType>>()).Name("RemoveWebService");
        }
    }

    public class WebServiceInputType : InputObjectType<WebServiceDTO>
    {
        protected override void Configure(IInputObjectTypeDescriptor<WebServiceDTO> descriptor)
        {
            descriptor.Field(x => x.WebServiceId).Type<IdType>();
            descriptor.Field(x => x.Name).Type<StringType>();
            descriptor.Field(x => x.Delay).Type<IntType>();
            descriptor.Field(x => x.Bundle).Type<BooleanType>();
            descriptor.Field(x => x.KeepSequence).Type<BooleanType>();
            descriptor.Field(x => x.RetryCount).Type<IntType>();
            descriptor.Field(x => x.RetryDelay).Type<IntType>();
            descriptor.Field(x => x.HistoryHours).Type<IntType>();
            descriptor.Field(x => x.Enabled).Type<BooleanType>();
            descriptor.Field(x => x.Instant).Type<BooleanType>();
            descriptor.Field(x => x.DestinationId).Type<StringType>();
            descriptor.Field(x => x.WebServiceGroupId).Type<StringType>();
            descriptor.Field(x => x.ElementIds).Type<ListType<StringType>>();
            descriptor.Field(x => x.CallIds).Type<ListType<StringType>>();

        }
    }
    public class WebServiceDTOInput : InputObjectType<WebServiceDTO>
    {
        protected override void Configure(IInputObjectTypeDescriptor<WebServiceDTO> descriptor)
        {
            descriptor.Field(x => x.WebServiceId).Type<IdType>();
            descriptor.Field(x => x.Name).Type<StringType>();
            descriptor.Field(x => x.Delay).Type<IntType>();
            descriptor.Field(x => x.Bundle).Type<BooleanType>();
            descriptor.Field(x => x.KeepSequence).Type<BooleanType>();
            descriptor.Field(x => x.RetryCount).Type<IntType>();
            descriptor.Field(x => x.RetryDelay).Type<IntType>();
            descriptor.Field(x => x.HistoryHours).Type<IntType>();
            descriptor.Field(x => x.Enabled).Type<BooleanType>();
            descriptor.Field(x => x.Instant).Type<BooleanType>();
            descriptor.Field(x => x.DestinationId).Type<StringType>();
            descriptor.Field(x => x.WebServiceGroupId).Type<StringType>();
            descriptor.Field(x => x.ElementIds).Type<ListType<StringType>>();
            descriptor.Field(x => x.CallIds).Type<ListType<StringType>>();

        }
    }

}
