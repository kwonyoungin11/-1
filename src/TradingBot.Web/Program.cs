using TradingBot.Web.Components;
using TradingBot.Web.Services;

// TradingBot Blazor host — mock read-only cockpit.
// Live orders blocked by default (live orders disallowed by default, KILL_SWITCH=true, ORDER_MODE=dry_run).
// No Toss order HTTP is registered or called from this host.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddTradingBotCockpit();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
