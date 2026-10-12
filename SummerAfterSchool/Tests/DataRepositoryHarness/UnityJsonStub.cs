using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

// 只替代 Unity 的字段 JSON 序列化，以便没有 Editor 授权时验证纯数据逻辑。
// 此目录不在 Assets 中，绝不进入 Unity 程序集。
namespace UnityEngine
{
    public static class Application
    {
        public static string DataPath;
        public static string persistentDataPath { get { return DataPath ?? Path.GetTempPath(); } }
    }

    public static class JsonUtility
    {
        private static JsonSerializerOptions Options(bool pretty)
        {
            return new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true, WriteIndented = pretty };
        }
        public static string ToJson(object value, bool prettyPrint = false)
        {
            string json = JsonSerializer.Serialize(value, value.GetType(), Options(prettyPrint));
            var save = value as Childhood.GameSave;
            if (save != null && save.pendingMatch == null)
            {
                // 原生 Unity 会把 null 的内联 class 展开为空对象；刻意复现，避免替身掩盖回归。
                var node = JsonNode.Parse(json);
                node["pendingMatch"] = JsonSerializer.SerializeToNode(new Childhood.PendingMatch { matchId = "", npcId = "", locationId = "" }, Options(false));
                json = node.ToJsonString(Options(prettyPrint));
            }
            if (save != null)
            {
                var node = JsonNode.Parse(json);
                if (save.tournament == null) node["tournament"] = JsonSerializer.SerializeToNode(new Childhood.TournamentRun { runId = "" }, Options(false));
                if (save.activeErrand == null) node["activeErrand"] = JsonSerializer.SerializeToNode(new Childhood.PendingErrand { completionId = "" }, Options(false));
                json = node.ToJsonString(Options(prettyPrint));
            }
            return json;
        }
        public static T FromJson<T>(string json) { return JsonSerializer.Deserialize<T>(json, Options(false)); }
    }
}
