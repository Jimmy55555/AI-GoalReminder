using 長期目標提醒器.Components;
using 長期目標提醒器.Services;

var builder = WebApplication.CreateBuilder(args);

// 沿用 GitHub Pages 版的 Supabase 設定，本機不再另存一份資料。
builder.Configuration.AddJsonFile(
    Path.Combine(builder.Environment.ContentRootPath, "..", "GitHubPagesApp", "wwwroot", "appsettings.json"),
    optional: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection(AiSettings.SectionName));

var supabaseConfig = builder.Configuration.GetSupabaseConfig();
builder.Services.AddSingleton(supabaseConfig);
builder.Services.AddScoped<SessionStore>();
builder.Services.AddScoped(sp =>
{
    var httpClient = new HttpClient { BaseAddress = new Uri(supabaseConfig.Url) };
    return new SupabaseAuthClient(httpClient, supabaseConfig.PublishableKey);
});
builder.Services.AddScoped(sp =>
{
    var sessions = sp.GetRequiredService<SessionStore>();
    var httpClient = new HttpClient { BaseAddress = new Uri(supabaseConfig.Url) };
    return new SupabaseClient(httpClient, supabaseConfig.PublishableKey, () => sessions.AccessToken);
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GoalStore>();
builder.Services.AddScoped<GoalProgressStore>();
builder.Services.AddScoped<EmailScheduleStore>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IGoalSkillGenerator, GoalSkillGenerator>();
builder.Services.AddScoped<IGoalAdviceClient, GoalAdviceClient>();
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
