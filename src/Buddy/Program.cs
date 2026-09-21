using System.ClientModel;
using Buddy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using OpenAI;
using OpenAI.Chat;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseSerilog();
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args);
builder.Services.Configure<AIOptions>(opt =>
{
    opt.OpenAIUrl = builder.Configuration.GetValue<string>("OpenAIUrl");
    opt.OpenAIModel = builder.Configuration.GetValue<string>("OpenAIModel");
    opt.OpenAIApiKey = builder.Configuration.GetValue<string>("OpenAIApiKey");
    opt.OpenAIPrompt = builder.Configuration.GetValue<string>("OpenAIPrompt");
});
builder.Services.AddChatClient(provider =>
{
    var options = provider.GetRequiredService<IOptions<AIOptions>>();

    return new ChatClient(
        options.Value.OpenAIModel,
        new ApiKeyCredential(options.Value.OpenAIApiKey),
        new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Value.OpenAIUrl),
            NetworkTimeout = Timeout.InfiniteTimeSpan
        }).AsIChatClient();
}).UseLogging();

var toolDescription = builder.Configuration.GetValue<string>("ToolDescription");
var promptDescription = builder.Configuration.GetValue<string>("PromptDescription");

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithRequestFilters(filters =>
    {
        filters.AddCallToolFilter(next => async (context, token) =>
        {
            using (Provider.UseProvider(context.Services))
            {
                return await next(context, token);
            }
        });
    })
    .WithTools([(McpServerTool.Create(async (string prompt, CancellationToken token ) =>
    {
        var client = Provider.Current.GetRequiredService<IChatClient>();
        var options = Provider.Current.GetRequiredService<IOptions<AIOptions>>();

        var response = await client.GetResponseAsync(prompt, new ChatOptions
        {
            Instructions = options.Value.OpenAIPrompt
        }, token);

        return response.Text;
    }, new McpServerToolCreateOptions
    {
        Name = "ask_buddy",
        Description = toolDescription,
        SchemaCreateOptions = new AIJsonSchemaCreateOptions
        {
            ParameterDescriptionProvider = info =>
            {
                return info.Name == "prompt"
                    ? promptDescription
                    : null;
            }
        }
    }))]);

var app = builder.Build();

app.Use(async (context, next) =>
{
    var auth = (string)context.Request.Headers["Authorization"];
    var currentToken = auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
        ? auth.Substring(7).Trim()
        : null;
    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    var expectedToken = configuration.GetValue<string>("ToolApiKey");
    var isValid = string.IsNullOrWhiteSpace(expectedToken) || string.Equals(expectedToken, currentToken, StringComparison.Ordinal);

    if (isValid)
    {
        await next(context);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
    }
});
app.MapMcp();
app.Run();
