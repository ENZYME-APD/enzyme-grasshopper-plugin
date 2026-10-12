import subprocess
import sys

# Key milestone versions to KEEP on the Yak Package Manager
KEEP_VERSIONS = {
    "1.0.2",
    "1.1.0",
    "1.2.0",
    "1.3.0",
    "1.4.0",
    "1.5.0",
    "1.6.0",
    "1.7.0",
    "1.8.0",
    "1.9.0",
    "1.10.0",
    "1.10.76"  # Current latest release
}

def get_all_tags():
    result = subprocess.run(["git", "tag", "--sort=v:refname"], capture_output=True, text=True, check=True)
    tags = [line.strip().lstrip("v") for line in result.stdout.strip().split("\n") if line.strip()]
    return tags

def main():
    dry_run = "--dry-run" in sys.argv
    tags = get_all_tags()
    to_yank = [v for v in tags if v not in KEEP_VERSIONS]

    print(f"Total versions identified: {len(tags)}")
    print(f"Versions to KEEP ({len(KEEP_VERSIONS)}): {sorted(list(KEEP_VERSIONS))}")
    print(f"Versions to YANK ({len(to_yank)}):")

    for v in to_yank:
        if dry_run:
            print(f"[DRY RUN] Would yank: Enzyme-Grasshopper-Plugin {v}")
        else:
            print(f"Yanking Enzyme-Grasshopper-Plugin {v}...")
            res = subprocess.run(["./yak", "yank", "Enzyme-Grasshopper-Plugin", v], capture_output=True, text=True)
            if res.returncode == 0:
                print(f"  ✓ Success: {v}")
            else:
                print(f"  Notice for {v}: {res.stdout.strip() or res.stderr.strip()}")

if __name__ == "__main__":
    main()
