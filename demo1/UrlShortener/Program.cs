using Microsoft.AspNetCore.Http.Extensions;
using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans(siloBuilder => 
{
    siloBuilder.UseLocalhostClustering();
});

var app = builder.Build();

var grainFactory = app.Services.GetRequiredService<IGrainFactory>();

app.MapGet("/", () => "Hello World!");

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

app.MapGet("/go/{shortenedRouteSegment}",
    async (IGrainFactory grains, string shortenedRouteSegment) =>
{
    var shortenerGrain = grains.GetGrain<IUrlShortenerGrain>(shortenedRouteSegment);
    var url = await shortenerGrain.GetUrl();

    return Results.Redirect(url);
});

app.Run();

public interface IUrlShortenerGrain : IGrainWithStringKey
{
    Task SetUrl(string shortenedRouteSegment, string fullUrl);
    Task<string> GetUrl();
}

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