using MMS.Bootstrap;
using MMS.Features;

namespace MMS;

/// <summary>
/// Entry point and composition root for the MatchMaking Server.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var isDevelopment = builder.Environment.IsDevelopment();

        ProgramState.IsDevelopment = isDevelopment;

        builder.Services.AddMmsCoreServices();
        builder.Services.AddMmsInfrastructure(isDevelopment);

        if (!builder.TryConfigureMmsHttps(isDevelopment))
            return;

        var app = builder.Build();
        ProgramState.Logger = app.Logger;

        app.UseMmsPipeline(isDevelopment);
        app.MapMmsEndpoints();
        app.Run();
    }
}
