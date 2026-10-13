# 童年游戏 · 放学后

以 1990 年代中国小镇为背景的童年游戏合集，当前收录 Unity 3D 打圆卡游戏**《放学后》**。

- **游戏工程**：[`SummerAfterSchool`](SummerAfterSchool)（Unity 6000.3.25f1）—— 故事、锦标赛、自由约战、铁盒、小卖部、街坊跑腿；玻璃珠是买卖圆片的货币
- **设计文档**：`打圆片_游戏策划方案_第一版.docx`

## 直接玩

仓库当前仅含源码，可按下面的方法在 Unity 中构建出 Windows / Android 版本。

## 在 Unity 中继续开发

使用 Unity 6000.3.25f1 打开 `SummerAfterSchool`（或双击 `打开Unity工程.cmd`），打开 `Assets/Scenes/SummerTown.unity` 点击 Play。完整说明见 [SummerAfterSchool/README.md](SummerAfterSchool/README.md)。

```powershell
# 导出 Windows 试玩版
powershell -ExecutionPolicy Bypass -File .\SummerAfterSchool\Tools\build.ps1
```

> 注：Unity 的 `Builds/`（各版本成品，约 2GB）不入库，仓库仅含源码；成品可按上述脚本重新构建。

## 许可证

- **代码**：[MIT License](LICENSE)
- **美术 / 模型 / 音频素材与设计文档**：[CC BY-NC 4.0](LICENSE-ASSETS)（署名—非商业性使用），商业使用需联系作者授权

