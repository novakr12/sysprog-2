#!/usr/bin/env python3

import sys
import time
import argparse
from concurrent.futures import ThreadPoolExecutor
from urllib.request import urlopen
from urllib.error import HTTPError, URLError

import pandas as pd
import matplotlib.pyplot as plt

# === ARGUMENTI ===
parser = argparse.ArgumentParser(description="Load test + grafik latencije za sysprog-2 server.")
parser.add_argument("--url", default="http://localhost:5000/launches", help="ciljni URL")
parser.add_argument("-n", "--requests", type=int, default=200, help="ukupan broj zahteva")
parser.add_argument("-c", "--concurrency", type=int, default=50, help="broj paralelnih zahteva")
args = parser.parse_args()

# === JEDAN ZAHTEV ===
def do_request(_):
    start = time.time()
    try:
        with urlopen(args.url, timeout=60) as r:
            status = r.status
            r.read()
    except HTTPError as e:
        status = e.code
    except URLError:
        status = 0
    elapsed = time.time() - start
    return {"status": status, "timestamp": start, "elapsed_seconds": elapsed}

# === POKRENI OPTERECENJE ===
print(f"Saljem {args.requests} zahteva ka {args.url} (concurrency = {args.concurrency})...")
with ThreadPoolExecutor(max_workers=args.concurrency) as pool:
    rows = list(pool.map(do_request, range(args.requests)))

df = pd.DataFrame(rows).sort_values("timestamp")

# Relativno vreme od prvog zahteva (citljivija x-osa)
df["t_rel"] = df["timestamp"] - df["timestamp"].min()

ok = df[df["status"] == 200]
err = df[df["status"] != 200]

# === STATISTIKA U KONZOLU ===
print(f"\nZahteva:        {len(df)}")
print(f"200 OK:         {len(ok)}")
print(f"Greske/503/500: {len(err)}")
print(f"Latencija min:  {df['elapsed_seconds'].min():.4f} s")
print(f"Latencija max:  {df['elapsed_seconds'].max():.4f} s")
print(f"Latencija avg:  {df['elapsed_seconds'].mean():.4f} s")
print(f"Latencija p95:  {df['elapsed_seconds'].quantile(0.95):.4f} s")

# === GRAFIK: latencija kroz vreme ===
plt.figure(figsize=(12, 6))
plt.plot(df["t_rel"], df["elapsed_seconds"], linestyle='-', color='lightgray', alpha=0.6, zorder=1)
plt.scatter(ok["t_rel"], ok["elapsed_seconds"], s=25, color='tab:green', label='200 OK', zorder=2)
if not err.empty:
    plt.scatter(err["t_rel"], err["elapsed_seconds"], s=30, color='tab:red',
                label='greska / 503 / 500', zorder=3)

plt.title("Vreme odgovora kroz vreme")
plt.xlabel("Vreme od prvog zahteva (s)")
plt.ylabel("Latencija (s)")
plt.grid(True, alpha=0.3)
plt.legend()
plt.tight_layout()
plt.savefig("latency.png", dpi=120)
print("\nGrafik sacuvan u latency.png")
plt.show()
