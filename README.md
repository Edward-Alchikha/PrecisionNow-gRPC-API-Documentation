The PrecisionNow gRPC API and provided helper methods are currently supported on .NET 5 on Windows (x64) and Linux (x64). 
The .NET 5 SDK can be downloaded at https://dotnet.microsoft.com/download/dotnet/5.0.

If you would like to use the PrecisionNow gRPC API outside of .NET 5 (helper methods not currently provided),
you can find the required protos in the Protos folder in this repository. For information on using the provided
protos for your language of choice you can look at https://grpc.io/docs/languages/.

# Setup for .NET 5 Projects
- In your newly created .NET 5 project, add a reference to PrecisionNow.dll, 
which can be found in the dDesk/dDock software directory in the "Program Files" subdirectory.
This will give you access to some bundled helper data encoding/decoding methods and pre-compiled
gRPC client classes that would otherwise have to be generated for your language of choice using protoc.
- Add necessary NuGet package references (this list may be changed in the future): 
  - Google.Protobuf@3.15.8
  - Grpc.Core.Api@3.27.0
  - Grpc.Net.Client@3.27.0
  - Grpc.Net.Client.Web@3.27.0
  - Recommended: System.Interactive.Async@5.0.0 for helper methods for working with IAsyncEnumerable
- Initialize connection to the gRPC backend:
    ```cs
    using var grpcChannel = ClientHelper.MakeGrpcChannel();

    // access relevant methods through clients for functionality
    var channelsClient = new Channels.ChannelsClient(grpcChannel);
    var projectClient = new Project.ProjectClient(grpcChannel);
    var modulesClient = new Modules.ModulesClient(grpcChannel);

    // can now access methods through the clients
    ```
<br>
With the project now set up you can access the methods provided by the different clients.
<br>
<br>

# Channels Client

## GetChannels
Available from dDock 3.4-prerelease1
<br><br>
Example usage:
```cs
var getChannelsStream = channelsClient.GetChannels(new Empty());

// the channels list is provided as a stream so that you can see when channels are created or deleted
// since we only care about the channels that currently exist, we can use the FirstAsync method
// from the System.Interactive.Async NuGet package
var getChannelsFirstResponse = 
    await getChannelsStream.ResponseStream
        .ReadAllAsync()
        .FirstAsync();

var currentChannels = getChannelsFirstResponse.Data;
```

## CreateChannel
Available from dDock 3.4-prerelease1
<br><br>
Example usage:
```cs
await channelsClient.CreateChannelAsync(
  new SchemaDefinition
  {
      TableName = "newChannelName",
      TableType = "Output",
      AttributeDefinitions =
      {
          new AttributeParams
          {
              AttributeName = "newStringAttribute",
              AttributeTypeName = "String",
              Unit = new Unit()
          },
          new AttributeParams
          {
              AttributeName = "newDoubleAttribute",
              AttributeTypeName = "Double",
              Unit = new Unit()
          },
      }
  }
);
```

## ReadChannel
Available from dDock 3.4-prerelease1
<br><br>
Example usage:
```cs
var attributesToRead = new[]
{
    new ReadChannelRequest.Types.AttributeOptions
    {
        Name = "DateTime",
        NameAs = "DateTime"
    },
    new ReadChannelRequest.Types.AttributeOptions
    {
        Name = "Value",
        NameAs = "Value"
    }
};

var readChannelRequest = new ReadChannelRequest {ChannelName = "timeSeries",};
readChannelRequest.Attributes.Add(attributesToRead);

var channelDataDecoder = new ChannelDataDecoder<TimeSeries>(attributesToRead);

var readCompletion = channelsClient.ReadChannel(readChannelRequest).ResponseStream
    .ReadAllAsync()
    // ForEachAsync is available from System.Interactive.Async
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
                foreach (var datum in decodedData)
                    Console.WriteLine($"{datum.DateTime}: {datum.Value}");
                break;
        }
    });
```
with the TimeSeries class defined elsewhere:
```cs
class TimeSeries
{
    public DateTime DateTime { get; set; }
    public double Value { get; set; }
}
```

## WriteChannel
Available from dDock 3.4-prerelease1
<br><br>
Example usage:
```cs
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

var writeChannelRequest = new WriteChannelRequest(new WriteChannelRequest
{
    ChannelName = "raw",
    Data = new ChannelDataEvent
    {
        Event = DataEventType.Add,
        Data = new ChannelDataEvent.Types.ChannelData
        {
            Count = 1,
            Data = channelDataEncoder.WriteByteString(
              new[]
              {
                  new TimeSeries
                  {
                      DateTime = DateTime.Now,
                      Value = value
                  }
              }
            )
            
        }
    }
});

await channelsClient.WriteChannelAsync(writeChannelRequest);

```

## DeleteChannel
Available from dDock 3.4-prerelease1
<br><br>
Example usage:
```cs
await channelsClient.DeleteChannelAsync(new ChannelName {ChannelName_ = "channelName"});
```

# Modules Client

## RunOnce
Available from dDock 3.4-prerelease1
<br><br>
Runs the modules once until completion. Suitable for working with historical data. Available in both dDesk and dDock.
This requires that the modules layout be previously set up through the running dDock or dDesk instance.
<br><br>
Example usage:
```cs
await modulesClient.RunOnceAsync(new Empty());
```

## Run
Available from dDock 3.4-prerelease1
<br><br>
Runs the modules continuously. Suitable for a real-time data environment. Available only in dDock.
This requires that the modules layout be previously set up through the running dDock instance.
<br><br>
Example usage:
```cs
await modulesClient.RunAsync(new Empty());
```

## Stop
Available from dDock 3.4-prerelease1
<br><br>
Stops the current execution of the modules. This operation may take a moment to complete if modules are currently
processing large chunks of data. If a module is waiting for data and not currently processing data it will stop instantly.
<br><br>
Example usage:
```cs
await modulesClient.StopAsync(new Empty());
```

# Project Client
Coming soon
