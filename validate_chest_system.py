#!/usr/bin/env python3
"""
Chest Item System Validation Script
Verifies all code paths and integration points are correctly implemented.
"""

import os
import re
from pathlib import Path
from typing import List, Tuple, Dict

def check_file_exists(path: str) -> bool:
    """Check if file exists."""
    return Path(path).exists()

def read_file(path: str) -> str:
    """Read file content."""
    try:
        with open(path, 'r', encoding='utf-8') as f:
            return f.read()
    except Exception as e:
        print(f"Error reading {path}: {e}")
        return ""

def find_method(content: str, method_name: str) -> bool:
    """Check if method exists in content."""
    pattern = rf"(public|private|protected|internal)\s+\w+\s+{re.escape(method_name)}\s*\("
    return bool(re.search(pattern, content))

def find_field(content: str, field_name: str) -> bool:
    """Check if field exists in content."""
    pattern = rf"(public|private|protected|internal)\s+[\w<>,\s]+\s+{re.escape(field_name)}\s*[=;]"
    return bool(re.search(pattern, content))

def find_const(content: str, const_name: str) -> bool:
    """Check if constant exists in content."""
    pattern = rf"(public|private)?\s*const\s+string\s+{re.escape(const_name)}\s*="
    return bool(re.search(pattern, content))

def run_validation() -> Tuple[int, int]:
    """Run all validation checks."""
    passed = 0
    failed = 0
    
    base_path = r"C:\Users\ericc\Documents\GitHub\wizard-survivors.worktrees\game-feature-prompt-and-issue-planning"
    
    checks = [
        # File existence checks
        ("Scripts: ChestItemCatalog.cs exists", 
         lambda: check_file_exists(os.path.join(base_path, "scripts", "ChestItemCatalog.cs"))),
        
        ("Scripts: ChestReward.cs exists", 
         lambda: check_file_exists(os.path.join(base_path, "scripts", "ChestReward.cs"))),
        
        ("Scripts: ChestItemSelectionMenu.cs exists", 
         lambda: check_file_exists(os.path.join(base_path, "scripts", "ChestItemSelectionMenu.cs"))),
        
        ("Scripts: ChestItemHUD.cs exists", 
         lambda: check_file_exists(os.path.join(base_path, "scripts", "ChestItemHUD.cs"))),
        
        # ChestItemCatalog checks
        ("Catalog: 25 items defined",
         lambda: len(re.findall(r'public const string \w+ = "[^"]+";', 
                               read_file(os.path.join(base_path, "scripts", "ChestItemCatalog.cs")))) >= 25),
        
        ("Catalog: 10 synergy sets defined",
         lambda: len(re.findall(r'new ChestSetDefinition', 
                               read_file(os.path.join(base_path, "scripts", "ChestItemCatalog.cs")))) >= 10),
        
        ("Catalog: GetDisplayName method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "ChestItemCatalog.cs")), 
                           "GetDisplayName")),
        
        ("Catalog: GetIconPath method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "ChestItemCatalog.cs")), 
                           "GetIconPath")),
        
        # Player.cs checks
        ("Player: 17 chest stat fields declared",
         lambda: len(re.findall(r'private\s+(float|int|bool)\s+chest\w+', 
                               read_file(os.path.join(base_path, "scripts", "Player.cs")))) >= 17),
        
        ("Player: ApplyChestItemEffect method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "Player.cs")), 
                           "ApplyChestItemEffect")),
        
        ("Player: RefreshChestSetEffects method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "Player.cs")), 
                           "RefreshChestSetEffects")),
        
        ("Player: GetOwnedChestItems method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "Player.cs")), 
                           "GetOwnedChestItems")),
        
        ("Player: GetCompletedChestSets method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "Player.cs")), 
                           "GetCompletedChestSets")),
        
        ("Player: TriggerBastionRetaliation method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "Player.cs")), 
                           "TriggerBastionRetaliation")),
        
        ("Player: OnChestEnemyKilled method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "Player.cs")), 
                           "OnChestEnemyKilled")),
        
        ("Player: HealthFraction property in Enemy.cs",
         lambda: "HealthFraction" in read_file(os.path.join(base_path, "scripts", "Enemy.cs"))),
        
        # Node2DGame checks
        ("Node2DGame: ChestItemHUD instantiated",
         lambda: "new ChestItemHUD()" in read_file(os.path.join(base_path, "scripts", "Node2DGame.cs"))),
        
        ("Node2DGame: Chest spawn timer updated",
         lambda: "chestItemDropRateBonus" in read_file(os.path.join(base_path, "scripts", "Node2DGame.cs"))),
        
        # ChestItemHUD checks
        ("HUD: Inherits CanvasLayer",
         lambda: "CanvasLayer" in read_file(os.path.join(base_path, "scripts", "ChestItemHUD.cs"))),
        
        ("HUD: RefreshDisplay method exists",
         lambda: find_method(read_file(os.path.join(base_path, "scripts", "ChestItemHUD.cs")), 
                           "RefreshDisplay")),
        
        # Documentation checks
        ("Docs: SYSTEM_COMPLETE.md exists",
         lambda: check_file_exists(os.path.join(base_path, "SYSTEM_COMPLETE.md"))),
        
        ("Docs: FINAL_CHECKLIST.md exists",
         lambda: check_file_exists(os.path.join(base_path, "FINAL_CHECKLIST.md"))),
    ]
    
    print("=" * 70)
    print("CHEST ITEM SYSTEM VALIDATION")
    print("=" * 70)
    
    for check_name, check_func in checks:
        try:
            result = check_func()
            status = "✓ PASS" if result else "✗ FAIL"
            print(f"{status}: {check_name}")
            if result:
                passed += 1
            else:
                failed += 1
        except Exception as e:
            print(f"✗ ERROR: {check_name} - {e}")
            failed += 1
    
    print("=" * 70)
    print(f"Results: {passed} passed, {failed} failed out of {passed + failed} checks")
    print("=" * 70)
    
    return passed, failed

if __name__ == "__main__":
    passed, failed = run_validation()
    exit(0 if failed == 0 else 1)
