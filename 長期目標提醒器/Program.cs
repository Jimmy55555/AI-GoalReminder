using 長期目標提醒器.Components;
using 長期目標提醒器.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection(AiSettings.SectionName));
builder.Services.AddHttpClient<IGoalAdviceClient, GoalAdviceClient>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
});
builder.Services.AddHttpClient<IGoalSkillGenerator, GoalSkillGenerator>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
});
builder.Services.AddSingleton<EmailScheduleStore>();
builder.Services.AddSingleton<GoalStore>();
builder.Services.AddSingleton<GoalProgressStore>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddHostedService<DailyGoalDigestWorker>();
builder.Services.AddScoped<SidebarState>();
builder.Services.AddScoped<IThemeService, BrowserThemeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.MapGet("/dev/email-preview", (EmailService email, EmailScheduleStore schedules, GoalStore goals) =>
    {
        var timeZone = AppTime.Resolve("Taipei Standard Time");
        return Results.Content(
            email.BuildBody(goals.Goals, AppTime.NowLocal(timeZone), schedules.Current),
            "text/html; charset=utf-8");
    });
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(長期目標提醒器.Client._Imports).Assembly);

app.Run();
