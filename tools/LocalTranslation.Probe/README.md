# 本机中英翻译模型探针

此工具只验证候选模型，不作为用户试用包。模型文件放在仓库忽略的 `artifacts/models/`，不提交 Git。

固定来源：`facebook/m2m100_418M` 的修订版 `55c2e61bbf05dfb8d7abccdc3fae6fc8512fd636`；本次先采用 LinguaRelay v0.1.5 发布的 CTranslate2 转换产物。`model-manifest.json` 固定发布归档 SHA-256 及翻译目录中七个文件的 SHA-256。哈希只能证实下载内容与发布清单一致，无法独立证明第三方转换过程；正式发布前还要核对转换产物来源与第三方许可文件。

在项目根目录运行：

```powershell
$env:UV_PYTHON_INSTALL_DIR = "$PWD/artifacts/uv-python"
$env:UV_CACHE_DIR = "$PWD/artifacts/uv-cache"
uv venv --python 3.11 artifacts/localtranslation-venv
uv pip install --python artifacts/localtranslation-venv/Scripts/python.exe -r tools/LocalTranslation.Probe/requirements.txt

curl.exe --noproxy "*" -L --fail -C - -o artifacts/models/LinguaRelay-0.1.5-models-small.zip https://github.com/MuzeAnisichael/LinguaRelay/releases/download/v0.1.5/LinguaRelay-0.1.5-models-small.zip
artifacts/localtranslation-venv/Scripts/python.exe tools/LocalTranslation.Probe/probe.py prepare --archive artifacts/models/LinguaRelay-0.1.5-models-small.zip
artifacts/localtranslation-venv/Scripts/python.exe tools/LocalTranslation.Probe/probe.py benchmark --device cpu --output artifacts/localtranslation-cpu.json
artifacts/localtranslation-venv/Scripts/python.exe tools/LocalTranslation.Probe/probe.py benchmark --device cuda --output artifacts/localtranslation-cuda.json
```

`--device cuda` 只有本机 CTranslate2 能加载 CUDA 时才会成功；失败应记录原始错误，不当作 CPU 结果。基准数据只测固定文字的模型加载和翻译，不包括音频采集、Whisper 识别或字幕绘制。`--repetitions 3` 的首轮结果保留为热身，后两轮分别统计 P50／P95。

需要验证与 Whisper 同时运行时，可加 `--hold-seconds 60`。探针输出报告后会保留已加载的模型 60 秒，供另一进程执行真实 WAV 识别。

## M2M100 失败后的中英专用模型对照

M2M100 在固定样例中把“断网”译反，beam 5 复测仍错误。按[可行性报告](../../docs/reports/local-zh-en-model-benchmark.md)继续比较两套 OPUS-MT 中英专用模型。归档来自 [Live Translator 模型仓库](https://huggingface.co/Unbakedcookie/live-translator-models/tree/50473df5681764ebdef107dd78f7d21da66c28a7)，`opus_probe.py` 固定两包的 SHA-256、原始模型修订版及方向。两个模型按包内声明采用 CC-BY-4.0，正式分发必须保留原作者署名、许可链接和转换说明。

```powershell
curl.exe --noproxy "*" -L --fail -o artifacts/models/opus-mt-en-zh.zip https://huggingface.co/Unbakedcookie/live-translator-models/resolve/50473df5681764ebdef107dd78f7d21da66c28a7/opus-mt-en-zh.zip
curl.exe --noproxy "*" -L --fail -o artifacts/models/opus-mt-zh-en.zip https://huggingface.co/Unbakedcookie/live-translator-models/resolve/50473df5681764ebdef107dd78f7d21da66c28a7/opus-mt-zh-en.zip
Expand-Archive artifacts/models/opus-mt-en-zh.zip artifacts/models/opus-mt-en-zh
Expand-Archive artifacts/models/opus-mt-zh-en.zip artifacts/models/opus-mt-zh-en
artifacts/localtranslation-venv/Scripts/python.exe tools/LocalTranslation.Probe/opus_probe.py --source en --output artifacts/opus-en-zh.json
artifacts/localtranslation-venv/Scripts/python.exe tools/LocalTranslation.Probe/opus_probe.py --source zh --output artifacts/opus-zh-en.json
artifacts/localtranslation-venv/Scripts/python.exe tools/LocalTranslation.Probe/dual_probe.py --hold-seconds 90
```

这两个转换模型必须把 `</s>` 放在原文 token 末尾；英文→中文还要在开头添加 `>>cmn_Hans<<`。漏掉末尾标记会出现重复输出。`opus_probe.py` 默认使用正确格式，`--no-source-eos` 仅供复现错误。

测试：

```powershell
artifacts/localtranslation-venv/Scripts/python.exe -m unittest discover -s tools/LocalTranslation.Probe/tests -v
```
