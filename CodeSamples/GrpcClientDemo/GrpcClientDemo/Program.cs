using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using PrecisionNow.Grpc;
using PrecisionNow.Protos;

namespace GrpcClientDemo
{
    class TripledTimeSeries
    {
        public DateTime DateTime { get; set; }
        public double TripledValue { get; set; }
    }

    class TimeSeries
    {
        public DateTime DateTime { get; set; }
        public double Value { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            #region Setup

            using var grpcChannel = ClientHelper.MakeGrpcChannel();

            var channelsClient = new Channels.ChannelsClient(grpcChannel);
            var projectClient = new Project.ProjectClient(grpcChannel);
            var modulesClient = new Modules.ModulesClient(grpcChannel);

            #endregion

            #region See existing channels

            var getChannelsStream = channelsClient.GetChannels(new Empty());

            // the channels list is provided as a stream so that you can see when channels are created or deleted
            // since we only care about the channels that currently exist, we can use the FirstAsync method
            // from the System.Interactive.Async NuGet package
            var getChannelsFirstResponse = await getChannelsStream.ResponseStream
                .ReadAllAsync()
                .FirstAsync();

            var currentChannels = getChannelsFirstResponse.Data;

            #endregion

            #region Creating a channel

            // after running this, the channels list on the project tab should immediately show the new channel
            await channelsClient.CreateChannelAsync(new SchemaDefinition
            {
                TableName = "testing creating a channel",
                TableType = "Output",
                AttributeDefinitions =
                {
                    new AttributeParams
                    {
                        AttributeName = "a string attribute",
                        AttributeTypeName = "String",
                        Unit = new Unit()
                    },
                    new AttributeParams
                    {
                        AttributeName = "a double attribute",
                        AttributeTypeName = "Double",
                        Unit = new Unit()
                    },
                }
            });

            #endregion

            # region Deleting a channel

            // after running this, the channels list on the project tab should immediately show the channel has been removed
            await channelsClient.DeleteChannelAsync(new ChannelName {ChannelName_ = "testing creating a channel"});

            #endregion

            #region Reading from an output channel (reading the results of a module)

            var attributesToRead = new[]
            {
                new ReadChannelRequest.Types.AttributeOptions
                {
                    Name = "DateTime",
                    NameAs = "DateTime"
                },
                new ReadChannelRequest.Types.AttributeOptions
                {
                    Name = "TripledValue",
                    NameAs = "TripledValue"
                }
            };

            var readChannelRequest = new ReadChannelRequest {ChannelName = "multipliedByThree",};
            readChannelRequest.Attributes.Add(attributesToRead);

            var channelDataDecoder = new ChannelDataDecoder<TripledTimeSeries>(attributesToRead);

            var readCompletion = channelsClient.ReadChannel(readChannelRequest).ResponseStream
                .ReadAllAsync()
                .ForEachAsync(channelDataEvent =>
                {
                    switch (channelDataEvent.Event)
                    {
                        // this is the event type for when new data is written to a channel
                        case DataEventType.Add:
                            var decodedData = channelDataDecoder.ReadAll(
                                channelDataEvent.Data.Data.Span,
                                channelDataEvent.Data.Count
                            );
                            // use the new data from the channel however it's needed
                            foreach (var datum in decodedData)
                                Console.WriteLine($"{datum.DateTime}: {datum.TripledValue}");
                            break;

                        // documentation and helper methods for handling other channel events will be added in the future
                    }
                });

            #endregion

            #region Writing to a channel (providing input to a module) in a loop

            var attributesToWrite = new[]
            {
                new AttributeParams
                {
                    AttributeName = "DateTime",
                    AttributeTypeName = "DateTime"
                },
                new AttributeParams
                {
                    AttributeName = "Value",
                    AttributeTypeName = "Double"
                }
            };

            var channelDataEncoder = new ChannelDataEncoder<TimeSeries>(attributesToWrite);

            async Task WriteDatumAsync(double value)
            {
                var writeChannelRequest = new WriteChannelRequest(new WriteChannelRequest
                {
                    ChannelName = "raw",
                    Data = new ChannelDataEvent
                    {
                        Event = DataEventType.Add,
                        Data = new ChannelDataEvent.Types.ChannelData
                        {
                            Count = 1,
                            Data = channelDataEncoder.WriteByteString(new[]
                            {
                                new TimeSeries
                                {
                                    DateTime = DateTime.Now,
                                    Value = value
                                }
                            })
                        }
                    }
                });

                await channelsClient.WriteChannelAsync(writeChannelRequest);
            }

            string enteredValue;

            do
            {
                enteredValue = Console.ReadLine();
                var success = double.TryParse(enteredValue, out var value);

                if (!success)
                    continue;

                await WriteDatumAsync(value);
            } while (!string.IsNullOrWhiteSpace(enteredValue));

            #endregion

            await modulesClient.StopAsync(new Empty());
        }
    }
}