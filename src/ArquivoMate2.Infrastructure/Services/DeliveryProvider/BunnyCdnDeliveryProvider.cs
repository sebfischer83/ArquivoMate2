using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using EasyCaching.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ArquivoMate2.Infrastructure.Services.DeliveryProvider
{
    public class BunnyCdnDeliveryProvider : IDeliveryProvider
    {
        private readonly BunnyDeliveryProviderSettings _settings;
        private readonly IEasyCachingProvider _cache;
        private readonly IPathService _pathService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BunnyCdnDeliveryProvider(
            IOptions<BunnyDeliveryProviderSettings> opts,
            IEasyCachingProviderFactory cachingProviderFactory,
            IPathService pathService,
            IHttpContextAccessor httpContextAccessor)
        {
            _settings = opts.Value;
            _cache = cachingProviderFactory.GetCachingProvider(EasyCachingConstValue.DefaultRedisName);
            _pathService = pathService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> GetAccessUrl(string fullPath)
        {
            if (!_settings.UseTokenAuthentication)
            {
                return $"{_settings.Host.TrimEnd('/')}/{fullPath}";
            }

            var cacheKey = $"bunnyDelivery:{fullPath}-{_settings.ToString()}";
            var cachedUrl = await _cache.GetAsync<string>(cacheKey);

            if (cachedUrl.HasValue)
            {
                return cachedUrl.Value;
            }

            var baseUrl = _settings.Host.TrimEnd('/');
            string url = $"{baseUrl}/{fullPath}";
            string securityKey = _settings.TokenAuthenticationKey;

            string ip = string.Empty;

            if (_settings.UseTokenIpValidation)
            {
                ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            }

            string signedUrl = SignUrl(
                url: url,
                securityKey: securityKey,
                expirationTime: 86400,
                userIp: ip,
                isDirectory: false,
                pathAllowed: "",
                countriesAllowed: _settings.TokenCountries,
                countriesBlocked: _settings.TokenCountriesBlocked
            );

            _cache.Set(cacheKey, signedUrl, TimeSpan.FromSeconds(86400) - TimeSpan.FromMinutes(30));


            return signedUrl;
        }


        private string AddCountries(string url, string countriesAllowed, string countriesBlocked)
        {
            var uri = new Uri(url);
            bool hasQuery = !string.IsNullOrEmpty(uri.Query);

            if (!string.IsNullOrEmpty(countriesAllowed))
            {
                url += (hasQuery ? "&" : "?") + "token_countries=" + countriesAllowed;
                hasQuery = true;
            }

            if (!string.IsNullOrEmpty(countriesBlocked))
            {
                url += (hasQuery ? "&" : "?") + "token_countries_blocked=" + countriesBlocked;
            }

            return url;
        }

        /// Generates URL Authentication Beacon
        /// </summary>
        /// <param name="url">CDN URL w/o the trailing '/' - e.g., http://test.b-cdn.net/file.png</param>
        /// <param name="securityKey">Security token found in your pull zone</param>
        /// <param name="expirationTime">Authentication validity (default: 86400 sec/24 hrs)</param>
        /// <param name="userIp">Optional parameter if you have the User IP feature enabled</param>
        /// <param name="isDirectory">Whether the URL is for a directory</param>
        /// <param name="pathAllowed">Optional allowed path</param>
        /// <param name="countriesAllowed">List of countries allowed (e.g., "CA,US,TH")</param>
        /// <param name="countriesBlocked">List of countries blocked (e.g., "CA,US,TH")</param>
        /// <returns>Signed URL with authentication token</returns>
        public string SignUrl(
            string url,
            string securityKey,
            int expirationTime = 86400,
            string userIp = "",
            bool isDirectory = true,
            string pathAllowed = "",
            string countriesAllowed = "",
            string countriesBlocked = "")
        {
            string parameterData = "";
            string parameterDataUrl = "";

            long expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expirationTime;
            url = AddCountries(url, countriesAllowed, countriesBlocked);

            var uri = new Uri(url);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);

            string signaturePath;
            if (!string.IsNullOrEmpty(pathAllowed))
            {
                signaturePath = pathAllowed;
                queryParams["token_path"] = signaturePath;
            }
            else
            {
                signaturePath = uri.AbsolutePath;
            }

            // Sort parameters
            var sortedParams = queryParams.AllKeys
            .Where(key => key != null)
            .OrderBy(key => key!)
            .ToDictionary(key => key!, key => queryParams[key]);

            foreach (var kvp in sortedParams)
            {
                if (parameterData.Length > 0)
                {
                    parameterData += "&";
                }
                parameterDataUrl += "&";

                parameterData += kvp.Key + "=" + kvp.Value;
                parameterDataUrl += kvp.Key + "=" + HttpUtility.UrlEncode(kvp.Value);
            }

            string hashableBase = securityKey + signaturePath + expires + parameterData +
                                 (!string.IsNullOrEmpty(userIp) ? userIp : "");

            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashableBase));
                string token = Convert.ToBase64String(hashBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");

                if (isDirectory)
                {
                    return $"{uri.Scheme}://{uri.Host}/bcdn_token={token}{parameterDataUrl}&expires={expires}{uri.AbsolutePath}";
                }
                else
                {
                    return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}?token={token}{parameterDataUrl}&expires={expires}";
                }
            }
        }
    }
}
