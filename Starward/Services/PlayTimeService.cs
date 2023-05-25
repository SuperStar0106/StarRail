﻿using Dapper;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace Starward.Services;

internal class PlayTimeService
{

    private readonly ILogger<PlayTimeService> _logger;

    private readonly DatabaseService _database;


    public PlayTimeService(ILogger<PlayTimeService> logger, DatabaseService database)
    {
        _logger = logger;
        _database = database;
    }



    public async Task LogPlayTimeAsync(GameBiz biz, int pid)
    {
        try
        {
            Log(biz, pid, PlayTimeItem.PlayState.None, 0, "Ready to log time");
            var process = Process.GetProcessById(pid);
            Log(biz, pid, PlayTimeItem.PlayState.Start, new DateTimeOffset(process.StartTime).ToUnixTimeMilliseconds(), process.ProcessName);
            var sw = Stopwatch.StartNew();
            long last = 0;
            while (true)
            {
                await Task.Delay(Random.Shared.Next(800, 1200));
                if (process.HasExited)
                {
                    Log(biz, pid, PlayTimeItem.PlayState.Stop);
                    break;
                }
                else
                {
                    if (sw.ElapsedMilliseconds - last > 30000)
                    {
                        Log(biz, pid, PlayTimeItem.PlayState.Play);
                        last = sw.ElapsedMilliseconds;
                    }
                }
            }
            _database.SetValue($"playtime_total_{biz}", GetPlayTimeTotal(biz));
            _database.SetValue($"playtime_month_{biz}", GetPlayCurrentMonth(biz));
            _database.SetValue($"playtime_week_{biz}", GetPlayCurrentWeek(biz));
        }
        catch (Exception ex)
        {
            Log(biz, pid, PlayTimeItem.PlayState.Error, 0, ex.Message);
            _logger.LogError(ex, "Log play time: GameBiz {biz}, Pid {pid}", biz, pid);
        }
    }




    private void Log(GameBiz biz, int pid, PlayTimeItem.PlayState state, long ts = 0, string? message = null)
    {
        try
        {
            using var dapper = _database.CreateConnection();
            User32.GetCursorPos(out var pos);
            var item = new PlayTimeItem
            {
                TimeStamp = ts == 0 ? DateTimeOffset.Now.ToUnixTimeMilliseconds() : ts,
                GameBiz = biz,
                Pid = pid,
                State = state,
                CursorPos = (((long)pos.X) << 32) | (long)pos.Y,
                Message = message,
            };
            dapper.Execute("INSERT OR REPLACE INTO PlayTimeItem (TimeStamp, GameBiz, Pid, State, CursorPos, Message) VALUES (@TimeStamp, @GameBiz, @Pid, @State, @CursorPos, @Message);", item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log play time: GameBiz {biz}, Pid {pid}, State {state}, Message {message}", biz, pid, state, message);
        }
    }



    public TimeSpan GetPlayTimeTotal(GameBiz biz)
    {
        using var dapper = _database.CreateConnection();
        var items = dapper.Query<PlayTimeItem>("SELECT * FROM PlayTimeItem WHERE GameBiz = @biz ORDER BY TimeStamp;", new { biz }).ToList();
        long ts = 0;
        var dic_last_time = new Dictionary<int, long>();
        var dic_last_start_time = new Dictionary<int, long>();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            int pid = item.Pid;
            if (item.State is PlayTimeItem.PlayState.Start)
            {
                if (dic_last_start_time.GetValueOrDefault(pid) != 0)
                {
                    ts += dic_last_time.GetValueOrDefault(pid) - dic_last_start_time.GetValueOrDefault(pid);
                }
                dic_last_start_time[pid] = item.TimeStamp;
            }
            if (item.State is PlayTimeItem.PlayState.Stop)
            {
                if (dic_last_start_time.GetValueOrDefault(pid) != 0)
                {
                    ts += item.TimeStamp - dic_last_start_time.GetValueOrDefault(pid);
                    dic_last_start_time[pid] = 0;
                }
            }
            if (item.State is PlayTimeItem.PlayState.Play && i == items.Count - 1)
            {
                ts += item.TimeStamp - dic_last_start_time.GetValueOrDefault(pid);
            }
            if (item.State is PlayTimeItem.PlayState.Error)
            {
                dic_last_start_time[pid] = 0;
            }
            dic_last_time[pid] = item.TimeStamp;
        }
        return TimeSpan.FromMilliseconds(ts);
    }


    public TimeSpan GetPlayCurrentMonth(GameBiz biz)
    {
        using var dapper = _database.CreateConnection();
        var now = DateTimeOffset.Now;
        var month = now.Add(-now.TimeOfDay).AddDays(-now.Day);
        long month_ts = month.ToUnixTimeMilliseconds();
        var items = dapper.Query<PlayTimeItem>("SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND TimeStamp >= @month_ts ORDER BY TimeStamp;", new { biz, month_ts }).ToList();
        long ts = 0;
        var dic_last_time = new Dictionary<int, long>();
        var dic_last_start_time = new Dictionary<int, long>();
        if (items.Count == 1)
        {
            if (items[0].State is PlayTimeItem.PlayState.Stop)
            {
                ts += items[0].TimeStamp - month_ts;
            }
        }
        if (items.Count > 1)
        {
            if (items[0].State is PlayTimeItem.PlayState.Stop)
            {
                ts += items[0].TimeStamp - month_ts;
            }
            if (items[0].State is PlayTimeItem.PlayState.Play)
            {
                dic_last_time[items[0].Pid] = month_ts;
                dic_last_start_time[items[0].Pid] = month_ts;
            }
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int pid = item.Pid;
                if (item.State is PlayTimeItem.PlayState.Start)
                {
                    if (dic_last_start_time.GetValueOrDefault(pid) != 0)
                    {
                        ts += dic_last_time.GetValueOrDefault(pid) - dic_last_start_time.GetValueOrDefault(pid);
                    }
                    dic_last_start_time[pid] = item.TimeStamp;
                }
                if (item.State is PlayTimeItem.PlayState.Stop)
                {
                    if (dic_last_start_time.GetValueOrDefault(pid) != 0)
                    {
                        ts += item.TimeStamp - dic_last_start_time.GetValueOrDefault(pid);
                        dic_last_start_time[pid] = 0;
                    }
                }
                if (item.State is PlayTimeItem.PlayState.Play && i == items.Count - 1)
                {
                    ts += item.TimeStamp - dic_last_start_time.GetValueOrDefault(pid);
                }
                if (item.State is PlayTimeItem.PlayState.Error)
                {
                    dic_last_start_time[pid] = 0;
                }
                dic_last_time[pid] = item.TimeStamp;
            }
        }
        return TimeSpan.FromMilliseconds(ts);
    }


    public TimeSpan GetPlayCurrentWeek(GameBiz biz)
    {
        using var dapper = _database.CreateConnection();
        var now = DateTimeOffset.Now;
        var week = now.Add(-now.TimeOfDay).AddDays(-(((int)now.DayOfWeek + 1) % 7));
        long week_ts = week.ToUnixTimeMilliseconds();
        var items = dapper.Query<PlayTimeItem>("SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND TimeStamp >= @week_ts ORDER BY TimeStamp;", new { biz, week_ts }).ToList();
        long ts = 0;
        var dic_last_time = new Dictionary<int, long>();
        var dic_last_start_time = new Dictionary<int, long>();
        if (items.Count == 1)
        {
            if (items[0].State is PlayTimeItem.PlayState.Stop)
            {
                ts += items[0].TimeStamp - week_ts;
            }
        }
        if (items.Count > 1)
        {
            if (items[0].State is PlayTimeItem.PlayState.Stop)
            {
                ts += items[0].TimeStamp - week_ts;
            }
            if (items[0].State is PlayTimeItem.PlayState.Play)
            {
                dic_last_time[items[0].Pid] = week_ts;
                dic_last_start_time[items[0].Pid] = week_ts;
            }
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int pid = item.Pid;
                if (item.State is PlayTimeItem.PlayState.Start)
                {
                    if (dic_last_start_time.GetValueOrDefault(pid) != 0)
                    {
                        ts += dic_last_time.GetValueOrDefault(pid) - dic_last_start_time.GetValueOrDefault(pid);
                    }
                    dic_last_start_time[pid] = item.TimeStamp;
                }
                if (item.State is PlayTimeItem.PlayState.Stop)
                {
                    if (dic_last_start_time.GetValueOrDefault(pid) != 0)
                    {
                        ts += item.TimeStamp - dic_last_start_time.GetValueOrDefault(pid);
                        dic_last_start_time[pid] = 0;
                    }
                }
                if (item.State is PlayTimeItem.PlayState.Play && i == items.Count - 1)
                {
                    ts += item.TimeStamp - dic_last_start_time.GetValueOrDefault(pid);
                }
                if (item.State is PlayTimeItem.PlayState.Error)
                {
                    dic_last_start_time[pid] = 0;
                }
                dic_last_time[pid] = item.TimeStamp;
            }
        }
        return TimeSpan.FromMilliseconds(ts);
    }


    public (DateTimeOffset Time, TimeSpan Span) GetLastPlayTime(GameBiz biz)
    {
        using var dapper = _database.CreateConnection();
        var start_item = dapper.QueryFirstOrDefault<PlayTimeItem>("SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND State = 1 ORDER BY TimeStamp DESC LIMIT 1;", new { biz });
        if (start_item != null)
        {
            var last_item = dapper.QueryFirstOrDefault<PlayTimeItem>("SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND Pid = @Pid ORDER BY TimeStamp DESC LIMIT 1;", new { biz, start_item.Pid });
            if (last_item != null)
            {
                return (DateTimeOffset.FromUnixTimeMilliseconds(start_item.TimeStamp), TimeSpan.FromMilliseconds(last_item.TimeStamp - start_item.TimeStamp));
            }
        }
        return (DateTimeOffset.MinValue, TimeSpan.Zero);
    }




    public void StartProcessToLog(GameBiz biz, Process process)
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppContext.BaseDirectory, "Starward.exe");
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"playtime --biz {biz} --pid {process.Id}",
                    CreateNoWindow = true,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start process to log play time");
        }
    }




}
