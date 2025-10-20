#!/usr/bin/env python3
"""
MainViewModel Cleanup Script
Removes ~700 lines of artifact-related code that has been moved to ArtifactViewModel.
"""

import re

file_path = r"c:\Users\sajaya\source\oras-desktop\OrasProject.OrasDesktop\ViewModels\MainViewModel.cs"

# Read the file
with open(file_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Original file: {len(lines)} lines")

# Track which lines to remove
lines_to_remove = set()

# 1. Remove event handler subscriptions (lines 163-167)
for i in range(len(lines)):
    line = lines[i]
    if 'DigestContextMenu.ManifestRequested +=' in line:
        lines_to_remove.add(i)
        if i > 0 and '// Wire up DigestContextMenu' in lines[i-1]:
            lines_to_remove.add(i-1)  # Remove comment too
    elif 'ReferrerNodeContextMenu.ManifestRequested +=' in line:
        lines_to_remove.add(i)
        if i > 0 and '// Wire up ReferrerNodeContextMenu' in lines[i-1]:
            lines_to_remove.add(i-1)  # Remove comment too

# 2. Find and mark LoadReferrersAsync method for removal (~lines 461-527)
in_load_referrers = False
load_referrers_start = None
for i in range(len(lines)):
    if 'private async Task LoadReferrersAsync(' in lines[i]:
        in_load_referrers = True
        load_referrers_start = i
    elif in_load_referrers and lines[i].strip().startswith('private ') and 'LoadReferrersAsync' not in lines[i]:
        # Found next method, mark everything up to here
        for j in range(load_referrers_start, i):
            lines_to_remove.add(j)
        in_load_referrers = False
        print(f"Marked LoadReferrersAsync for removal: lines {load_referrers_start+1} to {i}")
        break

# 3. Find and mark DeleteManifestAsync method for removal
in_delete_manifest = False
delete_manifest_start = None
for i in range(len(lines)):
    if 'private async Task DeleteManifestAsync()' in lines[i]:
        in_delete_manifest = True
        delete_manifest_start = i
    elif in_delete_manifest and lines[i].strip().startswith('private ') and 'DeleteManifestAsync' not in lines[i]:
        # Found next method
        for j in range(delete_manifest_start, i):
            lines_to_remove.add(j)
        in_delete_manifest = False
        print(f"Marked DeleteManifestAsync for removal: lines {delete_manifest_start+1} to {i}")
        break

# 4. Find and mark CopyReferenceWithTagAsync method for removal
in_copy_tag = False
copy_tag_start = None
for i in range(len(lines)):
    if 'private async Task CopyReferenceWithTagAsync()' in lines[i]:
        in_copy_tag = True
        copy_tag_start = i
    elif in_copy_tag and lines[i].strip().startswith('private ') and 'CopyReferenceWithTagAsync' not in lines[i]:
        for j in range(copy_tag_start, i):
            lines_to_remove.add(j)
        in_copy_tag = False
        print(f"Marked CopyReferenceWithTagAsync for removal: lines {copy_tag_start+1} to {i}")
        break

# 5. Find and mark CopyReferenceWithDigestAsync method for removal
in_copy_digest = False
copy_digest_start = None
for i in range(len(lines)):
    if 'private async Task CopyReferenceWithDigestAsync()' in lines[i]:
        in_copy_digest = True
        copy_digest_start = i
    elif in_copy_digest and lines[i].strip().startswith('private ') and 'CopyReferenceWithDigestAsync' not in lines[i]:
        for j in range(copy_digest_start, i):
            lines_to_remove.add(j)
        in_copy_digest = False
        print(f"Marked CopyReferenceWithDigestAsync for removal: lines {copy_digest_start+1} to {i}")
        break

# 6. Find and mark ViewPlatformManifestAsync method for removal
in_view_platform = False
view_platform_start = None
for i in range(len(lines)):
    if 'private async Task ViewPlatformManifestAsync(' in lines[i]:
        in_view_platform = True
        view_platform_start = i
    elif in_view_platform and lines[i].strip().startswith('private ') and 'ViewPlatformManifestAsync' not in lines[i]:
        for j in range(view_platform_start, i):
            lines_to_remove.add(j)
        in_view_platform = False
        print(f"Marked ViewPlatformManifestAsync for removal: lines {view_platform_start+1} to {i}")
        break

# 7. Find and mark UpdateReferrerNodeContextMenu method for removal
in_update_context = False
update_context_start = None
for i in range(len(lines)):
    if 'private void UpdateReferrerNodeContextMenu()' in lines[i]:
        in_update_context = True
        update_context_start = i
    elif in_update_context and (lines[i].strip().startswith('private ') or lines[i].strip().startswith('public ')) and 'UpdateReferrerNodeContextMenu' not in lines[i]:
        for j in range(update_context_start, i):
            lines_to_remove.add(j)
        in_update_context = False
        print(f"Marked UpdateReferrerNodeContextMenu for removal: lines {update_context_start+1} to {i}")
        break

# 8. Find and mark OnDigestManifestRequested handler for removal
in_digest_handler = False
digest_handler_start = None
for i in range(len(lines)):
    if 'private void OnDigestManifestRequested(object? sender' in lines[i]:
        in_digest_handler = True
        digest_handler_start = i
    elif in_digest_handler and (lines[i].strip().startswith('private ') or lines[i].strip().startswith('public ')) and 'OnDigestManifestRequested' not in lines[i]:
        for j in range(digest_handler_start, i):
            lines_to_remove.add(j)
        in_digest_handler = False
        print(f"Marked OnDigestManifestRequested for removal: lines {digest_handler_start+1} to {i}")
        break

# 9. Remove event handler code within OnManifestLoadCompleted that updates artifact properties
# We'll fix specific sections rather than remove the whole method
# Lines with ManifestContent, ManifestViewer, DigestContextMenu, LoadReferrersAsync calls

print(f"\nTotal lines to remove: {len(lines_to_remove)}")

# Create new file content
new_lines = [lines[i] for i in range(len(lines)) if i not in lines_to_remove]

print(f"New file: {len(new_lines)} lines")
print(f"Removed: {len(lines) - len(new_lines)} lines")

# Write the new file
with open(file_path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("\nSuccessfully cleaned up MainViewModel!")
