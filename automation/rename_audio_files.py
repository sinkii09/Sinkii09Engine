#!/usr/bin/env python3
"""
Audio File Renamer Script
Renames audio files from format: {album_name}{order}{title}.ext
To format: {order}.ext
"""

import os
import re
import argparse
from pathlib import Path

def extract_order_number(filename):
    """
    Extract order number from filename.
    Assumes order number is a sequence of digits in the filename.
    """
    # Remove file extension
    name_without_ext = os.path.splitext(filename)[0]
    
    # Find all digit sequences in the filename
    numbers = re.findall(r'\d+', name_without_ext)
    
    if numbers:
        # Return the first number found (assuming it's the order)
        return numbers[0]
    return None

def rename_files(directory, dry_run=True, pattern=None):
    """
    Rename audio files in directory to keep only order number.
    
    Args:
        directory: Path to directory containing audio files
        dry_run: If True, only show what would be renamed without actually renaming
        pattern: Optional regex pattern to extract order number
    """
    audio_extensions = {'.mp3', '.wav', '.ogg', '.m4a', '.flac', '.aac', '.wma', '.aiff', '.aif'}
    
    directory_path = Path(directory)
    if not directory_path.exists():
        print(f"Error: Directory '{directory}' does not exist")
        return
    
    files_to_rename = []
    
    # Collect all audio files
    for file_path in directory_path.iterdir():
        if file_path.is_file() and file_path.suffix.lower() in audio_extensions:
            old_name = file_path.name
            
            if pattern:
                # Use custom pattern if provided
                match = re.search(pattern, old_name)
                if match:
                    order = match.group(1) if match.groups() else match.group(0)
                else:
                    print(f"Warning: Pattern didn't match for '{old_name}', skipping...")
                    continue
            else:
                # Use default extraction
                order = extract_order_number(old_name)
                if not order:
                    print(f"Warning: Could not extract order number from '{old_name}', skipping...")
                    continue
            
            # Create new filename with just the order number
            new_name = f"{order}{file_path.suffix}"
            new_path = directory_path / new_name
            
            # Check for conflicts
            if new_path.exists() and new_path != file_path:
                print(f"Warning: '{new_name}' already exists, skipping '{old_name}'")
                continue
            
            files_to_rename.append((file_path, new_path, old_name, new_name))
    
    if not files_to_rename:
        print("No files to rename found.")
        return
    
    # Display changes
    print(f"\n{'DRY RUN - ' if dry_run else ''}Found {len(files_to_rename)} file(s) to rename:\n")
    print("-" * 60)
    
    for old_path, new_path, old_name, new_name in files_to_rename:
        print(f"{old_name:<30} => {new_name}")
    
    print("-" * 60)
    
    if not dry_run:
        # Perform actual renaming
        renamed_count = 0
        for old_path, new_path, old_name, new_name in files_to_rename:
            try:
                old_path.rename(new_path)
                renamed_count += 1
            except Exception as e:
                print(f"Error renaming '{old_name}': {e}")
        
        print(f"\nSuccessfully renamed {renamed_count} file(s)")
    else:
        print("\nThis was a dry run. Use --execute to actually rename files.")

def main():
    parser = argparse.ArgumentParser(
        description='Rename audio files to keep only order numbers',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Preview changes (dry run)
  python rename_audio_files.py /path/to/audio/folder
  
  # Actually rename files
  python rename_audio_files.py /path/to/audio/folder --execute
  
  # Use custom pattern to extract order (captures digits after 'track')
  python rename_audio_files.py /path/to/audio/folder --pattern "track(\d+)"
  
  # Common patterns:
  --pattern "(\d+)"           # First number in filename
  --pattern "track(\d+)"      # Number after 'track'
  --pattern "_(\d+)_"         # Number between underscores
  --pattern "^[A-Za-z]+(\d+)" # Number after album name at start
        """
    )
    
    parser.add_argument('directory', 
                       help='Path to directory containing audio files')
    parser.add_argument('--execute', 
                       action='store_true',
                       help='Actually rename files (default is dry run)')
    parser.add_argument('--pattern',
                       help='Regex pattern to extract order number (first capture group)')
    
    args = parser.parse_args()
    
    rename_files(args.directory, dry_run=not args.execute, pattern=args.pattern)

if __name__ == "__main__":
    main()