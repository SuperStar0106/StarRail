﻿using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Starward.Core.GameRecord.Genshin.TravelersDiary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Starward.Services;

internal class DatabaseService
{

    private readonly ILogger<DatabaseService> _logger;


    private readonly string _databasePath;


    private readonly string _connectionString;



    static DatabaseService()
    {
        SqlMapper.AddTypeHandler(new DapperSqlMapper.DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new DapperSqlMapper.TravelersDiaryPrimogemsMonthGroupStatsListHandler());
        SqlMapper.AddTypeHandler(new DapperSqlMapper.StringListHandler());
    }



    public DatabaseService(ILogger<DatabaseService> logger, string databaseFolder)
    {
        _logger = logger;
        _databasePath = Path.Combine(databaseFolder, "StarwardDatabase.db");
        _logger.LogInformation($"Database path is '{_databasePath}'");
        _connectionString = $"DataSource={_databasePath};";
        InitializeDatabase();
    }




    public SqliteConnection CreateConnection()
    {
        var con = new SqliteConnection(_connectionString);
        con.Open();
        return con;
    }





    private void InitializeDatabase()
    {
        using var con = CreateConnection();
        var version = con.QueryFirstOrDefault<int>("PRAGMA USER_VERSION;");
        _logger.LogInformation($"Database version is {version}, target version is {DatabaseSqls.Count}.");
        if (version == 0)
        {
            con.Execute("PRAGMA JOURNAL_MODE = WAL;");
        }
        foreach (var sql in DatabaseSqls.Skip(version))
        {
            con.Execute(sql);
        }
    }



    public void AutoBackupDatabase()
    {
        try
        {
            if (AppConfig.EnableAutoBackupDatabase)
            {
                var interval = Math.Clamp(AppConfig.BackupIntervalInDays, 1, int.MaxValue);
                GetValue<string>("AutoBackupDatabase", out var lastTime);
                if ((DateTime.Now - lastTime).TotalDays > interval)
                {
                    var dir = Path.Combine(AppConfig.UserDataFolder, "Backup");
                    Directory.CreateDirectory(dir);
                    var file = Path.Combine(dir, $"Database_{DateTime.Now:yyyyMMdd}.db");
                    using var backupCon = new SqliteConnection($"DataSource={file};");
                    backupCon.Open();
                    using var con = CreateConnection();
                    con.Execute("VACUUM;");
                    con.BackupDatabase(backupCon);
                    SetValue("AutoBackupDatabase", file);
                }
            }
        }
        catch { }
    }




    private class KVT
    {

        public KVT() { }


        public KVT(string key, string value, DateTime time)
        {
            Key = key;
            Value = value;
            Time = time;
        }

        public string Key { get; set; }

        public string Value { get; set; }

        public DateTime Time { get; set; }
    }





    public T? GetValue<T>(string key, out DateTime time, T? defaultValue = default)
    {
        time = DateTime.MinValue;
        try
        {
            using var con = CreateConnection();
            var kvt = con.QueryFirstOrDefault<KVT>("SELECT * FROM KVT WHERE Key = @key LIMIT 1;", new { key });
            if (kvt != null)
            {
                time = kvt.Time;
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter == null)
                {
                    return defaultValue;
                }
                return (T?)converter.ConvertFromString(kvt.Value);
            }
            else
            {
                return defaultValue;
            }
        }
        catch
        {
            return defaultValue;
        }
    }



    public bool TryGetValue<T>(string key, out T? result, out DateTime time, T? defaultValue = default)
    {
        result = defaultValue;
        time = DateTime.MinValue;
        try
        {
            using var con = CreateConnection();
            var kvt = con.QueryFirstOrDefault<KVT>("SELECT * FROM KVT WHERE Key = @key LIMIT 1;", new { key });
            if (kvt != null)
            {
                time = kvt.Time;
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter == null)
                {
                    return false;
                }
                result = (T?)converter.ConvertFromString(kvt.Value);
                return true;
            }
            else
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }



    public void SetValue<T>(string key, T value, DateTime? time = null)
    {
        try
        {
            using var con = CreateConnection();
            con.Execute("INSERT OR REPLACE INTO KVT (Key, Value, Time) VALUES (@Key, @Value, @Time);", new KVT(key, value?.ToString() ?? "", time ?? DateTime.Now));

        }
        catch { }
    }


    #region Database Structure


    private static readonly List<string> DatabaseSqls = new() { Sql_v1, Sql_v2, Sql_v3, Sql_v4 };


    private const string Sql_v1 = """
        BEGIN TRANSACTION;

        CREATE TABLE IF NOT EXISTS KVT
        (
            Key   TEXT NOT NULL PRIMARY KEY,
            Value TEXT NOT NULL,
            Time  TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS GameAccount
        (
            SHA256  TEXT    NOT NULL PRIMARY KEY,
            GameBiz INTEGER NOT NULL,
            Uid     INTEGER NOT NULL,
            Name    TEXT    NOT NULL,
            Value   BLOB    NOT NULL,
            Time    TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_GameAccount_GameBiz ON GameAccount (GameBiz);

        CREATE TABLE IF NOT EXISTS GachaLogUrl
        (
            GameBiz INTEGER NOT NULL,
            Uid     INTEGER NOT NULL,
            Url     TEXT    NOT NULL,
            Time    TEXT    NOT NULL,
            PRIMARY KEY (GameBiz, Uid)
        );

        CREATE TABLE IF NOT EXISTS GenshinGachaItem
        (
            Uid       INTEGER NOT NULL,
            Id        INTEGER NOT NULL,
            Name      TEXT    NOT NULL,
            Time      TEXT    NOT NULL,
            ItemId    INTEGER NOT NULL,
            ItemType  TEXT    NOT NULL,
            RankType  INTEGER NOT NULL,
            GachaType INTEGER NOT NULL,
            Count     INTEGER NOT NULL,
            Lang      TEXT,
            PRIMARY KEY (Uid, Id)
        );
        CREATE INDEX IF NOT EXISTS IX_GenshinGachaItem_Id ON GenshinGachaItem (Id);
        CREATE INDEX IF NOT EXISTS IX_GenshinGachaItem_RankType ON GenshinGachaItem (RankType);
        CREATE INDEX IF NOT EXISTS IX_GenshinGachaItem_GachaType ON GenshinGachaItem (GachaType);

        CREATE TABLE IF NOT EXISTS StarRailGachaItem
        (
            Uid       INTEGER NOT NULL,
            Id        INTEGER NOT NULL,
            Name      TEXT    NOT NULL,
            Time      TEXT    NOT NULL,
            ItemId    INTEGER NOT NULL,
            ItemType  TEXT    NOT NULL,
            RankType  INTEGER NOT NULL,
            GachaType INTEGER NOT NULL,
            GachaId   INTEGER NOT NULL,
            Count     INTEGER NOT NULL,
            Lang      TEXT,
            PRIMARY KEY (Uid, Id)
        );
        CREATE INDEX IF NOT EXISTS IX_StarRailGachaItem_Id ON StarRailGachaItem (Id);
        CREATE INDEX IF NOT EXISTS IX_StarRailGachaItem_RankType ON StarRailGachaItem (RankType);
        CREATE INDEX IF NOT EXISTS IX_StarRailGachaItem_GachaType ON StarRailGachaItem (GachaType);

        PRAGMA USER_VERSION = 1;
        COMMIT TRANSACTION;
        """;

    private const string Sql_v2 = """
        BEGIN TRANSACTION;

        CREATE TABLE IF NOT EXISTS PlayTimeItem
        (
            TimeStamp INTEGER PRIMARY KEY,
            GameBiz   INTEGER NOT NULL,
            Pid       INTEGER NOT NULL,
            State     INTEGER NOT NULL,
            CursorPos INTEGER NOT NULL,
            Message   TEXT
        );
        CREATE INDEX IF NOT EXISTS IX_PlayTimeItem_GameBiz ON PlayTimeItem(GameBiz);
        CREATE INDEX IF NOT EXISTS IX_PlayTimeItem_Pid ON PlayTimeItem(Pid);
        CREATE INDEX IF NOT EXISTS IX_PlayTimeItem_State ON PlayTimeItem(State);

        PRAGMA USER_VERSION = 2;
        COMMIT TRANSACTION;
        """;

    private const string Sql_v3 = """
        BEGIN TRANSACTION;

        CREATE TABLE IF NOT EXISTS GameAccount_dg_tmp
        (
            SHA256  TEXT    NOT NULL,
            GameBiz INTEGER NOT NULL,
            Uid     INTEGER NOT NULL,
            Name    TEXT    NOT NULL,
            Value   BLOB    NOT NULL,
            Time    TEXT    NOT NULL,
            PRIMARY KEY (SHA256, GameBiz)
        );

        INSERT INTO GameAccount_dg_tmp(SHA256, GameBiz, Uid, Name, Value, Time)
        SELECT SHA256, GameBiz, Uid, Name, Value, Time
        FROM GameAccount;

        DROP TABLE IF EXISTS GameAccount;
        ALTER TABLE GameAccount_dg_tmp RENAME TO GameAccount;
        CREATE INDEX IF NOT EXISTS IX_GameAccount_GameBiz ON GameAccount (GameBiz);

        PRAGMA USER_VERSION = 3;
        COMMIT TRANSACTION;
        """;

    private const string Sql_v4 = """
        BEGIN TRANSACTION;

        CREATE TABLE IF NOT EXISTS Setting
        (
            Key   TEXT NOT NULL PRIMARY KEY,
            Value TEXT
        );

        CREATE TABLE IF NOT EXISTS GameRecordUser
        (
            Uid       INTEGER NOT NULL,
            IsHoyolab INTEGER NOT NULL,
            Nickname  TEXT,
            Avatar    TEXT,
            Introduce TEXT,
            Gender    INTEGER NOT NULL,
            AvatarUrl TEXT,
            Pendant   TEXT,
            Cookie    TEXT,
            PRIMARY KEY (Uid, IsHoyolab)
        );

        CREATE TABLE IF NOT EXISTS GameRecordRole
        (
            Uid        INTEGER NOT NULL,
            GameBiz    TEXT    NOT NULL,
            Nickname   TEXT,
            Level      INTEGER NOT NULL,
            Region     TEXT    NOT NULL,
            RegionName TEXT,
            Cookie     TEXT,
            PRIMARY KEY (Uid, GameBiz)
        );
        CREATE INDEX IF NOT EXISTS IX_GameRecordRole_GameBiz ON GameRecordRole (GameBiz);

        CREATE TABLE IF NOT EXISTS SpiralAbyssInfo
        (
            Uid              INTEGER NOT NULL,
            ScheduleId       INTEGER NOT NULL,
            StartTime        TEXT    NOT NULL,
            EndTime          TEXT    NOT NULL,
            TotalBattleCount INTEGER NOT NULL,
            TotalWinCount    INTEGER NOT NULL,
            MaxFloor         TEXT,
            TotalStar        INTEGER NOT NULL,
            Value            TEXT    NOT NULL,
            PRIMARY KEY (Uid, ScheduleId)
        );
        CREATE INDEX IF NOT EXISTS IX_SpiralAbyssInfo_ScheduleId ON SpiralAbyssInfo (ScheduleId);

        CREATE TABLE IF NOT EXISTS TravelNotesMonthData
        (
            Uid                   INTEGER NOT NULL,
            Year                  INTEGER NOT NULL,
            Month                 INTEGER NOT NULL,
            CurrentPrimogems      INTEGER NOT NULL,
            CurrentMora           INTEGER NOT NULL,
            LastPrimogems         INTEGER NOT NULL,
            LastMora              INTEGER NOT NULL,
            CurrentPrimogemsLevel INTEGER NOT NULL,
            PrimogemsChangeRate   INTEGER NOT NULL,
            MoraChangeRate        INTEGER NOT NULL,
            PrimogemsGroupBy      TEXT    NOT NULL,
            PRIMARY KEY (Uid, Year, Month)
        );

        CREATE TABLE IF NOT EXISTS TravelersDiaryAwardItem
        (
            Id         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Uid        INTEGER NOT NULL,
            Year       INTEGER NOT NULL,
            Month      INTEGER NOT NULL,
            Type       INTEGER NOT NULL,
            ActionId   INTEGER NOT NULL,
            ActionName TEXT,
            Time       TEXT    NOT NULL,
            Number     INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_TravelersDiaryAwardItem_Uid_Year_Month ON TravelersDiaryAwardItem (Uid, Year, Month);
        CREATE INDEX IF NOT EXISTS IX_TravelersDiaryAwardItem_Type ON TravelersDiaryAwardItem (Type);
        CREATE INDEX IF NOT EXISTS IX_TravelersDiaryAwardItem_Time ON TravelersDiaryAwardItem (Time);

        CREATE TABLE IF NOT EXISTS ForgottenHallInfo
        (
            Uid        INTEGER NOT NULL,
            ScheduleId INTEGER NOT NULL,
            BeginTime  TEXT    NOT NULL,
            EndTime    TEXT    NOT NULL,
            StarNum    INTEGER NOT NULL,
            MaxFloor   TEXT,
            BattleNum  INTEGER NOT NULL,
            HasData    INTEGER NOT NULL,
            Value      TEXT    NOT NULL,
            PRIMARY KEY (Uid, ScheduleId)
        );
        CREATE INDEX IF NOT EXISTS IX_ForgottenHallInfo_ScheduleId ON ForgottenHallInfo (ScheduleId);

        CREATE TABLE IF NOT EXISTS SimulatedUniverseRecord
        (
            Uid           INTEGER NOT NULL,
            ScheduleId    INTEGER NOT NULL,
            FinishCount   INTEGER NOT NULL,
            ScheduleBegin TEXT    NOT NULL,
            ScheduleEnd   TEXT    NOT NULL,
            HasData       INTEGER NOT NULL,
            Value         TEXT    NOT NULL,
            PRIMARY KEY (Uid, ScheduleId)
        );
        CREATE INDEX IF NOT EXISTS IX_SimulatedUniverseRecord_ScheduleId ON SimulatedUniverseRecord (ScheduleId);

        CREATE TABLE IF NOT EXISTS TrailblazeCalendarMonthData
        (
            Uid              INTEGER NOT NULL,
            Month            TEXT    NOT NULL,
            CurrentHcoin     INTEGER NOT NULL,
            CurrentRailsPass INTEGER NOT NULL,
            LastHcoin        INTEGER NOT NULL,
            LastRailsPass    INTEGER NOT NULL,
            HcoinRate        INTEGER NOT NULL,
            RailsRate        INTEGER NOT NULL,
            GroupBy          TEXT    NOT NULL,
            PRIMARY KEY (Uid, Month)
        );

        CREATE TABLE IF NOT EXISTS TrailblazeCalendarDetailItem
        (
            Id         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Uid        INTEGER NOT NULL,
            Month      TEXT    NOT NULL,
            Type       INTEGER NOT NULL,
            Action     TEXT    NOT NULL,
            ActionName TEXT    NOT NULL,
            Time       TEXT    NOT NULL,
            Number     INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_TrailblazeCalendarDetailItem_Uid_Month ON TrailblazeCalendarDetailItem (Uid, Month);
        CREATE INDEX IF NOT EXISTS IX_TrailblazeCalendarDetailItem_Type ON TrailblazeCalendarDetailItem (Type);
        CREATE INDEX IF NOT EXISTS IX_TrailblazeCalendarDetailItem_Time ON TrailblazeCalendarDetailItem (Time);

        PRAGMA USER_VERSION = 4;
        COMMIT TRANSACTION;
        """;


    #endregion




    #region Sql Mapper



    internal class DapperSqlMapper
    {

        private static JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNameCaseInsensitive = true };

        public class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
        {
            public override DateTimeOffset Parse(object value)
            {
                if (value is string str)
                {
                    return DateTimeOffset.Parse(str);
                }
                else
                {
                    return new DateTimeOffset();
                }
            }

            public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
            {
                parameter.Value = value.ToString();
            }
        }



        public class TravelersDiaryPrimogemsMonthGroupStatsListHandler : SqlMapper.TypeHandler<List<TravelersDiaryPrimogemsMonthGroupStats>>
        {
            public override List<TravelersDiaryPrimogemsMonthGroupStats> Parse(object value)
            {
                if (value is string str)
                {
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        return JsonSerializer.Deserialize<List<TravelersDiaryPrimogemsMonthGroupStats>>(str)!;
                    }
                }
                return new();
            }

            public override void SetValue(IDbDataParameter parameter, List<TravelersDiaryPrimogemsMonthGroupStats> value)
            {
                parameter.Value = JsonSerializer.Serialize(value);
            }
        }


        public class StringListHandler : SqlMapper.TypeHandler<List<string>>
        {
            public override List<string> Parse(object value)
            {
                if (value is string str)
                {
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        return JsonSerializer.Deserialize<List<string>>(str)!;
                    }
                }
                return new();
            }

            public override void SetValue(IDbDataParameter parameter, List<string> value)
            {
                parameter.Value = JsonSerializer.Serialize(value, JsonSerializerOptions);
            }
        }


    }


    #endregion



}
