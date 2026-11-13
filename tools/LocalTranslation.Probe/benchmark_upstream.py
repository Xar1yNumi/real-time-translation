"""Measure fixed-sentence OPUS-MT latency with the packaged worker protocol."""

from __future__ import annotations

import argparse
import json
import math
import sys
import time
from pathlib import Path

from probe import SAMPLES

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "LocalTranslation.Worker"))
from worker import OpusEngine  # noqa: E402


def percentile(values: list[float], level: float) -> float:
    ordered = sorted(values)
    return round(ordered[math.ceil(len(ordered) * level) - 1], 2)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--models", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--reference", type=Path)
    parser.add_argument("--max-p95-ms", type=float)
    parser.add_argument("--compute-type", choices=("auto", "float32", "int8", "int8_float32"), default="auto")
    parser.add_argument("--intra-threads", type=int)
    parser.add_argument("--beam-en", type=int, default=2)
    parser.add_argument("--beam-zh", type=int, default=2)
    args = parser.parse_args()
    engine = OpusEngine(args.models)
    if args.compute_type != "auto" or args.intra_threads:
        import ctranslate2

        for source, (translator, encoder, decoder, tag) in engine.models.items():
            del translator
            model_dir = args.models / ("opus-mt-zh-en" if source == "zh" else "opus-mt-en-zh")
            engine.models[source] = (
                ctranslate2.Translator(
                    str(model_dir), device="cpu", compute_type=args.compute_type,
                    inter_threads=1, intra_threads=args.intra_threads or 0,
                ),
                encoder, decoder, tag,
            )
    def translate(text: str, source: str) -> str:
        beam_size = args.beam_en if source == "en" else args.beam_zh
        if beam_size == 2:
            return engine.translate(text, source)
        translator, encoder, decoder, tag = engine.models[source]
        tokens = encoder.encode(text.strip(), out_type=str)
        if tag:
            tokens.insert(0, tag)
        tokens.append("</s>")
        result = translator.translate_batch(
            [tokens], beam_size=beam_size, repetition_penalty=1.1,
            no_repeat_ngram_size=3, max_decoding_length=128,
        )[0]
        output = decoder.decode(
            [token for token in result.hypotheses[0] if token not in ("</s>", "<s>", "<pad>")]
        ).strip()
        return engine.simplifier.convert(output)
    report = {}
    for source in ("en", "zh"):
        samples = SAMPLES[source]
        outputs = []
        warm_ms = []
        for repeat in range(4):
            for text in samples:
                started = time.perf_counter()
                output = translate(text, source)
                elapsed_ms = (time.perf_counter() - started) * 1000
                if repeat == 0:
                    outputs.append({"input": text, "output": output})
                else:
                    warm_ms.append(elapsed_ms)
        report[source] = {
            "p50_ms": percentile(warm_ms, 0.5),
            "p95_ms": percentile(warm_ms, 0.95),
            "translations": outputs,
        }
    if args.reference:
        baseline = json.loads(args.reference.read_text(encoding="utf-8"))
        for source in ("en", "zh"):
            if report[source]["translations"] != baseline[source]["translations"]:
                raise RuntimeError(f"translation output changed for {source}")
    if args.max_p95_ms:
        for source in ("en", "zh"):
            if report[source]["p95_ms"] > args.max_p95_ms:
                raise RuntimeError(f"warm translation P95 exceeded limit for {source}")
    result = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result, encoding="utf-8")
    print(result)


if __name__ == "__main__":
    main()
