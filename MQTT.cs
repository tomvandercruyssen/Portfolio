// Deze klasse heb ik toegevoegd zodat communicatie tussen PLCs en de server mogelijk is via MQTT in de plaats van OPC UA.
// Om databasebewerkingen sneller uit te voeren, had ik voorgesteld om raw SQL te implementeren in plaats van het langzamere Entity Framework.
using Dapper;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Server;
using MySql.Data.MySqlClient;
using Serilog;
using SharedLib.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KreontaEdgeService.Services
{
    public class MQTTGathering
    {

        private static string conn = new ConnectionString()._connectionString;

        private readonly LoggingService _logger;

        private static readonly int MessageCounter = 0;
        private static InstantTransmission _IT;
        private static int port ;

        private static readonly MqttFactory mqttFactory = new();
        private MqttServerOptions mqttServerOptions = new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(1883).Build();
        private static MqttServer mqttServer;
        private static bool enabled = false;
        public void MakeMQTT()
        {
            try
            {
                if (!enabled)
                {
                    mqttServerOptions = new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(port).Build();
                    mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);
                    mqttServer.ValidatingConnectionAsync += (e) => onConnectedClient(e);
                    mqttServer.InterceptingPublishAsync += (e) => OnNewMessage(e);
                    mqttServer.StartAsync();
                    mqttServer.StartAsync();
                    enabled = true;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "MQTT -> Broker -> initBroker()");
                throw;
            }
        }

        private static Task onConnectedClient(ValidatingConnectionEventArgs e)
        {
            return e.Password != "pass" || e.Username != "user" ? Task.FromException(new Exception("User not authorized")) : Task.CompletedTask;
        }
        public MQTTGathering(int poort)
        {
            _IT = new InstantTransmission(_logger);
            port = poort;
            MakeMQTT();

        }
        public MQTTGathering()
        {

        }
    
        public Task InterceptApplicationMessagePublishAsync(InterceptingPublishEventArgs args)
        {
            try
            {
                args.ProcessPublish = true;
                OnNewMessage(args);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                //this.logger.Error("An error occurred: {Exception}.", ex);
                return Task.FromException(ex);
            }
        }

        public void stopMQTT()
        {
            if (mqttServer == null)
            {
                MakeMQTT();
            }
            mqttServer.StopAsync();
            enabled = false;
        }

        public void startMQTT()
        {
            if (mqttServer == null)
            {
                MakeMQTT();
            }
            mqttServer.StartAsync();
            enabled = true;
        }

        public void ChangePort(int newPort)
        {
            // Stop the MQTT server
            stopMQTT();

            // Update the port value
            port = newPort;

            // Start the MQTT server on the new port
            startMQTT();
        }

        public static Task OnNewMessage(InterceptingPublishEventArgs args)
        {
            var payload = args.ApplicationMessage?.Payload is null ? null : Encoding.UTF8.GetString(args.ApplicationMessage.Payload);

            Console.WriteLine("MessageId: " + MessageCounter + " - TimeStamp:" + DateTime.Now + "  -- Message: ClientId = " + args.ClientId + ", Topic = " + args.ApplicationMessage?.Topic + ", Payload = " + payload);
            MQTTGathering mQTTGathering = new MQTTGathering();
            UpdateServer(args.ClientId);
            UpdateTag(args.ApplicationMessage?.Topic, args.ClientId);
            AddReading(payload, args.ApplicationMessage?.Topic);
            Log.Logger.Information(
                "Verstuurd om: {TimeStamp}, Bericht:  {payload}",
                DateTime.Now,
                payload);
            return Task.CompletedTask;
        }

        private static Server UpdateServer(string name)
        {
            Server server = new();
            try
            {
                using (MySqlConnection mConnection = new(conn))
                {
                    server = mConnection.Query<Server>(@"Select * from server WHERE Name = '" + name + "'").FirstOrDefault();
                    string Protocol = "MQTT";
                    string TimeZone = "UTC";
                    if (server != null)
                    {
                        _ = mConnection.Execute(@"UPDATE server SET Protocol = @Protocol, TimeZone = @TimeZone WHERE Name = @Name;",
                            new { Protocol, TimeZone, name });
                    }
                    else
                    {
                        Guid serverId = Guid.NewGuid();
                        string serverIdString = serverId.ToString();
                        string ServerCredentialsId = "0011e1ec-c541-4128-a54b-518e51b74e6b";
                        _ = mConnection.Execute(@"INSERT INTO server (ServerId, Enabled, Endpoint, LifeTimeCount, MaxKeepAlive, MaxNotifications, Name, 
                            Protocol, PublishingInterval, ReconnectOnSubscriptionDelete, SessionTimeOut, TimeZone, ServerCredentialsId)
                            VALUES (@ServerIdString, @Enabled, @Endpoint, @LifetimeCount, @MaxKeepAlive, @MaxNotifications, @Name, @Protocol, @PublishingInterval, @ReconnectOnSubscriptionDelete,
                            @SessionTimeOut, @TimeZone, @ServerCredentialsId);", new { serverIdString, Enabled = 1, Endpoint = "", LifetimeCount = 0, MaxKeepAlive = 2, MaxNotifications = 1000, name, Protocol, PublishingInterval = 1000, ReconnectOnSubscriptionDelete = 1, SessionTimeOut = 0, TimeZone, ServerCredentialsId });

                    }
                }

                return server;
            }
            catch (Exception)
            {
                return server;
            }
        }

        public static void UpdateTag(string name, string servername)
        {
            try
            {

                using MySqlConnection mConnection = new(conn);

                Server server = mConnection.Query<Server>(@"Select * from server WHERE Name = '" + servername + "'").FirstOrDefault();

                int rowsAffected = mConnection.Execute(@"UPDATE tag SET Name = @Name, ServerId = @ServerId WHERE  Name = @Name;",
                         new { name, server.ServerId }); ;
                if (rowsAffected == 0)
                {
                    if (rowsAffected == 0)
                    {
                        mConnection.Execute(@"INSERT INTO tag (TagId, Name, NodeId, Type, BufferHours, SamplingInterval, QueueSize, TimestampPOC, DiscardOldest, ServerId)
                                VALUES (@TagId, @Name, @NodeId, @Type, @BufferHours, @SamplingInterval, @QueueSize, @TimestampPOC, @DiscardOldest, @ServerId);",
                                new { TagId = Guid.NewGuid().ToString(), Name = name, NodeId = "MQTT", Type = "String", BufferHours = 1, SamplingInterval = 1000, QueueSize = 1000, TimestampPOC = 0, DiscardOldest = 1, server.ServerId });

                    }

                }
            }
            catch (Exception)
            {
            }
        }

        public static void AddReading(string payload, string tagName)
        {
            using MySqlConnection mConnection = new(conn);

            Tag t = mConnection.Query<Tag>(@"Select * from Tag WHERE Name = '" + tagName + "'").FirstOrDefault();

            Reading r = new()
            {
                Created = DateTime.Now,
                Payload = payload
            };
            mConnection.Execute(@"SET FOREIGN_KEY_CHECKS=0;");
            mConnection.Execute(@"INSERT INTO reading (ReadingId, Created, Quality, StringValue, IntegerValue, UnsignedIntegerValue, FloatValue, TagId, Payload)
                                VALUES (@ReadingId, @Created, @Quality, @StringValue, @IntegerValue, @UnsignedIntegerValue, @FloatValue, @TagId, @Payload);",
                            new { ReadingId = Guid.NewGuid().ToString(), r.Created, Quality = "", StringValue = 0, IntegerValue = 0, UnsignedIntegerValue = 0, FloatValue = 0, t.TagId, payload });

            mConnection.Execute(@"SET FOREIGN_KEY_CHECKS=1;");
            AddWebServiceValuesForReading(r, t.TagId);

        }

        private static void AddWebServiceValuesForReading(Reading reading, string tagId)
        {

            using MySqlConnection mConnection = new(conn);

            List<string> webServiceElementIds = mConnection.Query<string>(@"Select WebServiceElementId from webserviceelement where TagId = '" + tagId + "';").ToList();
            List<WebServiceElementValue> values = new();
            if (!webServiceElementIds.Any())
            {
                return;
            }

            foreach (string elementId in webServiceElementIds)
            {
                string wsId = mConnection.Query<string>(@"Select WebServiceId from WeBserviceElement where WebServiceElementId = '" + elementId + "';").FirstOrDefault();
                WebService w = mConnection.Query<WebService>(@"Select * from webservice where WebServiceId = '" + wsId + "';").FirstOrDefault();
                if (w.Enabled)
                {
                    WebServiceElementValue value = new()
                    {
                        Created = reading.Created,
                        Value = reading.Payload,
                        WebServiceElement = mConnection.Query<WebServiceElement>(@"Select * from WebServiceElement where WebServiceElementId = '" + elementId + "';").FirstOrDefault()
                    };
                    values.Add(value);
                    mConnection.Execute(@"INSERT INTO WebServiceElementValue (WebServiceElementValueId, Created, Value, WebServiceElementId) VALUES (@WebServiceElementValueId, @Created, @Value, @WebServiceElementId);", new { value.WebServiceElementValueId, value.Created, value.Value, value.WebServiceElement.WebServiceElementId });
                }

                if (w.Instant)
                {
                    //_webServiceTimer = new WebServiceTimer((w.WebServiceId), _logger);
                    _IT.makeCall(w.WebServiceId);
                    // _dtps.GetCallReady();
                }

            };

        }
    }
}
