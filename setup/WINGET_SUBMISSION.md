# WinGet Package Submission Guide

This directory contains the manifests needed to submit **LightCrosshair v1.3.0** to the official Windows Package Manager repository.

## Prerequisites

1. **GitHub Account** — You need a GitHub account with contributor access to submit PRs
2. **WinGet CLI** (optional) — For local validation before submission
3. **Fork of microsoft/winget-pkgs** — Create your own fork at https://github.com/microsoft/winget-pkgs

## Submission Steps

### Step 1: Fork microsoft/winget-pkgs

1. Visit: https://github.com/microsoft/winget-pkgs
2. Click **"Fork"** in the top-right corner
3. Complete the fork creation

### Step 2: Prepare Your Local Repository

```bash
# Clone your fork (replace YOUR_USERNAME)
git clone https://github.com/YOUR_USERNAME/winget-pkgs.git
cd winget-pkgs

# Add upstream remote for syncing
git remote add upstream https://github.com/microsoft/winget-pkgs.git

# Sync with main branch
git fetch upstream
git checkout main
git merge upstream/main
```

### Step 3: Copy Manifests to Your Fork

Copy the entire `setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.3.0/` directory structure into your fork:

```bash
# From LightCrosshair repository root:
# Copy the manifests/p/PrimeBuild directory tree

# Destination in your forked winget-pkgs:
# manifests/p/PrimeBuild/LightCrosshair/1.3.0/
#   ├── PrimeBuild.LightCrosshair.yaml
#   ├── PrimeBuild.LightCrosshair.installer.yaml
#   └── PrimeBuild.LightCrosshair.locale.en-US.yaml
```

**Or use PowerShell:**

```powershell
# From LightCrosshair repo root
Copy-Item -Path "setup\winget\manifests\p" `
          -Destination "C:\path\to\winget-pkgs\manifests\" `
          -Recurse -Force
```

### Step 4: Validate Manifests (Optional but Recommended)

**Option A: Using WinGet CLI (requires Windows 11 or WinGet pre-release)**

```powershell
winget validate --manifest .\manifests\p\PrimeBuild\LightCrosshair\1.3.0\
```

**Option B: Using Online Validator**

1. Visit: https://winget.azureedge.net/cache/
2. Upload or paste your manifest files to validate syntax

### Step 5: Create a Branch and Commit

```bash
# Create a feature branch
git checkout -b add/lightcrosshair-1.3.0

# Stage the manifests
git add manifests/p/PrimeBuild/LightCrosshair/1.3.0/

# Commit with a descriptive message
git commit -m "Add LightCrosshair v1.3.0

- Package ID: PrimeBuild.LightCrosshair
- Version: 1.3.0
- Installer: Portable executable from GitHub Releases
- Requires: .NET 8.0 Runtime"

# Push to your fork
git push origin add/lightcrosshair-1.3.0
```

### Step 6: Create a Pull Request

1. Visit your forked repository: https://github.com/YOUR_USERNAME/winget-pkgs
2. Click **"Pull Requests"** tab
3. Click **"New Pull Request"**
4. Ensure:
   - Base repository: `microsoft/winget-pkgs` (main branch)
   - Head repository: `YOUR_USERNAME/winget-pkgs` (your feature branch)
5. Click **"Create Pull Request"**
6. Fill in the PR template:
   - **Title**: `Add LightCrosshair v1.3.0`
   - **Description**: Include version info, changelog, installation instructions
   - **Checklist**: Verify that:
     - ✅ Manifests follow the 1.4.0 schema
     - ✅ SHA256 checksum is valid
     - ✅ URL is publicly accessible
     - ✅ No breaking changes

### Step 7: Wait for Review

- Microsoft maintainers will review your PR (typically 24-72 hours)
- They may request changes (common issues: formatting, validation errors, missing fields)
- Once approved, they will merge the PR
- Your package becomes available in WinGet within a few hours

## Installation (After Approval)

Once merged into microsoft/winget-pkgs:

```bash
winget install PrimeBuild.LightCrosshair
# or
winget install LightCrosshair
```

## Updating to Future Versions

For **v1.3.1** or later, simply:

1. Create a new directory: `manifests/p/PrimeBuild/LightCrosshair/{NEW_VERSION}/`
2. Copy the 3 manifest files
3. Update version numbers and checksums
4. Submit a new PR

## Troubleshooting

### Manifest Validation Errors
- Check YAML syntax using an online validator
- Ensure proper indentation (2 spaces, no tabs)
- Verify all required fields are present

### Checksum Mismatch
- Recalculate SHA256 of the executable:
  ```powershell
  (Get-FileHash -Path "LightCrosshair.exe" -Algorithm SHA256).Hash
  ```
- Update all manifest files with the correct checksum

### URL Not Accessible
- Verify the GitHub Releases URL is public
- Ensure the release is published (not draft)

## More Information

- [WinGet Documentation](https://learn.microsoft.com/en-us/windows/package-manager/)
- [microsoft/winget-pkgs Contributing Guide](https://github.com/microsoft/winget-pkgs/blob/master/CONTRIBUTING.md)
- [Manifest Schema Reference](https://github.com/microsoft/winget-cli/tree/master/schemas)
