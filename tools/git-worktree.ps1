param(
    [ValidateSet("help", "list", "path", "open", "create", "remove")]
    [string]$Command = "help",
    [string]$Branch,
    [string]$Base = "main",
    [switch]$DeleteBranch,
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw "Execute este script dentro de um repositorio Git."
    }

    return $root.Trim()
}

function Get-WorktreeRoot([string]$repoRoot) {
    $repoName = Split-Path $repoRoot -Leaf
    $parent = Split-Path $repoRoot -Parent
    return Join-Path $parent ($repoName + ".worktrees")
}

function Resolve-WorktreeName([string]$branchName) {
    if ([string]::IsNullOrWhiteSpace($branchName)) {
        throw "Informe -Branch."
    }

    return ($branchName.Trim() -replace "[\\/]+", "-")
}

function Resolve-WorktreePath([string]$repoRoot, [string]$branchName) {
    $worktreeRoot = Get-WorktreeRoot $repoRoot
    return Join-Path $worktreeRoot (Resolve-WorktreeName $branchName)
}

function Assert-CleanWorktree {
    param(
        [string]$repoRoot,
        [switch]$AllowDirty
    )

    if ($AllowDirty) {
        return
    }

    $status = git -C $repoRoot status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Nao foi possivel ler o status do repositorio."
    }

    if (-not [string]::IsNullOrWhiteSpace(($status | Out-String))) {
        throw "Worktree atual possui mudancas pendentes. Limpe ou commit/stash antes de criar outra worktree."
    }
}

function Assert-BranchPrefix([string]$branchName) {
    if ($branchName -notmatch '^(feature|fix|spike)/') {
        throw "Use prefixo de branch feature/, fix/ ou spike/."
    }
}

function Show-Help {
    @'
git-worktree.ps1

Comandos:
  .\tools\git-worktree.ps1 list
  .\tools\git-worktree.ps1 path   -Branch feature/nome
  .\tools\git-worktree.ps1 open   -Branch feature/nome
  .\tools\git-worktree.ps1 create -Branch feature/nome [-Base main]
  .\tools\git-worktree.ps1 remove -Branch feature/nome [-DeleteBranch]

Regras:
  - cria worktrees em uma pasta irma: ../<repo>.worktrees/<branch>
  - bloqueia criacao quando a worktree atual esta suja
  - exige prefixo feature/, fix/ ou spike/
'@
}

$repoRoot = Get-RepoRoot
$worktreePath = if ($Branch) { Resolve-WorktreePath $repoRoot $Branch } else { $null }

switch ($Command) {
    "help" {
        Show-Help
    }
    "list" {
        git -C $repoRoot worktree list
        if ($LASTEXITCODE -ne 0) {
            throw "Nao foi possivel listar worktrees."
        }
    }
    "path" {
        if (-not $worktreePath) {
            throw "Informe -Branch para resolver o caminho."
        }

        $worktreePath
    }
    "open" {
        if (-not $worktreePath) {
            throw "Informe -Branch para abrir a worktree."
        }

        if (-not (Test-Path $worktreePath)) {
            throw "Worktree nao encontrada em $worktreePath"
        }

        Invoke-Item $worktreePath
    }
    "create" {
        if (-not $worktreePath) {
            throw "Informe -Branch para criar a worktree."
        }

        Assert-BranchPrefix $Branch
        Assert-CleanWorktree -repoRoot $repoRoot -AllowDirty:$AllowDirty

        if (Test-Path $worktreePath) {
            throw "Ja existe uma worktree em $worktreePath"
        }

        New-Item -ItemType Directory -Path (Split-Path $worktreePath -Parent) -Force | Out-Null
        $existingBranch = git -C $repoRoot branch --list $Branch
        if ($LASTEXITCODE -ne 0) {
            throw "Nao foi possivel consultar a branch $Branch"
        }

        if ([string]::IsNullOrWhiteSpace(($existingBranch | Out-String))) {
            git -C $repoRoot worktree add $worktreePath -b $Branch $Base
        }
        else {
            git -C $repoRoot worktree add $worktreePath $Branch
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Falha ao criar a worktree."
        }

        Write-Output "Worktree criada em $worktreePath"
    }
    "remove" {
        if (-not $worktreePath) {
            throw "Informe -Branch para remover a worktree."
        }

        if (-not (Test-Path $worktreePath)) {
            throw "Worktree nao encontrada em $worktreePath"
        }

        git -C $repoRoot worktree remove $worktreePath
        if ($LASTEXITCODE -ne 0) {
            throw "Falha ao remover a worktree."
        }

        if ($DeleteBranch) {
            git -C $repoRoot branch -D $Branch
            if ($LASTEXITCODE -ne 0) {
                throw "Worktree removida, mas falhou ao apagar a branch $Branch."
            }
        }

        Write-Output "Worktree removida: $worktreePath"
    }
}
