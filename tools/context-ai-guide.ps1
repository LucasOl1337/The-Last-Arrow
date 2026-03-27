param(
    [Parameter(Position = 0)]
    [ValidateSet('new-entry', 'refresh-current')]
    [string]$Command = 'new-entry',

    [string]$Slug = 'session',
    [string]$Title = 'context update',
    [ValidateSet('Daily', 'Packages')]
    [string]$Category = 'Daily',
    [string]$EntryRelativePath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$guideRoot = Join-Path $repoRoot 'Docs\ContextAndAiGuide'
$dailyRoot = Join-Path $guideRoot 'Daily'
$packagesRoot = Join-Path $guideRoot 'Packages'
$indexPath = Join-Path $guideRoot 'INDEX.md'
$currentPath = Join-Path $guideRoot 'CURRENT_CONTEXT.md'
$templatePath = Join-Path $guideRoot 'Templates\Daily-Update-Template.md'
$today = Get-Date -Format 'yyyy-MM-dd'

function Ensure-Directory([string]$Path)
{
    if (-not (Test-Path $Path))
    {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Normalize-Slug([string]$Value)
{
    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return 'session'
    }

    $normalized = $Value.Trim().ToLowerInvariant()
    $normalized = $normalized -replace '[^a-z0-9]+', '-'
    $normalized = $normalized.Trim('-')
    return [string]::IsNullOrWhiteSpace($normalized) ? 'session' : $normalized
}

function Ensure-IndexFile()
{
    if (-not (Test-Path $indexPath))
    {
        Set-Content -Path $indexPath -Value "# Index`r`n" -Encoding UTF8
    }
}

function Add-IndexEntry([string]$RelativePath, [string]$Summary)
{
    Ensure-IndexFile
    $entryLine = "- ``$today`` ``$RelativePath`` ``$Summary``"
    $content = Get-Content -Path $indexPath -Raw
    if ($content -notmatch [regex]::Escape($RelativePath))
    {
        $lines = Get-Content -Path $indexPath
        $header = @($lines | Select-Object -First 1)
        $rest = @($lines | Select-Object -Skip 1)
        $newLines = @($header + $entryLine + $rest)
        Set-Content -Path $indexPath -Value $newLines -Encoding UTF8
    }
}

function Refresh-Current([string]$RelativePath)
{
    $content = @(
        '# Current Context',
        '',
        "Ultima atualizacao: ``$today``",
        '',
        'Entrada mais recente:',
        "- ``$RelativePath``",
        '',
        'Pacote principal em aberto:',
        '- atualizar manualmente se houver novo pacote principal',
        '',
        'Estado atual:',
        '- registrar resumo curto da sessao atual',
        '',
        'Blocos de trabalho ativos:',
        '- atualizar manualmente',
        '',
        'Bloqueios conhecidos:',
        '- atualizar manualmente',
        '',
        'Proximos passos:',
        '- atualizar manualmente'
    )
    Set-Content -Path $currentPath -Value $content -Encoding UTF8
}

Ensure-Directory $guideRoot
Ensure-Directory $dailyRoot
Ensure-Directory $packagesRoot

switch ($Command)
{
    'new-entry'
    {
        $normalizedSlug = Normalize-Slug $Slug
        $targetRoot = if ($Category -eq 'Packages') { $packagesRoot } else { $dailyRoot }
        $relativeRoot = if ($Category -eq 'Packages') { 'Docs/ContextAndAiGuide/Packages' } else { 'Docs/ContextAndAiGuide/Daily' }
        $fileName = "$today-$normalizedSlug.md"
        $targetPath = Join-Path $targetRoot $fileName
        $relativePath = "$relativeRoot/$fileName"

        if (-not (Test-Path $targetPath))
        {
            $header = "# $today $Title"
            if (Test-Path $templatePath)
            {
                $templateLines = Get-Content -Path $templatePath
                $templateLines[0] = $header
                Set-Content -Path $targetPath -Value $templateLines -Encoding UTF8
            }
            else
            {
                Set-Content -Path $targetPath -Value @($header, '', '## Objective', '- ') -Encoding UTF8
            }
        }

        Add-IndexEntry -RelativePath $relativePath -Summary $Title
        if ($Category -eq 'Daily')
        {
            Refresh-Current -RelativePath $relativePath
        }

        Write-Output $targetPath
    }
    'refresh-current'
    {
        if ([string]::IsNullOrWhiteSpace($EntryRelativePath))
        {
            throw 'Use -EntryRelativePath para informar o arquivo mais recente.'
        }

        Refresh-Current -RelativePath $EntryRelativePath
        Write-Output $currentPath
    }
}
