"""Hold both bilingual translation models for a concurrent Whisper run."""

from __future__ import annotations

import argparse
import json
import socket
import time

from opus_probe import ARCHIVES, benchmark
from probe import ROOT, sha256_file


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--hold-seconds", type=int, default=90)
    parser.add_argument("--deny-sockets", action="store_true",
                        help="reject Python socket connections during both translations")
    args = parser.parse_args()
    if args.deny_sockets:
        def reject_connection(*_args, **_kwargs):
            raise OSError("network access disabled for the offline probe")

        socket.socket.connect = reject_connection
        socket.socket.connect_ex = reject_connection
        socket.create_connection = reject_connection
    reports = {}
    models = []
    for source in ("zh", "en"):
        filename, expected_hash = ARCHIVES[source]
        archive = ROOT / "artifacts/models" / filename
        if sha256_file(archive) != expected_hash:
            raise ValueError(f"archive SHA-256 mismatch: {filename}")
        report, model = benchmark(
            ROOT / "artifacts/models" / filename.removesuffix(".zip"),
            source, 3, 1, 1.1, 3, True,
        )
        reports[source] = {
            "load_ms": report["load_ms"],
            "rss_after_run_mib": report["rss_after_run_mib"],
            "gpu_after_run_mib": report["gpu_after_run_mib"],
            "warm_latency_ms": report["warm_latency_ms"],
        }
        models.append(model)
    output = ROOT / "artifacts/opus-dual-report.json"
    output.write_text(json.dumps(reports, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Both models loaded; report: {output}", flush=True)
    time.sleep(args.hold_seconds)
    models.clear()


if __name__ == "__main__":
    main()
