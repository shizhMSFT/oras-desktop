"""
Script to replace the artifact panel Grid with ArtifactView component in MainView.axaml
"""

file_path = r'c:\Users\sajaya\source\oras-desktop\OrasProject.OrasDesktop\Views\MainView.axaml'

# Read all lines
with open(file_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find the start and end of the artifact panel Grid
start_line = None
end_line = None

for i, line in enumerate(lines):
    # Look for the artifact panel Grid (around line 59)
    if '<!-- Right side: Artifact Details Panel -->' in line:
        start_line = i
    # Look for the closing Grid tag before "Row 2: Reference" (around line 461)
    if start_line is not None and '<!--  Row 2: Reference  -->' in line and i > 60:
        # The closing Grid is 2 lines before this comment
        end_line = i - 2
        break

if start_line is None or end_line is None:
    print(f"Could not find markers. Start: {start_line}, End: {end_line}")
    exit(1)

print(f"Found artifact panel from line {start_line+1} to {end_line+1}")
print(f"Removing {end_line - start_line} lines")

# Create new content
new_lines = []

# Keep everything before the artifact panel
new_lines.extend(lines[:start_line])

# Add the ArtifactView component
new_lines.append('      <!-- Right side: Artifact Details Panel -->\n')
new_lines.append('      <views:ArtifactView\n')
new_lines.append('        Grid.Column="1"\n')
new_lines.append('        Margin="0,10,10,0"\n')
new_lines.append('        DataContext="{Binding Artifact}" />\n')

# Keep everything after the artifact panel
new_lines.extend(lines[end_line+1:])

# Write the file
with open(file_path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print(f"Successfully replaced artifact panel!")
print(f"Old file: {len(lines)} lines")
print(f"New file: {len(new_lines)} lines")
print(f"Removed: {len(lines) - len(new_lines)} lines")
