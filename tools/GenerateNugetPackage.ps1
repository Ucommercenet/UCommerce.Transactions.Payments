[CmdletBinding()]
Param(
    [Parameter(Mandatory=$False)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

function Get-ScriptDirectory { 
    Split-Path -parent $PSCommandPath 
}

# Assigns variables and calles "GenerateNugetPackage" from Generate.ps1.
function Run-It () {
	$scriptPath = Get-ScriptDirectory;
	$base_dir = Resolve-Path "$scriptPath"
    $src = Resolve-Path "$scriptPath\..\src";

	Import-Module "$scriptPath\psake\4.3.0.0\psake.psm1"
 
     $properties = @{
         "src"=$src;
		 "configuration" = $Configuration;
     };

	Invoke-PSake "$scriptPath\Generate.ps1" GenerateNugetPackage -properties $properties
}

Run-It