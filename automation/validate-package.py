#!/usr/bin/env python3
"""
Sinkii09 Engine Package Validation Script

This script validates that the engine package is properly structured
and ready for distribution via Unity Package Manager or UnityPackage.
"""

import os
import json
import sys
from pathlib import Path

class Colors:
    GREEN = '\033[92m'
    RED = '\033[91m'
    YELLOW = '\033[93m'
    BLUE = '\033[94m'
    END = '\033[0m'
    BOLD = '\033[1m'

def print_success(message):
    print(f"{Colors.GREEN}✓ {message}{Colors.END}")

def print_error(message):
    print(f"{Colors.RED}✗ {message}{Colors.END}")

def print_warning(message):
    print(f"{Colors.YELLOW}⚠ {message}{Colors.END}")

def print_info(message):
    print(f"{Colors.BLUE}ℹ {message}{Colors.END}")

def print_header(message):
    print(f"\n{Colors.BOLD}{Colors.BLUE}=== {message} ==={Colors.END}")

class PackageValidator:
    def __init__(self, engine_path="Assets/Engine"):
        self.engine_path = Path(engine_path)
        self.issues = []
        self.warnings = []
        
    def validate(self):
        print_header("Validating Sinkii09 Engine Package")
        
        if not self.engine_path.exists():
            print_error(f"Engine path '{self.engine_path}' does not exist")
            return False
            
        self.check_required_files()
        self.check_package_json()
        self.check_assembly_definitions()
        self.check_folder_structure()
        self.check_meta_files()
        self.check_documentation()
        
        self.print_summary()
        return len(self.issues) == 0
    
    def check_required_files(self):
        print_header("Checking Required Files")
        
        required_files = [
            "package.json",
            "README.md", 
            "CHANGELOG.md",
            "INSTALLATION.md"
        ]
        
        for file in required_files:
            file_path = self.engine_path / file
            if file_path.exists():
                print_success(f"{file} found")
            else:
                self.issues.append(f"Missing required file: {file}")
                print_error(f"{file} missing")
    
    def check_package_json(self):
        print_header("Validating package.json")
        
        package_json_path = self.engine_path / "package.json"
        if not package_json_path.exists():
            self.issues.append("package.json is missing")
            return
            
        try:
            with open(package_json_path, 'r') as f:
                package_data = json.load(f)
                
            required_fields = ["name", "version", "displayName", "description", "unity"]
            for field in required_fields:
                if field in package_data:
                    print_success(f"package.json has {field}: {package_data[field]}")
                else:
                    self.issues.append(f"package.json missing required field: {field}")
                    print_error(f"Missing field: {field}")
            
            # Check dependencies
            if "dependencies" in package_data:
                print_success(f"Found {len(package_data['dependencies'])} dependencies")
                for dep, version in package_data["dependencies"].items():
                    print_info(f"  {dep}: {version}")
            else:
                self.warnings.append("No dependencies specified")
                print_warning("No dependencies found")
                
        except json.JSONDecodeError as e:
            self.issues.append(f"Invalid JSON in package.json: {e}")
            print_error(f"Invalid JSON: {e}")
    
    def check_assembly_definitions(self):
        print_header("Checking Assembly Definitions")
        
        expected_asmdefs = [
            "Runtime/Scripts/Sinkii09.Engine.asmdef",
            "Editor/Sinkii09.Engine.Editor.asmdef", 
            "Tests/Sinkii09.Engine.Test.asmdef"
        ]
        
        for asmdef in expected_asmdefs:
            asmdef_path = self.engine_path / asmdef
            if asmdef_path.exists():
                print_success(f"Assembly definition found: {asmdef}")
                
                # Validate asmdef content
                try:
                    with open(asmdef_path, 'r') as f:
                        asmdef_data = json.load(f)
                    
                    if "name" in asmdef_data:
                        print_info(f"  Name: {asmdef_data['name']}")
                    else:
                        self.warnings.append(f"Assembly definition {asmdef} missing name field")
                        
                except json.JSONDecodeError:
                    self.issues.append(f"Invalid JSON in assembly definition: {asmdef}")
                    print_error(f"Invalid JSON in {asmdef}")
            else:
                self.issues.append(f"Missing assembly definition: {asmdef}")
                print_error(f"Missing: {asmdef}")
    
    def check_folder_structure(self):
        print_header("Checking Folder Structure")
        
        expected_folders = [
            "Runtime",
            "Runtime/Scripts",
            "Runtime/Scripts/Core",
            "Runtime/Resources",
            "Editor",
            "Tests"
        ]
        
        for folder in expected_folders:
            folder_path = self.engine_path / folder
            if folder_path.exists() and folder_path.is_dir():
                print_success(f"Folder exists: {folder}")
            else:
                self.issues.append(f"Missing folder: {folder}")
                print_error(f"Missing folder: {folder}")
    
    def check_meta_files(self):
        print_header("Checking .meta Files")
        
        critical_paths = [
            self.engine_path,
            self.engine_path / "Runtime",
            self.engine_path / "Runtime" / "Scripts", 
            self.engine_path / "Editor",
            self.engine_path / "Tests"
        ]
        
        for path in critical_paths:
            if path.exists():
                meta_file = Path(str(path) + ".meta")
                if meta_file.exists():
                    print_success(f".meta file exists for {path.name}")
                else:
                    self.warnings.append(f"Missing .meta file for {path}")
                    print_warning(f"Missing .meta file for {path.name}")
    
    def check_documentation(self):
        print_header("Checking Documentation")
        
        readme_path = self.engine_path / "README.md"
        if readme_path.exists():
            with open(readme_path, 'r', encoding='utf-8') as f:
                readme_content = f.read()
                
            if len(readme_content) > 1000:
                print_success("README.md has substantial content")
            else:
                self.warnings.append("README.md seems too short")
                print_warning("README.md might need more content")
                
            # Check for key sections
            key_sections = ["Installation", "Features", "Usage", "Requirements"]
            for section in key_sections:
                if section.lower() in readme_content.lower():
                    print_success(f"README contains {section} section")
                else:
                    self.warnings.append(f"README missing {section} section")
                    print_warning(f"README might be missing {section} section")
    
    def print_summary(self):
        print_header("Validation Summary")
        
        if len(self.issues) == 0:
            print_success(f"✨ Package validation PASSED! ({len(self.warnings)} warnings)")
            print_info("Your package is ready for distribution!")
        else:
            print_error(f"❌ Package validation FAILED with {len(self.issues)} issues")
            print_info("\nIssues to fix:")
            for issue in self.issues:
                print(f"  • {issue}")
        
        if len(self.warnings) > 0:
            print_info(f"\nWarnings ({len(self.warnings)}):")
            for warning in self.warnings:
                print(f"  • {warning}")

def main():
    print(f"{Colors.BOLD}Sinkii09 Engine Package Validator{Colors.END}")
    print("Validating package structure and requirements...\n")
    
    # Allow custom engine path
    engine_path = sys.argv[1] if len(sys.argv) > 1 else "Assets/Engine"
    
    validator = PackageValidator(engine_path)
    success = validator.validate()
    
    print(f"\n{Colors.BOLD}Validation complete!{Colors.END}")
    
    if success:
        print_success("Package is ready for distribution! 🚀")
        sys.exit(0)
    else:
        print_error("Please fix the issues above before packaging. 🔧")
        sys.exit(1)

if __name__ == "__main__":
    main()