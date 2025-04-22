using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ArquivoMate2.Blazor
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
                        
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });


            builder.Services.AddOidcAuthentication(
                options =>
                {
                    //builder.Configuration.Bind("Oidc", options.ProviderOptions);

                    options.ProviderOptions.Authority = "https://auth2.modellfrickler.online";
                    options.ProviderOptions.ClientId = "egrVGZZH9GkuULNmnpux9Yr9neRhHXyaVup0pEUh";
                    options.ProviderOptions.ResponseType = OpenIdConnectResponseType.Code;
                    options.ProviderOptions.Authority = "https://auth2.modellfrickler.online/application/o/arquivomate2/";
                    options.ProviderOptions.DefaultScopes.Add("openid");
                    options.ProviderOptions.DefaultScopes.Add("profile");
                    options.ProviderOptions.DefaultScopes.Add("email");
                });

            builder.Services.AddAuthorizationCore();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddAuthenticationStateDeserialization();

            await builder.Build().RunAsync();
        }
    }
}
