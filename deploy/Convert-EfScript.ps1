<#
.SYNOPSIS
  Makes an EF Core generated migration script safe to run with sqlcmd: raw-SQL statements are wrapped in EXEC(N'...').

.DESCRIPTION
  `dotnet ef migrations script` emits a migration's raw `migrationBuilder.Sql(...)` text in the SAME batch as the preceding
  `ALTER TABLE ... ADD [col]`. SQL Server resolves columns of existing tables when the batch is COMPILED, so a raw UPDATE / CHECK
  constraint that references the new column fails with "Invalid column name" (observed on this project's StoreRequisitionLineTraceability
  and OrganizationPositionHierarchy migrations). Wrapping the statement in EXEC(N'...') defers compilation to execution time; the
  semantics are identical (single quotes are doubled).

  Only statements inside EF's `IF NOT EXISTS (history) BEGIN ... END;` blocks that START with one of the raw-SQL shapes below are touched;
  everything else is copied byte for byte. The script writes the converted file and prints how many statements were wrapped.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputFile,
    [Parameter(Mandatory)][string]$OutputFile
)
$ErrorActionPreference = 'Stop'
$text = [System.IO.File]::ReadAllText((Resolve-Path $InputFile).Path)
$rawShape = '^(UPDATE\s|DELETE\s+FROM\s|ALTER\s+TABLE\s+\w+\s+WITH\s+(NO)?CHECK\s|IF\s+NOT\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+\w+\s+WHERE\s+NOT\s*\()'   # unbracketed names / WITH CHECK = hand-written SQL, not EF-generated DDL
$script:wrapped = 0
$evaluator = [System.Text.RegularExpressions.MatchEvaluator]{
    param($m)
    $body = $m.Groups[2].Value.Trim()
    if ($body -notmatch $rawShape -or $body -match '^EXEC\s*\(') { return $m.Value }
    $script:wrapped++
    return $m.Groups[1].Value + "EXEC(N'" + $body.Replace("'", "''") + "');" + $m.Groups[3].Value
}
$out = [regex]::Replace($text, '(?ms)^(BEGIN\s*\r?\n\s*)(.*?)(\s*\r?\nEND;)', $evaluator)
[System.IO.File]::WriteAllText($OutputFile, $out, (New-Object System.Text.UTF8Encoding($true)))
Write-Host "Wrapped $($script:wrapped) raw-SQL statement(s) in EXEC(N'...') -> $OutputFile"
