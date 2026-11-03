"""Prepare and benchmark a pinned local Chinese/English translation model."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import subprocess
import time
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MANIFEST = Path(__file__).with_name("model-manifest.json")
SAMPLES = {
    "zh": [
        "你好，今天的天气怎么样？",
        "请在下一站下车。",
        "我正在测试实时翻译字幕。",
        "这段视频介绍了如何制作晚餐。",
        "如果网络断开，软件还能继续工作吗？",
        "请慢一点说，我想确认每个词都被识别正确。",
        "会议将在十分钟后开始。",
        "火车已经到站，请带好随身物品。",
    ],
    "en": [
        "Hello, how is the weather today?",
        "Please get off at the next stop.",
        "I am testing real-time translation captions.",
        "This video explains how to make dinner.",
        "Can the application continue when the network is disconnected?",
        "Please speak more slowly so I can check every word.",
        "The meeting will start in ten minutes.",
        "The train has arrived. Please take your belongings.",
    ],
}


def validate_route(source: str, target: str) -> tuple[str, str]:
    if (source, target) not in (("zh", "en"), ("en", "zh")):
        raise ValueError(f"unsupported route: {source}->{target}")
    return source, target


def encode_text(tokenizer, text: str, source: str) -> list[str] | None:
    if not text.strip():
        return None
    pieces = tokenizer.encode(text.strip(), out_type=str)
    if len(pieces) > 256:
        raise ValueError("translation input is too long (over 256 tokens)")
    return [f"__{source}__", *pieces, "</s>"]


def percentile(samples: list[float], percent: int) -> float:
    if not samples or not 0 <= percent <= 100:
        raise ValueError("percentile requires samples and a percentage from 0 to 100")
    values = sorted(samples)
    position = (len(values) - 1) * percent / 100
    lower = math.floor(position)
    upper = math.ceil(position)
    return round(values[lower] + (values[upper] - values[lower]) * (position - lower), 2)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def prepare(archive: Path, model_dir: Path) -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if sha256_file(archive) != manifest["archive_sha256"]:
        raise ValueError("archive SHA-256 does not match the pinned GitHub release")

    model_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as bundle:
        entries = {Path(item.filename).name: item for item in bundle.infolist()
                   if "/m2m100_418m_ct2/" in "/" + item.filename.replace("\\", "/")
                   and not item.is_dir()}
        for name, expected in manifest["files"].items():
            entry = entries.get(name)
            if entry is None or entry.file_size != expected["size"]:
                raise ValueError(f"missing or wrong-size model file: {name}")
            destination = model_dir / name
            temporary = model_dir / f"{name}.part"
            with bundle.open(entry) as source, temporary.open("wb") as output:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    output.write(chunk)
            if sha256_file(temporary) != expected["sha256"]:
                temporary.unlink(missing_ok=True)
                raise ValueError(f"model file SHA-256 mismatch: {name}")
            temporary.replace(destination)
            print(f"verified {name}", flush=True)


def gpu_memory_mib() -> int | None:
    try:
        result = subprocess.run(
            ["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"],
            capture_output=True, text=True, check=True, timeout=5,
        )
        return int(result.stdout.strip().splitlines()[0])
    except (OSError, ValueError, subprocess.SubprocessError, IndexError):
        return None


def benchmark(model_dir: Path, device: str, repetitions: int,
              beam_size: int, repetition_penalty: float,
              no_repeat_ngram_size: int) -> tuple[dict, object]:
    import ctranslate2
    import psutil
    import sentencepiece
    from opencc import OpenCC

    for name in json.loads(MANIFEST.read_text(encoding="utf-8"))["files"]:
        if not (model_dir / name).is_file():
            raise FileNotFoundError(model_dir / name)
    if repetitions < 1 or beam_size < 1 or repetition_penalty <= 0 or no_repeat_ngram_size < 0:
        raise ValueError("invalid benchmark settings")

    process = psutil.Process()
    before_rss = process.memory_info().rss
    before_gpu = gpu_memory_mib()
    start = time.perf_counter()
    tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(model_dir / "sentencepiece.bpe.model"))
    translator = ctranslate2.Translator(str(model_dir), device=device, compute_type="auto")
    load_ms = round((time.perf_counter() - start) * 1000, 2)
    load_rss = process.memory_info().rss
    load_gpu = gpu_memory_mib()
    simplifier = OpenCC("t2s")
    rows = []
    for source, texts in SAMPLES.items():
        target = "en" if source == "zh" else "zh"
        validate_route(source, target)
        for text in texts:
            tokens = encode_text(tokenizer, text, source)
            assert tokens is not None
            for repeat in range(repetitions):
                started = time.perf_counter()
                result = translator.translate_batch(
                    [tokens], target_prefix=[[f"__{target}__"]], beam_size=beam_size,
                    repetition_penalty=repetition_penalty,
                    no_repeat_ngram_size=no_repeat_ngram_size,
                    max_decoding_length=256,
                )[0]
                elapsed_ms = round((time.perf_counter() - started) * 1000, 2)
                output = result.hypotheses[0]
                if output and output[0] == f"__{target}__":
                    output = output[1:]
                translated = tokenizer.decode(
                    [token for token in output if token not in ("</s>", "<s>", "<pad>")]
                ).strip()
                if target == "zh":
                    translated = simplifier.convert(translated)
                rows.append({"source": source, "target": target, "input": text,
                             "output": translated, "repeat": repeat, "elapsed_ms": elapsed_ms})

    warm = {route: [row["elapsed_ms"] for row in rows
                    if row["repeat"] > 0 and row["source"] == route]
            for route in ("zh", "en")}
    report = {
        "device": device,
        "python": os.sys.version.split()[0],
        "ctranslate2": ctranslate2.__version__,
        "sentencepiece": sentencepiece.__version__,
        "beam_size": beam_size,
        "repetition_penalty": repetition_penalty,
        "no_repeat_ngram_size": no_repeat_ngram_size,
        "load_ms": load_ms,
        "rss_before_mib": round(before_rss / 2**20, 1),
        "rss_after_load_mib": round(load_rss / 2**20, 1),
        "rss_after_run_mib": round(process.memory_info().rss / 2**20, 1),
        "gpu_before_mib": before_gpu,
        "gpu_after_load_mib": load_gpu,
        "gpu_after_run_mib": gpu_memory_mib(),
        "warm_latency_ms": {
            source: {"p50": percentile(values, 50), "p95": percentile(values, 95)}
            if values else None for source, values in warm.items()
        },
        "translations": [row for row in rows if row["repeat"] == 0],
    }
    return report, translator


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("prepare", "benchmark"))
    parser.add_argument("--archive", type=Path)
    parser.add_argument("--model-dir", type=Path,
                        default=ROOT / "artifacts/models/m2m100_418m_ct2")
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cpu")
    parser.add_argument("--repetitions", type=int, default=3)
    parser.add_argument("--beam-size", type=int, default=2)
    parser.add_argument("--repetition-penalty", type=float, default=1.0)
    parser.add_argument("--no-repeat-ngram-size", type=int, default=0)
    parser.add_argument("--hold-seconds", type=int, default=0,
                        help="keep the loaded model alive for a concurrent Whisper check")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.action == "prepare":
        if args.archive is None:
            parser.error("--archive is required for prepare")
        prepare(args.archive, args.model_dir)
    else:
        report, loaded_model = benchmark(
            args.model_dir, args.device, args.repetitions,
            args.beam_size, args.repetition_penalty, args.no_repeat_ngram_size)
        output = json.dumps(report, ensure_ascii=False, indent=2)
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(output + "\n", encoding="utf-8")
        print(output, flush=True)
        if args.hold_seconds > 0:
            time.sleep(args.hold_seconds)
        del loaded_model


if __name__ == "__main__":
    main()
