using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Bitenovac.DecompressionCalculator.Web;
using Bitenovac.DecompressionCalculator.Web.Planning;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<IKeyValueStorage, BrowserStorage>();
builder.Services.AddScoped<PlanStore>();
builder.Services.AddScoped<ShareService>();
builder.Services.AddScoped<PwaService>();

await builder.Build().RunAsync();
