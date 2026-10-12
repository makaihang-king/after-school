using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Childhood.Editor
{
    /// <summary>使用真正 Unity JsonUtility 检查收藏的 null/class round-trip 与完整对局事务。</summary>
    public static class SaveRepositoryValidation
    {
        [Serializable] private sealed class ValidationReport
        {
            public bool passed;
            public string engine;
            public string saveDirectory;
            public bool observedInlineNullExpansion;
            public int checks;
            public List<string> observations = new List<string>();
            public List<string> errors = new List<string>();
        }

        [Serializable] private sealed class LegacyEnvelope { public string payload = ""; }

        [MenuItem("放学后/验证原生收藏存档")]
        public static void Run()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(root, "Logs", "QA", "Data");
            Directory.CreateDirectory(output);
            var report = new ValidationReport
            {
                engine = Application.unityVersion,
                saveDirectory = Path.Combine(output, "save-" + Guid.NewGuid().ToString("N"))
            };
            Exception failure = null;
            try
            {
                var repository = new SaveRepository(report.saveDirectory);
                var created = repository.LoadOrCreate();
                var nativeCopy = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(created));
                report.observedInlineNullExpansion = nativeCopy.pendingMatch != null && string.IsNullOrEmpty(nativeCopy.pendingMatch.matchId);
                Check(report, created.pendingMatch == null, "新建存档返回 null pending");
                Check(report, repository.Current.pendingMatch == null, "Current 副本规范化 null pending");
                Check(report, !repository.HasPendingMatch && repository.GetPendingMatch() == null, "新建存档没有幽灵约战");
                var reloaded = new SaveRepository(report.saveDirectory);
                Check(report, reloaded.LoadOrCreate().pendingMatch == null, "真实 JSON 写入并重载后 pending 仍为 null");
                Check(report, !reloaded.HasPendingMatch && reloaded.GetPendingMatch() == null, "读取 API 的 nullable 语义一致");

                var player = reloaded.PlayerCards.First(card => card.CanStake);
                var npc = reloaded.NpcCards("xiaoyu")[0];
                var match = reloaded.CreatePendingMatch("xiaoyu", "school_gate", RuleKind.Flip, new[] { player.instanceId }, new[] { npc.instanceId }, true, 41);
                Check(report, match.matchId == reloaded.GetPendingMatch().matchId && reloaded.HasPendingMatch, "真实约战保留有效编号");
                reloaded.UpdatePendingProgress(match.matchId, "{\"round\":1}", 1, 0, 0, 1, true);
                reloaded.AbortPendingMatch();
                var resumed = new SaveRepository(report.saveDirectory);
                resumed.LoadOrCreate();
                Check(report, resumed.GetPendingMatch().paused && resumed.GetPendingMatch().checkpoint.shotsUsed == 1, "原生暂停和进度 round-trip");
                resumed.ResumePendingMatch();
                var result = resumed.ResolvePendingMatch(match.matchId, MatchOutcome.PlayerWin, 2);
                Check(report, result.gainedCardIds.Count == 1 && resumed.GetInstance(npc.instanceId).ownerId == GameCatalog.PlayerOwnerId, "原生交易只移动约定实例");
                Check(report, resumed.Current.pendingMatch == null && !resumed.HasPendingMatch && resumed.GetPendingMatch() == null, "结算后三个读取 API 都清除 pending");
                var settled = new SaveRepository(report.saveDirectory);
                settled.LoadOrCreate();
                Check(report, settled.Current.pendingMatch == null && !settled.HasPendingMatch && settled.GetPendingMatch() == null, "结算后的写盘与重载不产生幽灵约战");
                long revision = settled.Current.revision;
                settled.ResolvePendingMatch(match.matchId, MatchOutcome.NpcWin, 999);
                Check(report, settled.Current.revision == revision && settled.Current.progress.totalMatches == 1, "原生重复结算仍然幂等");

                var practice = settled.CreatePracticeMatch("xiaoyu", "school_gate", RuleKind.Flip, 17, player.instanceId, true);
                var practiceReload = new SaveRepository(report.saveDirectory);
                practiceReload.LoadOrCreate();
                Check(report, practiceReload.GetPendingMatch().soloPractice, "原生恢复街边自己练手模式");
                practiceReload.ResolvePendingMatch(practice.matchId, MatchOutcome.Draw, 0);
                var final = new SaveRepository(report.saveDirectory);
                final.LoadOrCreate();
                Check(report, !final.HasPendingMatch && final.Current.progress.practiceMatches == 1, "练习结算同样清除 nullable pending");

                // 真实玩家旧档只读导入到独立验收目录，原目录逐文件比对，绝不切换玩家收藏。
                string legacyDirectory = Path.Combine(Application.persistentDataPath, "SummerAfterSchool");
                string legacyFile = Path.Combine(legacyDirectory, "collection.json");
                if (File.Exists(legacyFile))
                {
                    var originalFiles = Directory.GetFiles(legacyDirectory).ToDictionary(path => path, File.ReadAllBytes);
                    var envelope = JsonUtility.FromJson<LegacyEnvelope>(File.ReadAllText(legacyFile));
                    var oldSave = JsonUtility.FromJson<GameSave>(envelope.payload);
                    var importedRepository = new SaveRepository(Path.Combine(output, "import-" + Guid.NewGuid().ToString("N")), legacyDirectory);
                    var imported = importedRepository.LoadOrCreate();
                    Check(report, imported.schemaVersion == 2 && imported.profileId == oldSave.profileId && imported.glassMarbles == 12,
                        "真实旧档原生升级 schema2，保留身份并一次给予12珠");
                    Check(report, oldSave.cards.All(card => imported.cards.Any(copy => copy.instanceId == card.instanceId && copy.definitionId == card.definitionId && copy.ownerId == card.ownerId && copy.history.Count >= card.history.Count)),
                        "真实旧档全部原实例、图案、归属与转手记录保留");
                    Check(report, imported.progress.totalMatches == oldSave.progress.totalMatches && imported.progress.marbleRounds == oldSave.progress.marbleRounds && imported.progress.marbleBestScore == oldSave.progress.marbleBestScore,
                        "真实旧档对局及旧玻璃珠成绩保留");
                    var importedReload = new SaveRepository(Path.GetDirectoryName(importedRepository.SavePath), legacyDirectory).LoadOrCreate();
                    Check(report, importedReload.revision == imported.revision && importedReload.glassMarbles == 12,
                        "真实旧档升级重载不重复导入或补发启动珠");
                    Check(report, Directory.GetFiles(legacyDirectory).Length == originalFiles.Count && originalFiles.All(entry => File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)),
                        "真实旧存档目录每个原文件及文件数量完全未变");
                }

                // 保留原始 round-trip 文本，便于观察该 Editor 版本的内联 class 行为。
                File.WriteAllText(Path.Combine(output, "native-null-roundtrip.json"), JsonUtility.ToJson(nativeCopy, true));
                report.passed = true;
            }
            catch (Exception exception)
            {
                failure = exception;
                report.errors.Add(exception.ToString());
            }
            File.WriteAllText(Path.Combine(output, "native-save-report.json"), JsonUtility.ToJson(report, true));
            if (failure != null) throw new Exception("原生收藏存档验证失败。报告：" + output, failure);
            Debug.Log("SUMMER_NATIVE_SAVE_VALIDATION_OK checks=" + report.checks + " inlineNullExpanded=" + report.observedInlineNullExpansion);
        }

        private static void Check(ValidationReport report, bool passed, string observation)
        {
            if (!passed) throw new InvalidOperationException(observation);
            report.checks++;
            report.observations.Add(observation);
        }
    }
}
