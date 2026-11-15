using AutoIssue;
using AutoIssue.Example;
using AutoIssue.Example.Components;
using AutoIssue.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register AutoIssue.
// Set validateOnStart: false so the example starts even with placeholder credentials.
// Fill in the GitHubFeedback section in appsettings.json before submitting.
builder.Services.AddGitHubFeedback(builder.Configuration, validateOnStart: false);

builder.Services.AddScoped<IAttachmentFileValidator, CustomValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();