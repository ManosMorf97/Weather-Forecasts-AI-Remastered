#!/usr/bin/env python3
"""
Generate PNG diagram(s) from PlantUML file(s) in the system_design directory.

Usage:
    python generate_diagram.py                # Generate all diagrams
    python generate_diagram.py system_design   # Generate a specific diagram
"""

import plantuml
import glob
import sys


def generate_all_diagrams(file_patterns=None):
    pl = plantuml.PlantUML(url='http://www.plantuml.com/plantuml/img/')

    if file_patterns:
        puml_files = []
        for pattern in file_patterns:
            if not pattern.endswith('.puml'):
                pattern = pattern if '*' in pattern else f"{pattern}*.puml"
            puml_files.extend(glob.glob(pattern))
        puml_files = sorted(set(puml_files))
    else:
        puml_files = sorted(glob.glob("*.puml"))

    if not puml_files:
        print("No .puml files found!")
        return

    for puml_file in puml_files:
        output_file = puml_file.replace('.puml', '.png')
        try:
            pl.processes_file(puml_file, outfile=output_file)
            print(f"Successfully generated: {output_file}")
        except Exception as e:
            print(f"Error generating diagram for {puml_file}: {e}")


if __name__ == "__main__":
    file_patterns = sys.argv[1:] if len(sys.argv) > 1 else None
    generate_all_diagrams(file_patterns)
