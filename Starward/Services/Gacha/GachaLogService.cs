﻿using Dapper;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using Starward.Core;
using Starward.Core.Gacha;
using Starward.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Services.Gacha;


internal abstract class GachaLogService
{


    protected readonly ILogger<GachaLogService> _logger;

    protected readonly DatabaseService _database;

    protected readonly GachaLogClient _client;


    protected GachaLogService(ILogger<GachaLogService> logger, DatabaseService database, GachaLogClient client)
    {
        _logger = logger;
        _database = database;
        _client = client;
    }



    protected abstract GameBiz GameBiz { get; }

    protected abstract string GachaTableName { get; }

    protected abstract IReadOnlyCollection<int> GachaTypes { get; }


    protected abstract List<GachaLogItemEx> GetGroupGachaLogItems(IEnumerable<GachaLogItemEx> items, GachaType type);



    public static string GetGachaLogText(GameBiz biz)
    {
        return biz switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_global or GameBiz.hk4e_cloud => Lang.GachaLogService_WishRecords,
            GameBiz.hkrpg_cn or GameBiz.hkrpg_global => Lang.GachaLogService_WarpRecords,
            _ => ""
        };
    }



    public virtual List<int> GetUids()
    {
        using var dapper = _database.CreateConnection();
        return dapper.Query<int>($"SELECT DISTINCT Uid FROM {GachaTableName};").ToList();
    }



    public virtual List<GachaLogItemEx> GetGachaLogItemEx(int uid)
    {
        using var dapper = _database.CreateConnection();
        var list = dapper.Query<GachaLogItemEx>($"SELECT * FROM {GachaTableName} WHERE Uid = @uid ORDER BY Id;", new { uid }).ToList();
        foreach (var type in GachaTypes)
        {
            var l = GetGroupGachaLogItems(list, (GachaType)type);
            int index = 0;
            int pity = 0;
            foreach (var item in l)
            {
                item.Index = ++index;
                item.Pity = ++pity;
                if (item.RankType == 5)
                {
                    pity = 0;
                }
            }
        }
        return list;
    }



    public virtual string? GetGachaLogUrlFromWebCache(GameBiz gameBiz, string path)
    {
        return GachaLogClient.GetGachaUrlFromWebCache(gameBiz, path);
    }




    public virtual async Task<int> GetUidFromGachaLogUrl(string url)
    {
        var uid = await _client.GetUidByGachaUrlAsync(url);
        if (uid > 0)
        {
            using var dapper = _database.CreateConnection();
            dapper.Execute("INSERT OR REPLACE INTO GachaLogUrl (GameBiz, Uid, Url, Time) VALUES (@GameBiz, @Uid, @Url, @Time);", new GachaLogUrl(GameBiz, uid, url));
        }
        return uid;
    }



    public virtual string? GetGachaLogUrlByUid(int uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<string>("SELECT Url FROM GachaLogUrl WHERE Uid = @uid AND GameBiz = @GameBiz LIMIT 1;", new { uid, GameBiz });
    }



    protected abstract int InsertGachaLogItems(List<GachaLogItem> items);



    public virtual async Task<int> GetGachaLogAsync(string url, bool all, string? lang = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        using var dapper = _database.CreateConnection();
        // 正在获取 uid
        progress?.Report(Lang.GachaLogService_GettingUid);
        var uid = await _client.GetUidByGachaUrlAsync(url);
        if (uid == 0)
        {
            // 该账号最近6个月没有抽卡记录
            progress?.Report(Lang.GachaLogService_ThisAccountHasNoGachaRecordsInTheLast6Months);
        }
        else
        {
            long endId = 0;
            if (!all)
            {
                endId = dapper.QueryFirstOrDefault<long>($"SELECT Id FROM {GachaTableName} WHERE Uid = @Uid ORDER BY Id DESC LIMIT 1;", new { Uid = uid });
                _logger.LogInformation($"Last gacha log id of uid {uid} is {endId}");
            }

            var internalProgress = new Progress<(GachaType GachaType, int Page)>((x) => progress?.Report(string.Format(Lang.GachaLogService_GetGachaProgressText, x.GachaType.ToLocalization(), x.Page)));
            var list = (await _client.GetGachaLogAsync(url, endId, lang, internalProgress, cancellationToken)).ToList();
            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }
            var oldCount = dapper.QueryFirstOrDefault<int>($"SELECT COUNT(*) FROM {GachaTableName} WHERE Uid = @Uid;", new { Uid = uid });
            InsertGachaLogItems(list);
            var newCount = dapper.QueryFirstOrDefault<int>($"SELECT COUNT(*) FROM {GachaTableName} WHERE Uid = @Uid;", new { Uid = uid });
            // 获取 {list.Count} 条记录，新增 {newCount - oldCount} 条记录
            progress?.Report(string.Format(Lang.GachaLogService_GetGachaResult, list.Count, newCount - oldCount));
        }
        return uid;
    }






    public virtual List<GachaTypeStats> GetGachaTypeStats(int uid)
    {
        var statsList = new List<GachaTypeStats>();
        using var dapper = _database.CreateConnection();
        var alllist = GetGachaLogItemEx(uid);
        if (alllist.Count > 0)
        {
            foreach (int type in GachaTypes)
            {
                var list = GetGroupGachaLogItems(alllist, (GachaType)type);
                var stats = new GachaTypeStats
                {
                    GachaType = (GachaType)type,
                    Count = list.Count,
                    Count_5 = list.Count(x => x.RankType == 5),
                    Count_4 = list.Count(x => x.RankType == 4),
                    Count_3 = list.Count(x => x.RankType == 3),
                };
                if (stats.Count > 0)
                {
                    stats.StartTime = list.First().Time;
                    stats.EndTime = list.Last().Time;
                    stats.Ratio_5 = (double)stats.Count_5 / stats.Count;
                    stats.Ratio_4 = (double)stats.Count_4 / stats.Count;
                    stats.Ratio_3 = (double)stats.Count_3 / stats.Count;
                    stats.List_5 = list.Where(x => x.RankType == 5).Reverse().ToList();
                    stats.List_4 = list.Where(x => x.RankType == 4).Reverse().ToList();
                    stats.Pity_5 = list.Last().Pity;
                    if (list.Last().RankType == 5)
                    {
                        stats.Pity_5 = 0;
                    }
                    stats.Average_5 = (double)(stats.Count - stats.Pity_5) / stats.Count_5;
                    stats.Pity_4 = list.Count - 1 - list.FindLastIndex(x => x.RankType == 4);
                }
                statsList.Add(stats);
            }
        }
        return statsList;
    }






    public virtual int DeleteUid(int uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.Execute($"DELETE FROM {GachaTableName} WHERE Uid = @uid;", new { uid });
    }






    public abstract Task ExportGachaLogAsync(int uid, string file, string format);




    public abstract int ImportGachaLog(string file);




}
