"""Compare pinned OPUS-MT Chinese/English models with the M2M100 candidate."""

from __future__ import annotations

import argparse
import json
import os
import time
from pathlib import Path

from probe import ROOT, SAMPLES, gpu_memory_mib, percentile, sha256_file


ARCHIVES = {
    "en": ("opus-mt-en-zh.zip", "1748dfa40aadf2aea67eff4d6845d8aabbd3093604fc22125b7232f17f404947"),
    "zh": ("opus-mt-zh-en.zip", "5aa44cb302ea85132b041b5c5dd7e76dbad92aa4cc4537674ee4389a0dbd3cef"),
}
EXPECTED_MODELS = {
    "en": {"repo": "Helsinki-NLP/opus-mt-en-zh",
           "revision": "408d9bc410a388e1d9aef112a2daba955b945255",
           "licence": "CC-BY-4.0", "language_tag": ">>cmn_Hans<<"},
    "zh": {"repo": "Helsinki-NLP/opus-mt-zh-en",
           "revision": "cf109095479db38d6df799875e34039d4938aaa6",
           "licence": "CC-BY-4.0", "language_tag": ""},
}


def source_tokens(tokenizer, text: str, language_tag: str,
                  append_eos: bool = True) -> list[str] | None:
    if not text.strip():
        return None
    tokens = tokenizer.encode(text.strip(), out_type=str)
    if len(tokens) > 256:
        raise ValueError("translation input is too long (over 256 tokens)")
    if language_tag:
        tokens.insert(0, language_tag)
    if append_eos:
        tokens.append("</s>")
    return tokens


def benchmark(model_dir: Path, source: str, repetitions: int, beam_size: int,
              repetition_penalty: float, no_repeat_ngram_size: int,
              append_eos: bool) -> tuple[dict, object]:
    import ctranslate2
    import psutil
    import sentencepiece
    from opencc import OpenCC

    if source not in ARCHIVES or repetitions < 2:
        raise ValueError("source must be en or zh, and repetitions must be at least two")
    model_info = json.loads((model_dir / "opus_mt.json").read_text(encoding="utf-8"))
    if any(model_info.get(key) != value for key, value in EXPECTED_MODELS[source].items()):
        raise ValueError("model metadata does not match the pinned direction and revision")
    tokenizer_source = sentencepiece.SentencePieceProcessor(model_file=str(model_dir / "source.spm"))
    tokenizer_target = sentencepiece.SentencePieceProcessor(model_file=str(model_dir / "target.spm"))
    process = psutil.Process()
    rss_before = process.memory_info().rss
    gpu_before = gpu_memory_mib()
    started = time.perf_counter()
    translator = ctranslate2.Translator(str(model_dir), device="cpu", compute_type="auto")
    load_ms = round((time.perf_counter() - started) * 1000, 2)
    rss_after_load = process.memory_info().rss
    simplifier = OpenCC("t2s")
    target = "zh" if source == "en" else "en"
    rows = []
    for text in SAMPLES[source]:
        tokens = source_tokens(tokenizer_source, text, model_info["language_tag"], append_eos)
        assert tokens is not None
        for repeat in range(repetitions):
            started = time.perf_counter()
            result = translator.translate_batch(
                [tokens], beam_size=beam_size, repetition_penalty=repetition_penalty,
                no_repeat_ngram_size=no_repeat_ngram_size, max_decoding_length=128,
            )[0]
            elapsed_ms = round((time.perf_counter() - started) * 1000, 2)
            output = tokenizer_target.decode(
                [token for token in result.hypotheses[0] if token not in ("</s>", "<s>", "<pad>")]
            ).strip()
            if target == "zh":
                output = simplifier.convert(output)
            rows.append({"input": text, "output": output, "repeat": repeat,
                         "elapsed_ms": elapsed_ms})

    warm = [row["elapsed_ms"] for row in rows if row["repeat"] > 0]
    report = {
        "source": source,
        "target": target,
        "model": model_info,
        "python": os.sys.version.split()[0],
        "ctranslate2": ctranslate2.__version__,
        "beam_size": beam_size,
        "repetition_penalty": repetition_penalty,
        "no_repeat_ngram_size": no_repeat_ngram_size,
        "append_eos": append_eos,
        "load_ms": load_ms,
        "rss_before_mib": round(rss_before / 2**20, 1),
        "rss_after_load_mib": round(rss_after_load / 2**20, 1),
        "rss_after_run_mib": round(process.memory_info().rss / 2**20, 1),
        "gpu_before_mib": gpu_before,
        "gpu_after_run_mib": gpu_memory_mib(),
        "warm_latency_ms": {"p50": percentile(warm, 50), "p95": percentile(warm, 95)},
        "translations": [row for row in rows if row["repeat"] == 0],
    }
    return report, translator


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", choices=("en", "zh"), required=True)
    parser.add_argument("--repetitions", type=int, default=3)
    parser.add_argument("--beam-size", type=int, default=1)
    parser.add_argument("--repetition-penalty", type=float, default=1.1)
    parser.add_argument("--no-repeat-ngram-size", type=int, default=3)
    parser.add_argument("--no-source-eos", action="store_true",
                        help="diagnostic only: reproduces incomplete repeated outputs")
    parser.add_argument("--hold-seconds", type=int, default=0)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    filename, expected_hash = ARCHIVES[args.source]
    archive = ROOT / "artifacts/models" / filename
    if sha256_file(archive) != expected_hash:
        raise ValueError(f"archive SHA-256 mismatch: {filename}")
    model_dir = ROOT / "artifacts/models" / filename.removesuffix(".zip")
    report, loaded_model = benchmark(
        model_dir, args.source, args.repetitions, args.beam_size,
        args.repetition_penalty, args.no_repeat_ngram_size, not args.no_source_eos)
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
