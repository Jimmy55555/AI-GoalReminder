using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using 長期目標提醒器;
using 長期目標提醒器.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddOptions();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("Ai"));

builder.Services.AddScoped<SessionStore>();

builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSupabaseConfig();
    var httpClient = new HttpClient { BaseAddress = new Uri(config.Url) };
    return new SupabaseAuthClient(httpClient, config.PublishableKey);
});

builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSupabaseConfig();
    var sessions = sp.GetRequiredService<SessionStore>();
    var httpClient = new HttpClient { BaseAddress = new Uri(config.Url) };
    return new SupabaseClient(httpClient, config.PublishableKey, () => sessions.AccessToken);
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GoalStore>();
builder.Services.AddScoped<GoalProgressStore>();
builder.Services.AddScoped<EmailScheduleStore>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IGoalSkillGenerator, GoalSkillGenerator>();
builder.Services.AddScoped<ISkillStepGenerator, SkillStepGenerator>();
builder.Services.AddScoped<IGoalAdviceClient, GoalAdviceClient>();
builder.Services.AddScoped<IThemeService, BrowserThemeService>();
builder.Services.AddScoped<SidebarState>();

await builder.Build().RunAsync();
