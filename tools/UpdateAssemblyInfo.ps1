[CmdletBinding()]
Param(
	[Parameter(Mandatory=$true, HelpMessage="Valid version number.")]
	[ValidatePattern("^[0-9]{1,5}(\.[0-9]{1,5}){2}$")]
	[string]$version = "Version"
)

. ..\tools\Get-FolderItem.ps1

function Run-It () {
	$assemblyVersionPattern = 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)'
    $fileVersionPattern = 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)'
	$assemblyInformationalVersionPattern = 'AssemblyInformationalVersion\("[0-9]+(\.([0-9]+|\*)){1,3} .*\)'
	$versionDateNumberPart = (Get-Date).Year.ToString().Substring(2) + "" + (Get-Date).DayOfYear.ToString("000");
	$version = "$version." + $versionDateNumberPart;


	$scriptPath = Split-Path -parent $PSCommandPath;
	$src = Resolve-Path "$scriptPath\..\src";


	$assemblyVersion = 'AssemblyVersion("' + $version + '")';
    $fileVersion = 'AssemblyFileVersion("' + $version + '")';
	$assemblyInformationalVersion = 'AssemblyInformationalVersion("' + $version + '")';

	 Get-FolderItem -Path $src -Filter AssemblyInfo.cs | ForEach-Object {
            $filename = $_.FullName
            $filename + ' -> ' + $version

		 (Get-Content $filename) | ForEach-Object {
                % {$_ -replace $assemblyVersionPattern, $assemblyVersion } |
                % {$_ -replace $fileVersionPattern, $fileVersion } |
                % {$_ -replace $assemblyInformationalVersionPattern, $assemblyInformationalVersion }
            } | Set-Content $filename
	}
}

Run-It