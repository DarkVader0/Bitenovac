
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Bitenovac.DecompressionCalculator.Export;
using Bitenovac.DecompressionCalculator.Web;
using Bitenovac.DecompressionCalculator.Web.Planning;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<IKeyValueStorage, BrowserStorage>();
builder.Services.AddScoped<PlanStore>();
builder.Services.AddScoped<ShareService>();
builder.Services.AddScoped<PwaService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<PreferencesStore>();

var host = builder.Build();

await using (var scope = host.Services.CreateAsyncScope())
{
    var preferences = await scope.ServiceProvider.GetRequiredService<PreferencesStore>().GetAsync();
    WebText.Culture = ExportLanguage.Culture = AppLanguages.Culture(preferences.Language);
}

await host.RunAsync();
