"""Render the public, static SVG charts from tracked docs/perf CSVs.

Requires matplotlib. Run from any directory; no local benchmark results are needed.
"""
import argparse
import csv
import math
from pathlib import Path
from statistics import mean

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator

ROOT = Path(__file__).resolve().parents[2]
BG, TEXT, MUTED, ACCENT, BORDER = "#0E0F11", "#E8EAED", "#9AA0A6", "#3DDC97", "#26292E"


def rows(directory, name):
    with (directory / name).open(encoding="utf-8", newline="") as source:
        return list(csv.DictReader(source))


def style(ax):
    ax.set_facecolor(BG)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    for side in ("bottom", "left"):
        ax.spines[side].set_color(BORDER)
    ax.tick_params(colors=MUTED, length=0, pad=9)
    ax.set_axisbelow(True)
    ax.grid(axis="y", color=BORDER, linewidth=.8)
    ax.yaxis.set_major_locator(MaxNLocator(nbins=4, min_n_ticks=3))
    ax.yaxis.label.set_color(TEXT)
    ax.xaxis.label.set_color(TEXT)


def machine(run):
    cpu = run["machine"].replace("8-Core Processor", "").strip()
    os = run["os"].replace("Microsoft Windows NT 10.0.", "Windows 11 build ").removesuffix(".0")
    return f'{cpu} · {os} · {run["measured_date_cet"]}'


def save(fig, output, name, preview):
    fig.savefig(output / name, format="svg", facecolor=BG,
                metadata={"Date": None, "Creator": "LightCrosshair performance charts"})
    svg_path = output / name
    svg_path.write_text("\n".join(line.rstrip() for line in svg_path.read_text(encoding="utf-8").splitlines()) + "\n", encoding="utf-8")
    if preview:
        preview.mkdir(parents=True, exist_ok=True)
        fig.savefig(preview / name.replace(".svg", ".png"), dpi=140, facecolor=BG)
    plt.close(fig)


def render(data, output, preview):
    plt.rcParams.update({
        "font.family": "Segoe UI", "font.size": 12, "text.color": TEXT,
        "axes.labelcolor": TEXT, "figure.facecolor": BG, "savefig.facecolor": BG,
        "svg.fonttype": "none", "svg.hashsalt": "lightcrosshair-release-charts",
    })
    runs = {row["run"]: row for row in rows(data, "runs.csv")}
    samples = rows(data, "idle-samples.csv")
    idle_runs = [runs["idle-v19"], runs["idle-v20"]]
    memory, cpu = [], []
    for run in idle_runs:
        group = [row for row in samples if row["version"] == run["version"]]
        expected = int(run["idle_seconds"]) // int(run["sample_interval_seconds"])
        assert len(group) == expected and group
        memory.append(mean(int(row["private_bytes"]) for row in group) / 1_000_000)
        cpu.append(mean(float(row["cpu_one_core_percent"]) for row in group))
    assert all(math.isfinite(value) and value >= 0 for value in memory + cpu)

    fig, axes = plt.subplots(1, 2, figsize=(12, 6))
    fig.subplots_adjust(left=.08, right=.97, bottom=.30, top=.79, wspace=.35)
    fig.text(.08, .93, "Idle resident process · LightCrosshair", fontsize=23, weight=600)
    for ax, values, title, unit, digits in zip(axes, (memory, cpu),
            ("Private memory", "CPU"), ("MB", "% of one logical core"), (2, 4)):
        style(ax)
        ax.bar([0, 1], values, color=[MUTED, ACCENT], width=.55, zorder=3)
        ax.set_title(title, loc="left", color=TEXT, fontsize=15, pad=17)
        ax.set_ylabel(unit, labelpad=10)
        ax.set_xticks([0, 1], [run["version"] for run in idle_runs], color=TEXT)
        ax.set_ylim(0, max(values) * 1.30)
        for index, value in enumerate(values):
            label = f"{value:.{digits}f}" + ("*" if value == 0 else "")
            ax.annotate(label, (index, value), xytext=(0, 9), textcoords="offset points",
                        ha="center", color=[MUTED, ACCENT][index], fontsize=16, weight=600)
            if value == 0:
                ax.plot(index, 0, marker="_", markersize=24, color=ACCENT, clip_on=False, zorder=4)
    old, new = idle_runs
    fig.text(.08, .18, machine(new), color=MUTED, fontsize=10.5)
    fig.text(.08, .135, f'{new["build"]} · {old["version"]}: {old["framework"]} ({old["source_commit"]}) · '
             f'{new["version"]}: {new["framework"]} ({new["source_commit"]})', color=MUTED, fontsize=10.5)
    fig.text(.08, .09, f'{new["warmup_seconds"]} s warmup · {new["idle_seconds"]} × '
             f'{new["sample_interval_seconds"]} s samples · static crosshair · FPS off · settings closed', color=MUTED, fontsize=10.5)
    fig.text(.08, .045, "MB = 1,000,000 bytes. CPU is not divided by processor count. *Zero is below sampling resolution.", color=MUTED, fontsize=10)
    save(fig, output, "idle-v19-vs-v20.svg", preview)

    soak = rows(data, "library-soak.csv")
    sessions = [int(row["session"]) for row in soak]
    assert sessions == list(range(max(sessions) + 1))
    before = [float(row["before_private_mb"]) for row in soak]
    after = [float(row["after_private_mb"]) for row in soak]
    for row in soak:
        for phase in ("before", "after"):
            assert abs(float(row[f"{phase}_private_mib"]) * 1_048_576 / 1_000_000
                       - float(row[f"{phase}_private_mb"])) < .000001
    thumbnails = {int(row["thumbnails"]) for row in soak if int(row["session"]) > 0}
    assert len(thumbnails) == 1
    budget = next(row for row in rows(data, "budgets.csv") if row["metric"] == "ui_private_growth")
    assert budget["unit"] == "MB"
    allowance, target_session = float(budget["value"]), int(budget["applies_at_session"])
    old, new = runs["library-before"], runs["library-after"]
    fig, ax = plt.subplots(figsize=(12, 6.6))
    fig.subplots_adjust(left=.08, right=.97, bottom=.31, top=.81)
    fig.text(.08, .94, f"Library soak · {max(sessions)} UI sessions", fontsize=23, weight=600)
    style(ax)
    ax.axhspan(after[0], after[0] + allowance, facecolor="#1D2024", zorder=0)
    ax.axhline(after[0] + allowance, color=MUTED, linestyle=(0, (3, 4)), linewidth=1)
    ax.text(max(sessions) * .52, after[0] + allowance / 2,
            f"Initial +{allowance:g} MB band · target at session {target_session}",
            color=MUTED, fontsize=10, va="center")
    ax.plot(sessions, before, color=MUTED, linewidth=2, label="Before fix")
    ax.plot(sessions, after, color=ACCENT, linewidth=2, label="After fix")
    ax.set(xlabel="Completed UI sessions", ylabel="Core private memory (MB)", xlim=(0, max(sessions)))
    ax.xaxis.set_major_locator(MaxNLocator(nbins=10, integer=True))
    ax.set_ylim(min(before + after) * .90, max(before + after) * 1.12)
    ax.legend(frameon=False, loc="upper left", labelcolor=TEXT)
    fig.text(.08, .205, machine(runs["idle-v20"]), color=MUTED, fontsize=10.5)
    fig.text(.08, .16, f'{new["build"]} · {new["framework"]} · before {old["source_commit"]} / after {new["source_commit"]}', color=MUTED, fontsize=10.5)
    fig.text(.08, .115, f'Real Library page · {next(iter(thumbnails))} thumbnails/session · '
             f'{new["paint_settle_ms"]} ms paint + {new["exit_settle_ms"]} ms post-exit settle · no forced GC in curve', color=MUTED, fontsize=10)
    fig.text(.08, .07, f'MB = 1,000,000 bytes. Band is the session-{target_session} target, not a requirement for every later session.', color=MUTED, fontsize=10)
    save(fig, output, "library-soak.svg", preview)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data", type=Path, default=ROOT / "docs/perf")
    parser.add_argument("--output", type=Path, default=ROOT / "docs/perf")
    parser.add_argument("--preview-dir", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    render(args.data, args.output, args.preview_dir)
