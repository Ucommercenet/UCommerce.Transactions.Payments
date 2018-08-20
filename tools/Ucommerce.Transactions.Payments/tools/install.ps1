param($installPath, $toolsPath, $package, $project)

Write-Host "installPath:" "${installPath}"
Write-Host "toolsPath:" "${toolsPath}"

Write-Host " "

if ($project) {
	$dateTime = Get-Date -Format yyyyMMdd-HHmmss

	# Create paths and list them
	$projectPath = (Get-Item $project.Properties.Item("FullPath").Value).FullName
	Write-Host "projectPath:" "${projectPath}"
	$backupPath = Join-Path $projectPath "App_Data\NuGetBackup\$dateTime"
	Write-Host "backupPath:" "${backupPath}"
	$copyLogsPath = Join-Path $backupPath "CopyLogs"
	Write-Host "copyLogsPath:" "${copyLogsPath}"	
	$webConfigSource = Join-Path $projectPath "Web.config"
	Write-Host "webConfigSource:" "${webConfigSource}"
	$configFolder = Join-Path $projectPath "Config"
	Write-Host "configFolder:" "${configFolder}"
	
    $binFolderTarget = Join-Path $projectPath "bin"
    $binFolderSource = Join-Path $installPath "Ucommerce.Transactions.Payments"

    robocopy $binFolderSource $binFolderTarget /is /it /e
}