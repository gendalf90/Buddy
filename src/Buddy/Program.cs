using System.ClientModel;
using Buddy;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using OpenAI;
using OpenAI.Responses;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Host.UseSerilog();

builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var openAIUrl = builder.Configuration.GetValue<string>("OpenAIUrl");
var openAIModel = builder.Configuration.GetValue<string>("OpenAIModel");
var openAIApiKey = builder.Configuration.GetValue<string>("OpenAIApiKey");
var openAIPrompt = builder.Configuration.GetValue<string>("OpenAIPrompt");
var toolApiKey = builder.Configuration.GetValue<string>("ToolApiKey");
var toolDescription = builder.Configuration.GetValue<string>("ToolDescription");
var promptDescription = builder.Configuration.GetValue<string>("PromptDescription");

builder.Services.AddChatClient(provider =>
{
    #pragma warning disable OPENAI001
    return new ResponsesClient(
        new ApiKeyCredential(openAIApiKey), 
        new ResponsesClientOptions
        {
            Endpoint = new Uri(new Uri(openAIUrl), "v1/"),
            NetworkTimeout = Timeout.InfiniteTimeSpan
        }).AsIChatClient(openAIModel);
    #pragma warning restore OPENAI001
}).UseLogging();

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
    .WithTools([(McpServerTool.Create(async (string prompt, CancellationToken token) =>
    {
        var client = Provider.Current.GetRequiredService<IChatClient>();

        var response = await client.GetResponseAsync(prompt, new ChatOptions
        {
            Instructions = openAIPrompt
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
    var isValid = string.IsNullOrWhiteSpace(toolApiKey) || string.Equals(toolApiKey, currentToken, StringComparison.Ordinal);

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
