#!/usr/bin/env python3
"""
Generate PNG diagrams from PlantUML files in the activity_diagrams directory.

Usage:
    python generate_all_diagrams.py                    # Generate all diagrams
    python generate_all_diagrams.py UC5*.puml          # Generate specific diagram(s)
    python generate_all_diagrams.py UC1*.puml UC5*.puml  # Generate multiple specific diagrams
"""

import plantuml
import os
import glob
import sys

def generate_all_activity_diagrams(file_patterns=None):
    """Generate PNGs from PlantUML files.
    
    Args:
        file_patterns: List of file patterns to process. If None, processes all .puml files.
    """
    
    # Create PlantUML object (uses public PlantUML server)
    pl = plantuml.PlantUML(url='http://www.plantuml.com/plantuml/img/')
    
    # Get .puml files to process
    if file_patterns:
        # Process specified files/patterns
        puml_files = []
        for pattern in file_patterns:
            # If pattern doesn't have .puml extension, add it
            if not pattern.endswith('.puml'):
                pattern = pattern if '*' in pattern else f"{pattern}*.puml"
            matched = glob.glob(pattern)
            puml_files.extend(matched)
        puml_files = sorted(set(puml_files))  # Remove duplicates and sort
    else:
        # Process all .puml files in current directory
        puml_files = sorted(glob.glob("*.puml"))
    
    if not puml_files:
        print("No .puml files found!")
        if file_patterns:
            print(f"Searched for: {', '.join(file_patterns)}")
        return
    
    print(f"Found {len(puml_files)} PlantUML files to process\n")
    
    success_count = 0
    failed_count = 0
    
    for puml_file in puml_files:
        output_file = puml_file.replace('.puml', '.png')
        
        try:
            pl.processes_file(puml_file, outfile=output_file)
            print(f"✓ Generated: {output_file}")
            success_count += 1
        except Exception as e:
            print(f"✗ Failed: {puml_file} - {e}")
            failed_count += 1
    
    print(f"\n{'='*50}")
    print(f"Summary: {success_count} succeeded, {failed_count} failed")
    print(f"{'='*50}")

if __name__ == "__main__":
    print("Generating PNG diagrams from PlantUML files...")
    print(f"{'='*50}\n")
    
    # Get file patterns from command line arguments (if any)
    file_patterns = sys.argv[1:] if len(sys.argv) > 1 else None
    
    if file_patterns:
        print(f"Processing specified files: {', '.join(file_patterns)}\n")
    else:
        print("Processing all .puml files in current directory\n")
    
    generate_all_activity_diagrams(file_patterns)
