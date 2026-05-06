using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class StrategicWorldSaveService
{
    private const string SaveDirectory = "user://saves";
    private const string SaveFileName = "strategic_world_v1.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SavePath => Path.Combine(ProjectSettings.GlobalizePath(SaveDirectory), SaveFileName);

    public bool Save(StrategicWorldState state, out string message)
    {
        message = "";
        if (state == null)
        {
            message = "没有可保存的战略世界状态。";
            return false;
        }

        try
        {
            string directory = ProjectSettings.GlobalizePath(SaveDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(state, JsonOptions));
            message = "战略世界已保存。";
            GameLog.Info(nameof(StrategicWorldSaveService), $"StrategicWorldSaved path={SavePath} tick={state.WorldTick}");
            return true;
        }
        catch (Exception exception)
        {
            message = $"保存失败：{exception.Message}";
            GameLog.Warn(nameof(StrategicWorldSaveService), $"Save failed error={exception.Message}");
            return false;
        }
    }

    public bool TryLoad(out StrategicWorldState state, out string message)
    {
        state = null;
        message = "";

        try
        {
            if (!File.Exists(SavePath))
            {
                message = "没有找到战略世界存档。";
                return false;
            }

            state = JsonSerializer.Deserialize<StrategicWorldState>(File.ReadAllText(SavePath), JsonOptions);
            if (state == null)
            {
                message = "战略世界存档为空。";
                return false;
            }

            message = "战略世界已读取。";
            GameLog.Info(nameof(StrategicWorldSaveService), $"StrategicWorldLoaded path={SavePath} tick={state.WorldTick}");
            return true;
        }
        catch (Exception exception)
        {
            message = $"读取失败：{exception.Message}";
            GameLog.Warn(nameof(StrategicWorldSaveService), $"Load failed error={exception.Message}");
            return false;
        }
    }
}
