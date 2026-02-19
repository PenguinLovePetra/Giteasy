using System;
using System.Collections.Generic;
using System.IO;
using Giteasy.Models;
using Microsoft.Data.Sqlite;

namespace Giteasy.Services;

/// <summary>
/// SQLite を使用したデータ永続化サービス。
/// プロジェクト情報と設定値を管理します。
/// </summary>
public class DatabaseService
{
    private readonly string _dbPath;

    public DatabaseService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitEasy", "Database");
        Directory.CreateDirectory(appDataDir);
        _dbPath = Path.Combine(appDataDir, "giteasy.db");
        InitializeDatabase();
    }

    private string ConnectionString => $"Data Source={_dbPath}";

    // ─── 初期化 ─────────────────────────

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Projects (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL,
                LocalPath   TEXT NOT NULL UNIQUE,
                RemoteUrl   TEXT NOT NULL DEFAULT '',
                CreatedAt   TEXT NOT NULL,
                LastOpenedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key   TEXT PRIMARY KEY,
                Value TEXT NOT NULL DEFAULT ''
            );
        ";
        cmd.ExecuteNonQuery();
    }

    // ─── プロジェクト CRUD ─────────────────

    public List<ProjectInfo> GetAllProjects()
    {
        var projects = new List<ProjectInfo>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Projects ORDER BY LastOpenedAt DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            projects.Add(new ProjectInfo
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                LocalPath = reader.GetString(2),
                RemoteUrl = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                LastOpenedAt = DateTime.Parse(reader.GetString(5)),
            });
        }
        return projects;
    }

    public void AddProject(ProjectInfo project)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO Projects (Name, LocalPath, RemoteUrl, CreatedAt, LastOpenedAt)
            VALUES ($name, $localPath, $remoteUrl, $createdAt, $lastOpenedAt)";
        cmd.Parameters.AddWithValue("$name", project.Name);
        cmd.Parameters.AddWithValue("$localPath", project.LocalPath);
        cmd.Parameters.AddWithValue("$remoteUrl", project.RemoteUrl);
        cmd.Parameters.AddWithValue("$createdAt", project.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$lastOpenedAt", project.LastOpenedAt.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void UpdateLastOpened(string localPath)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Projects SET LastOpenedAt = $now WHERE LocalPath = $path";
        cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$path", localPath);
        cmd.ExecuteNonQuery();
    }

    public void DeleteProject(int id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Projects WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ─── 設定 KVS ─────────────────────────

    public string? GetSetting(string key)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }
}
