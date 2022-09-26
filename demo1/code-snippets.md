# URL Shortener Demo

## Step 1 - Add packages

```csharp
dotnet add package Microsoft.Orleans.Server -v 3.6.3
dotnet add package Microsoft.Orleans.CodeGenerator.MSBuild -v 3.6.3
```

## Step 2 - Add using statements

```csharp
using Microsoft.AspNetCore.Http.Extensions;
using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;
```

## Step 3 - Add grain interface

```csharp
public interface IUrlShortenerGrain : IGrainWithStringKey
{
    Task SetUrl(string shortenedRouteSegment, string fullUrl);
    Task<string> GetUrl();
}
```

## Step 4 - Implement grain

```csharp
public class UrlShortenerGrain : Grain, IUrlShortenerGrain
{
    private KeyValuePair<string, string> _cache;

    public Task SetUrl(string shortenedRouteSegment, string fullUrl)
    {
        _cache = new KeyValuePair<string, string>(shortenedRouteSegment, fullUrl);
        return Task.CompletedTask;
    }

    public Task<string?> GetUrl()
    {
        return Task.FromResult(_cache.Value);
    }
}
```

## Step 5 - Create the silo

```csharp
var builder = WebApplication.CreateBuilder();

builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
});

var app = builder.Build();
```

## Step 6 - Get the grain factory

```csharp
var grainFactory = app.Services.GetRequiredService<IGrainFactory>();
```

## Step 7 - Build the GET request

```csharp
app.MapGet("/shorten/{*path}",
async (IGrainFactory grains, HttpRequest request, string path) =>
{
    var shortenedRouteSegment = Guid.NewGuid().GetHashCode().ToString("X");
    var shortenerGrain = grains.GetGrain<IUrlShortenerGrain>(shortenedRouteSegment);
    await shortenerGrain.SetUrl(shortenedRouteSegment, path);
    var resultBuilder = new UriBuilder(request.GetEncodedUrl())
    {
        Path = $"/go/{shortenedRouteSegment}"
    };

    return Results.Ok(resultBuilder.Uri);
});
```

## Step 8 - Handle redirects

```csharp
app.MapGet("/go/{shortenedRouteSegment}",
    async (IGrainFactory grains, string shortenedRouteSegment) =>
{
    var shortenerGrain = grains.GetGrain<IUrlShortenerGrain>(shortenedRouteSegment);
    var url = await shortenerGrain.GetUrl();

    return Results.Redirect(url);
});
```

## Step 9 - Add State interface

```csharp
public interface IPersistentState<TState> where TState : new()
{
    TState State { get; set; }
    string Etag { get; }
    Task ClearStateAsync();
    Task WriteStateAsync();
    Task ReadStateAsync();
}
```

## Step 10 - Update the UrlShortenerGrain

```csharp
public class UrlShortenerGrain : Grain, IUrlShortenerGrain
{
    private readonly IPersistentState<KeyValuePair<string, string>> _cache;

    public UrlShortenerGrain(
        [PersistentState(
            stateName: "url",
            storageName: "urls")]
            IPersistentState<KeyValuePair<string, string>> state)
    {
        _cache = state;
    }
}
```

## Step 11 - Configure grain state

```csharp
siloBuilder.AddAzureBlobGrainStorage("urls",
        options => options.ConfigureBlobServiceClient(connectionString));        
```

## Step 12 - Configure grain storage

```csharp
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();


    siloBuilder.AddMemoryGrainStorage("urls");
});
```

## Step 13 - Update grains to use persistent storage

```csharp
private IPersistentState<KeyValuePair<string, string>> _cache;

public UrlShortenerGrain(
    [PersistentState(
        stateName: "url",
        storageName: "urls")]
        IPersistentState<KeyValuePair<string, string>> state)
{
    _cache = state;
}
```

**and**

```csharp
public async Task SetUrl(string shortenedRouteSegment, string fullUrl)
{
    _cache.State = new KeyValuePair<string, string>(shortenedRouteSegment, fullUrl);
    await _cache.WriteStateAsync();
}

public Task<string> GetUrl()
{
    return Task.FromResult(_cache.State.Value);
}
```
