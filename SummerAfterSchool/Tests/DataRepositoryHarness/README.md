# 收藏与存档回归验证

在项目根目录运行：

```powershell
dotnet run --project Tests/DataRepositoryHarness/DataRepositoryHarness.csproj --configuration Release
```

需 .NET 10 SDK。测试链接实际的 `GameCatalog.cs`、`CampaignCatalog.cs` 和 `SaveRepository.cs`，只替代 `Application.persistentDataPath` 与 `JsonUtility` 的字段 JSON 接口，不加载 Unity Editor。

258 项断言覆盖 42 张卡、八处场景、批准剧情表与对白、八关首通奖励、三轮杯赛与连续平局重赛、两条路线交叉的 Boss 招牌归属和商店解锁；覆盖练习片交易与真局保护、单次珍藏确认、同稀有度每份清单每日换片、固定价格及买卖套利防护、零库存跑腿购片恢复、幂等收据和冻结环境。

存档回归实际创建文件、持有独占锁并验证原子失败，覆盖 schema1 真实/练习 pending 只读导入、所有旧 GUID 与历史、独立 v2 默认目录、未来版本拒读而不回退、备份恢复、多客户端冲突、钱包/日期整数边界、1–12 张赌注上限及 NPC 普通片补购。停用的弹珠纪录仍保留旧字段与旧业务校验。替身刻意复现 Unity 把 null 内联 class 展开为空对象的行为，防止幽灵约战、杯赛或跑腿。

临时存档创建在操作系统临时目录，测试结束只清理唯一的测试子目录；默认目录导入测试也将 persistentDataPath 指向该临时子目录，绝不访问玩家真实存档。

项目另外提供 `Childhood.Editor.SaveRepositoryValidation.Run`，可从 Unity 菜单“放学后/验证原生收藏存档”或 `-executeMethod` 运行。它使用真正 `JsonUtility` 验证 nullable class、创建对局、暂停恢复、结算清除与幂等；在 Unity `6000.3.25f1` 中十三项已通过。报告在 `Logs/QA/Data/native-save-report.json`，原始 null round-trip 文本同时留存。

纯数据与原生序列化验证不代替实际输入、物理手感、音画或硬件性能测试。
