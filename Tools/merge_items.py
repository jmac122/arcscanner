#!/usr/bin/env python3
"""
Merge items from RaidTheory/arcraiders-data GitHub repo into our items.json

This script:
1. Downloads all item JSON files from the RaidTheory GitHub repository
2. Builds an ID-to-name lookup for converting recyclesInto references
3. Merges with existing items.json, preserving our custom fields
4. Outputs an updated items.json with 500+ items

Usage:
    python Tools/merge_items.py [--dry-run] [--no-cache]

Options:
    --dry-run   Preview changes without writing to items.json
    --no-cache  Force re-download of all items (ignore cache)
"""

import json
import os
import sys
import time
import argparse
import re
from pathlib import Path
from urllib.request import urlopen, Request
from urllib.error import HTTPError, URLError

# Paths
SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
ITEMS_JSON = PROJECT_ROOT / "ArcRaidersOverlay" / "Data" / "items.json"
CACHE_DIR = SCRIPT_DIR / "cache" / "items"

# GitHub API
GITHUB_API_BASE = "https://api.github.com/repos/RaidTheory/arcraiders-data/contents/items"
GITHUB_RAW_BASE = "https://raw.githubusercontent.com/RaidTheory/arcraiders-data/main/items"

# Rate limiting
RATE_LIMIT_DELAY = 0.1  # 100ms between requests
MAX_RETRIES = 3
RETRY_BACKOFF = 2

# Category mapping from RaidTheory type to our category
CATEGORY_MAP = {
    "Basic Material": "Material",
    "Refined Material": "Component",
    "Consumable": "Consumable",
    "Quick Use": "Consumable",
    "Tool": "Material",
    "Weapon": "Weapon",
    "Attachment": "Attachment",
    "Key": "Quest",
    "Quest Item": "Quest",
    "Armor": "Armor",
    "Armor Plate": "Armor",
    "Helmet Plate": "Armor",
    "Blueprint": "Blueprint",
    "Color": "Cosmetic",
    "Valuable": "Valuable",
    "Ammo": "Ammo",
    "Grenade": "Consumable",
    "Trap": "Consumable",
    "Food": "Consumable",
    "Drink": "Consumable",
}


def get_github_token():
    """Get GitHub token from environment if available"""
    return os.environ.get("GITHUB_TOKEN")


def make_request(url, retries=MAX_RETRIES):
    """Make HTTP request with retry logic"""
    headers = {"User-Agent": "ArcScanner-ItemMerge/1.0"}
    token = get_github_token()
    if token:
        headers["Authorization"] = f"token {token}"

    for attempt in range(retries):
        try:
            req = Request(url, headers=headers)
            with urlopen(req, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except HTTPError as e:
            if e.code == 403:
                print(f"Rate limited. Waiting {RETRY_BACKOFF ** attempt}s...")
                time.sleep(RETRY_BACKOFF ** attempt)
            elif e.code == 404:
                return None
            else:
                raise
        except URLError as e:
            if attempt < retries - 1:
                print(f"Network error, retrying in {RETRY_BACKOFF ** attempt}s...")
                time.sleep(RETRY_BACKOFF ** attempt)
            else:
                raise
    return None


def fetch_raw_file(filename):
    """Fetch a raw file from GitHub"""
    url = f"{GITHUB_RAW_BASE}/{filename}"
    headers = {"User-Agent": "ArcScanner-ItemMerge/1.0"}
    token = get_github_token()
    if token:
        headers["Authorization"] = f"token {token}"

    for attempt in range(MAX_RETRIES):
        try:
            req = Request(url, headers=headers)
            with urlopen(req, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except HTTPError as e:
            if e.code == 403:
                print(f"Rate limited. Waiting {RETRY_BACKOFF ** attempt}s...")
                time.sleep(RETRY_BACKOFF ** attempt)
            elif e.code == 404:
                return None
            else:
                raise
        except URLError as e:
            if attempt < MAX_RETRIES - 1:
                time.sleep(RETRY_BACKOFF ** attempt)
            else:
                raise
    return None


def get_item_list():
    """Get list of all item files from GitHub API"""
    print("Fetching item list from GitHub...")
    items = []
    url = GITHUB_API_BASE

    while url:
        data = make_request(url)
        if data is None:
            break

        for item in data:
            if item["name"].endswith(".json"):
                items.append(item["name"])

        # GitHub API pagination (if > 1000 files)
        url = None  # No pagination header parsing for simplicity

    return items


def download_all_items(use_cache=True):
    """Download all items from GitHub, using cache if available"""
    if use_cache:
        CACHE_DIR.mkdir(parents=True, exist_ok=True)

    item_files = get_item_list()
    print(f"Found {len(item_files)} items")

    items = {}
    for i, filename in enumerate(item_files):
        cache_path = CACHE_DIR / filename if use_cache else None

        # Check cache first
        if cache_path and cache_path.exists():
            with open(cache_path, "r", encoding="utf-8") as f:
                item_data = json.load(f)
        else:
            # Download from GitHub
            item_data = fetch_raw_file(filename)
            if item_data is None:
                print(f"  Failed to download {filename}")
                continue

            # Save to cache
            if cache_path:
                with open(cache_path, "w", encoding="utf-8") as f:
                    json.dump(item_data, f, indent=2)

            time.sleep(RATE_LIMIT_DELAY)

        item_id = item_data.get("id", filename.replace(".json", ""))
        items[item_id] = item_data

        # Progress indicator
        if (i + 1) % 50 == 0 or i == len(item_files) - 1:
            print(f"  Downloaded {i + 1}/{len(item_files)} items")

    return items


def normalize_name(name):
    """Normalize item name for matching"""
    if not name:
        return ""
    return re.sub(r"[^a-z0-9]", "", name.lower())


def id_to_display_name(item_id, id_to_name_map):
    """Convert snake_case ID to display name"""
    if item_id in id_to_name_map:
        return id_to_name_map[item_id]
    # Fallback: convert snake_case to Title Case
    return item_id.replace("_", " ").title()


def parse_found_in(found_in_str):
    """Parse comma-separated foundIn string to list"""
    if not found_in_str:
        return []
    return [loc.strip() for loc in found_in_str.split(",") if loc.strip()]


def auto_recommend(item):
    """Generate recommendation for new items"""
    item_type = item.get("type", "")
    rarity = item.get("rarity", "Common")

    # Quest items - always keep
    if item_type in ["Key", "Quest Item"]:
        return "Keep"

    # Valuables - always sell
    if item_type == "Valuable":
        return "Sell"

    # Rare+ items - generally keep
    if rarity in ["Rare", "Epic", "Legendary"]:
        return "Keep"

    # Consumables that are used - keep
    if item_type in ["Quick Use", "Consumable"]:
        return "Keep"

    # Items with recycle outputs - recycle
    recycles_into = item.get("recyclesInto", {})
    if recycles_into and len(recycles_into) > 0:
        return "Recycle"

    # Weapons/Armor - keep
    if item_type in ["Weapon", "Armor", "Armor Plate", "Helmet Plate", "Attachment"]:
        return "Keep"

    return "Either"


def transform_item(rt_item, id_to_name_map, existing_item=None):
    """Transform RaidTheory item to our schema"""
    # Get English name
    name_obj = rt_item.get("name", {})
    name = name_obj.get("en", "") if isinstance(name_obj, dict) else str(name_obj)

    # Get English description
    desc_obj = rt_item.get("description", {})
    description = desc_obj.get("en", "") if isinstance(desc_obj, dict) else str(desc_obj)

    # Map category
    rt_type = rt_item.get("type", "Material")
    category = CATEGORY_MAP.get(rt_type, "Material")

    # Convert recyclesInto IDs to display names
    recycles_into = rt_item.get("recyclesInto", {})
    recycle_outputs = None
    if recycles_into and len(recycles_into) > 0:
        recycle_outputs = {}
        for item_id, count in recycles_into.items():
            if count > 0:
                display_name = id_to_display_name(item_id, id_to_name_map)
                recycle_outputs[display_name] = count
        if not recycle_outputs:
            recycle_outputs = None

    # Parse foundIn
    found_in = parse_found_in(rt_item.get("foundIn", ""))

    # Build base item
    item = {
        "name": name,
        "category": category,
        "rarity": rt_item.get("rarity", "Common"),
        "value": rt_item.get("value", 0),
        "description": description,
        "weight": rt_item.get("weightKg", 0.1),
        "stackSize": rt_item.get("stackSize", 1),
        "recycleValuePercent": None,  # We don't have this data from RaidTheory
        "recycleOutputs": recycle_outputs,
        "foundIn": found_in if found_in else None,
        "imageUrl": rt_item.get("imageFilename"),
    }

    # Preserve or generate custom fields
    if existing_item:
        item["projectUses"] = existing_item.get("projectUses", [])
        item["workshopUses"] = existing_item.get("workshopUses", [])
        item["keepForQuests"] = existing_item.get("keepForQuests", False)
        item["questUses"] = existing_item.get("questUses", [])
        item["recommendation"] = existing_item.get("recommendation", "Either")
        # Keep existing recycleValuePercent if we had it
        if existing_item.get("recycleValuePercent") is not None:
            item["recycleValuePercent"] = existing_item["recycleValuePercent"]
    else:
        item["projectUses"] = []
        item["workshopUses"] = []
        item["keepForQuests"] = False
        item["questUses"] = []
        item["recommendation"] = auto_recommend(rt_item)

    return item


def levenshtein_distance(s1, s2):
    """Calculate Levenshtein distance between two strings"""
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)
    if len(s2) == 0:
        return len(s1)

    previous_row = range(len(s2) + 1)
    for i, c1 in enumerate(s1):
        current_row = [i + 1]
        for j, c2 in enumerate(s2):
            insertions = previous_row[j + 1] + 1
            deletions = current_row[j] + 1
            substitutions = previous_row[j] + (c1 != c2)
            current_row.append(min(insertions, deletions, substitutions))
        previous_row = current_row

    return previous_row[-1]


def find_existing_item(name, existing_items_by_name):
    """Find existing item by name with fuzzy matching"""
    normalized = normalize_name(name)

    # Exact match first
    if normalized in existing_items_by_name:
        return existing_items_by_name[normalized]

    # Fuzzy match
    best_match = None
    best_distance = float("inf")

    for existing_name, item in existing_items_by_name.items():
        distance = levenshtein_distance(normalized, existing_name)
        # Only consider if distance is less than 20% of the name length
        max_distance = max(3, len(normalized) * 0.2)
        if distance < best_distance and distance <= max_distance:
            best_distance = distance
            best_match = item

    return best_match


def main():
    parser = argparse.ArgumentParser(description="Merge items from RaidTheory into items.json")
    parser.add_argument("--dry-run", action="store_true", help="Preview changes without writing")
    parser.add_argument("--no-cache", action="store_true", help="Force re-download (ignore cache)")
    args = parser.parse_args()

    print("=== ArcScanner Item Merge Tool ===\n")

    # Phase 1: Download all RaidTheory items
    print("Phase 1: Downloading items from RaidTheory/arcraiders-data...")
    rt_items = download_all_items(use_cache=not args.no_cache)
    print(f"  Total: {len(rt_items)} items\n")

    # Phase 2: Build ID-to-name lookup
    print("Phase 2: Building ID-to-name lookup...")
    id_to_name_map = {}
    for item_id, item in rt_items.items():
        name_obj = item.get("name", {})
        name = name_obj.get("en", "") if isinstance(name_obj, dict) else str(name_obj)
        if name:
            id_to_name_map[item_id] = name
    print(f"  Mapped {len(id_to_name_map)} IDs to names\n")

    # Phase 3: Load existing items
    print("Phase 3: Loading existing items.json...")
    existing_items = []
    if ITEMS_JSON.exists():
        with open(ITEMS_JSON, "r", encoding="utf-8") as f:
            existing_items = json.load(f)

    # Build lookup by normalized name
    existing_by_name = {}
    for item in existing_items:
        norm_name = normalize_name(item.get("name", ""))
        if norm_name:
            existing_by_name[norm_name] = item
    print(f"  Loaded {len(existing_items)} existing items\n")

    # Phase 4: Merge items
    print("Phase 4: Merging items...")
    merged_items = []
    stats = {"updated": 0, "added": 0, "skipped": 0}

    for item_id, rt_item in rt_items.items():
        name_obj = rt_item.get("name", {})
        name = name_obj.get("en", "") if isinstance(name_obj, dict) else str(name_obj)

        if not name:
            stats["skipped"] += 1
            continue

        existing = find_existing_item(name, existing_by_name)
        merged = transform_item(rt_item, id_to_name_map, existing)
        merged_items.append(merged)

        if existing:
            stats["updated"] += 1
        else:
            stats["added"] += 1

    # Sort by name for consistency
    merged_items.sort(key=lambda x: x["name"].lower())

    print(f"\n=== Merge Report ===")
    print(f"Source: RaidTheory/arcraiders-data ({len(rt_items)} items)")
    print(f"Target: {ITEMS_JSON.relative_to(PROJECT_ROOT)} ({len(existing_items)} items)")
    print(f"")
    print(f"Updated: {stats['updated']} items (existing with new data)")
    print(f"Added:   {stats['added']} items (new)")
    print(f"Skipped: {stats['skipped']} items (no name)")
    print(f"")
    print(f"Total:   {len(merged_items)} items")

    # Write output
    if args.dry_run:
        print(f"\n[DRY RUN] Would write {len(merged_items)} items to {ITEMS_JSON}")
        print("\nSample of new items:")
        new_items = [m for m in merged_items if not find_existing_item(m["name"], existing_by_name)]
        for item in new_items[:5]:
            print(f"  - {item['name']} ({item['category']}, {item['rarity']})")
    else:
        with open(ITEMS_JSON, "w", encoding="utf-8") as f:
            json.dump(merged_items, f, indent=2, ensure_ascii=False)
        print(f"\nWrote {len(merged_items)} items to {ITEMS_JSON}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
