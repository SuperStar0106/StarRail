﻿using Microsoft.Win32;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Starward.Core.Gacha;

public abstract class GachaLogClient
{


    protected readonly HttpClient _httpClient;


    protected abstract IReadOnlyCollection<int> GachaTypes { get; init; }



    public GachaLogClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });
        }
        else
        {
            _httpClient = httpClient;
        }
    }



    protected abstract string GetGachaUrlPrefix(string gachaUrl, string? lang = null);



    public async Task<int> GetUidByGachaUrlAsync(string gachaUrl)
    {
        var prefix = GetGachaUrlPrefix(gachaUrl);
        foreach (var gachaType in GachaTypes)
        {
            var param = new QueryParam(gachaType, 1, 1, 0);
            var list = await GetGachaLogByQueryParamAsync<GachaLogItem>(prefix, param);
            if (list.Any())
            {
                return list.First().Uid;
            }
        }
        return 0;
    }




    protected async Task<List<T>> GetGachaLogAsync<T>(string gachaUrl, long endId = 0, string? lang = null, IProgress<(int GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        endId = Math.Clamp(endId, 0, long.MaxValue);
        var prefix = GetGachaUrlPrefix(gachaUrl, lang);
        var result = new List<T>();
        foreach (var gachaType in GachaTypes)
        {
            result.AddRange(await GetGachaLogAsyncInternal<T>(prefix, gachaType, endId, progress, cancellationToken));
        }
        return result;
    }




    protected async Task<List<T>> GetGachaLogAsync<T>(string gachaUrl, int gachaType, long endId = 0, string? lang = null, IProgress<(int GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        endId = Math.Clamp(endId, 0, long.MaxValue);
        var prefix = GetGachaUrlPrefix(gachaUrl, lang);
        return await GetGachaLogAsyncInternal<T>(prefix, gachaType, endId, progress, cancellationToken);
    }






    private async Task<List<T>> GetGachaLogAsyncInternal<T>(string prefix, int gachaType, long endId = 0, IProgress<(int GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        var param = new QueryParam(gachaType, 1, 20, 0);
        var result = new List<T>();
        while (true)
        {
            progress?.Report((gachaType, param.Page));
            var list = await GetGachaLogByQueryParamAsync<T>(prefix, param, cancellationToken);
            result.AddRange(list);
            if (list.Count == 20 && list.Last().Id > endId)
            {
                param.Page++;
                param.EndId = list.Last().Id;
            }
            else
            {
                break;
            }
        }
        return result;
    }





    internal async Task<List<T>> GetGachaLogByQueryParamAsync<T>(string warpUrlPrefix, QueryParam param, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        await Task.Delay(Random.Shared.Next(200, 300));
        var url = $"{warpUrlPrefix}&{param}";
        var wrapper = await _httpClient.GetFromJsonAsync(url, typeof(MihoyoApiWrapper<GachaLogResult<T>>), GachaLogJsonContext.Default, cancellationToken) as MihoyoApiWrapper<GachaLogResult<T>>;
        if (wrapper is null)
        {
            return new List<T>();
        }
        else if (wrapper.Retcode != 0)
        {
            throw new MihoyoApiException(wrapper.Retcode, wrapper.Message);
        }
        else
        {
            return wrapper.Data.List;
        }
    }






    [SupportedOSPlatform("windows")]
    protected static string? GetGameInstallPathFromRegistry(string regKey)
    {
        var launcherPath = Registry.GetValue(regKey, "InstallPath", null) as string;
        var configPath = Path.Join(launcherPath, "config.ini");
        if (File.Exists(configPath))
        {
            var str = File.ReadAllText(configPath);
            var installPath = Regex.Match(str, @"game_install_path=(.+)").Groups[1].Value.Trim();
            if (Directory.Exists(installPath))
            {
                return Path.GetFullPath(installPath);
            }
        }
        return null;
    }




}