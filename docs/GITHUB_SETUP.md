# GitHub repository setup

Recommended repository name:

```text
GuideVault-LaunchBox-Connector
```

Recommended description:

```text
LaunchBox plugin for connecting games to GuideVault manuals, strategy guides, and magazines.
```

Keep this plugin in its own repository. It has different build dependencies, install paths, and release packaging than the GuideVault server.

## Create the repository

Recommended GitHub options:

```text
Visibility: Public
README: No, if this source package already contains README.md
.gitignore: No, if this source package already contains .gitignore
License: MIT, unless you prefer a different license
```

If you let GitHub create the license file, pull the starter commit before pushing your local source.

## Push source

From the extracted repository folder:

```powershell
git init
git branch -M main
git add .
git commit -m "Initial GuideVault LaunchBox Connector 0.4.22 release"
git remote add origin https://github.com/<your-github-user>/GuideVault-LaunchBox-Connector.git
git push -u origin main
```

If the remote already has a README or license commit:

```powershell
git fetch origin
git pull origin main --allow-unrelated-histories --no-rebase
git push -u origin main
```

## Build a release package

Build/package on a Windows machine with LaunchBox installed:

```powershell
.\scripts\Package-PluginRelease.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox"
```

Attach the generated zip from `dist` to a GitHub release named:

```text
v0.4.22
```

## Dependency note

The repository cannot build in a normal GitHub Actions runner unless the LaunchBox plugin API DLL is supplied during the workflow. The local build script uses the DLL from the installed LaunchBox folder.
