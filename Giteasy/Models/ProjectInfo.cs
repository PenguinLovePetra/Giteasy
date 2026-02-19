using System;

namespace Giteasy.Models;

/// <summary>
/// 登録済みプロジェクトの情報。
/// </summary>
public class ProjectInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public string RemoteUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastOpenedAt { get; set; } = DateTime.Now;
}
