import re

with open(r'c:\Users\sajaya\source\oras-desktop\OrasProject.OrasDesktop\Views\MainView.axaml', 'r', encoding='utf-8') as f:
    content = f.read()

# Find and replace the artifact Grid with ArtifactView  
# Match from the comment to the closing Grid tag
pattern = r'(      <!-- Right side: Artifact Details Panel -->\s*)<Grid\s+Grid\.Column="1".*?      </Grid>(\s+</Grid>\s+<!--  Row 2: Reference  -->)'
replacement = r'\1<views:ArtifactView\n        Grid.Column="1"\n        Margin="0,10,10,0"\n        DataContext="{Binding Artifact}" />\2'

new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)

with open(r'c:\Users\sajaya\source\oras-desktop\OrasProject.OrasDesktop\Views\MainView.axaml', 'w', encoding='utf-8', newline='\n') as f:
    f.write(new_content)

print('Replacement complete!')
