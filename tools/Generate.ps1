properties {
    $src = '.'
	$configuration = 'Debug'
}

task GenerateNugetPackage -depends SetVersionNumberFromCoreNugetPackage, UpdateUcommerceCoreDependencyVersionInNuspecFile, UpdateAssemblyInfo, CopyPaymentsDll, GenerateNugetBasedOnNuspec{
}

# Copies the Ucommerce.Transactions.Payments.dll to a folder from where it will be included in the NuGet package.
task CopyPaymentsDll{
    $scriptPath = Split-Path -parent $PSCommandPath 

    $dllSourcePath = "$scriptPath\..\src\UCommerce.Transactions.Payments\bin\$configuration\UCommerce.Transactions.Payments.dll"
    $dllTargetPath = "$scriptPath\Ucommerce.Transactions.Payments\Ucommerce.Payments"
    Copy-Item -Path "$dllSourcePath" -Destination "$dllTargetPath" -Force
}

# Load Get-FolderItem script used below in UpdateAssemblyInfo.
. .\Get-FolderItem.ps1

# Generates the actual NuGet package based on the nuspec file.
task GenerateNugetBasedOnNuspec{
    .\NuGet.exe pack .\Ucommerce.Transactions.Payments\Ucommerce.Transactions.Payments.nuspec
}

# Reads the assembly version number from the uCommerce.Core package (Ucommerce.Admin.dll file) to be used as version number for the Ucommerce.Transactions.Payments.dll.
task SetVersionNumberFromCoreNugetPackage {
	Push-Location "."

	$folderItem = Get-ChildItem uCommerce.Core.* 

	Push-Location "..\src\packages"

	$info = Get-ChildItem -Filter UCommerce.Admin.dll -Recurse | Select-Object -ExpandProperty VersionInfo

	$script:version = $info.FileVersion

	Pop-Location
}


task UpdateUcommerceCoreDependencyVersionInNuspecFile {
	$nuspecFilePath = "$src\..\tools\Ucommerce.Transactions.Payments\Ucommerce.Transactions.Payments.nuspec";

	$nuspecFileText = Get-Content $nuspecFilePath;
	$newNuspecFileText = $nuspecFileText -replace "<version>.*</version>","<version>$script:version</version>";
	$newNuspecFileText = $newNuspecFileText -replace "<dependency id='uCommerce\.Core' version='.*?'\/>","<dependency id='uCommerce.Core' version='[$script:version]'/>";
	$newNuspecFileText > $nuspecFilePath
}

# Updates the assembly info.
task UpdateAssemblyInfo -description "Updates the AssemblyInfo.cs file." {
        Push-Location $src

		$hgChangeSetHash = hg identify
		
        $assemblyVersionPattern = 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)'
        $fileVersionPattern = 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)'
	$assemblyInformationalVersionPattern = 'AssemblyInformationalVersion\("[0-9]+(\.([0-9]+|\*)){1,3} .*\)'
        #$cmsVersionPattern = 'CompiledFor\("\w*"\)'
        $assemblyVersion = 'AssemblyVersion("' + $script:version + '")';
        $fileVersion = 'AssemblyFileVersion("' + $script:version + '")';
	$assemblyInformationalVersion = 'AssemblyInformationalVersion("' + $script:version + ' ' + $hgChangeSetHash + '")';

        Get-FolderItem -Path $src -Filter AssemblyInfo.cs | ForEach-Object {
            $filename = $_.FullName
            $filename + ' -> ' + $script:version
        
            # If you are using a source control that requires to check-out files before 
            # modifying them, make sure to check-out the file here.
            # For example, TFS will require the following command:
            # tf checkout $filename
            (Get-Content $filename) | ForEach-Object {
                % {$_ -replace $assemblyVersionPattern, $assemblyVersion } |
                % {$_ -replace $fileVersionPattern, $fileVersion } |
                % {$_ -replace $assemblyInformationalVersionPattern, $assemblyInformationalVersion }
            } | Set-Content $filename
        }

        $clientDependencyVersionPattern = 'version="[0-9]+"';
		$clientDependencyVersion = 'version="' + $script:versionDateNumberPart + '"';
        
        Pop-Location
    
}